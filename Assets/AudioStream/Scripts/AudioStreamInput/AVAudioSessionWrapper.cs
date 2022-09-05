// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Runtime.InteropServices;

namespace AudioStream
{
    /// <summary>
    /// C# wrapper around iOS AVAudioSession calls
    /// Generates testing input on a dummy interface when not run on a device
    /// </summary>
    public class AVAudioSessionWrapper
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _UpdateAVAudioSession(bool bluetoothRecording, bool defaultToSpeaker);
        [DllImport("__Internal")]
        static extern bool _IsSessionReady();
        [DllImport("__Internal")]
        static extern IntPtr _AvailableInputs(ref int count);
        [DllImport("__Internal")]
        static extern IntPtr _AvailableOutputs(ref int count);
        [DllImport("__Internal")]
        static extern void _SetPreferredInput(int input);
        [DllImport("__Internal")]
        static extern uint _Channels();
        [DllImport("__Internal")]
        static extern double _Samplerate();
        // float[][] is non blittable type for Marshal.Copy so we use ref IntPtr for linearly laid out miltidimensional array instead
        [DllImport("__Internal")]
        static extern void _PcmData(ref IntPtr pcmDataPtr);
        [DllImport("__Internal")]
        static extern uint _PcmDataSamples();
        [DllImport("__Internal")]
        static extern uint _PcmDataBytesPerSample();
        [DllImport("__Internal")]
        static extern void _StartRecording();
        [DllImport("__Internal")]
        static extern void _StopRecording();
        [DllImport("__Internal")]
        static extern bool _IsRecording();
#else
        /*
         * test session when not running on target platform
         */
        static readonly uint test_channels = 2;
        static readonly double test_samplerate = 44100;
        static float[] test_pcmdata = null;
        static readonly uint test_pcmdatasamples = 4192;
        static readonly uint test_pcmdatabytespersample = 4;
        static bool isRecording = false;
        // IntPtr for pcmdata
        static GCHandle test_arrayhandle;
        static IntPtr test_arrayptr;
        /*
         * test signal - just 440 Hz sin wave based on target run above
         */
        static float testSignal_volume = 0.5f;
        static double[] testSignal_phase;
        static double testSignal_increment;
        const double testSignal_pi = Math.PI;
        static int testSignal_frequency = 440;
#endif
        public static void UpdateAVAudioSession(bool bluetoothRecording, bool defaultToSpeaker)
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                _UpdateAVAudioSession(bluetoothRecording, defaultToSpeaker);
            }
#else
            {
                AudioStreamSupport.LOG(LogLevel.DEBUG, LogLevel.DEBUG, "", null, "UpdateAVAudioSession bluetoothRecording: {0}, defaultToSpeaker {1}", bluetoothRecording, defaultToSpeaker);
            }
#endif
        }

        public static bool IsSessionReady()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _IsSessionReady();
            }
#else
            {
                return true;
            }
#endif
        }
        public static string[] AvailableInputs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                int icount = 0;
                var ptrs = _AvailableInputs(ref icount);

                var result = AudioStreamSupport.PtrToStringArray(icount, ptrs);

                return result;
            }
#else
            {
                return new string[] { "Default microphone" };
            }
#endif
        }

        public static string[] AvailableOutputs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                int ocount = 0;
                var ptrs = _AvailableOutputs(ref ocount);

                var result = AudioStreamSupport.PtrToStringArray(ocount, ptrs);

                return result;
            }
#else
            {
                return new string[] { "Default speaker" };
            }
#endif
        }

        public static void SetPreferredInput(int input)
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                _SetPreferredInput(input);
            }
#else
            {
                AudioStreamSupport.LOG(LogLevel.DEBUG, LogLevel.DEBUG, "", null, "SetPreferredInput input: {0}", input);
            }
#endif
        }

        public static uint Channels()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _Channels();
            }
#else
            {
                return test_channels;
            }
#endif
        }

        public static double Samplerate()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _Samplerate();
            }
#else
            {
                return test_samplerate;
            }
#endif
        }

        public static void PcmData(ref IntPtr pcmDataPtr)
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                _PcmData(ref pcmDataPtr);
            }
#else
            {
                // generate some noise for testing

                var channels = Channels();
                var samples = PcmDataSamples();

                if (test_pcmdata == null
                    || test_pcmdata.Length != (channels * samples)
                    )
                {
                    test_pcmdata = new float[channels * samples];
                    testSignal_phase = new double[channels];

                    for (var ch = 0; ch < channels; ++ch)
                        testSignal_phase[ch] = 0;

                    testSignal_increment = testSignal_pi * 2 * testSignal_frequency / Samplerate();

                    test_arrayhandle = GCHandle.Alloc(test_pcmdata, GCHandleType.Pinned);
                    test_arrayptr = test_arrayhandle.AddrOfPinnedObject();
                }

                for (int ch = 0; ch < channels; ch++)
                {
                    for (int n = 0; n < samples; n++)
                    {
                        test_pcmdata[(ch * samples) + n] = testSignal_volume * (float)Math.Sin(testSignal_phase[ch]);

                        testSignal_phase[ch] += testSignal_increment;

                        if (testSignal_phase[ch] > 2 * testSignal_pi)
                            testSignal_phase[ch] -= 2 * testSignal_pi;
                    }
                }

                pcmDataPtr = test_arrayptr;
            }
#endif
        }

        public static uint PcmDataSamples()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _PcmDataSamples();
            }
#else
            {
                return test_pcmdatasamples;
            }
#endif
        }

        public static uint PcmDataBytesPerSample()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _PcmDataBytesPerSample();
            }
#else
            {
                return test_pcmdatabytespersample;
            }
#endif
        }

        public static void StartRecording()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                _StartRecording();
            }
#else
            {
                isRecording = true;
                AudioStreamSupport.LOG(LogLevel.DEBUG, LogLevel.DEBUG, "", null, "StartRecording");
            }
#endif
        }

        public static void StopRecording()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                _StopRecording();
            }
#else
            {
                isRecording = false;

                test_pcmdata = null;
                if (test_arrayhandle.IsAllocated)
                    test_arrayhandle.Free();

                AudioStreamSupport.LOG(LogLevel.DEBUG, LogLevel.DEBUG, "", null, "StopRecording");
            }
#endif
        }

        public static bool IsRecording()
        {
#if UNITY_IOS && !UNITY_EDITOR
            {
                return _IsRecording();
            }
#else
            {
                return isRecording;
            }
#endif
        }
    }
}