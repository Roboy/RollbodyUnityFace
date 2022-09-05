// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// namespace RosSharp.RosBridgeClient
// {
//     public class EmotionPublisher : UnityPublisher<MessageTypes.Std.String>
//     {
//         private RoboyAnimator animator;
//         private bool isMessageReceived;
//         private string emotion;

//         // Start is called before the first frame update
//         protected override void Start()
//         {
//             base.Start();
//             animator = GetComponent<RoboyAnimator>();
//         }

//         public void SetEmotion(int VisemeIndex,float TriggerValue)
//         {
//             Publish(new MessageTypes.Std.String(VisemeIndex.ToString()+":"+TriggerValue.ToString()));
//         }
//     }
// }