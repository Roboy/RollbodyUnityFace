// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// FMOD system created per output device
    /// System for output 0 is also for output devices enumeration and output devices list changes notifications, DEVICELISTCHANGED callback is installed for it
    /// </summary>
    public class FMODSystemOutputDevice
    {
        // ========================================================================================================================================
        #region fmod buffer callbacks
        /// <summary>
        /// An FMOD sound specific audio buffers for static PCM callbacks
        /// </summary>
        public static Dictionary<System.IntPtr, PCMCallbackBuffer> pcmCallbackBuffers = new Dictionary<System.IntPtr, PCMCallbackBuffer>();
        /// <summary>
        /// Make instance cb buffer size some large(r) number in order not to shuffle around much
        /// </summary>
        const int cbuffer_capacity = 100000;
        static object pcm_callback_lock = new object();
        // Callback has to be a static method for IL2CPP/AOT to be able to make the delegate call
        [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMREAD_CALLBACK))]
        static FMOD.RESULT PCMReadCallback(System.IntPtr soundraw, System.IntPtr data, uint datalen)
        {
            lock (FMODSystemOutputDevice.pcm_callback_lock)
            {
                // clear the array first - fix for non running AudioSource
                var zeroArr = new byte[datalen];
                global::System.Runtime.InteropServices.Marshal.Copy(zeroArr, 0, data, (int)datalen);

                // retrieve instance specific buffer
                PCMCallbackBuffer pCMCallbackBuffer = null;
                if (!FMODSystemOutputDevice.pcmCallbackBuffers.TryGetValue(soundraw, out pCMCallbackBuffer))
                {
                    // create pcm buffer with capacity with requested length of the callback
                    pCMCallbackBuffer = new PCMCallbackBuffer(cbuffer_capacity);
                    FMODSystemOutputDevice.pcmCallbackBuffers.Add(soundraw, pCMCallbackBuffer);
                }

                // store few useful statistics
                if (datalen > pCMCallbackBuffer.maxdatalen)
                    pCMCallbackBuffer.maxdatalen = datalen;

                if (datalen < pCMCallbackBuffer.mindatalen && datalen > 0)
                    pCMCallbackBuffer.mindatalen = datalen;

                pCMCallbackBuffer.datalen = datalen;

                // resize instance buffer if needed - FMOD can adjust length of the requested buffer dynamically - in grow direction only
                if (datalen > pCMCallbackBuffer.Capacity)
                {
                    Debug.LogFormat("PCMReadCallback {0} increase change, requested: {1} / capacity: {2}", soundraw, datalen, pCMCallbackBuffer.Capacity);

                    // copy out existing data
                    var copy = pCMCallbackBuffer.Dequeue(pCMCallbackBuffer.Available);

                    // replace instance buffer preserving existing data
                    FMODSystemOutputDevice.pcmCallbackBuffers[soundraw] = null;

                    pCMCallbackBuffer = new PCMCallbackBuffer((int)datalen * 2);

                    // restore existing data
                    pCMCallbackBuffer.Enqueue(copy);

                    FMODSystemOutputDevice.pcmCallbackBuffers[soundraw] = pCMCallbackBuffer;
                }

                // copy out available bytes
                var count_available = pCMCallbackBuffer.Available;
                var count_provide = (int)Mathf.Min(count_available, (int)datalen);

                pCMCallbackBuffer.underflow = count_available < datalen;

                // in case of input buffer underflow there's unfortunately little we can do automatically - OAFR simply can't provide data fast enough - 
                // usually the best course of action is to match # of input and output channels and/or
                // improve Unity audio bandwidth (change DSP Buffer Size in AudioManager) and/or on Windows install low latency drivers

                if (pCMCallbackBuffer.underflow)
                {
                    // .. don't spam the console if playback hasn't even started yet
                    if (count_available > 0)
                        Debug.LogFormat("PCMReadCallback {0} underflow - requested {1} while having {2} (providing {3}),  time: {4}", soundraw, datalen, count_available, count_provide, AudioSettings.dspTime);
                }

                var audioArr = pCMCallbackBuffer.Dequeue(count_provide);
                global::System.Runtime.InteropServices.Marshal.Copy(audioArr, 0, data, audioArr.Length);

                return FMOD.RESULT.OK;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMSETPOS_CALLBACK))]
        static FMOD.RESULT PCMSetPosCallback(System.IntPtr soundraw, int subsound, uint position, FMOD.TIMEUNIT postype)
        {
            /*
            Debug.LogFormat("PCMSetPosCallback sound {0}, subsound {1} requesting position {2}, postype {3}, time: {4}"
                , soundraw
                , subsound
                , position
                , postype
                , AudioSettings.dspTime
                );
                */
            return FMOD.RESULT.OK;
        }
        /// <summary>
        /// Mainly for displaying some stats
        /// </summary>
        /// <returns></returns>
        public static PCMCallbackBuffer PCMCallbackBuffer(FMOD.Sound sound)
        {
            if (!FMODSystemOutputDevice.pcmCallbackBuffers.ContainsKey(sound.handle))
                return null;

            return FMODSystemOutputDevice.pcmCallbackBuffers[sound.handle];
        }
        #endregion
        // ========================================================================================================================================
        #region FMOD system - output device + proxy sound for AudioSource
        public readonly int OutputDeviceID;
        public readonly FMOD.System System;
        public readonly string VersionString;
        public readonly uint DSPBufferLength;
        public readonly int DSPBufferCount;
        /// <summary>
        /// FMOD's sytem handle (contrary to sound handle it seems) is completely unreliable / e.g. clearing it via .clearHandle() has no effect in following check for !null/hasHandle() /
        /// Use this pointer copied after creation as release/functionality guard instead
        /// </summary>
        public System.IntPtr SystemHandle = global::System.IntPtr.Zero;

        FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.SOUND_PCMREAD_CALLBACK pcmreadcallback;
        FMOD.SOUND_PCMSETPOS_CALLBACK pcmsetposcallback;
        /// <summary>
        /// Sounds automatically created for buffer redirection (more than one component might be using the same system)
        /// and user sounds played by this system
        /// Used for tracking playing sounds in StopSound for automatic system release
        /// </summary>
        readonly List<FMOD.Sound> sounds = new List<FMOD.Sound>();
        /// <summary>
        /// Creates system and calls setDriver on it
        /// Optionally installs notification callback if not already
        /// </summary>
        /// <param name="forOutputDevice"></param>
        /// <param name="withSpeakerMode"></param>
        /// <param name="withNumOfRawSpeakers"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public FMODSystemOutputDevice(int forOutputDevice
            , FMOD.SPEAKERMODE withSpeakerMode
            , int withNumOfRawSpeakers
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            this.System = default(FMOD.System);
            this.VersionString = string.Empty;
            this.DSPBufferLength = 0;
            this.DSPBufferCount = 0;
            this.isNotificationSystem = false;

            /*
             * create component sound system (tm)
             */
            uint version = 0;

            result = FMOD.Factory.System_Create(out this.System);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "FMOD.Factory.System_Create");

            result = this.System.getVersion(out version);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.getVersion");

            if (version < FMOD.VERSION.number)
                AudioStreamSupport.ERRCHECK(FMOD.RESULT.ERR_HEADER_MISMATCH, logLevel, gameObjectName, onError, "version < FMOD.VERSION.number");

            /*
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = global::System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.VersionString = string.Format("{0}.{1}.{2}", global::System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

            /*
             * Use default settings for selected output device (leave input/output channels handling and resampling to fmod, which should do right thing based on callback/extinfo
             * - if not requested by user
             * (setSoftwareFormat and setDSPBufferSize must be called before init)
             */
            int od_namelen = 255;
            string od_name;
            System.Guid od_guid;
            int od_systemrate;
            FMOD.SPEAKERMODE od_speakermode;
            int od_speakermodechannels;

            result = this.System.getDriverInfo(forOutputDevice, out od_name, od_namelen, out od_guid, out od_systemrate, out od_speakermode, out od_speakermodechannels);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.getDriverInfo");

            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Output device {0} info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", forOutputDevice, od_systemrate, od_speakermode, od_speakermodechannels);

            if (withSpeakerMode != FMOD.SPEAKERMODE.DEFAULT)
            {
                // TODO: Allow to change samplerate ?
                this.System.setSoftwareFormat(od_systemrate, withSpeakerMode, withNumOfRawSpeakers);
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.setSoftwareFormat");

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Output device {0} user override: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", forOutputDevice, od_systemrate, withSpeakerMode, withNumOfRawSpeakers);
            }

            /*
             * Adjust DSP buffers if requested by user
             */
            // TODO: reenable if DSP buffer settings are exposed again per system
            /*
            if (withCustomDSPBufferLength.HasValue && withCustomDSPBufferCount.HasValue)
            {
                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Setting FMOD DSP buffer by user: {0} length, {1} buffers", withCustomDSPBufferLength.Value, withCustomDSPBufferCount.Value);

                result = this.System.setDSPBufferSize(withCustomDSPBufferLength.Value, (int)withCustomDSPBufferCount.Value);
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.setDSPBufferSize");
            }
            */

            /*
             * ASIO flies on windows - with properly configured ASIO4ALL single output device ..
             */
            // result = this.System.setOutput(FMOD.OUTPUTTYPE.ASIO);
            // AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.setOutput");

            /*
             * init the system
             */
            // https://www.fmod.com/docs/api/content/generated/FMOD_System_Init.html
            // Currently the maximum channel limit is 4093.

            var outputtype = FMOD.OUTPUTTYPE.AUTODETECT;
            result = this.System.setOutput(outputtype);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.setOutput");

            var flags = FMOD.INITFLAGS.NORMAL;
            System.IntPtr extradriverdata = global::System.IntPtr.Zero;
            result = this.System.init(4093, flags, extradriverdata);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.init");

            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Setting output to driver {0} ", forOutputDevice);

            result = this.System.setDriver(forOutputDevice);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.setDriver");


            result = this.System.getDSPBufferSize(out this.DSPBufferLength, out this.DSPBufferCount);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.getDSPBufferSize");

            this.SystemHandle = this.System.handle;

            this.OutputDeviceID = forOutputDevice;
        }
        /// <summary>
        /// (it's public to allow to be called at any time for 0 system)
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public void SetAsNotificationSystem(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            // install notification callback for output 0
            // make it one per application
            if (this.OutputDeviceID == 0
                && this.outputDevicesChangedCallback == null)
            {
                this.outputDevicesChangedCallback = new FMOD.SYSTEM_CALLBACK(FMODSystemOutputDevice.OutputDevicesChangedCallback);

                // notification for 
                result = this.System.setCallback(this.outputDevicesChangedCallback
                    , FMOD.SYSTEM_CALLBACK_TYPE.DEVICELISTCHANGED
                    | FMOD.SYSTEM_CALLBACK_TYPE.DEVICELOST
                    | FMOD.SYSTEM_CALLBACK_TYPE.RECORDLISTCHANGED
                    );
                AudioStreamSupport.ERRCHECK(result, logLevel, "FMODSystem_OutputMonitoring", onError, "system.setCallback");

                AudioStreamSupport.LOG(LogLevel.INFO, LogLevel.INFO, gameObjectName, onError, "Installed DEVICELISTCHANGED callback on driver {0} ", this.OutputDeviceID);

                this.isNotificationSystem = true;
            }
        }
        /// <summary>
        /// Release this' FMOD system
        /// called by manager's release (if from StopSound), or from destructor (if no sounds was created via manager)
        /// </summary>
        public void Release(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = System.close();
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "fmodsystem.System.close");

                result = System.release();
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "fmodsystem.System.release");

                AudioStreamSupport.LOG(LogLevel.DEBUG, logLevel, gameObjectName, onError, "Released system {0} for output {1}", this.System.handle, this.OutputDeviceID);

                // unreliable - e.g. the debug log afterwards still prints !IntPtr.Zero
                System.clearHandle();
                // Debug.LogFormat("System handle after clearHandle: {0}", System.handle);

                this.SystemHandle = global::System.IntPtr.Zero;
            }
        }
        /// <summary>
        /// Call continuosly (i.e. from Unity Update, don't call from OnAudioFilterRead)
        /// </summary>
        public FMOD.RESULT Update()
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = this.System.update();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemOutputDevice", null, "this.System.update");

                return result;
            }
            else
            {
                return FMOD.RESULT.ERR_INVALID_HANDLE;
            }
        }
        /// <summary>
        /// Creates this system's sound and sets up pcm callbacks
        /// The sounds has Unity's samplerate and output channels to match 'input' audio
        /// </summary>
        /// <param name="soundChannels"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns>Created FMOD sound</returns>
        public FMOD.Sound CreateAndPlaySound(int soundChannels
            , int latency
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            /*
             * setup FMOD callback:
             * 
             * the created sound should match current audio -
             * samplerate is current Unity output samplerate, channels are from Unity's speaker mode
             * Resampling and channel distribution is handled by FMOD (uses output device's default settings, unless changed by user)
             * 
             * - pcmcallback uses a 16k block and calls the callback multiple times if it needs more data.
             * - extinfo.decodebuffersize determines the size of the double buffer (in PCM samples) that a stream uses.  Use this for user created streams if you want to determine the size of the callback buffer passed to you.  Specify 0 to use FMOD's default size which is currently equivalent to 400ms of the sound format created/loaded.
             * 
             * extinfo.decodebuffersize:
             * - FMOD does not seem to work with anything lower than 1024 properly at all
             * - directly affects latency of the output (e.g. volume changes..)
             * - if it's too high, the latency is very high, defaul value (0) gives 400 ms latency, which is not very useable
             * so the goal is to minize it as much as possible -
             * - if it's too low, and in the scene are 'many' AudioSources with e.g. 7.1 output (high bandwidth), the sound sometimes just stops playing (although all callbacks are still firing... )
             * (was using 1024 as 'default' where this happened)
             * We'll try to reach 50 ms
             * 
             * - createSound calls back immediately once after it is created
             */

            // PCMFLOAT format maps directly to Unity audio buffer so we don't have to do any conversions
            var elementSize = global::System.Runtime.InteropServices.Marshal.SizeOf(typeof(float));

            // all incoming bandwidth
            var nAvgBytesPerSec = soundChannels * AudioSettings.outputSampleRate * elementSize;
            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Default channels from Unity speaker mode: {0} (samplerate: {1}, elment size: {2}) -> nAvgBytesPerSec: {3}", soundChannels, AudioSettings.outputSampleRate, elementSize, nAvgBytesPerSec);

            var msPerSample = latency / (float)soundChannels / 1000f;
            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Requested latency: {0}, default channels: {1} -> msPerSample: {2}", latency, soundChannels, msPerSample);

            var decodebuffersize = (uint)Mathf.Max(nAvgBytesPerSec * msPerSample, 1024);

            if ((nAvgBytesPerSec * msPerSample) < 1024)
                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Not setting decodebuffersize below 1024 minimum");

            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Avg bytes per sec: {0}, msPerSample: {1} -> exinfo.decodebuffersize: {2}", nAvgBytesPerSec, msPerSample, decodebuffersize);

            // Explicitly create the delegate object and assign it to a member so it doesn't get freed by the garbage collector while it's not being used
            this.pcmreadcallback = new FMOD.SOUND_PCMREAD_CALLBACK(PCMReadCallback);
            this.pcmsetposcallback = new FMOD.SOUND_PCMSETPOS_CALLBACK(PCMSetPosCallback);

            FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
            // exinfo.cbsize = sizeof(FMOD.CREATESOUNDEXINFO);
            exinfo.numchannels = soundChannels;                                                         /* Number of channels in the sound. */
            exinfo.defaultfrequency = AudioSettings.outputSampleRate;                                   /* Default playback rate of sound. */
            exinfo.decodebuffersize = decodebuffersize;                                                 /* Chunk size of stream update in samples. This will be the amount of data passed to the user callback. */
            exinfo.length = (uint)(exinfo.defaultfrequency * exinfo.numchannels * elementSize);         /* Length of PCM data in bytes of whole song (for Sound::getLength) - this is 1 s here - (does not affect latency) */
            exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;                                                 /* Data format of sound. */
            exinfo.pcmreadcallback = this.pcmreadcallback;                                              /* User callback for reading. */
            exinfo.pcmsetposcallback = this.pcmsetposcallback;                                          /* User callback for seeking. */
            exinfo.cbsize = global::System.Runtime.InteropServices.Marshal.SizeOf(exinfo);


            FMOD.Sound sound = default(FMOD.Sound);
            FMOD.Channel channel;

            result = System.createSound(gameObjectName + "_FMOD_sound"
                    , FMOD.MODE.OPENUSER
                    | FMOD.MODE.CREATESTREAM
                    | FMOD.MODE.LOOP_NORMAL
                    , ref exinfo
                    , out sound);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "fmodsystem.system.createSound");

            FMOD.ChannelGroup master;
            result = System.getMasterChannelGroup(out master);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "fmodsystem.system.getMasterChannelGroup");

            result = System.playSound(sound, master, false, out channel);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "fmodsystem.system.playSound");

            this.sounds.Add(sound);

            AudioStreamSupport.LOG(LogLevel.DEBUG, logLevel, gameObjectName, onError, "Created sounds {0} on system {1} / output {2}", sound.handle, System.handle, this.OutputDeviceID);

            return sound;
        }
        /// <summary>
        /// Stops the sound, removes its reference from callbacks and removes it from internal playing list
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public FMOD.RESULT StopSound(FMOD.Sound sound
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            // pause was in original sample; however, it causes noticeable pop on default device when stopping the sound
            // removing it does not _seem_ to affect anything

            // global::System.Threading.Thread.Sleep(50);

            if (sound.hasHandle() && this.sounds.Contains(sound))
            {
                // cbnba
                global::System.Threading.Thread.Sleep(10);

                result = sound.release();
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "sound.release");


                // remove stopped sound from internal track list and from static callbacks
                this.sounds.Remove(sound);
                FMODSystemOutputDevice.pcmCallbackBuffers.Remove(sound.handle);

                AudioStreamSupport.LOG(LogLevel.DEBUG, logLevel, gameObjectName, onError, "Released sound {0} for system {1}, output {2}", sound.handle, SystemHandle, this.OutputDeviceID);

                // sets properly sound's handle to IntPtr.Zero
                sound.clearHandle();
            }
            else
            {
                AudioStreamSupport.LOG(LogLevel.WARNING, LogLevel.WARNING, gameObjectName, onError, "Not releasing sound {0} for system {1}, output {2}, {3} in list", sound.handle, SystemHandle, this.OutputDeviceID, this.sounds.Contains(sound) ? "" : "NOT");
            }

            return result;
        }
        /// <summary>
        /// Pass on the output audio
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="bpcm"></param>
        public void Feed(FMOD.Sound sound, byte[] bpcm)
        {
            if (sound.hasHandle() && FMODSystemOutputDevice.pcmCallbackBuffers.ContainsKey(sound.handle))
                FMODSystemOutputDevice.pcmCallbackBuffers[sound.handle].Enqueue(bpcm);
        }
        /// <summary>
        /// Returns # of sounds currently being played by this system
        /// </summary>
        /// <returns></returns>
        public int SoundsPlaying()
        {
            return this.sounds.Count;
        }
        #endregion
        // ========================================================================================================================================
        #region individual sounds/channels mix matrix
        /// <summary>
        /// Plays user audio file (allowed netstream) directly by FMOD
        /// - creates a new sound on this system and optionally sets a mix matrix on channel - that means that there's always 1 sound : 1 channel, subsequent calls can update mix matrix
        /// </summary>
        /// <param name="audioUri">filename - full or relative file path or netstream address</param>
        /// <param name="loop"></param>
        /// <param name="volume"></param>
        /// <param name="withMixmatrix">optional mix matrix - pass null when not needed</param>
        /// <param name="outchannels">ignored if withMixmatrix == null</param>
        /// <param name="inchannels">ignored if withMixmatrix == null</param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="channel">FMOD channel returned to the user to be used in subsequent calls</param>
        /// <returns></returns>

// don't exception
        public FMOD.RESULT PlayUserSound(string audioUri
            , bool loop
            , float volume
            , float[,] withMixmatrix
            , int outchannels
            , int inchannels
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , out FMOD.Channel channel
            )
        {
            channel = default(FMOD.Channel);

            // preliminary check for matrix dimensions if it's specified
            if (withMixmatrix != null
                && 
                outchannels * inchannels != withMixmatrix.Length)
            {
                AudioStreamSupport.LOG(LogLevel.ERROR, logLevel, gameObjectName, onError, "Make sure to provide correct mix matrix dimensions: {0} x {1} != {2}", outchannels, inchannels, withMixmatrix.Length);
                return FMOD.RESULT.ERR_INVALID_PARAM;
            }

            // create a new sound
            var sound = default(FMOD.Sound);
            FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
            exinfo.cbsize = global::System.Runtime.InteropServices.Marshal.SizeOf(exinfo);

            result = System.createSound(audioUri
                    , FMOD.MODE.DEFAULT
                    | (loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF)
                    , ref exinfo
                    , out sound);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.createSound", false);

            if (result != FMOD.RESULT.OK)
                return result;

            this.sounds.Add(sound);

            // play the sound on master
            FMOD.ChannelGroup master;
            result = System.getMasterChannelGroup(out master);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.getMasterChannelGroup", false);

            result = System.playSound(sound, master, false, out channel);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "System.playSound", false);

            result = channel.setVolume(volume);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "channel.setVolume", false);

            if (withMixmatrix != null)
            {
                result = this.SetMixMatrix(channel, withMixmatrix, outchannels, inchannels);
                AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "SetMixMatrix", false);
            }

            return result;
        }

        public FMOD.RESULT SetUserSoundVolume(FMOD.Channel channel
            , float volume)
        {
            result = channel.setVolume(volume);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemOutputDevice", null, "channel.setVolume", false);

            return result;
        }
        public bool IsPlaying(FMOD.Channel channel)
        {
            bool isPlaying;
            result = channel.isPlaying(out isPlaying);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemOutputDevice", null, "channel.isPlaying", false);

            if (result != FMOD.RESULT.OK)
                return false;
            else
                return isPlaying;
        }

        public FMOD.RESULT StopUserSound(FMOD.Channel channel
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMOD.Sound sound;
            result = channel.getCurrentSound(out sound);
            AudioStreamSupport.ERRCHECK(result, logLevel, gameObjectName, onError, "channel.getCurrentSound", false);

            if (result == FMOD.RESULT.OK)
                return this.StopSound(sound, logLevel, gameObjectName, onError);
            else
                return FMOD.RESULT.ERR_INVALID_PARAM;
        }

        public FMOD.RESULT SetMixMatrix(FMOD.Channel forChannel
            , float[,] mixMatrix
            , int outchannels
            , int inchannels)
        {
            // check for matrix dimensions
            if (outchannels * inchannels != mixMatrix.Length)
                return FMOD.RESULT.ERR_INVALID_PARAM;

            // flatten the matrix for API call
            float[] mixMatrix_flatten = new float[outchannels * inchannels];
            for (var r = 0; r < outchannels; ++r)
            {
                for (var c = 0; c < inchannels; ++c)
                    mixMatrix_flatten[r * inchannels + c] = mixMatrix[r, c];
            }

            return forChannel.setMixMatrix(mixMatrix_flatten, outchannels, inchannels);
        }

        public FMOD.RESULT GetMixMatrix(FMOD.Channel ofChannel, out float[,] mixMatrix, out int outchannels, out int inchannels)
        {
            mixMatrix = null;
            outchannels = inchannels = 0;

            float[] mixMatrix_flatten = null;

            result = ofChannel.getMixMatrix(mixMatrix_flatten, out outchannels, out inchannels);
            if (result != FMOD.RESULT.OK)
                return result;

            mixMatrix = new float[outchannels, inchannels];
            for (var i = 0; i < mixMatrix_flatten.Length; ++i)
                mixMatrix[i / inchannels, i % outchannels] = mixMatrix_flatten[i];

            return result;
        }
        #endregion
        // ========================================================================================================================================
        #region Output devices changed notification
        public bool isNotificationSystem { get; protected set; }
        /// <summary>
        /// Keep the reference around..
        /// </summary>
        FMOD.SYSTEM_CALLBACK outputDevicesChangedCallback = null;
        /// <summary>
        /// AudioSourceOutputDevice instances added/removed via AddToNotifiedInstances/RemoveFromNotifiedInstances to be notified when system output device list changes
        /// </summary>
        readonly static HashSet<IntPtr> outputDevicesChangedNotify = new HashSet<IntPtr>();
        /// <summary>
        /// we should probably guard access to HashSet
        /// </summary>
        readonly static object notification_callback_lock = new object();
        public static void AddToNotifiedInstances(IntPtr notifiedInstance)
        {
            lock (FMODSystemOutputDevice.notification_callback_lock)
            {
                FMODSystemOutputDevice.outputDevicesChangedNotify.Add(notifiedInstance);
            }
        }
        public static void RemoveFromNotifiedInstances(IntPtr notifiedInstance)
        {
            lock (FMODSystemOutputDevice.notification_callback_lock)
            {
                FMODSystemOutputDevice.outputDevicesChangedNotify.Remove(notifiedInstance);
            }
        }
        [AOT.MonoPInvokeCallback(typeof(FMOD.SYSTEM_CALLBACK))]
        static FMOD.RESULT OutputDevicesChangedCallback(IntPtr system, FMOD.SYSTEM_CALLBACK_TYPE type, IntPtr commanddata1, IntPtr commanddata2, IntPtr userdata)
        {
            // Debug.LogFormat("emitting from {0}, type {1}, commanddata1 {2}, commanddata2 {3} userdata {4}, list {5}", system, type, commanddata1, commanddata2, userdata, FMODSystemOutputDevice.outputDevicesChangedNotify.Count);

            lock (FMODSystemOutputDevice.notification_callback_lock)
            {
                foreach (var instancePtr in FMODSystemOutputDevice.outputDevicesChangedNotify)
                {
                    GCHandle objecthandle = GCHandle.FromIntPtr(instancePtr);
                    var audioStreamOutput = (objecthandle.Target as AudioStreamDevicesChangedNotify);
                    audioStreamOutput.outputDevicesChanged = true;
                }
            }

            return FMOD.RESULT.OK;
        }
        #endregion
    }
}