// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays all demo scenes list and provides their un/loading
/// If scene with this script is opened in the Editor, try to populate Build Settings with AudioStream scenes (since that is what the user most likely wants)
/// </summary>
public class AudioStreamMainScene : MonoBehaviour
{
    public AudioStream.BuildSettings buildSettings;
    List<string> sceneNames = new List<string>();

    // (very) basic single GO instance handling
    static AudioStreamMainScene instance;

    void Awake()
    {
        if (AudioStreamMainScene.instance == null)
        {
            AudioStreamMainScene.instance = this;
            DontDestroyOnLoad(this.gameObject);

            // override proxy settings by saved user config, if it does exist
            var proxyServerName = PlayerPrefs.GetString(AudioStreamProxySettingsDemo.prefs_prefix + "proxyServerName", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerName);
            var proxyServerPort = PlayerPrefs.GetInt(AudioStreamProxySettingsDemo.prefs_prefix + "proxyServerPort", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerPort);
            var proxyServerUsername = PlayerPrefs.GetString(AudioStreamProxySettingsDemo.prefs_prefix + "proxyServerUsername", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUsername);
            var proxyServerUserpass = PlayerPrefs.GetString(AudioStreamProxySettingsDemo.prefs_prefix + "proxyServerUserpass", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUserpass);

            AudioStream.AudioStream_ProxyConfiguration.Instance.UpdateProxySettings(proxyServerName, proxyServerPort, proxyServerUsername, proxyServerUserpass);
        }
        else
        {
            // we are being loaded again - destroy ourselves since the other instance is already alive
            Destroy(this.gameObject);
        }

        // this will append all AudioStream demo scenes if needed to build settings when run in the Editor
        this.buildSettings.AddAudioStreamDemoScenesToBuildSettingsIfNeeded();
    }

    void Start()
    {
        // iPhone 7 326
        // macOS 256
        if (Screen.dpi > 255) // ~~ retina
        {
            AudioStreamMainScene.dpiMult = 2;
            AudioStreamMainScene.ResetStyles();
        }
    }

    #region dpi size adjusted styles
    /// <summary>
    /// try to make fonts more visible on high DPI resolutions
    /// </summary>
    static int dpiMult = 1;
    static GUIStyle _guiStyleLabelSmall = GUIStyle.none;
    public static GUIStyle guiStyleLabelSmall
    {
        get
        {
            if (AudioStreamMainScene._guiStyleLabelSmall == GUIStyle.none)
            {
                AudioStreamMainScene._guiStyleLabelSmall = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamMainScene._guiStyleLabelSmall.fontSize = 8 * AudioStreamMainScene.dpiMult;
                AudioStreamMainScene._guiStyleLabelSmall.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamMainScene._guiStyleLabelSmall;
        }
        protected set { AudioStreamMainScene._guiStyleLabelSmall = value; }
    }
    static GUIStyle _guiStyleLabelMiddle = GUIStyle.none;
    public static GUIStyle guiStyleLabelMiddle
    {
        get
        {
            if (AudioStreamMainScene._guiStyleLabelMiddle == GUIStyle.none)
            {
                AudioStreamMainScene._guiStyleLabelMiddle = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamMainScene._guiStyleLabelMiddle.fontSize = 10 * AudioStreamMainScene.dpiMult;
                AudioStreamMainScene._guiStyleLabelMiddle.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamMainScene._guiStyleLabelMiddle;
        }
        protected set { AudioStreamMainScene._guiStyleLabelMiddle = value; }
    }
    static GUIStyle _guiStyleLabelNormal = GUIStyle.none;
    public static GUIStyle guiStyleLabelNormal
    {
        get
        {
            if (AudioStreamMainScene._guiStyleLabelNormal == GUIStyle.none)
            {
                AudioStreamMainScene._guiStyleLabelNormal = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamMainScene._guiStyleLabelNormal.fontSize = 11 * AudioStreamMainScene.dpiMult;
                AudioStreamMainScene._guiStyleLabelNormal.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamMainScene._guiStyleLabelNormal;
        }
        protected set { AudioStreamMainScene._guiStyleLabelNormal = value; }
    }
    static GUIStyle _guiStyleButtonNormal = GUIStyle.none;
    public static GUIStyle guiStyleButtonNormal
    {
        get
        {
            if (AudioStreamMainScene._guiStyleButtonNormal == GUIStyle.none)
            {
                AudioStreamMainScene._guiStyleButtonNormal = new GUIStyle(GUI.skin.GetStyle("Button"));
                AudioStreamMainScene._guiStyleButtonNormal.fontSize = 14 * AudioStreamMainScene.dpiMult;
                AudioStreamMainScene._guiStyleButtonNormal.margin = new RectOffset(5, 5, 5, 5);
            }
            return AudioStreamMainScene._guiStyleButtonNormal;
        }
        protected set { AudioStreamMainScene._guiStyleButtonNormal = value; }
    }
    static void ResetStyles()
    {
        AudioStreamMainScene.guiStyleButtonNormal =
            AudioStreamMainScene.guiStyleLabelMiddle =
            AudioStreamMainScene.guiStyleLabelNormal =
            AudioStreamMainScene.guiStyleLabelSmall =
            GUIStyle.none;
    }
    #endregion
    int selectedSceneGroup = 0;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        // DPI debugging
        /*
        {
            var tHeight = Screen.height / 32;

            using (new GUILayout.AreaScope(new Rect(0, 0, Screen.width, tHeight)))
            {
                AudioStreamMainScene.ResetStyles();

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("DPIx: ");
                    AudioStreamMainScene.dpiMult = (int)GUILayout.HorizontalSlider(AudioStreamMainScene.dpiMult, 1, 10);
                    GUILayout.Label(string.Format("{0}, DPI: {1}", AudioStreamMainScene.dpiMult, Screen.dpi));
                }
            }
        }
        */

        // display scene list on main scene
        if (SceneManager.GetActiveScene().name == AudioStream.BuildSettings.AudioStreamMainSceneName)
        {
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
            GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
            GUILayout.Label(AudioStream.About.versionString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
            GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

            GUILayout.Label("Pick demo scenes group", AudioStreamMainScene.guiStyleLabelNormal);

            this.selectedSceneGroup = GUILayout.SelectionGrid(this.selectedSceneGroup, new string[] { "Features scenes", "Stress tests scenes", "Legacy components" }, 3
                , AudioStreamMainScene.guiStyleButtonNormal
                , GUILayout.MaxWidth(Screen.width));
            switch (this.selectedSceneGroup)
            {
                case 0:
                    this.sceneNames = this.buildSettings.audioStreamSceneNames.Where(s => !s.Contains("StressTest") && !s.Contains("Legacy")).ToList();
                    break;
                case 1:
                    this.sceneNames = this.buildSettings.audioStreamSceneNames.Where(s => s.Contains("StressTest") && !s.Contains("Legacy")).ToList();
                    break;
                case 2:
                    this.sceneNames = this.buildSettings.audioStreamSceneNames.Where(s => s.Contains("Legacy")).ToList();
                    break;
            }

            GUILayout.Label("Press button to run a demo scene", AudioStreamMainScene.guiStyleLabelNormal);

            this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

            foreach (var sceneName in this.sceneNames)
            {
                if (GUILayout.Button(sceneName, AudioStreamMainScene.guiStyleButtonNormal))
                    SceneManager.LoadScene(sceneName);
            }

            // cache cleanup button
            GUILayout.Label(string.Format("[Cleanup temporary cache w downloads/decoded audio at {0}]", Application.temporaryCachePath), AudioStreamMainScene.guiStyleLabelNormal);
            if (GUILayout.Button("Cleanup cache"))
            {
                foreach (var fp in System.IO.Directory.GetFiles(Application.temporaryCachePath))
                {
                    System.IO.File.Delete(fp);
                }
            }

            GUILayout.EndScrollView();
        }
        else
        {
            // display navigation bottom bar on a single scene

            // bottom bar line height
            var bHeight = Screen.height / 16;

            using (new GUILayout.AreaScope(new Rect(0, Screen.height - bHeight, Screen.width, bHeight)))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(" < ", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        var i = this.sceneNames.IndexOf(SceneManager.GetActiveScene().name) - 1;
                        if (i < 0)
                            i = this.sceneNames.Count - 1;

                        // find the scene in full scene list and load it
                        i = this.buildSettings.audioStreamSceneNames.IndexOf(this.sceneNames[i]);
                        SceneManager.LoadScene(this.buildSettings.audioStreamSceneNames[i]);
                    }

                    GUILayout.Label(SceneManager.GetActiveScene().name, AudioStreamMainScene.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight));

                    if (GUILayout.Button("Return to main", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        SceneManager.LoadScene(AudioStream.BuildSettings.AudioStreamMainSceneName);
                        Resources.UnloadUnusedAssets();
                    }
                    if (GUILayout.Button(" > ", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        var i = this.sceneNames.IndexOf(SceneManager.GetActiveScene().name) + 1;
                        if (i > this.sceneNames.Count - 1)
                            i = 0;

                        // find the scene in full scene list and load it
                        i = this.buildSettings.audioStreamSceneNames.IndexOf(this.sceneNames[i]);
                        SceneManager.LoadScene(this.buildSettings.audioStreamSceneNames[i]);
                    }
                }
            }
        }
    }
}