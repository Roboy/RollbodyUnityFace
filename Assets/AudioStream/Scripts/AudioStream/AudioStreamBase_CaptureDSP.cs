// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using FMOD;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Abstract base with passthru DSP to capture decoder for components which don't play directly for interop with Unity audio
    /// </summary>
    public abstract partial class AudioStreamBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Capture DSP
        protected DSP captureDSP;
        readonly static object dsp_callback_lock = new object();
        /*
         * DSP callbacks
         * due to IL2CPP not supporting marshaling delegates that point to instance methods to native code we need to circumvent this via static dispatch
         */
        [AOT.MonoPInvokeCallback(typeof(DSP_READCALLBACK))]
        static RESULT DSP_READCALLBACK(ref DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
        {
            lock (AudioStreamBase.dsp_callback_lock)
            {
                //UnityEngine.Debug.LogFormat("DSP_STATE {0} {1} {2} {3} {4} {5} {6} {7}"
                //    , dsp_state.instance
                //    , dsp_state.plugindata
                //    , dsp_state.channelmask
                //    , dsp_state.source_speakermode
                //    , dsp_state.sidechaindata
                //    , dsp_state.sidechainchannels
                //    , dsp_state.functions
                //    , dsp_state.systemobject
                //    );
                // DSP_STATE 2075161808 0 0 0 0 0 2078285328 1

                // get instance from user data (class pointer)
                var functions = (DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(DSP_STATE_FUNCTIONS));

                IntPtr userdata;
                var result = functions.getuserdata(ref dsp_state, out userdata);
                if (result == RESULT.OK)
                {
                    GCHandle classHandle = GCHandle.FromIntPtr(userdata);
                    AudioStreamBase audioStream = classHandle.Target as AudioStreamBase;

                    var farr = new float[length * inchannels];
                    Marshal.Copy(inbuffer, farr, 0, (int)length * inchannels);

                    audioStream.decoderAudioQueue.Write(farr);
                    // UnityEngine.Debug.LogFormat("DSP wrote {0}, size: {1}", farr.Length, audioStream.decoderAudioQueue.Available());
                }

                outchannels = inchannels;

                return RESULT.OK;
            }
        }
        DSP_READCALLBACK dsp_ReadCallback;

        [AOT.MonoPInvokeCallback(typeof(DSP_CREATECALLBACK))]
        static RESULT DSP_CREATECALLBACK(ref DSP_STATE dsp_state)
        {
            return RESULT.OK;
        }
        DSP_CREATECALLBACK dsp_CreateCallback;

        [AOT.MonoPInvokeCallback(typeof(DSP_RELEASECALLBACK))]
        static RESULT DSP_RELEASECALLBACK(ref DSP_STATE dsp_state)
        {
            return RESULT.OK;
        }
        DSP_RELEASECALLBACK dsp_ReleaseCallback;

        [AOT.MonoPInvokeCallback(typeof(DSP_GETPARAM_DATA_CALLBACK))]
        static RESULT DSP_GETPARAM_DATA_CALLBACK(ref DSP_STATE dsp_state, int index, ref IntPtr data, ref uint length, IntPtr valuestr)
        {
            return RESULT.OK;
        }
        DSP_GETPARAM_DATA_CALLBACK dsp_GetParamDataCallback;

        #endregion

        // ========================================================================================================================================
        #region FMOD <-> Unity
        /// <summary>
        /// Incoming decoder data <-> PCM callback exchange
        /// </summary>
        protected ThreadSafeListFloat decoderAudioQueue = null;
        #endregion
    }
}