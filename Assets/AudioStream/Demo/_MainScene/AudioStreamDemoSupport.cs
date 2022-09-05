// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using UnityEngine;

public static class AudioStreamDemoSupport
{
    /// <summary>
    /// On Android copies a file out of application archive StreamingAssets into external storage directory and returns its new file path
    /// On all other platforms just returns StreamingAssets location directly
    /// </summary>
    /// <param name="filename">file name in StreamingAssets</param>
    /// <param name="newDestination">called with new file path destination once file is copied out</param>
    /// <returns></returns>
    public static IEnumerator GetFilenameFromStreamingAssets(string filename, System.Action<string> newDestination)
    {
        var sourceFilepath = System.IO.Path.Combine(System.IO.Path.Combine(Application.streamingAssetsPath, "AudioStream") , filename);

        if (Application.platform == RuntimePlatform.Android)
        {
            using (AndroidJavaClass jcEnvironment = new AndroidJavaClass("android.os.Environment"))
            {
                using (AndroidJavaObject joExDir = jcEnvironment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory"))
                {
                    var destinationDirectory = joExDir.Call<string>("toString");
                    var destinationPath = System.IO.Path.Combine(destinationDirectory, filename);

                    // 2018_3 has first deprecation warning
#if UNITY_2018_3_OR_NEWER
                    using (var www = UnityEngine.Networking.UnityWebRequest.Get(sourceFilepath))
                    {
                        yield return www.SendWebRequest();

                        if (!string.IsNullOrEmpty(www.error)
                            || www.isNetworkError
                            || www.isHttpError)
                        {
                            Debug.LogErrorFormat("Can't find {0} in StreamingAssets ({1}): {2}", filename, sourceFilepath, www.error);

                            yield break;
                        }

                        while (!www.downloadHandler.isDone)
                            yield return null;

                        Debug.LogFormat("Copying streaming asset, {0}b", www.downloadHandler.data.Length);

                        System.IO.File.WriteAllBytes(destinationPath, www.downloadHandler.data);
                    }
#else
                    using (WWW www = new WWW(sourceFilepath))
                    {
                        yield return www;

                        if (!string.IsNullOrEmpty(www.error))
                        {
                            Debug.LogErrorFormat("Can't find {0} in StreamingAssets ({1}): {2}", filename, sourceFilepath, www.error);

                            yield break;
                        }

                        System.IO.File.WriteAllBytes(destinationPath, www.bytes);
                    }
#endif
                    sourceFilepath = destinationPath;
                }
            }
        }

        newDestination.Invoke(sourceFilepath);
    }
}
