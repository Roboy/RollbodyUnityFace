// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Manages FMOD systems creation/release for playback and decoding
    /// One system is created for non realtime (Unity audio + decoding) usage
    /// A system is created for each realtime (FMOD/direct) playback
    /// </summary>
    public static partial class FMODSystemsManager
    {
        // ========================================================================================================================================
        #region N/RT systems creation/release
        /// <summary>
        /// FMOD system for non realtime decoding for Unity (/AudioClips)
        /// </summary>
        static FMODSystem system_NoSound_NRT = null;
        /// <summary>
        /// Don't overrelease on quit/destroy
        /// </summary>
        static int system_NoSound_NRT_refCount = 0;
        /// <summary>
        /// FMOD system for realtime playback with Unity AudioSources
        /// </summary>
        static FMODSystem system_NoSound_RT = null;
        /// <summary>
        /// Don't overrelease on quit/destroy
        /// </summary>
        static int system_NoSound_RT_refCount = 0;
        /// <summary>
        /// FMOD systems for AudioStreamMinimal playback/stream
        /// Since AudioStreamMinimal can set output driver separately and at any time even on an already playing sound, let each instance to have its own system
        /// Realtime system since it's being used by FMOD itself for playback
        /// </summary>
        static List<FMODSystem> systems_direct_sound = new List<FMODSystem>();
        /// <summary>
        /// Creates FMOD system for non realtime decoding, if needed
        /// </summary>
        /// <param name="withSpeakerMode"></param>
        /// <param name="withNumOfRawSpeakers"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static FMODSystem FMODSystem_NoSound_NRT(FMOD.SPEAKERMODE withSpeakerMode
            , int withNumOfRawSpeakers
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMODSystemsManager.system_NoSound_NRT_refCount++;

            if (FMODSystemsManager.system_NoSound_NRT == null)
            {
                FMODSystemsManager.system_NoSound_NRT = new FMODSystem(withSpeakerMode, withNumOfRawSpeakers, true, false);
                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Created FMOD system for non realtime decoding {0}", FMODSystemsManager.system_NoSound_NRT.System.handle);
            }

            return FMODSystemsManager.system_NoSound_NRT;
        }
        /// <summary>
        /// Manual system release
        /// </summary>
        /// <param name="fmodsystem"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public static void FMODSystem_NoSound_NRT_Release(FMODSystem fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem != FMODSystemsManager.system_NoSound_NRT)
                Debug.LogErrorFormat("System being released was not previously created via FMODSystemsManager");

            if (FMODSystemsManager.system_NoSound_NRT_refCount < 1)
                Debug.LogWarningFormat("System is being overreleased");

            if (--FMODSystemsManager.system_NoSound_NRT_refCount < 1)
            {
                // although 'overreleasing' fmod call is harmless, the system has to be around until last release is requested
                // parameter _can_ be null though
                if (fmodsystem != null)
                {
                    fmodsystem.Release();
                    AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Released system for non realtime decoding {0}", fmodsystem.System.handle);
                }

                fmodsystem = FMODSystemsManager.system_NoSound_NRT = null;
            }
        }
        /// <summary>
        /// Creates FMOD system for Unity AudioSource playback, if needed
        /// </summary>
        /// <param name="withSpeakerMode"></param>
        /// <param name="withNumOfRawSpeakers"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static FMODSystem FMODSystem_NoSound_RT(FMOD.SPEAKERMODE withSpeakerMode
            , int withNumOfRawSpeakers
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMODSystemsManager.system_NoSound_RT_refCount++;

            if (FMODSystemsManager.system_NoSound_RT == null)
            {
                FMODSystemsManager.system_NoSound_RT = new FMODSystem(withSpeakerMode, withNumOfRawSpeakers, true, true);
                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Created FMOD system for Unity AudioSources {0}", FMODSystemsManager.system_NoSound_RT.System.handle);
            }

            return FMODSystemsManager.system_NoSound_RT;
        }
        /// <summary>
        /// Manual system release
        /// </summary>
        /// <param name="fmodsystem"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public static void FMODSystem_NoSound_RT_Release(FMODSystem fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem != FMODSystemsManager.system_NoSound_RT)
                Debug.LogErrorFormat("System being released was not previously created via FMODSystemsManager");

            if (FMODSystemsManager.system_NoSound_RT_refCount < 1)
                Debug.LogWarningFormat("System is being overreleased");

            if (--FMODSystemsManager.system_NoSound_RT_refCount < 1)
            {
                // although 'overreleasing' fmod call is harmless, the system has to be around until last release is requested
                // parameter _can_ be null though
                if (fmodsystem != null)
                {
                    fmodsystem.Release();
                    AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Released system for Unity AudioSources {0}", fmodsystem.System.handle);
                }

                fmodsystem = FMODSystemsManager.system_NoSound_RT = null;
            }
        }
        /// <summary>
        /// Creates FMOD system for direct FMOD playback, just with prevention of accidental double instantiation
        /// Needs to be released manually
        /// </summary>
        /// <param name="currentSystem"></param>
        /// <param name="withSpeakerMode"></param>
        /// <param name="withNumOfRawSpeakers"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static FMODSystem FMODSystem_DirectSound(FMODSystem currentSystem
            , FMOD.SPEAKERMODE withSpeakerMode
            , int withNumOfRawSpeakers
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMODSystem result = currentSystem;

            if (!FMODSystemsManager.systems_direct_sound.Contains(currentSystem))
            {
                result = new FMODSystem(withSpeakerMode, withNumOfRawSpeakers, false, true);
                FMODSystemsManager.systems_direct_sound.Add(result);

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Created FMOD system for direct/minimal FMOD playback {0}", result.System.handle);
            }

            return result;
        }
        /// <summary>
        /// Manual system release
        /// </summary>
        /// <param name="fmodsystem"></param>
        public static void FMODSystem_DirectSound_Release(FMODSystem fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem == null)
                return;

            fmodsystem.Release();

            if (!FMODSystemsManager.systems_direct_sound.Contains(fmodsystem))
                Debug.LogWarningFormat("System being released was not previously created via FMODSystemsManager");
            else
                FMODSystemsManager.systems_direct_sound.Remove(fmodsystem);

            AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Released system for direct/minimal FMOD playback {0}", fmodsystem.System.handle);

            fmodsystem = null;
        }
        #endregion
        // ========================================================================================================================================
        #region FMOD diagnostics - For debugging only - ! *UNSTABLE* with more than one system
        [AOT.MonoPInvokeCallback(typeof(FMOD.DEBUG_CALLBACK))]
        static FMOD.RESULT DEBUG_CALLBACK(FMOD.DEBUG_FLAGS flags, FMOD.StringWrapper file, int line, FMOD.StringWrapper func, FMOD.StringWrapper message)
        {
            var msg = (string)message;
            if (!msg.Contains("FMOD_RESULT = 63")) // missing tags in stream is reported for every frame
                Debug.LogFormat("{0} {1}:{2} {3} {4}", flags, System.IO.Path.GetFileName((string)file), line, (string)func, (string)message);

            return FMOD.RESULT.OK;
        }

        static FMOD.DEBUG_CALLBACK DEBUG_CALLBACK_DELEGATE = null;

        public static void InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS flags)
        {
            if (FMODSystemsManager.DEBUG_CALLBACK_DELEGATE == null)
            {
                FMODSystemsManager.DEBUG_CALLBACK_DELEGATE = new FMOD.DEBUG_CALLBACK(FMODSystemsManager.DEBUG_CALLBACK);

                Debug.LogFormat("new FMODSystemsManager.DEBUG_CALLBACK_DELEGATE {0}", FMODSystemsManager.DEBUG_CALLBACK_DELEGATE);

                var result = FMOD.Debug.Initialize(flags
                    , FMOD.DEBUG_MODE.CALLBACK
                    , FMODSystemsManager.DEBUG_CALLBACK_DELEGATE
                    , null
                    );

                if (result != FMOD.RESULT.OK)
                    Debug.LogErrorFormat("InitializeFMODDiagnostics - {0} {1}", result, FMOD.Error.String(result));
            }
        }
        #endregion
    }
}