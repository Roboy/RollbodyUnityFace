// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using FMOD;
using UnityEngine;

namespace AudioStream
{
    public class AudioStreamMinimal : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[AudioStreamMinimal]")]
        [Range(0f, 1f)]
        [Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        public float volume = 1f;

        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        #endregion

        /// <summary>
        /// Various calls to system in this method might fail if device list was changed ( and we're rquesting newly added device.. )
        /// </summary>
        /// <param name="_outputDriverID"></param>
        public override void SetOutput(int _outputDriverID)
        {
            LOG(LogLevel.INFO, "Setting output to driver {0} ", _outputDriverID);

            // try to help the system to update its surroundings
            result = fmodsystem.Update();
            ERRCHECK(result, "fmodsystem.System.setDriver", false);

            if (result != RESULT.OK)
                return;

            result = fmodsystem.System.setDriver(_outputDriverID);
            ERRCHECK(result, "fmodsystem.System.setDriver", false);

            // TODO: LOG(LogLevel.WARNING, "Setting output to newly appeared device {0} during runtime is currently not supported for AudioStreamMinimal", _outputDriverID);
            if (result != RESULT.OK)
                return;
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
            ERRCHECK(result, "fmodsystem.System.getDriverInfo", false);

            if (result != RESULT.OK)
                return;

            LOG(LogLevel.INFO, "Device {0} Info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, od_speakermode, od_speakermodechannels);

            if (this.speakerMode != FMOD.SPEAKERMODE.DEFAULT)
                LOG(LogLevel.INFO, "Device {0} User: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, this.speakerMode, this.numOfRawSpeakers);

            this.outputDriverID = _outputDriverID;
        }

        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            float defFrequency;
            int defPriority;
            result = sound.getDefaults(out defFrequency, out defPriority);
            ERRCHECK(result, "sound.getDefaults", false);

            LOG(LogLevel.INFO, "Stream samplerate change from {0}, {1}", defFrequency, sound_format);

            result = sound.setDefaults(samplerate, defPriority);
            ERRCHECK(result, "sound.setDefaults", false);

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}, {1}", samplerate, sound_format);
        }

        protected override void StreamStarting()
        {
            this.SetOutput(this.outputDriverID);

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume", false);
        }

        protected override void StreamStarving()
        {
            if (channel.hasHandle())
            {
                if (!this.starving)
                {
                    result = channel.setVolume(this.volume);
                    // ERRCHECK(result, "channel.setVolume", false);
                }
            }
        }

        protected override void StreamStopping()
        {
        }
    }
}