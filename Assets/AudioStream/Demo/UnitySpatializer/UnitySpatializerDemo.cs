// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class UnitySpatializerDemo : MonoBehaviour
{
    /// <summary>
    /// Demo references - this just to display FMOD version..
    /// </summary>
    public AudioStream.AudioStream audioStream;
    public AudioStreamInput audioStreamInput;

    #region UI events
    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();
    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();

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

    public void OnError_AStream(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnRecordingStarted(string goName)
    {
        this.inputStreamsStatesFromEvents[goName] = "recording";
    }

    public void OnRecordingPaused(string goName, bool paused)
    {
        this.inputStreamsStatesFromEvents[goName] = paused ? "paused" : "recording";
    }

    public void OnRecordingStopped(string goName)
    {
        this.inputStreamsStatesFromEvents[goName] = "stopped";
    }

    public void OnError_AInput(string goName, string msg)
    {
        this.inputStreamsStatesFromEvents[goName] = msg;
    }

    #endregion

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStream ? " " + this.audioStream.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;


        GUILayout.Label("One cube is playing a radio stream, second is streaming default recording input in 3D.\r\nBoth are moving with random speeds in circle, their colors change from blue to yellow according to signal intensity.", AudioStreamMainScene.guiStyleLabelNormal);

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(audioStream.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);
    }
}