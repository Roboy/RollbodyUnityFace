using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;


public class HeadAnglesSetter : MonoBehaviour
{
    private float pitch = 0.0F;
    private float yaw = 0.0F;
    private float roll = 0.0F;
    TCPClient client;
    
    void Start(){
        client = GameObject.Find("CommsObject").GetComponent<TCPClient>();
    }

    // Update is called once per frame
    void Update()
    {
        pitch = client.headPitch;
        roll = client.headRoll;
        yaw = client.headYaw;
        // roll = float.Parse(client.headRoll, CultureInfo.InvariantCulture.NumberFormat);
        // yaw = float.Parse(client.headYaw, CultureInfo.InvariantCulture.NumberFormat);
        // pitch = float.Parse(client.headPitch, CultureInfo.InvariantCulture.NumberFormat);
        transform.eulerAngles = new Vector3(pitch, yaw, roll);
    }
}
