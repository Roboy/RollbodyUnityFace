// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using Concentus.Common;
using Concentus.Structs;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Base abstract class for network client
    /// Provides audio decoding and queuing, leaving newtork implementation for its descendant 
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public abstract class AudioStreamNetworkClient : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Required descendant's implementation
        protected abstract ThreadSafeQueue<byte[]> networkQueue { get; set; }
        #endregion

        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Header("[Audio]")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Silences audio after processing (just convenience if needed)")]
        public bool listenHere = true;

        [Header("[Decoder]")]
        [Range(0, 10)]
        public int resamplerQuality = 10;

        [Tooltip("You can increase the encoder thread priority if needed, but it's usually ok to leave it even below default Normal depending on how network and main thread perform")]
        public System.Threading.ThreadPriority decoderThreadPriority = System.Threading.ThreadPriority.Normal;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnConnected;
        public EventWithStringParameter OnDisonnected;
        public EventWithStringStringParameter OnError;
        #endregion
        #endregion

        // ========================================================================================================================================
        #region Non editor
        public int clientSampleRate
        {
            get;
            private set;
        }
        public int clientChannels
        {
            get;
            private set;
        }
        public int serverSampleRate
        {
            get;
            protected set;
        }
        public int serverChannels
        {
            get;
            protected set;
        }
        public string lastErrorString
        {
            get;
            protected set;
        }
        #endregion

        // ========================================================================================================================================
        #region Opus decoder

        OpusDecoder opusDecoder = null;
        SpeexResampler speexResampler = null;
        /// <summary>
        /// Last detected frame size.
        /// </summary>
        public int frameSize
        {
            get;
            private set;
        }
        /// <summary>
        /// Decode Info
        /// </summary>
        public Concentus.Enums.OpusBandwidth opusBandwidth
        {
            get;
            private set;
        }
        public Concentus.Enums.OpusMode opusMode
        {
            get;
            private set;
        }
        public int opusNumFramesPerPacket
        {
            get;
            private set;
        }
        public int opusNumSamplesPerFrame
        {
            get;
            private set;
        }
        /// <summary>
        /// Set once first audio packet is processed
        /// </summary>
        public bool decoderRunning
        {
            get;
            private set;
        }
        /// <summary>
        /// Starts decoding; expects all parameters to be set up prior
        /// </summary>
        protected void StartDecoder()
        {
            this.opusDecoder = new OpusDecoder(AudioStreamNetworkSource.opusSampleRate, this.serverChannels);
            this.speexResampler = new SpeexResampler(this.serverChannels, this.serverSampleRate, this.clientSampleRate, this.resamplerQuality);

            this.decodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.DecodeLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecodeLoop));
                this.decodeThread.Priority = this.decoderThreadPriority;
#endif
            this.decoderRunning = true;
            this.decodeThread.Start();

            this.capturedAudioFrame = false;

            if (this.@as != null)
                @as.Play();

            LOG(LogLevel.INFO, "Started decoder received server samplerate: {0}, channels: {1}, client samplerate: {2}, channels: {3}", this.serverSampleRate, this.serverChannels, this.clientSampleRate, this.clientChannels);
        }

        protected void StopDecoder()
        {
            if (this.@as != null)
                @as.Stop();

            this.capturedAudioFrame = false;

            if (this.decodeThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decoderRunning = false;
#if !UNITY_WSA
                this.decodeThread.Join();
#endif
                this.decodeThread = null;
            }

            this.speexResampler = null;
            this.opusDecoder = null;
        }

        #endregion

        // ========================================================================================================================================
        #region Decoder loop
        /// <summary>
        /// audioQueue with max capacity
        /// </summary>
        ThreadSafeQueue<float[]> audioQueue = new ThreadSafeQueue<float[]>(100);
        float[] fArr;
        short[] resampledBuffer = null;
#if UNITY_WSA
        Task
#else
        Thread
#endif
        decodeThread = null;
        /// <summary>
        /// Continuosly enqueues (decoded) signal into audioQueue
        /// </summary>
        void DecodeLoop()
        {
            while (this.decoderRunning)
            {
                var networkPacket = this.networkQueue.Dequeue();

                if (networkPacket != null)
                {
                    if (this.opusDecoder == null || this.speexResampler == null)
                        continue;

                    // Normal decoding

                    this.frameSize = OpusPacketInfo.GetNumSamples(networkPacket, 0, networkPacket.Length, AudioStreamNetworkSource.opusSampleRate);
                    this.opusBandwidth = OpusPacketInfo.GetBandwidth(networkPacket, 0);
                    this.opusMode = OpusPacketInfo.GetEncoderMode(networkPacket, 0);
                    this.serverChannels = OpusPacketInfo.GetNumEncodedChannels(networkPacket, 0);
                    this.opusNumFramesPerPacket = OpusPacketInfo.GetNumFrames(networkPacket, 0, networkPacket.Length);
                    this.opusNumSamplesPerFrame = OpusPacketInfo.GetNumSamplesPerFrame(networkPacket, 0, AudioStreamNetworkSource.opusSampleRate);

                    // keep 2 channels here - decoder can't cope with 1 channel only when e.g. decreased quality
                    short[] decodeBuffer = new short[this.frameSize * 2];

                    // frameSize == thisFrameSize here 
                    int thisFrameSize = this.opusDecoder.Decode(networkPacket, 0, networkPacket.Length, decodeBuffer, 0, this.frameSize, false);

                    if (thisFrameSize > 0)
                    {
                        // resample audio for destination output samplerate if needed
                        if (this.clientSampleRate != this.serverSampleRate)
                        {
                            if (this.resampledBuffer == null)
                            {
                                var ratesRatio = (float)this.clientSampleRate / (float)this.serverSampleRate;
                                var rsize = Mathf.CeilToInt((float)decodeBuffer.Length * ratesRatio) + 2; // + one off error

                                this.resampledBuffer = new short[rsize];
                            }

                            int in_len = this.frameSize;
                            int out_len = this.frameSize;

                            this.speexResampler.ProcessInterleaved(decodeBuffer, 0, ref in_len, this.resampledBuffer, 0, ref out_len);

                            // take only actually processed frames;-
                            // keep 2 channels here - decoder can't cope with 1 channel only when e.g. decreased quality

                            short[] resampledFrame = new short[out_len * 2];
                            System.Array.Copy(this.resampledBuffer, resampledFrame, resampledFrame.Length);

                            AudioStreamSupport.ShortArrayToFloatArray(resampledFrame, (uint)resampledFrame.Length, ref this.fArr);

                            this.audioQueue.Enqueue(this.fArr);
                        }
                        else
                        {
                            AudioStreamSupport.ShortArrayToFloatArray(decodeBuffer, (uint)decodeBuffer.Length, ref this.fArr);
                            this.audioQueue.Enqueue(this.fArr);
                        }
                    }
                }
                //else
                //{
                //    // packet loss path not taken here since decoding loop runs usually much faster than audio loop

                //    this.frameSize = 960;
                //    this.serverChannels = 2;

                //    float[] decodeBuffer = new float[this.frameSize * this.serverChannels];

                //    // int thisFrameSize = 
                //    this.opusDecoder.Decode(null, 0, 0, decodeBuffer, 0, this.frameSize, true);

                //    this.audioQueue.Enqueue(decodeBuffer);
                //}

                // don't tax CPU continuosly, but decode as fast as possible
#if UNITY_WSA
                this.decodeThread.Wait(1);
#else
                Thread.Sleep(1);
#endif
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        AudioSource @as;

        protected virtual void Awake()
        {
            this.gameObjectName = this.gameObject.name;

            this.@as = this.GetComponent<AudioSource>();

            var ac = AudioSettings.GetConfiguration();
            this.clientSampleRate = ac.sampleRate;
            this.clientChannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

            this.dspBufferSize = ac.dspBufferSize * this.clientChannels;

            // we want to play so I guess want the output to be heard, on iOS too:
            // Since 2017.1 there is a setting 'Force iOS Speakers when Recording' for this workaround needed in previous versions
#if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                AudioStreamSupport.LOG(LogLevel.INFO, LogLevel.INFO, this.gameObject.name, null, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }
#endif
        }

        protected virtual void Start()
        {

        }

        protected virtual void Update()
        {
            this.@as.volume = this.volume;

#if !UNITY_WSA
            if (this.decodeThread != null)
                this.decodeThread.Priority = this.decoderThreadPriority;
#endif
        }

        BasicBuffer<float> outputAudioSamples = new BasicBuffer<float>(10000);
        public bool capturedAudioFrame { get; private set; }
        public int capturedAudioSamples { get { return this.outputAudioSamples.Available(); } }
        public int dspBufferSize { get; private set; }

        void OnAudioFilterRead(float[] data, int channels)
        {
            var samples = this.audioQueue.Dequeue();
            if (samples != null)
                this.outputAudioSamples.Write(samples);

            if (this.outputAudioSamples.Available() >= data.Length)
            {
                var floats = this.outputAudioSamples.Read(data.Length);
                System.Array.Copy(floats, data, data.Length);
                this.capturedAudioFrame = true;
            }
            else
            {
                // not enough frames arrived
                this.capturedAudioFrame = false;
            }

            if (!this.listenHere)
                System.Array.Clear(data, 0, data.Length);
        }

        protected virtual void OnDestroy()
        {
            this.StopDecoder();
        }

        #endregion

        // ========================================================================================================================================
        #region Support
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        #endregion
    }
}