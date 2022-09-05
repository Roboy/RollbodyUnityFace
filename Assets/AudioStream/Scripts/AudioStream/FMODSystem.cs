// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// FMOD system created for streaming/playback usage
    /// </summary>
    public class FMODSystem
    {
        // ========================================================================================================================================
        #region FMOD system
        public readonly FMOD.System System;
        public readonly string VersionString;
        public readonly uint DSPBufferLength;
        public readonly int DSPBufferCount;
        /// <summary>
        /// FMOD's sytem handle (contrary to sound handle it seems) is completely unreliable / e.g. clearing it via .clearHandle() has no effect in following check for !null/hasHandle() /
        /// Use this pointer copied after creation as release/functionality guard instead
        /// </summary>
        public System.IntPtr SystemHandle = global::System.IntPtr.Zero;

        FMOD.RESULT result = FMOD.RESULT.OK;
        /// <summary>
        /// Creates default FMOD system
        /// </summary>
        /// <param name="withSpeakerMode"></param>
        /// <param name="withNumOfRawSpeakers"></param>
        /// <param name="realtime">Realtime for FMOD playback/default mixer update, non realtime for Unity/audio callbacks driven mixer update</param>
        public FMODSystem(FMOD.SPEAKERMODE withSpeakerMode
            , int withNumOfRawSpeakers
            , bool nosound
            , bool realtime
            )
        {
            this.System = default(FMOD.System);
            this.VersionString = string.Empty;
            this.DSPBufferLength = 0;
            this.DSPBufferCount = 0;

            /*
             * create component sound system (tm)
             */
            uint version = 0;

            result = FMOD.Factory.System_Create(out this.System);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "FMOD.Factory.System_Create");

            result = this.System.getVersion(out version);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "this.System.getVersion");

            if (version < FMOD.VERSION.number)
                AudioStreamSupport.ERRCHECK(FMOD.RESULT.ERR_HEADER_MISMATCH, LogLevel.ERROR, "FMODSystem", null, "version < FMOD.VERSION.number");

            /*
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = global::System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.VersionString = string.Format("{0}.{1}.{2}", global::System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

            /*
             * Match initial internal FMOD samplerate with Unity - should be ~ 48000 on desktop; we change it on the sound only when stream requests it.
             */
            result = this.System.setSoftwareFormat(AudioSettings.outputSampleRate, withSpeakerMode, withNumOfRawSpeakers);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "system.setSoftwareFormat");

            // setOutput must be be4 init on iOS ...
            // for ASIO set FMOD.OUTPUTTYPE.ASIO here too

            // realtime are direct output systems - 
            // otherwise the update/mix is driven by Unity update loop to feed PCM callback from capture DSP
            var outputtype = FMOD.OUTPUTTYPE.AUTODETECT;

            var flags = FMOD.INITFLAGS.NORMAL;
            // we could afford to go thread unsafe if we called from unity only, but decoder update is called from NRT memory/download threads
            // var flags = FMOD.INITFLAGS.THREAD_UNSAFE;

            // without STREAM_FROM_UPDATE FMOD had problems keeping getting the data from filesystem callback in consistent manner
            // :- its own timer caused hiccups and read skips without it
            // TODO: probably too much update calls and it couldn't keep up each time /?. regardless this seems to help as long as framerate is sufficient 0 will have to probably decide this automatically in the future/for mobiles
            flags |= FMOD.INITFLAGS.STREAM_FROM_UPDATE;

            if (nosound)
            {
                if (!realtime)
                    outputtype = FMOD.OUTPUTTYPE.NOSOUND_NRT;
                else
                    outputtype = FMOD.OUTPUTTYPE.NOSOUND;
            }

            if (!realtime)
            {
                flags |= FMOD.INITFLAGS.MIX_FROM_UPDATE;

                // DSP buffers setup directly affects processing speed: increase it as much as possible
                // 8k bufferlength is **EXTREME** ; ( yields in 16k DSP callback buffer size (most of the time anyway))
                // TODO: do something with it to be user settable ?

                result = this.System.setDSPBufferSize(4096, 8);
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "system.setDSPBufferSize");
            }

            result = this.System.setOutput(outputtype);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "System.setOutput");

            // https://www.fmod.com/docs/api/content/generated/FMOD_System_Init.html
            // Currently the maximum channel limit is 4093.
            result = this.System.init(4093, flags, global::System.IntPtr.Zero);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "system.init");

            result = this.System.getDSPBufferSize(out this.DSPBufferLength, out this.DSPBufferCount);
            AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "this.System.getDSPBufferSize");

            this.SystemHandle = this.System.handle;
        }
        /// <summary>
        /// Close and release for system
        /// </summary>
        public void Release()
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = System.close();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "System.close");

                result = System.release();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "System.release");

                System.clearHandle();
                // Debug.Log(System.handle);

                this.SystemHandle = global::System.IntPtr.Zero;
            }
        }
        /// <summary>
        /// Call continuosly (i.e. typically from Unity Update, or OnAudioFilterRead)
        /// </summary>
        public FMOD.RESULT Update()
        {
            if (this.SystemHandle != global::System.IntPtr.Zero)
            {
                result = this.System.update();
                AudioStreamSupport.ERRCHECK(result, LogLevel.ERROR, "FMODSystem", null, "this.System.update");

                return result;
            }
            else
            {
                return FMOD.RESULT.ERR_INVALID_HANDLE;
            }
        }

        #endregion
    }
}