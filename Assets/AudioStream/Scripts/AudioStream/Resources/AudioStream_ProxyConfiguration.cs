// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// A scriptable object holding the network proxy configuration
    /// All UnityWeRequests (playlist retrieval) will use it via UNITY_PROXYSERVER environment variable and all FMOD streaming will use it directly set before streaming starts
    /// </summary>
    // [CreateAssetMenu]
    public class AudioStream_ProxyConfiguration : ScriptableObject
    {
        /// <summary>
        /// should be working for all from 5.5.4 and up 
        /// UNITY_PROXYSERVER=http://user:password@domain.com:port
        /// these seem to be working as well, but are dangerous D
        /// HTTP_PROXY=http://user:password@domain.com:port
        /// HTTPS_PROXY=http://user:password@domain.com:port
        /// </summary>
        /// test: "127.0.0.1:8118"
        const string UNITY_PROXYSERVER_ENVKEY = "UNITY_PROXYSERVER";

        // ========================================================================================================================================
        #region editor + SO singleton
        [Header("[Proxy]")]
        [Tooltip("Proxy server DNS name or IP\r\n(Used by UnityWebRequest for playlist retrieval and separately by FMOD for final connection)\r\n\r\nLeave blank for AudioStream components to not use a proxy (default)\r\n\r\nNote: In general it is necessary to restart application/Unity Editor after change (user environment variable is not immediately properly picked up by runtime for UnityWebRequest when changed, this is not needed for FMOD itself however)")]
        [SerializeField]
        public string proxyServerName = string.Empty;
        [Tooltip("Proxy server port\r\n\r\nThis value is always appended to proxy server name")]
        [SerializeField]
        public int proxyServerPort = 0;
        [Header("[Proxy authentication]")]
        [Tooltip("Optional user name for authentication for the proxy\r\n(Note: this is *just* proxy server authentication - any other advanced stuff such as setting custom HTTP/S headers is currently not supported by FMOD network queries)\r\n\r\nLeave blank to not use (default)")]
        [SerializeField]
        public string proxyServerUsername = string.Empty;
        [Tooltip("Optional user password for proxy server authentication")]
        [SerializeField]
        public string proxyServerUserpass = string.Empty;
        /// <summary>
        /// SO loaded instance from Resources
        /// </summary>
        static AudioStream_ProxyConfiguration _instance;
        public static AudioStream_ProxyConfiguration Instance
        {
            get
            {
                if (AudioStream_ProxyConfiguration._instance == null)
                {
                    AudioStream_ProxyConfiguration._instance = Resources.Load<AudioStream_ProxyConfiguration>("AudioStream_ProxyConfiguration");
                }

                return AudioStream_ProxyConfiguration._instance;
            }
        }
        /// <summary>
        /// Returns current proxy as string with either plain text or hidden password
        /// </summary>
        /// <param name="forDisplay">If true password is not included (it is a slight lol since it can be found in resources and/or depending on where user saves it, but..)</param>
        /// <returns></returns>
        public string ProxyString(bool forDisplay)
        {
            string result = null;

            if (!string.IsNullOrEmpty(AudioStream_ProxyConfiguration.Instance.proxyServerName))
            {
                if (!string.IsNullOrEmpty(AudioStream_ProxyConfiguration.Instance.proxyServerUsername)
                    || !string.IsNullOrEmpty(AudioStream_ProxyConfiguration.Instance.proxyServerUserpass)
                    )
                    result = string.Format("{0}:{1}@", AudioStream_ProxyConfiguration.Instance.proxyServerUsername, forDisplay ? "****" : AudioStream_ProxyConfiguration.Instance.proxyServerUserpass);

                result = string.Format("{0}{1}:{2}", result, AudioStream_ProxyConfiguration.Instance.proxyServerName, AudioStream_ProxyConfiguration.Instance.proxyServerPort);
            }

            return result;
        }
        /// <summary>
        /// Sets user env. variable for Unity webrequest proxy
        /// Changes are picked up by UnityWebRequest/mono/runtime only after restart, though
        /// Only System.EnvironmentVariableTarget.User target worked for UnityWebRequest (stored in registry on Windows)
        /// Called automatically at startup to apply editor serialized SO values; override by user values at runtime by calling UpdateProxySettings after change
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void UpdateEnvVariables()
        {
            // user set proxy, either in editor or changed at runtime
            var envvalue = AudioStream_ProxyConfiguration.Instance.ProxyString(false);

            // set the evn var
            // Using System.EnvironmentVariableTarget.User target since System.EnvironmentVariableTarget.Process alone was not enough (5.5.4)
            // - System.EnvironmentVariableTarget.Process seems to be set as a sideeffect as well
            // .. null removes the variable (registry key)

            System.Environment.SetEnvironmentVariable(AudioStream_ProxyConfiguration.UNITY_PROXYSERVER_ENVKEY, envvalue, System.EnvironmentVariableTarget.User);

            // log proxy set
            if (!string.IsNullOrEmpty(envvalue))
            {
                Debug.LogFormat("[AudioStream_ProxyConfiguration] set user environment variable '{0}' for proxy server to: {1}\r\n=============================================="
                    , AudioStream_ProxyConfiguration.UNITY_PROXYSERVER_ENVKEY
                    , System.Environment.GetEnvironmentVariable(AudioStream_ProxyConfiguration.UNITY_PROXYSERVER_ENVKEY, System.EnvironmentVariableTarget.User)
                    );
            }

            if (string.IsNullOrEmpty(envvalue))
            {
                // remove variable from process environment (see above)
                System.Environment.SetEnvironmentVariable(AudioStream_ProxyConfiguration.UNITY_PROXYSERVER_ENVKEY, null, System.EnvironmentVariableTarget.Process);

                // remove the var w/o logging - don't bother w blank user settings
                // Debug.LogFormat("[AudioStream_ProxyConfiguration] removed user environment variable '{0}'\r\n=============================================="
                //  , AudioStream_ProxyConfiguration.UNITY_PROXYSERVER_ENVKEY
                //  );
            }
        }
        #endregion
        // ========================================================================================================================================
        #region runtime
        /// <summary>
        /// Call this manually after changing the proxy configuration at runtime
        /// Note: don't call this every frame
        /// </summary>
        public void UpdateProxySettings(string _proxyServerName, int _proxyServerPort, string _proxyServerUsername, string _proxyServerUserpass)
        {
            if (_proxyServerName != this.proxyServerName
                || _proxyServerPort != this.proxyServerPort
                || _proxyServerUsername != this.proxyServerUsername
                || _proxyServerUserpass != this.proxyServerUserpass)
            {
                Debug.LogFormat("[AudioStream_ProxyConfiguration] Overriding proxy settings\r\n==============================================");

                this.proxyServerName = _proxyServerName;
                this.proxyServerPort = _proxyServerPort;
                this.proxyServerUsername = _proxyServerUsername;
                this.proxyServerUserpass = _proxyServerUserpass;

                AudioStream_ProxyConfiguration.UpdateEnvVariables();
            }
        }
        #endregion
    }
}