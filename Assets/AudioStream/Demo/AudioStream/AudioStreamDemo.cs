// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioStreamDemo : MonoBehaviour
{
    public AudioStreamBase[] audioStreams;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, object>> tags = new Dictionary<string, Dictionary<string, object>>();

    public void OnPlaybackStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "stopped";
    }

    public void OnTagChanged(string goName, string _key, object _value)
    {
        // care only about 'meaningful' tags
        var key = _key.ToLower();
        //
        // for ID2v2 tags present in mp3 files:
        // album art is under 'APIC_##' tag name
        // where '##' is e.g. '0' ( Other ), or '3' Cover(front), see all types in 'Picture type' in AudioStreamBase comments
        //
        // TIT1, TIT2, TIT3: related to title
        // TPE1, TPE2, TPE3, TPE4: related to performer / artist
        // TALB is album/show
        // for more see http://id3.org/id3v2.3.0
        //
        if (
            // shoutcast/icecast radio tags
            key == "artist"
            || key == "album"
            || key == "title"
            // ID3v2 tags
            || key.StartsWith("apic")
            || key.StartsWith("tit")
            || key.StartsWith("tpe")
            || key.StartsWith("talb")
            )
        {
            if (key.StartsWith("apic"))
                Debug.LogFormat("ID3v2 album artwork type {0} present", key.Split('_')[1]);

            // little juggling around dictionaries..

            if (this.tags.ContainsKey(goName))
                this.tags[goName][_key] = _value;
            else
                this.tags[goName] = new Dictionary<string, object>() { { _key, _value } };
        }
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    #endregion

    System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStreams != null && this.audioStreams.Length > 0 ? " " + this.audioStreams[0].fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        var proxy = AudioStream.About.proxyUsed;
        if (!string.IsNullOrEmpty(proxy))
            GUILayout.Label(proxy, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Press Play to play entered stream.\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without the 'file://' prefix)", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("-- AudioStream component plays audio via Unity's AudioSource/AudioClip\r\n-- AudioStreamMinimal bypasses Unity audio playing directly via FMOD", AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        for (var aidx = 0; aidx < this.audioStreams.Length; ++aidx)
        {
            var audioStream = this.audioStreams[aidx];

            FMOD.RESULT lastError;
            string lastErrorString = audioStream.GetLastError(out lastError);

            GUILayout.Label(audioStream.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);


            // state
            GUILayout.Label(string.Format("State = {0} {1}"
                , audioStream.isPlaying ? "Playing" + (audioStream.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamMainScene.guiStyleLabelNormal);

            // stream url, format
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                audioStream.url = GUILayout.TextField(audioStream.url);
            }

            GUILayout.Label("Use 'blockalign' and 'blockalign multiplier for download' below to set the size of initial download before playback attempt is started. Increase (one or both) if some files can't be played (e.g. when format can't be recognized, or for certain files which contain album artwork), or decrease if possible for faster startup. - e.g. netradios can use 2k times 1, some compressed/MPEG files might need 64k times 10 or more." +
                " NOTE: Currently Ogg/Vorbis files require this to amount for the whole file size (file will be downloaded first) - please set 'blockalign' to the file size and 'blockalign multiplier for download' to 1 for Ogg/Vorbis format." +
                " Applies only for playback from network - local files are accessed directly, and use 'blockalign' parameter only."
            , AudioStreamMainScene.guiStyleLabelNormal
            );

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Decoder 'blockalign' size (bytes): ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                var dbs = audioStream.blockalign;
                if (uint.TryParse(
                    GUILayout.TextField(audioStream.blockalign.ToString())
                    , out dbs)
                    )
                    audioStream.blockalign = dbs;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(string.Format("'blockalign multiplier for download' (times): {0}", audioStream.blockalignDownloadMultiplier), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                audioStream.blockalignDownloadMultiplier = (uint)GUILayout.HorizontalSlider(audioStream.blockalignDownloadMultiplier, 1, 16);
            }

            GUILayout.Label(string.Format("Initial download until playback attempt is started: {0} b", audioStream.blockalign * audioStream.blockalignDownloadMultiplier), AudioStreamMainScene.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(string.Format("Stream format{0}: ", audioStream.streamType == AudioStreamBase.StreamAudioType.AUTODETECT ? " (autodetection fails on iOS often - please select stream type explicitely on iOS)" : ""), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                audioStream.streamType = (AudioStreamBase.StreamAudioType)ComboBoxLayout.BeginLayout(aidx, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), (int)audioStream.streamType, 10, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2));
            }

            // dont have style for toggle - leave always on in demo
            //GUILayout.Space(10);
            //audioStream.continuosStreaming = GUILayout.Toggle(audioStream.continuosStreaming, "Auto reconnect/play again when the connection is dropped/finished.", AudioStreamMainScene.guiStyleLabelNormal);

            if (audioStream.streamType == AudioStreamBase.StreamAudioType.RAW)
            {
                // display additional RAW parameters

                using (new GUILayout.VerticalScope())
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("RAW Format: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                        audioStream.RAWSoundFormat = (FMOD.SOUND_FORMAT)GUILayout.SelectionGrid((int)audioStream.RAWSoundFormat, System.Enum.GetNames(typeof(FMOD.SOUND_FORMAT)), 5, AudioStreamMainScene.guiStyleButtonNormal);
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("RAW Samplerate: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                        uint input = 0;
                        if (uint.TryParse(
                            GUILayout.TextField(audioStream.RAWFrequency.ToString())
                            , out input
                            )
                            )
                        {
                            audioStream.RAWFrequency = (int)input;
                        }
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("RAW Channels: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                        uint input = 0;
                        if (uint.TryParse(
                            GUILayout.TextField(audioStream.RAWChannels.ToString())
                            , out input
                            )
                            )
                        {
                            audioStream.RAWChannels = (int)input;
                        }
                    }
                }
            }

            if (audioStream.url.ToLowerInvariant().StartsWith("http"))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Media buffer (MEMORY will use small circular memory buffer for playback from nework, DISK buffer will cache incoming audio in application cache and allow seeking in the whole file as it is being downloaded. Applies only for audio retrieved from network, local files are accessed directly): ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                    var mediaBufferType = (AudioStreamBase.MEDIABUFFERTYPE)GUILayout.SelectionGrid((int)audioStream.mediaBufferType, System.Enum.GetNames(typeof(AudioStreamBase.MEDIABUFFERTYPE)), 2, AudioStreamMainScene.guiStyleButtonNormal);

                    if (mediaBufferType != audioStream.mediaBufferType)
                    {
                        audioStream.Stop();
                        audioStream.mediaBufferType = mediaBufferType;
                    }
                }

                if (audioStream.mediaBufferType == AudioStreamBase.MEDIABUFFERTYPE.DISK)
                    GUILayout.Label(string.Format("[(Using cache at: {0})]", Application.temporaryCachePath), AudioStreamMainScene.guiStyleLabelNormal);
            }

            // download progress / length
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(string.Format("[Downloaded / Total media length (bytes): {0} / {1}]", audioStream.mediaDownloaded, audioStream.MediaIsInfinite ? "(Undefined/Infinite length stream)" : audioStream.mediaLength.ToString())
                    , AudioStreamMainScene.guiStyleLabelNormal
                    //, GUILayout.MaxWidth(Screen.width / 2)
                    );

                // display download progress
                if (!audioStream.MediaIsInfinite)
                {
                    GUI.enabled = false;
                    GUILayout.HorizontalSlider(audioStream.mediaDownloaded, 0f, audioStream.mediaLength);
                    GUI.enabled = true;
                }
            }

            // media availability gauge
            using (new GUILayout.HorizontalScope())
            {
                var r = Mathf.Clamp01(audioStream.mediaAvailable / (float)audioStream.blockalign);
                var c = Mathf.Min((r * 100f) / 10f, 10f);

                GUILayout.Label(string.Format("[Allocated / Available for playback (bytes): {0} / {1}]", audioStream.mediaCapacity, audioStream.mediaAvailable), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUI.color = c < 10 ? Color.Lerp(Color.red, Color.green, c / 5f) : Color.green;

                this.gauge.Length = 0;
                for (int i = 0; i <= c; ++i) this.gauge.Append("#");

                GUILayout.Label(this.gauge.ToString(), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUI.color = Color.white;
            }

            // volume control
            using (new GUILayout.HorizontalScope())
            {
                if (audioStream is AudioStream.AudioStream)
                {
                    var @as = audioStream as AudioStream.AudioStream;
                    var asource = @as.GetComponent<AudioSource>();

                    GUILayout.Label(string.Format("Volume: {0} %", Mathf.Round(asource.volume * 100f)), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                    asource.volume = GUILayout.HorizontalSlider(asource.volume, 0f, 1f);
                }
                else
                {
                    var @as = audioStream as AudioStream.AudioStreamMinimal;

                    GUILayout.Label(string.Format("Volume: {0} %", Mathf.Round(@as.volume * 100f)), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                    @as.volume = GUILayout.HorizontalSlider(@as.volume, 0f, 1f);
                }
            }

            // playback controls
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
                {
                    if (audioStream.isPlaying)
                        audioStream.Stop();
                    else
                    {
                        if (audioStream.ready)
                        {
                            // clear any previous tags when starting
                            this.tags.Remove(audioStream.gameObject.name);

                            audioStream.Play();
                        }
                    }
                }

                if (audioStream.isPlaying)
                {
                    if (GUILayout.Button(audioStream.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                        if (audioStream.isPaused)
                            audioStream.Pause(false);
                        else
                            audioStream.Pause(true);

                    if (audioStream.IsSeekable)
                    {
                        if (GUILayout.Button("<< 30 sec", AudioStreamMainScene.guiStyleButtonNormal))
                            audioStream.PositionInSeconds -= 30;

                        if (GUILayout.Button(">> 30 sec", AudioStreamMainScene.guiStyleButtonNormal))
                            audioStream.PositionInSeconds += 30;
                    }
                }

            }

            // time + position
            if (!audioStream.MediaIsInfinite)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(string.Format("Time: {0} / {1} | Progress:"
                        , AudioStreamSupport.TimeStringFromSeconds(audioStream.PositionInSeconds)
                        , AudioStreamSupport.TimeStringFromSeconds(audioStream.SoundLengthInSeconds)
                        )
                        , AudioStreamMainScene.guiStyleLabelNormal
                        , GUILayout.MaxWidth(Screen.width / 2));

                    GUI.enabled = audioStream.IsSeekable;

                    var position = GUILayout.HorizontalSlider(audioStream.PositionInSeconds, 0f, audioStream.SoundLengthInSeconds);
                    if (position != audioStream.PositionInSeconds
                        && position < audioStream.SoundLengthInDownloadedSeconds)
                        audioStream.PositionInSeconds = position;

                    GUI.enabled = true;
                }
            }
            else
            {
                GUILayout.Label(string.Format("Time: {0}"
                    , AudioStreamSupport.TimeStringFromSeconds(audioStream.PositionInSeconds)
                    )
                    , AudioStreamMainScene.guiStyleLabelNormal
                    , GUILayout.MaxWidth(Screen.width / 2));
            }

            // print out any and all tags received for given GO
            // print texts in the column and any album artwork next to it
            Dictionary<string, object> _tags;
            if (this.tags.TryGetValue(audioStream.gameObject.name, out _tags))
            {
                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(GUILayout.MaxWidth(Screen.width / 2));
                foreach (var d in _tags)
                    if (d.Value is string)
                        GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.EndVertical();

                foreach (var d in _tags)
                    if (d.Value is Texture2D)
                        GUILayout.Label(d.Value as Texture2D, AudioStreamMainScene.guiStyleLabelNormal, GUILayout.Width(Screen.width / 10), GUILayout.Height(Screen.height / 10));

                GUILayout.EndHorizontal();
            }
        }

        ComboBoxLayout.EndAllLayouts();
    }
}