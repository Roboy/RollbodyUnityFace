// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Unity AudioSource redirection
    /// Main part tying together Unity AudioSource and FMOD output via a FMOD user sound
    /// </summary>
    public class AudioSourceOutputDevice : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor

        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;
        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        [Tooltip("Mute the signal after being routed.\r\n - otherwise the signal will be audible on redirected and on system (Unity) default output simultaneously\r\nAlso useful when having more than one AudioSourceOutputDevice on one AudioSource/Listener for multiple devices at the same time.\r\n- only the last one in chain should be muted in that case.")]
        public bool muteAfterRouting = true;

        [Header("[Input mix latency (ms)]")]
        [Range(1, 400)]
        [Tooltip("User adjustable latency for the incoming Unity signal (note: that runs under its own separate latency) - \r\nchange according to the actual conditions (i.e. until 'things still work')\r\nThis is FMOD's created sound latency to sample Unity's audio from. By default FMOD sets this to 400 ms, which is way too high to be useable. Note that setting this to too low value might cause the output audio to be silenced altogether, even after a while.")]
        public int inputLatency = 25;

        [Header("[Output device latency (ms) (info only)]")]
        [Tooltip("Computed for current output device at runtime")]
        public float latencyBlock;
        [Tooltip("Computed for current output device at runtime")]
        public float latencyTotal;
        [Tooltip("Computed for current output device at runtime")]
        public float latencyAverage;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRedirectStarted;
        public EventWithStringParameter OnRedirectStopped;
        public EventWithStringStringParameter OnError;
        #endregion

        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region FMOD && Unity audio callback
        /// <summary>
        /// Component startup sync
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;
        /// <summary>
        /// The system which plays the sound on selected output - one per output, released when sound (redirection) is stopped,
        /// </summary>
        FMODSystemOutputDevice outputdevice_system = null;
        /// <summary>
        /// FMOD sound ptr passed to static callback
        /// needed to identify this instance's audio buffers
        /// </summary>
        protected FMOD.Sound sound;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        /// <summary>
        /// Retrieve shared FMOD system and create this' sound
        /// </summary>
        void Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gameObjectName = this.gameObject.name;
            /*
             * start system & sound
             * will be stopped only OnDestroy, or when outptut device changes
             */
            this.StartFMODSound();

            this.ready = true;
        }

        void Update()
        {
            // update output system
            if (
                this.outputdevice_system != null
                && this.outputdevice_system.SystemHandle != IntPtr.Zero
                )
            {
                this.outputdevice_system.Update();
            }
        }
        /// <summary>
        /// Forward audio buffer if fmod component is running
        /// (don't touch fmod system update - audio thread interferes even with GC cleanup)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channels"></param>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.outputdevice_system != null && this.outputdevice_system.SystemHandle != IntPtr.Zero)
            {
                if (this.sound.hasHandle())
                {
                    // direct 'bytewise' copy is enough since we know the format of the output

                    byte[] bArr = new byte[data.Length * sizeof(float)];

                    Buffer.BlockCopy(data, 0, bArr, 0, bArr.Length);

                    this.outputdevice_system.Feed(this.sound, bArr);
                }
            }

            if (this.muteAfterRouting)
                // clear the output buffer if needed
                // e.g. when this component is the last one in audio chain (at the bottom in the inspector)
                Array.Clear(data, 0, data.Length);
        }
        /// <summary>
        /// OnDestroy should be proper place to release the sound - it's called on scene unload / application quit and since the sound should be running for the lifetime of this GO this is should be last place to properly release it
        /// </summary>
        void OnDestroy()
        {
            this.StopFMODSound();
        }
        #endregion

        // ========================================================================================================================================
        #region internal FMOD Start / Stop
        /// <summary>
        /// Creates a sound on shared FMOD system
        /// </summary>
        void StartFMODSound()
        {
            /*
             * before creating system for target output, check if it wouldn't fail with it first :: device list can be changed @ runtime now
             * if it would, fallback to default ( 0 ) which should be hopefully always available - otherwise we would have failed miserably some time before already
             */
            if (!FMODSystemsManager.AvailableOutputs(this.logLevel, this.gameObjectName, this.OnError).Select( s => s.id).Contains(this.outputDriverID))
            {
                LOG(LogLevel.WARNING, "Output device {0} is not available, using default output (0) as fallback", this.outputDriverID);
                this.outputDriverID = 0;
            }

            // TODO: throws exception in the constructor
            this.outputdevice_system = FMODSystemsManager.FMODSystemForOutputDevice_Create(this.outputDriverID, false, this.logLevel, this.gameObjectName, this.OnError);
            this.fmodVersion = this.outputdevice_system.VersionString;

            var channels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

            // store sound to associate this instance with static callback
            // TODO: reenable dspBufferLength if DSP buffer settings are exposed again per system
            this.sound = this.outputdevice_system.CreateAndPlaySound(channels, this.inputLatency, this.logLevel, this.gameObjectName, this.OnError);

            LOG(LogLevel.INFO, string.Format("Started redirect to device {0}", this.outputDriverID));

            if (this.OnRedirectStarted != null)
                this.OnRedirectStarted.Invoke(this.gameObjectName);

            // compute latency as last step
            uint blocksize;
            int numblocks;
            result = this.outputdevice_system.System.getDSPBufferSize(out blocksize, out numblocks);
            ERRCHECK(result, "outputdevice_system.System.getDSPBufferSize");

            int samplerate;
            FMOD.SPEAKERMODE sm;
            int speakers;
            result = this.outputdevice_system.System.getSoftwareFormat(out samplerate, out sm, out speakers);
            ERRCHECK(result, "outputdevice_system.System.getSoftwareFormat");

            float ms = (float)blocksize * 1000.0f / (float)samplerate;

            this.latencyBlock = ms;
            this.latencyTotal = ms * numblocks;
            this.latencyAverage = ms * ((float)numblocks - 1.5f);
        }

        /*
         * System is released automatically when the last sound being played via it is stopped (sounds are tracked by 'sounds' member list)
         * There was not a good place to release it otherwise since it has to be released after all sounds are released and:
         * - OnApplicationQuit is called *before* OnDestroy so it couldn't be used (sound can be released when switching scenes)
         * - when released in class destructor (exit/ domain reload) it led to crashes / deadlocks in FMOD - *IF* a sound was created/played on that system before -
         */

        /// <summary>
        /// Stops sound created by this component on shared FMOD system and removes reference to it
        /// If the system is playing 0 sounds afterwards, it is released too
        /// </summary>
        void StopFMODSound()
        {
            if (this.outputdevice_system != null)
            {
                this.outputdevice_system.StopSound(this.sound, this.logLevel, this.gameObjectName, this.OnError);

                FMODSystemsManager.FMODSystemForOutputDevice_Release(this.outputdevice_system, this.logLevel, this.gameObjectName, this.OnError);

                this.outputdevice_system = null;

                LOG(LogLevel.INFO, string.Format("Stopped current redirection"));

                if (this.OnRedirectStopped != null)
                    this.OnRedirectStopped.Invoke(this.gameObjectName);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            if (!this.ready)
                errorCode = FMOD.RESULT.ERR_NOTREADY;
            else
                errorCode = this.lastError;

            return FMOD.Error.String(errorCode);
        }
        #endregion

        // ========================================================================================================================================
        #region User support
        /// <summary>
        /// Changing output device means we have to set sound format, which is allowed only before system init -> we have to restart
        /// </summary>
        /// <param name="_outputDriverID"></param>
        public void SetOutput(int _outputDriverID)
        {
            if (!this.ready)
            {
                Debug.LogErrorFormat("Please make sure to wait for 'ready' flag before calling this method");
                return;
            }

            if (_outputDriverID == this.outputDriverID)
                return;

            this.outputDriverID = _outputDriverID;

            // redirection is always running so restart it with new output
            this.StopFMODSound();

            this.StartFMODSound();
        }
        /// <summary>
        /// this' PCM callback buffer
        /// mainly for displaying some stats
        /// </summary>
        /// <returns></returns>
        public PCMCallbackBuffer PCMCallbackBuffer()
        {
            if (!this.sound.hasHandle())
                return null;

            return FMODSystemOutputDevice.PCMCallbackBuffer(this.sound);
        }
        /// <summary>
        /// Wrapper call around FMOD's setMixMatrix for current 'redirection' FMOD sound master channel - this means all Unity audio using this output will use this (last set) mix matrix
        /// Useful only in rather specific cases when *all* audio being played on this output will go to selected output channels
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="outchannels"></param>
        /// <param name="inchannels"></param>
        public void SetUnitySound_MixMatrix(float[] matrix, int outchannels, int inchannels)
        {
            if (!this.ready)
            {
                Debug.LogErrorFormat("Please make sure to wait for this component's 'ready' flag before calling this method");
                return;
            }

            if (outchannels * inchannels != matrix.Length)
            {
                Debug.LogErrorFormat("Make sure to provide correct mix matrix dimensions");
                return;
            }

            FMOD.ChannelGroup channel;
            result = this.outputdevice_system.System.getMasterChannelGroup(out channel);
            ERRCHECK(result, "outputdevice_system.system.getMasterChannelGroup");

            if (!channel.hasHandle())
            {
                LOG(LogLevel.ERROR, "AudioSourceOutputDevice not yet initialized before usage.");
                return;
            }

            result = channel.setMixMatrix(matrix, outchannels, inchannels, inchannels);
            ERRCHECK(result, "channel.setMixMatrix");

            var matrixAsString = string.Empty;

            for (var row = 0; row < outchannels; ++row)
            {
                for (var column = 0; column < inchannels; ++column)
                    matrixAsString += matrix[row * inchannels + column];

                matrixAsString += "\r\n";
            }

            LOG(LogLevel.INFO, "Set custom mix matrix to:\r\n{0}", matrixAsString);
        }
        #endregion
    }
}
