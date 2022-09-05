// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Simple component for capturing a game object's filter callback audio buffer for other filters to read from
    /// (no AudioSource dependency since it can be used e.g. on listener too)
    /// </summary>
	public class AudioSourceCaptureBuffer : MonoBehaviour
	{
        public float[] captureBuffer = null;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.captureBuffer == null
                || this.captureBuffer.Length != data.Length)
                this.captureBuffer = new float[data.Length];

            System.Array.Copy(data, 0, this.captureBuffer, 0, data.Length);
        }
    }
}