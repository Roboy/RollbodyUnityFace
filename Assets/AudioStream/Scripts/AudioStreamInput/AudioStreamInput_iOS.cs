// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using Concentus.Common;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Conceptually the same as AudioStreamInput2D, only with FMOD completely replaced by calls to AVAudioSession on iOS
    /// Just testing signal via the native wrapper is generated when not running on actual target platform
    /// </summary>
    public class AudioStreamInput_iOS : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        // "No. of audio channels provided by selected recording device.
        [HideInInspector]
        public int recChannels = 0;
        [HideInInspector]
        public int recRate = 0;

        [Header("[Source]")]
        [Tooltip("Audio input driver ID")]
        public int recordDeviceId = 0;

        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("When checked the recording will start automatically on Start with parameters set in Inspector. Otherwise StartCoroutine(Record()) of this component.")]
        public bool recordOnStart = true;

        [Tooltip("Input gain. Default 1\r\nBoosting this artificially to high value can help with faint signals for e.g. further reactive processing (although will probably distort audible signal)")]
        [Range(0f, 5f)]
        public float gain = 1f;
        /// <summary>
        /// Leaving the parameter here since it can be passed to session, but we always use all possible devices by default for now
        /// </summary>
        [Tooltip("Include bluetooth recording devices\r\n - this has to be explicitely requested on iOS for Bluetooth devices such as AirPods to be accessible as recording input (Bluetooth output is always requested automatically by the plugin)")]
        readonly bool bluetoothRecording = true;
        /// <summary>
        /// Same as above - we'll leave it here for now, and the session should respect Unity player 'Override speaker' setting
        /// </summary>
        [Tooltip("Override speaker for recording ")]
        readonly bool defaultToSpeaker = false;
        // TODO: add request speaker default for session

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRecordingStarted;
        public EventWithStringBoolParameter OnRecordingPaused;
        public EventWithStringParameter OnRecordingStopped;
        public EventWithStringStringParameter OnError;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion
        // ========================================================================================================================================
        #region Init
        /// <summary>
        /// Component startup sync
        /// Also AVAudioSession needs some time to enumerate all present recording devices - we need to wait for it. Check this flag when using from scripting.
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        /// <summary>
        /// cached size
        /// </summary>
        protected int floatSize = sizeof(float);

        protected virtual IEnumerator Start()
        {
            this.gameObjectName = this.gameObject.name;

            // setup the AudioSource if it's being used
            var audiosrc = this.GetComponent<AudioSource>();
            if (audiosrc)
            {
                audiosrc.playOnAwake = false;
                audiosrc.Stop();
                audiosrc.clip = null;
            }

            // always init session to populate (input) ports..
            this.UpdateAudioSession();

            while (!AVAudioSessionWrapper.IsSessionReady())
                yield return null;

            this.ready = true;

            if (this.recordOnStart)
                StartCoroutine(this.Record());
        }

        #endregion
        // ========================================================================================================================================
        #region Recording
        [Header("[Runtime]")]
        [Tooltip("Set during recording.")]
        public bool isRecording = false;

        public IEnumerator Record()
        {
            if (this.isRecording)
            {
                LOG(LogLevel.WARNING, "Already recording.");
                yield break;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.ERROR, "Will not start on disabled GameObject.");
                yield break;
            }

            this.isRecording = false;

            this.Stop_Internal(); // try to clean partially started recording / Start initialized system

            while (!AVAudioSessionWrapper.IsSessionReady())
                yield return null;

            /*
             * clear previous run if any
             */

            // clear previously retrieved recording data
            this.outputBuffer.Clear();

            /*
             * set preferref input
             */
            AVAudioSessionWrapper.SetPreferredInput(this.recordDeviceId);

            /*
             * record and wait for device
             */
            AVAudioSessionWrapper.StartRecording();

            /*
             * wait for device
             */
            AVAudioSessionWrapper.PcmData(ref this.pcm_ptr);
            this.pcm_samples = AVAudioSessionWrapper.PcmDataSamples();
            this.recChannels = (int)AVAudioSessionWrapper.Channels();
            this.pcm_bytesPerSample = AVAudioSessionWrapper.PcmDataBytesPerSample();

            while (
                this.recChannels < 1
                || this.pcm_ptr == IntPtr.Zero
                || this.pcm_samples < 1
                || this.pcm_bytesPerSample < 1
                )
            {
                AVAudioSessionWrapper.PcmData(ref this.pcm_ptr);
                this.pcm_samples = AVAudioSessionWrapper.PcmDataSamples();
                this.recChannels = (int)AVAudioSessionWrapper.Channels();
                this.pcm_bytesPerSample = AVAudioSessionWrapper.PcmDataBytesPerSample();

                LOG(LogLevel.INFO, "Waiting for session - pcm ptr : {0}, samples : {1}, channels: {2}, bytesPerSample: {3}"
                    , this.pcm_ptr, this.pcm_samples, this.recChannels, this.pcm_bytesPerSample);

                yield return null;
            }

            this.recRate = (int)AVAudioSessionWrapper.Samplerate();

            LOG(LogLevel.INFO, "Opened device, channels: {0}, rate: {1}", this.recChannels, this.recRate);

            this.RecordingStarted();

            this.isRecording = true;

            if (this.OnRecordingStarted != null)
                this.OnRecordingStarted.Invoke(this.gameObjectName);

            while (this.isRecording)
            {
                this.RecordingUpdate();
                yield return null;
            }
        }

        // TODO: implement RMS and other potentially useful stats per input channel
        // TODO: add meters to demo scenes separately for input...
        #endregion
        // ========================================================================================================================================
        #region Shutdown
        public void Stop()
        {
            LOG(LogLevel.INFO, "Stopping..");

            this.StopAllCoroutines();

            this.RecordingStopped();

            this.Stop_Internal();

            if (this.OnRecordingStopped != null)
                this.OnRecordingStopped.Invoke(this.gameObjectName);
        }

        /// <summary>
        /// Stop and try to release sound resources
        /// </summary>
        void Stop_Internal()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                asource.Stop();
                Destroy(asource.clip);
                asource.clip = null;
            }

            this.isRecording = false;

            /*
                Shut down sound
            */
            AVAudioSessionWrapper.StopRecording();
        }

        protected virtual void OnDestroy()
        {
            this.Stop();
        }
        #endregion
        // ========================================================================================================================================
        #region Support
        protected void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }
        #endregion
        // ========================================================================================================================================
        #region User support
        /// <summary>
        /// Enumerates available audio inputs in the system and returns their names.
        /// </summary>
        /// <returns></returns>
        public string[] AvailableInputs()
        {
            if (!this.ready)
                throw new System.NotSupportedException(string.Format("Make sure you wait until this component ({0}) is ready before trying to enumerate inputs.", this.gameObjectName));

            var availableDriversNames = AVAudioSessionWrapper.AvailableInputs();

            return availableDriversNames;
        }
        /// <summary>
        /// Split session update and record call
        /// </summary>
        public void UpdateAudioSession()
        {
            AVAudioSessionWrapper.UpdateAVAudioSession(this.bluetoothRecording, this.defaultToSpeaker);
        }
        #endregion
        // ========================================================================================================================================
        #region Recording
        [Header("[Advanced - AudioStreamInput2D]")]
        [Tooltip("Use Unity's builtin resampling (by setting the pitch) and input/output channels mapping.\r\nNote: if input and output samplerates differ significantly this might lead to drifting over time (resulting signal will be delayed)\r\nIn that case it's possible to use Speex resampler directly (by setting useUnityToResampleAndMapChannels to false), and also provide custom mix matrix if needed in that case (see 'SetCustomMixMatrix' method).\r\n\r\nA mix matrix is computed automatically if not specified, but it might be not enough for more advanced setups.\r\nPlease be also aware that custom Speex resampler supports only 1 and 2 channels (Mono and Stereo signals) and performs worse then default quality wise (please see README for details).")]
        // TODO: readd speex resampling in the future once its performance is resolved
        // public
        bool useUnityToResampleAndMapChannels = true;
        /// <summary>
        /// Resampler
        /// </summary>
        SpeexResampler speexResampler = null;
        /// <summary>
        /// Unity output samplerate
        /// </summary>
        int outputSampleRate;
        /// <summary>
        /// Unity no. of output channles based on user selected speaker mode
        /// </summary>
        int outputChannels;
        /// <summary>
        /// Input/output samplerates ratio used for size of the resulting resample buffer
        /// </summary>
        float ratesRatio;
        /// <summary>
        /// Automatic mix matrix
        /// </summary>
        float[,] mixMatrix;
        /// <summary>
        /// User provided mix matrix
        /// </summary>
        float[,] customMixMatrix = null;
        /// <summary>
        /// Provide custom mix matrix between inputs and outputs - first dimension is inputs, the second one is outputs
        /// Actual signal's respective channel value is multiplied by provided float value according to the mapping
        /// Example of possible mappings: https://en.wikipedia.org/wiki/Matrix_decoder
        /// Needs to be called before starting the recording
        /// </summary>
        /// <param name="_customMixMatrix"></param>
        public void SetCustomMixMatrix(float[,] _customMixMatrix)
        {
            if (_customMixMatrix.Rank != 2)
                throw new System.NotSupportedException("Custom mix matrix providing mapping between inputs and outputs must be of dimension 2");

            this.customMixMatrix = _customMixMatrix;
        }
        /// <summary>
        /// Returns mix matrix currently being used
        /// </summary>
        /// <returns></returns>
        public float[,] GetMixMatrix()
        {
            return this.mixMatrix;
        }
        /// <summary>
        /// If AudioSource is available on this GameObject, simulate resampling to output sample rate by setting the pitch (since we have no AudioClip created)
        /// , or resample using SpeexResampler - in which case we also need to map channels based on potential user setting
        /// </summary>
        void RecordingStarted()
        {
            this.outputSampleRate = AudioSettings.outputSampleRate;
            this.outputChannels = AudioStreamSupport.ChannelsFromUnityDefaultSpeakerMode();

            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                if (this.useUnityToResampleAndMapChannels)
                {
                    var pitch_nominator = (float)(recRate * recChannels);
                    var pitch_denominator = (float)(this.outputSampleRate * this.outputChannels);
                    asource.pitch = pitch_nominator / pitch_denominator;

                    LOG(LogLevel.DEBUG, "Pitch based on current settings: {0}, from: {1} / {2}, (recRate * recChannels) {3} * {4} / (output samplerate * output channels) {5} * {6}, speaker mode: {7}"
                        , asource.pitch
                        , pitch_nominator
                        , pitch_denominator
                        , recRate
                        , recChannels
                        , this.outputSampleRate
                        , this.outputChannels
                        , AudioSettings.speakerMode
                        );
                }
                else
                {
                    // check if custom mix matrix is present
                    if (this.customMixMatrix != null)
                    {
                        if (this.customMixMatrix.GetLength(0) != this.recChannels)
                            throw new System.NotSupportedException(string.Format("Custom mix matrix's input channels don't match selected input with {0} channels", this.recChannels));

                        if (this.customMixMatrix.GetLength(1) != this.outputChannels)
                            throw new System.NotSupportedException(string.Format("Custom mix matrix's output channels don't match selected output with {0} channels", this.outputChannels));

                        this.mixMatrix = this.customMixMatrix;
                    }
                    else
                    {
                        // setup automatic mix matrix with which tries to distribute evenly inputs to outputs

                        this.mixMatrix = new float[this.recChannels, this.outputChannels];

                        var in_stride = this.recChannels / this.outputChannels;
                        in_stride = Mathf.Clamp(in_stride, 1, in_stride);

                        var out_stride = this.outputChannels / this.recChannels;
                        out_stride = Mathf.Clamp(out_stride, 1, out_stride);

                        // 1st pass 
                        for (var in_s = 0; in_s < this.recChannels; in_s += in_stride)
                        {
                            for (var in_ch = in_s; in_ch < in_s + in_stride; ++in_ch)
                            {
                                for (var out_ch = (in_s * out_stride); out_ch < (in_s * out_stride) + out_stride; ++out_ch)
                                {
                                    if (in_ch < this.recChannels && out_ch < this.outputChannels)
                                        this.mixMatrix[in_ch, out_ch] = 1f;
                                }
                            }
                        }

                        // 2nd inversed pass
                        for (var out_s = 0; out_s < this.outputChannels; out_s += out_stride)
                        {
                            for (var out_ch = out_s; out_ch < out_s + out_stride; ++out_ch)
                            {
                                for (var in_ch = (out_s * in_stride); in_ch < (out_s * in_stride) + in_stride; ++in_ch)
                                {
                                    if (in_ch < this.recChannels && out_ch < this.outputChannels)
                                        this.mixMatrix[in_ch, out_ch] = 1f;
                                }
                            }
                        }
                    }

                    // log the matrix
                    var matrixAsString = string.Empty;
                    for (var row = 0; row < this.recChannels; ++row)
                    {
                        for (var column = 0; column < this.outputChannels; ++column)
                            matrixAsString += this.mixMatrix[row, column] + " ";

                        matrixAsString += "\r\n";
                    }

                    LOG(LogLevel.INFO, "{0}Mix matrix (Inputs <-> Outputs mapping):\r\n{1}", this.customMixMatrix != null ? "Custom " : string.Empty, matrixAsString);

                    this.ratesRatio = (float)this.outputSampleRate / (float)this.recRate;

                    // (speex uses the same channels # for input and output)
                    this.speexResampler = new SpeexResampler(this.recChannels, this.recRate, this.outputSampleRate, 10);

                    // re/initialize resulting resampled array
                    this.inputResampled = null;

                    asource.pitch = 1f; // !
                }

                asource.Play();
            }
        }
        /// <summary>
        /// Nothing to do since data is retrieved via OnAudioFilterRead
        /// </summary>
        void RecordingUpdate()
        {
        }
        /// <summary>
        /// Resampler output
        /// </summary>
        float[] inputResampled;
        /// <summary>
        /// Resampler output for channel processing
        /// </summary>
        float[] inputResampledF;
        /// <summary>
        /// mix of inputs based on custom mix matrix or when input != output channels
        /// </summary>
        float[] inputMix = null;
        /// <summary>
        /// Final OAFR signal
        /// </summary>
        BasicBuffer<float> oafrOutputBuffer = new BasicBuffer<float>(10000);

        public IntPtr pcm_ptr = IntPtr.Zero;
        public uint pcm_samples;
        public uint pcm_bytesPerSample;
        /// <summary>
        /// Retrieves float signal from native tap and adds it to output buffer after interleaving it
        /// </summary>
        protected void UpdateRecordBuffer()
        {
            // TODO: use Span later to map buffer directly

            AVAudioSessionWrapper.PcmData(ref this.pcm_ptr);

            // copy out native buffer if it was updated only
            if (this.pcm_ptr != IntPtr.Zero)
            {
                this.pcm_samples = AVAudioSessionWrapper.PcmDataSamples();
                this.recChannels = (int)AVAudioSessionWrapper.Channels();
                this.pcm_bytesPerSample = AVAudioSessionWrapper.PcmDataBytesPerSample();

                // marshal copy buffer
                float[] sound_in = new float[this.recChannels * pcm_samples];
                Marshal.Copy(this.pcm_ptr, sound_in, 0, sound_in.Length);

                // interleave channels
                // TODO: get info from the native side about non/interleaved format
                var sound_out = new float[this.recChannels * pcm_samples];

                for (var channel = 0; channel < this.recChannels; ++channel)
                {
                    for (var sample = 0; sample < pcm_samples; ++sample)
                    {
                        sound_out[(sample * this.recChannels) + channel] = sound_in[(channel * pcm_samples) + sample];
                    }
                }

                this.AddToOutputBuffer(sound_out);
            }
        }
        /// <summary>
        /// Exchange buffer between native and Unity
        /// this needs capacity at contruction but will be resized later if needed
        /// </summary>
        BasicBuffer<float> outputBuffer = new BasicBuffer<float>(10000);
        /// <summary>
        /// Stores audio retrieved from native via UpdateRecordBuffer call
        /// </summary>
        /// <param name="arr"></param>
        void AddToOutputBuffer(float[] arr)
        {
            // check if there's still enough space in noncircular buffer
            // (since circular buffer does not seem to work / )

            if (this.outputBuffer.Available() + arr.Length > this.outputBuffer.Capacity())
            {
                var newCap = this.outputBuffer.Capacity() * 2;

                LOG(LogLevel.INFO, "Resizing output buffer from: {0} to {1}", this.outputBuffer.Capacity(), newCap);

                // preserve existing data
                BasicBuffer<float> newBuffer = new BasicBuffer<float>(newCap);
                newBuffer.Write(this.outputBuffer.Read(this.outputBuffer.Available()));

                this.outputBuffer = newBuffer;
            }

            outputBuffer.Write(arr);
        }
        /// <summary>
        /// Retrieves recorded data for Unity callbacks
        /// </summary>
        /// <param name="_len"></param>
        /// <returns></returns>
        protected float[] GetAudioOutputBuffer(uint _len)
        {
            // adjust requested byte size
            int len = (int)_len;

            // adjust to what's available
            len = Mathf.Min(len, this.outputBuffer.Available());

            // read available bytes
            var oafrDataArr = this.outputBuffer.Read(len);

            return oafrDataArr;
        }
        // 
        // TODO: refactor this to use here and in AudioStreamInput2D
        //
        /// <summary>
        /// Retrieve recording data, and provide them for output.
        /// - Data can be filtered here
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channels"></param>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!this.isRecording)
                return;

            // keep record update loop running even if paused
            this.UpdateRecordBuffer();

            // always drain incoming buffer
            // drain 1 : 1 to requested output consistently for best latency
            var inputDrainSync = this.recChannels > this.outputChannels ? this.recChannels / (float)this.outputChannels : 1f;
            var inputSignal = this.GetAudioOutputBuffer((uint)data.Length * (uint)inputDrainSync);

            // TODO: have a pause
            // if paused don't process it
            // if (this.isPaused)
            //  return;

            var inputSignal_length = inputSignal.Length;

            if (inputSignal_length > 0)
            {
                // TODO: have a !resample input
                // if resampling is not needed just copy original signal out 
                //if (!this.resampleInput)
                //{
                //    for (int i = 0; i < Mathf.Min(data.Length, inputSignal_length); ++i) data[i] += (inputSignal[i] * this.gain);
                //    return;
                //}
                if (this.useUnityToResampleAndMapChannels)
                {
                    // just fill the output buffer with input signal and leave everything to Unity
                    for (int i = 0; i < Mathf.Min(data.Length, inputSignal_length); ++i) data[i] += (inputSignal[i] * this.gain);
                }
                else
                {
                    // prepare mix for output first
                    // (speex needs the same # of inputs and outputs, or custom mix matrix was set)

                    // map input channels to output channels
                    // - iterate in input channels chunks and find out how to map them to output based on mix matrix

                    if (this.inputMix == null
                        || this.inputMix.Length != (inputSignal.Length / this.recChannels) * this.outputChannels
                        )
                        this.inputMix = new float[(inputSignal.Length / this.recChannels) * this.outputChannels];

                    float[] inputSample = new float[this.recChannels];

                    for (var i = 0; i < inputSignal.Length; i += this.recChannels)
                    {
                        System.Array.Copy(inputSignal, i, inputSample, 0, this.recChannels);

                        var mix = new float[this.outputChannels];

                        // iterate over input channels, output channels and add input based on mix matrix
                        for (var in_ch = 0; in_ch < inputSample.Length; ++in_ch)
                            for (var out_ch = 0; out_ch < this.outputChannels; ++out_ch)
                                mix[out_ch] += (inputSample[in_ch] * this.mixMatrix[in_ch, out_ch]);

                        System.Array.Copy(mix, 0, this.inputMix, (i / this.recChannels) * this.outputChannels, this.outputChannels);
                    }

                    // if input and output sample rates are the same, there's nothing to do
                    if (this.recRate != this.outputSampleRate)
                    {
                        // resample original signal preserving original input channels

                        var resampledlength = Mathf.CeilToInt((float)this.inputMix.Length * ratesRatio) + this.outputChannels; // + one off error

                        if (this.inputResampled == null || this.inputResampled.Length != resampledlength)
                            this.inputResampled = new float[resampledlength];

                        int in_len = this.inputMix.Length / this.outputChannels;
                        int out_len = resampledlength / this.outputChannels;

                        // convert to resampler domain
                        for (int i = 0; i < this.inputMix.Length; ++i)
                            this.inputMix[i] *= (float)short.MaxValue;

                        this.speexResampler.ProcessInterleaved(this.inputMix, 0, ref in_len, this.inputResampled, 0, ref out_len);

                        // process only actually produced output
                        var rlength = out_len * this.outputChannels;
                        if (this.inputResampledF == null
                            || this.inputResampledF.Length != rlength)
                            this.inputResampledF = new float[rlength];

                        System.Array.Copy(this.inputResampled, this.inputResampledF, rlength);

                        // convert from resampler domain
                        for (int i = 0; i < this.inputResampledF.Length; ++i)
                            this.inputResampledF[i] /= (float)short.MaxValue;

                        this.oafrOutputBuffer.Write(this.inputResampledF);
                    }
                    else
                    {
                        this.oafrOutputBuffer.Write(this.inputMix);
                    }

                    // get the final result for output
                    // - retrieve what's possible in current frame
                    var length = Mathf.Min(data.Length, this.oafrOutputBuffer.Available());

                    var outArr = this.oafrOutputBuffer.Read(length);
                    for (int i = 0; i < length; ++i) data[i] += (outArr[i] * this.gain);

                    // if (data.Length > length) LOG(LogLevel.WARNING, "!skipped frame: {0}", length);
                }
            }
        }

        void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
                asource.Stop();

            this.customMixMatrix = null;
        }
        #endregion
    }
}