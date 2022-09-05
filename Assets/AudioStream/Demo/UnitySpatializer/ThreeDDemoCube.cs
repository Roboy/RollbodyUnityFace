// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ThreeDDemoCube : MonoBehaviour
{
    float radius = 8f;
    float speed;

    AudioSource asource;
    float signalEnergy = 0f;
    float[] aBuffer = new float[512];

    Material mat;

    void Start()
    {
        this.speed = Mathf.Clamp(Random.value, 0.1f, 1f);
        this.asource = this.GetComponent<AudioSource>();
        this.mat = this.GetComponent<MeshRenderer>().material;
    }

    void Update()
    {
        this.transform.position = new Vector3(
            Mathf.Cos(Time.timeSinceLevelLoad * this.speed) * this.radius
            , 0f
            , Mathf.Sin(Time.timeSinceLevelLoad * this.speed) * this.radius
            );

        if (this.asource.isPlaying
            && this.asource.clip != null)
        {
            // access the sound buffer and look at some values
            this.signalEnergy = 0;

            for (int ch = 0; ch < this.asource.clip.channels; ++ch)
            {
                this.asource.GetOutputData(this.aBuffer, ch);

                for (int i = 0; i < this.aBuffer.Length; ++i)
                    this.signalEnergy += this.aBuffer[i] * this.aBuffer[i];
            }

            this.signalEnergy = Mathf.Lerp(0f, 1f, this.signalEnergy * 10f);

            this.mat.color = Color.Lerp(Color.blue, Color.yellow, this.signalEnergy);
        }
    }
}
