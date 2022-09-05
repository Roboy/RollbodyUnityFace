// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Streams selected input and processes it via FMOD's supplied Resonance DSP plugin
    /// Right now the channel is played on default output by FMOD, bypassing Unity AudioSource
    /// </summary>
    public class ResonanceInput : AudioStreamInputBase
    {
        // ========================================================================================================================================
        #region Resonance Plugin + Resonance parameters
        ResonancePlugin resonancePlugin = null;

        [Header("[3D Settings]")]
        [Tooltip("If left empty, main camera transform will be considered for listener position")]
        public Transform listener;

        [Range(0f, 360f)]
        [Tooltip("Spread")]
        public float spread = 0f;

        [Range(0f, 10000f)]
        [Tooltip("min distance")]
        public float minDistance = 1f;

        [Range(0f, 10000f)]
        [Tooltip("max distance")]
        public float maxDistance = 500f;

        [Tooltip("rolloff")]
        public ResonancePlugin.DistanceRolloff distanceRolloff = ResonancePlugin.DistanceRolloff.LOGARITHMIC;

        [Range(0f, 10f)]
        [Tooltip("occlusion")]
        public float occlusion = 0f;

        // very narrow forward oriented cone for testing:
        // directivity          -   0.8 -   forward cone only
        // directivitySharpness -   10  -   narrow focused cone

        [Range(0f, 1f)]
        [Tooltip("directivity")]
        public float directivity = 0f;

        [Range(1f, 10f)]
        [Tooltip("directivity sharpness")]
        public float directivitySharpness = 1f;

        [Tooltip("Room is not fully implemented. If OFF a default room will be applied resulting in slight reverb")]
        public bool bypassRoom = true;

        /// <summary>
        /// previous positions for velocity
        /// </summary>
		Vector3 last_relative_position = Vector3.zero;
        Vector3 last_position = Vector3.zero;
        /// <summary>
        /// separate flag for DSPs when Resonance is loaded and DSP are added to desired channel
        /// </summary>
        bool dspRunning = false;

        #endregion
        
        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            if (this.listener == null)
                this.listener = Camera.main.transform;

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;

            yield return StartCoroutine(base.Start());
        }

        protected override void Update()
        {
            // (base updates notification system)
            base.Update();

            if (this.resonancePlugin != null && this.dspRunning)
            {
                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.resonancePlugin.ResonanceSource_SetGain(this.gain);
                this.resonancePlugin.ResonanceSource_SetSpread(this.spread);
                this.resonancePlugin.ResonanceSource_SetMinDistance(this.minDistance);
                this.resonancePlugin.ResonanceSource_SetMaxDistance(this.maxDistance);
                this.resonancePlugin.ResonanceSource_SetDistanceRolloff(this.distanceRolloff);
                this.resonancePlugin.ResonanceSource_SetOcclusion(this.occlusion);
                this.resonancePlugin.ResonanceSource_SetDirectivity(this.directivity);
                this.resonancePlugin.ResonanceSource_SetDirectivitySharpness(this.directivitySharpness);

                this.resonancePlugin.ResonanceSource_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    );

                this.resonancePlugin.ResonanceSource_SetBypassRoom(this.bypassRoom);
            }
        }

        protected override void OnDestroy()
        {
            // was even started
            if (this.resonancePlugin != null)
            {
                // remove DSPs first (if e.g. changing scene directly)
                this.RecordingStopped();
            }

            base.OnDestroy();
        }
        #endregion
        
        // ========================================================================================================================================
        #region FMOD
        /// <summary>
        /// FMOD channel to run DSPs on
        /// </summary>
        FMOD.ChannelGroup master;
        #endregion
        
        // ========================================================================================================================================
        #region AudioStreamInputBase
        protected override void RecordingStarted()
        {
            /*
             *  load Resonance DSPs
             *  has to be just here since it needs a valid system (needs to wait for the base, but base can call record from Start)
             *  so startup will be probably slower overall for now
             */
            this.resonancePlugin = new ResonancePlugin(this.recording_system, this.logLevel);

            /*
             * play the sound along recording
             * that will run the DSP chain
             */
            result = this.recording_system.getMasterChannelGroup(out master);
            ERRCHECK(result, "system.getMasterChannelGroup");

            FMOD.Channel channel;
            result = this.recording_system.playSound(sound, master, false, out channel);
            ERRCHECK(result, "system.playSound");

            result = master.setVolume(this.gain);
            ERRCHECK(result, "master.setVolume");

            /*
             * Add Resonance DSPs to the master channel
             * -: that performs better than adding to just playSound channel (overly/not in sync processing resulting in late echos..)
             */
            result = master.addDSP(0, this.resonancePlugin.ResonanceListener_DSP);
            ERRCHECK(result, "master.addDSP");

            result = master.addDSP(1, this.resonancePlugin.ResonanceSource_DSP);
            ERRCHECK(result, "master.addDSP");

            /*
             * debug into about playing channel
             */
            int channels, realchannels;
            result = this.recording_system.getChannelsPlaying(out channels, out realchannels);
            ERRCHECK(result, "system.getChannel", false);

            LOG(LogLevel.INFO, "Channels of recording device: {0}, real channels: {1}", channels, realchannels);

            this.dspRunning = true;
        }
        /// <summary>
        /// Nothing to do w/ rec buffer since the channel is played directly by FMOD only
        /// </summary>
        protected override void RecordingUpdate()
        {
            result = master.setVolume(this.gain);
            ERRCHECK(result, "master.setVolume");
        }

        protected override void RecordingStopped()
        {
            if (master.hasHandle())
            {
                if (this.resonancePlugin != null)
                {
                    result = master.removeDSP(this.resonancePlugin.ResonanceSource_DSP);
                    ERRCHECK(result, "master.removeDSP", false);

                    result = master.removeDSP(this.resonancePlugin.ResonanceListener_DSP);
                    ERRCHECK(result, "master.removeDSP", false);

                    this.resonancePlugin.Release();
                    this.resonancePlugin = null;
                }
            }

            this.dspRunning = false;
        }
        #endregion
    }
}
