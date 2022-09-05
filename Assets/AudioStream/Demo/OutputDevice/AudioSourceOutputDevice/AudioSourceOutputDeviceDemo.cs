// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// [ExecuteInEditMode()]
public class AudioSourceOutputDeviceDemo : MonoBehaviour
{
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMODSystemsManager.OUTPUT_DEVICE> availableOutputs = new List<FMODSystemsManager.OUTPUT_DEVICE>();
    /// <summary>
    /// AudioStream with redirect attached
    /// </summary>
    public AudioStream.AudioStream audioStream;
    /// <summary>
    /// AudioStreamMinimal allows to change output directly
    /// </summary>
    public AudioStreamMinimal audioStreamMinimal;
    /// <summary>
    /// Unity AudioSource
    /// </summary>
    public AudioSourceOutputDevice audioSourceOutput;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> redirectionStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> outputNotificationStatesFromEvents = new Dictionary<string, string>();
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

    public void OnError_Audio(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnRedirectionStarted(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection started";
    }

    public void OnRedirectionStopped(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection stopped";
    }

    public void OnError_Redirection(string goName, string msg)
    {
        this.redirectionStatesFromEvents[goName] = msg;
    }

    public void OnError_OutputNotification(string goName, string msg)
    {
        this.outputNotificationStatesFromEvents[goName] = msg;
    }

    public void OnOutputDevicesChanged(string goName)
    {
        this.UpdateOutputDevicesList();
    }
    #endregion
    /// <summary>
    /// Refreshes available outputs device list
    /// </summary>
    void UpdateOutputDevicesList()
    {
        // use e.g. this.audioStream for log level and error logging
        this.availableOutputs = FMODSystemsManager.AvailableOutputs(this.audioStream.logLevel, this.gameObject.name, this.audioStream.OnError);

        string msg = "Available outputs:" + System.Environment.NewLine;

        for (int i = 0; i < this.availableOutputs.Count; ++i)
            msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
    }
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = 0;

    IEnumerator Start()
    {
        while (!this.audioStream.ready || !this.audioStreamMinimal.ready || !this.audioSourceOutput.ready)
            yield return null;

        // check for available outputs
        // (does not matter which instance of FMOD will be checked)
        if (Application.isPlaying)
        {
            this.UpdateOutputDevicesList();
        }
    }

    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStream ? " " + this.audioStream.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("This scene will play AudioStream playback components and regular Unity AudioSource on selected system output. Mixing is done by FMOD automatically.", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Select output device and press Play (default output device is preselected). You can switch between outputs while playing.", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("NOTE: you can un/plug device/s in your system during runtime - the device list should update accordingly", AudioStreamMainScene.guiStyleLabelNormal);

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamMainScene.guiStyleLabelNormal);

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select(s => string.Format("{0}: {1}", s.id, s.name)).ToArray()
            , 1, AudioStreamMainScene.guiStyleButtonNormal);

        if (this.selectedOutput != this.previousSelectedOutput)
        {

            if (Application.isPlaying)
            {
                this.audioStream.SetOutput(this.availableOutputs[this.selectedOutput].id);

                this.audioStreamMinimal.SetOutput(this.availableOutputs[this.selectedOutput].id);

                this.audioSourceOutput.SetOutput(this.availableOutputs[this.selectedOutput].id);
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();


        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        foreach (var p in this.redirectionStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        foreach (var p in this.outputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;
        

        // AudioStream:

        FMOD.RESULT lastError;
        string lastErrorString = this.audioStream.GetLastError(out lastError);

        GUILayout.Label(this.audioStream.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal);
        this.audioStream.url = GUILayout.TextField(this.audioStream.url);
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.audioStream.isPlaying ? "Playing" + (this.audioStream.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        // AudioStream+AudioSourceOutpuDevice
        AudioSourceOutputDevice asod = this.audioStream.GetComponent<AudioSourceOutputDevice>();

        var pcmb = asod.PCMCallbackBuffer();
        var underflow = (pcmb != null && pcmb.underflow) ? ", underflow" : string.Empty;

        GUILayout.Label(string.Format("Input mix latency: {0} ms", asod.inputLatency), AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Output device latency average: {0} ms{1}", asod.latencyAverage, underflow), AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

        var _as = this.audioStream.GetComponent<AudioSource>();
        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.audioStream.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            if (this.audioStream.isPlaying)
                this.audioStream.Stop();
            else
                this.audioStream.Play();

        if (this.audioStream.isPlaying)
        {
            if (GUILayout.Button(this.audioStream.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.audioStream.isPaused)
                    this.audioStream.Pause(false);
                else
                    this.audioStream.Pause(true);
        }

        GUILayout.EndHorizontal();

        /*
         * took too much screen estate on demo scene when there are e.g. multiple output devices
        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.audioStream.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);
        */

        // AudioStreamMinimal:
        // uses default DSP buffers
        // TODO: implement FMODSourceOutpuDevice to cover both components in similar way..

        lastErrorString = this.audioStreamMinimal.GetLastError(out lastError);

        GUILayout.Label(this.audioStreamMinimal.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Stream: ", AudioStreamMainScene.guiStyleLabelNormal);
        audioStreamMinimal.url = GUILayout.TextField(audioStreamMinimal.url);
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.audioStreamMinimal.isPlaying ? "Playing" + (this.audioStreamMinimal.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

        this.audioStreamMinimal.volume = GUILayout.HorizontalSlider(this.audioStreamMinimal.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(this.audioStreamMinimal.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.audioStreamMinimal.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            if (this.audioStreamMinimal.isPlaying)
                this.audioStreamMinimal.Stop();
            else
                this.audioStreamMinimal.Play();

        if (this.audioStreamMinimal.isPlaying)
        {
            if (GUILayout.Button(this.audioStreamMinimal.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.audioStreamMinimal.isPaused)
                    this.audioStreamMinimal.Pause(false);
                else
                    this.audioStreamMinimal.Pause(true);
        }

        GUILayout.EndHorizontal();

        /*
         * took too much screen estate on demo scene when there are e.g. multiple output devices
        if (this.tags.TryGetValue(this.audioStreamMinimal.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);
        
        */

        // standalone AudioSource:

        _as = this.audioSourceOutput.GetComponent<AudioSource>();

        lastErrorString = this.audioSourceOutput.GetLastError(out lastError);

        GUILayout.Label(_as.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

        GUILayout.Label("Common Unity AudioClip", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Clip: " + _as.clip.name, AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0} {1}"
            , _as.isPlaying ? "Playing" : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        // AudioSoutce+AudioSourceOutpuDevice

        pcmb = this.audioSourceOutput.PCMCallbackBuffer();
        underflow = (pcmb != null && pcmb.underflow) ? ", underflow" : string.Empty;

        GUILayout.Label(string.Format("Input mix latency: {0} ms", this.audioSourceOutput.inputLatency), AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Output device latency average: {0} ms{1}", this.audioSourceOutput.latencyAverage, underflow), AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(_as.isPlaying ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            if (_as.isPlaying)
            {
                _as.Stop();

                this.OnPlaybackStopped(_as.gameObject.name);
            }
            else
            {
                _as.Play();

                this.OnPlaybackStarted(_as.gameObject.name);
            }

        GUILayout.EndHorizontal();
    }
}
