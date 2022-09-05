// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class AudioStreamInput_iOSDemo : MonoBehaviour
{
    public AudioStreamInput_iOS audioStreamInput_iOS;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<string> availableInputs = new List<string>();

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
    /// - we need this for / this should be == to/ OAFR signal
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

    IEnumerator Start()
    {
        while (!this.audioStreamInput_iOS.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            var inputs = this.audioStreamInput_iOS.AvailableInputs();

            while (inputs == null || inputs.Length < 1)
                yield return null;

            this.availableInputs = inputs.ToList();

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += i.ToString() + " : " + this.availableInputs[i] + System.Environment.NewLine;
        }

        // get output channels#
        this.outputChannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

        // setup visuals

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
        if (!this.audioStreamInput_iOS.ready)
            return;

        // poll input devices
        this.availableInputs = this.audioStreamInput_iOS.AvailableInputs().ToList();

        // update selection on route change to not crash when Record on nonexistent input
        if (this.selectedInput > this.availableInputs.Count - 1)
            this.selectedInput = 0;

        if (this.audioStreamInput_iOS.isRecording)
        {
            // access the recording buffer and look at some values
            var _as = this.audioStreamInput_iOS.GetComponent<AudioSource>();

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
        GUILayout.Label(AudioStream.About.versionString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);


        GUILayout.Label("This scene properly displays and records from all connected device/s when run on an actual iOS device.", AudioStreamMainScene.guiStyleLabelNormal);

        if (Application.platform != RuntimePlatform.IPhonePlayer)
            GUILayout.Label("On all other platforms only testing sine signal is played from a dummy device.", AudioStreamMainScene.guiStyleLabelNormal);
        else
            GUILayout.Label("You should be able to dis/connect e.g. Bluetooth headset/ AirPods and select it for recording.", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Press 'Update audio session' to update iOS audio session if device is dis/connected and notification is not properly handled (i.e. it doesn't disappear/show up).\r\nThis setup is called automatically on scene load.", AudioStreamMainScene.guiStyleLabelNormal);

        if (GUILayout.Button("Update audio session", AudioStreamMainScene.guiStyleButtonNormal))
            this.audioStreamInput_iOS.UpdateAudioSession();

        GUILayout.Label("Choose from available recording devices and press Record.\r\nThe cube will react to sound and signal energy is computed from AudioSource's GetOutputData for each output channel separately.", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Available recording devices:", AudioStreamMainScene.guiStyleLabelNormal);

        // selection of available audio inputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.Select((input, index) => string.Format("{0}: {1}", index, input)).ToArray(), 1
            , AudioStreamMainScene.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        if (this.selectedInput != this.previousSelectedInput)
        {
            if (Application.isPlaying)
            {
                this.audioStreamInput_iOS.Stop();
                this.audioStreamInput_iOS.recordDeviceId = this.selectedInput;
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
            string lastErrorString = this.audioStreamInput_iOS.GetLastError(out lastError);

            GUILayout.Label(this.audioStreamInput_iOS.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.audioStreamInput_iOS.isRecording ? "Recording" : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label("Signal energy per output channel from GetOutputData: ", AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < this.signalChannelsEnergies.Count; ++i)
                GUILayout.Label(string.Format("CH #{0}: {1}", i, System.Math.Round(this.signalChannelsEnergies[i], 6).ToString().PadRight(8, '0')));
            GUILayout.EndHorizontal();

            // TODO: readd speex resampling in the future once its performance is resolved
            // this.audioStreamInput_iOS.useUnityToResampleAndMapChannels = GUILayout.Toggle(this.audioStreamInput_iOS.useUnityToResampleAndMapChannels, "Use Unity to resample and map inputs to outputs (note: might drift over time)");

            GUILayout.Label("Recording will automatically restart if it was running after changing these.", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Gain: ", AudioStreamMainScene.guiStyleLabelNormal);

            this.audioStreamInput_iOS.gain = GUILayout.HorizontalSlider(this.audioStreamInput_iOS.gain, 0f, 5f);
            GUILayout.Label(Mathf.Round(this.audioStreamInput_iOS.gain * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.audioStreamInput_iOS.isRecording ? "Stop" : "Record", AudioStreamMainScene.guiStyleButtonNormal))
                if (this.audioStreamInput_iOS.isRecording)
                    this.audioStreamInput_iOS.Stop();
                else
                    StartCoroutine(this.audioStreamInput_iOS.Record());

            GUILayout.EndHorizontal();

            this.audioStreamInput_iOS.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle(this.audioStreamInput_iOS.GetComponent<AudioSourceMute>().mute, "Mute output");


            GUILayout.Label("channels: " + this.audioStreamInput_iOS.recChannels);
            GUILayout.Label("rate    : " + this.audioStreamInput_iOS.recRate);

            GUILayout.Label("native pcm ptr : " + (this.audioStreamInput_iOS.pcm_ptr == System.IntPtr.Zero ? "" : "updated"));
            GUILayout.Label("samples : " + this.audioStreamInput_iOS.pcm_samples);
            GUILayout.Label("bytes per sample : " + this.audioStreamInput_iOS.pcm_bytesPerSample);
        }
    }
}