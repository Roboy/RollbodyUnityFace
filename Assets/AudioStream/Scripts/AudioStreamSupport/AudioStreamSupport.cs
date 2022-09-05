// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace AudioStream
{
    // ========================================================================================================================================
    #region About
    public static class About
    {
        public static string versionNumber = "2.4.1";
        public static string versionString = "AudioStream v " + versionNumber + " © 2016-2020 Martin Cvengros";
        public static string fmodNotice = ", uses FMOD by Firelight Technologies Pty Ltd";
        public static string buildString = About.UpdateBuildString();
        public static string UpdateBuildString()
        {
            return string.Format("Built {0}, Unity version: {1}{2}, {3} bit, {4}"
            , BuildSettings.buildTimeS
            , Application.unityVersion
            , !string.IsNullOrEmpty(BuildSettings.scriptingBackendS) ? (", " + BuildSettings.scriptingBackendS) : ""
            , System.Runtime.InteropServices.Marshal.SizeOf(IntPtr.Zero) * 8
            , AudioStreamSupport.UnityAudioLatencyDescription()
            );
        }
        public static string defaultOutputProperties = string.Format("System default output samplerate: {0}, application speaker mode: {1} [HW: {2}]", AudioSettings.outputSampleRate, AudioSettings.speakerMode, AudioSettings.driverCapabilities);
        public static string proxyUsed
        {
            get
            {
                var proxyString = AudioStream_ProxyConfiguration.Instance.ProxyString(true);
                return string.IsNullOrEmpty(proxyString) ? null : string.Format("Proxy server to be used: {0}", proxyString);
            }
        }
    }
    #endregion

    // ========================================================================================================================================
    #region Unity events
    [System.Serializable]
    public class EventWithStringParameter : UnityEvent<string> { };
    [System.Serializable]
    public class EventWithStringBoolParameter : UnityEvent<string, bool> { };
    [System.Serializable]
    public class EventWithStringStringParameter : UnityEvent<string, string> { };
    [System.Serializable]
    public class EventWithStringStringObjectParameter : UnityEvent<string, string, object> { };
    [System.Serializable]
    public class EventWithStringAudioClipParameter : UnityEvent<string, AudioClip> { };
    #endregion

    // ========================================================================================================================================
    #region just LogLevel enum
    public enum LogLevel
    {
        ERROR = 0
            , WARNING = 1 << 0
            , INFO = 1 << 1
            , DEBUG = 1 << 2
    }
    #endregion

    // ========================================================================================================================================
    #region Static utilities
    public static class AudioStreamSupport
    {
        // ========================================================================================================================================
        #region Logging
        /// <summary>
        /// Checks FMOD result and either throws an exception with error message, or logs error message
        /// Log requires game object's current log level, name and error event handler
        /// TODO: !thread safe because of event handler
        /// </summary>
        /// <param name="result"></param>
        /// <param name="currentLogLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="customMessage"></param>
        /// <param name="throwOnError"></param>
        public static void ERRCHECK(
            FMOD.RESULT result
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string customMessage
            , bool throwOnError = true
            )
        {
            if (result != FMOD.RESULT.OK)
            {
                var m = string.Format("{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));

                if (throwOnError)
                    throw new System.Exception(m);
                else
                    LOG(LogLevel.ERROR, currentLogLevel, gameObjectName, onError, m);
            }
            else
            {
                LOG(LogLevel.DEBUG, currentLogLevel, gameObjectName, onError, "{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));
            }
        }
        /// <summary>
        /// Checks FMOD result and either throws an exception with error message, or logs error message
        /// Log requires game object's current log level, name and error event handler
        /// </summary>
        /// <param name="result"></param>
        /// <param name="currentLogLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="customMessage"></param>
        /// <param name="throwOnError"></param>
        /// 
        // TODO: iOS error checking
        /*
        public static void ERRCHECK(
            int result
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string customMessage
            , bool throwOnError = true
            )
        {
            if (result != 0)
            {
                var m = string.Format("{0} {1} - {2}", customMessage, result, AudioStreamInput_iOS.GetErrorDescription(result));

                if (throwOnError)
                    throw new System.Exception(m);
                else
                    LOG(LogLevel.ERROR, currentLogLevel, gameObjectName, onError, m);
            }
            else
            {
                LOG(LogLevel.DEBUG, currentLogLevel, gameObjectName, onError, "{0} {1} - {2}", customMessage, result, AudioStreamInput_iOS.GetErrorDescription(result));
            }
        }
        */

        /// <summary>
        /// Logs message based on log level and invokes error handler (for calling from ERRCHECK)
        /// TODO: !thread safe because of event handler
        /// </summary>
        /// <param name="requestedLogLevel"></param>
        /// <param name="currentLogLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void LOG(
            LogLevel requestedLogLevel
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string format
            , params object[] args
            )
        {
            if (requestedLogLevel == LogLevel.ERROR)
            {
                var time = DateTime.Now.ToString("s");
                var msg = string.Format(format, args);

                Debug.LogError(
                    gameObjectName + " [ERROR][" + time + "] " + msg + "\r\n=======================================\r\n"
                    );

                if (onError != null)
                    onError.Invoke(gameObjectName, msg);
            }
            else if (currentLogLevel >= requestedLogLevel)
            {
                var time = DateTime.Now.ToString("s");

                if (requestedLogLevel == LogLevel.WARNING)
                    Debug.LogWarningFormat(
                        gameObjectName + " [WARNING][" + time + "] " + format + "\r\n=======================================\r\n"
                        , args);
                else
                    Debug.LogFormat(
                        gameObjectName + " [" + requestedLogLevel + "][" + time + "] " + format + "\r\n=======================================\r\n"
                        , args);
            }
        }

        public static string TimeStringFromSeconds(double seconds)
        {
            // There are 10,000 ticks in a millisecond:
            var ticks = seconds * 1000 * 10000;
            var span = new TimeSpan((long)ticks);

            return string.Format("{0:D2}h : {1:D2}m : {2:D2}s : {3:D3}ms"
                , span.Hours
                , span.Minutes
                , span.Seconds
                , span.Milliseconds
                );
        }
        #endregion

        // ========================================================================================================================================
        #region audio byte array
        readonly static object _thelock = new object();
        /// <summary>
        /// FMOD stream -> Unity
        /// </summary>
        /// <param name="byteArray"></param>
        /// <param name="byteArray_length"></param>
        /// <param name="resultFloatArray"></param>
        /// <returns></returns>
        public static int ByteArrayToFloatArray(byte[] byteArray, uint byteArray_length, byte bytes_per_value, FMOD.SOUND_FORMAT sound_format, ref float[] resultFloatArray)
        {
            lock (AudioStreamSupport._thelock)
            {
                if (resultFloatArray == null || resultFloatArray.Length != (byteArray_length / bytes_per_value))
                    resultFloatArray = new float[byteArray_length / bytes_per_value];

                int arrIdx = 0;
                for (int i = 0; i < byteArray_length; i += bytes_per_value)
                {
                    var barr = new byte[bytes_per_value];
                    for (int ii = 0; ii < bytes_per_value; ++ii) barr[ii] = byteArray[i + ii];

                    if (sound_format == FMOD.SOUND_FORMAT.PCMFLOAT)
                    {
                        resultFloatArray[arrIdx++] = BitConverter.ToSingle(barr, 0);
                    }
                    else
                    {
                        // inlined former 'BytesToFloat' method
                        // TODO: figure out how to cast this on iOS in IL2CPP - PCM24, & format does not work there
                        // TODO: but widened type does not correctly construct base PCM16 (which we will rather keep)
#if !UNITY_IOS && !UNITY_ANDROID
                        Int64 result = 0;
                        for (int barridx = 0; barridx < barr.Length; ++barridx)
                            result |= ((Int64)barr[barridx] << (8 * barridx));

                        var f = (float)(Int32)result;
#else
                        int result = 0;
                        for (int barridx = 0; barridx < barr.Length; ++barridx)
                            result |= ((int)barr[barridx] << (8 * barridx));

                        var f = (float)(short)result;
#endif
                        switch (sound_format)
                        {
                            case FMOD.SOUND_FORMAT.PCM8:
                                // PCM8 is unsigned:
                                if (f > 127)
                                    f = f - 255f;
                                f = f / (float)127;
                                break;
                            case FMOD.SOUND_FORMAT.PCM16:
                                f = (Int16)f / (float)Int16.MaxValue;
                                break;
                            case FMOD.SOUND_FORMAT.PCM24:
                                if (f > 8388607)
                                    f = f - 16777215;
                                f = f / (float)8388607;
                                break;
                            case FMOD.SOUND_FORMAT.PCM32:
                                f = (Int32)f / (float)Int32.MaxValue;
                                break;
                        }

                        resultFloatArray[arrIdx++] = f;
                    }
                }

                return arrIdx;
            }
        }

        static float BytesToFloat(byte firstByte, byte secondByte)
        {
            return (float)((short)((int)secondByte << 8 | (int)firstByte << 0)) / 32768f;
        }
        /// <summary>
        /// Decoded stream -> Unity
        /// </summary>
        /// <param name="shortArray"></param>
        /// <param name="shortArray_length"></param>
        /// <param name="resultFloatArray"></param>
        /// <returns></returns>
        public static int ShortArrayToFloatArray(short[] shortArray, uint shortArray_length, ref float[] resultFloatArray)
        {
            if (resultFloatArray == null || resultFloatArray.Length != (shortArray_length))
                resultFloatArray = new float[shortArray_length];

            for (int i = 0; i < shortArray_length; ++i)
            {
                var f = (float)(shortArray[i] / 32768f);

                resultFloatArray[i] = f;
            }

            return resultFloatArray.Length;
        }
        /// <summary>
        /// Unity -> byte stream, floats are converted to UInt16 PCM data
        /// TODO: might use Buffer.BlockCopy here ? - not used in time critical components though..
        /// </summary>
        /// <param name="floatArray"></param>
        /// <param name="floatArray_length"></param>
        /// <param name="resultByteArray"></param>
        /// <returns></returns>
        public static int FloatArrayToPCM16yteArray(float[] floatArray, uint floatArray_length, ref byte[] resultByteArray)
        {
            if (resultByteArray == null || resultByteArray.Length != (floatArray_length * sizeof(UInt16)))
                resultByteArray = new byte[floatArray_length * sizeof(UInt16)];

            for (int i = 0; i < floatArray_length; ++i)
            {
                var bArr = FloatToByteArray(floatArray[i] * 32768f);

                resultByteArray[i * 2] = bArr[0];
                resultByteArray[(i * 2) + 1] = bArr[1];
            }

            return resultByteArray.Length;
        }
        static byte[] FloatToByteArray(float _float)
        {
            var result = new byte[2];

            var fa = (UInt16)(_float);
            byte b0 = (byte)(fa >> 8);
            byte b1 = (byte)(fa & 0xFF);

            result[0] = b1;
            result[1] = b0;

            return result;

            // BitConverter preserves endianess, but is slower..
            // return BitConverter.GetBytes(Convert.ToInt16(_float));
        }
        /// <summary>
        /// Unity -> encoder
        /// </summary>
        /// <param name="floatArray"></param>
        /// <param name="floatArray_length"></param>
        /// <param name="resultShortArray"></param>
        /// <returns></returns>
        public static int FloatArrayToShortArray(float[] floatArray, uint floatArray_length, ref short[] resultShortArray)
        {
            if (resultShortArray == null || resultShortArray.Length != floatArray_length)
                resultShortArray = new short[floatArray_length];

            for (int i = 0; i < floatArray_length; ++i)
            {
                resultShortArray[i] = (short)(floatArray[i] * 32768f);
            }

            return resultShortArray.Length;
        }

        /// <summary>
        /// Unity -> (network) stream
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] StringToBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
        /// <summary>
        /// (Network) stream -> Unity
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string BytesToString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }
        #endregion

        // ========================================================================================================================================
        #region Channel count conversions
        /// <summary>
        /// Tries to return no. of output channels Unity is currently using based on AudioSettings.speakerMode (this should match channels in e.g. OnAudioFilterRead callbacks)
        /// If bandwidth of user selected Default Speaker Mode in AudioSettings (AudioSettings.speakerMode) differs from actual HW capabilities (AudioSettings.driverCapabilities), Unity will render audio
        /// with lower/actual bandwidth using AudioSettings.driverCapabilities channels instead in some cases. We report this channel count in this case as well.
        /// </summary>
        /// <returns></returns>
        public static int ChannelsFromUnityDefaultSpeakerMode()
        {
            var speakerMode = AudioSettings.speakerMode;

            // check user selected vs. hw channels/bandwidth
            // it seems they do "stuff" for Mono/Stereo, but channels for all other outputs are incorrect then, except ProLogic since that mixes Stero (!) to 5.1
            // [initial report by @ddf on the forums (thanks, @ddf)
            // problematic case was AudioSettings.speakerMode           == Mode7point1
            // &&                   AudioSettings.driverCapabilities    == Stereo
            // AudioSettings.driverCapabilities was used since that was what OAFR was operating on according to report (pitch was set incorrectly in recording)]
            if (AudioSettings.driverCapabilities != speakerMode)
            {
                if (speakerMode != AudioSpeakerMode.Mono
                    && speakerMode != AudioSpeakerMode.Stereo
                    && speakerMode != AudioSpeakerMode.Prologic)
                {
                    speakerMode = AudioSettings.driverCapabilities;

                    Debug.LogWarningFormat("Output HW driver [{0}] doesn't match currently selected Unity Default Speaker Mode [{1}] - Unity will (probably) use [{2}] in this case. Consider matching your Default Speaker Mode with current actual hardware used for default output"
                        , AudioSettings.driverCapabilities, AudioSettings.speakerMode, AudioSettings.driverCapabilities);
                }
            }

            switch (speakerMode)
            {
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Prologic:
                    return 6;
                case AudioSpeakerMode.Quad:
                    return 4;
#if !UNITY_2019_2_OR_NEWER
                case AudioSpeakerMode.Raw:
                    Debug.LogError("Don't call ChannelsFromUnityDefaultSpeakerMode with Unity 'Default Speaker Mode' set to 'Raw' - provide channel count manually in that case. Returning 2 (Stereo).");
                    return 2;
#endif
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Surround:
                    return 5;
                default:
                    Debug.LogError("Unknown AudioSettings.speakerMode - Returning 2 (Stereo).");
                    return 2;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Platform
        /// <summary>
        /// returns true if the project is running on 64-bit architecture, false if 32-bit 
        /// </summary>
        /// <returns></returns>
        public static bool Is64bitArchitecture()
        {
            int sizeOfPtr = System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr));
            return (sizeOfPtr > 4);
        }
        /// <summary>
        /// Empirically observed Unity audio latency string
        /// </summary>
        /// <returns></returns>
        public static string UnityAudioLatencyDescription()
        {
            // 256 - Best latency
            // 512 - Good latency
            // 1024 - Default in pre-2018.1 versions, Best performance

            string result = "Unity audio: ";
            switch (AudioSettings.GetConfiguration().dspBufferSize)
            {
                case 256:
                    result += "Best latency";
                    break;
                case 512:
                    result += "Good latency";
                    break;
                case 1024:
                    result += "Best performance";
                    break;
                default:
                    result += "Unknown latency";
                    break;
            }
            return result;
        }
        #endregion

        // ========================================================================================================================================
        #region FMOD helpers
        /// <summary>
        /// Gets string from native pointer - uses adapted FMOD StringHelper, which is not public
        /// (At the time - around 1.10.04 (?) - also worked around early exit bug in stringFromNative which is since fixed)
        /// </summary>
        /// <param name="nativePtr">pointer to the string</param>
        /// <param name="nativeLen">bytes the string occupies in memory</param>
        /// <returns></returns>
        public static string StringFromNative(IntPtr nativePtr, out uint bytesRead)
        {
            string result = string.Empty;
            int nativeLength = 0;

            using (StringHelper.ThreadSafeEncoding encoder = StringHelper.GetFreeHelper())
            {
                result = encoder.stringFromNative(nativePtr, out nativeLength);
            }

            bytesRead = (uint)nativeLength;

            return result;
        }
        /// <summary>
        /// Adapted based on StringHelper from fmod.cs since - as of FMOD 1.10.10 - it's not public, and we need bytes count the string occupies, too
        /// </summary>
        static class StringHelper
        {
            public class ThreadSafeEncoding : IDisposable
            {
                System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
                byte[] encodedBuffer = new byte[128];
                char[] decodedBuffer = new char[128];
                bool inUse;

                public bool InUse() { return inUse; }
                public void SetInUse() { inUse = true; }

                private int roundUpPowerTwo(int number)
                {
                    int newNumber = 1;
                    while (newNumber <= number)
                    {
                        newNumber *= 2;
                    }

                    return newNumber;
                }

                public byte[] byteFromStringUTF8(string s)
                {
                    if (s == null)
                    {
                        return null;
                    }

                    int maximumLength = encoding.GetMaxByteCount(s.Length) + 1; // +1 for null terminator
                    if (maximumLength > encodedBuffer.Length)
                    {
                        int encodedLength = encoding.GetByteCount(s) + 1; // +1 for null terminator
                        if (encodedLength > encodedBuffer.Length)
                        {
                            encodedBuffer = new byte[roundUpPowerTwo(encodedLength)];
                        }
                    }

                    int byteCount = encoding.GetBytes(s, 0, s.Length, encodedBuffer, 0);
                    encodedBuffer[byteCount] = 0; // Apply null terminator

                    return encodedBuffer;
                }

                public string stringFromNative(IntPtr nativePtr, out int nativeLen)
                {
                    nativeLen = 0;

                    if (nativePtr == IntPtr.Zero)
                    {
                        return "";
                    }

                    while (System.Runtime.InteropServices.Marshal.ReadByte(nativePtr, nativeLen) != 0)
                    {
                        nativeLen++;
                    }

                    if (nativeLen == 0)
                    {
                        return "";
                    }

                    if (nativeLen > encodedBuffer.Length)
                    {
                        encodedBuffer = new byte[roundUpPowerTwo(nativeLen)];
                    }

                    System.Runtime.InteropServices.Marshal.Copy(nativePtr, encodedBuffer, 0, nativeLen);

                    int maximumLength = encoding.GetMaxCharCount(nativeLen);
                    if (maximumLength > decodedBuffer.Length)
                    {
                        int decodedLength = encoding.GetCharCount(encodedBuffer, 0, nativeLen);
                        if (decodedLength > decodedBuffer.Length)
                        {
                            decodedBuffer = new char[roundUpPowerTwo(decodedLength)];
                        }
                    }

                    int charCount = encoding.GetChars(encodedBuffer, 0, nativeLen, decodedBuffer, 0);

                    return new String(decodedBuffer, 0, charCount);
                }

                public void Dispose()
                {
                    lock (encoders)
                    {
                        inUse = false;
                    }
                }
            }

            static List<ThreadSafeEncoding> encoders = new List<ThreadSafeEncoding>(1);

            public static ThreadSafeEncoding GetFreeHelper()
            {
                lock (encoders)
                {
                    ThreadSafeEncoding helper = null;
                    // Search for not in use helper
                    for (int i = 0; i < encoders.Count; i++)
                    {
                        if (!encoders[i].InUse())
                        {
                            helper = encoders[i];
                            break;
                        }
                    }
                    // Otherwise create another helper
                    if (helper == null)
                    {
                        helper = new ThreadSafeEncoding();
                        encoders.Add(helper);
                    }
                    helper.SetInUse();
                    return helper;
                }
            }
        }
        #endregion

        // ========================================================================================================================================
        #region DL cache
        /// <summary>
        /// Returns SHA512 unique hash of given (url + uniqueCacheId) in temp cache file path
        /// Appends 'extension' as file name extension
        /// </summary>
        /// <param name="fromUrl">Base url/filename</param>
        /// <param name="uniqueCacheId">Optional unique id which will be appended to url for having more than one cached downloads from a single source</param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static string CachedFilePath(string fromUrl, string uniqueCacheId, string extension)
        {
            var fileName = AudioStreamSupport.EscapedBase64Hash(fromUrl + uniqueCacheId);
            return Path.Combine(Application.temporaryCachePath, fileName + extension);
        }
        public static string EscapedBase64Hash(string ofUri)
        {
            var byteArray = ofUri.ToCharArray().Select(s => (byte)s).ToArray<byte>();

            using (var sha = System.Security.Cryptography.SHA512.Create())
            {
                var hash = sha.ComputeHash(byteArray);

                return Uri.EscapeDataString(
                    Convert.ToBase64String(hash)
                    );
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Marshaling helpers
        // copied from Mono P/Invoke page (https://www.mono-project.com/docs/advanced/pinvoke/)
        public static string PtrToString(IntPtr p)
        {
            // TODO: deal with character set issues.  Will PtrToStringAnsi always
            // "Do The Right Thing"?
            if (p == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringAnsi(p);
        }

        public static string[] PtrToStringArray(IntPtr stringArray)
        {
            if (stringArray == IntPtr.Zero)
                return new string[] { };


            int argc = CountStrings(stringArray);
            return PtrToStringArray(argc, stringArray);
        }

        private static int CountStrings(IntPtr stringArray)
        {
            int count = 0;
            while (Marshal.ReadIntPtr(stringArray, count * IntPtr.Size) != IntPtr.Zero)
                ++count;
            return count;
        }

        public static string[] PtrToStringArray(int count, IntPtr stringArray)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "< 0");
            if (stringArray == IntPtr.Zero)
                return new string[count];


            string[] members = new string[count];
            for (int i = 0; i < count; ++i)
            {
                IntPtr s = Marshal.ReadIntPtr(stringArray, i * IntPtr.Size);
                members[i] = PtrToString(s);
            }


            return members;
        }

        public static T[] PtrToStructsArray<T>(IntPtr structArray) where T : struct
        {
            if (structArray == IntPtr.Zero)
                return new T[] { };


            int argc = CountStrings(structArray);
            return PtrToStructsArray<T>(argc, structArray);
        }

        public static T[] PtrToStructsArray<T>(int count, IntPtr structArray) where T : struct
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "< 0");
            if (structArray == IntPtr.Zero)
                return new T[count];


            T[] members = new T[count];
            for (int i = 0; i < count; ++i)
            {
                IntPtr s = Marshal.ReadIntPtr(structArray, i * IntPtr.Size);
                members[i] = Marshal.PtrToStructure<T>(s);
            }

            return members;
        }

        public static int CountStructs<T>(IntPtr structArray) where T:struct
        {
            int count = 0;
            while (Marshal.ReadIntPtr(structArray, count * Marshal.SizeOf<T>()) != IntPtr.Zero)
                ++count;
            return count;
        }
        #endregion

        // ========================================================================================================================================
        #region some math
        public static uint HighestPowerof2LowerThan(uint n)
        {
            uint p = (uint)Mathf.Log(n, 2f);
            var result = (uint)Mathf.Pow(2, p);

            return result * 4;
        }
        public static uint LowestPowerOf2HigherThan(uint n)
        {
            uint p = 1;
            if (n > 0 && (n & (n - 1)) == 0)
                return n;

            while (p < n)
                p <<= 1;

            return p;
        }
        #endregion

#if UNITY_EDITOR
        // ========================================================================================================================================
        #region Demo StreamingAssets Editor support
        /// <summary>
        /// Will be called after Editor reload which is more than enough (e.g. after the asset import)
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod()]
        static void SetupStreamingAssets()
        {
            // check for file existence should be very quick
            var flagFilename = Path.Combine(Path.Combine(Application.streamingAssetsPath, "AudioStream"), "_audiostream_demo_assets_prepared");

            if (!File.Exists(flagFilename))
            {
                AudioStreamSupport.SetupStreamingAssetsIfNeeded();
                using (var f = File.Create(flagFilename)) { f.Close(); }
            }
        }
        /// <summary>
        /// Copies runtime demo assets into application StreamingAssets fdolder if they don't exist there already and the asset location hasn't moved
        /// (which should be the case after initial import; if not, the user who imported elsewhere should be able to fix it anyway)
        /// Might need assets refresh to show up in the Editor
        /// </summary>
        static void SetupStreamingAssetsIfNeeded()
        {
            // get the list of assets in 'AudioStream/StreamingAssets'
            List<string> asStreamingAssets = new List<string>();

            // search in all Assets, package could be imported anywhere..
            // TODO: there will be fun when asset store packages arrive..
            foreach (var s in UnityEditor.AssetDatabase.FindAssets("t:Object", new string[] { "Assets" }))
            {
                // convert object's GUID to asset path
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(s);

                // add the asset path
                if (assetPath.Contains("AudioStream/StreamingAssets/"))
                    asStreamingAssets.Add(assetPath);
            }

            if (asStreamingAssets.Count > 0)
            {
                // list of files in 'StreamingAssets'
                // - directory must exist for the call to succeed..
                var dirname = Path.Combine(Application.streamingAssetsPath, "AudioStream");

                if (!Directory.Exists(dirname))
                    Directory.CreateDirectory(dirname);

                var streamingAssetsContent = Directory.GetFiles(dirname, "*.*");

                foreach (var asStreamingAsset in asStreamingAssets)
                    if (!streamingAssetsContent.Select(s => Path.GetFileName(s)).Contains(Path.GetFileName(asStreamingAsset)))
                    {
                        var src = asStreamingAsset;
                        var dst = Path.Combine(dirname, Path.GetFileName(asStreamingAsset));
                        Debug.LogWarningFormat("One time copy of AudioStream demo asset: {0} into project StreamingAssets: {1}", src, dst);
                        File.Copy(src, dst);
                    }
            }
        }
        #endregion
#endif
    }
    #endregion
}
