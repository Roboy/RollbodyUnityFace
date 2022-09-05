// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioStream
{
    /// <summary>
    /// System with simple defaults for input devices enumeration
    /// </summary>
    public class FMODSystemInputDevice
    {
        public readonly FMOD.System system;
        public readonly string VersionString;
        /// <summary>
        /// FMOD's sytem handle (contrary to sound handle it seems) is completely unreliable / e.g. clearing it via .clearHandle() has no effect in following check for !null/hasHandle() /
        /// Use this pointer copied after creation as release/functionality guard instead
        /// </summary>
        public System.IntPtr SystemHandle = global::System.IntPtr.Zero;
        FMOD.RESULT result = FMOD.RESULT.OK;

        public FMODSystemInputDevice()
        {
            /*
            Create a System object and initialize.
            */
            uint version = 0;

            result = FMOD.Factory.System_Create(out this.system);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "Factory.System_Create");

            result = system.getVersion(out version);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "system.getVersion");

            /*
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.VersionString = string.Format("{0}.{1}.{2}", System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

#if UNITY_ANDROID && !UNITY_EDITOR
            // For recording to work on Android OpenSL support is needed:
            // https://www.fmod.org/questions/question/is-input-recording-supported-on-android/

            result = system.setOutput(FMOD.OUTPUTTYPE.OPENSL);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "system.setOutput", false);

            if (result != FMOD.RESULT.OK)
            {
                var msg = "OpenSL support needed for recording not available.";

                AudioStreamSupport.LOG(LogLevel.ERROR, LogLevel.ERROR, "FMODSystemInputDevice", null, msg);

                return;
            }
#endif
            /*
            System initialization
            */
            result = system.init(100, FMOD.INITFLAGS.NORMAL, System.IntPtr.Zero);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "system.init");

            this.SystemHandle = this.system.handle;
        }

        // !
        // TODO: move this all into manager partial class to avoid public exposure

        /// <summary>
        /// Close and release for system
        /// </summary>
        public void Release()
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = system.close();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "System.close");

                result = system.release();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "System.release");

                system.clearHandle();
                // Debug.Log(System.handle);

                this.SystemHandle = global::System.IntPtr.Zero;
            }
        }
        /// <summary>
        /// Called continuosly (i.e. Update)
        /// </summary>
        public FMOD.RESULT Update()
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = this.system.update();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystemInputDevice", null, "this.System.update");

                return result;
            }
            else
            {
                return FMOD.RESULT.ERR_INVALID_HANDLE;
            }
        }
    }
}