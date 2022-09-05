// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteInEditMode]
public class AudioStreamMemoryDemo : MonoBehaviour
{
    public AudioStreamMemory asMemory;
    public AudioSource userAudioSource;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        // playback started means also decoding has been started
        this.streamsStatesFromEvents[goName] = "decoding";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
        this.@as_isPaused = false;
    }
    /// <summary>
    /// Invoked when decoding has finished and clip is created
    /// </summary>
    /// <param name="goName"></param>
    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "decoded & clip created";
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
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value as string} };
        }
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnAudioClipCreated(string goName, AudioClip newAudioClip)
    {
        Destroy(this.userAudioSource.clip);
        this.userAudioSource.clip = newAudioClip;

        if (this.playClipAfterDecoding)
            this.userAudioSource.Play();
    }
    #endregion

    byte[] bytes = null;
    GCHandle handle;
    IEnumerator Start()
    {
        while (!this.asMemory.ready)
            yield return null;

        // load compressed audio to memory

        string filepath = "";
        yield return StartCoroutine(AudioStreamDemoSupport.GetFilenameFromStreamingAssets("electronic-senses-shibuya.mp3", (newDestination) => filepath = newDestination));

        this.bytes = File.ReadAllBytes(filepath);

        this.handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        this.asMemory.memoryLocation = handle.AddrOfPinnedObject();
        this.asMemory.memoryLength = (uint)bytes.Length;
    }

    void OnDestroy()
    {
        if (this.handle.IsAllocated)
            this.handle.Free();
    }

    bool @as_isPaused = false;
    bool playClipAfterDecoding = true;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (this.asMemory != null ? " " + this.asMemory.fmodVersion : ""), AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Press Decode to start decoding demo track from memory\r\nDemo will read the file into a memory buffer, AudioStreamMemory will then read and decode audio from it; once done/stopped it will then construct an AudioClip and pass it to user AudioSource for playback\r\n" +
            "The whole decoding and new clip creation is done in-memory (can optionally use disk cache)", AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamMainScene.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.asMemory.GetLastError(out lastError);

        GUILayout.Label(this.asMemory.GetType() + "   ========================================", AudioStreamMainScene.guiStyleLabelSmall);


        /*
        uint input = 0;
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Reader buffer size (bytes): ", AudioStreamMainScene.guiStyleLabelNormal);
            if (uint.TryParse(
                GUILayout.TextField(this.asMemory.readlength.ToString())
                , out input
                )
                )
            {
                if (input >= 0)
                    this.asMemory.readlength = input;
            }
        }
        */

        this.playClipAfterDecoding = GUILayout.Toggle(this.playClipAfterDecoding, "Play decoded clip immediately after the decoding is stopped or the whole file is processed");

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.asMemory.isPlaying ? "Playing" + (this.asMemory.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamMainScene.guiStyleLabelNormal);

        if (this.userAudioSource)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(string.Format("Volume: {0} %", Mathf.Round(this.userAudioSource.volume * 100f)), AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                this.userAudioSource.volume = GUILayout.HorizontalSlider(this.userAudioSource.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
            }
        }

        GUILayout.Label(string.Format("Decoded bytes: {0} (encoded memory size: {1})", this.asMemory.decoded_bytes, this.asMemory.memoryLength + " b"), AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Last decoding + AudioClip creation took: {0} ms", this.asMemory.decodingToAudioClipTimeInMs), AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.Label("You can enter a unique cache id, subsequent Plays will go straight into cache instead of memory decoding: ", AudioStreamMainScene.guiStyleLabelNormal);
        this.asMemory.cacheIdentifier = GUILayout.TextField(this.asMemory.cacheIdentifier);

        if (this.bytes == null)
        {
            GUILayout.Label("Waiting for file to load into memory....", AudioStreamMainScene.guiStyleLabelNormal);
        }
        else
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.asMemory.isPlaying ? "Stop Decoding" : "Decode" + (this.playClipAfterDecoding ? " and Play" : ""), AudioStreamMainScene.guiStyleButtonNormal))
                if (this.asMemory.isPlaying)
                    this.asMemory.Stop();
                else
                {
                    this.asMemory.Play();
                }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        if (this.userAudioSource != null && this.userAudioSource.clip != null)
        {
            // clip decoded

            GUILayout.Label(string.Format("Decoded AudioClip channels: {0}, length: {1} s, playback position: {2} s", this.userAudioSource.clip.channels, this.userAudioSource.clip.length, this.userAudioSource.time), AudioStreamMainScene.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.userAudioSource.isPlaying || this.@as_isPaused ? "Stop Playback" : "Play", AudioStreamMainScene.guiStyleButtonNormal))
            {
                if (this.userAudioSource.isPlaying || this.@as_isPaused)
                    this.userAudioSource.Stop();
                else if (!this.@as_isPaused)
                    this.userAudioSource.Play();

                this.@as_isPaused = false;
            }

            if (this.userAudioSource.isPlaying || this.@as_isPaused)
            {
                if (GUILayout.Button(this.@as_isPaused ? "Resume Playback" : "Pause Playback", AudioStreamMainScene.guiStyleButtonNormal))
                    if (this.@as_isPaused)
                    {
                        this.userAudioSource.UnPause();
                        this.@as_isPaused = false;
                    }
                    else
                    {
                        this.userAudioSource.Pause();
                        this.@as_isPaused = true;
                    }
            }

            GUILayout.EndHorizontal();
        }

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.asMemory.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamMainScene.guiStyleLabelNormal);





        GUILayout.Label("Audio track used in this demo:", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Shibuya by Electronic Senses | https://soundcloud.com/electronicsenses", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Music promoted by https://www.free-stock-music.com", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Creative Commons Attribution - ShareAlike 3.0 Unported", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("https://creativecommons.org/licenses/by-sa/3.0/deed.en_US", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("audio file is read from StreamingAssets", AudioStreamMainScene.guiStyleLabelNormal);
    }
}