// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using Concentus.Oggfile;
using Concentus.Structs;
using OggVorbisEncoder;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    public class IcecastSource : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        public enum IcecastSourceCodec
        {
            /// <summary>
            /// OPUS encoded audio in Ogg container
            /// </summary>
            OGGOPUS
            /// <summary>
            /// Vorbis encoded audio in Ogg container
            /// </summary>
            , OGGVORBIS
            /// <summary>
            /// raw float PCM data converted to byte[] arrays
            /// </summary>
            , PCM16
        }

        [Header("[Icecast source setup]")]
        [Tooltip("Hostname of the Icecast source to connect to. This should be the same as <listen-socket>::<bind-address> in Icecast config.")]
        public string hostname = "localhost";
        [Tooltip("Port of the Icecast source to connect to. This should be the same as <listen-socket>::<port> in Icecast config.")]
        public ushort port = 8000;
        [Tooltip("Mount point of the Icecast source to connect to. This should be the same as <listen-socket>::<shoutcast-mount> in Icecast config.")]
        public string mountPoint = "/stream";
        /// <summary>
        /// Username for source is not configurable.
        /// </summary>
        string username = "source";
        [Tooltip("Password for source username of the Icecast source to connect to. This should be the same as <authentication>::<source-password> in Icecast config.")]
        public string password = "hackme";
        [Tooltip("Source name - just description on Icecast server")]
        public string sourceName;
        [Tooltip("Source description - just description on Icecast server")]
        public string sourceDescription;
        [Tooltip("Source genre - just description on Icecast server")]
        public string sourceGenre;
        [Tooltip("Server announce url - used only for Icy-url announce in stream tag")]
        public string url;
        /// <summary>
        /// bitrate
        /// </summary>
        [Tooltip("Desired bitrate in kbit/s\r\nThis will be pushed to Icecast as one of the parameters\r\n\r\nOPUS encoder will use this for encoding\r\nVORBIS will try to use highest possible quality.")]
        public ushort KBitrate = 128;
        /// <summary>
        /// Is this for public stream directory ?
        /// </summary>
        bool _public = false;
        [Tooltip("User agent of this source connection")]
        public string userAgent = "";
        [Tooltip("Default ARTIST tag for this stream which is sent initially. You should call UpdateTags(...) later when streaming.")]
        public string tagDefaultArtist = "DEFAULT_ARTIST";
        [Tooltip("Default TITLE tag for this stream which is sent initially. You should call UpdateTags(...) later when streaming.")]
        public string tagDefaultTitle = "DEFAULT_TITLE";
        [Tooltip("Connect to the mountpoint at Start.\r\n(Otherwise call Connect() method).")]
        public bool autoConnectOnStart = true;

        [Header("[Audio]")]
        // TODO: custom editor for IcecastSourceCodec
        [Tooltip("Source can be pushed as OGGVORBIS or OGGOPUS encoded, or raw PCM data.\r\n\r\nRaw PCM stream does not use any codec, but requires (significantly) higher bandwidth. Client has to be configured to have the same signal properties as this machine output, i.e. the same samplerate, channel count and byte format.\r\n\r\nOPUS encoded in OGG container is not supported by FMOD/AudioStream client, but can be played in most common streaming clients/browsers.")]
        public IcecastSourceCodec codec = IcecastSourceCodec.OGGOPUS;
        [Tooltip("Set this to channel count of the audio source.\r\n\r\nNote: OGGVORBIS currently supports only 40k+ Stereo VBR encoding.\r\nOPUS can encode only 1 or 2-channels sound.")]
        public byte channels = 2;
        [Tooltip("If disabled silences sent audio afterwards.")]
        public bool listen = false;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnServerConnected;
        public EventWithStringParameter OnServerDisconnected;
        public EventWithStringStringParameter OnError;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region VORBIS/OGGVORBIS settings
        [Range(0f, 1f)]
        float vorbis_baseQuality = 0.99f;
        ProcessingState ogg_processingState = null;
        OggPage page = null;
        OggStream oggStream = null;
        float[][] vorbis_buffer = null;
        VorbisInfo vorbis_info = null;
        #endregion

        // ========================================================================================================================================
        #region OPUS/OGGOPUS settings
        OpusEncoder opusEncoder = null;
        OpusOggWriteStream opusOggWriteStream = null;
        #endregion

        // ========================================================================================================================================
        #region Writer
        IcecastWriter icecastWriter = null;
        public bool Connected
        {
            get { return this.icecastWriter != null && this.icecastWriter.Connected; }
        }
        /// <summary>
        /// This should be called for each tags change when appropriate. You can send any arbitrary tags in dictionary, typically {{"ARTIST":"ARTIST_NAME"},{"TITLE":"TITLE"}, etc. }
        /// </summary>
        /// <param name="withTags"></param>
        public void UpdateTags(Dictionary<string, string> withTags)
        {
            if (this.oggStream != null)
            {
                if (withTags.Count > 0)
                {
                    var headerBuilder = new HeaderPacketBuilder();
                    var comments = new Comments();

                    foreach (var tag in withTags)
                        comments.AddTag(tag.Key, tag.Value);

                    var commentsPacket = headerBuilder.BuildCommentsPacket(comments);
                    this.oggStream.PacketIn(commentsPacket);
                }
            }

            if (this.opusOggWriteStream != null && this.icecastWriter.streamWriter != null)
            {
                OpusTags tags = new OpusTags();
                foreach (var tag in withTags)
                    tags.Fields[tag.Key] = tag.Value;

                this.opusOggWriteStream = new OpusOggWriteStream(this.opusEncoder, this.icecastWriter.streamWriter.BaseStream, tags, AudioSettings.outputSampleRate);
            }
        }

        public void Connect()
        {
            string contentType = string.Empty;
            switch (this.codec)
            {
                case IcecastSourceCodec.OGGVORBIS:
                    contentType = "audio/ogg";      // spec should be 'audio/ogg; codecs=vorbis', Icecast detects subtype correctly though
                    break;
                case IcecastSourceCodec.OGGOPUS:
                    contentType = "audio/ogg";     // spec should be 'audio/ogg; codecs=opus', Icecast detects subtype correctly though
                    break;
                case IcecastSourceCodec.PCM16:
                    contentType = "audio/raw";
                    break;
            }

            this.icecastWriter = new IcecastWriter()
            {
                Hostname = this.hostname,
                Port = this.port,
                Mountpoint = this.mountPoint,
                Username = this.username,
                Password = this.password,
                ContentType = contentType,
                Name = this.sourceName,
                Description = this.sourceDescription,
                Genre = this.sourceGenre,
                Url = this.url,
                KBitrate = this.KBitrate,
                Channels = this.channels,
                Samplerate = AudioSettings.outputSampleRate,
                Public = this._public,
                UserAgent = string.IsNullOrEmpty(this.userAgent) ? "AudioStream " + About.versionNumber : this.userAgent
            };

            Debug.LogFormat("[{0}:{1}] Testing connection to master server...", this.icecastWriter.Hostname, this.icecastWriter.Port);
            if (!this.icecastWriter.Open())
            {
                var message = string.Format("[{0}:{1}] Connection declined: Master server was unavailable", this.icecastWriter.Hostname, this.icecastWriter.Port);
                Debug.LogError(message);
                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, message);

                return;
            }
            else
            {
                Debug.LogFormat("[{0}:{1}] Connection accepted", this.icecastWriter.Hostname, this.icecastWriter.Port);
                if (this.OnServerConnected != null)
                    this.OnServerConnected.Invoke(this.gameObjectName);
            }

            switch (this.codec)
            {
                case IcecastSourceCodec.OGGVORBIS:

                    // Stores all the static vorbis bitstream settings

                    // TODO:
                    // Interval 0.3..0.59999 is out of bounds...
                    // IndexOutOfRangeException: Array index is out of range.
                    // OggVorbisEncoder.VorbisInfo.ToneMaskSetup(OggVorbisEncoder.CodecSetup codecSetup, Double toneMaskSetting, Int32 block, OggVorbisEncoder.Setup.Att3[] templatePsyToneMasterAtt, System.Int32[] templatePsyTone0Decibel, OggVorbisEncoder.Setup.AdjBlock[] templatePsyToneAdjLong)
                    // OggVorbisEncoder.VorbisInfo.InitVariableBitRate(Int32 channels, Int32 sampleRate, Single baseQuality)
                    if (this.vorbis_baseQuality >= 0.3f && this.vorbis_baseQuality < 0.6f)
                    {
                        var d1 = Mathf.Abs(this.vorbis_baseQuality - 0.3f);
                        var d2 = Mathf.Abs(this.vorbis_baseQuality - 0.6f);

                        if (d1 < d2)
                            this.vorbis_baseQuality = 0.29999f;
                        else
                            this.vorbis_baseQuality = 0.6f;

                        Debug.LogWarningFormat("Adjusting quality within acceptable range - ", this.vorbis_baseQuality);
                    }

                    // Currently only supports 40k+ Stereo VBR encoding
                    var sr = AudioSettings.outputSampleRate;
                    if (sr < 40000)
                    {
                        var message = string.Format("Currently only 40k+ Stereo VBR encoding is supported and the output sample rate is: {0}", sr);
                        if (this.OnError != null)
                            this.OnError.Invoke(this.gameObjectName, message);

                        throw new NotSupportedException(message);
                    }

                    this.vorbis_info = VorbisInfo.InitVariableBitRate(this.channels, AudioSettings.outputSampleRate, this.vorbis_baseQuality);

                    // set up our packet->stream encoder
                    var serial = new System.Random().Next();
                    this.oggStream = new OggStream(serial);

                    // =========================================================
                    // HEADER
                    // =========================================================
                    // Vorbis streams begin with three headers; the initial header (with
                    // most of the codec setup parameters) which is mandated by the Ogg
                    // bitstream spec.  The second header holds any comment fields.  The
                    // third header holds the bitstream codebook.

                    var headerBuilder = new HeaderPacketBuilder();

                    var infoPacket = headerBuilder.BuildInfoPacket(this.vorbis_info);
                    var booksPacket = headerBuilder.BuildBooksPacket(this.vorbis_info);

                    this.oggStream.PacketIn(infoPacket);
                    this.UpdateTags(new Dictionary<string, string>() { { "ARTIST", this.tagDefaultArtist }, { "TITLE", this.tagDefaultTitle } });
                    this.oggStream.PacketIn(booksPacket);

                    // Flush to force audio data onto its own page per the spec
                    while (this.oggStream.PageOut(out page, true))
                    {
                        if (!this.icecastWriter.Push(page.Header)
                            || !this.icecastWriter.Push(page.Body)
                            )
                        {
                            var message = string.Format("Unable to push to server");
                            if (this.OnError != null)
                                this.OnError.Invoke(this.gameObjectName, message);
                        }
                    }

                    // =========================================================
                    // BODY (Audio Data)
                    // =========================================================
                    this.ogg_processingState = ProcessingState.Create(this.vorbis_info);

                    break;

                case IcecastSourceCodec.OGGOPUS:
                    this.opusEncoder = new OpusEncoder(AudioSettings.outputSampleRate, this.channels, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
                    this.opusEncoder.Bitrate = this.KBitrate * 1000;

                    this.opusOggWriteStream = new OpusOggWriteStream(this.opusEncoder, this.icecastWriter.streamWriter.BaseStream, null, AudioSettings.outputSampleRate);

                    break;
            }
        }

        public void Disconnect()
        {
            if (this.ogg_processingState != null)
                this.ogg_processingState.WriteEndOfStream();

            if (this.opusOggWriteStream != null)
                this.opusOggWriteStream.Finish();

            if (this.icecastWriter != null)
                this.icecastWriter.Close();

            if (this.OnServerDisconnected != null)
                this.OnServerDisconnected.Invoke(this.gameObjectName);
        }
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        void Start()
        {
            this.gameObjectName = this.gameObject.name;

            if (this.autoConnectOnStart)
                this.Connect();
        }

        void OnDestroy()
        {
            this.Disconnect();
        }

        byte[] bArr = null;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.icecastWriter != null && this.icecastWriter.Connected)
            {
                switch (this.codec)
                {
                    case IcecastSourceCodec.OGGVORBIS:

                        // AudioSource might have been already started before Connect
                        if (this.ogg_processingState != null)
                        {
                            if (channels != 2)
                            {
                                var message = "Currently only 40k+ Stereo VBR encoding is supported and the audio does not have 2 channels";
                                if (this.OnError != null)
                                    this.OnError.Invoke(this.gameObjectName, message);

                                throw new NotSupportedException(message);
                            }

                            var samples = data.Length / 2;

                            if (this.vorbis_buffer == null)
                            {
                                this.vorbis_buffer = new float[this.vorbis_info.Channels][];
                                this.vorbis_buffer[0] = new float[samples];
                                this.vorbis_buffer[1] = new float[samples];
                            }

                            // uninterleave samples
                            for (var i = 0; i < samples; i++)
                            {
                                this.vorbis_buffer[0][i] = data[i * 2];
                                this.vorbis_buffer[1][i] = data[(i * 2) + 1];
                            }

                            this.ogg_processingState.WriteData(this.vorbis_buffer, samples);

                            OggPacket packet;
                            if (this.ogg_processingState.PacketOut(out packet))
                            {
                                this.oggStream.PacketIn(packet);

                                while (this.oggStream.PageOut(out page, true))
                                {
                                    if (!this.icecastWriter.Push(page.Header)
                                        || !this.icecastWriter.Push(page.Body)
                                        )
                                    {
                                        var message = string.Format("Unable to push to server");

                                        Debug.LogError(message);

                                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                                        {
                                            if (this.OnError != null)
                                                this.OnError.Invoke(this.gameObjectName, message);
                                        }
                                        );
                                    }
                                }
                            }
                        }

                        break;

                    case IcecastSourceCodec.OGGOPUS:

                        // AudioSource might have been already started before Connect
                        if (this.opusOggWriteStream != null)
                            this.opusOggWriteStream.WriteSamples(data, 0, data.Length);

                        break;

                    case IcecastSourceCodec.PCM16:

                        AudioStreamSupport.FloatArrayToPCM16yteArray(data, (uint)data.Length, ref this.bArr);

                        if (!this.icecastWriter.Push(this.bArr))
                        {
                            var message = string.Format("Unable to push to server");

                            Debug.LogError(message);

                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                if (this.OnError != null)
                                    this.OnError.Invoke(this.gameObjectName, message);
                            }
                            );
                        }

                        break;
                }
            }

            if (!this.listen)
                Array.Clear(data, 0, data.Length);
        }
        #endregion
    }
}