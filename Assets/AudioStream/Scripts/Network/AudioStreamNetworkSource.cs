// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using Concentus.Enums;
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
    /// Base abstract class for network source
    /// Provides audio encoding and queuing, leaving newtork implementation for its descendant 
    /// </summary>
    public abstract class AudioStreamNetworkSource : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("Silences audio after processing")]
        public bool listenHere = false;

        [Tooltip("You can increase the encoder thread priority if needed, but it's usually ok to leave it even below default Normal depending on how network and main thread perform")]
        public System.Threading.ThreadPriority encoderThreadPriority = System.Threading.ThreadPriority.Normal;

        [Header("[Opus codec]")]

        [Range(6, 510)]
        [Tooltip("At very low bitrate codec switches to mono with further optimizations. Rates above 320 are not very practical.")]
        public int bitrate = 128;

        [Range(0, 10)]
        [Tooltip("Higher complexity provides e.g. better stereo resolution.")]
        public int complexity = 10;

        public enum OPUSAPPLICATIONTYPE
        {
            AUDIO
                , RESTRICTED_LOWDELAY
                , VOIP
        }
        [Tooltip("Encoder general aim:\r\nAUDIO - broadcast/high-fidelity application\r\nRESTRICTED_LOWDELAY - lowest-achievable latency, voice modes are not used.\r\nVOIP - VoIP/videoconference applications\r\n\r\nCannot be changed once the encoder has started.")]
        public OPUSAPPLICATIONTYPE opusApplicationType = OPUSAPPLICATIONTYPE.AUDIO;

        public enum RATE
        {
            CBR
                , VBR
                , Constrained_VBR
        }
        [Tooltip("Constatnt/Variable/Constrained variable bitrate")]
        public RATE rate = RATE.CBR;

        public enum OPUSFRAMESIZE
        {
            OPUSFRAMESIZE_120 = 120
                , OPUSFRAMESIZE_240 = 240
                , OPUSFRAMESIZE_480 = 480
                , OPUSFRAMESIZE_960 = 960
                , OPUSFRAMESIZE_1920 = 1920
                , OPUSFRAMESIZE_2880 = 2880
        }
        [Tooltip("The number of samples per channel in the input signal. This must be set such that\r\n\r\n1] is valid for Opus codec encoder\r\n2] be large enough to capture current OnAudioFilterRead buffer continously, and\r\n3] fit into MTU size as allowed by current network infrastructure.\r\n\r\nThe default (960) is usually OK for (Unity) Default DSP buffer size and common LAN routers with 1500 MTU size.")]
        public OPUSFRAMESIZE frameSize = OPUSFRAMESIZE.OPUSFRAMESIZE_960;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringStringParameter OnClientConnected;
        public EventWithStringStringParameter OnClientDisconnected;
        public EventWithStringStringParameter OnError;
        #endregion
        #endregion

        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// (default listen port)
        /// </summary>
        public const int listenPortDefault = 33000;
        /// <summary>
        /// Encoded packets, read by networking descendant with max capacity
        /// </summary>
        protected ThreadSafeQueue<byte[]> networkQueue = new ThreadSafeQueue<byte[]>(100);
        /// <summary>
        /// String prefix for config message
        /// </summary>
        public const string AUDIOSTREAMSOURCE_CONFIG_PREFIX = "AUDIOSTREAMSOURCECONFIG";
        /// <summary>
        /// Current output samplerate and channels
        /// </summary>
        public int serverSampleRate
        {
            get;
            private set;
        }
        public int serverchannels
        {
            get;
            private set;
        }
        /// <summary>
        /// Set once first audio packet is processed
        /// </summary>
        public bool encoderRunning
        {
            get;
            private set;
        }
        public int networkQueueSize
        {
            get { return this.networkQueue.Size(); }
        }
        public int audioSamplesSize
        {
            get { return this.audioSamples.Available(); }
        }
        public int dspBufferSize
        {
            get;
            private set;
        }

        public string lastErrorString
        {
            get;
            protected set;
        }
        #endregion

        // ========================================================================================================================================
        #region Opus encoder

        OpusEncoder opusEncoder;

        /// <summary>
        /// Encoder samplerate (used regardless of actual sample rate for transfer)
        /// </summary>
        public const int opusSampleRate = 48000;

        void UpdateCodecBitrate(int _bitrate)
        {
            this.opusEncoder.Bitrate = (_bitrate * 1024);
        }

        void UpdateCodecComplexity(int _complexity)
        {
            this.opusEncoder.Complexity = _complexity;
        }

        void UpdateCodecVBRMode(RATE _rate)
        {
            this.rate = _rate;
            bool vbr = false, vbr_constrained = false;

            switch (this.rate)
            {
                case RATE.CBR:
                    vbr = vbr_constrained = false;
                    break;
                case RATE.VBR:
                    vbr = true;
                    vbr_constrained = false;
                    break;
                case RATE.Constrained_VBR:
                    vbr = vbr_constrained = true;
                    break;
            }

            this.opusEncoder.UseVBR = vbr;
            this.opusEncoder.UseConstrainedVBR = vbr_constrained;
        }

        void StartEncoder()
        {
            // only 1 or 2 channels permitted for the Opus encoder
            if (this.serverchannels != 1 && this.serverchannels != 2)
            {
                LOG(LogLevel.ERROR, "Unable to create Opus encoder - system channels have to be either 1 or 2");
                return;
            }

            // start the encoder
            OpusApplication opusApplication = OpusApplication.OPUS_APPLICATION_AUDIO;
            switch (this.opusApplicationType)
            {
                case OPUSAPPLICATIONTYPE.AUDIO:
                    opusApplication = OpusApplication.OPUS_APPLICATION_AUDIO;
                    break;
                case OPUSAPPLICATIONTYPE.RESTRICTED_LOWDELAY:
                    opusApplication = OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY;
                    break;
                case OPUSAPPLICATIONTYPE.VOIP:
                    opusApplication = OpusApplication.OPUS_APPLICATION_VOIP;
                    break;
            }
            this.opusEncoder = new OpusEncoder(AudioStreamNetworkSource.opusSampleRate, this.serverchannels, opusApplication);
            this.opusEncoder.EnableAnalysis = true;

            this.opusEncoder.UseInbandFEC = true;

            this.StartEncodeLoop();

            LOG(LogLevel.INFO, "Created OPUS encoder {0} samplerate, {1} channels, {2}", AudioStreamNetworkSource.opusSampleRate, this.serverchannels, opusApplication);
        }
#if UNITY_WSA
        Task
#else
        Thread
#endif
        encodeThread;
        #endregion

        // ========================================================================================================================================
        #region Encoder loop
        void StartEncodeLoop()
        {
            this.encodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.EncodeLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.EncodeLoop));
            this.encodeThread.Priority = this.encoderThreadPriority;
#endif
            this.encoderRunning = true;
            this.encodeThread.Start();
        }

        void StopEncodeLoop()
        {
            if (this.encodeThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.encoderRunning = false;
#if !UNITY_WSA
                this.encodeThread.Join();
#endif
                this.encodeThread = null;
            }
        }
        readonly object audioSamplesLock = new object();
        /// <summary>
        /// Samples to be encoded queue
        /// </summary>
        BasicBufferShort audioSamples = new BasicBufferShort(10000); // should be enough (tm) for audio to be picked up by decoder
        /// <summary>
        /// Samples to be encoded
        /// </summary>
        short[] samples2encode = null;
        /// <summary>
        /// encode buffer
        /// </summary>
        byte[] encodeBuffer = null;
        /// <summary>
        /// Packet enqueued
        /// </summary>
        byte[] packet = null;
        void EncodeLoop()
        {
            while (this.encoderRunning)
            {
                int fs = (int)this.frameSize;
                var fsSamples = fs * this.serverchannels;

                if (this.audioSamples.Available() >= fsSamples)
                {
                    // make enough room for decode buffer + 'one off error'/?
                    if (this.encodeBuffer == null || this.encodeBuffer.Length != fsSamples + 2)
                        this.encodeBuffer = new byte[fsSamples + 2];

                    lock (this.audioSamplesLock)
                    {
                        this.samples2encode = this.audioSamples.Read(fsSamples);
                    }

                    // don't allow thread to throw
                    int thisPacketSize = 0;
                    try
                    {
                        thisPacketSize = this.opusEncoder.Encode(this.samples2encode, 0, fs, this.encodeBuffer, 0, this.encodeBuffer.Length); // this throws OpusException on a failure, rather than returning a negative number
                    }
                    catch (System.Exception ex)
                    {
                        LOG(LogLevel.ERROR, "{0} / w bounds {1} {2} {3} ", ex.Message, this.frameSize, this.audioSamples.Available(), this.encodeBuffer.Length);
                    }

                    if (thisPacketSize > 0)
                    {
                        this.packet = new byte[thisPacketSize];
                        System.Array.Copy(this.encodeBuffer, this.packet, thisPacketSize);

                        this.networkQueue.Enqueue(this.packet);
                    }
                }

                // don't tax CPU continuosly, but encode as fast as possible
#if UNITY_WSA
                this.encodeThread.Wait(1);
#else
                Thread.Sleep(1);
#endif
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle

        protected virtual void Awake()
        {
            this.gameObjectName = this.gameObject.name;

            var ac = AudioSettings.GetConfiguration();
            this.serverSampleRate = ac.sampleRate;
            this.serverchannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

            this.dspBufferSize = ac.dspBufferSize * this.serverchannels;
        }

        protected virtual void Start()
        {
            this.StartEncoder();
        }

        protected virtual void Update()
        {
            if (this.opusEncoder != null)
            {
                this.UpdateCodecBitrate(this.bitrate);
                this.UpdateCodecComplexity(this.complexity);
                this.UpdateCodecVBRMode(this.rate);
            }

#if !UNITY_WSA
            if (this.encodeThread != null)
                this.encodeThread.Priority = this.encoderThreadPriority;
#endif
        }

        short[] samples2write;
        void OnAudioFilterRead(float[] data, int channels)
        {
            AudioStreamSupport.FloatArrayToShortArray(data, (uint)data.Length, ref this.samples2write);

            lock (this.audioSamplesLock)
            {
                this.audioSamples.Write(this.samples2write);
            }

            if (!this.listenHere)
                System.Array.Clear(data, 0, data.Length);
        }

        protected virtual void OnDestroy()
        {
            this.StopEncodeLoop();
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