// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// This is the original - now legacy - version which was using FMOD networking to download streamed audio data, up to version 1.9 of the asset
    /// Only AudioStreamLegacy and AudioStreamLegacyMinimal inherit from this legacy base now
    /// </summary>
    public abstract class AudioStreamLegacyBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Required descendant's implementation
        /// <summary>
        /// Called immediately after a valid stream has been established
        /// </summary>
        /// <param name="samplerate"></param>
        /// <param name="channels"></param>
        /// <param name="sound_format"></param>
        protected abstract void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format);
        /// <summary>
        /// Called per frame to determine runtime status of the incoming data
        /// </summary>
        /// <returns></returns>
        protected abstract bool StreamStarving();
        /// <summary>
        /// User pause
        /// </summary>
        /// <param name="pause"></param>
        protected abstract void StreamPausing(bool pause);
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
        /// Allows setting the output device directly for AudioStreamLegacyMinimal, or calls SetOutput on AudioStreamLegacy's sibling GO component if it is attached
        /// </summary>
        /// <param name="outputDriverId"></param>
        public abstract void SetOutput(int outputDriverId);
        /// <summary>
        /// Returns playback time in seconds for AudioStreamLegacyMinimal, or PCM callback time in seconds - which will be little bit behind the networked data
        /// </summary>
        /// <returns></returns>
        public abstract double PlaybackTimeSeconds();
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

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("When checked the stream will play on start. Otherwise use Play() method of this GameObject.")]
        public bool playOnStart = false;

        [Tooltip("If ON the component will attempt to stream continuosly regardless of any error/s that may occur. This is done automatically by restarting 'Play' method when needed (an error/end of file occurs)\r\nRecommended for streams.\r\nNote: if used with finite sized files while ON, the streaming of the file will restart from beginning even when reaching the end, too. You might want to turn this OFF for finite sized files, and check state via OnPlaybackStopped event.\r\n\r\nFlag is ignored when needed, e.g. for AudioStreamMemory component")]
        public bool continuosStreaming = true;

        [Tooltip("Default is fine in most cases")]
        public FMOD.SPEAKERMODE speakerMode = FMOD.SPEAKERMODE.DEFAULT;
        [Tooltip("No. of speakers for RAW speaker mode. You must also provide mix matrix for custom setups,\r\nsee remarks at https://www.fmod.com/docs/api/content/generated/FMOD_SPEAKERMODE.html, \r\nand https://www.fmod.com/docs/api/content/generated/FMOD_Channel_SetMixMatrix.html about how to setup the matrix.")]
        // Specify 0 to ignore; when raw speaker mode is selected that defaults to 2 speakers ( stereo ), unless set by user.
        public int numOfRawSpeakers = 0;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringBoolParameter OnPlaybackPaused;
        public EventWithStringParameter OnPlaybackStopped;
        // TODO: type callback parameters
        public EventWithStringParameter OnStarvation;
        // TODO: type callback parameters
        public EventWithStringParameter OnStarvationEnded;
        public EventWithStringStringObjectParameter OnTagChanged;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Advanced]")]
        [Tooltip("Do not change this unless you have problems opening certain streamed files over the network.\nGenerally increasing this to some bigger value of few tens kB should help when having trouble opening the stream with ERR_FILE_COULDNOTSEEK error - this often occurs with e.g. mp3s containing tags with embedded artwork.\nFor more info see https://www.fmod.org/docs/content/generated/FMOD_System_SetFileSystem.html and 'blockalign' parameter discussion.")]
        public int streamBlockAlignment = 16 * 1024;
        [Tooltip("It can take some time until the stream is caught on unreliable/slow network connections. You can increase frame count before giving up here.\r\n\r\nDefault is 60 frames which on reliable network is almost never reached.")]
        public int initialConnectionRetryCount = 60;
        [Tooltip("This is frame count after which the connection is dropped when the network is starving continuosly.\r\nDefault is 300 which for 60 fps means ~ 5 secs.")]
        public int starvingRetryCount = 300;
        [Tooltip("If you keep getting inconsistent streaming performance, try adjusting stream buffer size here\r\nHelps especially in corner cases such as near end of decoding of some files.\r\n\r\nDefault (64 kB) seems not to fit all network + esp. mobile device/OS combinations.\r\n\r\nCan range from very low (512/1024 bytes) on some mobiles to rather large (~ few 100kB) on standalones.")]
        public uint streamBufferSize = 64 * 1024;
        /// <summary>
        /// Consider new starvingRetryCount when end of file is reached in order to stop immediately automatically
        /// TODO: this should probably depend on something - ( such as on actual size of FMOD PCM buffer and frequency by which it's retrieved )
        /// - set manually to 30 frames which should provide time to either stop or recover
        /// </summary>
        protected const int kStarvingRetryCount_FileStopped = 30;
        protected int? starvingRetryCount_FileStopped = null;

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

        public virtual IEnumerator Start()
        {
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
             * or each time a new system for AudioStreamLegacyMinimal and the rest
             */
            if (this is AudioStreamLegacyMinimal)
                this.fmodsystem = FMODSystemsManager.FMODSystem_DirectSound(this.fmodsystem, this.speakerMode, this.numOfRawSpeakers, this.logLevel, this.gameObjectName, this.OnError);
            else
                // AudioStreamLegacy
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
            int numDrivers = 0;
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

            this.ready = true;

            if (this.playOnStart)
                this.Play();
        }

        #endregion

        // ========================================================================================================================================
        #region Playback
        [Header("[Playback info]")]
        [Range(0f, 100f)]
        [Tooltip("Set during playback. Stream buffer fullness")]
        public uint bufferFillPercentage = 0;
        [Tooltip("Set during playback.")]
        public bool isPlaying = false;
        [Tooltip("Set during playback.")]
        public bool isPaused = false;
        /// <summary>
        /// starving flag doesn't seem to work without playSound
        /// this is updated from Sound::readData/AudioStreamLegacy and from getOpenState/AudioStreamLegacyMinimal
        /// </summary>
        [Tooltip("Set during playback.")]
        public bool starving = false;
        [Tooltip("Set during playback when stream is refreshing data.")]
        public bool deviceBusy = false;
        [Tooltip("Radio station title. Set from PLS playlist.")]
        public string title;
        [Tooltip("Set during playback.")]
        public int streamChannels;
        public float streamSampleRate;
        //: - [Tooltip("Tags supplied by the stream. Varies heavily from stream to stream")]
        Dictionary<string, object> tags = new Dictionary<string, object>();
        /// <summary>
        /// Stop playback after too many dropped frames,
        /// allow for a bit of a grace period during which some loss is recoverable / acceptable
        /// getOpenState and update in base are still OK (connection is open) although playback is finished
        /// starving condition is determined in each descendant individually depending on their method used - see starving flag description
        /// </summary>
        int starvingFrames = 0;
        /// <summary>
        /// Returns playback time in seconds
        /// note: works reliably for AudioStreamLegacyMinimal, for AudioStreamLegacy this is network read time, so it's actualy little bit (by PCM Unity callback latency) sooner than actual audio
        /// </summary>
        protected double playback_time = 0;
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
                * url format check
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

            this.isPaused = false;
            this.starvingFrames = 0;

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

            this.isPlaying = true;

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

            // is playlist
            // this.playlistType = PlaylistType.PLS;

            if (this.playlistType.HasValue)
            {
                string playlist = string.Empty;

                // allow local playlist
                if (!this.url.ToLower().StartsWith("http", System.StringComparison.OrdinalIgnoreCase) && !this.url.ToLower().StartsWith("file", System.StringComparison.OrdinalIgnoreCase))
                    this.url = "file://" + this.url;

                LOG(LogLevel.INFO, "Using {0} as proxy for web request", string.IsNullOrEmpty(proxystring) ? "[NONE]" : proxystring);

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

                    this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                    yield break;
                }
            }

            // allow FMOD to stream locally
            if (this.urlFinal.ToLower().StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
                this.urlFinal = this.urlFinal.Substring(7);

            LOG(LogLevel.INFO, "Using {0} as proxy for streaming", string.IsNullOrEmpty(proxystring) ? "[NONE]" : proxystring);

            result = fmodsystem.System.setNetworkProxy(proxystring == null ? "" : proxystring);
            ERRCHECK(result, "fmodsystem.System.setNetworkProxy");


            /*
             * opening flags for streaming createSound
             */
            var flags = FMOD.MODE.CREATESTREAM
                // | FMOD.MODE.NONBLOCKING - trade safe release for blocking UI when connecting
                ;

            if (this is AudioStreamLegacy)
                flags |= FMOD.MODE.OPENONLY; // is driven via readData

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

            extInfo.cbsize = Marshal.SizeOf(extInfo);

            /*
             * Additional streaming setup
             */

            /* Increase the file buffer size a little bit to account for Internet lag. */
            result = fmodsystem.System.setStreamBufferSize(streamBufferSize, FMOD.TIMEUNIT.RAWBYTES);
            ERRCHECK(result, "fmodsystem.System.setStreamBufferSize");

            /* tags ERR_FILE_COULDNOTSEEK:
                http://stackoverflow.com/questions/7154223/streaming-mp3-from-internet-with-fmod
                https://www.fmod.com/docs/api/content/generated/FMOD_System_SetFileSystem.html
                */
            result = fmodsystem.System.setFileSystem(null, null, null, null, null, null, this.streamBlockAlignment);
            ERRCHECK(result, "fmodsystem.System.setFileSystem");

            /*
             * Start streaming
             */
            result = fmodsystem.System.createSound(this.urlFinal
                , flags
                , ref extInfo
                , out sound);
            ERRCHECK(result, "fmodsystem.System.createSound");

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
            bool streamCaught = false;

            for (; ; )
            {
                if (this.isPaused)
                    yield return null;

                if (!streamCaught)
                {
                    int c = 0;
                    do
                    {
                        fmodsystem.Update();

                        result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                        ERRCHECK(result, null, false);

                        LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);

                        if (result == FMOD.RESULT.OK && openstate == FMOD.OPENSTATE.READY)
                        {
                            /*
                             * stream caught
                             */
                            FMOD.SOUND_TYPE _streamType;
                            FMOD.SOUND_FORMAT _streamFormat;
                            int _streamBits;

                            result = sound.getFormat(out _streamType, out _streamFormat, out this.streamChannels, out _streamBits);
                            ERRCHECK(result, null);

                            float freq; int prio;
                            result = sound.getDefaults(out freq, out prio);
                            ERRCHECK(result, null);

                            // do small sanity check of stream properties too
                            if (
                                _streamFormat != FMOD.SOUND_FORMAT.NONE
                                && this.streamChannels > 0
                                && _streamBits > 0
                                && freq > 0
                                )
                            {
                                this.streamSampleRate = freq;

                                LOG(LogLevel.INFO, "Stream type: {0} format: {1}, {2} channels {3} bits {4} samplerate", _streamType, _streamFormat, this.streamChannels, _streamBits, this.streamSampleRate);

                                this.StreamStarting((int)this.streamSampleRate, this.streamChannels, _streamFormat);

                                streamCaught = true;
                                this.starvingFrames = 0;
                                this.starvingRetryCount_FileStopped = null;

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
                            if (++c > this.initialConnectionRetryCount)
                            {
                                if (isNetworkSource)
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Please make sure that correct audio type of stream is selected, network is reachable and possibly check Advanced setting.");
#if UNITY_EDITOR
                                    LOG(LogLevel.ERROR, "If everything seems to be ok, restarting the editor often helps while having trouble connecting to especially OGG streams.");
#endif
                                }
                                else
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Unrecognized audio type.");
                                }


                                ERRCHECK(FMOD.RESULT.ERR_NET_CONNECT, "Can't start playback", false);

                                this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                                yield break;
                            }
                        }

                        yield return new WaitForSeconds(0.1f);

                    } while (result != FMOD.RESULT.OK || openstate != FMOD.OPENSTATE.READY);
                }

                if (this.StreamStarving())
                {
                    LOG(LogLevel.DEBUG, "Starving frame: {0}", this.starvingFrames);

                    if (this.OnStarvation != null)
                        this.OnStarvation.Invoke(this.gameObjectName);

                    if (++this.starvingFrames > (this.starvingRetryCount_FileStopped.HasValue ? this.starvingRetryCount_FileStopped.Value : this.starvingRetryCount))
                    {
                        LOG(LogLevel.INFO, "Stream buffer starving - stopping playback");

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                        yield break;
                    }
                }
                else
                {
                    if (this.starvingFrames > 0)
                    {
                        if (this.OnStarvationEnded != null)
                            this.OnStarvationEnded.Invoke(this.gameObjectName);
                    }

                    this.starvingFrames = 0;
                }

                // process actions dispatched on the main thread for album art texture creation
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

            this.StreamPausing(pause);

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
                        // TODO: actual float and samplerate change test - is there a way to test this ?

                        // resampling is done via the AudioClip - but we have to recreate it for AudioStreamLegacy ( will cause noticeable pop/pause, but there's probably no other way )
                        // , do it via direct calls without events

                        // float frequency = *((float*)streamTag.data);
                        float[] frequency = new float[1];
                        Marshal.Copy(streamTag.data, frequency, 0, sizeof(float));

                        // get current sound_format
                        FMOD.SOUND_TYPE _streamType;
                        FMOD.SOUND_FORMAT _streamFormat;
                        int _streamBits;
                        result = sound.getFormat(out _streamType, out _streamFormat, out this.streamChannels, out _streamBits);
                        ERRCHECK(result, null);

                        // TODO: will be needed to be invoked on main thread anyway 
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
                                if (FMODtag_Name == "APIC")
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
                            FMODtag_Value = Marshal.PtrToStringAnsi(streamTag.data);
                            break;

                        case FMOD.TAGDATATYPE.STRING_UTF16:
                        case FMOD.TAGDATATYPE.STRING_UTF16BE:
                        case FMOD.TAGDATATYPE.STRING_UTF8:

                            FMODtag_Value = Marshal.PtrToStringAuto(streamTag.data);
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
        #endregion

        // ========================================================================================================================================
        #region Shutdown
        /// <summary>
        /// Reason if calling Stop to distinguish between user initiad stop, stop on error, and stop on error/end of file.
        /// </summary>
        enum ESTOP_REASON
        {
            /// <summary>
            /// Just stop and don't perform any recovery actions
            /// </summary>
            User,
            /// <summary>
            /// This is either error, of actually finished file on established connection
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

            this.bufferFillPercentage = 0;
            this.isPlaying = false;
            this.isPaused = false;
            this.starving = false;
            this.deviceBusy = false;

            this.StopAllCoroutines();

            this.StreamStopping();

            this.tags = new Dictionary<string, object>();

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

                    if (openstate == FMOD.OPENSTATE.BUFFERING || openstate == FMOD.OPENSTATE.LOADING || openstate == FMOD.OPENSTATE.CONNECTING)
                    {
                        // If buffering not on wrong stream type but on unaccessible network, release normally
                        if (result != FMOD.RESULT.OK
                            && result != FMOD.RESULT.ERR_NET_URL
                            && result != FMOD.RESULT.ERR_NET_CONNECT
                            && result != FMOD.RESULT.ERR_NET_SOCKET_ERROR
                            && result != FMOD.RESULT.ERR_NET_WOULD_BLOCK
                            )
                        {
                            this.unstableShutdown = true;
                            var msg = string.Format("Unstable state while stopping the stream detected - will attempt recovery on release. [{0} {1}]"
                                , openstate
                                , result);

                            LOG(LogLevel.WARNING, msg);
                        }
                    }

                    /*
                     * Shut down
                     */
                    if (sound.hasHandle() && !this.unstableShutdown)
                    {
                        LOG(LogLevel.DEBUG, "Releasing sound..");

                        System.Threading.Thread.Sleep(10);

                        result = sound.release();
                        ERRCHECK(result, "sound.release", false);

                        sound.clearHandle();
                    }
                }
            }

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
                        LOG(LogLevel.INFO, "Attempting to restart connection...");
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

            System.Threading.Thread.Sleep(20);

            if (this.unstableShutdown)
            {
                // attempt to release sound once more after a delay -

                if (sound.hasHandle())
                {
                    result = sound.release();
                    ERRCHECK(result, "sound.release", false);

                    sound.clearHandle();
                }
            }

            if (this is AudioStreamLegacyMinimal)
                FMODSystemsManager.FMODSystem_DirectSound_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
            else
                FMODSystemsManager.FMODSystem_NoSound_RT_Release(this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);

            /*
             * Destructor can catch stray (i.e. non released due to 'unstableShutdown') systems, but has severe problems on 2018(?) and up - it is fine on 5.5.4 though :-/ - so we can't use it
            // system should be released in the destructor which seems to survive even unstable state there
            this.fmodsystem = null;

            // nudge it a bit, should not be a problem when called from here (hopefully (tm))
            System.GC.Collect();
            */
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
        #region Tag support
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
            picture_type = 0;

            // Debug.LogFormat("IntPtr: {0}, length: {1}", fromAddress, datalen);

            var text_encoding = (byte)Marshal.PtrToStructure(fromAddress, typeof(byte));
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + 1);
            datalen--;

            // Debug.LogFormat("text_encoding: {0}", text_encoding);

            // Frames that allow different types of text encoding have a text encoding description byte directly after the frame size. If ISO-8859-1 is used this byte should be $00, if Unicode is used it should be $01
            uint terminator_size = 0;
            if (text_encoding == 0)
                terminator_size = 1;
            else if (text_encoding == 1)
                terminator_size = 2;
            else
                // Not 1 && 2 text encoding is invalid - should we try the string to be terminated by... single 0 ?
                terminator_size = 1;

            // Debug.LogFormat("terminator_size: {0}", terminator_size);

            uint bytesRead = 0;

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
                MIMEtype.ToLower().StartsWith("image/")
                && (
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