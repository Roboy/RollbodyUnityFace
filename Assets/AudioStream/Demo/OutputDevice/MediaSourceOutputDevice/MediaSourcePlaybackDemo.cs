// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class MediaSourcePlaybackDemo : MonoBehaviour
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
    public string media_StreamingAssetsFilename = "24ch_polywav_16bit_48k.wav";
    /// <summary>
    /// FMOD channel (sounds playing) reference of the sounds
    /// </summary>
    FMOD.Channel channel;
    /// <summary>
    /// has to be independent from user sound/channel to be manipulable when channel is not playing
    /// </summary>
    float channel_volume = 1f;

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

    IEnumerator Start()
    {
        while (!this.mediaSourceOutputDevice.ready)
            yield return null;

        // get demo assets paths
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets(this.media_StreamingAssetsFilename, (path) => this.media_StreamingAssetsFilename = path);

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

        GUILayout.Label("This scene will play testing 24 channel audio file on selected output\r\n" +
            "Output channels selection is not changed so audio file channels are mapped (and down/mixed) to outputs automatically by FMOD\r\n" +
            "This component doesn't use Unity audio", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("Select output device and press Play (default output device is preselected).", AudioStreamMainScene.guiStyleLabelNormal);

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

        // Display output channels of currently selected output for each media separately once everything is available
        if (this.availableOutputs.Count > this.selectedOutput)
        {
            using (new GUILayout.HorizontalScope())
            {
                // media
                using (new GUILayout.VerticalScope())
                {
                    // display info about media assets used

                    GUILayout.Label("24 ch audio clip: " + this.media_StreamingAssetsFilename, AudioStreamMainScene.guiStyleLabelNormal);
                    GUILayout.Label("by Jon Olive of MagicBeans Physical Audio Ltd.", AudioStreamMainScene.guiStyleLabelNormal);

                    GUILayout.Label("Gain of the output mix: ");

                    GUILayout.BeginHorizontal();

                    this.channel_volume = (float)System.Math.Round(
                        GUILayout.HorizontalSlider(this.channel_volume, 0f, 1.2f, GUILayout.MaxWidth(Screen.width / 2))
                        , 2
                        );
                    GUILayout.Label(Mathf.Round(this.channel_volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal);

                    if (this.channel_volume != this.mediaSourceOutputDevice.GetVolume(this.channel))
                        this.mediaSourceOutputDevice.SetVolume(this.channel, this.channel_volume);

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(this.mediaSourceOutputDevice.IsSoundPlaying(this.channel) ? "Stop" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
                        if (this.mediaSourceOutputDevice.IsSoundPlaying(this.channel))
                        {
                            this.mediaSourceOutputDevice.StopUserSound(this.channel);
                        }
                        else
                        {
                            this.mediaSourceOutputDevice.PlayUserSound(this.media_StreamingAssetsFilename, this.channel_volume, true, null, 0, 0, out this.channel);
                        }

                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}