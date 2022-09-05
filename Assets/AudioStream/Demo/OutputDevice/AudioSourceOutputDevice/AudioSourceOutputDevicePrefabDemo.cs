// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioSourceOutputDevicePrefabDemo : MonoBehaviour
{
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMODSystemsManager.OUTPUT_DEVICE> availableOutputs = new List<FMODSystemsManager.OUTPUT_DEVICE>();
    /// <summary>
    /// Unity AudioSource with redirect component prefab
    /// </summary>
    public AudioSourceOutputDevice AudioSourceOutputDevicePrefab;
    /// <summary>
    /// Instantiated game objects from prefab
    /// </summary>
    List<AudioSourceOutputDevice> instantiatedOutputs = new List<AudioSourceOutputDevice>();
    /// <summary>
    /// GUI
    /// </summary>
    int selectedInstance = 0;
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default

    void Start()
    {
        // check for available outputs
        if (Application.isPlaying)
        {
            string msg = "Available outputs:" + System.Environment.NewLine;

            this.availableOutputs = FMODSystemsManager.AvailableOutputs(this.AudioSourceOutputDevicePrefab.logLevel, this.AudioSourceOutputDevicePrefab.gameObject.name, this.AudioSourceOutputDevicePrefab.OnError);

            for (int i = 0; i < this.availableOutputs.Count; ++i)
                msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
        }
    }

    string fmodVersion = string.Empty;

    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        {
            // grab the version for display purposes if a system was created for prefab
            if (this.instantiatedOutputs.Count > 0)
                this.fmodVersion = this.instantiatedOutputs[0].fmodVersion;
        }

        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + " " + this.fmodVersion, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("This scene demonstrates advanced usage of instantiating a prefab with AudioSourceOutputDevice component attached\r\n- prefab has by default its output device set to 0, the output is changed at runtime based on selection", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Select output device and press Instantiate button to create new prefab playing its AudioClip on selected output", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Space(10);

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamMainScene.guiStyleLabelNormal);

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select(s => string.Format("{0}: {1}", s.id, s.name)).ToArray()
            , 1
            , AudioStreamMainScene.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        // GUILayout.Label(":", AudioStreamMainScene.guiStyleLabelNormal);
        if (GUILayout.Button("Instantiate output device prefab and play its AudioClip on selected output", AudioStreamMainScene.guiStyleButtonNormal))
        {
            // NOTE: you *have* to instantiate prefab in a coroutine otherwise initial sound won't be properly released
            StartCoroutine(this.InstantiatieAndPlayAudioSourceOutputDevicePrefab(this.AudioSourceOutputDevicePrefab, this.availableOutputs[this.selectedOutput].id));
        }

        GUILayout.Space(10);

        if (this.instantiatedOutputs.Count > 0)
            GUILayout.Label("Running instances (playback of each one can be stopped/started individually):", AudioStreamMainScene.guiStyleLabelNormal);

        // display list of instances and allow them to be stopped/played
        // this hack for getting pressed button on the selection grid surprisingly works

        this.selectedInstance = -1;
        this.selectedInstance = GUILayout.SelectionGrid(this.selectedInstance, this.instantiatedOutputs.Select(i => (i.GetComponent<AudioSource>().isPlaying ? "Stop " : "Play ") + i.name).ToArray(), 2);
        if (this.selectedInstance > -1)
        {
            if (this.selectedInstance < this.instantiatedOutputs.Count)
            {
                var @as = this.instantiatedOutputs[this.selectedInstance].GetComponent<AudioSource>();
                if (@as.isPlaying)
                    @as.Stop();
                else
                    @as.Play();
            }
        }
    }

    IEnumerator InstantiatieAndPlayAudioSourceOutputDevicePrefab(AudioSourceOutputDevice prefab, int onOutputDevice)
    {
        var asod = Instantiate(prefab);
        asod.name += "#" + (this.instantiatedOutputs.Count + 1);

        while (!asod.ready)
            yield return null;

        // when prefab is instantiated it creates/retrieves FMOD system for its configured output
        // this will restart and create/reuse all systems as needed
        asod.SetOutput(onOutputDevice);

        asod.GetComponent<AudioSource>().Play();

        // this is hard dependendy on this demo scene - remove if needed
        this.instantiatedOutputs.Add(asod);
    }
}