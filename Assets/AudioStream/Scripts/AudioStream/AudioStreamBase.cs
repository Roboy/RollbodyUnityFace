// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace AudioStream
{
    /// <summary>
    /// Abstract base with download handler, file system for FMOD and playback controls
    /// </summary>
    public abstract partial class AudioStreamBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region UnityWebRequest
        #region Custom certificate handler
#if UNITY_2018_1_OR_NEWER
        /// <summary>
        /// https://docs.unity3d.com/ScriptReference/Networking.CertificateHandler.html
        /// Note: Custom certificate validation is currently only implemented for the following platforms - Android, iOS, tvOS and desktop platforms.
        /// </summary>
        class NoCheckPolicyCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                // Allow all certificates to pass..
                return true;

                /*
                 * optional key check:
                 */

                // var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateData);
                // var pubk = certificate.GetPublicKeyString();
                // Debug.LogFormat("Certificate public key: {0}", pubk);

                // if (pk.ToLower().Equals(PUBLIC_KEY.ToLower())) ..
                // ;
            }
        }
#endif
        #endregion
        #region Custom download handler
        /// <summary>
        /// Custom download handler which writes to injected FMOD filesystem
        /// </summary>
        class ByteStreamDownloadHandler : DownloadHandlerScript
        {
            public uint contentLength = INFINITE_LENGTH;
            public uint downloaded;
            public bool downloadComplete = false;
            /// <summary>
            /// injected the whole class mainly for logging
            /// </summary>
            AudioStreamBase audioStream;
            /// <summary>
            /// Pre-allocated scripted download handler - should eliminate memory allocations
            /// </summary>
            /// <param name="downloadHandlerBuffer"></param>
            /// <param name="audioStreamWithFileSystem"></param>
            public ByteStreamDownloadHandler(byte[] downloadHandlerBuffer, AudioStreamBase audioStreamWithFileSystem)
                : base(downloadHandlerBuffer)
            {
                this.contentLength = INFINITE_LENGTH;
                this.downloaded = 0;
                this.downloadComplete = false;

                this.audioStream = audioStreamWithFileSystem;
            }
            /// <summary>
            /// Required by DownloadHandler base class.
            /// Not used - the data is being written directly into injected audio buffer
            /// </summary>
            /// <returns></returns>
            protected override byte[] GetData()
            {
                return null;
            }
            /// <summary>
            /// Called once per frame when data has been received from the network.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="dataLength"></param>
            /// <returns></returns>
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || data.Length < 1 || dataLength < 1)
                {
                    this.downloadComplete = true;
                    return false;
                }

                // take just given length
                var newData = new byte[dataLength];
                Array.Copy(data, 0, newData, 0, dataLength);

                // Debug.LogFormat("ReceiveData/Writing: {0}", dataLength);

                // write incoming buffer
                this.audioStream.mediaBuffer.Write(newData);
                this.downloaded += (uint)dataLength;

                return true;
            }
            /// <summary>
            /// Called when all data has been received from the server and delivered via ReceiveData.
            /// </summary>
            protected override void CompleteContent()
            {
                this.downloadComplete = true;
            }
            /// <summary>
            /// Called when a Content-Length header is received from the server.
            /// </summary>
            /// <param name="_contentLength"></param>
#if UNITY_2019_1_OR_NEWER
            protected override void ReceiveContentLengthHeader(ulong _contentLength)
            {
                base.ReceiveContentLengthHeader(_contentLength);

                // can be sent more than once but it looks like whatever came last is good enough question mark
                this.audioStream.LOG(LogLevel.INFO, "Received Content length: {0}", _contentLength);
                this.contentLength = (uint)_contentLength;
            }
#else
            protected override void ReceiveContentLength(int _contentLength)
            {
                // can be sent more than once but it looks like whatever came last is good enough question mark
                this.audioStream.LOG(LogLevel.INFO, "Received Content length: {0}", _contentLength);
                this.contentLength = (uint)_contentLength;
            }
#endif
        }
        #endregion
        /*
         * FMOD file system
         */
        public DownloadFileSystemBase mediaBuffer { get; protected set; }
        /*
         * network streams
         */
        ByteStreamDownloadHandler downloadHandler = null;
        UnityWebRequest webRequest = null;
        #endregion

        // ========================================================================================================================================
        #region FMOD filesystem callbacks
        readonly static object fmod_callback_lock = new object();
        /*
            File callbacks
            due to IL2CPP not supporting marshaling delegates that point to instance methods to native code we need to circumvent this via static dispatch
            (similarly as for exinfo for output device )
        */
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_OPEN_CALLBACK))]
        static RESULT Media_Open(FMOD.StringWrapper name, ref uint filesize, ref IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarning("Media_Open userdata == 0");
                    return RESULT.ERR_FILE_EOF;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                // callback was universal but is for network streams only now 
                if (audioStream.mediaType != MEDIATYPE.NETWORK)
                {
                    audioStream.ERRCHECK(RESULT.ERR_INVALID_PARAM, "Custom filesystem works with network streams only", false);
                    return RESULT.ERR_INVALID_PARAM;
                }

                // will 4GB stream be always enough, question mark
                filesize = audioStream.downloadHandler.contentLength;

                audioStream.mediaLength = filesize;
                audioStream.mediaAvailable = 0;

                handle = IntPtr.Zero;

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_open ---------------");

                return FMOD.RESULT.OK;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_CLOSE_CALLBACK))]
        static FMOD.RESULT Media_Close(IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_Close userdata == 0");
                    return RESULT.OK;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_close ---------------");

                return FMOD.RESULT.OK;
            }
        }
        /// <summary>
        /// See below
        /// </summary>
        FMOD.RESULT media_read_lastResult = FMOD.RESULT.OK;
        /*
         * async version of the API
        */
        /// <summary>
        /// flag for Marshal.StructureToPtr 
        /// https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.structuretoptr?view=netframework-4.8
        /// If you use the StructureToPtr<T>(T, IntPtr, Boolean) method to copy a different instance to the memory block at a later time, specify true for fDeleteOld to remove reference counts for reference types in the previous instance. Otherwise, the managed reference types and unmanaged copies are effectively leaked.
        /// </summary>
        // bool fDeleteOld = false;
        /// <summary>
        /// Retrieves any encoded data based on info and immediately satisfies read request
        /// Sets 'media_read_lastResult' for main thread to detect media data shortage
        /// <param name="infoptr"></param>
        /// <param name="userdata"></param>
        /// <returns>FMOD.RESULT.OK if all requested bytes were read</returns>
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCREAD_CALLBACK))]
        static FMOD.RESULT Media_AsyncRead(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncRead userdata == 0");
                    return RESULT.ERR_INVALID_HANDLE;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);


                if (infoptr == IntPtr.Zero)
                {
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                    return audioStream.media_read_lastResult;
                }

                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                // media type is verified while opening

                var downloadBytes = audioStream.mediaBuffer.Read(info.offset, info.sizebytes);
                info.bytesread = (uint)downloadBytes.Length;
                Marshal.Copy(downloadBytes, 0, info.buffer, (int)info.bytesread);

                // ! update the unmanaged data (here esp. bytesread)
                Marshal.StructureToPtr(info, infoptr
                    , false);
                // , fDeleteOld);
                // fDeleteOld = true;

                if (info.bytesread < info.sizebytes)
                {
                    audioStream.LOG(LogLevel.DEBUG, "FED     {0}/{1} bytes, offset {2} (* EOF)", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                }
                else
                {
                    audioStream.LOG(LogLevel.DEBUG,"FED     {0}/{1} bytes, offset {2}", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.OK;
                }

                info.done(infoptr, audioStream.media_read_lastResult);

                return audioStream.media_read_lastResult;
            }
        }
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCCANCEL_CALLBACK))]
        static FMOD.RESULT Media_AsyncCancel(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncCancel userdata == 0");
                    return RESULT.ERR_FILE_DISKEJECTED;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_asynccancel --------------- {0}", infoptr);

                if (infoptr == IntPtr.Zero)
                    return FMOD.RESULT.ERR_FILE_DISKEJECTED;

                // Signal FMOD to wake up, this operation has been cancelled
                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                // Debug.LogFormat("CANCEL {0} bytes, offset {1} PRIORITY = {2}.", info_deptr.sizebytes, info_deptr.offset, info_deptr.priority);

                info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);

                return FMOD.RESULT.ERR_FILE_DISKEJECTED;
            }
        }
        FMOD.FILE_OPEN_CALLBACK fileOpenCallback;
        FMOD.FILE_CLOSE_CALLBACK fileCloseCallback;
        // async version of the API
        FMOD.FILE_ASYNCREAD_CALLBACK fileAsyncReadCallback;
        FMOD.FILE_ASYNCCANCEL_CALLBACK fileAsyncCancelCallback;
        #endregion

        // ========================================================================================================================================
        #region Required descendant's implementation
        /// <summary>
        /// Called immediately after a valid stream has been established
        /// </summary>
        /// <param name="samplerate"></param>
        /// <param name="channels"></param>
        /// <param name="sound_format"></param>
        protected abstract void StreamStarting();
        /// <summary>
        /// Called per frame to determine runtime status of the playback
        /// Channel state for AudioStreamMinimal, channel state + later PCM callback for AudioStream
        /// </summary>
        /// <returns></returns>
        protected abstract void StreamStarving();
        /// <summary>
        /// Called immediately before stopping and releasing the sound
        /// </summary>
        protected abstract void StreamStopping();
        /// <summary>
        /// Called when mid-stream samplerate changed
        /// (very rare, not tested properly)
        /// </summary>
        /// <param name="samplerate"></param>
        /// <param name="channels"></param>
        /// <param name="sound_format"></param>
        protected abstract void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format);
        /// <summary>
        /// Allows setting the output device directly for AudioStreamMinimal, or calls SetOutput on AudioStream's sibling GO component if it is attached
        /// </summary>
        /// <param name="outputDriverId"></param>
        public abstract void SetOutput(int outputDriverId);
        /// <summary>
        /// Return time of the file being played, or -1 (undefined) for streams
        /// </summary>
        /// <returns></returns>
        // TODO: public abstract double MediaTimeSeconds();
        #endregion

        // ========================================================================================================================================
        #region Editor
        /// <summary>
        /// Slightly altered FMOD.SOUND_TYPE for convenience
        /// replaced name of UNKNOWN for AUTODETECT, and omitted the last (MAX) value
        /// 'PLAYLIST' and 'USER' are included to keep easy type correspondence, but are not implemented - if used an exception is thrown when attempting to play
        /// </summary>
        public enum StreamAudioType
        {
            AUTODETECT,      /* let FMOD guess the stream format */

            AIFF,            /* AIFF. */
            ASF,             /* Microsoft Advanced Systems Format (ie WMA/ASF/WMV). */
            DLS,             /* Sound font / downloadable sound bank. */
            FLAC,            /* FLAC lossless codec. */
            FSB,             /* FMOD Sample Bank. */
            IT,              /* Impulse Tracker. */
            MIDI,            /* MIDI. extracodecdata is a pointer to an FMOD_MIDI_EXTRACODECDATA structure. */
            MOD,             /* Protracker / Fasttracker MOD. */
            MPEG,            /* MP2/MP3 MPEG. */
            OGGVORBIS,       /* Ogg vorbis. */
            PLAYLIST,        /* Information only from ASX/PLS/M3U/WAX playlists */
            RAW,             /* Raw PCM data. */
            S3M,             /* ScreamTracker 3. */
            USER,            /* User created sound. */
            WAV,             /* Microsoft WAV. */
            XM,              /* FastTracker 2 XM. */
            XMA,             /* Xbox360 XMA */
            AUDIOQUEUE,      /* iPhone hardware decoder, supports AAC, ALAC and MP3. extracodecdata is a pointer to an FMOD_AUDIOQUEUE_EXTRACODECDATA structure. */
            AT9,             /* PS4 / PSVita ATRAC 9 format */
            VORBIS,          /* Vorbis */
            MEDIA_FOUNDATION,/* Windows Store Application built in system codecs */
            MEDIACODEC,      /* Android MediaCodec */
            FADPCM,          /* FMOD Adaptive Differential Pulse Code Modulation */
        }

        [Header("[Source]")]

        [Tooltip("Audio stream - such as shoutcast/icecast - direct URL or m3u/8/pls playlist URL,\r\nor direct URL link to a single audio file.\r\n\r\nNOTE: it is possible to stream a local file. Pass complete file path with or without the 'file://' prefix in that case. Stream type is ignored in that case.")]
        public string url = string.Empty;
        /// <summary>
        /// Final url as passed to createSound (might be e.g. extracted from a playlist). Currently used only by AudioStreamDownload for determining the file size.
        /// </summary>
        protected string urlFinal = string.Empty;

        [Tooltip("Audio format of the stream\r\n\r\nAutodetect lets FMOD autodetect the stream format and is default and recommended for desktop and Android platforms.\r\n\r\nFor iOS please select correct type - autodetecting there most often does not work.\r\n\r\nBe aware that if you select incorrect format for a given radio/stream you will risk problems such as unability to connect and stop stream.\r\n\r\nFor RAW audio format user must specify at least frequency, no. of channles and byte format.")]
        public StreamAudioType streamType = StreamAudioType.AUTODETECT;

        [Header("[RAW codec parameters]")]
        public FMOD.SOUND_FORMAT RAWSoundFormat = FMOD.SOUND_FORMAT.PCM16;
        public int RAWFrequency = 44100;
        public int RAWChannels = 2;

        [Header("[Setup]")]

        [Tooltip("Output type of decoded audio/stream.\r\nDefault is fine in most cases")]
        public FMOD.SPEAKERMODE speakerMode = FMOD.SPEAKERMODE.DEFAULT;
        [Tooltip("No. of speakers for RAW speaker mode. You must also provide mix matrix for custom setups,\r\nsee remarks at https://www.fmod.com/docs/api/content/generated/FMOD_SPEAKERMODE.html, \r\nand https://www.fmod.com/docs/api/content/generated/FMOD_Channel_SetMixMatrix.html about how to setup the matrix.")]
        // Specify 0 to ignore; when raw speaker mode is selected that defaults to 2 speakers ( stereo ), unless set by user.
        public int numOfRawSpeakers = 0;

        [Tooltip("When checked the stream will play at start. Otherwise use Play() method of this instance.")]
        public bool playOnStart = false;

        [Tooltip("If ON the component will attempt to stream continuosly regardless of any error/s that may occur. This is done automatically by restarting 'Play' method when needed (an error/end of file occurs)\r\nRecommended for streams.\r\nNote: if used with finite sized files the streaming of the file will restart from beginning even when reaching the end, too. You might want to turn this OFF for finite sized files, and check state via OnPlaybackStopped event.\r\n\r\nFlag is ignored when needed, e.g. for AudioStreamMemory component")]
        public bool continuosStreaming = true;

        public enum MEDIABUFFERTYPE
        {
            MEMORY
                , DISK
        }

        [Tooltip("Handling of the media being downloaded (does not apply for local files)\r\n- MEMORY will use small in-memory circular buffer. Seeking in the stream is possible within small few seconds window.\r\n- DISK will use application cache directory to progressively store downloaded data. Seeking is possible within all downloaded audio (up to the currently downloaded point).")]
        public MEDIABUFFERTYPE mediaBufferType = MEDIABUFFERTYPE.MEMORY;

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringBoolParameter OnPlaybackPaused;
        public EventWithStringParameter OnPlaybackStopped;
        public EventWithStringStringObjectParameter OnTagChanged;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Advanced]")]
        [Tooltip("Directly affects decoding, and the size of the initial download until playback is started.\r\n" +
            "Do not change this unless you have problems opening certain files/streams, or you want to speed up initial startup of playback on slower networks.\r\n" +
            "You can try 2-4 kB to capture a file/stream quickly, though you might risk its format might not be recognized correctly in that case.\r\n" +
            "Generally increasing this to some bigger value of few tens kB should help when having trouble opening the stream - this often occurs with e.g. mp3s containing tags with embedded artwork, or when working with different audio formats\r\n\r\n" +
            "Important: Currently Ogg/Vorbis format requires this to be set to the size of the whole file - if that is known/possible - in order to play it (the whole Vorbis file has to be downloaded first)\r\n\r\n" +
            "(technically, it's 'blockalign' parameter of FMOD setFileSystem call: https://fmod.com/resources/documentation-api?version=2.0&page=core-api-system.html#system_setfilesystem)"
            )]
        public uint blockalign = 16 * 1024; // 'blockalign' setFileSystem parameter - default works for most formats. FMOD default is 2048
        [Tooltip("This works in conjuction with previous parameter - several 'blockalign' sized chunks have to be downloaded in order for decoder to properly recognize format, parse tags/artwork and start playback.\r\n" +
            ": total initial download before the playback is started is (blockalign * blockalignDownloadMultiplier) bytes\r\n" +
            "Currently Ogg/Vorbis format needs the whole file to be downloaded first: please set blockalign to file size (if it is/can be known) and this paramter to 1 for Ogg/Vorbis format.\r\n" +
            "\r\n" +
            "Applies only for playback from network - local files are accessed directly, and use 'blockalign' parameter only.")]
        [Range(1, 16)]
        public uint blockalignDownloadMultiplier = 2;
        [Tooltip("Frame count after which the playback is stopped when the network is starving continuosly.\r\nDefault is 60 which for 60 fps means ~ 1 sec.\r\n(only applies to network streams)")]
        public int starvingRetryCount = 60;
        [Tooltip("Recommended to turn off if not needed/handled (e.g. for AudioStreamMemory/AudioStreamDownload)\r\nDefault on")]
        public bool readTags = true;
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region Init && FMOD structures
        /// <summary>
        /// Set once FMOD had been fully initialized
        /// You should always check this flag before starting the playback
        /// </summary>
        public bool ready { get; protected set; }
        [HideInInspector]
        public string fmodVersion;

        protected FMODSystem fmodsystem = null;
        protected FMOD.Sound sound;
        protected FMOD.Channel channel;
        public FMOD.RESULT result { get; protected set; }
        protected FMOD.OPENSTATE openstate = FMOD.OPENSTATE.MAX;

        FMOD.RESULT lastError = FMOD.RESULT.OK;
        /// <summary>
        /// main thread id - for invoking action (texture creation) on the main thread
        /// </summary>
        int mainThreadId;
        /// <summary>
        /// Type of source media - async/read callbacks and processing will differ based on that
        /// </summary>
        public enum MEDIATYPE
        {
            NETWORK
                , FILESYSTEM
                , MEMORY
        }
        /// <summary>
        /// Type of source media - async/read callbacks and processing will differ based on that
        /// </summary>
        MEDIATYPE mediaType;
        /// <summary>
        /// handle to this' ptr for usedata in callbacks
        /// </summary>
        GCHandle gc_thisPtr;
        protected virtual IEnumerator Start()
        {
            this.gc_thisPtr = GCHandle.Alloc(this);

            this.mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);

            this.gameObjectName = this.gameObject.name;

#if AUDIOSTREAM_IOS_DEVICES
            // AllowBluetooth is only valid with AVAudioSessionCategoryRecord and AVAudioSessionCategoryPlayAndRecord
            // DefaultToSpeaker is only valid with AVAudioSessionCategoryPlayAndRecord
            // So user has 'Prepare iOS for recording' and explicitely enable it here
            AVAudioSessionWrapper.UpdateAVAudioSession(false, true);

            // be sure to wait until session change notification
            while (!AVAudioSessionWrapper.IsSessionReady())
                yield return null;
#endif
            /*
             * Create and init default system if needed for all Unity playback
             * or each time a new system for AudioStreamMinimal and the rest
             */
            if (this is AudioStreamMinimal || this is ResonanceSoundfield || this is ResonanceSource)
                this.fmodsystem = FMODSystemsManager.FMODSystem_DirectSound(this.fmodsystem, this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);
            else if (this is AudioStreamMemory)
                this.fmodsystem = FMODSystemsManager.FMODSystem_NoSound_NRT(this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);
            else if (this is AudioStreamDownload)
            {
                if ((this as AudioStreamDownload).realTimeDecoding)
                    this.fmodsystem = FMODSystemsManager.FMODSystem_NoSound_RT(this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);
                else
                    this.fmodsystem = FMODSystemsManager.FMODSystem_NoSound_NRT(this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);
            }
            else
                // AudioStream
                this.fmodsystem = FMODSystemsManager.FMODSystem_NoSound_RT(this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);

            this.fmodVersion = this.fmodsystem.VersionString;



            int rate;
            FMOD.SPEAKERMODE sm;
            int smch;

            result = fmodsystem.System.getSoftwareFormat(out rate, out sm, out smch);
            ERRCHECK(result, "fmodsystem.System.getSoftwareFormat");

            LOG(LogLevel.INFO, "FMOD samplerate: {0}, speaker mode: {1}, num. of raw speakers: {2}", rate, sm, smch);

            // wait for FMDO to catch up to be safe if requested to play immediately [i.e. autoStart]
            // (technically we probably don't need ouput for the memory component...)
            int numDrivers;
            int retries = 0;

            do
            {
                result = fmodsystem.System.getNumDrivers(out numDrivers);
                ERRCHECK(result, "fmodsystem.System.getNumDrivers");

                LOG(LogLevel.INFO, "Got {0} driver/s available", numDrivers);

                if (++retries > 500)
                {
                    var msg = string.Format("There seems to be no audio output device connected");

                    ERRCHECK(FMOD.RESULT.ERR_OUTPUT_NODRIVERS, msg, false);

                    yield break;
                }

                yield return null;

            } while (numDrivers < 1);

            // make a capture DSP when needed
            if (this is AudioStream
                || this is AudioStreamDownload
                || this is AudioStreamMemory)
            {
                // prepare the DSP
                this.dsp_ReadCallback = new DSP_READCALLBACK(AudioStreamBase.DSP_READCALLBACK);
                this.dsp_CreateCallback = new DSP_CREATECALLBACK(AudioStreamBase.DSP_CREATECALLBACK);
                this.dsp_ReleaseCallback = new DSP_RELEASECALLBACK(AudioStreamBase.DSP_RELEASECALLBACK);
                this.dsp_GetParamDataCallback = new DSP_GETPARAM_DATA_CALLBACK(AudioStreamBase.DSP_GETPARAM_DATA_CALLBACK);

                DSP_DESCRIPTION dspdesc = new DSP_DESCRIPTION();

                dspdesc.version = 0x00010000;
                dspdesc.numinputbuffers = 1;
                dspdesc.numoutputbuffers = 1;
                dspdesc.read = this.dsp_ReadCallback;
                dspdesc.create = this.dsp_CreateCallback;
                dspdesc.release = this.dsp_ReleaseCallback;
                dspdesc.getparameterdata = this.dsp_GetParamDataCallback;
                dspdesc.setparameterfloat = null;
                dspdesc.getparameterfloat = null;
                dspdesc.numparameters = 0;
                dspdesc.paramdesc = IntPtr.Zero;
                dspdesc.userdata = GCHandle.ToIntPtr(this.gc_thisPtr);

                result = fmodsystem.System.createDSP(ref dspdesc, out this.captureDSP);
                ERRCHECK(result, "fmodsystem.System.createDSP");
            }

            this.ready = true;

            if (this.playOnStart)
                this.Play();
        }

        #endregion

        // ========================================================================================================================================
        #region Playback
        /// <summary>
        /// undefined length indicator
        /// </summary>
        public const uint INFINITE_LENGTH = uint.MaxValue;
        /// <summary>
        /// Detected media size [bytes] (uint max value indicates undefined/infinite stream)
        /// </summary>
        public uint mediaLength
        {
            get { return this._mediaLength; }
            protected set { this._mediaLength = value; }
        }
        // be nice to default UI:
        private uint _mediaLength = AudioStreamBase.INFINITE_LENGTH;
        /// <summary>
        /// Downloaded overall [bytes]
        /// </summary>
        public uint mediaDownloaded
        {
            get
            {
                return this.downloadHandler != null ? this.downloadHandler.downloaded : this._mediaDownloaded;
            }

            protected set { this._mediaDownloaded = value; }
        }
        private uint _mediaDownloaded;
        /// <summary>
        /// Availabile for playback [bytes]
        /// </summary>
        public uint mediaAvailable
        {
            get
            {
                return this.mediaBuffer != null ? this.mediaBuffer.available : this._mediaAvailable;
            }

            protected set { this._mediaAvailable = value; }
        }
        private uint _mediaAvailable;
        public uint mediaCapacity
        {
            get
            {
                return this.mediaBuffer != null ? this.mediaBuffer.capacity : 0;
            }
        }
        /// <summary>
        /// Infinite stream / finite file indicator
        /// </summary>
        public bool MediaIsInfinite
        {
            get
            {
                return this.mediaLength == INFINITE_LENGTH;
            }
        }
        /// <summary>
        /// bufferFillPercentage has nothing to do with network any more
        /// </summary>
        // [Range(0f, 100f)]
        // [Tooltip("Set during playback. Playback buffer fullness")]
        // public
        uint bufferFillPercentage = 0;
        /// <summary>
        /// User pressed Play (UX updates)
        /// </summary>
        bool isPlayingUser;
        public bool isPlaying
        {
            get
            {
                return this.isPlayingChannel || this.isPlayingUser;
            }
        }
        /// <summary>
        /// Channel playing during playback
        /// </summary>
        bool isPlayingChannel
        {
            get
            {
                bool channelPlaying = false;

                if (this.channel.hasHandle())
                {
                    result = channel.isPlaying(out channelPlaying);
                    // ERRCHECK(result, "channel.isPlaying", false); // - will ERR_INVALID_HANDLE on finished channel -
                }

                return channelPlaying && result == FMOD.RESULT.OK;
            }
        }
        /// <summary>
        /// Channel paused during playback
        /// </summary>
        public bool isPaused
        {
            get
            {
                bool channelPaused = false;
                if (channel.hasHandle())
                {
                    result = this.channel.getPaused(out channelPaused);
                    // ERRCHECK(result, "channel.getPaused", false); // - will ERR_INVALID_HANDLE on finished channel -
                }

                return channelPaused && result == FMOD.RESULT.OK;
            }

            set
            {
                if (channel.hasHandle())
                {
                    result = this.channel.setPaused(value);
                    ERRCHECK(result, "channel.setPaused", false);
                }
            }
        }
        /// <summary>
        /// starving flag is now meaningless - FMOD doesn't seem to be updating it correctly despite FS returning nothing
        /// </summary>
        // [Tooltip("Set during playback.")]
        // public
        protected bool starving = false;
        /// <summary>
        /// Refreshing from media buffer is purely cosmetic
        /// </summary>
        // [Tooltip("Set during playback when stream is refreshing data.")]
        // public
        bool deviceBusy = false;
        [Header("[Playback info]")]
        [Tooltip("Radio station title. Set from PLS playlist.")]
        public string title;
        [Tooltip("Set during playback.")]
        public FMOD.SOUND_FORMAT streamFormat;
        public int streamChannels;
        public byte streamBytesPerSample;
        public int streamSampleRate;
        //: - [Tooltip("Tags supplied by the stream. Varies heavily from stream to stream")]
        Dictionary<string, object> tags = new Dictionary<string, object>();
        public void Play()
        {
            if (this.streamType == StreamAudioType.USER
                || this.streamType == StreamAudioType.PLAYLIST)
            {
                ERRCHECK(FMOD.RESULT.ERR_FORMAT, string.Format("{0} stream type is currently not supported. Please use other than {1} and {2} stream type.", this.streamType, StreamAudioType.USER, StreamAudioType.PLAYLIST), false);
                return;
            }

            if (this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Already playing.");
                return;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.ERROR, "Will not start on disabled GameObject.");
                return;
            }

            /*
             * Check playback from cache
             */
            if (!(this is AudioStreamMemory))
            {
                /*
                 * + url format check
                 */
                if (string.IsNullOrEmpty(this.url))
                {
                    var msg = "Can't stream empty URL";

                    ERRCHECK(FMOD.RESULT.ERR_FILE_NOTFOUND, msg, false);

                    return;
                }

                if (this.url.ToLower().EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) && (this.streamType != StreamAudioType.OGGVORBIS && this.streamType != StreamAudioType.AUTODETECT))
                {
                    var msg = "It looks like you're trying to play OGGVORBIS stream, but have not selected proper 'Stream Type'. This might result in various problems while playing and stopping unsuccessful connection with this setup.";

                    ERRCHECK(FMOD.RESULT.ERR_FORMAT, msg, false);

                    return;
                }

                this.tags = new Dictionary<string, object>();

                // check for early exit if download is not to be overwritten
                if (this is AudioStreamDownload)
                {
                    var asd = this as AudioStreamDownload;
                    var filepath = AudioStreamSupport.CachedFilePath(this.url, (this as AudioStreamDownload).uniqueCacheId, ".raw");

                    if (!asd.overwriteCachedDownload
                        && System.IO.File.Exists(filepath)
                        )
                    {
                        LOG(LogLevel.INFO, "Playing from cache: {0}", filepath);

                        // pair start / stop event to indicate start and stop of download/construction of AudioClip

                        if (this.OnPlaybackStarted != null)
                            this.OnPlaybackStarted.Invoke(this.gameObjectName);

                        // create clip from cache
                        asd.StopDownloadAndCreateAudioClip(true);

                        // and early stop
                        this.StopWithReason(ESTOP_REASON.User);

                        return;
                    }
                }
            }
            else
            {
                // parameters check for AudioStreamMemory
                var asm = this as AudioStreamMemory;
                if (asm.memoryLocation == System.IntPtr.Zero)
                {
                    ERRCHECK(FMOD.RESULT.ERR_INVALID_PARAM, "Set memory location before calling Play", false);
                    return;
                }

                if (asm.memoryLength < 1) // -)
                {
                    ERRCHECK(FMOD.RESULT.ERR_INVALID_PARAM, "Set memory length before calling Play", false);
                    return;
                }

                // retrieve cache is requested and stop immediately
                if (!string.IsNullOrEmpty(asm.cacheIdentifier))
                {
                    var filepath = AudioStreamSupport.CachedFilePath(asm.cacheIdentifier, "", ".raw");

                    if (System.IO.File.Exists(filepath))
                    {
                        LOG(LogLevel.INFO, "Playing from cache: {0}", filepath);

                        // pair start / stop event to indicate start and stop of download/construction of AudioClip

                        if (this.OnPlaybackStarted != null)
                            this.OnPlaybackStarted.Invoke(this.gameObjectName);

                        // create clip from cache
                        asm.StopDecodingAndCreateAudioClip(true);

                        // and early stop
                        this.StopWithReason(ESTOP_REASON.User);

                        return;
                    }
                }
            }

            StartCoroutine(this.PlayCR());
        }

        enum PlaylistType
        {
            PLS
                , M3U
                , M3U8
        }

        PlaylistType? playlistType;

        IEnumerator PlayCR()
        {
            // yield a frame for potential Stop explicit call to finish
            yield return null;

            this.isPlayingUser = true;

            // determine the type of the source media
            if (this is AudioStreamMemory)
                this.mediaType = MEDIATYPE.MEMORY;
            else if (this.url.ToLower().StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                this.mediaType = MEDIATYPE.NETWORK;
            else
                this.mediaType = MEDIATYPE.FILESYSTEM;

            // retrieve playlist / entry
            if (this.mediaType != MEDIATYPE.MEMORY)
            {
                var proxystring = AudioStream_ProxyConfiguration.Instance.ProxyString(false);

                this.urlFinal = this.url;

                this.playlistType = null;

                if (this.url.ToLower().EndsWith("pls", System.StringComparison.OrdinalIgnoreCase))
                    this.playlistType = PlaylistType.PLS;
                else if (this.url.ToLower().EndsWith("m3u", System.StringComparison.OrdinalIgnoreCase))
                    this.playlistType = PlaylistType.M3U;
                else if (this.url.ToLower().EndsWith("m3u8", System.StringComparison.OrdinalIgnoreCase))
                    this.playlistType = PlaylistType.M3U8;

                // TODO:
                // Allow to explicitely set that the link is a playlist
                // e.g. http://yp.shoutcast.com/sbin/tunein-station.pls?id=1593461

                if (this.playlistType.HasValue)
                {
                    string playlist = string.Empty;

                    // allow local playlist
                    if (!this.url.ToLower().StartsWith("http", System.StringComparison.OrdinalIgnoreCase) && !this.url.ToLower().StartsWith("file", System.StringComparison.OrdinalIgnoreCase))
                        this.url = "file://" + this.url;

                    if (!this.url.ToLower().StartsWith("file", System.StringComparison.OrdinalIgnoreCase))
                        LOG(LogLevel.INFO, "Using {0} as proxy for playlist retrieval", string.IsNullOrEmpty(proxystring) ? "[NONE]" : proxystring);

                    //
                    // UnityWebRequest introduced in 5.2, but WWW still worked on standalone/mobile
                    // However, in 5.3 is WWW hardcoded to Abort() on iOS on non secure requests - which is likely a bug - so from 5.3 on we require UnityWebRequest
                    //
#if UNITY_5_3_OR_NEWER
#if UNITY_5_3
                    using (UnityEngine.Experimental.Networking.UnityWebRequest www = UnityEngine.Experimental.Networking.UnityWebRequest.Get(this.url))
#else
                    using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(this.url))
#endif
                    {
                        LOG(LogLevel.INFO, "Retrieving {0}", this.url);

#if UNITY_2017_2_OR_NEWER
                        yield return www.SendWebRequest();
#else
                        yield return www.Send();
#endif

                        if (
#if UNITY_2017_1_OR_NEWER
                            www.isNetworkError
#else
                            www.isError
#endif
                            || !string.IsNullOrEmpty(www.error)
                            )
                        {
                            var msg = string.Format("Can't read playlist from {0} - {1}", this.url, www.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_URL, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                            yield break;
                        }

                        playlist = www.downloadHandler.text;
                    }
#else
                    using (WWW www = new WWW(this.url))
                    {
					    LOG(LogLevel.INFO, "Retrieving {0}", this.url );

                        yield return www;

                        if (!string.IsNullOrEmpty(www.error))
                        {
                            var msg = string.Format("Can't read playlist from {0} - {1}", this.url, www.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_URL, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                
                            yield break;
                        }

                        playlist = www.text;
                    }
#endif
                    // TODO: !HLS
                    // - relative entries
                    // - recursive entries
                    // - AAC - streaming chunks ?

                    if (this.playlistType.Value == PlaylistType.M3U
                        || this.playlistType.Value == PlaylistType.M3U8)
                    {
                        this.urlFinal = this.URLFromM3UPlaylist(playlist);
                        LOG(LogLevel.INFO, "URL from M3U/8 playlist: {0}", this.urlFinal);
                    }
                    else
                    {
                        this.urlFinal = this.URLFromPLSPlaylist(playlist);
                        LOG(LogLevel.INFO, "URL from PLS playlist: {0}", this.urlFinal);
                    }

                    if (string.IsNullOrEmpty(this.urlFinal))
                    {
                        var msg = string.Format("Can't parse playlist {0}", this.url);

                        ERRCHECK(FMOD.RESULT.ERR_FORMAT, msg, false);

                        // pause little bit before possible next retrieval attempt
                        yield return new WaitForSeconds(0.5f);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                        yield break;
                    }
                }

                if (this.mediaType == MEDIATYPE.NETWORK)
                    LOG(LogLevel.INFO, "Using {0} as proxy for streaming", string.IsNullOrEmpty(proxystring) ? "[NONE]" : proxystring);
            }

            // create/open appropriate .net media handlers
            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:

                    // start the request

                    // download handler buffer

                    // size (will fetch mostly less)
                    // several blockAlign size blocks are needed for correct codec detection and decoder start
                    var webRequestBuffer = new byte[Mathf.Max(2048, (int)this.blockalign * (int)this.blockalignDownloadMultiplier)];

                    // decoder buffer to write to
                    switch (this.mediaBufferType)
                    {
                        case MEDIABUFFERTYPE.MEMORY:
                            this.mediaBuffer = new DownloadFileSystemMemoryBuffer((uint)webRequestBuffer.Length);
                            break;

                        case MEDIABUFFERTYPE.DISK:
                            this.mediaBuffer = new DownloadFileSystemCachedFile(AudioStreamSupport.CachedFilePath(this.url, "", ".compressedaudio"), (uint)webRequestBuffer.Length);

                            break;

                        default:
                            ERRCHECK(FMOD.RESULT.ERR_INVALID_HANDLE, "Unknown media buffer type");
                            break;
                    }


                    this.downloadHandler = new ByteStreamDownloadHandler(webRequestBuffer, this);

                    this.webRequest = new UnityWebRequest(this.urlFinal)
                    {
                        disposeDownloadHandlerOnDispose = true,
                        downloadHandler = this.downloadHandler
                    };

                    // new runtime (?) needs to ignore certificate explicitely it seems
#if UNITY_2018_1_OR_NEWER
                    this.webRequest.certificateHandler = new NoCheckPolicyCertificateHandler();
#endif
                    this.webRequest.SendWebRequest();

                    // fill initial dl buffer (if not finished already)
                    while (this.mediaDownloaded < webRequestBuffer.Length
                        && !this.downloadHandler.downloadComplete)
                    {
                        if (this.webRequest.isNetworkError
                            || this.webRequest.isHttpError
                            || !string.IsNullOrEmpty(this.webRequest.error)
                            )
                        {
                            var msg = string.Format("{0}", this.webRequest.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_CONNECT, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                            yield break;
                        }

                        LOG(LogLevel.DEBUG, "Getting initial download data: {0}/{1} [{2} * {3}]", this.mediaDownloaded, webRequestBuffer.Length, this.blockalign, webRequestBuffer.Length / this.blockalign);

                        yield return null;
                    }

                    LOG(LogLevel.INFO, "Downloaded initial data: {0}/{1} [{2} * {3}]", this.mediaDownloaded, webRequestBuffer.Length, this.blockalign, webRequestBuffer.Length / this.blockalign);

                    break;
            }

            // start FMOD decoder

            // If userasyncread callback is specified - userread and userseek will not be called at all, so they can be set to 0 / null.
            // Explicitly create the delegate object and assign it to a member so it doesn't get freed by the garbage collector while it's not being used
            this.fileOpenCallback = new FMOD.FILE_OPEN_CALLBACK(Media_Open);
            this.fileCloseCallback = new FMOD.FILE_CLOSE_CALLBACK(Media_Close);
            this.fileAsyncReadCallback = new FMOD.FILE_ASYNCREAD_CALLBACK(Media_AsyncRead);
            this.fileAsyncCancelCallback = new FMOD.FILE_ASYNCCANCEL_CALLBACK(Media_AsyncCancel);
            /*
             * opening flags for streaming createSound
             */
            // -- FMOD.MODE.IGNORETAGS has to be OFF for MPEG (general mp3s with artwork won't play at all), and doesn't affect other formats - leaving OFF for all -
            // -- use FMOD.MODE.NONBLOCKING to have at least some control over opening the sound which would otherwise lock up FMOD when loading/opening currently impossible formats (hello netstream Vorbis)
            // -- FMOD.MODE.OPENONLY doesn't seem to make a difference
            var flags = FMOD.MODE.CREATESTREAM
                // | FMOD.MODE.OPENONLY
                // | FMOD.MODE.IGNORETAGS
                | FMOD.MODE.NONBLOCKING
                | FMOD.MODE.LOWMEM
                ;

            if (this is AudioStreamMemory)
                // FMOD.MODE.OPENMEMORY_POINT should not duplicate/copy memory (compared to FMOD.MODE.OPENMEMORY)
                flags |= FMOD.MODE.OPENMEMORY_POINT;

            /*
             * pass empty / default CREATESOUNDEXINFO, otherwise it hits nomarshalable unmanaged structure path on IL2CPP 
             */
            var extInfo = new FMOD.CREATESOUNDEXINFO();

            // suggestedsoundtype must be hinted on iOS due to ERR_FILE_COULDNOTSEEK on getOpenState
            // allow any type for local files
            switch (this.streamType)
            {
                case StreamAudioType.AUTODETECT:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.UNKNOWN;
                    break;

                case StreamAudioType.RAW:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.RAW;

                    // raw data needs to ignore audio format and
                    // Use FMOD_CREATESOUNDEXINFO to specify format.Requires at least defaultfrequency, numchannels and format to be specified before it will open.Must be little endian data.
                    flags |= FMOD.MODE.OPENRAW;

                    extInfo.format = this.RAWSoundFormat;
                    extInfo.defaultfrequency = this.RAWFrequency;
                    extInfo.numchannels = this.RAWChannels;

                    break;

                default:
                    extInfo.suggestedsoundtype = (FMOD.SOUND_TYPE)this.streamType;
                    break;
            }

            if (this is AudioStreamMemory)
            {
                // set memory location length for the sound:
                extInfo.length = (this as AudioStreamMemory).memoryLength;
            }

            /*
             * + this ptr as userdata in exinfo/fs callbacks
             */
            extInfo.fileuserdata = GCHandle.ToIntPtr(this.gc_thisPtr);
            extInfo.cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO));

            /* Increase the file buffer size a little bit to account for Internet lag. */
            result = fmodsystem.System.setStreamBufferSize(65536, FMOD.TIMEUNIT.RAWBYTES);
            ERRCHECK(result, "fmodsystem.System.setStreamBufferSize");

            /* 
             * setup 'file system' callbacks for audio data downloaded by unity web reqeust
             * also tags ERR_FILE_COULDNOTSEEK:
                http://stackoverflow.com/questions/7154223/streaming-mp3-from-internet-with-fmod
                https://www.fmod.com/docs/api/content/generated/FMOD_System_SetFileSystem.html
             */
            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:
                    // use async API to have offset in read requests
                    result = fmodsystem.System.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, this.fileAsyncReadCallback, this.fileAsyncCancelCallback, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.System.setFileSystem");

                    break;

                case MEDIATYPE.FILESYSTEM:
                    // check the file and its size -:
                    var binaryReader = new System.IO.BinaryReader(System.IO.File.Open(this.urlFinal, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read));
                    this.mediaLength = this.mediaDownloaded = this.mediaAvailable = (uint)binaryReader.BaseStream.Length;

                    result = fmodsystem.System.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, null, null, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.System.setFileSystem");

                    break;

                case MEDIATYPE.MEMORY:
                    this.mediaLength = this.mediaDownloaded = this.mediaAvailable = (this as AudioStreamMemory).memoryLength;

                    result = fmodsystem.System.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, null, null, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.System.setFileSystem");

                    break;
            }

            /*
             * Start streaming
             */
            LOG(LogLevel.DEBUG, "Creating sound for: {0}...", this.urlFinal);

            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:
                    result = fmodsystem.System.createSound("_-====-_ decoder"
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.System.createSound");

                    // looks like this is needed after setting up the custom filesystem
                    System.Threading.Thread.Sleep(20);

                    break;

                case MEDIATYPE.FILESYSTEM:
                    result = fmodsystem.System.createSound(this.urlFinal
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.System.createSound");

                    break;

                case MEDIATYPE.MEMORY:
                    result = fmodsystem.System.createSound((this as AudioStreamMemory).memoryLocation
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.System.createSound");

                    break;
            }

            // do a graceful stop if our decoding skills are not up to par for now
            // TODO: should be for all result != OK possibly handled at ERR method level
            if (result != FMOD.RESULT.OK)
            {
                this.StopWithReason(ESTOP_REASON.User);
                yield break;
            }

            // Since 2017.1 there is a setting 'Force iOS Speakers when Recording' for this workaround needed in previous versions
#if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                LOG(LogLevel.INFO, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }
#endif
            LOG(LogLevel.INFO, "About to play from: {0}...", this.urlFinal);

            yield return StartCoroutine(this.StreamCR());
        }
        IEnumerator StreamCR()
        {
            var isNetworkSource = this.url.ToLower().StartsWith("http");
            // try few frames if playback can't initiated
            var initialConnectionRetryCount = 30;
            //
            bool streamCaught = false;
            //
            float stopFade = 0;
            //
            uint starvingFrames = 0;

            for (; ; )
            {
                if (this.isPaused)
                    yield return null;

                // FMOD playSound after it was opened
                if (!streamCaught)
                {
                    int c = 0;
                    do
                    {
                        fmodsystem.Update();

                        result = sound.getOpenState(out this.openstate, out this.bufferFillPercentage, out this.starving, out this.deviceBusy);
                        ERRCHECK(result, string.Format("sound.getOpenState {0}", openstate), false);

                        LOG(LogLevel.INFO, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", this.openstate, this.bufferFillPercentage, this.starving, this.deviceBusy);

                        if (result == FMOD.RESULT.OK && (openstate == FMOD.OPENSTATE.READY || openstate == FMOD.OPENSTATE.PLAYING))
                        {
                            /*
                             * stream caught
                             */
                            FMOD.SOUND_TYPE _streamType;
                            int _streamBits;
                            result = sound.getFormat(out _streamType, out this.streamFormat, out this.streamChannels, out _streamBits);
                            ERRCHECK(result, null);

                            float freq; int prio;
                            result = sound.getDefaults(out freq, out prio);
                            ERRCHECK(result, null);

                            // do small sanity check of stream properties too
                            if (
                                this.streamFormat != FMOD.SOUND_FORMAT.NONE
                                && this.streamChannels > 0
                                && _streamBits > 0
                                && freq > 0
                                )
                            {
                                this.streamSampleRate = (int)freq;
                                this.streamBytesPerSample = (byte)(_streamBits / 8);

                                LOG(LogLevel.INFO, "Stream type: {0} format: {1}, {2} channels {3} bits {4} samplerate", _streamType, this.streamFormat, this.streamChannels, _streamBits, this.streamSampleRate);

                                // get master channel group
                                FMOD.ChannelGroup masterChannelGroup;
                                result = fmodsystem.System.getMasterChannelGroup(out masterChannelGroup);
                                ERRCHECK(result, "fmodsystem.System.getMasterChannelGroup");

                                // play the sound
                                result = fmodsystem.System.playSound(sound, masterChannelGroup, false, out channel);
                                ERRCHECK(result, "fmodsystem.System.playSound");

                                this.StreamStarting();

                                streamCaught = true;

                                if (this.OnPlaybackStarted != null)
                                    this.OnPlaybackStarted.Invoke(this.gameObjectName);
                            }

                            break;
                        }
                        else
                        {
                            /*
                             * Unable to stream
                             */
                            if (++c > initialConnectionRetryCount)
                            {
                                if (isNetworkSource)
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Please make sure that correct audio type of stream is selected, network is reachable and possibly check Advanced setting. {0} {1}", result, openstate);
#if UNITY_EDITOR
                                    LOG(LogLevel.ERROR, "If everything seems to be ok, restarting the editor often helps while having trouble connecting to especially Ogg/Vorbis streams. {0} {1}", result, openstate);
#endif
                                }
                                else
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Unrecognized audio type.");
                                }


                                ERRCHECK(FMOD.RESULT.ERR_FILE_BAD, string.Format("Can't start playback{0} {1}", result, openstate), false);

                                // pause little bit before possible next retrieval attempt
                                yield return new WaitForSeconds(0.5f);

                                this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                                yield break;
                            }
                        }

                        yield return new WaitForSeconds(0.1f);

                    } while (result != FMOD.RESULT.OK || openstate != FMOD.OPENSTATE.READY);
                }

                //
                // Updates
                //

                // (cosmetic) network connection check, since absolutely nothing in webRequst or handler indicates any error state when e.g. network is disconnected (tF)
                // leaving it here for now.. 
                if (this.mediaType == MEDIATYPE.NETWORK)
                {
                    if (this.webRequest.isNetworkError
                        || this.webRequest.isHttpError
                        || !string.IsNullOrEmpty(this.webRequest.error)
                        )
                    {
                        var msg = string.Format("{0}", this.webRequest.error);
                        ERRCHECK(FMOD.RESULT.ERR_NET_CONNECT, msg, false);

                        // pause little bit before possible next retrieval attempt
                        yield return new WaitForSeconds(0.5f);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                        yield break;
                    }
                }

                // process actions dispatched on the main thread such as for album art texture creation / calls from callbacks which need to call Unity
                lock (this.executionQueue)
                {
                    if (this.executionQueue.Count > 0)
                    {
                        this.executionQueue.Dequeue().Invoke();
                    }
                }

                // notify tags update
                if (this.tagsChanged)
                {
                    this.tagsChanged = false;

                    if (this.OnTagChanged != null)
                        lock (this.tagsLock)
                            foreach (var tag in this.tags)
                                this.OnTagChanged.Invoke(this.gameObjectName, tag.Key, tag.Value);
                }

                //
                // FMOD update & playing check
                //
                result = fmodsystem.Update();
                ERRCHECK(result, "fmodsystem.Update", false);

                // channel isPlaying is reliable for finite sizes (FMOD will stop channel automatically on finite lengths)
                // for 'infinite' size nothing in FMOD (results from update calls, open state, ...) seems to be indicating any data shortage whatsoever - everything just keeps playing in seemingly correct state, despite returning RESULT.ERR_FILE_EOF from async read ... (tF)

                // result = sound.getOpenState(out openstate, out bufferFillPercentage, out this.starving, out deviceBusy);
                // ERRCHECK(result, "sound.getOpenState", false);
                // LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, this.starving, deviceBusy);

                // in that case check for consecutive unchanged RESULT.ERR_FILE_EOF media_asyncread (~~ downloaded media buffer) results:
                // [ this.downloadHandler.downloaded resp. this.webRequest.downloadedBytes dont'change, too, we picked the above ]
                bool haveEnoughData = true;

                if (this.isPlayingChannel)
                {
                    // Silence the stream until there's data for smooth playback
                    result = channel.setMute(this.starving);
                    ERRCHECK(result, "channel.setMute", false);

                    // update specific
                    this.StreamStarving();

                    // update incoming tags
                    if (!this.starving
                        && this.readTags)
                        this.ReadTags();

                    // count stalled frames for non finite streams - otherwise FMOD should stop channel automatically
                    if (this.mediaType == MEDIATYPE.NETWORK)
                    {
                        // -- initial empty reads (from supposedly end of the stream) are exhausted on opening, so these should occur only after real playback has started --
                        if (this.media_read_lastResult == FMOD.RESULT.ERR_FILE_EOF)
                            starvingFrames++;
                        else
                            starvingFrames = 0;

                        if (starvingFrames >= this.starvingRetryCount
                            && this.downloadHandler.contentLength == INFINITE_LENGTH
                            && this.mediaAvailable < this.blockalign
                            )
                        {
                            haveEnoughData = false;
                            LOG(LogLevel.WARNING, "Download stalled for {0} frames..", starvingFrames);
                        }
                    }
                }

                // end
                if (!this.isPlayingChannel || !haveEnoughData)
                {
                    if (this is AudioStream)
                    {
                        // kill Unity PCM only after some time since it has to yet play some
                        // TODO: try to compute this
                        // this clip length / channels question mark
                        if ((stopFade += Time.deltaTime) > 2f)
                        {
                            LOG(LogLevel.INFO, "Starving frame [playing:{0}({1}), enough media data: {2}]", this.isPlayingChannel, this.media_read_lastResult, haveEnoughData);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                            yield break;
                        }
                    }
                    else
                    {
                        // !PCM callback
                        // channel is stopped/finished
                        LOG(LogLevel.INFO, "Starving frame [playing:{0}({1}), enough media data: {2}]", this.isPlayingChannel, this.media_read_lastResult, haveEnoughData);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                        yield break;
                    }
                }

                yield return null;
            }
        }

        public void Pause(bool pause)
        {
            if (!this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Not playing..");
                return;
            }

            this.isPaused = pause;

            LOG(LogLevel.INFO, "{0}", this.isPaused ? "paused." : "resumed.");

            if (this.OnPlaybackPaused != null)
                this.OnPlaybackPaused.Invoke(this.gameObjectName, this.isPaused);
        }

        bool tagsChanged = false;
        readonly object tagsLock = new object();
        /// <summary>
        /// Reads 1 tag from running stream
        /// Sets flag for CR to update due to threading
        /// </summary>
        protected void ReadTags()
        {
            // Read any tags that have arrived, this could happen if a radio station switches to a new song.
            FMOD.TAG streamTag;
            // Have to use FMOD >= 1.10.01 for tags to work - https://github.com/fmod/UnityIntegration/pull/11

            while (sound.getTag(null, -1, out streamTag) == FMOD.RESULT.OK)
            {
                // do some tag examination and logging for unhandled tag types
                // special FMOD tag type for detecting sample rate change

                var FMODtag_Type = streamTag.type;
                string FMODtag_Name = (string)streamTag.name;
                object FMODtag_Value = null;

                if (FMODtag_Type == FMOD.TAGTYPE.FMOD)
                {
                    // When a song changes, the samplerate may also change, so update here.
                    if (FMODtag_Name == "Sample Rate Change")
                    {
                        // resampling is done via the AudioClip - but we have to recreate it for AudioStream ( will cause small stream disruption but there's probably no other way )
                        // , do it via direct calls without events

                        // float frequency = *((float*)streamTag.data);
                        float[] frequency = new float[1];
                        Marshal.Copy(streamTag.data, frequency, 0, 1);

                        LOG(LogLevel.WARNING, "Stream sample rate changed to: {0}", frequency[0]);

                        // get current sound_format
                        FMOD.SOUND_TYPE _streamType;
                        FMOD.SOUND_FORMAT _streamFormat;
                        int _streamBits;
                        result = sound.getFormat(out _streamType, out _streamFormat, out this.streamChannels, out _streamBits);
                        ERRCHECK(result, "sound.getFormat", false);

                        this.StreamChanged(frequency[0], this.streamChannels, _streamFormat);
                    }
                }
                else
                {
                    switch (streamTag.datatype)
                    {
                        case FMOD.TAGDATATYPE.BINARY:

                            FMODtag_Value = "binary data";

                            // check if it's ID3v2 'APIC' tag for album/cover art
                            if (FMODtag_Type == FMOD.TAGTYPE.ID3V2)
                            {
                                if (FMODtag_Name == "APIC" || FMODtag_Name == "PIC")
                                {
                                    byte[] picture_data;
                                    byte picture_type;

                                    // read all texture bytes into picture_data
                                    this.ReadID3V2TagValue_APIC(streamTag.data, streamTag.datalen, out picture_data, out picture_type);

                                    // since 'There may be several pictures attached to one file, each in their individual "APIC" frame, but only one with the same content descriptor.'
                                    // we store its type alongside tag name and create every texture present
                                    if (picture_data != null)
                                    {
                                        // Load texture on the main thread, if needed
                                        this.LoadTexture_OnMainThread(picture_data, picture_type);
                                    }
                                }
                            }

                            break;

                        case FMOD.TAGDATATYPE.FLOAT:
                            FMODtag_Value = this.ReadFMODTagValue_float(streamTag.data, streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.INT:
                            FMODtag_Value = this.ReadTagValue_int(streamTag.data, streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.STRING:
                            FMODtag_Value = Marshal.PtrToStringAnsi(streamTag.data, (int)streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.STRING_UTF16:
                        case FMOD.TAGDATATYPE.STRING_UTF16BE:
                        case FMOD.TAGDATATYPE.STRING_UTF8:

                            FMODtag_Value = Marshal.PtrToStringAnsi(streamTag.data, (int)streamTag.datalen);
                            break;
                    }

                    // update tags, binary data (texture) is handled separately
                    if (streamTag.datatype != FMOD.TAGDATATYPE.BINARY)
                    {
                        lock (this.tagsLock)
                            this.tags[FMODtag_Name] = FMODtag_Value;
                        this.tagsChanged = true;
                    }
                }

                LOG(LogLevel.INFO, "{0} tag: {1}, [{2}] value: {3}", FMODtag_Type, FMODtag_Name, streamTag.datatype, FMODtag_Value);
            }
        }
        /// <summary>
        /// Total sound length in seconds for playback info
        /// </summary>
        public float SoundLengthInSeconds
        {
            get
            {
                float seconds = 0f;
                if (sound.hasHandle())
                {
                    uint length_time_uint;
                    result = sound.getLength(out length_time_uint, FMOD.TIMEUNIT.MS);
                    // don't spam the console while opening & not ready
                    if (result != RESULT.ERR_NOTREADY)
                        ERRCHECK(result, "sound.getLength", false);

                    seconds = length_time_uint / 1000f;
                }

                return seconds;
            }
        }
        /// <summary>
        /// Approximate the lenght of sound so far from downloaded bytes based on total length of the sound
        /// </summary>
        public float SoundLengthInDownloadedSeconds
        {
            get
            {
                var dlRatio = this.mediaDownloaded / (float)this.mediaLength;
                return this.SoundLengthInSeconds * dlRatio;
            }
        }
        /// <summary>
        /// position in seconds for playback info
        /// (playback time will be slightly ahead in AudioStream due Unity PCM callback latency)
        /// </summary>
        public float PositionInSeconds
        {
            get
            {
                float position = 0f;
                if (this.isPlayingChannel)
                {
                    uint position_ms;
                    result = this.channel.getPosition(out position_ms, FMOD.TIMEUNIT.MS);
                    // ERRCHECK(result, "channel.getPosition", false); // - will ERR_INVALID_HANDLE on finished channel -

                    if (result == FMOD.RESULT.OK)
                        position = position_ms / 1000f;
                }

                return position;
            }
            set
            {
                if (this.isPlayingChannel)
                {
                    uint position_ms = (uint)(value * 1000f);
                    result = this.channel.setPosition(position_ms, FMOD.TIMEUNIT.MS);
                    // ERRCHECK(result, "channel.setPosition", false); // - will ERR_INVALID_POSITION when seeking out of bounds of the lenght of the sound, so e.g. don't
                }
            }
        }
        /// <summary>
        /// [UX] Allow arbitrary position change on networked media with DISK buffer, or any non networked media
        /// </summary>
        public bool IsSeekable
        {
            get
            {
                return this.mediaBufferType == MEDIABUFFERTYPE.DISK || this.mediaType == MEDIATYPE.FILESYSTEM || this.mediaType == MEDIATYPE.MEMORY;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Shutdown
        /// <summary>
        /// Reason if calling Stop to distinguish between user initiad stop, and stop on error/end of file.
        /// </summary>
        enum ESTOP_REASON
        {
            /// <summary>
            /// Just stop and don't perform any recovery actions
            /// </summary>
            User,
            /// <summary>
            /// Error from network, of actually finished file on established connection
            /// Will try to reconnect based on user setting
            /// </summary>
            ErrorOrEOF
        }
        /// <summary>
        /// wrong combination of requested audio type and actual stream type leads to still BUFFERING/LOADING state of the stream
        /// don't release sound and system in that case and notify user
        /// </summary>
        bool unstableShutdown = false;
        /// <summary>
        /// User facing Stop -
        /// - if initiated by user we won't be restarting automatically and ignore replay/reconnect attempt
        /// We'll ignore any state we're in and just straight go ahead with stopping - UnityWebRequest and FMOD seem to got better so interruptions should be ok
        /// </summary>
        public void Stop()
        {
            this.StopWithReason(ESTOP_REASON.User);
        }
        /// <summary>
        /// Called by user, and internally when needed to stop automatically
        /// </summary>
        /// <param name="stopReason"></param>
        void StopWithReason(ESTOP_REASON stopReason)
        {
            LOG(LogLevel.INFO, "Stopping..");

            this.isPlayingUser = false;

            this.StopAllCoroutines();

            this.StreamStopping();

            /*
             * try to release FMOD sound resources
             */

            /*
             * Stop the channel, then wait for it to finish opening before we release it.
             */
            if (channel.hasHandle())
            {
                // try to stop the channel, but don't check any error - might have been already stopped because end was reached
                result = channel.stop();
                // ERRCHECK(result, "channel.stop", false);

                channel.clearHandle();
            }

            /*
             * If the sound is still buffering at this point (but not trying to connect without available connection), we can't do much - namely we can't release sound and system since FMOD deadlocks in this state
             * This happens when requesting wrong audio type for stream.
             */
            this.unstableShutdown = false;

            // system has to be still valid for next calls
            if (fmodsystem != null)
            {
                result = fmodsystem.Update();

                if (result == FMOD.RESULT.OK)
                {
                    if (sound.hasHandle())
                    {
                        result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                        ERRCHECK(result, "sound.getOpenState", false);

                        LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);
                    }

                    if (openstate == FMOD.OPENSTATE.BUFFERING || openstate == FMOD.OPENSTATE.LOADING)
                    {
                        this.unstableShutdown = true;
                        var msg = string.Format("Unstable state while stopping the stream detected - will attempt recovery on release. [{0} {1}]"
                            , openstate
                            , result);

                        LOG(LogLevel.WARNING, msg);
                    }

                    /*
                     * Shut down
                     */
                    System.Threading.Thread.Sleep(10);

                    if (sound.hasHandle() && !this.unstableShutdown)
                    {
                        result = sound.release();
                        ERRCHECK(result, "sound.release", false);

                        sound.clearHandle();
                    }
                }
            }

            // needed to dispose handler + callback
            if (this.webRequest != null)
            {
                this.webRequest.Dispose();
                this.webRequest = null;
            }

            if (this.mediaBuffer != null)
            {
                this.mediaBuffer.CloseStore();
                this.mediaBuffer = null;
            }

            this.mediaAvailable = 0;
            this.starving = false;
            this.deviceBusy = false;
            this.tags = new Dictionary<string, object>();


            // based on stopping reason we either stop completely and call user event handler
            // , attempt to reconect on failed attempt
            // , 
            switch (stopReason)
            {
                case ESTOP_REASON.User:
                    // everything finished - just call user event handler 
                    if (this.OnPlaybackStopped != null)
                        this.OnPlaybackStopped.Invoke(this.gameObjectName);

                    break;

                case ESTOP_REASON.ErrorOrEOF:
                    // we have no way of distinguishing between an actual error, or finished file here
                    // so we need user flag to determine between stop + event, or reconnect

                    if (this.continuosStreaming)
                    {
                        // Coroutine scheduled here should be safe
                        // Start the playback again with existing parameters (few initial checks are skipped)
                        LOG(LogLevel.INFO, "Attempting to restart the connection ('continuous streaming' is ON)...");
                        StartCoroutine(this.PlayCR());
                    }
                    else
                    {
                        if (this.OnPlaybackStopped != null)
                            this.OnPlaybackStopped.Invoke(this.gameObjectName);
                    }

                    break;
            }
        }

        public virtual void OnDestroy()
        {
            // try to stop even when only connecting when component is being destroyed - 
            // if the stream is of correct type the shutdown should be clean 
            this.StopWithReason(ESTOP_REASON.User);

            // : based on FMOD Debug logging : Init FMOD file thread. Priority: 1, Stack Size: 16384, Semaphore: No, Sleep Time: 10, Looping: Yes.
            // wait for file thread sleep time+
            // ^ that seems to correctly release the system 

            if (this.unstableShutdown)
            {
                // attempt to release sound once more after a delay -

                System.Threading.Thread.Sleep(20);

                if (sound.hasHandle())
                {
                    result = sound.release();
                    ERRCHECK(result, "sound.release", false);

                    sound.clearHandle();
                }

                System.Threading.Thread.Sleep(20);
            }
            else
            {
                System.Threading.Thread.Sleep(10);
            }

            if (this is AudioStreamMinimal || this is ResonanceSoundfield || this is ResonanceSource)
                FMODSystemsManager.FMODSystem_DirectSound_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
            else if (this is AudioStreamMemory)
                FMODSystemsManager.FMODSystem_NoSound_NRT_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
            else if (this is AudioStreamDownload)
            {
                if ((this as AudioStreamDownload).realTimeDecoding)
                    FMODSystemsManager.FMODSystem_NoSound_RT_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
                else
                    FMODSystemsManager.FMODSystem_NoSound_NRT_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
            }
            else
                FMODSystemsManager.FMODSystem_NoSound_RT_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);

            if (this.gc_thisPtr.IsAllocated)
                this.gc_thisPtr.Free();
        }
        #endregion

        // ========================================================================================================================================
        #region Support

        public void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            if (throwOnError)
            {
                try
                {
                    AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
                }
                catch (System.Exception ex)
                {
                    // clear the startup flag only when requesting abort on error
                    throw ex;
                }
            }
            else
            {
                AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
            }
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }

        /// <summary>
        /// M3U/8 = its own simple format: https://en.wikipedia.org/wiki/M3U
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromM3UPlaylist(string _playlist)
        {
            using (System.IO.StringReader source = new System.IO.StringReader(_playlist))
            {
                string s = source.ReadLine();
                while (s != null)
                {
                    // If the read line isn't a metadata, it's a file path
                    if ((s.Length > 0) && (s[0] != '#'))
                        return s;

                    s = source.ReadLine();
                }

                return null;
            }
        }

        /// <summary>
        /// PLS ~~ INI format: https://en.wikipedia.org/wiki/PLS_(file_format)
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromPLSPlaylist(string _playlist)
        {
            using (System.IO.StringReader source = new System.IO.StringReader(_playlist))
            {
                string s = source.ReadLine();

                int equalIndex;
                while (s != null)
                {
                    if (s.Length > 4)
                    {
                        // If the read line isn't a metadata, it's a file path
                        if ("FILE" == s.Substring(0, 4).ToUpper())
                        {
                            equalIndex = s.IndexOf("=") + 1;
                            s = s.Substring(equalIndex, s.Length - equalIndex);

                            return s;
                        }
                    }

                    s = source.ReadLine();
                }

                return null;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Tags support
        #region Main thread execution queue for texture creation
        readonly Queue<System.Action> executionQueue = new Queue<System.Action>();
        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        void ScheduleAction(System.Action action)
        {
            lock (this.executionQueue)
            {
                this.executionQueue.Enqueue(action);
            }
        }
        #endregion
        /// <summary>
        /// Calls texture creation if on main thread, otherwise schedules its creation to the main thread
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="picture_bytes"></param>
        /// <param name="picture_type"></param>
        void LoadTexture_OnMainThread(byte[] picture_bytes, byte picture_type)
        {
            // follow APIC tag for now
            var tagName = "APIC_" + picture_type;

            // if on main thread, create & load texture
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == this.mainThreadId)
            {
                this.LoadTexture(tagName, picture_bytes);
            }
            else
            {
                this.ScheduleAction(() => { this.LoadTexture(tagName, picture_bytes); });
            }
        }
        /// <summary>
        /// Creates new texture from raw jpg/png bytes and adds it to the tags dictionary
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="picture_bytes"></param>
        void LoadTexture(string tagName, byte[] picture_bytes)
        {
            Texture2D image = new Texture2D(2, 2);
            image.LoadImage(picture_bytes);

            lock (this.tagsLock)
                this.tags[tagName] = image;
            this.tagsChanged = true;
        }
        // ID3V2 APIC tag specification
        // 
        // Following http://id3.org/id3v2.3.0
        // 
        // Numbers preceded with $ are hexadecimal and numbers preceded with % are binary. $xx is used to indicate a byte with unknown content.
        // 
        // <Header for 'Attached picture', ID: "APIC">
        // Text encoding   $xx
        // MIME type<text string> $00
        // Picture type    $xx
        // Description<text string according to encoding> $00 (00)
        // Picture data<binary data>
        //
        // Frames that allow different types of text encoding have a text encoding description byte directly after the frame size. If ISO-8859-1 is used this byte should be $00, if Unicode is used it should be $01
        //
        // Picture type:
        // $00     Other
        // $01     32x32 pixels 'file icon' (PNG only)
        // $02     Other file icon
        // $03     Cover(front)
        // $04     Cover(back)
        // $05     Leaflet page
        // $06     Media(e.g.lable side of CD)
        // $07     Lead artist/lead performer/soloist
        // $08     Artist/performer
        // $09     Conductor
        // $0A     Band/Orchestra
        // $0B     Composer
        // $0C     Lyricist/text writer
        // $0D     Recording Location
        // $0E     During recording
        // $0F     During performance
        // $10     Movie/video screen capture
        // $11     A bright coloured fish
        // $12     Illustration
        // $13     Band/artist logotype
        // $14     Publisher/Studio logotype

        /// <summary>
        /// Reads value (image data) of the 'APIC' tag as per specification from http://id3.org/id3v2.3.0
        /// We are *hoping* that any and all strings are ASCII/Ansi *only*
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
        protected void ReadID3V2TagValue_APIC(System.IntPtr fromAddress, uint datalen, out byte[] picture_data, out byte picture_type)
        {
            picture_data = null;

            // Debug.LogFormat("IntPtr: {0}, length: {1}", fromAddress, datalen);

            var text_encoding = (byte)Marshal.PtrToStructure(fromAddress, typeof(byte));
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + 1);
            datalen--;

            // Debug.LogFormat("text_encoding: {0}", text_encoding);

            // Frames that allow different types of text encoding have a text encoding description byte directly after the frame size. If ISO-8859-1 is used this byte should be $00, if Unicode is used it should be $01
            uint terminator_size;
            if (text_encoding == 0)
                terminator_size = 1;
            else if (text_encoding == 1)
                terminator_size = 2;
            else
                // Not 1 && 2 text encoding is invalid - should we try the string to be terminated by... single 0 ?
                terminator_size = 1;

            // Debug.LogFormat("terminator_size: {0}", terminator_size);

            uint bytesRead;

            string MIMEtype = AudioStreamSupport.StringFromNative(fromAddress, out bytesRead);
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + bytesRead + terminator_size);
            datalen -= bytesRead;
            datalen -= terminator_size;

            // Debug.LogFormat("MIMEtype: {0}", MIMEtype);

            picture_type = (byte)Marshal.PtrToStructure(fromAddress, typeof(byte));
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + 1);
            datalen--;

            // Debug.LogFormat("picture_type: {0}", picture_type);

            // string description_text = 
            AudioStreamSupport.StringFromNative(fromAddress, out bytesRead);
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + bytesRead + terminator_size);
            datalen -= bytesRead;
            datalen -= terminator_size;

            // Debug.LogFormat("description_text: {0}", description_text);

            // Debug.LogFormat("Supposed picture byte size: {0}", datalen);
            if (
                // "image/" prefix is from spec, but some tags them are broken
                // MIMEtype.ToLower().StartsWith("image/")
                // &&
                (
                    MIMEtype.ToLower().EndsWith("jpeg")
                    || MIMEtype.ToLower().EndsWith("jpg")
                    || MIMEtype.ToLower().EndsWith("png")
                    )
                )
            {
                picture_data = new byte[datalen];
                Marshal.Copy(fromAddress, picture_data, 0, (int)datalen);
            }
        }
        /// <summary>
        /// https://www.fmod.com/docs/api/content/generated/FMOD_TAGDATATYPE.html
        /// IEEE floating point number. See FMOD_TAG structure to confirm if the float data is 32bit or 64bit (4 vs 8 bytes).
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
        protected float ReadFMODTagValue_float(System.IntPtr fromAddress, uint datalen)
        {
            byte[] barray = new byte[datalen];

            for (var offset = 0; offset < datalen; ++offset)
                barray[offset] = Marshal.ReadByte(fromAddress, offset);

            return System.BitConverter.ToSingle(barray, 0);
        }
        /// <summary>
        /// https://www.fmod.com/docs/api/content/generated/FMOD_TAGDATATYPE.html
        /// Integer - Note this integer could be 8bit / 16bit / 32bit / 64bit. See FMOD_TAG structure for integer size (1 vs 2 vs 4 vs 8 bytes).
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
        protected long ReadTagValue_int(System.IntPtr fromAddress, uint datalen)
        {
            byte[] barray = new byte[datalen];

            for (var offset = 0; offset < datalen; ++offset)
                barray[offset] = Marshal.ReadByte(fromAddress, offset);

            return System.BitConverter.ToInt64(barray, 0);
        }
        #endregion
    }
}