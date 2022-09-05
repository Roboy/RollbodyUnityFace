// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This is the original - now legacy - demo which was using FMOD networking to download streamed audio data, up to version 1.9 of the asset
/// </summary>
// [ExecuteInEditMode]
public class AudioStreamLegacyStressTest : MonoBehaviour
{
    /// <summary>
    /// List of components created at the start from code
    /// </summary>
    List<AudioStream.AudioStreamLegacy> audioStreams = new List<AudioStream.AudioStreamLegacy>();

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
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value as string} };
        }
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }
    #endregion

    IEnumerator Start()
    {
        var testObjectsCount = 10;

        for (var i = 0; i < testObjectsCount; ++i)
        {
            var go = new GameObject("AudioStreamLegacy#" + i);

            var @as = go.AddComponent<AudioStream.AudioStreamLegacy>();
            // @as.logLevel = AudioStream.LogLevel.INFO;

            while (!@as.ready)
                yield return null;

            @as.url = "http://somafm.com/spacestation.pls";

            @as.OnPlaybackStarted = new AudioStream.EventWithStringParameter();
            @as.OnPlaybackStarted.AddListener(this.OnPlaybackStarted);

            @as.OnPlaybackPaused = new AudioStream.EventWithStringBoolParameter();
            @as.OnPlaybackPaused.AddListener(this.OnPlaybackPaused);

            @as.OnPlaybackStopped = new AudioStream.EventWithStringParameter();
            @as.OnPlaybackStopped.AddListener(this.OnPlaybackStopped);

            @as.OnTagChanged = new AudioStream.EventWithStringStringObjectParameter();
            @as.OnTagChanged.AddListener(this.OnTagChanged);

            @as.OnError = new AudioStream.EventWithStringStringParameter();
            @as.OnError.AddListener(this.OnError);

            @as.GetComponent<AudioSource>().volume = 0.1f;

            this.audioStreams.Add(@as);
        }

        this.allReady = true;
    }

    bool allReady = false;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStreams != null && this.audioStreams.Count> 0 ? " " + this.audioStreams[0].fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Legacy version of the components - using FMOD networking", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Stress testing scene for the AudioStreamLegacy component", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("{0} AudioStreamLegacy game objects are created at Start and are set to start streaming simultaneously the same url", this.audioStreams.Count), AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("[Depending on the network connection several might not be able to actually connect]", AudioStreamMainScene.guiStyleLabelNormal);

        if (this.allReady)
        {
            var atLeastOnePlaying = false;
            foreach (var @as in this.audioStreams)
                if (@as.isPlaying)
                {
                    atLeastOnePlaying = true;
                    break;
                }

            if (GUILayout.Button(atLeastOnePlaying ? "Stop all" : "Start all", AudioStreamMainScene.guiStyleButtonNormal))
            {
                foreach (var @as in this.audioStreams)
                    if (atLeastOnePlaying)
                        @as.Stop();
                    else if (!@as.isPlaying)
                        @as.Play();
            }
        }

        GUI.color = Color.yellow;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var p in this.streamsStatesFromEvents)
            sb.Append(" | " + p.Key + " : " + p.Value);
        GUILayout.Label(sb.ToString(), AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        foreach (var audioStream in this.audioStreams)
        {
            FMOD.RESULT lastError;
            string lastErrorString = audioStream.GetLastError(out lastError);

            GUILayout.BeginHorizontal();
            GUILayout.Label(audioStream.gameObject.name, AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            // GUILayout.Label(audioStream.url, AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.Label(string.Format("State = {0} {1} {2} {3}"
                , audioStream.isPlaying ? "Playing" + (audioStream.isPaused ? " / Paused" : "") : "Stopped"
                , audioStream.starving ? "(STARVING)" : ""
                , lastError + " " + lastErrorString
                , audioStream.deviceBusy ? "(refreshing)" : ""
                )
                , AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2)))
                if (audioStream.isPlaying)
                    audioStream.Stop();
                else
                {
                    if (audioStream.ready)
                        audioStream.Play();
                }

            if (audioStream.isPlaying)
            {
                if (GUILayout.Button(audioStream.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2)))
                    if (audioStream.isPaused)
                        audioStream.Pause(false);
                    else
                        audioStream.Pause(true);
            }
            GUILayout.EndHorizontal();
        }
    }
}