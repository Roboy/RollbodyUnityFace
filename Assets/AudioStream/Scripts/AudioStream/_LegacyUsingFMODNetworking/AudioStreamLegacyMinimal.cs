// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// This is the original - now legacy - version which was using FMOD networking to download streamed audio data, up to version 1.9 of the asset
    /// </summary>
    public class AudioStreamLegacyMinimal : AudioStreamLegacyBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[AudioStreamLegacyMinimal]")]
        [Range(0f, 1f)]
        [Tooltip("Volume for AudioStreamLegacyMinimal has to be set independently from Unity audio")]
        public float volume = 1f;

        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        #endregion

        // ========================================================================================================================================
        #region AudioStreamLegacyBase
        protected override void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            this.SetOutput(this.outputDriverID);

            FMOD.ChannelGroup master;
            result = fmodsystem.System.getMasterChannelGroup(out master);
            ERRCHECK(result, "fmodsystem.System.getMasterChannelGroup");

            result = fmodsystem.System.playSound(sound, master, false, out channel);
            ERRCHECK(result, "fmodsystem.System.playSound");

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume");
        }

        protected override bool StreamStarving()
        {
            // update the system -
            result = fmodsystem.Update();
            ERRCHECK(result, null, false);

            result = sound.getOpenState(out openstate, out bufferFillPercentage, out this.starving, out deviceBusy);
            ERRCHECK(result, null, false);

            LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, this.starving, deviceBusy);

            if (channel.hasHandle())
            {
                /* Silence the stream until we have sufficient data for smooth playback. */
                // doesn't [always?] work - starving flag does one big FU when buffer is drained completely on slow connections
                // AudioStream component works better in this regard..
                result = channel.setMute(this.starving);
                // ERRCHECK(result, "channel.setMute", false);

                // ERR_INVALID_HANDLE should indicate invalid but finished channel
                // similarly as in AudiOStream case shorten the starvation period to stop immediately

                if (result == FMOD.RESULT.ERR_INVALID_HANDLE)
                {
                    this.starvingRetryCount_FileStopped = AudioStreamLegacyBase.kStarvingRetryCount_FileStopped;
                    LOG(LogLevel.INFO, "Updated starvingRetryCount automatically to {0} frame/s due to possible end of file", this.starvingRetryCount_FileStopped.Value);
                }
                else if (result == FMOD.RESULT.OK)
                {
                    // update playback time
                    uint playback_time_uint;
                    result = channel.getPosition(out playback_time_uint, FMOD.TIMEUNIT.MS);
                    ERRCHECK(result, "getPosition");
                    this.playback_time = playback_time_uint;
                }

                if (!this.starving)
                {
                    result = channel.setVolume(this.volume);
                    // ERRCHECK(result, "channel.setVolume", false);

                    this.ReadTags();
                }
            }

            return this.starving || result != FMOD.RESULT.OK;
        }

        protected override void StreamPausing(bool pause)
        {
            if (channel.hasHandle())
            {
                result = this.channel.setPaused(pause);
                ERRCHECK(result, "channel.setPaused");
            }
        }

        protected override void StreamStopping() { }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            float defFrequency;
            int defPriority;
            result = sound.getDefaults(out defFrequency, out defPriority);
            ERRCHECK(result, "sound.getDefaults");

            LOG(LogLevel.INFO, "Stream samplerate change from {0}, {1}", defFrequency, sound_format);

            result = sound.setDefaults(samplerate, defPriority);
            ERRCHECK(result, "sound.setDefaults");

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}, {1}", samplerate, sound_format);
        }

        public override void SetOutput(int _outputDriverID)
        {
            LOG(LogLevel.INFO, "Setting output to driver {0} ", _outputDriverID);

            result = fmodsystem.System.setDriver(_outputDriverID);
            ERRCHECK(result, "fmodsystem.System.setDriver");

            /*
             * Log output device info
             */
            int od_namelen = 255;
            string od_name;
            System.Guid od_guid;
            int od_systemrate;
            FMOD.SPEAKERMODE od_speakermode;
            int od_speakermodechannels;

            result = fmodsystem.System.getDriverInfo(this.outputDriverID, out od_name, od_namelen, out od_guid, out od_systemrate, out od_speakermode, out od_speakermodechannels);
            ERRCHECK(result, "fmodsystem.System.getDriverInfo");

            LOG(LogLevel.INFO, "Device {0} Info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, od_speakermode, od_speakermodechannels);

            if (this.speakerMode != FMOD.SPEAKERMODE.DEFAULT)
                LOG(LogLevel.INFO, "Device {0} User: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, this.speakerMode, this.numOfRawSpeakers);

            this.outputDriverID = _outputDriverID;
        }
        /// <summary>
        /// Returns playback time in seconds
        /// </summary>
        /// <returns></returns>
        public override double PlaybackTimeSeconds()
        {
            return this.playback_time / 1000;
        }
        #endregion
    }
}