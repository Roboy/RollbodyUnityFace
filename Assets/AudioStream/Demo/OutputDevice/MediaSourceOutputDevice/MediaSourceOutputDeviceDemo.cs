// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class MediaSourceOutputDeviceDemo : MonoBehaviour
{
    /// <summary>
    /// Available audio outputs reported by FMOD
    /// </summary>
    List<FMODSystemsManager.OUTPUT_DEVICE> availableOutputs = new List<FMODSystemsManager.OUTPUT_DEVICE>();
    /// <summary>
    /// FMOD output component to play audio on selected device + its channels
    /// </summary>
    public MediaSourceOutputDevice mediaSourceOutputDevice;
    /// <summary>
    /// demo sample assets stored in 'StreamingAssets/AudioStream'
    /// </summary>
    public string media1_StreamingAssetsFilename = "429956__jack-master__fat-drum-loop-80-bpm-1.wav";
    public string media2_StreamingAssetsFilename = "429957__jack-master__fat-drum-loop-80-bpm-2.wav";
    /// <summary>
    /// FMOD channels (sounds playing) references of the two demo sounds
    /// </summary>
    FMOD.Channel channel1, channel2;
    /// <summary>
    /// has to be independent from user sound/channel to be manipulable when channel is not playing
    /// </summary>
    float channel1_volume = 1f, channel2_volume = 1f;

    #region UI events

    Dictionary<string, string> playbackStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> notificationStatesFromEvents = new Dictionary<string, string>();

    public void OnPlaybackStarted(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback started";
    }
    public void OnPlaybackPaused(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback paused";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback stopped";
    }

    public void OnPlaybackError(string goName, string msg)
    {
        this.playbackStatesFromEvents[goName] = msg;
    }

    public void OnNotificationError(string goName, string msg)
    {
        this.notificationStatesFromEvents[goName] = msg;
    }

    public void OnNotificationDevicesChanged(string goName)
    {
        this.notificationStatesFromEvents[goName] = "Devices changed";
        this.availableOutputs = FMODSystemsManager.AvailableOutputs(this.mediaSourceOutputDevice.logLevel, this.mediaSourceOutputDevice.gameObject.name, this.mediaSourceOutputDevice.OnError);
    }

    #endregion
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = -1; // trigger device change at start
    /// <summary>
    /// user selected output channels for each media/file
    /// </summary>
    bool[] media1_selectedOutputChannels;
    bool[] media2_selectedOutputChannels;
    /// <summary>
    /// change flag(s) to update mix output
    /// </summary>
    bool media1_selectedOutputChannelsChanged = true; // trigger channel change at start
    bool media2_selectedOutputChannelsChanged = true;

    IEnumerator Start()
    {
        while (!this.mediaSourceOutputDevice.ready)
            yield return null;

        // get demo assets paths
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets(this.media1_StreamingAssetsFilename, (path) => this.media1_StreamingAssetsFilename = path);
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets(this.media2_StreamingAssetsFilename, (path) => this.media2_StreamingAssetsFilename = path);

        // check for available outputs once ready, i.e. FMOD is started up
        if (Application.isPlaying)
        {
            string msg = "Available outputs:" + System.Environment.NewLine;

            this.availableOutputs = FMODSystemsManager.AvailableOutputs(this.mediaSourceOutputDevice.logLevel, this.mediaSourceOutputDevice.gameObject.name, this.mediaSourceOutputDevice.OnError);

            for (int i = 0; i < this.availableOutputs.Count; ++i)
                msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
        }
    }

    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + " " + this.mediaSourceOutputDevice.fmodVersion, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("This scene will play two MONO audio files opened from StreamingAssets on selected output device on selected channels of that output\r\n" +
            "You can pick more than one channel for each audio (it will be played on all selected channels), and both files can be played simultaneously on different channel/s and independently from each other\r\n" +
            "This component uses FMOD channels directly, so it doesn't use Unity AudioSource and AudioClips" +
            "(a MONO clip is used to more easily map channels in the audio files to output device channel/s)", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Select output device and press Play (default output device is preselected)", AudioStreamMainScene.guiStyleLabelNormal);

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamMainScene.guiStyleLabelNormal);

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select(s => string.Format("{0}: {1}", s.id, s.name)).ToArray()
            , 1, AudioStreamMainScene.guiStyleButtonNormal);

        if (this.selectedOutput != this.previousSelectedOutput
            && this.availableOutputs.Count > 0)
        {
            if (Application.isPlaying)
            {
                // swtich output
                this.mediaSourceOutputDevice.SetOutput(this.availableOutputs[this.selectedOutput].id);

                // populate output channels
                var chanCount = this.availableOutputs[this.selectedOutput].channels;
                this.media1_selectedOutputChannels = new bool[chanCount];
                this.media2_selectedOutputChannels = new bool[chanCount];

                // turn on 1st channel by default
                if (chanCount > 0)
                {
                    this.media1_selectedOutputChannels[0] = this.media2_selectedOutputChannels[0] = true;
                    this.media1_selectedOutputChannelsChanged = this.media2_selectedOutputChannelsChanged = true;
                }
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.playbackStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        foreach (var p in this.notificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString;

        lastErrorString = this.mediaSourceOutputDevice.GetLastError(out lastError);

        GUILayout.Label(this.mediaSourceOutputDevice.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(string.Format("State = {0}"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label(string.Format("Output device latency average: {0} ms", this.mediaSourceOutputDevice.latencyAverage), AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Space(10);

        GUILayout.Label("Select output channel of the selected output device to play the MONO clip on. You can select more than one channel (the same MONO clip will be played on all of them)\r\nYou can change output channels while playing.", AudioStreamMainScene.guiStyleLabelNormal);

        // Display output channels of currently selected output for each media separately once everything is available
        if (this.availableOutputs.Count > this.selectedOutput)
        {
            using (new GUILayout.HorizontalScope())
            {
                var channels = Enumerable.Range(0, this.availableOutputs[this.selectedOutput].channels)
                    .Select(s => string.Format("CH #{0}", s));

                // media 1
                using (new GUILayout.VerticalScope())
                {
                    this.media1_selectedOutputChannelsChanged = false;

                    using (new GUILayout.HorizontalScope())
                    {
                        for (var i = 0; i < channels.Count(); ++i)
                        {
                            var oldvalue = this.media1_selectedOutputChannels[i];
                            this.media1_selectedOutputChannels[i] = GUILayout.Toggle(this.media1_selectedOutputChannels[i], channels.ElementAt(i), "Button");
                            if (oldvalue != this.media1_selectedOutputChannels[i])
                                this.media1_selectedOutputChannelsChanged = true;
                        }
                    }

                    // display info about media assets used

                    GUILayout.Label("MONO AudioClip", AudioStreamMainScene.guiStyleLabelNormal);

                    GUILayout.Label("Clip: " + this.media1_StreamingAssetsFilename, AudioStreamMainScene.guiStyleLabelNormal);

                    GUILayout.Label("You can adjust gain of the output channel (note that negative values will invert the signal): ");

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

                    this.channel1_volume = (float)System.Math.Round(
                        GUILayout.HorizontalSlider(this.channel1_volume, -1.2f, 1.2f, GUILayout.MaxWidth(Screen.width / 2))
                        , 2
                        );
                    GUILayout.Label(Mathf.Round(this.channel1_volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

                    if (this.channel1_volume != this.mediaSourceOutputDevice.GetVolume(this.channel1))
                        this.mediaSourceOutputDevice.SetVolume(this.channel1, this.channel1_volume);

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(this.mediaSourceOutputDevice.IsSoundPlaying(this.channel1) ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
                        if (this.mediaSourceOutputDevice.IsSoundPlaying(this.channel1))
                        {
                            this.mediaSourceOutputDevice.StopUserSound(this.channel1);
                        }
                        else
                        {
                            var outchannels = channels.Count();
                            var inchannels = 1;
                            var mixmatrix = new float[outchannels, inchannels];
                            for (var o = 0; o < this.media1_selectedOutputChannels.Length; ++o)
                                if (this.media1_selectedOutputChannels[o])
                                    mixmatrix[o, 0] = 1f;

                            this.mediaSourceOutputDevice.PlayUserSound(this.media1_StreamingAssetsFilename, this.channel1_volume, true, mixmatrix, outchannels, inchannels, out this.channel1);
                        }

                    GUILayout.EndHorizontal();


                    // update channel mix matrix
                    if (this.media1_selectedOutputChannelsChanged)
                    {
                        this.media1_selectedOutputChannelsChanged = false;

                        var outchannels = channels.Count();
                        var inchannels = 1;
                        var mixmatrix = new float[outchannels, inchannels];
                        for (var o = 0; o < this.media1_selectedOutputChannels.Length; ++o)
                            if (this.media1_selectedOutputChannels[o])
                                mixmatrix[o, 0] = 1f;

                        this.mediaSourceOutputDevice.SetMixMatrix(this.channel1, mixmatrix, outchannels, inchannels);
                    }
                }

                // media 2
                // same as above fro 2nd clip
                using (new GUILayout.VerticalScope())
                {
                    this.media2_selectedOutputChannelsChanged = false;

                    using (new GUILayout.HorizontalScope())
                    {
                        for (var i = 0; i < channels.Count(); ++i)
                        {
                            var oldvalue = this.media2_selectedOutputChannels[i];
                            this.media2_selectedOutputChannels[i] = GUILayout.Toggle(this.media2_selectedOutputChannels[i], channels.ElementAt(i), "Button");
                            if (oldvalue != this.media2_selectedOutputChannels[i])
                                this.media2_selectedOutputChannelsChanged = true;
                        }
                    }

                    // display info about media assets used

                    GUILayout.Label("MONO AudioClip", AudioStreamMainScene.guiStyleLabelNormal);

                    GUILayout.Label("Clip: " + this.media2_StreamingAssetsFilename, AudioStreamMainScene.guiStyleLabelNormal);

                    GUILayout.Label("You can adjust gain of the output channel (note that negative values will invert the signal): ");

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal);

                    // var volume = this.mediaSourceOutputDevice.GetVolume(this.channel2);
                    this.channel2_volume = (float)System.Math.Round(
                        GUILayout.HorizontalSlider(this.channel2_volume, -1.2f, 1.2f, GUILayout.MaxWidth(Screen.width / 2))
                        , 2
                        );
                    GUILayout.Label(Mathf.Round(this.channel2_volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

                    if (this.channel2_volume != this.mediaSourceOutputDevice.GetVolume(this.channel2))
                        this.mediaSourceOutputDevice.SetVolume(this.channel2, this.channel2_volume);

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(this.mediaSourceOutputDevice.IsSoundPlaying(this.channel2) ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
                        if (this.mediaSourceOutputDevice.IsSoundPlaying(this.channel2))
                        {
                            this.mediaSourceOutputDevice.StopUserSound(this.channel2);
                        }
                        else
                        {
                            var outchannels = channels.Count();
                            var inchannels = 1;
                            var mixmatrix = new float[outchannels, inchannels];
                            for (var o = 0; o < this.media2_selectedOutputChannels.Length; ++o)
                                if (this.media2_selectedOutputChannels[o])
                                    mixmatrix[o, 0] = 1f;

                            this.mediaSourceOutputDevice.PlayUserSound(this.media2_StreamingAssetsFilename, this.channel2_volume, true, mixmatrix, outchannels, inchannels, out this.channel2);
                        }

                    GUILayout.EndHorizontal();


                    // update channel mix matrix
                    if (this.media2_selectedOutputChannelsChanged)
                    {
                        this.media2_selectedOutputChannelsChanged = false;

                        var outchannels = channels.Count();
                        var inchannels = 1;
                        var mixmatrix = new float[outchannels, inchannels];
                        for (var o = 0; o < this.media2_selectedOutputChannels.Length; ++o)
                            if (this.media2_selectedOutputChannels[o])
                                mixmatrix[o, 0] = 1f;

                        this.mediaSourceOutputDevice.SetMixMatrix(this.channel2, mixmatrix, outchannels, inchannels);
                    }
                }
            }

            var anyPlaying = this.mediaSourceOutputDevice.IsSoundPlaying(this.channel1) || this.mediaSourceOutputDevice.IsSoundPlaying(this.channel2);

            if (GUILayout.Button(anyPlaying ? "Stop both" : "Play both", AudioStreamMainScene.guiStyleButtonNormal))
                if (anyPlaying)
                {
                    this.mediaSourceOutputDevice.StopUserSound(this.channel1);
                    this.mediaSourceOutputDevice.StopUserSound(this.channel2);
                }
                else
                {
                    this.mediaSourceOutputDevice.PlayUserSound(this.media1_StreamingAssetsFilename, this.channel1_volume, true, null, 0, 0, out this.channel1);
                    this.mediaSourceOutputDevice.PlayUserSound(this.media2_StreamingAssetsFilename, this.channel2_volume, true, null, 0, 0, out this.channel2);
                }
        }
    }
}