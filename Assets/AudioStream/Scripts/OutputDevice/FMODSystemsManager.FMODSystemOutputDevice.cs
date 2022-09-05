// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using System.Collections.Generic;

namespace AudioStream
{
    /// <summary>
    /// Manages FMOD systems creation/release for each system output
    /// System for output 0 is used for outputs enumeration and output changes notifications and is refcounted separately/on top of normal create/release
    /// </summary>
    public static partial class FMODSystemsManager
    {
        // ========================================================================================================================================
        #region create/release FMOD system for default output
        /// <summary>
        /// output 0 is refcounted on top of being present in output <-> system list
        /// </summary>
        static int system_output0_refCount = 0;
        #endregion
        // ========================================================================================================================================
        #region create/release FMOD system per output
        /// <summary>
        /// Output Device <-> FMOD System
        /// </summary>
        static Dictionary<int, FMODSystemOutputDevice> systems4devices = new Dictionary<int, FMODSystemOutputDevice>();
        /// <summary>
        /// Creates new system per output device if needed, otherwise returns already previously created one
        /// </summary>
        /// <param name="forOutputDevice"></param>
        /// <param name="isNotificationSystem"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static FMODSystemOutputDevice FMODSystemForOutputDevice_Create(int forOutputDevice
            , bool isNotificationSystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMODSystemOutputDevice system;

            if (!FMODSystemsManager.systems4devices.TryGetValue(forOutputDevice, out system))
            {
                var devicesconfigs = OutputDevicesConfiguration.Instance.outputDevicesConfiguration;
                FMOD.SPEAKERMODE withSpeakerMode = FMOD.SPEAKERMODE.DEFAULT;
                int withNumOfRawSpeakers = 0;

                if (devicesconfigs.Count > forOutputDevice)
                {
                    withSpeakerMode = devicesconfigs[forOutputDevice].SPEAKERMODE;
                    withNumOfRawSpeakers = devicesconfigs[forOutputDevice].NumOfRawSpeakers;

                    AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Using user override for output device {0}: speaker mode: {1}, no. of speakers: {2}", forOutputDevice, withSpeakerMode, withNumOfRawSpeakers);
                }

                system = new FMODSystemOutputDevice(forOutputDevice
                    , withSpeakerMode
                    , withNumOfRawSpeakers
                    , logLevel
                    , gameObjectName
                    , onError
                    );

                FMODSystemsManager.systems4devices.Add(forOutputDevice, system);

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Created system {0} for output driver {1}, effective FMOD DSP buffer: {2} length, {3} buffers", system.System.handle, forOutputDevice, system.DSPBufferLength, system.DSPBufferCount);
            }
            else
            {
                // TODO: check for configuration changes (output spaker mode, DSP buffers when added.. )
                // - need to release existing system and create new one in that case, too

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Retrieved system {0} for output driver {1}, effective FMOD DSP buffer: {2} length, {3} buffers", system.System.handle, forOutputDevice, system.DSPBufferLength, system.DSPBufferCount);
            }

            if (isNotificationSystem)
                system.SetAsNotificationSystem(logLevel, gameObjectName, onError);

            if (forOutputDevice == 0)
                FMODSystemsManager.system_output0_refCount++;

            return system;
        }
        /// <summary>
        /// Releases passed FMOD system if it's not being used and removes it from internal list
        /// </summary>
        /// <param name="fmodsystem"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public static void FMODSystemForOutputDevice_Release(FMODSystemOutputDevice fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem == null)
            {
                AudioStreamSupport.LOG(LogLevel.WARNING, logLevel, gameObjectName, onError, "Already released ({0})", fmodsystem);
                return;
            }

            var releaseAndRemoveSystem = false;

            if (fmodsystem.OutputDeviceID == 0)
            {
                if (FMODSystemsManager.system_output0_refCount < 1)
                    AudioStreamSupport.LOG(LogLevel.WARNING, logLevel, gameObjectName, onError, "System {0} for output {1} is being overreleased", fmodsystem.System.handle, fmodsystem.OutputDeviceID);

                if (--FMODSystemsManager.system_output0_refCount < 1)
                {
                    releaseAndRemoveSystem = true;
                }
            }
            else if (fmodsystem.SoundsPlaying() < 1)
            {
                releaseAndRemoveSystem = true;
            }

            if (releaseAndRemoveSystem)
            {
                // although 'overreleasing' fmod call is harmless, the system has to be around until last release is requested
                // parameter _can_ be null though
                if (fmodsystem != null)
                {
                    fmodsystem.Release(logLevel, gameObjectName, onError);
                }

                if (!FMODSystemsManager.systems4devices.ContainsValue(fmodsystem))
                    AudioStreamSupport.LOG(LogLevel.ERROR, logLevel, gameObjectName, onError, "System {0} for output device {1} being released was not previously created via FMODSystemsManager", fmodsystem.System.handle, fmodsystem.OutputDeviceID);
                else
                    FMODSystemsManager.systems4devices.Remove(fmodsystem.OutputDeviceID);

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "Released {0} system {1} for output driver {2}", fmodsystem.isNotificationSystem ? "notification" : "", fmodsystem.System.handle, fmodsystem.OutputDeviceID);

                fmodsystem = null;
            }
        }
        #endregion
        // ========================================================================================================================================
        #region enumeration/support
        public struct OUTPUT_DEVICE
        {
            public int id;
            public string name;
            public int samplerate;
            public FMOD.SPEAKERMODE speakermode;
            public int channels;
        }
        /// <summary>
        /// Enumerates available audio outputs in the system and returns their names.
        /// Uses default system for enumeration
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static List<OUTPUT_DEVICE> AvailableOutputs(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            // re/use default output system
            // (make sure to not throw an exception anywhere so the system is always released)
            var fmodsystem = FMODSystemsManager.FMODSystemForOutputDevice_Create(0, false, logLevel, gameObjectName, onError);

            var result = fmodsystem.Update();
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.Update", false);

            List<OUTPUT_DEVICE> availableDrivers = new List<OUTPUT_DEVICE>();

            int numDrivers;
            result = fmodsystem.System.getNumDrivers(out numDrivers);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.System.getNumDrivers", false);

            for (int i = 0; i < numDrivers; ++i)
            {
                int namelen = 255;
                string name;
                System.Guid guid;
                int systemrate;
                FMOD.SPEAKERMODE speakermode;
                int speakermodechannels;

                result = fmodsystem.System.getDriverInfo(i, out name, namelen, out guid, out systemrate, out speakermode, out speakermodechannels);
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.System.getDriverInfo", false);

                if (result != FMOD.RESULT.OK)
                {
                    AudioStreamSupport.LOG(LogLevel.ERROR, logLevel, gameObjectName, onError, "!error output {0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4}"
                        , name
                        , guid
                        , systemrate
                        , speakermode
                        , speakermodechannels
                        );

                    continue;
                }

                availableDrivers.Add(new OUTPUT_DEVICE() { id = i, name = name, samplerate = systemrate, speakermode = speakermode, channels = speakermodechannels });

                AudioStreamSupport.LOG(LogLevel.INFO, logLevel, gameObjectName, onError, "{0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4}"
                    , name
                    , guid
                    , systemrate
                    , speakermode
                    , speakermodechannels
                    );
            }

            // release 
            FMODSystemsManager.FMODSystemForOutputDevice_Release(fmodsystem, logLevel, gameObjectName, onError);

            return availableDrivers;
        }
        #endregion
    }
}