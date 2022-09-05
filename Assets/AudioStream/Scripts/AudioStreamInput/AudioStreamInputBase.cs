// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public abstract class AudioStreamInputBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Required descendant's implementation
        protected abstract void RecordingStarted();
        protected abstract void RecordingUpdate();
        protected abstract void RecordingStopped();
        #endregion

        // ========================================================================================================================================
        #region Editor
        // "No. of audio channels provided by selected recording device.
        [HideInInspector]
        public int recChannels = 0;
        [HideInInspector]
        protected int recRate = 0;

        [Header("[Source]")]
        [Tooltip("Audio input driver ID")]
        public int recordDeviceId = 0;

        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("When checked the recording will start automatically on Start with parameters set in Inspector. Otherwise StartCoroutine(Record()) of this component.")]
        public bool recordOnStart = true;

		[Tooltip("Input gain. Default 1\r\nBoosting this artificially to high value can help with faint signals for e.g. further reactive processing (although will probably distort audible signal)")]
		[Range(0f, 10f)]
		public float gain = 1f;

        [Header("[Input mixer latency (ms)]")]
        public float latencyBlock;
        public float latencyTotal;
        public float latencyAverage;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRecordingStarted;
        public EventWithStringBoolParameter OnRecordingPaused;
        public EventWithStringParameter OnRecordingStopped;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Advanced]")]
        [Tooltip("FMOD usually detects 'correct' DSP buffer size (lenght and no. of buffes) for given input, so it's recommended to leave this to default (on)\r\nWhen overriden (off), DSP buffer lenght and no. of buffers can be set manually to drive latency as low as possible (usually in 'until it still works' way).\r\n\r\nFor further notes about Unity's Best Latency setting see documentation.")]
        public bool useAutomaticDSPBufferSize = true;
        [Tooltip("Set DSP buffer size as small as possible for small latency.\r\nYou can go very low on platforms which support it - such as 32/2 on desktops - but you have to find correct *combination* with which the FMOD mixer will still work with current input.")]
        [SerializeField]
        protected uint dspBufferLength_Custom = 64;
        uint dspBufferLength_Auto = 512;
        [Tooltip("Set the count as small as possible for best latency.\r\nYou can go very low on platforms which support it - such as 32/2 on desktops - but you have to find correct *combination* with which the FMOD mixer will still work with current input.")]
        [SerializeField]
        protected uint dspBufferCount_Custom = 2;
        uint dspBufferCount_Auto = 4;
        [Tooltip("This is useful to turn off if the original samplerate of the input has to be preserved - e.g. when some other plugin does resampling on its own and needs original samplerate to do so\r\n\r\nOtherwise - when on (default) - the input signal is resampled to current Unity output samplerate, either via AudioClip or Speex resampler depending on user setting")]
        public bool resampleInput = true;
        [Tooltip("Use Unity's builtin resampling (either by setting the pitch, or directly via AudioClip) and input/output channels mapping.\r\nNote: if input and output samplerates differ significantly this might lead to drifting over time (resulting signal will be delayed)\r\nIn that case it's possible to use Speex resampler (by setting this to false), and also provide custom mix matrix if needed in that case (see 'SetCustomMixMatrix' method).\r\n\r\nA mix matrix is computed automatically if not specified, but it might be not enough for more advanced setups.\r\nPlease be also aware that custom Speex resampler for other than 1 and 2 output channels (Mono and Stereo output) might not work and needs Unity default (not Best) latency setting (please see README for details).")]
        public bool useUnityToResampleAndMapChannels = true;
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region Init && FMOD structures
        /// <summary>
        /// Component startup sync
        /// Also in case of recording FMOD needs some time to enumerate all present recording devices - we need to wait for it. Check this flag when using from scripting.
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;

        // !
        // TODO: create this via manager

        /// <summary>
        /// System created for each recording session, released on Stop
        /// </summary>
        protected FMOD.System recording_system;
        protected FMOD.Sound sound;
        protected FMOD.RESULT result;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        FMOD.CREATESOUNDEXINFO exinfo;
        uint version;
        uint datalength = 0;
        uint soundlength;
        uint recordpos = 0;
        uint lastrecordpos = 0;
        /// <summary>
        /// cached size for single channel for used format
        /// </summary>
        protected int channelSize = sizeof(float);
        /// <summary>
        /// Unity output sample rate (retrieved from main thread to be consumed at all places)
        /// </summary>
        public int outputSampleRate { get; protected set; }
        /// <summary>
        /// Unity output channels from default speaker mode (retrieved from main thread to be consumed at all places)
        /// </summary>
        public int outputChannels { get; protected set; }
        protected virtual IEnumerator Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);

            // Reference Microphone class on Android in order for Unity to include necessary manifest permission automatically
#if UNITY_ANDROID
            for (var i = 0; i < Microphone.devices.Length; ++i)
                print(string.Format("Enumerating Unity input devices on Android - {0}: {1}", i, Microphone.devices[i]));
#endif
            this.gameObjectName = this.gameObject.name;

            // setup the AudioSource if/when it's being used
            var audiosrc = this.GetComponent<AudioSource>();
            if (audiosrc)
            {
                audiosrc.playOnAwake = false;
                audiosrc.Stop();
                audiosrc.clip = null;
            }

            /*
             * Auto DSP buffer
             */
            // get enumeration system
            var fmodsystem = FMODSystemsManager.FMODSystemInputDevice_Create(this.logLevel, this.gameObjectName, this.OnError);

            uint bufferLength;
            int numBuffers;
            result = fmodsystem.system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "system.getDSPBufferSize");

            LOG(LogLevel.INFO, "FMOD DSP buffer: {0} length, {1} buffers", bufferLength, numBuffers);

            this.dspBufferLength_Auto = bufferLength;
            this.dspBufferCount_Auto = (uint)numBuffers;

            // wait for FMDO to catch up - recordDrivers are not populated if called immediately [e.g. from Start]

            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            int retries = 0;

            while (numConnectedDrivers < 1)
            {
                result = fmodsystem.system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
                ERRCHECK(result, "system.getRecordNumDrivers");

                LOG(LogLevel.INFO, "Drivers\\Connected drivers: {0}\\{1}", numAllDrivers, numConnectedDrivers);

                if (++retries > 30)
                {
                    var msg = string.Format("There seems to be no audio input device connected");

                    LOG(LogLevel.ERROR, msg);

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                // this timeout is necessary for recordOnStart
                // TODO: is ad hoc value
                yield return new WaitForSeconds(0.1f);
            }

            FMODSystemsManager.FMODSystemInputDevice_Release(fmodsystem, this.logLevel, this.gameObjectName, this.OnError);

            this.ready = true;

            if (this.recordOnStart)
                this.Record();
        }

        protected virtual void Update()
        {
            // update is empty leftover from previous notification implementation which got moved
        }
        #endregion

        // ========================================================================================================================================
        #region Recording
        [Header("[Runtime]")]
        [Tooltip("Set during recording.")]
        public bool isRecording = false;
        [Tooltip("Set during recording.")]
        public bool isPaused = false;
        public void Record()
        {
            this.outputSampleRate = AudioSettings.outputSampleRate;
            this.outputChannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

            StartCoroutine(this.Record_CR());
        }
        public IEnumerator Record_CR()
        {
            while (!this.ready)
            {
                LOG(LogLevel.INFO, "Waiting for device {0} to be ready..", this.recordDeviceId);
                yield return null;
            }

            if (this.isRecording)
            {
                LOG(LogLevel.WARNING, "Already recording.");
                yield break;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.ERROR, "Will not start on disabled GameObject.");
                yield break;
            }

            this.isRecording = false;
            this.isPaused = false;

            this.Stop_Internal(); // try to clean partially started recording / Start initialized system

            /*
            Create a System object and initialize.
            */
            // TODO: create this from common manager - will have to pair DSP buffer sizes -
            // when created per recording session it seems to help with starting with clean buffers, however
            result = FMOD.Factory.System_Create(out recording_system);
            ERRCHECK(result, "Factory.System_Create");

            result = recording_system.getVersion(out version);
            ERRCHECK(result, "recording_system.getVersion");

            if (version < FMOD.VERSION.number)
            {
                var msg = string.Format("FMOD lib version {0} doesn't match header version {1}", version, FMOD.VERSION.number);

                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                yield break;
            }

            /*
             * Adjust DSP buffer for recording if requested by user
             */
            if (!this.useAutomaticDSPBufferSize)
            {
                LOG(LogLevel.INFO, "Setting FMOD DSP buffer by user: {0} length, {1} buffers", this.dspBufferLength_Custom, this.dspBufferCount_Custom);

                result = recording_system.setDSPBufferSize(this.dspBufferLength_Custom, (int)this.dspBufferCount_Custom);
                ERRCHECK(result, "recording_system.setDSPBufferSize");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // For recording to work on Android OpenSL support is needed:
            // https://www.fmod.org/questions/question/is-input-recording-supported-on-android/

            result = recording_system.setOutput(FMOD.OUTPUTTYPE.OPENSL);
            ERRCHECK(result, "recording_system.setOutput", false);

            if ( result != FMOD.RESULT.OK )
            {
                LOG(LogLevel.ERROR, "OpenSL support needed for recording not available.");
                yield break;
            }
#endif
            /*
            System initialization
            */
            result = recording_system.init(100, FMOD.INITFLAGS.NORMAL, System.IntPtr.Zero);
            ERRCHECK(result, "recording_system.init");

            uint bufferLength;
            int numBuffers;

            result = recording_system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "recording_system.getDSPBufferSize");

            this.dspBufferLength_Auto = bufferLength;
            this.dspBufferCount_Auto = (uint)numBuffers;

            LOG(LogLevel.INFO, "Effective FMOD DSP buffer: {0} length, {1} buffers", bufferLength, numBuffers);

            // wait for FMDO to catch up - recordDrivers are not populated if called immediately [e.g. from Start]

            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            int retries = 0;

            while (numConnectedDrivers < 1)
            {
                result = recording_system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
                ERRCHECK(result, "recording_system.getRecordNumDrivers");

                LOG(LogLevel.INFO, "Drivers\\Connected drivers: {0}\\{1}", numAllDrivers, numConnectedDrivers);

                if (++retries > 30)
                {
                    var msg = string.Format("There seems to be no audio input device connected");

                    LOG(LogLevel.ERROR, msg);

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            // Unity 2017.1 and up has iOS Player Setting 'Force iOS Speakers when Recording' which should be respected
            #if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                LOG(LogLevel.INFO, "Setting audio output to default/earspeaker ...");
                iOSSpeaker.RouteForRecording();
            }
            #endif

            /*
             * clear previous run if any
             */

            // reset FMOD record buffer positions
            this.lastrecordpos = this.recordpos = 0;

            // and clear previously retrieved recording data
            this.outputBuffer.Clear();

            /*
             * create FMOD sound
             */
            int namelen = 255;
            string name;
            System.Guid guid;
            FMOD.SPEAKERMODE speakermode;
            FMOD.DRIVER_STATE driverstate;
            result = recording_system.getRecordDriverInfo(this.recordDeviceId, out name, namelen, out guid, out recRate, out speakermode, out recChannels, out driverstate);
            ERRCHECK(result, "recording_system.getRecordDriverInfo");

            exinfo = new FMOD.CREATESOUNDEXINFO();
            exinfo.numchannels = recChannels;
            exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;                         /* this implies higher bandwidth (i.e. 4 bytes per channel/sample) but seems to work on desktops with DSP sizes low enough */
            exinfo.defaultfrequency = recRate;
            exinfo.length = (uint)(recRate * this.channelSize * recChannels);     /* 1 second buffer, size here doesn't change latency */
            exinfo.cbsize = Marshal.SizeOf(exinfo);

            result = recording_system.createSound(string.Empty, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exinfo, out sound);
            ERRCHECK(result, "recording_system.createSound");

            LOG(LogLevel.INFO, "Opened device, channels: {0}, format: {1}, rate: {2} (sound length: {3}, info size: {4}))", exinfo.numchannels, exinfo.format, exinfo.defaultfrequency, exinfo.length, exinfo.cbsize);

            result = recording_system.recordStart(this.recordDeviceId, sound, true);
            ERRCHECK(result, "recording_system.recordStart");

            result = sound.getLength(out soundlength, FMOD.TIMEUNIT.PCM);
            ERRCHECK(result, "sound.getLength");

            datalength = 0;

            this.RecordingStarted();

            this.isRecording = true;

            if (this.OnRecordingStarted != null)
                this.OnRecordingStarted.Invoke(this.gameObjectName);

            // compute latency as last step
            uint blocksize;
            int numblocks;
            result = recording_system.getDSPBufferSize(out blocksize, out numblocks);
            ERRCHECK(result, "recording_system.getDSPBufferSize");

            int samplerate;
            FMOD.SPEAKERMODE sm;
            int speakers;
            result = recording_system.getSoftwareFormat(out samplerate, out sm, out speakers);
            ERRCHECK(result, "recording_system.getSoftwareFormat");

            float ms = (float)blocksize * 1000.0f / (float)samplerate;

            this.latencyBlock = ms;
            this.latencyTotal = ms * numblocks;
            this.latencyAverage = ms * ((float)numblocks - 1.5f);


            while (this.isRecording)
            {
                this.RecordingUpdate();
                yield return null;
            }
        }

        // TODO: implement RMS and other potentially useful stats per input channel
        // - add meters to demo scenes separately for input...

        /// <summary>
        /// Helper method called from descendant
        /// Since it might be required at different times, i.e. either from OnAudioFilterRead or from normal Update, it can't be called directly by base from here
        /// </summary>
        protected void UpdateRecordBuffer()
        {
            result = recording_system.update();
            ERRCHECK(result, "recording_system.update", false);

            result = recording_system.getRecordPosition(this.recordDeviceId, out recordpos);
            ERRCHECK(result, "recording_system.getRecordPosition");

            /// <summary>
            /// FMOD recording buffers and their lengths
            /// </summary>
            System.IntPtr ptr1, ptr2;
            uint len1, len2;

            if (recordpos != lastrecordpos)
            {
                int blocklength;

                blocklength = (int)recordpos - (int)lastrecordpos;
                if (blocklength < 0)
                {
                    blocklength += (int)soundlength;
                }

                /*
                Lock the sound to get access to the raw data.
                */
                result = sound.@lock((uint)(lastrecordpos * exinfo.numchannels * this.channelSize), (uint)(blocklength * exinfo.numchannels * this.channelSize), out ptr1, out ptr2, out len1, out len2);   /* if e.g. stereo 16bit, exinfo.numchannels * 2 = 1 sample = 4 bytes. */
                if (result != FMOD.RESULT.OK)
                    return;
                /*
                Write it to output.
                */
                if (ptr1 != System.IntPtr.Zero && len1 > 0)
                {
                    datalength += len1;
                    byte[] barr = new byte[len1];
                    Marshal.Copy(ptr1, barr, 0, (int)len1);

                    this.AddBytesToOutputBuffer(barr);
                }
                if (ptr2 != System.IntPtr.Zero && len2 > 0)
                {
                    datalength += len2;
                    byte[] barr = new byte[len2];
                    Marshal.Copy(ptr2, barr, 0, (int)len2);

                    this.AddBytesToOutputBuffer(barr);
                }

                /*
                Unlock the sound to allow FMOD to use it again.
                */
                result = sound.unlock(ptr1, ptr2, len1, len2);
                if (result != FMOD.RESULT.OK)
                    return;
            }
            else
            {
                len1 = len2 = 0;
            }

            lastrecordpos = recordpos;
        }

        public void Pause(bool pause)
        {
            if (!this.isRecording)
            {
                LOG(LogLevel.WARNING, "Not recording..");
                return;
            }

            this.isPaused = pause;

            LOG(LogLevel.INFO, "{0}", this.isPaused ? "paused." : "resumed.");

            if (this.OnRecordingPaused != null)
                this.OnRecordingPaused.Invoke(this.gameObjectName, this.isPaused);
        }
        #endregion

        // ========================================================================================================================================
        #region Shutdown
        public void Stop()
        {
            LOG(LogLevel.INFO, "Stopping..");

            this.StopAllCoroutines();

            this.RecordingStopped();

            this.Stop_Internal();

            if (this.OnRecordingStopped != null)
                this.OnRecordingStopped.Invoke(this.gameObjectName);
        }

        /// <summary>
        /// Stop and try to release FMOD sound resources
        /// </summary>
        void Stop_Internal()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                asource.Stop();
                Destroy(asource.clip);
                asource.clip = null;
            }

            this.isRecording = false;
            this.isPaused = false;

            /*
                Shut down sound
            */
            if (sound.hasHandle())
            {
                result = sound.release();
                ERRCHECK(result, "sound.release", false);

                sound.clearHandle();
            }

            /*
                Shut down
            */
            if (recording_system.hasHandle())
            {
                result = recording_system.close();
                ERRCHECK(result, "system.close", false);

                result = recording_system.release();
                ERRCHECK(result, "system.release", false);

                recording_system.clearHandle();
            }
        }

        protected virtual void OnDestroy()
        {
            this.Stop();
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        protected void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }

        /// <summary>
        /// Exchange buffer between FMOD and Unity
        /// this needs capacity at contruction but will be resized later if needed
        /// </summary>
        BasicBufferByte outputBuffer = new BasicBufferByte(10000);
        readonly object outputBufferLock = new object();
        /// <summary>
        /// Stores audio retrieved from FMOD's sound via UpdateRecordBuffer call
        /// </summary>
        /// <param name="arr"></param>
        void AddBytesToOutputBuffer(byte[] arr)
        {
            lock (this.outputBufferLock)
            {
                // if it's paused discard retrieved input ( but the whole recording update loop is running to allow for seamless continuation )
                if (this.isPaused)
                    return;

                // check if there's still enough space in noncircular buffer
                // (since circular buffer does not seem to work / )
                if (this.outputBuffer.Available() + arr.Length > this.outputBuffer.Capacity())
                {
                    var newCap = this.outputBuffer.Capacity() * 2;

                    LOG(LogLevel.INFO, "Resizing output buffer from: {0} to {1} (FMOD is retrieving more data than Unity is able to drain continuosly : input channels: {2}, output channels: {3})"
                        , this.outputBuffer.Capacity()
                        , newCap
                        , this.recChannels
                        , this.outputChannels
                        );

                    // preserve existing data
                    BasicBufferByte newBuffer = new BasicBufferByte(newCap);
                    newBuffer.Write(this.outputBuffer.Read(this.outputBuffer.Available()));

                    this.outputBuffer = newBuffer;
                }

                outputBuffer.Write(arr);
            }
        }
        /// <summary>
        /// Retrieves recorded data for Unity callbacks
        /// </summary>
        /// <param name="_len"></param>
        /// <returns></returns>
        protected float[] GetAudioOutputBuffer(uint _len)
        {
            lock (this.outputBufferLock)
            {
                // get bytes based on format used
                int len = (int)_len * this.channelSize;

                // adjust to what's available
                len = Mathf.Min(len, this.outputBuffer.Available());

                // read available bytes
                byte[] bArr = this.outputBuffer.Read(len);

                // copy out bytes to float array
                float[] oafrDataArr = new float[len / this.channelSize];
                System.Buffer.BlockCopy(bArr, 0, oafrDataArr, 0, bArr.Length);

                return oafrDataArr;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region User support
        /// <summary>
        /// Changing DSP buffers before Init means we have to restart
        /// </summary>
        /// <param name="_dspBufferLength"></param>
        /// <param name="_dspBufferCount"></param>
        public void SetUserDSPBuffers(uint _dspBufferLength, uint _dspBufferCount)
        {
            // prevent crashing on macOS/iOS/Android and overall nonsensical too small values for FMOD
            if (_dspBufferLength < 16)
            {
                Debug.LogWarningFormat("Not setting too small value {0} for DSP buffer length", _dspBufferLength);
                return;
            }

            if (_dspBufferCount < 2)
            {
                Debug.LogWarningFormat("Not setting too small value {0} for DSP buffer count", _dspBufferCount);
                return;
            }

            LOG(LogLevel.INFO, "SetDSPBuffers _dspBufferLength: {0}, _dspBufferCount: {1}", _dspBufferLength, _dspBufferCount);

            this.dspBufferLength_Custom = _dspBufferLength;
            this.dspBufferCount_Custom = _dspBufferCount;

            // using custom DSP buffer size -
            this.useAutomaticDSPBufferSize = false;

            // if input is running, restart it
            var wasRunning = this.isRecording;
            this.Stop();
            if (wasRunning)
                this.Record();
        }
        /// <summary>
        /// Changing DSP buffers before Init means we have to restart
        /// </summary>
        /// <param name="_useAutomaticDSPBufferSize"></param>
        public void SetAutomaticDSPBufferSize()
        {
            LOG(LogLevel.INFO, "SetAutomaticDSPBufferSize");

            this.useAutomaticDSPBufferSize = true;

            // if input is running, restart it
            var wasRunning = this.isRecording;
            this.Stop();
            if (wasRunning)
                this.Record();
        }
        /// <summary>
        /// Gets current DSP buffer size
        /// </summary>
        /// <param name="dspBufferLength"></param>
        /// <param name="dspBufferCount"></param>
        public void GetDSPBufferSize(out uint dspBufferLength, out uint dspBufferCount)
        {
            if (this.useAutomaticDSPBufferSize)
            {
                dspBufferLength = this.dspBufferLength_Auto;
                dspBufferCount = this.dspBufferCount_Auto;

            }
            else
            {
                dspBufferLength = this.dspBufferLength_Custom;
                dspBufferCount = this.dspBufferCount_Custom;
            }
        }
        #endregion
    }
}