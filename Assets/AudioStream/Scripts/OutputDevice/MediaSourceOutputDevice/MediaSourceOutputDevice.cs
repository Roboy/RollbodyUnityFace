// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Output device + specific channel selection via mix matrix for media played directly by FMOD only
    /// There is no Editor/Inspector functionality for user sounds (except Unity events) - only API is exposed via this component currently -
    /// Please see how it's used in MediaSourceOutputDeviceDemo scene
    /// </summary>
    public class MediaSourceOutputDevice : MonoBehaviour
	{
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("You can set initial output driver on which audio will be played by default.\r\nMost of functionality is accessible via API only currently. Please see 'MediaSourceOutputDeviceDemo' for more.")]
        /// <summary>
        /// This' FMOD system current output device
        /// </summary>
        public int outputDriverID = 0;
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringParameter OnPlaybackStopped;
        public EventWithStringParameter OnPlaybackPaused;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Output device latency (ms) (info only)]")]
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyBlock;
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyTotal;
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyAverage;
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion
        // ========================================================================================================================================
        #region FMOD start/release/play sound
        /// <summary>
        /// Component can play multiple user's sounds via API - manage all of them separately + their volumes
        /// </summary>
        List<FMOD.Channel> channels = new List<FMOD.Channel>();
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
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;

        void StartFMODSystem()
        {
            /*
             * before creating system for target output, check if it wouldn't fail with it first :: device list can be changed @ runtime now
             * if it would, fallback to default ( 0 ) which should be hopefully always available - otherwise we would have failed miserably some time before already
             */
            if (!FMODSystemsManager.AvailableOutputs(this.logLevel, this.gameObjectName, this.OnError).Select(s => s.id).Contains(this.outputDriverID))
            {
                LOG(LogLevel.WARNING, "Output device {0} is not available, using default output (0) as fallback", this.outputDriverID);
                this.outputDriverID = 0;
            }

            // TODO: throws exception in the constructor
            this.outputdevice_system = FMODSystemsManager.FMODSystemForOutputDevice_Create(this.outputDriverID, false, this.logLevel, this.gameObjectName, this.OnError);
            this.fmodVersion = this.outputdevice_system.VersionString;

            // compute latency as last step
            // TODO: move latency to system creation

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
        void StopFMODSystem()
        {
            if (this.outputdevice_system != null)
            {
                foreach (var channel in this.channels)
                {
                    this.outputdevice_system.StopUserSound(channel, this.logLevel, this.gameObjectName, this.OnError);
                }

                this.channels.Clear();

                FMODSystemsManager.FMODSystemForOutputDevice_Release(this.outputdevice_system, this.logLevel, this.gameObjectName, this.OnError);

                this.outputdevice_system = null;
            }
        }
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        void Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gameObjectName = this.gameObject.name;

            this.StartFMODSystem();

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

                // update running and cleanup finished sounds
                var removeChannels = new List<FMOD.Channel>();

                foreach (var channel in this.channels)
                {
                    if (!this.outputdevice_system.IsPlaying(channel))
                    {
                        this.outputdevice_system.StopUserSound(channel, this.logLevel, this.gameObjectName, this.OnError);
                        removeChannels.Add(channel);
                    }
                }

                foreach (var channel in removeChannels)
                    this.channels.Remove(channel);
            }
        }

        void OnDestroy()
        {
            this.StopFMODSystem();
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
        /// Use different output
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
            this.StopFMODSystem();

            this.StartFMODSystem();
        }
        /// <summary>
        /// Plays 
        /// </summary>
        /// <param name="audioUri"></param>
        public void PlayUserSound(string audioUri
            , float volume
            , bool loop
            , float[,] mixMatrix
            , int outchannels
            , int inchannels
            , out FMOD.Channel channel
            )
        {
            result = this.outputdevice_system.PlayUserSound(
                audioUri
                , loop
                , volume
                , mixMatrix
                , outchannels
                , inchannels
                , this.logLevel
                , this.gameObjectName
                , this.OnError
                , out channel
                );

            if (result == FMOD.RESULT.OK)
            {
                this.channels.Add(channel);

                if (this.OnPlaybackStarted != null)
                    this.OnPlaybackStarted.Invoke(this.gameObjectName);
            }
            else
            {
                var msg = string.Format("Can't start sound: {0}", FMOD.Error.String(result));

                LOG(LogLevel.ERROR, msg);
                
                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);
            }
        }
        public FMOD.RESULT PauseUserSound(FMOD.Channel channel, bool paused)
        {
            result = channel.setPaused(paused);
            ERRCHECK(result, "channel.setPaused", false);

            if (result == FMOD.RESULT.OK)
                if (this.OnPlaybackPaused != null)
                    this.OnPlaybackPaused.Invoke(this.gameObjectName);

            return result;
        }

        public FMOD.RESULT IsSoundPaused(FMOD.Channel channel, out bool paused)
        {
            result = channel.getPaused(out paused);
            // ERRCHECK(result, "channel.getPaused", false);

            return result;
        }

        public bool IsSoundPlaying(FMOD.Channel channel)
        {
            bool isPlaying;
            result = channel.isPlaying(out isPlaying);
            // ERRCHECK(result, "channel.isPlaying", false);

            if (result == FMOD.RESULT.OK)
                return isPlaying;
            else
                return false;
        }
        public float GetVolume(FMOD.Channel channel)
        {
            float volume;
            result = channel.getVolume(out volume);
            // ERRCHECK(result, "channel.getVolume", false);

            if (result == FMOD.RESULT.OK)
                return volume;
            else
                return 0f;
        }
        public void SetVolume(FMOD.Channel channel, float volume)
        {
            result = channel.setVolume(volume);
            // ERRCHECK(result, "channel.setVolume", false);
        }

        public void SetPitch(FMOD.Channel channel, float pitch)
        {
            result = channel.setPitch(pitch);
            // ERRCHECK(result, "channel.setPitch", false);
        }
        public float GetPitch(FMOD.Channel channel)
        {
            float pitch;
            result = channel.getPitch(out pitch);
            // ERRCHECK(result, "channel.getPitch", false);

            if (result == FMOD.RESULT.OK)
                return pitch;
            else
                return 1f;
        }
        public void SetTimeSamples(FMOD.Channel channel, int timeSamples)
        {
            result = channel.setPosition((uint)timeSamples, FMOD.TIMEUNIT.PCM);
            // ERRCHECK(result, "channel.setPosition", false);
        }
        public int GetTimeSamples(FMOD.Channel channel)
        {
            uint timeSamples;
            result = channel.getPosition(out timeSamples, FMOD.TIMEUNIT.PCM);
            if (result == FMOD.RESULT.OK)
                return (int)timeSamples;
            else
                return -1;
        }

        public int GetLengthSamples(FMOD.Channel channel)
        {
            uint lengthSamples;
            FMOD.Sound sound;
            result = channel.getCurrentSound(out sound);
            if (result == FMOD.RESULT.OK)
            {
                result &= sound.getLength(out lengthSamples, FMOD.TIMEUNIT.PCM);
                if (result == FMOD.RESULT.OK)
                    return (int)lengthSamples;
                else return -1;
            }
            return -1;
        }

        public FMOD.RESULT SetMixMatrix(FMOD.Channel channel
            , float[,] mixMatrix
            , int outchannels
            , int inchannels
            )
        {
            return this.outputdevice_system.SetMixMatrix(channel, mixMatrix, outchannels, inchannels);
        }

        public FMOD.RESULT GetMixMatrix(FMOD.Channel channel, out float[,] mixMatrix, out int outchannels, out int inchannels)
        {
            return this.outputdevice_system.GetMixMatrix(channel, out mixMatrix, out outchannels, out inchannels);
        }

        /// <summary>
        /// Stops playback started w/ PlayUserSound
        /// </summary>
        /// <param name="audioUri"></param>
        public void StopUserSound(FMOD.Channel channel)
        {
            FMOD.Sound sound;
            result = channel.getCurrentSound(out sound);
            // don't log failure on not started sound
            // ERRCHECK(result, "channel.getCurrentSound", false);

            if (result != FMOD.RESULT.OK)
                return;

            result = this.outputdevice_system.StopSound(sound, this.logLevel, this.gameObjectName, this.OnError);
            ERRCHECK(result, "outputdevice_system.StopUserSound", false);

            if (result == FMOD.RESULT.OK)
            {
                this.channels.Remove(channel);

                if (this.OnPlaybackStopped != null)
                    this.OnPlaybackStopped.Invoke(this.gameObjectName);
            }
        }
        #endregion
    }
}
