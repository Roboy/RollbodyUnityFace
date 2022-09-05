// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

/// <summary>
/// Computes RMS for visualization per channel from this' audio filter
/// </summary>
public class RMSPerChannelToTransforms : MonoBehaviour
{
    /// <summary>
    /// Channel count for distributing RMS values per channel
    /// </summary>
    int channelCount;

    public void SetChannels(int value)
    {
        this.channelCount = value;

        this.scale = new Vector3[this.channelCount];
        this.rotation = new Quaternion[this.channelCount];
        this.squareSum = new float[this.channelCount];
        this.sampleCount = new int[this.channelCount];
        this.yRot = new float[this.channelCount];
    }

    public Vector3[] scale;
    public Quaternion[] rotation;

    const float zeroOffset = 1.5849e-13f;
    const float refLevel = 0.70710678118f; // 1/sqrt(2)
    const float minDB = -60.0f;

    float[] squareSum;
    int[] sampleCount;
    // float xRot;
    float[] yRot;

    void Update()
    {
        for (var i = 0; i < this.channelCount; ++i)
        {
            if (sampleCount[i] < 1) return;

            var rms = Mathf.Min(1.0f, Mathf.Sqrt(squareSum[i] / sampleCount[i]));
            var db = 20.0f * Mathf.Log10(rms / refLevel + zeroOffset);
            var meter = -Mathf.Log10(0.1f + db / (minDB * 1.1f));

            // map meter to scale reaction, clamp at 0 not to overshoot to negative scale
            var someReactiveVariable = meter * 50f;
            someReactiveVariable = Mathf.Clamp(someReactiveVariable, 0f, someReactiveVariable);


            this.scale[i] = Vector3.one * someReactiveVariable;
            this.rotation[i] = Quaternion.Euler(0f, (yRot[i] += someReactiveVariable) / 20f, 0f);

            sampleCount[i] = 0;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (var channel = 0; channel < this.channelCount; ++channel)
        {
            squareSum[channel] = 0;

            for (var i = 0; (i * channels) < data.Length; ++i)
            {
                var level = data[(i * channels) + channel];
                // Looks like NaN in data buffer _can_ happen - is that driver/Unity FMOD/ bug ?
                if (float.IsNaN(level)) level = 0;

                squareSum[channel] += level * level;
            }

            sampleCount[channel] += data.Length;
        }
    }
}