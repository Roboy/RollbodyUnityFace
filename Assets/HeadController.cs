using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;


public class HeadController : MonoBehaviour
{
    private TCPHeadClient client;
    private GameObject head;
    
    void Start(){
        client = gameObject.GetComponent<TCPHeadClient>();
        head = GameObject.Find("roboy30head_uvfront");
    }

    void Update()
    {
        float pitch = Math.Clamp(client.headPitch, -10, 10);
        float roll = Math.Clamp(client.headRoll, -20, 20);
        float yaw = Math.Clamp(client.headYaw, -30, 30);
        head.SetActive(client.isPresent);
        head.transform.eulerAngles = new Vector3(pitch, yaw+180, roll);
    }
}
