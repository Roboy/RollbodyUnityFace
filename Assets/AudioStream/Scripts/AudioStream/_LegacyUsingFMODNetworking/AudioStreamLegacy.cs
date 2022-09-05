// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections;
using System.Runtime.InteropServices;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// This is the original - now legacy - version which was using FMOD networking to download streamed audio data, up to version 1.9 of the asset
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioStreamLegacy : AudioStreamLegacyBase
    {
        /// <summary>
        /// autoresolved reference for automatic playback redirection
        /// </summary>
        AudioSourceOutputDevice audioSourceOutputDevice = null;

        // ========================================================================================================================================
        #region Unity lifecycle
        public override IEnumerator Start()
        {
            // setup the AudioSource
            var audiosrc = this.GetComponent<AudioSource>();
            audiosrc.playOnAwake = false;
            audiosrc.Stop();
            audiosrc.clip = null;

            // and check if AudioSourceOutputDevice is present
            this.audioSourceOutputDevice = this.GetComponent<AudioSourceOutputDevice>();

            yield return StartCoroutine(base.Start());

            // TODO: deconstantize
            if (AudioSettings.GetConfiguration().dspBufferSize == 256
                && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                )
                LOG(LogLevel.WARNING, "Consider using other than Best latency setting on Windows with AudioStreamLegacy component - otherwise this usually results in playback fluctuations.");
        }

        byte[] streamDataBytes = null;
        GCHandle streamDataBytesPinned;
        System.IntPtr streamDataBytesPtr = System.IntPtr.Zero;
        float[] oafrDataArr = null; // instance buffer
        FMOD.SOUND_FORMAT stream_sound_format;
        byte stream_bytes_per_sample;
        /// <summary>
        /// Starving flag for Sound::readData is separate from base class
        /// </summary>
        bool _networkStarving = false;
        /// <summary>
        /// Flag indicating end of file and not generic network error
        /// </summary>
        bool eof = false;
        /// <summary>
        /// Ignore first few underruns in PCM callback while warming up
        /// </summary>
        int pcmCallbackWarmup = 0;
        readonly int pcmCallbackWarmupMax = 9;
        /// <summary>
        /// DSP time marker
        /// </summary>
        double dspTime_Last;
        /// <summary>
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any processing here, just return them
        /// No dependency of FMOD state here - rely just on existing available audio data
        /// On all platforms it seems to behave consistently the same (w Best latency): 8x 4096 long data, followed by 1x 2512 long data, repeated
        /// Updates main thread timeout based on the state of the exchange buffer (which - contrary to other statistics - seems to be working)
        /// </summary>
        /// <param name="data"></param>
        void PCMReaderCallback(float[] data)
        {
            // clear the arrays with repeated content..
            Array.Clear(data, 0, data.Length);

            // always update dsp delta
            var dsptime = AudioSettings.dspTime;
            if (this.dspTime_Last == 0)
                this.dspTime_Last = dsptime;

            if (this.isPlaying && !this.isPaused)
            {
                // measure play time
                this.playback_time += (dsptime - this.dspTime_Last);

                this._networkStarving = false;

                // copy out all that's available
                var floats = this.networkAudioQueue.Read(data.Length);
                Array.Copy(floats, data, floats.Length);

                if (floats.Length < data.Length) // signal underrun when there's not enough data
                {
                    this._networkStarving = true;

                    // end sooner if the file ended
                    if (this.eof)
                    {
                        this.starvingRetryCount_FileStopped = AudioStreamLegacyBase.kStarvingRetryCount_FileStopped;
                        LOG(LogLevel.INFO, "Updated starvingRetryCount automatically to {0} frame/s due to possible end of file [{1}]", this.starvingRetryCount_FileStopped.Value, FMOD.Error.String(result));
                    }
                    else
                    {
                        this.starvingRetryCount_FileStopped = null;
                    }
                }
                else
                {
                    if (++this.pcmCallbackWarmup > this.pcmCallbackWarmupMax)
                    {
                        // if not enough data drop timeout immediately to lowest possible value
                        // , but be less aggressive when under 50% of streaming buffer in order to not stall it
                        if (floats.Length < data.Length)
                        {
                            if (this.bufferFillPercentage >= 50)
                            {
                                this.streamTimeoutBase = this.streamTimeout_Min;
                            }
                            else
                            {
                                this.streamTimeoutBase = (this.streamTimeout_Max + this.streamTimeout_Min) / 2.0;
                            }
                        }
                        else
                        {
                            // adjusting network drain timeout according to the state of network/PCM exchange buffer seems to be working across all platforms
                            // , be less aggressive when under 50% of streaming buffer in order to not stall it
                            var br = this.networkAudioQueue.Available() / (float)this.networkAudioQueue.Capacity();

                            if (this.bufferFillPercentage < 50)
                            {
                                this.streamTimeoutBase = this.streamTimeout_Max * br;
                            }
                            else
                            {
                                this.streamTimeoutBase = Mathf.Lerp((float)this.streamTimeout_Min, (float)this.streamTimeout_Max, br);
                            }
                        }

                        if (this.streamTimeoutBase < this.streamTimeout_Min)
                            this.streamTimeoutBase = this.streamTimeout_Min;

                        else if (this.streamTimeoutBase > this.streamTimeout_Max)
                            this.streamTimeoutBase = this.streamTimeout_Max;

                        // There are 10,000 ticks in a millisecond:
                        this.spanTimeout = new TimeSpan((long)(this.streamTimeoutBase * 10000));
                    }
                }
            }

            //  always update dsp delta
            this.dspTime_Last = dsptime;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (this.streamDataBytesPinned.IsAllocated)
                this.streamDataBytesPinned.Free();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamLegacyBase
        protected override void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            this._networkStarving = this.eof = false;

            this.stream_sound_format = sound_format;
            switch (sound_format)
            {
                case FMOD.SOUND_FORMAT.PCM8:
                    this.stream_bytes_per_sample = 1;
                    break;
                case FMOD.SOUND_FORMAT.PCM16:
                    this.stream_bytes_per_sample = 2;
                    break;
                case FMOD.SOUND_FORMAT.PCM24:
                    this.stream_bytes_per_sample = 3;
                    break;
                case FMOD.SOUND_FORMAT.PCM32:
                    this.stream_bytes_per_sample = 4;
                    break;
                case FMOD.SOUND_FORMAT.PCMFLOAT:
                    this.stream_bytes_per_sample = 4;
                    break;
            }

            // start the sound and network
            this.pcmCallbackWarmup = 0;

            /*
             * Setup network read buffer and thread sleep timeout for realtime playback to be as much consistent with actual audio drain as possible / to avoid either long term buffer accumulation, or underflows /
             * 
             * network thread sleep timeout is computed from stream characteristics since data retrieval rate from network should match stream rate/channels and not current Unity/FMOD output settings (AudioClip is created to match stream)
             * 
             * The timeout *HAS* to be below the ideal though due to erroneous behaviour near the end of the file(s) (near last chunk is getting repeated from readData instead playing the rest), and 
             * to account to faster rate with Best latency setting
             * 
             * HOW MUCH below ?
             * Nobody knows.
             * PCM callback size seems to be consistent across platforms, but the rate varies
             */

            // buffer length for decoded audio network read 
            // - this empirically determined value seems to cover all Unity latency settings, short local and network files, and does not result in anomalous network reads and behaviour by fmod (due to either too low, or too high timeout)
            // - anomalous network reads by fmod means repeating chunks of near the end, premature drops of playback as detected in PCM callback and audio drops
            // (basing this on e.g. Unity DSP buffer size didn't help much (with Best latency setting the timeout is too small resulting in anomalies and e.g. data lenght of the buffer in Unity's PCM callback seems to be constant regardless of latency setting anyway (PCM callback data length seems to be constant/consistently the same on all platforms))

            this.readlength = 1024;

            // compute initial, the most optimal byte rate and thread timeout based on that:
            // Bps = readlength * refresh_times_per_second
            // => refresh_times_per_second (Hz) = Bps / readlength
            var output_Bps = (int)this.streamSampleRate * this.streamChannels * this.stream_bytes_per_sample;

            this.streamTimeoutBase = Math.Round((double)this.readlength / (double)output_Bps, 12) * 1000f; // ms

            this.streamTimeout_Max = this.streamTimeoutBase * 1.1;
            this.streamTimeout_Min = this.streamTimeoutBase * 0.6;

            // start time
            this.dspTime_Last = this.playback_time = 0;

            // compute initial TimeSpan ticks for needed sub millisecond resolution
            // There are 10,000 ticks in a millisecond:
            this.spanTimeout = new TimeSpan((long)(this.streamTimeoutBase * 10000));

            LOG(LogLevel.INFO, "Bps: {0} ; based on samplerate: {1}, channels: {2}, stream_bytes_per_sample: {3}", output_Bps, (int)this.streamSampleRate, this.streamChannels, this.stream_bytes_per_sample);
            LOG(LogLevel.INFO, "readData length: {0}, computed optimal network thread timeout: {1} ms)", this.readlength, this.streamTimeoutBase);

            // create network <-> PCM exchange
            this.networkAudioQueue = new ThreadSafeListFloat(output_Bps);

            // start time elapsed
            this.dspTime_Last = this.playback_time = 0;

            // create short (5 sec) looping Unity audio clip based on stream properties
            int loopingBufferSamplesCount = ((int)this.streamSampleRate * this.streamChannels) * 5;

            var asource = this.GetComponent<AudioSource>();
            asource.clip = AudioClip.Create(this.url, loopingBufferSamplesCount, channels, samplerate, true, this.PCMReaderCallback);
            asource.loop = true;

            LOG(LogLevel.INFO, "Created streaming looping audio clip, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, channels, samplerate);

            asource.Play();

            this.StartNetworkLoop();
        }

        // we are not playing the channel and retrieving decoded frames manually via readData, starving check is handled by readData + PCM callback
        protected override bool StreamStarving() { return this._networkStarving; }

        protected override void StreamPausing(bool pause) { }

        protected override void StreamStopping()
        {
            var asource = this.GetComponent<AudioSource>();

            asource.Stop();

            Destroy(asource.clip);

            asource.clip = null;

            this.StopNetworkLoop();
        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.GetComponent<AudioSource>().clip.frequency);

            this.StreamStopping();

            this.StreamStarting((int)samplerate, channels, sound_format);

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }

        public override void SetOutput(int outputDriverId)
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.SetOutput(outputDriverId);
        }
        /// <summary>
        /// Returns playback time in seconds
        /// note: this is Unity PCM read time, which does not correspond to stream time exactly (unfortunately)
        /// </summary>
        /// <returns></returns>
        public override double PlaybackTimeSeconds()
        {
            return this.playback_time;
        }
        #endregion

        // ========================================================================================================================================
        #region Network
        /// <summary>
        /// Flag for the network thread
        /// </summary>
        bool networkLoopRunning;
#if UNITY_WSA
        Task
#else
        Thread
#endif
        networkThread;
        /// <summary>
        /// Incoming network data <-> PCM callback exchange
        /// </summary>
        ThreadSafeListFloat networkAudioQueue = null;
        /// <summary>
        /// Small helpers for demo UI
        /// </summary>
        public float networkAudioQueueFullness
        {
            get
            {
                if (this.networkAudioQueue == null)
                    return 0;

                return (float)this.networkAudioQueue.Available() / (float)this.networkAudioQueue.Capacity();
            }
        }
        public int networkAudioQueueAvailable
        {
            get
            {
                if (this.networkAudioQueue == null)
                    return 0;

                return this.networkAudioQueue.Available();
            }
        }
        /// <summary>
        /// Network buffer readData size
        /// </summary>
        public uint readlength { get; protected set; }
        /// <summary>
        /// Automatically computed network thread timeout based on current stream characteristics to match decoded audio buffer in realtime
        /// </summary>
        public double streamTimeoutBase { get; protected set; }
        /// <summary>
        /// max deviations from the optimal base
        /// </summary>
        double streamTimeout_Min;
        double streamTimeout_Max;
        /// <summary>
        /// Thread timeout for sub millisecond resolution
        /// </summary>
        TimeSpan spanTimeout;

        void StartNetworkLoop()
        {
            this.networkThread =
#if UNITY_WSA
                new Task(new Action(this.NetworkLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.NetworkLoop));
            this.networkThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.networkLoopRunning = true;
            this.networkThread.Start();
        }

        void StopNetworkLoop()
        {
            if (this.networkThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.networkLoopRunning = false;
#if !UNITY_WSA
                this.networkThread.Join();
#endif
                this.networkThread = null;
            }
        }

        void NetworkLoop()
        {
            // setup readData buffer
            LOG(LogLevel.DEBUG, "Allocating new stream buffer of size {0} ({1}b per sample)", this.readlength, this.stream_bytes_per_sample);
            this.streamDataBytes = new byte[this.readlength];

            if (this.streamDataBytesPinned.IsAllocated)
                this.streamDataBytesPinned.Free();
            this.streamDataBytesPinned = GCHandle.Alloc(this.streamDataBytes, GCHandleType.Pinned);

            this.streamDataBytesPtr = this.streamDataBytesPinned.AddrOfPinnedObject();

            // leave the loop running - if there's no data the starvation gets picked up in PCM callback
            // except for EOF condition which we flag here for PCM to pick up, too
            while (this.networkLoopRunning)
            {
                // update the system - 
                result = fmodsystem.Update();
                AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "fmodsystem.Update", false);

                result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "sound.getOpenState", false);

                LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);

                if (result == FMOD.RESULT.OK && openstate == FMOD.OPENSTATE.READY && this.isPlaying && !this.isPaused)
                {
                    uint read = 0;
                    result = this.sound.readData(this.streamDataBytesPtr, this.readlength, out read);
                    // don't log the error - might be EOF

                    // handle error edge case when readLenght size + timeout combination results in this anomaly
                    // detect (probably) looping of short audio and signal EOF manually in that case
                    // (should not happen anymore - but leaving it here is ok)
                    if (this.bufferFillPercentage < 0 || this.bufferFillPercentage > 100)
                        result = FMOD.RESULT.ERR_FILE_EOF;

                    if (result == FMOD.RESULT.OK)
                    {
                        this.eof = false;

                        if (read > 0)
                        {
                            int length = AudioStreamSupport.ByteArrayToFloatArray(this.streamDataBytes, read, this.stream_bytes_per_sample, this.stream_sound_format, ref this.oafrDataArr);

                            var farr = new float[length];
                            Array.Copy(this.oafrDataArr, farr, length);

                            this.networkAudioQueue.Write(farr);

                            this.ReadTags();
                        }
                        else
                        {
                            /*
                             * do some logging but nothing more
                             */
                            AudioStreamSupport.LOG(LogLevel.WARNING, this.logLevel, this.gameObjectName, null, "[NetworkLoop] !(read > 0)");
                        }
                    }
                    else
                    {
                        /*
                         * ERR_FILE_EOF should indicate the end of a file was reached - update the starvation flag, but don't log the error; update starvingRetryCount_FileStopped to a low value in order to stop immediately when PCM picks this up
                         */
                        if (result == FMOD.RESULT.ERR_FILE_EOF)
                        {
                            this.eof = true;

                            // we don't have to necessarily break here - so keep trying - the network condition might evetually revert back to good state
                            // this seems to help when the bandwidth is saturated (e.g. different concurrent download) with occasional audio drops and the connection stabilizing after a while
                            // break;
                        }
                        else
                        {
                            // log all other errors
                            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "Network sound.readData", false);
                        }
                    }
                }
#if UNITY_WSA
                this.networkThread.Wait(this.spanTimeout);
#else
                Thread.Sleep(this.spanTimeout);
#endif
            }
        }
        #endregion
    }
}