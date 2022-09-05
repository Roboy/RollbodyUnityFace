// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class ResonanceInputDemo : MonoBehaviour
{
    public ResonanceInput resonanceInput;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMODSystemsManager.INPUT_DEVICE> availableInputs = new List<FMODSystemsManager.INPUT_DEVICE>();

    #region UI events
    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();

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

    public void OnError(string goName, string msg)
    {
        this.inputStreamsStatesFromEvents[goName] = msg;
    }

    public void OnRecordDevicesChanged(string goName)
    {
        // update device list
        if (this.resonanceInput.ready)
            this.availableInputs = FMODSystemsManager.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);
    }
    #endregion
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedInput = 0; // 0 is system default
    int previousSelectedInput = 0;
    /// <summary>
    /// DSP OnGUI
    /// </summary>
    uint dspBufferLength_new, dspBufferCount_new;
    /// <summary>
    /// Include loop back interfaces
    /// </summary>
    bool includeLoopbacks = true;

    IEnumerator Start()
    {
        while (!this.resonanceInput.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = FMODSystemsManager.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += this.availableInputs[i].id + " : " + this.availableInputs[i].name + System.Environment.NewLine;

        }

        // get user buffer since we don't have possible initial change triggered
        this.resonanceInput.GetDSPBufferSize(out this.dspBufferLength_new, out this.dspBufferCount_new);
    }

    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.resonanceInput ? " " + this.resonanceInput.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Input audio is being played via FMOD's provided Google Resonance plugin.");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");
        GUILayout.Label("");

        GUILayout.Label("Choose from available recording devices and press Record.\r\nThe input singal will be processed by Resonance and played from the cube's 3D position.", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Available recording devices:", AudioStreamMainScene.guiStyleLabelNormal);

        var _includeLoopbacks = GUILayout.Toggle(this.includeLoopbacks, " Include loopback interfaces [you can turn this off to filter only recording devices Unity's Microphone class can see]");
        if (_includeLoopbacks != this.includeLoopbacks)
        {
            this.includeLoopbacks = _includeLoopbacks;
            this.availableInputs = FMODSystemsManager.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);
            // small reselect if out of range..
            this.selectedInput = 0;
        }

        // selection of available audio inputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.Select(s => string.Format("[device ID: {0}] {1} rate: {2} speaker mode: {3} channels: {4}", s.id, s.name, s.samplerate, s.speakermode, s.channels)).ToArray()
            , 1
            , AudioStreamMainScene.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        if (this.selectedInput != this.previousSelectedInput)
        {
            if (Application.isPlaying)
            {
                this.resonanceInput.Stop();
                this.resonanceInput.recordDeviceId = this.availableInputs[this.selectedInput].id;
            }

            this.previousSelectedInput = this.selectedInput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        // wait for startup

        if (this.availableInputs.Count > 0)
        {
            GUI.color = Color.white;

            FMOD.RESULT lastError;
            string lastErrorString = this.resonanceInput.GetLastError(out lastError);

            GUILayout.Label(this.resonanceInput.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.resonanceInput.isRecording ? "Recording" + (this.resonanceInput.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamMainScene.guiStyleLabelNormal);

            // DSP buffers

            // we need an action on toggle/bool field change..
            var useAutomaticDSPBufferSize_previous = this.resonanceInput.useAutomaticDSPBufferSize;
            this.resonanceInput.useAutomaticDSPBufferSize = GUILayout.Toggle(this.resonanceInput.useAutomaticDSPBufferSize, "Use Automatic DSP Buffer Size");

            if (this.resonanceInput.useAutomaticDSPBufferSize)
            {
                // retrieve current size (after flag is set)
                uint dspBufferLength, dspBufferCount;
                this.resonanceInput.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);

                GUILayout.BeginHorizontal();
                GUILayout.Label("DSP buffer length: ");
                GUILayout.Label(dspBufferLength.ToString());
                GUILayout.Label("DSP buffer count: ");
                GUILayout.Label(dspBufferCount.ToString());
                GUILayout.EndHorizontal();

                if (useAutomaticDSPBufferSize_previous != this.resonanceInput.useAutomaticDSPBufferSize)
                {
                    if (Application.isPlaying)
                    {
                        // update is only when needed..
                        this.resonanceInput.SetAutomaticDSPBufferSize();
                    }
                }
            }
            else
            {
                if (useAutomaticDSPBufferSize_previous != this.resonanceInput.useAutomaticDSPBufferSize)
                {
                    // retrieve current size (after flag is set)
                    uint dspBufferLength, dspBufferCount;
                    this.resonanceInput.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);

                    this.dspBufferLength_new = dspBufferLength;
                    this.dspBufferCount_new = dspBufferCount;
                }

                uint input;

                GUILayout.BeginHorizontal();

                GUILayout.Label("DSP buffer length: ");
                if (uint.TryParse(
                    GUILayout.TextField(this.dspBufferLength_new.ToString())
                    , out input
                    )
                    )
                    this.dspBufferLength_new = input;

                GUILayout.Label("DSP buffer count: ");

                if (uint.TryParse(
                    GUILayout.TextField(this.dspBufferCount_new.ToString())
                    , out input
                    )
                    )
                    this.dspBufferCount_new = input;

                if (GUILayout.Button("Update User DSP buffer size")
                    || useAutomaticDSPBufferSize_previous != this.resonanceInput.useAutomaticDSPBufferSize)
                {
                    if (Application.isPlaying)
                    {
                        // update is only when needed..
                        this.resonanceInput.SetUserDSPBuffers(this.dspBufferLength_new, this.dspBufferCount_new);

                        // verify set buffers
                        this.resonanceInput.GetDSPBufferSize(out this.dspBufferLength_new, out this.dspBufferCount_new);
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Label("Keep the combination of DSP buffer length and count as small as possible for small latency\r\nThe default in this demo scene (64) seems to be working with Best Latency Unity audio setting on desktops.\r\nBe careful esp. on lower end/Android devices - it might be necessary to keep DSP buffer size and count higher, e.g. 256/8, otherwise FMOD might struggle or ignore it completely.");
            }

            GUILayout.Label(string.Format("Input mixer latency average: {0} ms", this.resonanceInput.latencyAverage), AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label("Recording will automatically restart if it was running after changing these.", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Gain: ", AudioStreamMainScene.guiStyleLabelNormal);

            this.resonanceInput.gain = GUILayout.HorizontalSlider(this.resonanceInput.gain, 0f, 10f);
            GUILayout.Label(Mathf.Round(this.resonanceInput.gain * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.resonanceInput.isRecording ? "Stop" : "Record", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.resonanceInput.isRecording)
                    this.resonanceInput.Stop();
                else
                    this.resonanceInput.Record();

            if (this.resonanceInput.isRecording)
            {
                if (GUILayout.Button(this.resonanceInput.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                    if (this.resonanceInput.isPaused)
                        this.resonanceInput.Pause(false);
                    else
                        this.resonanceInput.Pause(true);
            }

            GUILayout.EndHorizontal();

            // TODO: enable once AudioSource/DSP interop works
            // this.resonanceInput.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle(this.resonanceInput.GetComponent<AudioSourceMute>().mute, "Mute output");
        }
    }
}
