// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// A ScriptableObject auto populating list of scenes and info from the build settings
    /// Runs only in the Editor when required via OnEnable, exposes gathered scenes via audioStreamSceneNames at runtime
    /// </summary>
    // leave this commented out for release to not clutter the UI
    // [CreateAssetMenu(fileName = "BuildSettings", menuName = "AudioStream BuildSettings")]
    public class BuildSettings : ScriptableObject
    {
        /// <summary>
        /// main scene name for build settings scene names filtering and main scene decisions
        /// </summary>
        public const string AudioStreamMainSceneName = "AudioStreamMainScene";
        /// <summary>
        /// List of AudioStream scenes from Build Settings
        /// </summary>
        public List<string> audioStreamSceneNames = new List<string>();
        /// <summary>
        /// Fields serialized in SO from Editor run
        /// </summary>
        public string scriptingBackend;
        public string scriptingRuntimeVersion;
        public string apiLevel;
        public string buildTime;                // GetType().Assembly.Location is empty on IL2CPP so can be used only at Editor time
        /// <summary>
        /// Little hack to pass values from instance. Since BuildSettings.asset is referenced in the main scene, this should be populated for later access e.g. in build.
        /// Proper singleton via Resources.FindObjectsOfTypeAll for this SO didn't seem to work for some reason in 5.4.5
        /// </summary>
        public static string scriptingBackendS;
        public static string buildTimeS;
        /// <summary>
        /// Adds AudioStream demo scenes to Build Settings if needed - called from main demo scene -
        /// </summary>
        public void AddAudioStreamDemoScenesToBuildSettingsIfNeeded()
        {
#if UNITY_EDITOR
            // current all scenes in build settings
            var originalScenesInBuildSettings = UnityEditor.EditorBuildSettings.scenes;

            // find all audiostream demo scenes in the project
            List<string> audioStreamDemoScenePaths = new List<string>();

            // find all objects of type scene
            // TODO: there will be fun when asset store packages arrive..
            foreach (var s in UnityEditor.AssetDatabase.FindAssets("t:scene", new string[] { "Assets" }))
            {
                // convert object's GUID to asset path
                var scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(s);

                // add AudioStream scene path
                if (scenePath.Contains("AudioStream/Demo/"))
                    audioStreamDemoScenePaths.Add(scenePath);
            }

            // find which demo scenes are currently not in the build settings
            // scene paths match:
            // Scene in project:        Assets/AudioStream/Demo/_MainScene/AudioStreamMainScene.unity
            // Scene in Build Settings: Assets/AudioStream/Demo/_MainScene/AudioStreamMainScene.unity

            List<string> audioStreamDemoScenePathsToAdd = new List<string>();
            var originalScenesInBuildSettingsPaths = UnityEditor.EditorBuildSettings.scenes.Select(s => s.path);

            foreach (var scenePath in audioStreamDemoScenePaths)
            {
                if (!originalScenesInBuildSettingsPaths.Contains(scenePath))
                    audioStreamDemoScenePathsToAdd.Add(scenePath);
            }

            // new scenes for build settings
            List<UnityEditor.EditorBuildSettingsScene> newScenesInBuildSettings = new List<UnityEditor.EditorBuildSettingsScene>();

            // add all original scenes and make sure all demo scenes are enabled
            var updatedCount = 0;
            for (int i = 0; i < originalScenesInBuildSettings.Length; ++i)
            {
                if (audioStreamDemoScenePaths.Contains(originalScenesInBuildSettings[i].path))
                {
                    updatedCount += originalScenesInBuildSettings[i].enabled ? 0 : 1;
                    newScenesInBuildSettings.Add(new UnityEditor.EditorBuildSettingsScene(originalScenesInBuildSettings[i].path, true));
                }
                else
                    newScenesInBuildSettings.Add(originalScenesInBuildSettings[i]);
            }

            // add new scenes
            foreach (var scenePath in audioStreamDemoScenePathsToAdd)
                newScenesInBuildSettings.Add(new UnityEditor.EditorBuildSettingsScene(scenePath, true));

            if (updatedCount > 0 || audioStreamDemoScenePathsToAdd.Count > 0)
            {
                Debug.LogWarningFormat("Automatically enabled {0} and added {1} AudioStream demo scene/s to Build Settings scene list", updatedCount, audioStreamDemoScenePathsToAdd.Count);
                Debug.LogWarningFormat("Please restart the scene to reload Build Settings if main demo scene starts with empty/not fully populated demo scenes list");
            }

            // update editor build settings
            UnityEditor.EditorBuildSettings.scenes = newScenesInBuildSettings.ToArray();
#endif
            // update scene names for SO when in editor
            this.OnEnable();
        }
        /// <summary>
        /// Transfer scene list and editor time settings into runtime fields
        /// </summary>
        void OnEnable()
        {
#if UNITY_EDITOR
            // populate scene list from build settings in Editor
            this.audioStreamSceneNames = new List<string>();

            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
                if (scene.path.Contains("AudioStream/Demo/") && scene.enabled && !scene.path.Contains(BuildSettings.AudioStreamMainSceneName)) // just official AudioStream demo scenes except the main scene
                    this.audioStreamSceneNames.Add(Path.GetFileNameWithoutExtension(scene.path));

            // transfer editor time values into SO
            var buildTargetGroup = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;

            this.scriptingBackend = UnityEditor.PlayerSettings.GetScriptingBackend(buildTargetGroup).ToString();

            this.scriptingRuntimeVersion =
#if UNITY_2017_1_OR_NEWER
#if UNITY_2019_3_OR_NEWER
                // scripting runtime version obsoleted in 2019.3
                string.Empty;
#else
                UnityEditor.PlayerSettings.scriptingRuntimeVersion.ToString();
#endif
#else
                string.Empty;
#endif
            this.apiLevel =
#if UNITY_5_6_OR_NEWER
                UnityEditor.PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup).ToString();
#else
                string.Empty;
#endif
            this.buildTime = System.IO.File.GetCreationTime(this.GetType().Assembly.Location).ToString("MMMM dd yyyy");
#endif

            BuildSettings.scriptingBackendS = string.Format("{0} scripting backend{1}{2}"
                , this.scriptingBackend
                , !string.IsNullOrEmpty(this.scriptingRuntimeVersion) ? ", " + this.scriptingRuntimeVersion + " runtime" : string.Empty
                , !string.IsNullOrEmpty(this.apiLevel) ? ", " + this.apiLevel + " API" : string.Empty
                );

            BuildSettings.buildTimeS = this.buildTime;
        }
    }
}