// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

namespace AudioStream
{
    [ExecuteInEditMode()]
    public class AudioStreamNetMQClientDemo : MonoBehaviour
    {
        public AudioStreamNetMQClient audioStreamNetMQClient;

        System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);

        void OnGUI()
        {
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
            GUILayout.Label(About.versionString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

            GUILayout.Label("If a socket is left connected from previous scene, press Disconnect first.", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.Label("==== Decoder", AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Decoder thread priority: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQClient.decoderThreadPriority = (System.Threading.ThreadPriority)GUILayout.SelectionGrid((int)this.audioStreamNetMQClient.decoderThreadPriority, System.Enum.GetNames(typeof(System.Threading.ThreadPriority)), 5, AudioStreamMainScene.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            if (!this.audioStreamNetMQClient.isConnected)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Resampler quality: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.resamplerQuality = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQClient.resamplerQuality, 0, 10, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(this.audioStreamNetMQClient.resamplerQuality.ToString(), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Network");

                GUILayout.Label("Enter running AudioStream server IP and port below and press Connect", AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server IP: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.serverIP = GUILayout.TextField(this.audioStreamNetMQClient.serverIP, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server port: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.serverTransferPort = int.Parse(GUILayout.TextField(this.audioStreamNetMQClient.serverTransferPort.ToString(), GUILayout.MaxWidth(Screen.width / 4)));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Connect", AudioStreamMainScene.guiStyleButtonNormal))
                    this.audioStreamNetMQClient.Connect();
            }
            else
            {
                GUILayout.Label("Decode info:");
                GUILayout.Label(string.Format("Current frame size: {0}", this.audioStreamNetMQClient.frameSize), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Bandwidth: {0}", this.audioStreamNetMQClient.opusBandwidth), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Mode: {0}", this.audioStreamNetMQClient.opusMode), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Channels: {0}", this.audioStreamNetMQClient.serverChannels), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Frames per packet: {0}", this.audioStreamNetMQClient.opusNumFramesPerPacket), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Samples per frame: {0}", this.audioStreamNetMQClient.opusNumSamplesPerFrame), AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.Space(10);

                GUILayout.Label("==== Audio source");

                GUILayout.Label(string.Format("Output sample rate: {0}, channels: {1}", this.audioStreamNetMQClient.clientSampleRate, this.audioStreamNetMQClient.clientChannels), AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.volume = GUILayout.HorizontalSlider(this.audioStreamNetMQClient.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(Mathf.RoundToInt(this.audioStreamNetMQClient.volume * 100f) + " %", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                this.audioStreamNetMQClient.listenHere = GUILayout.Toggle(this.audioStreamNetMQClient.listenHere, "Listen here");

                GUILayout.Space(10);

                GUILayout.Label("==== Network", AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.Label(string.Format("Connected to: {0}:{1}", this.audioStreamNetMQClient.serverIP, this.audioStreamNetMQClient.serverTransferPort), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Server sample rate: {0}, channels: {1}", this.audioStreamNetMQClient.serverSampleRate, this.audioStreamNetMQClient.serverChannels), AudioStreamMainScene.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Network buffer size: {0}", this.audioStreamNetMQClient.networkQueueSize), AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Client thread sleep timeout: ", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.clientThreadSleepTimeout = (int)GUILayout.HorizontalSlider(audioStreamNetMQClient.clientThreadSleepTimeout, 1, 20, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(this.audioStreamNetMQClient.clientThreadSleepTimeout.ToString().PadLeft(2, '0') + " ms", AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Status", AudioStreamMainScene.guiStyleLabelNormal);

                GUILayout.Label(string.Format("State = {0} {1}"
                    , this.audioStreamNetMQClient.decoderRunning ? "Playing" : "Stopped"
                    , this.audioStreamNetMQClient.lastErrorString
                    )
                    , AudioStreamMainScene.guiStyleLabelNormal
                    );

                GUILayout.BeginHorizontal();

                GUILayout.Label(string.Format("Audio buffer size: {0} / available: {1}", this.audioStreamNetMQClient.dspBufferSize, this.audioStreamNetMQClient.capturedAudioSamples), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                var r = Mathf.CeilToInt(((float)this.audioStreamNetMQClient.capturedAudioSamples / (float)this.audioStreamNetMQClient.dspBufferSize) * 10f);
                var c = Mathf.Min(r, 10);

                GUI.color = this.audioStreamNetMQClient.capturedAudioFrame ? Color.Lerp(Color.red, Color.green, c / 10f) : Color.red;

                this.gauge.Length = 0;
                for (int i = 0; i < c; ++i) this.gauge.Append("#");
                GUILayout.Label(this.gauge.ToString(), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUILayout.EndHorizontal();

                GUI.color = Color.white;

                GUILayout.Space(20);
                
                if (GUILayout.Button("Disconnect", AudioStreamMainScene.guiStyleButtonNormal))
                    this.audioStreamNetMQClient.Disconnect();
            }
        }
    }
}