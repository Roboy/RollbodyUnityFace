// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class AudioStreamInput2DDemo : MonoBehaviour
{
    public AudioStreamInput2D audioStreamInput;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMODSystemsManager.INPUT_DEVICE> availableInputs = new List<FMODSystemsManager.INPUT_DEVICE>();

    #region UI events
    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> inputNotificationStatesFromEvents = new Dictionary<string, string>();

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

    public void OnError_InputNotification(string goName, string msg)
    {
        this.inputNotificationStatesFromEvents[goName] = msg;
    }

    public void OnRecordDevicesChanged(string goName)
    {
        // update device list
        if (this.audioStreamInput.ready)
            this.availableInputs = FMODSystemsManager.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);
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
    /// Output channels based on current Unity audio settings
    /// - we need this for cubes (this should be == to OAFR signal)
    /// </summary>
    int outputChannels = 0;

    // signal energy per channel for UI
    float[] recBuffer = new float[512];
    List<float> signalChannelsEnergies = new List<float>();

    // RMS -> reaction cubes per channel
    // rms reaction values - being attached to audioStreamInput they compute their values from its audio filter,
    // also attached above needed AudioSourceMute component, which needs to be 'last' one - at the bottom - otherwise the signal would be supressed before it reaches rms components
    public RMSPerChannelToTransforms rmsPerChannelToTransforms;
    // audio reaction cubes
    GameObject[] cubes;
    /// <summary>
    /// Include loop back interfaces
    /// </summary>
    bool includeLoopbacks = true;

    IEnumerator Start()
    {
        while (!this.audioStreamInput.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = FMODSystemsManager.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += this.availableInputs[i].id + " : " + this.availableInputs[i].name + System.Environment.NewLine;
        }

        // get user buffer since we don't have possible initial change triggered
        this.audioStreamInput.GetDSPBufferSize(out this.dspBufferLength_new, out this.dspBufferCount_new);

        // setup visuals

        // get output channels#
        this.outputChannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

        // start RMS distribution per channel on output audio filter
        this.rmsPerChannelToTransforms.SetChannels(this.outputChannels);

        // crete cubes
        this.cubes = new GameObject[this.outputChannels];

        for (var i = 0; i < this.outputChannels; ++i)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // set some (editor) id
            cube.gameObject.name = string.Format("Channel #{0}", i.ToString());
            // set some rotation
            cube.transform.rotation = Quaternion.Euler(-45f, -35.7f, 0f);

            // place cubes evenly across the screen along x at y,z = 0
            var screenX = (Camera.main.pixelWidth / (float)(this.outputChannels + 1)) * (i + 1);
            var distanceFromCamera = Mathf.Abs(Camera.main.transform.position.z);
            var ray = Camera.main.ScreenPointToRay(new Vector3(screenX, 0, 0));

            cube.transform.position = new Vector3((ray.direction * distanceFromCamera).x, 0, 0);

            this.cubes[i] = cube;
        }

        // setup signalChannelsEnergies based on current output
        this.signalChannelsEnergies = new List<float>();
        for (var i = 0; i < this.outputChannels; ++i)
            this.signalChannelsEnergies.Add(0f);
    }

    void Update()
    {
        if (this.audioStreamInput.isRecording)
        {
            // access the recording buffer and look at some values
            var _as = this.audioStreamInput.GetComponent<AudioSource>();

            for (int ch = 0; ch < this.outputChannels; ++ch)
            {
                _as.GetOutputData(this.recBuffer, ch);

                var signalEnergy = 0f;

                for (int i = 0; i < this.recBuffer.Length; ++i)
                    signalEnergy += this.recBuffer[i] * this.recBuffer[i];

                this.signalChannelsEnergies[ch] = signalEnergy;
            }

            // distribute RMS values per channel to cubes
            for (var i = 0; i < this.outputChannels; ++i)
            {
                this.cubes[i].transform.localScale = this.rmsPerChannelToTransforms.scale[i];
                this.cubes[i].transform.rotation = this.rmsPerChannelToTransforms.rotation[i];
            }
        }
    }

    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.audioStreamInput ? " " + this.audioStreamInput.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Choose from available recording devices and press Record.\r\nThe cube will react to sound and signal energy is computed from AudioSource's GetOutputData for each output channel separately.", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("NOTE: you can un/plug device/s from your system during runtime - the device list will update accordingly", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Available recording devices:", AudioStreamMainScene.guiStyleLabelNormal);

        var _includeLoopbacks = GUILayout.Toggle(this.includeLoopbacks, " Include loopback interfaces [you can turn this off to filter only recording devices Unity's Microphone class can see]");
        if (_includeLoopbacks != this.includeLoopbacks)
        {
            this.includeLoopbacks = _includeLoopbacks;
            this.availableInputs = FMODSystemsManager.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);
            // small reselect if out of range..
            this.selectedInput = 0;
        }

        // selection of available audio inputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.Select( s => string.Format("[device ID: {0}] {1} rate: {2} speaker mode: {3} channels: {4}", s.id, s.name, s.samplerate, s.speakermode, s.channels)).ToArray()
            , 1
            , AudioStreamMainScene.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        if (this.selectedInput != this.previousSelectedInput)
        {
            if (Application.isPlaying)
            {
                this.audioStreamInput.Stop();
                this.audioStreamInput.recordDeviceId = this.availableInputs[this.selectedInput].id;
            }

            this.previousSelectedInput = this.selectedInput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        foreach (var p in this.inputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);
        
        // wait for startup

        if (this.availableInputs.Count > 0)
        {
            GUI.color = Color.white;

            FMOD.RESULT lastError;
            string lastErrorString = this.audioStreamInput.GetLastError(out lastError);

            GUILayout.Label(this.audioStreamInput.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.audioStreamInput.isRecording ? "Recording" + (this.audioStreamInput.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label("Signal energy per output channel from GetOutputData: ", AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < this.signalChannelsEnergies.Count; ++i)
                GUILayout.Label(string.Format("CH #{0}: {1}", i, System.Math.Round(this.signalChannelsEnergies[i], 6).ToString().PadRight(8, '0')));
            GUILayout.EndHorizontal();

            // DSP buffers

            // we need an action on toggle/bool field change..
            var useAutomaticDSPBufferSize_previous = this.audioStreamInput.useAutomaticDSPBufferSize;
            this.audioStreamInput.useAutomaticDSPBufferSize = GUILayout.Toggle(this.audioStreamInput.useAutomaticDSPBufferSize, "Use Automatic DSP Buffer Size");

            if (this.audioStreamInput.useAutomaticDSPBufferSize)
            {
                // retrieve current size (after flag is set)
                uint dspBufferLength, dspBufferCount;
                this.audioStreamInput.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);

                GUILayout.BeginHorizontal();
                GUILayout.Label("DSP buffer length: ");
                GUILayout.Label(dspBufferLength.ToString());
                GUILayout.Label("DSP buffer count: ");
                GUILayout.Label(dspBufferCount.ToString());
                GUILayout.EndHorizontal();

                if (useAutomaticDSPBufferSize_previous != this.audioStreamInput.useAutomaticDSPBufferSize)
                {
                    if (Application.isPlaying)
                    {
                        // update is only when needed..
                        this.audioStreamInput.SetAutomaticDSPBufferSize();
                    }
                }
            }
            else
            {
                if (useAutomaticDSPBufferSize_previous != this.audioStreamInput.useAutomaticDSPBufferSize)
                {
                    // retrieve current size (after flag is set)
                    uint dspBufferLength, dspBufferCount;
                    this.audioStreamInput.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);

                    this.dspBufferLength_new = dspBufferLength;
                    this.dspBufferCount_new = dspBufferCount;
                }

                uint input = 0;

                GUILayout.BeginHorizontal();
                
                GUILayout.Label("DSP buffer length: ");
                if (uint.TryParse(
                    GUILayout.TextField(this.dspBufferLength_new.ToString())
                    , out input
                    )
                    )
                    this.dspBufferLength_new = input;

                GUILayout.Label("DSP buffer count: ");
                input = 0;
                if (uint.TryParse(
                    GUILayout.TextField(this.dspBufferCount_new.ToString())
                    , out input
                    )
                    )
                    this.dspBufferCount_new = input;

                if (GUILayout.Button("Update User DSP buffer size")
                    || useAutomaticDSPBufferSize_previous != this.audioStreamInput.useAutomaticDSPBufferSize)
                {
                    if (Application.isPlaying)
                    {
                        // update is only when needed..
                        this.audioStreamInput.SetUserDSPBuffers(this.dspBufferLength_new, this.dspBufferCount_new);

                        // verify set buffers
                        this.audioStreamInput.GetDSPBufferSize(out this.dspBufferLength_new, out this.dspBufferCount_new);
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Label("Keep the combination of DSP buffer length and count as small as possible for small latency\r\nThe default in this demo scene (64) seems to be working with Best Latency Unity audio setting on desktops.\r\nBe careful esp. on lower end/Android devices - it might be necessary to keep DSP buffer size and count higher, e.g. 256/8, otherwise FMOD might struggle or ignore it completely.");
            }

            GUILayout.Label(string.Format("Input mixer latency average: {0} ms", this.audioStreamInput.latencyAverage), AudioStreamMainScene.guiStyleLabelNormal);

            var useUnityToResampleAndMapChannels_previous = this.audioStreamInput.useUnityToResampleAndMapChannels;

            this.audioStreamInput.useUnityToResampleAndMapChannels = GUILayout.Toggle(this.audioStreamInput.useUnityToResampleAndMapChannels, "Use Unity to resample and map inputs to outputs (note: might drift over time)");
            GUILayout.Label("^ when OFF Speex resampler is used which should help with drifting, but audio quality is not the best esp. when going from lower rates; it is sufficient, however, for doing reactive work based on audio; mapping of inputs to outputs is done automatically in that case (displayed below) - this might not be correct for all situations, but it's possible to provide a custom mix matrix in those cases");

            if (useUnityToResampleAndMapChannels_previous != this.audioStreamInput.useUnityToResampleAndMapChannels)
            {
                // invoke restart if needed by calling change of dsp buffer
                if (Application.isPlaying)
                {
                    if (this.audioStreamInput.useAutomaticDSPBufferSize)
                        this.audioStreamInput.SetAutomaticDSPBufferSize();
                    else
                        this.audioStreamInput.SetUserDSPBuffers(this.dspBufferLength_new, this.dspBufferCount_new);

                    // verify set buffers
                    this.audioStreamInput.GetDSPBufferSize(out this.dspBufferLength_new, out this.dspBufferCount_new);
                }
            }

            GUILayout.Label("Recording will automatically restart if it was running after changing these.", AudioStreamMainScene.guiStyleLabelNormal);

            if (!this.audioStreamInput.useUnityToResampleAndMapChannels)
            {
                // display current mix matrix
                GUILayout.Label("Current mix matrix (rows - output speakers, columns - input channels):", AudioStreamMainScene.guiStyleLabelMiddle);

                var mm = this.audioStreamInput.GetMixMatrix();
                if (mm != null && mm.Rank == 2)
                {
                    for (var row = 0; row < mm.GetLength(0); ++row)
                    {
                        var srow = string.Empty;
                        for (var column = 0; column < mm.GetLength(1); ++column)
                            srow += " " + mm[row, column];

                        GUILayout.Label(srow, AudioStreamMainScene.guiStyleLabelSmall);
                    }
                }
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label("Gain: ", AudioStreamMainScene.guiStyleLabelNormal);

            this.audioStreamInput.gain = GUILayout.HorizontalSlider(this.audioStreamInput.gain, 0f, 5f);
            GUILayout.Label(Mathf.Round(this.audioStreamInput.gain * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.audioStreamInput.isRecording ? "Stop" : "Record", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.audioStreamInput.isRecording)
                    this.audioStreamInput.Stop();
                else
                    this.audioStreamInput.Record();

            if (this.audioStreamInput.isRecording)
            {
                if (GUILayout.Button(this.audioStreamInput.isPaused ? "Resume" : "Pause", AudioStreamMainScene.guiStyleButtonNormal))
                    if (this.audioStreamInput.isPaused)
                        this.audioStreamInput.Pause(false);
                    else
                        this.audioStreamInput.Pause(true);
            }

            GUILayout.EndHorizontal();

            this.audioStreamInput.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle(this.audioStreamInput.GetComponent<AudioSourceMute>().mute, "Mute output");
        }
    }
}
