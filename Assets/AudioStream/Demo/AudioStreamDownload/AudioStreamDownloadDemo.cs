// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioStreamDownloadDemo : MonoBehaviour
{
    public AudioStreamDownload asDownload;
    public AudioSource userAudioSource;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        // playback started means also download has been started
        this.streamsStatesFromEvents[goName] = "downloading";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }
    /// <summary>
    /// Invoked when download has finished and clip is created
    /// </summary>
    /// <param name="goName"></param>
    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "downloaded & clip created";
        this.@as_isPaused = false;
    }

    public void OnTagChanged(string goName, string _key, object _value)
    {
        // care only about 'meaningful' tags
        var key = _key.ToLower();

        if (key == "artist" || key == "title")
        {
            // little juggling around dictionaries..

            if (this.tags.ContainsKey(goName))
                this.tags[goName][_key] = _value as string;
            else
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value as string } };
        }
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnAudioClipCreated(string goName, AudioClip newAudioClip)
    {
        Destroy(this.userAudioSource.clip);
        this.userAudioSource.clip = newAudioClip;

        if (this.playClipAfterDownload)
            this.userAudioSource.Play();
    }
    #endregion

    IEnumerator Start()
    {
        string filepath = "";
        yield return StartCoroutine(AudioStreamDemoSupport.GetFilenameFromStreamingAssets("electronic-senses-shibuya.mp3", (newDestination) => filepath = newDestination));

        this.asDownload.url = filepath;
    }

    bool @as_isPaused = false;
    bool playClipAfterDownload = true;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.asDownload != null ? " " + this.asDownload.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("NOTE: this component is primarily aimed at downloading files via *non realtime* speeds which are (much) higher than real time playback - this means that real time streams such as net radio streams most likely won't work", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("- you can, however, force it to run in realtime speed via Inspector which will allow it to e.g. stream & save netradios and optionally play back the audio while the download is in progress on user provided AudioSource", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("NOTE: URL of a file placed in application's StreamingAssets is used in this demo by default (which kind of defeats the purpose since local file is read directly normally) - enter URL of a file on LAN or on a quick enough network to test properly", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Press Download to start downloading entered URL\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without the 'file://' prefix)", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("AudioStreamDownload will download/stream and decode audio instead of playing it; once done/stopped it will then construct an AudioClip and pass it to user AudioSource for playback", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("The downloaded data can be played later offline from a cache if/when needed", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Temporary PCM data used for AudioClip stored as .RAW file in " + Application.temporaryCachePath, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.asDownload.GetLastError(out lastError);

        GUILayout.Label(this.asDownload.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.asDownload.url = GUILayout.TextField(this.asDownload.url, GUILayout.MaxWidth(Screen.width / 2));
        }

        // GUILayout.BeginHorizontal();
        this.asDownload.overwriteCachedDownload = GUILayout.Toggle(this.asDownload.overwriteCachedDownload, "Overwrite previously downloaded data for this url");
        this.playClipAfterDownload = GUILayout.Toggle(this.playClipAfterDownload, "Play downloaded clip immediately after the download is stopped or the whole file is downloaded");
        // GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.asDownload.isPlaying ? "Playing" + (this.asDownload.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

        this.userAudioSource.volume = GUILayout.HorizontalSlider(this.userAudioSource.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(this.userAudioSource.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Decoded downloaded bytes: {0} (file size: {1})", this.asDownload.decoded_bytes, this.asDownload.file_size.HasValue ? this.asDownload.file_size.Value + " b" : "N/A (streamed content)"));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label(string.Format("Last AudioClip creation took: {0} ms", this.asDownload.decodingToAudioClipTimeInMs));

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream format: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.asDownload.streamType = (AudioStreamBase.StreamAudioType)ComboBoxLayout.BeginLayout(0, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), (int)this.asDownload.streamType, 10, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2));
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.asDownload.isPlaying ? "Stop Download" : "Download" + (this.playClipAfterDownload ? " and Play" : ""), AudioStreamMainScene.guiStyleButtonNormal))
            if (this.asDownload.isPlaying)
                this.asDownload.Stop();
            else
            {
                this.asDownload.Play();
            }

        if (this.asDownload.isPlaying)
        {
            if (GUILayout.Button(this.asDownload.isPaused ? "Resume Download" : "Pause Download", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.asDownload.isPaused)
                    this.asDownload.Pause(false);
                else
                    this.asDownload.Pause(true);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label(string.Format("Cached file samplerate: {0}", this.asDownload.streamSampleRate), AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Cached file channels  : {0}", this.asDownload.streamChannels), AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Space(10);

        if (this.userAudioSource.clip != null)
        {
            // clip downloaded

            GUILayout.Label(string.Format("Downloaded AudioClip channels: {0}, length: {1} s, playback position: {2} s", this.userAudioSource.clip.channels, this.userAudioSource.clip.length, this.userAudioSource.time), AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.userAudioSource.isPlaying || this.@as_isPaused ? "Stop Playback" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            {
                if (this.userAudioSource.isPlaying || this.@as_isPaused)
                    this.userAudioSource.Stop();
                else if (!this.@as_isPaused)
                    this.userAudioSource.Play();

                this.@as_isPaused = false;
            }

            if (this.userAudioSource.isPlaying || this.@as_isPaused)
            {
                if (GUILayout.Button(this.@as_isPaused ? "Resume Playback" : "Pause Playback", AudioStreamMainScene.guiStyleButtonNormal))
                    if (this.@as_isPaused)
                    {
                        this.userAudioSource.UnPause();
                        this.@as_isPaused = false;
                    }
                    else
                    {
                        this.userAudioSource.Pause();
                        this.@as_isPaused = true;
                    }
            }

            GUILayout.EndHorizontal();
        }

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.asDownload.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);

        ComboBoxLayout.EndAllLayouts();
    }
}