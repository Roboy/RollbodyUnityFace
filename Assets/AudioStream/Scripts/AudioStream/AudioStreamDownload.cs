// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections;
using System.IO;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
#endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// AudioStreamDownload is *very* similar to AudioStream, but allows much smaller readData thread timeout set by user, much bigger download buffer for non realtime stream downloading,
    /// does not use Unity's AudioClip's PCM callback and caches streamed data.
    /// Resulting AudioClip is returned in event callback once the download is finished/stopped.
    /// </summary>
    public class AudioStreamDownload : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[AudioStreamDownload]")]
        /// <summary>
        /// Checked from base before setting up the stream in order to skip streaming completely and retrieve previously saved audio from cache instead
        /// </summary>
        [Tooltip("If previously cached download exists for given url/file, the download can be skipped and AudioClip can be created immediately from cached file instead if this is not enabled.\r\nOtherwise the stream is always started and any previously downloaded data is overwritten")]
        public bool overwriteCachedDownload = false;
        /// <summary>
        /// Advanced scripting usage - provide your own ID which will be used in conjuction with url for cache identifier
        /// Allows caching of multiple downloads from the same source/url (e.g. from the same web radio)
        /// </summary>
        [HideInInspector]
        public string uniqueCacheId = string.Empty;

        // TODO: turned off for now since it would need separate thread just for BinaryReader.ReadSingle() for every sinlge read from potentially large decoded file -
        // so probably not going to happen
        // [Tooltip("If true, the AudioClip creation will use *less* memory, but will be 5-10 times slower depending on the platform.\r\n\r\nThis might help if you experience memory related crashes esp. on mobiles when creating long/er clips.")]
        // BinaryReader.ReadBytes is ~ 5x -10x faster than BinaryReader.ReadSingle() loop
        // (small testing mp3 file, Unity 5.5.4, .net 3.5, win_64 ):
        // br.ReadBytes ms:
        // 5, 2, 10, 3, 2, 11, 10, 10, 10, 11
        // br.ReadSingle ms:
        // 72, 60, 60, 69, 60, 63, 59, 60, 68, 66
        // public bool slowClipCreation = false;

        /// <summary>
        /// User event with new AudioClip
        /// The passed clip is always created anew so it's user's resposibility to handle its memory/mamagement - see demo scene for example usage
        /// </summary>
        [Tooltip("User event containing new AudioClip after the download is complete.\r\nThe passed clip is always created anew so it's user's resposibility to handle its memory/mamagement - see demo scene for example usage")]
        public EventWithStringAudioClipParameter OnAudioClipCreated;
        [Tooltip("Decoded noncompressed PCM audio data")]
        /// <summary>
        /// Decoded bytes read from (file)stream
        /// </summary>
        public long decoded_bytes;
        public long decodingToAudioClipTimeInMs;
        [Header("[AudioStreamDownload - Advanced]")]
        [Tooltip("You can force the decoder to run at realtime - to e.g. stream & save netradios - (in other words to be running slower to match realtime audio)\r\nTurning this on enables 'playWhileDownloading' option\r\nWARNING: this option can not be changed after this GO is started/instantiated and can be set current only before entering play mode")]
        public bool realTimeDecoding = false;
        [Tooltip("If turned on, the audio will be played back on 'audioSourceToPlayWhileDownloading' AudioSource via automatically created new AudioClip while download is in progress")]
        public bool playWhileDownloading = false;
        [Tooltip("User AudioSource to play audio being downloaded")]
        public AudioSource audioSourceToPlayWhileDownloading;
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        ThreadSafeListFloat playbackAudioQueue = null;
        protected override IEnumerator Start()
        {
            yield return StartCoroutine(base.Start());

            if (this.playWhileDownloading
                && this.realTimeDecoding // could have been turned off but playWhileDownloading already serialized..
                )
            {
                // setup the AudioSource
                if (this.audioSourceToPlayWhileDownloading != null)
                {
                    this.audioSourceToPlayWhileDownloading.playOnAwake = false;
                    this.audioSourceToPlayWhileDownloading.Stop();
                    this.audioSourceToPlayWhileDownloading.clip = null;
                }
                else
                {
                    LOG(LogLevel.ERROR, "Playback while download requested, but no AudioSource is attached.");
                }
            }
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
                var floats = this.playbackAudioQueue.Read(data.Length);
                Array.Copy(floats, data, floats.Length);
            }
        }
        #endregion
        // ========================================================================================================================================
        #region AudioStreamBase
        public override void SetOutput(int outputDriverId)
        {
            throw new System.NotImplementedException("Please use AudioSourceOutputDevice as separate component if redirection is needed.");
        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.streamSampleRate);

            this.StreamStopping();

            this.StreamStarting();

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }
        /// <summary>
        /// Measures time of just AudioClip creation, i.e. just time from cache to AudioClip
        /// (decoding + saving need not be necessarily always run)
        /// </summary>
        System.Diagnostics.Stopwatch sw;
        protected override void StreamStarting()
        {
            // compute byte rate
            // Bps = readlength * refresh_times_per_second
            // => refresh_times_per_second (Hz) = Bps / readlength
            var output_Bps = (int)this.streamSampleRate * this.streamChannels * this.streamBytesPerSample;

            LOG(LogLevel.INFO, "Bps: {0} ; based on samplerate: {1}, channels: {2}, stream_bytes_per_sample: {3}", output_Bps, (int)this.streamSampleRate, this.streamChannels, this.streamBytesPerSample);

            // create decoder <-> PCM exchange
            this.decoderAudioQueue = new ThreadSafeListFloat(output_Bps);

            // add capture DSP to read decoded data
            result = channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.captureDSP);
            ERRCHECK(result, "channel.addDSP");

            // create cache file and save stream properties

            // - save stream properties into its header/beginning for cached clip retrieval 
            var filepath = AudioStreamSupport.CachedFilePath(this.url, this.uniqueCacheId, ".raw");
            LOG(LogLevel.INFO, "Creating cache file {0} with samplerate: {1}, channels: {2} ({3} bytes per sample)", filepath, this.streamSampleRate, this.streamChannels, sizeof(float));

            this.fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
            this.bw = new BinaryWriter(this.fs);

            this.bw.Write(this.streamSampleRate);
            this.bw.Write(this.streamChannels);

            if (this.playWhileDownloading
                && this.realTimeDecoding // could have been turned off but playWhileDownloading already serialized..
                )
            {
                this.playbackAudioQueue = new ThreadSafeListFloat(output_Bps);

                // create streaming Unity audio clip based on stream properties and play it on user AudioSource
                int loopingBufferSamplesCount = ((int)this.streamSampleRate * this.streamChannels) * 5;

                if (this.audioSourceToPlayWhileDownloading != null)
                {
                    this.audioSourceToPlayWhileDownloading.clip = AudioClip.Create(this.url, loopingBufferSamplesCount, this.streamChannels, AudioSettings.outputSampleRate, true, this.PCMReaderCallback);
                    this.audioSourceToPlayWhileDownloading.loop = true;

                    LOG(LogLevel.INFO, "Created streaming looping audio clip, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, this.streamChannels, this.streamSampleRate);

                    this.audioSourceToPlayWhileDownloading.Play();
                }
                else
                {
                    LOG(LogLevel.ERROR, "Playback while download requested, but no AudioSource is attached.");
                }
            }

            // start the dsp read
            this.StartDownload();
        }

        protected override void StreamStarving() { }

        protected override void StreamStopping()
        {
            // stop and retrieve the clip if dl was running prior
            this.StopDownloadAndCreateAudioClip(false);
        }
        #endregion

        // ========================================================================================================================================
        #region DSP read
        /// <summary>
        /// (Encoded) file size - note: this means final decoded_bytes won't match this
        /// </summary>
        public long? file_size;
        /// <summary>
        /// Flag for the decoder thread
        /// </summary>
        bool decoderLoopRunning;
#if UNITY_WSA
        Task
#else
        Thread
#endif
        decoderThread;
        /// <summary>
        /// File cache for incoming decoder data per url
        /// </summary>
        FileStream fs = null;
        BinaryWriter bw = null;

        void StartDownload()
        {
            // reset download progress and try to determine the size of the file
            this.decoded_bytes = 0;
            this.file_size = this.mediaLength == INFINITE_LENGTH ? null : (long?)this.mediaLength;

            this.decoderThread =
#if UNITY_WSA
                new Task(new Action(this.DecoderLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecoderLoop));
            this.decoderThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.decoderLoopRunning = true;
            this.decoderThread.Start();
        }

        public void StopDownloadAndCreateAudioClip(bool forceRetrievalFromCache)
        {
            if (this.decoderThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decoderLoopRunning = false;
#if !UNITY_WSA
                this.decoderThread.Join();
#endif
                this.decoderThread = null;
            }

            // Process cached data if called after download (cache file writer is still open), or when requesting cache retrieval directly (from startup)
            // ( will be also called from e.g. Stop (scene exit ..) in which case this gets skipped)
            if (this.fs != null || forceRetrievalFromCache)
            {
                this.sw = System.Diagnostics.Stopwatch.StartNew();

                // finish the file
                if (this.fs != null)
                {
                    this.bw.Close();
                    this.fs.Close();

                    this.bw = null;
                    this.fs = null;
                }

                // file saved - create new AudioClip

                // create clip from saved file
                // we'll use BinaryReader.ReadBytes
                // (not using BinaryReader.ReadSingle() loop based on user 'slowClipCreation' choice since it's not used)
                using (var fs = new FileStream(AudioStreamSupport.CachedFilePath(this.url, this.uniqueCacheId, ".raw"), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var br = new BinaryReader(fs))
                    {
                        // retrieve saved stream properties
                        this.streamSampleRate = br.ReadInt32();
                        this.streamChannels = br.ReadInt32();

                        var headerSize = sizeof(int) + sizeof(int);

                        // retrieve bytes audio data
                        var remainingBytes = (int)fs.Length - headerSize;
                        float[] samples = new float[(remainingBytes / sizeof(float))];

                        /*
                        if (this.slowClipCreation)
                        {
                            // use slower method reading directly from the file into samples, which but skips creation of another in-memory buffer for conversion
                            for (var i = 0; i < samples.Length; ++i)
                                samples[i] = br.ReadSingle();
                        }
                        else
                        */
                        {
                            // read the whole file into memory first
                            var bytes = br.ReadBytes(remainingBytes);  // = File.ReadAllBytes(AudioStreamSupport.CachedFilePath(this.url));

                            // convert byte array to audio floats
                            // since it has has known format we can use BlockCopy instead of AudioStreamSupport.ByteArrayToFloatArray which is too slow for large clips
                            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                        }

                        // AudiClip.Create will throw on empty data
                        if (remainingBytes > 0)
                        {
                            // create the clip and set its samples

                            var audioClip = AudioClip.Create(this.url, samples.Length / this.streamChannels, this.streamChannels, AudioSettings.outputSampleRate, false);

                            if (audioClip.SetData(samples, 0))
                            {
                                LOG(LogLevel.INFO, "Created audio clip, samples: {0}, channels: {1}, samplerate: {2}", samples.Length, this.streamChannels, this.streamSampleRate);

                                if (this.OnAudioClipCreated != null)
                                    this.OnAudioClipCreated.Invoke(this.gameObject.name, audioClip);
                            }
                            else
                                LOG(LogLevel.ERROR, "Unable to set the clip data");
                        }
                    }
                }

                this.sw.Stop();
                this.decodingToAudioClipTimeInMs = sw.ElapsedMilliseconds;
                this.sw = null;

                LOG(LogLevel.INFO, "AudioClip from downloaded data created in: {0} ms", this.decodingToAudioClipTimeInMs);
            }
        }

        public int ntimeout { get; protected set; }

        void DecoderLoop()
        {
            var readlength = int.MaxValue;

            this.ntimeout = 1;

            float[] oafrDataArr = null;

            // leave the loop running - if there's no data the starvation gets picked up in base
            while (this.decoderLoopRunning)
            {
                result = this.fmodsystem.Update();
                AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "decoder fmodsystem.Update", false);

                if (result == FMOD.RESULT.OK)
                {
                    // copy out all that's available
                    var paddedSignal = this.decoderAudioQueue.Read(readlength);

                    // detect DSP block trailing padding on finished signal
                    // i mean what _are_ we to do here - at least eliminate the whole 0 block if it starts with 0 and continues till the end
                    var lastNon0At = paddedSignal.Length;
                    float sum0 = 0;
                    for (var i = paddedSignal.Length - 1; i > -1; --i)
                    {
                        sum0 += paddedSignal[i];
                        if (sum0 != 0)
                        {
                            lastNon0At = i;
                            break;
                        }
                    }

                    if (paddedSignal.Length - lastNon0At > 4096) // . TODO: make this a DSP callback block at least
                    {
                        oafrDataArr = new float[lastNon0At + 1];
                        Array.Copy(paddedSignal, 0, oafrDataArr, 0, lastNon0At + 1);
                    }
                    else
                        oafrDataArr = paddedSignal;

                    var length = oafrDataArr.Length;
                    if (length > 0)
                    {
                        this.decoded_bytes += oafrDataArr.Length;

                        // save decoded data to disk

                        // since BinaryWriter needs byte[] we have to convert the floats
                        var barr = new byte[oafrDataArr.Length * sizeof(float)];
                        Buffer.BlockCopy(oafrDataArr, 0, barr, 0, barr.Length);

                        this.bw.Write(barr);

                        // queue the same decoded data for AudioSource for playback
                        if (this.playWhileDownloading
                            && this.realTimeDecoding // could have been turned off but playWhileDownloading already serialized..
                            )
                            this.playbackAudioQueue.Write(oafrDataArr);
                    }
               }
#if UNITY_WSA
                this.decoderThread.Wait(this.ntimeout);
#else
                Thread.Sleep(this.ntimeout);
#endif
            }
        }
        #endregion
    }
}