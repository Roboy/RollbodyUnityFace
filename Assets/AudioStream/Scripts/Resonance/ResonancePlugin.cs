// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace AudioStream
{
    public class ResonancePlugin
    {
        // ========================================================================================================================================
        #region Editor simulacrum
        LogLevel logLevel = LogLevel.INFO;
        string gameObjectName = "Resonance Plugin";
        #endregion

        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = this.result;
            AudioStreamSupport.ERRCHECK(this.result, this.logLevel, this.gameObjectName, null, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, null, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }
        #endregion

        // ========================================================================================================================================
        #region FMOD
        FMOD.System system;
        FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        #endregion

        // ========================================================================================================================================
        #region FMOD nested plugins
        uint resonancePlugin_handle = 0;

        const int ResonanceListener_paramID_Gain = 0;
        const int ResonanceListener_paramID_RoomProperties = 1;
        public FMOD.DSP ResonanceListener_DSP;


        const int ResonanceSoundfield_paramID_Gain = 0;
        const int ResonanceSoundfield_paramID_3DAttributes = 1;
        public FMOD.DSP ResonanceSoundfield_DSP;

        
        const int ResonanceSource_paramID_Gain = 0;
        const int ResonanceSource_paramID_Spread = 1;
        const int ResonanceSource_paramID_MinDistance = 2;
        const int ResonanceSource_paramID_MaxDistance = 3;
        const int ResonanceSource_paramID_DistanceRolloff = 4;
        const int ResonanceSource_paramID_Occlusion = 5;
        const int ResonanceSource_paramID_Directivity = 6;
        const int ResonanceSource_paramID_DirectivitySharpness = 7;
        const int ResonanceSource_paramID_3DAttributes = 8;
        const int ResonanceSource_paramID_BypassRoom = 9;

        public FMOD.DSP ResonanceSource_DSP;

        [System.Serializable()]
        public enum DistanceRolloff
        {
            LINEAR = 0
                , LOGARITHMIC = 1
                , OFF = 2
        }
        #endregion

        public ResonancePlugin(FMOD.System forSystem, LogLevel _logLevel)
        {
            this.system = forSystem;
            this.logLevel = _logLevel;

            /*
             * Load Resonance plugin
             * On platforms which support it and binary is provided for, load dynamically
             * On iOS/tvOS plugin is statically linked, and enabled via fmodplugins.cpp (which has to be imported from FMOD Unity Integration and called manually from native side)
             */
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // suppose (somehow naively) that plugins are indexed in order they were registered -

                uint handle; // handle of the registered DSP plugin
                /*
                 * check DSP parameters list
                 */
                int numparams; // parameters check for loaded DSP

                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 0, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.ResonanceListener_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");

                result = this.ResonanceListener_DSP.getNumParameters(out numparams);
                ERRCHECK(result, "dsp.getNumParameters");

                for (var p = 0; p < numparams; ++p)
                {
                    FMOD.DSP_PARAMETER_DESC paramdesc;
                    result = this.ResonanceListener_DSP.getParameterInfo(p, out paramdesc);
                    ERRCHECK(result, "dsp.getParameterInfo");

                    string p_name = Encoding.ASCII.GetString(paramdesc.name).TrimEnd('\0');
                    string p_label = Encoding.ASCII.GetString(paramdesc.label).TrimEnd('\0');
                    var p_description = paramdesc.description;

                    LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", 0, p, paramdesc.type, p_name, p_label, p_description);
                }



                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 1, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.ResonanceSoundfield_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");


                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 2, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.ResonanceSource_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");

            }
            else
            {
                string pluginName = string.Empty;
                // load from a particular folder in Editor
                // TODO: hardcoded FMOD in Editor
                // plugins in player are in 'Plugins/arch' in 2019.3.10 | 'Plugins' in 2017 and 2018 / 2.00.08 
                var pluginsPath = Application.isEditor ? Path.Combine(Application.dataPath, "Plugins/FMOD/lib") : Path.Combine(Application.dataPath, "Plugins");
                bool arch64 = AudioStreamSupport.Is64bitArchitecture();

                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "win/x86_64"), "resonanceaudio");
                        else
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "win/x86"), "resonanceaudio");
                        break;

                    case RuntimePlatform.LinuxEditor:
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "linux/x86_64"), "resonanceaudio");
                        else
                            // linux x86 binary in 2.00.03 doesn't seem to be provided though
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "linux/x86"), "resonanceaudio");
                        break;

                    case RuntimePlatform.OSXEditor:
                        pluginName = Path.Combine(Path.Combine(pluginsPath, "mac"), "resonanceaudio.bundle");
                        break;

                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.LinuxPlayer:
#if UNITY_2019_1_OR_NEWER
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86_64"), "resonanceaudio");
                        else
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86"), "resonanceaudio");
#else
                        // original behaviour
                        if (arch64)
                            pluginName = Path.Combine(pluginsPath, "resonanceaudio");
                        else
                            pluginName = Path.Combine(pluginsPath, "resonanceaudio");
#endif
                        break;

                    case RuntimePlatform.OSXPlayer:
                        pluginName = Path.Combine(pluginsPath, "resonanceaudio.bundle");
                        break;

                    case RuntimePlatform.Android:
                        /*
                         * load library with fully qualified name from hinted folder
                         */
                        pluginsPath = Path.Combine(Application.dataPath, "lib");

                        result = system.setPluginPath(pluginsPath);
                        ERRCHECK(result, "system.setPluginPath");

                        pluginName = "libresonanceaudio.so";
                        break;

                    default:
                        throw new NotSupportedException("Platform not supported.");
                }

                LOG(LogLevel.DEBUG, "Loading '{0}'", pluginName);

                result = system.loadPlugin(pluginName, out this.resonancePlugin_handle);
                ERRCHECK(result, string.Format("system.loadPlugin at {0}", pluginName));

                /*
                 * Create DSPs from all nested plugins, test enumerate && info for parameters
                 */
                int numNestedPlugins;
                result = system.getNumNestedPlugins(this.resonancePlugin_handle, out numNestedPlugins);
                ERRCHECK(result, "system.getNumNestedPlugins");

                LOG(LogLevel.DEBUG, "Got {0} nested plugins", numNestedPlugins);

                for (var n = 0; n < numNestedPlugins; ++n)
                {
                    /*
                     * Load nested plugin
                     */
                    uint nestedHandle;
                    result = system.getNestedPlugin(this.resonancePlugin_handle, n, out nestedHandle);
                    ERRCHECK(result, "system.getNestedPlugin");

                    FMOD.PLUGINTYPE pluginType;
                    int namelen = 255;
                    string dspPluginName;
                    uint version;
                    result = system.getPluginInfo(nestedHandle, out pluginType, out dspPluginName, namelen, out version);

                    LOG(LogLevel.DEBUG, "DSP {0} || plugin type: {1} || plugin name: {2} || version: {3}", n, pluginType, dspPluginName, version);

                    /*
                     * Create DSP effect
                     */
                    FMOD.DSP dsp;
                    result = system.createDSPByPlugin(nestedHandle, out dsp);
                    ERRCHECK(result, "system.createDSPByPlugin");

                    /*
                     * dsp.getInfo seems to be unused
                     */

                    /*
                     * check DSP parameters list
                     */
                    int numparams;
                    result = dsp.getNumParameters(out numparams);
                    ERRCHECK(result, "dsp.getNumParameters");

                    for (var p = 0; p < numparams; ++p)
                    {
                        FMOD.DSP_PARAMETER_DESC paramdesc;
                        result = dsp.getParameterInfo(p, out paramdesc);
                        ERRCHECK(result, "dsp.getParameterInfo");

                        string p_name = Encoding.ASCII.GetString(paramdesc.name).TrimEnd('\0');
                        string p_label = Encoding.ASCII.GetString(paramdesc.label).TrimEnd('\0');
                        var p_description = paramdesc.description;

                        LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", n, p, paramdesc.type, p_name, p_label, p_description);
                    }

                    /*
                     * save DSPs
                     */
                    // looks like it's at const int ResonanceListener_nestedPluginID = 0;
                    if (dspPluginName.ToString() == "Resonance Audio Listener")
                        this.ResonanceListener_DSP = dsp;

                    // looks like it's at const int ResonanceSoundfield_nestedPluginID = 1;
                    if (dspPluginName.ToString() == "Resonance Audio Soundfield")
                        this.ResonanceSoundfield_DSP = dsp;

                    // looks like it's at const int ResonanceSource_nestedPluginID = 2;
                    if (dspPluginName.ToString() == "Resonance Audio Source")
                        this.ResonanceSource_DSP = dsp;
                }
            }
        }

        public void Release()
        {
            result = this.ResonanceListener_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.ResonanceSoundfield_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.ResonanceSource_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.ResonanceListener_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            result = this.ResonanceSoundfield_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            result = this.ResonanceSource_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            // this call caused too much issues
            // it was not possible to call it cleanly - with success return code -, and after invoked Unity crashed occasionally.
            // I suspect/hope FMOD cleans everything when releasing system, and not calling it won't cause further issues with unreleased memory in the editor.
            // result = this.system.unloadPlugin(this.resonancePlugin_handle);
            // ERRCHECK(result, "system.unloadPlugin", false);
        }

        // ========================================================================================================================================
        #region ResonanceListener
        public void ResonanceListener_SetGain(float gain)
        {
            result = ResonanceListener_DSP.setParameterFloat(ResonanceListener_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceListener_GetGain()
        {
            float fvalue;
            result = ResonanceListener_DSP.getParameterFloat(ResonanceListener_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceListener_SetRoomProperties()
        {
            // TODO: finish room

            // Set the room properties to a null room, which will effectively disable the room effects.
            result = ResonanceListener_DSP.setParameterData(ResonanceListener_paramID_RoomProperties, IntPtr.Zero.ToBytes(0));
            ERRCHECK(result, "dsp.setParameterData", false);
        }
        #endregion

        // ========================================================================================================================================
        #region ResonanceSoundfield
        public void ResonanceSoundfield_SetGain(float gain)
        {
            result = ResonanceSoundfield_DSP.setParameterFloat(ResonanceSoundfield_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSoundfield_GetGain()
        {
            float fvalue;
            result = ResonanceSoundfield_DSP.getParameterFloat(ResonanceSoundfield_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSoundfield_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up)
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI();

            attributes.numlisteners = 1;

            attributes.relative = new FMOD.ATTRIBUTES_3D[1];
            attributes.relative[0].position = relative_position.ToFMODVector();
            attributes.relative[0].velocity = relative_velocity.ToFMODVector();
            attributes.relative[0].forward = relative_forward.ToFMODVector();
            attributes.relative[0].up = relative_up.ToFMODVector();

            attributes.weight = new float[1];
            attributes.weight[0] = 1f;

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = this.ResonanceSource_DSP.setParameterData(ResonanceSource_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI));

            Marshal.FreeHGlobal(attributes_ptr);
        }
        #endregion

        // ========================================================================================================================================
        #region ResonanceSource
        public void ResonanceSource_SetGain(float gain)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetGain()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetSpread(float spread)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_Spread, spread);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetSpread()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_Spread, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetMinDistance(float mindistance)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_MinDistance, mindistance);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetMinDistance()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_MinDistance, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetMaxDistance(float maxdistance)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_MaxDistance, maxdistance);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetMaxDistance()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_MaxDistance, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDistanceRolloff(DistanceRolloff distanceRolloff)
        {
            result = ResonanceSource_DSP.setParameterInt(ResonanceSource_paramID_DistanceRolloff, (int)distanceRolloff);
            ERRCHECK(result, "dsp.setParameterInt", false);
        }

        public DistanceRolloff ResonanceSource_GetDistanceRolloff()
        {
            int ivalue;
            result = ResonanceSource_DSP.getParameterInt(ResonanceSource_paramID_DistanceRolloff, out ivalue);
            ERRCHECK(result, "dsp.getParameterInt", false);

            return (DistanceRolloff)ivalue;
        }

        public void ResonanceSource_SetOcclusion(float occlusion)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_Occlusion, occlusion);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetOcclusion()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_Occlusion, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDirectivity(float directivity)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_Directivity, directivity);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetDirectivity()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_Directivity, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDirectivitySharpness(float directivitySharpness)
        {
            result = ResonanceSource_DSP.setParameterFloat(ResonanceSource_paramID_DirectivitySharpness, directivitySharpness);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetDirectivitySharpness()
        {
            float fvalue;
            result = ResonanceSource_DSP.getParameterFloat(ResonanceSource_paramID_DirectivitySharpness, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up)
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES();

            attributes.relative.position = relative_position.ToFMODVector();
            attributes.relative.velocity = relative_velocity.ToFMODVector();
            attributes.relative.forward = relative_forward.ToFMODVector();
            attributes.relative.up = relative_up.ToFMODVector();

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = this.ResonanceSource_DSP.setParameterData(ResonanceSource_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES));

            Marshal.FreeHGlobal(attributes_ptr);
        }

        public void ResonanceSource_SetBypassRoom(bool bypassRoom)
        {
            result = ResonanceSource_DSP.setParameterBool(ResonanceSource_paramID_BypassRoom, bypassRoom);
            ERRCHECK(result, "dsp.setParameterBool", false);
        }

        public bool ResonanceSource_GetBypassRoom()
        {
            bool bvalue;
            result = ResonanceSource_DSP.getParameterBool(ResonanceSource_paramID_BypassRoom, out bvalue);
            ERRCHECK(result, "dsp.getParameterBool", false);

            return bvalue;
        }

        #endregion
    }
}