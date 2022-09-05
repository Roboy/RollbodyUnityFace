// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This is the original - now legacy - demo which was using FMOD networking to download streamed audio data, up to version 1.9 of the asset
/// </summary>
[ExecuteInEditMode()]
public class AudioStreamLegacyDemo : MonoBehaviour
{
    public AudioStreamLegacyBase[] audioStreams;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, object>> tags = new Dictionary<string, Dictionary<string, object>>();

    bool enableUIFromStreamState = true;
    public void OnPlaybackStarted(string goName)
    {
        this.enableUIFromStreamState = true;
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnStarvation(string goName)
    {
        // TODO: type callback parameters
        this.enableUIFromStreamState = false;
        this.streamsStatesFromEvents[goName] = "starving";
    }

    public void OnStarvationEnded(string goName)
    {
        // TODO: type callback parameters
        this.enableUIFromStreamState = true;
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.enableUIFromStreamState = true;
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.enableUIFromStreamState = true;
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
    string latencyChangedMessage = string.Empty;
    int startingDspBufferSize;
    void Start()
    {
        this.latencyChangedMessage = string.Empty;

        if (Application.isPlaying)
        {
            // set latency to Good, if best latency is selected for the application in order for smoother playback on this scene
            var aconfig = AudioSettings.GetConfiguration();
            this.startingDspBufferSize = aconfig.dspBufferSize;

            // TODO: deconstantize
            if (aconfig.dspBufferSize == 256
                && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                )
            {
                aconfig.dspBufferSize = 512;
                if (AudioSettings.Reset(aconfig))
                {
                    this.latencyChangedMessage = string.Format("Note: To ensure smoother playback of AudioStreamLegacy component 'Good latency' was set for this scene");
                    Debug.LogFormat(this.latencyChangedMessage);
                }
                else
                {
                    this.latencyChangedMessage = string.Format("Failed to set 'Good latency' for this scene");
                    Debug.LogErrorFormat(this.latencyChangedMessage);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            // reset latency to starting state
            var aconfig = AudioSettings.GetConfiguration();
            if (aconfig.dspBufferSize != this.startingDspBufferSize)
            {
                aconfig.dspBufferSize = this.startingDspBufferSize;

                if (AudioSettings.Reset(aconfig))
                    Debug.LogFormat("Audio latency set to original state");
                else
                    Debug.LogErrorFormat("Failed to set original {0} latency", this.startingDspBufferSize);
            }
        }
    }

    System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStreams != null && this.audioStreams.Length > 0 ? " " + this.audioStreams[0].fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Legacy version of the components - using FMOD networking", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Press Play to play entered stream.\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without the 'file://' prefix)", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("-- AudioStreamLegacy component plays audio via Unity's AudioSource/AudioClip\r\n-- AudioStreamLegacyMinimal bypasses Unity audio playing directly via FMOD", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Change stream buffer size if you experience either bad streaming performance, or repeated chunks of audio when nearing the end of a file when streaming finite sized file.\r\nSize can vary from e.g. 512/1024 bytes for mobile streaming up to few 100s of kB to help decoder correctly decode ending chunks of a file.", AudioStreamMainScene.guiStyleLabelNormal);

        if (!string.IsNullOrEmpty(this.latencyChangedMessage))
            GUILayout.Label(this.latencyChangedMessage, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        for (var ai = 0; ai < this.audioStreams.Length; ++ai)
        {
            var audioStream = this.audioStreams[ai];

            FMOD.RESULT lastError;
            string lastErrorString = audioStream.GetLastError(out lastError);

            GUILayout.Label(audioStream.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            audioStream.url = GUILayout.TextField(audioStream.url);
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("State = {0} {1} {2} {3}"
                , audioStream.isPlaying ? "Playing" + (audioStream.isPaused ? " / Paused" : "") : "Stopped"
                , audioStream.starving ? "(STARVING)" : ""
                , lastError + " " + lastErrorString
                , audioStream.deviceBusy ? "(refreshing)" : ""
                )
                , AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.Label(string.Format("Buffer Percentage = {0}", audioStream.bufferFillPercentage), AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.Label(string.Format("Time: {0}", AudioStreamSupport.TimeStringFromSeconds(audioStream.PlaybackTimeSeconds())), AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            if (audioStream is AudioStream.AudioStreamLegacy)
            {
                var @as = audioStream as AudioStream.AudioStreamLegacy;

                var asource = @as.GetComponent<AudioSource>();
                asource.volume = GUILayout.HorizontalSlider(asource.volume, 0f, 1f);
                GUILayout.Label(Mathf.Round(asource.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);
            }
            else
            {
                var @as = (audioStream as AudioStream.AudioStreamLegacyMinimal);
                @as.volume = GUILayout.HorizontalSlider(@as.volume, 0f, 1f);
                GUILayout.Label(Mathf.Round(@as.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Stream buffer size: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            uint bs = audioStream.streamBufferSize;
            if (uint.TryParse(
                GUILayout.TextField(audioStream.streamBufferSize.ToString())
                , out bs)
                )
                audioStream.streamBufferSize = bs;
            GUILayout.Label(" bytes", AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.EndHorizontal();

            // align vertically with default TextField style
            GUILayout.Space(5);

            // display the timeout on separate line since it might be long as.. and volume slider is unusable
            if (audioStream is AudioStream.AudioStreamLegacy)
            {
                var @as = audioStream as AudioStream.AudioStreamLegacy;
                // var digits = @as.maxTimeout > 0 ? Mathf.FloorToInt(Mathf.Log10(@as.maxTimeout) + 1) : 1;
                // GUILayout.Label(string.Format(" (network timeout/max: {0}/{1} ms)", @as.ntimeout.ToString().PadLeft(digits, '0'), @as.maxTimeout), AudioStreamMainScene.guiStyleLabelNormal);
                var s_timeout = @as.streamTimeoutBase.ToString().PadRight(16, '0');

                GUILayout.BeginHorizontal();

                GUILayout.Label(string.Format("[audio decoder buffer read: {0} b, timeout: {1} ms]", @as.readlength, s_timeout), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUILayout.Label(string.Format("[Network/PCM queue: {0} b]", @as.networkAudioQueueAvailable), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                var r = Mathf.CeilToInt(@as.networkAudioQueueFullness * 10f);
                var c = Mathf.Min(r, 10);

                GUI.color = c <= 9 ? Color.Lerp(Color.red, Color.green, c / 5f) : Color.red;

                this.gauge.Length = 0;
                for (int i = 0; i < c; ++i) this.gauge.Append("#");

                GUILayout.Label(this.gauge.ToString(), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUILayout.EndHorizontal();

                GUI.color = Color.white;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Stream format: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                audioStream.streamType = (AudioStreamLegacyBase.StreamAudioType)ComboBoxLayout.BeginLayout(ai, System.Enum.GetNames(typeof(AudioStreamLegacyBase.StreamAudioType)), (int)audioStream.streamType, 10, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2));
            }

            if (audioStream.streamType == AudioStreamLegacyBase.StreamAudioType.RAW)
            {
                // display additional RAW parameters

                GUILayout.Space(10);
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
            else if (audioStream.streamType == AudioStreamLegacyBase.StreamAudioType.AUTODETECT)
            {
                GUILayout.Label("(Note: autodetection fails on iOS often - please select stream type explicitely on iOS)", AudioStreamMainScene.guiStyleLabelNormal);
            }

            // GUILayout.Space(10);
            // audioStream.autoReconnect = GUILayout.Toggle(audioStream.autoReconnect, "Auto reconnect/play again when the connection is dropped/finished.");

            GUILayout.Space(10);

            GUI.enabled = this.enableUIFromStreamState;

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            {
                if (audioStream.isPlaying)
                    audioStream.Stop();
                else
                {
                    if (audioStream.ready)
                    {
                        // turn off UI interaction until it starts stremaing
                        this.enableUIFromStreamState = false;

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
            }

            GUILayout.EndHorizontal();

            GUI.enabled = true;

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