using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class AutoBlink : MonoBehaviour
{

    DateTime lastBlink;
    double nextBlinkDelta;
    Animator anim;
    System.Random rand = new System.Random(); //reuse this if you are generating many
    public double blinkMean = 12;
    public double blinkStdDev = 3;

    // Start is called before the first frame update
    void Start()
    {
        lastBlink = DateTime.Now;
        anim = GetComponent<Animator>();
        nextBlinkDelta = 1;
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log((DateTime.Now - lastBlink).TotalSeconds + " vs " + nextBlinkDelta);
        if ( (DateTime.Now - lastBlink).TotalSeconds > nextBlinkDelta)
        {
            anim.SetTrigger("blink_both");
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            nextBlinkDelta = blinkMean + blinkStdDev * randStdNormal; //random normal(mean,stdDev^2)
            lastBlink = DateTime.Now;
        }
    }
}
