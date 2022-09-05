// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Linq;
using UnityEngine;

namespace AudioStream
{
    [ExecuteInEditMode()]
    public class AudioStreamNetMQSourceDemo : MonoBehaviour
    {
        public AudioStreamNetMQSource audioStreamNetMQSource;

        AudioSource @as;

        void Start()
        {
            this.@as = this.audioStreamNetMQSource.GetComponent<AudioSource>();
        }

        int frameSizeEnumSelection = 3;

        System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);

        void OnGUI()
        {
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
            GUILayout.Label(About.versionString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

            GUILayout.Label("Connect to this network source based on Network address below from other running client instance.", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label("==== Encoder", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Encoder thread priority: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQSource.encoderThreadPriority = (System.Threading.ThreadPriority)GUILayout.SelectionGrid((int)this.audioStreamNetMQSource.encoderThreadPriority, System.Enum.GetNames(typeof(System.Threading.ThreadPriority)), 6, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Encoder application type: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            GUILayout.Label(this.audioStreamNetMQSource.opusApplicationType.ToString(), AudioStreamMainScene.guiStyleLabelNormal);
            GUILayout.EndHorizontal();

            if (this.audioStreamNetMQSource.encoderRunning)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Bitrate: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQSource.bitrate = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.bitrate, 6, 510, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(this.audioStreamNetMQSource.bitrate + " KBits/s", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Complexity: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQSource.complexity = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.complexity, 0, 10, GUILayout.MaxWidth(Screen.width / 2));
                var cdesc = "(Medium)";
                switch (this.audioStreamNetMQSource.complexity)
                {
                    case 0:
                    case 1:
                    case 2:
                        cdesc = "(Low)";
                        break;
                    case 3:
                    case 4:
                    case 5:
                        cdesc = "(Medium)";
                        break;
                    case 6:
                    case 7:
                    case 8:
                        cdesc = "(High)";
                        break;
                    case 9:
                    case 10:
                        cdesc = "(Very High)";
                        break;
                }
                GUILayout.Label(this.audioStreamNetMQSource.complexity + " " + cdesc, AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Rate Control: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQSource.rate = (AudioStreamNetworkSource.RATE)GUILayout.SelectionGrid((int)this.audioStreamNetMQSource.rate, System.Enum.GetNames(typeof(AudioStreamNetworkSource.RATE)), 3, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Frame size: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.frameSizeEnumSelection = GUILayout.SelectionGrid(this.frameSizeEnumSelection, System.Enum.GetNames(typeof(AudioStreamNetworkSource.OPUSFRAMESIZE)).Select(s => s.Split('_')[1]).ToArray(), 6, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
                GUILayout.EndHorizontal();
                switch (this.frameSizeEnumSelection)
                {
                    case 0:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_120;
                        break;
                    case 1:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_240;
                        break;
                    case 2:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_480;
                        break;
                    case 3:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_960;
                        break;
                    case 4:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_1920;
                        break;
                    case 5:
                        this.audioStreamNetMQSource.frameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_2880;
                        break;
                }
            }

            GUILayout.Space(10);

            GUILayout.Label("==== Audio source", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label(string.Format("Output samplerate: {0} channels: {1}", this.audioStreamNetMQSource.serverSampleRate, this.audioStreamNetMQSource.serverchannels), AudioStreamMainScene.guiStyleLabelNormal);

            if (this.@as != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.@as.volume = GUILayout.HorizontalSlider(this.@as.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(Mathf.RoundToInt(this.@as.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();
            }

            this.audioStreamNetMQSource.listenHere = GUILayout.Toggle(this.audioStreamNetMQSource.listenHere, "Listen here");

            GUILayout.Space(10);

            GUILayout.Label("==== Network", AudioStreamMainScene.guiStyleLabelNormal);

            if (this.audioStreamNetMQSource.listenIP != "0.0.0.0")
                GUILayout.Label(string.Format("Running at {0} : {1}", this.audioStreamNetMQSource.listenIP, this.audioStreamNetMQSource.listenPort), AudioStreamMainScene.guiStyleLabelNormal);
            else
                GUILayout.Label("No network seems to be available", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label(string.Format("Network buffer size: {0}", this.audioStreamNetMQSource.networkQueueSize), AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Source thread sleep timeout: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQSource.sourceThreadSleepTimeout = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.sourceThreadSleepTimeout, 1, 20, GUILayout.MaxWidth(Screen.width / 2));
            GUILayout.Label(this.audioStreamNetMQSource.sourceThreadSleepTimeout.ToString().PadLeft(2, '0') + " ms", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("==== Status", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.audioStreamNetMQSource.encoderRunning ? "Playing" : "Stopped"
                , this.audioStreamNetMQSource.lastErrorString
                )
                , AudioStreamMainScene.guiStyleLabelNormal
                );

            GUILayout.BeginHorizontal();

            GUILayout.Label(string.Format("Audio buffer size: {0} / available: {1}", this.audioStreamNetMQSource.dspBufferSize, this.audioStreamNetMQSource.audioSamplesSize), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth( Screen.width / 3 ));

            var r = Mathf.CeilToInt(((float)this.audioStreamNetMQSource.audioSamplesSize / (float)this.audioStreamNetMQSource.dspBufferSize) * 10f);
            var c = Mathf.Min(r, 10);

            GUI.color = Color.Lerp(Color.red, Color.green, c / 10f);

            this.gauge.Length = 0;
            for (int i = 0; i < c; ++i) this.gauge.Append("#");
            GUILayout.Label(this.gauge.ToString(), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            GUILayout.EndHorizontal();
        }
    }
}