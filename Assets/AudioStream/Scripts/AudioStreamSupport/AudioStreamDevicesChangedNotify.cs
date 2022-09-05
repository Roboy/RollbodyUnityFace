// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Sets up and automatically notifies Unity Event subscribers about changes of audio input/output devices in the system
    /// Uses a system for default (0) device for FMOD callbacks
    /// </summary>
    public class AudioStreamDevicesChangedNotify : MonoBehaviour
	{
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringStringParameter OnError;
        public EventWithStringParameter OnDevicesChanged;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion
        // ========================================================================================================================================
        #region FMOD
        /// <summary>
        /// System for output 0 which will be also notification system and used for devices enumeration - one per application, refcounted
        /// == outputdevice_system for output 0
        /// </summary>
        FMODSystemOutputDevice notification_system = null;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.ERR_NOTREADY;
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        /// <summary>
        /// handle to this' ptr for usedata in FMOD callbacks
        /// </summary>
        GCHandle gc_thisPtr;

        void Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gc_thisPtr = GCHandle.Alloc(this);

            this.gameObjectName = this.gameObject.name;

            // create/get fmod system for enumerating available output drivers and registering output devices changes notification
            this.notification_system = FMODSystemsManager.FMODSystemForOutputDevice_Create(0
                , true
                , this.logLevel
                , this.gameObjectName
                , this.OnError);

            this.lastError = FMOD.RESULT.OK;

            // add this to be notified
            FMODSystemOutputDevice.AddToNotifiedInstances(GCHandle.ToIntPtr(this.gc_thisPtr));
        }

        [HideInInspector()]
        /// <summary>
        /// Set in callback upon receiving the notification (not user settable so hidden in inspector)
        /// </summary>
        public bool outputDevicesChanged = false;
        /// <summary>
        /// Do a notification system update and check in Update with small timeout
        /// </summary>
        float updateTimeout = 0f;
        void Update()
        {
            if ((this.updateTimeout += Time.deltaTime) >= 0.1f)
            {
                this.updateTimeout = 0f;

                if (this.notification_system != null
                    && this.notification_system.SystemHandle != IntPtr.Zero
                    )
                {
                    result = this.notification_system.Update();
                    ERRCHECK(result, "notification_system.update", false);

                    if (this.outputDevicesChanged)
                    {
                        this.outputDevicesChanged = false;

                        AudioStreamSupport.LOG(LogLevel.INFO, this.logLevel, this.gameObjectName, this.OnError, "Received DEVICELISTCHANGED | DEVICELOST notification");

                        if (this.OnDevicesChanged != null)
                            this.OnDevicesChanged.Invoke(this.gameObjectName);
                    }
                }
            }
        }
        void OnDestroy()
        {
            // release notification system
            FMODSystemOutputDevice.RemoveFromNotifiedInstances(GCHandle.ToIntPtr(this.gc_thisPtr));
            FMODSystemsManager.FMODSystemForOutputDevice_Release(this.notification_system, this.logLevel, this.gameObjectName, this.OnError);

            if (this.gc_thisPtr.IsAllocated)
                this.gc_thisPtr.Free();
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }
        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;

            return FMOD.Error.String(errorCode);
        }
        #endregion
    }
}