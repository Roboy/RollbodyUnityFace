using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listener class for the head rotation
/// </summary>

namespace ROS2
{

public class HeadAnglesSubscriber : MonoBehaviour
{
    private ROS2UnityComponent ros2Unity;
    private ROS2Node ros2Node;
    private ISubscription<geometry_msgs.msg.Twist> head_angles_sub;
    private float pitch = 0.0F;
    private float yaw = 0.0F;
    private float roll = 0.0F;
    
    // Start is called before the first frame update
    void Start()
    {
        ros2Unity = GetComponent<ROS2UnityComponent>();
    }

    void subCallback(geometry_msgs.msg.Twist twistMsg) 
    {
        pitch = (float)twistMsg.Angular.X;
        yaw = 0.0F;
        roll = 0.0F;
        Debug.Log("Unity listener heard: [" + pitch.ToString() + "]");
        transform.eulerAngles = new Vector3(pitch, yaw, roll);
    }

    // Update is called once per frame
    void Update()
    {
        if (ros2Node == null && ros2Unity.Ok())
        {
            ros2Node = ros2Unity.CreateNode("HeadAnglesSubscriber");
            head_angles_sub = ros2Node.CreateSubscription<geometry_msgs.msg.Twist>(
              "headAngles", msg => subCallback(msg)
              );
        }
    }
}

}  // namespace ROS2
