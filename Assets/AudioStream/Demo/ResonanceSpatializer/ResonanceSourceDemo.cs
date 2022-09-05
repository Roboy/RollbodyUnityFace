// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class ResonanceSourceDemo : MonoBehaviour
{
    /// <summary>
    /// Demo references
    /// </summary>
    public AudioStream.ResonanceSource resonanceSource;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

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

    #endregion

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.resonanceSource ? " " + this.resonanceSource.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Streamed audio is being played via FMOD's provided Google Resonance plugin.");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");

        GUILayout.Label("Press Play to play entered stream.\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without the 'file://' prefix)");
        GUILayout.Label("Note: all AudioStream audio formats can be played by this component");

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.resonanceSource.GetLastError(out lastError);

        GUILayout.Label(this.resonanceSource.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.resonanceSource.url = GUILayout.TextField(this.resonanceSource.url, GUILayout.MaxWidth(Screen.width / 2));
        }

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.resonanceSource.isPlaying ? "Playing" + (this.resonanceSource.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(string.Format("Volume: {0} dB", Mathf.RoundToInt(this.resonanceSource.gain)), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.resonanceSource.gain = GUILayout.HorizontalSlider(this.resonanceSource.gain, -80f, 24f, GUILayout.MaxWidth(Screen.width / 2));
        }

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream format: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.resonanceSource.streamType = (AudioStreamBase.StreamAudioType)ComboBoxLayout.BeginLayout(0, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), (int)this.resonanceSource.streamType, 10, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2));
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.resonanceSource.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            if (this.resonanceSource.isPlaying)
                this.resonanceSource.Stop();
            else
                this.resonanceSource.Play();

        if (this.resonanceSource.isPlaying)
        {
            if (GUILayout.Button(this.resonanceSource.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.resonanceSource.isPaused)
                    this.resonanceSource.Pause(false);
                else
                    this.resonanceSource.Pause(true);
        }

        GUILayout.EndHorizontal();

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.resonanceSource.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);


        ComboBoxLayout.EndAllLayouts();
    }
}