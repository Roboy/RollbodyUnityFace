// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Same as AudioStreamDownload except the whole decoder runs on a sound opened from a memory location with fixed minimal timeout
    /// Can optionally use disk cache while decoding, otherwise is completely in-memory.
    /// Resulting AudioClip is returned in event callback.
    /// </summary>
    public class AudioStreamMemory : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        /// <summary>
        /// Pointer to memory with (encoded) audio data
        /// </summary>
        // (IntPtr location performs much faster and with lower memory overhead than byte[] passed to createSound...)
        [HideInInspector]
        public IntPtr memoryLocation = IntPtr.Zero;
        /// <summary>
        /// Length of the audio data at the memory location, in bytes
        /// </summary>
        [HideInInspector]
        public uint memoryLength = 0;

        [Header("[AudioStreamMemory]")]

        // TODO: xtreme case not used right now
        // [Tooltip("Use disk cache to progressively write decoded audio to.\r\n\r\nShould make sense for extremely large files in order not to pressure memory. It should not make too much difference speed-wise.\r\nDefault OFF")]
        // public bool useDiskCacheForDecoding = false;

        [Tooltip("If specified, will be used as cache identifier for decoded audio, so any future Play() calls will go to cache directly based on it without decoding.\r\nIf not specified, in-memory decoding will always happen.")]
        /// <summary>
        /// If specified, will be used as cache identifier for decoded audio, so any future Play() calls will go to cache directly based on it without decoding.
        /// If not specified, in-memory decoding will always happen
        /// </summary>
        public string cacheIdentifier = string.Empty;

        // TODO: turned 'slowClipCreation' off for now since it would need separate thread just for BinaryReader.ReadSingle() for every sinlge read from potentially large decoded file -
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
        public EventWithStringAudioClipParameter OnAudioClipCreated;
        /// <summary>
        /// Measures time of decoding + AudioClip creation
        /// </summary>
        System.Diagnostics.Stopwatch stopwatch;
        [Tooltip("Simple performance timer measuring time from start to when new AudioClip is created and returned")]
        public long decodingToAudioClipTimeInMs;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            // no point for continuous streaming from memory...
            this.continuosStreaming = false;

            yield return StartCoroutine(base.Start());
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        public override void SetOutput(int outputDriverId)
        {
            throw new System.NotImplementedException();
        }

        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            throw new System.NotImplementedException();
        }

        protected override void StreamStarting()
        {
            this.stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

            this.memorySamples = new List<float>();

            // start the decoding
            this.StartDecoding();
        }

        protected override void StreamStarving() { }

        protected override void StreamStopping()
        {
            // stop and retrieve the clip if decoder was running prior
            this.StopDecodingAndCreateAudioClip(false);

            if (this.stopwatch != null)
            {
                this.stopwatch.Stop();
                this.decodingToAudioClipTimeInMs = stopwatch.ElapsedMilliseconds;
                this.stopwatch = null;

                LOG(LogLevel.INFO, "Decoding including AudioClip creation took: {0} ms", this.decodingToAudioClipTimeInMs);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Decoding
        /// <summary>
        /// Decoded bytes read from (file)stream
        /// </summary>
        [Tooltip("Decoded noncompressed PCM audio data")]
        public long decoded_bytes;
        /// <summary>
        /// Flag for the decoding thread
        /// </summary>
        bool decodingLoopRunning;
#if UNITY_WSA
        Task
#else
        Thread
#endif
        decodingThread;
        /// <summary>
        /// Decoded floats for in-memory decoding
        /// </summary>
        List<float> memorySamples = null;
        void StartDecoding()
        {
            // reset decoding progress and try to determine the size of the file
            this.decoded_bytes = 0;

            this.decodingThread =
#if UNITY_WSA
                new Task(new Action(this.DecodingLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecodingLoop));
            this.decodingThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.decodingLoopRunning = true;
            this.decodingThread.Start();
        }

        public void StopDecodingAndCreateAudioClip(bool forceRetrievalFromCache)
        {
            // stop decoder
            if (this.decodingThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decodingLoopRunning = false;
#if !UNITY_WSA
                this.decodingThread.Join();
#endif
                this.decodingThread = null;
            }
            else
            {
                // if decoder was not running and not to retrieve from cache there's nothing to do
                if (!forceRetrievalFromCache)
                    return;

                UnityEngine.Debug.Assert(this.memorySamples == null);
            }

            // just decoded or retrieved array to keep one .ToArray() call
            float[] samples = null;

            // use cache if cache identifier is set
            // : write on finish, or retrieve from it when requested
            if (!string.IsNullOrEmpty(this.cacheIdentifier))
            {
                var float_size = sizeof(float);

                // write cache file
                if (this.memorySamples != null)
                {
                    samples = this.memorySamples.ToArray();

                    var filepath = AudioStreamSupport.CachedFilePath(this.cacheIdentifier, "", ".raw");

                    LOG(LogLevel.INFO, "Saving to cache at: {0}, samplerate: {1}, channels: {2}, {3} b per sample", filepath, this.streamSampleRate, this.streamChannels, float_size);

                    // create cache file and save stream properties to retrieve them later when needed
                    using (var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (var bw = new BinaryWriter(fs))
                        {
                            bw.Write(this.streamSampleRate);
                            bw.Write(this.streamChannels);

                            // BinaryWriter needs byte[] - we have to convert the floats
                            var barr = new byte[samples.Length * float_size];
                            Buffer.BlockCopy(samples, 0, barr, 0, barr.Length);

                            bw.Write(barr);

                            bw.Close();
                        }

                        fs.Close();
                    }
                }
                else
                {
                    // retrieve cache file
                    // we'll use BinaryReader.ReadBytes
                    // (not using BinaryReader.ReadSingle() loop based on user 'slowClipCreation' choice since it's not used)
                    using (var fs = new FileStream(AudioStreamSupport.CachedFilePath(this.cacheIdentifier, "", ".raw"), FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        using (var br = new BinaryReader(fs))
                        {
                            // retrieve saved stream properties
                            this.streamSampleRate = br.ReadInt32();
                            this.streamChannels = br.ReadInt32();

                            var headerSize = sizeof(int) + sizeof(int);

                            // retrieve bytes audio data
                            var remainingBytes = (int)fs.Length - headerSize;
                            samples = new float[(remainingBytes / sizeof(float))];

                            /*
                            if (this.slowClipCreation)
                            {
                                // use slower method reading directly from the file into samples, which but skips creation of another in-memory buffer for conversion
                                for (var i = 0; i < samples.Length; ++i)
                                {
                                    samples[i] = br.ReadSingle();
                                    print(i + "/" + samples.Length);
                                    yield return null;
                                }
                            }
                            else
                            */
                            {
                                // read the whole file into memory first
                                var bytes = br.ReadBytes(remainingBytes);  // = File.ReadAllBytes(AudioStreamSupport.CachedFilePath(this.memoryLocation.ToString()));

                                // convert byte array to audio floats
                                // since it has has known format (saved in decoding retrieval) we can use BlockCopy instead of AudioStreamSupport.ByteArrayToFloatArray which is too slow for large clips
                                Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                            }

                            br.Close();
                        }

                        fs.Close();
                    }
                }
            }
            else
            {
                samples = this.memorySamples.ToArray();
            }

            // at this point samples contain audio data which is to be set on AudioClip
            // AudiClip.Create will throw on empty data
            if (samples.Length > 0)
            {
                // create the clip and set its samples

                var audioClip = AudioClip.Create(string.Format("Clip requested from memory at {0}{1}", this.memoryLocation, this.memorySamples == null ? ", from cache" : "")
                    , samples.Length / this.streamChannels, this.streamChannels, AudioSettings.outputSampleRate, false);

                if (audioClip.SetData(samples, 0))
                {
                    LOG(LogLevel.INFO, "Created audio clip, samples: {0}, channels: {1}, samplerate: {2}", samples.Length, this.streamChannels, this.streamSampleRate);

                    if (this.OnAudioClipCreated != null)
                        this.OnAudioClipCreated.Invoke(this.gameObject.name, audioClip);
                }
                else
                    LOG(LogLevel.ERROR, "Unable to set the clip data");
            }
            else
            {
                LOG(LogLevel.WARNING, "Received 0 samples");
            }

            // cleanup after / don't run from scene exit
            this.memorySamples = null;
        }

        public int ntimeout { get; protected set; }
        void DecodingLoop()
        {
            var readlength = int.MaxValue;

            this.ntimeout = 1;

            float[] oafrDataArr = null;

            // leave the loop running - if there's no data the starvation gets picked up in base
            while (this.decodingLoopRunning)
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
                        // print(this.oafrDataArr.Length);

                        this.decoded_bytes += oafrDataArr.Length;

                        this.memorySamples.AddRange(oafrDataArr);
                    }
#if UNITY_WSA
                this.decodingThread.Wait(this.ntimeout);
#else
                    Thread.Sleep(this.ntimeout);
#endif
                }
            }
            #endregion
        }
    }
}