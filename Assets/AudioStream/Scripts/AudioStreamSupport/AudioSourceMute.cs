// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

namespace AudioStream
{
    public class AudioSourceMute : MonoBehaviour
    {
        [Tooltip("Supress AudioSource signal here.\nNote: this is implemented via OnAudioFilterRead, which might not be optimal - you can consider e.g. mixer routing and supress signal there.")]
        public bool mute = true;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (mute)
                System.Array.Clear(data, 0, data.Length);
        }
    }
}