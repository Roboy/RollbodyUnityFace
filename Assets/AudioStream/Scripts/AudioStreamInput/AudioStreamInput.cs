// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStreamInput : AudioStreamInputBase
    {
        // ========================================================================================================================================
        #region Init && FMOD structures
        protected override IEnumerator Start()
        {
            yield return StartCoroutine(base.Start());
        }
        #endregion

        // ========================================================================================================================================
        #region Recording
        /// <summary>
        /// Create new audio clip with FMOD callback for spatialization. Latency will suffer.
        /// </summary>
        protected override void RecordingStarted()
        {
            var asource = this.GetComponent<AudioSource>();

            // set looping audio clip samples to match the size of recording buffer ( see exinfo.length in base )
            // otherwise Android (weaker devices?) couldn't cope 

            int loopingBufferSamplesCount = recRate * this.channelSize * recChannels;
            var inputs = FMODSystemsManager.AvailableInputs(this.logLevel, this.gameObject.name, this.OnError);
            asource.clip = AudioClip.Create(inputs[this.recordDeviceId].name, loopingBufferSamplesCount, recChannels, this.resampleInput ? recRate : AudioSettings.outputSampleRate, true, this.PCMReaderCallback);
            asource.loop = true;

            LOG(LogLevel.DEBUG, "Created streaming looping recording clip for AudioSource, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, recChannels, recRate);

            asource.Play();
        }
        /// <summary>
        /// Provide recording data for FMOD callback
        /// </summary>
        protected override void RecordingUpdate()
        {
            // keep record update loop running even if paused
            this.UpdateRecordBuffer();
        }

        protected override void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
                asource.Stop();
        }
        /// <summary>
        /// Satisfy AudioClip data requests
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any addition/filtering here
        /// </summary>
        /// <param name="data"></param>
        void PCMReaderCallback(float[] data)
        {
            if (this.isRecording && sound.hasHandle())
            {
                // always drain incoming buffer
                var fArr = this.GetAudioOutputBuffer((uint)data.Length);

                // if paused don't read from it
                if (this.isPaused)
                    goto zero;

                int length = fArr.Length;
                for (int i = 0; i < length; ++i)
                    data[i] = fArr[i] * this.gain;

                return;
            }

            zero:
            {
                System.Array.Clear(data, 0, data.Length);
            }
        }
        #endregion
    }
}
