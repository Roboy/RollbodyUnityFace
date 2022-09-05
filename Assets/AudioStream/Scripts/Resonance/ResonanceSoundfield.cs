// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using UnityEngine;

namespace AudioStream
{
    public class ResonanceSoundfield : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        //[Range(0f, 1f)]
        //[Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        // Leave volume at 1 for channel using Resonance gain.
        float volume = 1f;

        [Header("[System]")]
        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        #endregion

        // ========================================================================================================================================
        #region Resonance Plugin + Resonance parameters
        ResonancePlugin resonancePlugin = null;

        [Header("[3D Settings]")]
        /// <summary>
        /// 
        /// </summary>
        public Transform listener;

        [Range(-80f, 24f)]
        [Tooltip("Gain")]
        public float gain = 0f;

        /// <summary>
        /// previous positions for velocity
        /// </summary>
		Vector3 last_relative_position = Vector3.zero;
        Vector3 last_position = Vector3.zero;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            if (this.listener == null)
                this.listener = Camera.main.transform;

            yield return StartCoroutine(base.Start());

            while (!channel.hasHandle())
                yield return null;

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;
        }

        void Update()
        {
            // update source position for resonance plugin
            if (this.resonancePlugin != null)
            {
                this.resonancePlugin.ResonanceSoundfield_SetGain(this.gain);

                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.resonancePlugin.ResonanceSoundfield_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    );
            }
        }

        public override void OnDestroy()
        {
            // was even started
            if (this.resonancePlugin != null)
            {
                // remove DSPs first (if e.g. changing scene directly)
                this.StreamStopping();

                this.resonancePlugin.Release();
                this.resonancePlugin = null;
            }

            base.OnDestroy();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        protected override void StreamStarting()
        {
            this.SetOutput(this.outputDriverID);

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume", false);

            /*
             *
             */
            this.resonancePlugin = new ResonancePlugin(fmodsystem.System, this.logLevel);

            this.resonancePlugin.ResonanceListener_SetRoomProperties();

            /*
             * Add Resonance DSPs to the default channel when started.
             */
            channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonancePlugin.ResonanceListener_DSP);
            ERRCHECK(result, "channel.addDSP", false);

            channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonancePlugin.ResonanceSoundfield_DSP);
            ERRCHECK(result, "channel.addDSP", false);
        }

        protected override void StreamStarving()
        {
            if (channel.hasHandle())
            {
                if (!starving)
                {
                    result = channel.setVolume(this.volume);
                    //ERRCHECK(result, "channel.setVolume", false);
                }
            }
        }

        protected override void StreamStopping()
        {
            if (channel.hasHandle())
            {
                if (this.resonancePlugin != null)
                {
                    result = channel.removeDSP(this.resonancePlugin.ResonanceListener_DSP);
                    // channel object handle seems to be not reliable when using plugin (?)
                    // ERRCHECK(result, "channel.removeDSP", false);

                    result = channel.removeDSP(this.resonancePlugin.ResonanceSoundfield_DSP);
                    // channel object handle seems to be not reliable when using plugin (?)
                    // ERRCHECK(result, "channel.removeDSP", false);
                }
            }
        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
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

        public override void SetOutput(int _outputDriverID)
        {
            LOG(LogLevel.INFO, "Setting output to driver {0} ", _outputDriverID);

            result = fmodsystem.System.setDriver(_outputDriverID);
            ERRCHECK(result, "fmodsystem.System.setDriver", false);

            // TODO: LOG(LogLevel.WARNING, "Setting output to newly appeared device {0} during runtime is currently not supported for AudioStreamMinimal", _outputDriverID);
            if (result != FMOD.RESULT.OK)
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

            LOG(LogLevel.INFO, "Device {0} Info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, od_speakermode, od_speakermodechannels);

            if (this.speakerMode != FMOD.SPEAKERMODE.DEFAULT)
                LOG(LogLevel.INFO, "Device {0} User: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, this.speakerMode, this.numOfRawSpeakers);

            this.outputDriverID = _outputDriverID;
        }
        #endregion
    }
}