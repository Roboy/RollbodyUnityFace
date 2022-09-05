// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using Concentus.Common;
using UnityEngine;

namespace AudioStream
{
    public class AudioStreamInput2D : AudioStreamInputBase
    {
        /// <summary>
        /// Resampler
        /// </summary>
        SpeexResampler speexResampler = null;
        /// <summary>
        /// Input/output samplerates ratio used for size of the resulting resample buffer
        /// </summary>
        float ratesRatio;
        /// <summary>
        /// Automatic mix matrix
        /// </summary>
        float[] mixMatrix;
        /// <summary>
        /// User provided mix matrix
        /// </summary>
        float[] customMixMatrix = null;
        /// <summary>
        /// Provide custom mix matrix between inputs and outputs - first dimension (rows) for outputs, the second one (columns) for inputs
        /// Actual signal's respective channel value is multiplied by provided float value according to the mapping
        /// Example of possible mappings: https://en.wikipedia.org/wiki/Matrix_decoder
        /// Needs to be called before starting the recording
        /// </summary>
        /// <param name="_customMixMatrix"></param>
        public void SetCustomMixMatrix(float[,] _customMixMatrix)
        {
            if (_customMixMatrix.Rank != 2)
                throw new System.NotSupportedException("Custom mix matrix providing mapping between inputs and outputs must be of dimension 2");

            var rows = _customMixMatrix.GetLength(0);
            var columns = _customMixMatrix.GetLength(1);

            this.customMixMatrix = new float[rows * columns];
            for (var row = 0; row < rows; ++row)
                for (var column = 0; column < columns; ++column)
                    this.customMixMatrix[row * columns + column] = _customMixMatrix[row, column];
        }
        /// <summary>
        /// Returns mix matrix currently being used - valid once recording has started - NaNs otherwise
        /// First dimension (rows) for outputs, 2nd dimension (columns) for inputs
        /// </summary>
        /// <returns></returns>
        public float[,] GetMixMatrix()
        {
            var result = new float[this.outputChannels, this.recChannels];

            // init w/ non existing values
            if (this.mixMatrix == null)
                for (var row = 0; row < this.outputChannels; ++row)
                    for (var column = 0; column < this.recChannels; ++column)
                        result[row, column] = float.NaN;
            else
                for (var i = 0; i < this.mixMatrix.Length; ++i)
                    result[i / this.recChannels, i % this.recChannels] = this.mixMatrix[i];

            return result;
        }
        // ========================================================================================================================================
        #region Recording
        /// <summary>
        /// If AudioSource is available on this GameObject, simulate resampling to output sample rate by setting the pitch (since we have no AudioClip with PCM callback created)
        /// , or resample using SpeexResampler - in which case we also need to map channels based on potential user setting
        /// </summary>
        protected override void RecordingStarted()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                // if not resampling is not needed just start the source

                if (this.resampleInput)
                {
                    if (this.useUnityToResampleAndMapChannels)
                    {
                        // resample the input by setting the pitch on the AudioSource based on input/output samplerate ratio
                        // this hack seems to be the only way of doing this without an AudioClip with given input rate wich has to have PCM callback (as in AudioStreamInput)
                        // this can lead to drifting over time if the rates are different enough though
                        var pitch_nominator = (float)(recRate * recChannels);
                        var pitch_denominator = (float)(this.outputSampleRate * this.outputChannels);
                        asource.pitch = pitch_nominator / pitch_denominator;

                        LOG(LogLevel.INFO, "'Resampling' pitch based on current settings: {0}, from: {1} / {2} [ (recRate * recChannels) {3} * {4} / (output samplerate * output channels) {5} * {6} ], speaker mode: {7}"
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
                            // (just assigns oveflown channel/s to last available)

                            this.mixMatrix = new float[this.outputChannels * this.recChannels];

                            // map from columns (inputs)
                            for (var column = 0; column < this.recChannels; ++column)
                            {
                                var row = Mathf.Min(column, this.outputChannels - 1);
                                this.mixMatrix[row * this.recChannels + column] = 1f;
                            }

                            // map from rows (outputs)
                            for (var row = 0; row < this.outputChannels; ++row)
                            {
                                var column = Mathf.Min(row, this.recChannels - 1);
                                this.mixMatrix[row * this.recChannels + column] = 1f;
                            }
                        }

                        // log the matrix
                        var matrixAsString = string.Empty;
                        for (var row = 0; row < this.outputChannels; ++row)
                        {
                            for (var column = 0; column < this.recChannels; ++column)
                                matrixAsString += this.mixMatrix[row * this.recChannels + column] + " ";

                            matrixAsString += "\r\n";
                        }

                        LOG(LogLevel.INFO, "{0}Mix matrix (Inputs <-> Outputs mapping):\r\n{1}", this.customMixMatrix != null ? "Custom " : string.Empty, matrixAsString);


                        this.ratesRatio = (float)this.outputSampleRate / (float)this.recRate;

                        // (speex uses the same channels # for input and output)
                        this.speexResampler = new SpeexResampler(this.outputChannels, this.recRate, this.outputSampleRate, 10);

                        // re/initialize resulting resampled array
                        this.inputResampled = null;

                        asource.pitch = 1f; // !
                    }
                }
                else
                {
                    asource.pitch = 1f; // !
                }

                asource.Play();
            }
            else
            {
                if (this.GetComponent<AudioListener>() == null)
                    Debug.LogError("AudioStreamInput2D has no hard dependency on AudioSource and there is none attached, there is no AudioListener attached too. Please make sure that AudioSource is attached to this GameObject, or that this component is attached to GameObject with AudioListener");
            }

            int channels, realchannels;
            result = recording_system.getChannelsPlaying(out channels, out realchannels);
            ERRCHECK(result, "system.getChannel", false);

            LOG(LogLevel.DEBUG, "Channels of recording device: {0}, real channels: {1}", channels, realchannels);
        }
        /// <summary>
        /// Nothing to do since data is retrieved via OnAudioFilterRead
        /// </summary>
        protected override void RecordingUpdate()
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

            // if paused don't process it
            if (this.isPaused)
                return;

            var inputSignal_length = inputSignal.Length;

            if (inputSignal_length > 0)
            {
                // if resampling is not needed just copy original signal out 
                if (!this.resampleInput)
                {
                    for (int i = 0; i < Mathf.Min(data.Length, inputSignal_length); ++i) data[i] += (inputSignal[i] * this.gain);
                    return;
                }

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
                        if (this.mixMatrix != null)
                            for (var in_ch = 0; in_ch < inputSample.Length; ++in_ch)
                                for (var out_ch = 0; out_ch < this.outputChannels; ++out_ch)
                                    mix[out_ch] += (inputSample[in_ch] * this.mixMatrix[out_ch * inputSample.Length + in_ch]);

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

        protected override void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
                asource.Stop();

            this.customMixMatrix = null;
        }
        #endregion
    }
}