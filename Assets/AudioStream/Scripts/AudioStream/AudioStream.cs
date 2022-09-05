// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using FMOD;
using System;
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStream : AudioStreamBase
    {
        /// <summary>
        /// autoresolved reference for automatic playback redirection
        /// </summary>
        AudioSourceOutputDevice audioSourceOutputDevice = null;
        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            yield return StartCoroutine(base.Start());

            // setup the AudioSource
            var audiosrc = this.GetComponent<AudioSource>();
            audiosrc.playOnAwake = false;
            audiosrc.Stop();
            audiosrc.clip = null;

            // and check if AudioSourceOutputDevice is present
            this.audioSourceOutputDevice = this.GetComponent<AudioSourceOutputDevice>();
        }
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any processing here, just return them
        /// No dependency of FMOD state here - rely just on existing provided PCM data
        /// (On all platforms it seems to behave consistently the same (w Best latency): 8x 4096 long data, followed by 1x 2512 long data, repeated)
        /// </summary>
        /// <param name="data"></param>
        void PCMReaderCallback(float[] data)
        {
            // clear the arrays with repeated content..
            Array.Clear(data, 0, data.Length);

            if (this.isPlaying && !this.isPaused)
            {
                // copy out all that's available
                var floats = this.decoderAudioQueue.Read(data.Length);
                Array.Copy(floats, data, floats.Length);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        public override void SetOutput(int outputDriverId)
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.SetOutput(outputDriverId);
        }

        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.GetComponent<AudioSource>().clip.frequency);

            this.StreamStopping();

            this.StreamStarting();

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }

        protected override void StreamStarting()
        {
            // compute byte rate
            // Bps = readlength * refresh_times_per_second
            // => refresh_times_per_second (Hz) = Bps / readlength
            var output_Bps = (int)this.streamSampleRate * this.streamChannels * this.streamBytesPerSample;

            LOG(LogLevel.INFO, "Bps: {0} ; based on samplerate: {1}, channels: {2}, stream_bytes_per_sample: {3}", output_Bps, (int)this.streamSampleRate, this.streamChannels, this.streamBytesPerSample);

            // create decoder <-> PCM exchange
            this.decoderAudioQueue = new ThreadSafeListFloat(output_Bps);

            // add capture DSP to feed AudioSource PCM callback
            result = channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.captureDSP);
            ERRCHECK(result, "channel.addDSP");


            // create short (5 sec) looping Unity audio clip based on stream properties
            int loopingBufferSamplesCount = ((int)this.streamSampleRate * this.streamChannels) * 5;

            var asource = this.GetComponent<AudioSource>();
            asource.clip = AudioClip.Create(this.url, loopingBufferSamplesCount, this.streamChannels, AudioSettings.outputSampleRate, true, this.PCMReaderCallback);
            asource.loop = true;

            LOG(LogLevel.INFO, "Created streaming looping audio clip, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, this.streamChannels, this.streamSampleRate);

            asource.Play();

            return;
        }

        protected override void StreamStarving()
        {
        }

        protected override void StreamStopping()
        {
            var asource = this.GetComponent<AudioSource>();
            asource.Stop();

            Destroy(asource.clip);
            asource.clip = null;

            result = channel.removeDSP(this.captureDSP);
            // ERRCHECK(result, "channel.removeDSP", false); - will ERR_INVALID_HANDLE on finished channel -
        }
        #endregion
    }
}