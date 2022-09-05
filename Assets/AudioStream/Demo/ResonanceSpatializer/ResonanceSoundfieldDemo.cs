// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class ResonanceSoundfieldDemo : MonoBehaviour
{
    /// <summary>
    /// Demo references
    /// </summary>
    public AudioStream.ResonanceSoundfield resonanceSoundfield;
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

    IEnumerator Start()
    {
        while (!this.resonanceSoundfield.ready)
            yield return null;

        /*
         * the below demo track retrieval from Resources works, but Unity import settings do not handle ambisonic multichannel format properly
         * - resulting in correct data, but incorrect samplerate -
         * so we read from StreamingAssets directly instead
         */
        /*
        if (!System.IO.File.Exists(filePath))
        {
            var ambclip = Resources.Load<AudioClip>("Leonard-Orfeo_Trio");
            float[] data = new float[ambclip.samples * ambclip.channels];

            ambclip.GetData(data, 0);

            var audioSaveToFile = this.GetComponent<AudioStream.GOAudioSaveToFile>();

            audioSaveToFile.StartSaving("Leonard-Orfeo_Trio.wav", (ushort)ambclip.channels, (uint)ambclip.frequency);

            audioSaveToFile.AddToSave(data);

            audioSaveToFile.StopSaving();
        }
        */

        // set path for demo audio file and play it
        // (Android has special needs - copy the file out of archive from StreamingAsset to some accessible location)

        string filepath = "";
        yield return StartCoroutine(AudioStreamDemoSupport.GetFilenameFromStreamingAssets("Leonard-Orfeo_Trio.wav", (newDestination) => filepath = newDestination));

        this.resonanceSoundfield.url = filepath;

        this.resonanceSoundfield.Play();
    }

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.resonanceSoundfield ? " " + this.resonanceSoundfield.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("An ambisonic soundfield source is being played using FMOD's provided Resonance plugin.");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");

        GUILayout.Label("Press Play to play an ambisonic file with entered path.\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without 'file://' prefix)");
        GUILayout.Label("Note: only valid ambisonic files can be played by this component");

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.resonanceSoundfield.GetLastError(out lastError);

        GUILayout.Label(this.resonanceSoundfield.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.resonanceSoundfield.url = GUILayout.TextField(this.resonanceSoundfield.url, GUILayout.MaxWidth(Screen.width / 2));
        }

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.resonanceSoundfield.isPlaying ? "Playing" + (this.resonanceSoundfield.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(string.Format("Volume: {0} dB", Mathf.RoundToInt(this.resonanceSoundfield.gain)), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            this.resonanceSoundfield.gain = GUILayout.HorizontalSlider(this.resonanceSoundfield.gain, -80f, 24f, GUILayout.MaxWidth(Screen.width / 2));
        }

        /*
         * this for testing stream type 
         * 
         * GUILayout.BeginHorizontal();
         * audioStream.streamType = (AudioStreamBase.StreamAudioType)GUILayout.SelectionGrid((int)audioStream.streamType, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), 5);
         * GUILayout.EndHorizontal();
        */

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.resonanceSoundfield.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            if (this.resonanceSoundfield.isPlaying)
                this.resonanceSoundfield.Stop();
            else
                this.resonanceSoundfield.Play();

        if (this.resonanceSoundfield.isPlaying)
        {
            if (GUILayout.Button(this.resonanceSoundfield.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.resonanceSoundfield.isPaused)
                    this.resonanceSoundfield.Pause(false);
                else
                    this.resonanceSoundfield.Pause(true);
        }

        GUILayout.EndHorizontal();

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.resonanceSoundfield.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Audio track used in this demo:", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("B-format ambisonic 'Leonard-Orfeo_Trio'.\r\nRecording from https://github.com/ambisonictoolkit/atk-sounds, used according to license therein.", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("audio file is read from StreamingAssets", AudioStreamMainScene.guiStyleLabelNormal);
    }
}