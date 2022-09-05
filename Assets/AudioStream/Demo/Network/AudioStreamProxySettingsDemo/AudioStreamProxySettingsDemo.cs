// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

[ExecuteInEditMode]
public class AudioStreamProxySettingsDemo : MonoBehaviour
{
    /// <summary>
    /// PlayerPrefs items prefix for application user settings for proxy
    /// </summary>
    public const string prefs_prefix = "AudioStreamDemo_";

    void OnDestroy()
    {
        // save application proxy settings
        PlayerPrefs.SetString(prefs_prefix + "proxyServerName", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerName);
        PlayerPrefs.SetInt(prefs_prefix + "proxyServerPort", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerPort);
        PlayerPrefs.SetString(prefs_prefix + "proxyServerUsername", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUsername);
        PlayerPrefs.SetString(prefs_prefix + "proxyServerUserpass", AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUserpass);
    }

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamMainScene.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.buildString, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.defaultOutputProperties, AudioStreamMainScene.guiStyleLabelMiddle);
        GUILayout.Label(AudioStream.About.proxyUsed, AudioStreamMainScene.guiStyleLabelMiddle);

        GUILayout.Label("Enter proxy settings to be used for all network queries and for streaming from network\r\nThese will be saved to PlayerPrefs of this application", AudioStreamMainScene.guiStyleLabelNormal);
        GUILayout.Label("Note: restart is needed if changed", AudioStreamMainScene.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Proxy server name/ip: ", AudioStreamMainScene.guiStyleLabelNormal);
        AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerName = GUILayout.TextField(AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerName);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Proxy server port (needed if server name is set): ", AudioStreamMainScene.guiStyleLabelNormal);
        int port = 0;
        if (int.TryParse(GUILayout.TextField(AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerPort.ToString()), out port))
            AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerPort = port;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Proxy server user name (optional): ", AudioStreamMainScene.guiStyleLabelNormal);
        AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUsername = GUILayout.TextField(AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUsername);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Proxy server user pass (optional): ", AudioStreamMainScene.guiStyleLabelNormal);
        AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUserpass = GUILayout.TextField(AudioStream.AudioStream_ProxyConfiguration.Instance.proxyServerUserpass);
        GUILayout.EndHorizontal();
    }
}