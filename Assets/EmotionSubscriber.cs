// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// namespace RosSharp.RosBridgeClient
// {
//     public class EmotionSubscriber : UnitySubscriber<MessageTypes.RoboyControl.Emotion>
//     {
//         private RoboyAnimator animator;

//         [Serializable]
//         public class FacePart
//         {
//             public string Name;
//             public Transform Part;
//             internal Vector3 initialScale;
//             internal Vector3 initialPosition;
//             internal Vector3 offsetScale;
//             internal Vector3 offsetPosition;
//         }
//         public List<FacePart> faceSetup = new List<FacePart>();
//         private bool isMessageReceived;
//         private string emotion;

//         // Start is called before the first frame update
//         protected override void Start()
//         {
//             base.Start();
//             animator = GetComponent<RoboyAnimator>();
//             for (int i = 0; i < faceSetup.Count; i++)
//             {
//                 FacePart f = faceSetup[i];
//                 f.initialScale = f.Part.localScale;
//                 f.initialPosition = f.Part.localPosition;
//                 f.offsetPosition = PlayerPrefs.HasKey("calib_pos_" + f.Name) ? StringToVector3(PlayerPrefs.GetString("calib_pos_" + f.Name)) : Vector3.zero;
//                 f.offsetScale = PlayerPrefs.HasKey("calib_scale_" + f.Name) ? StringToVector3(PlayerPrefs.GetString("calib_scale_" + f.Name)) : Vector3.zero;
//                 f.Part.localPosition = f.initialPosition + f.offsetPosition;
//                 f.Part.localScale = f.initialScale + f.offsetScale;
//             }
//         }

//         private void OnDisable()
//         {
//             for (int i = 0; i < faceSetup.Count; i++)
//             {
//                 FacePart f = faceSetup[i];
//                 PlayerPrefs.SetString("calib_pos_" + f.Name, f.offsetPosition.ToString());
//                 PlayerPrefs.SetString("calib_scale_" + f.Name, f.offsetScale.ToString());
//             }
//         }

//         public static Vector3 StringToVector3(string sVector)
//         {
//             // Remove the parentheses
//             if (sVector.StartsWith("(") && sVector.EndsWith(")"))
//             {
//                 sVector = sVector.Substring(1, sVector.Length - 2);
//             }

//             // split the items
//             string[] sArray = sVector.Split(',');

//             // store as a Vector3
//             Vector3 result = new Vector3(
//                 float.Parse(sArray[0]),
//                 float.Parse(sArray[1]),
//                 float.Parse(sArray[2]));

//             return result;
//         }

//         // Update is called once per frame
//         private void Update()
//         {
//             if (isMessageReceived)
//                 ProcessMessage();

//         }

//         protected override void ReceiveMessage(MessageTypes.RoboyControl.Emotion message)
//         {
//             emotion = message.emotion;
//             isMessageReceived = true;
//         }

//         private void ProcessMessage()
//         {
//             if (isMessageReceived)
//             {
//                 // Move Face Parts around using emotions structured as "calib_FacePart.Name_Operation_Direction(+/-)", e.g. calib_eyeleft_scale_x+
//                 if (emotion.StartsWith("calib_"))
//                 {
//                     string[] ep = emotion.Split('_');
//                     if (ep.Length >= 3 && faceSetup.Exists((x) => x.Name == ep[1]))
//                     {
//                         FacePart f = faceSetup.Find((x) => x.Name == ep[1]);
//                         switch (ep[2])
//                         {
//                             case "scale":
//                                 switch (ep[3])
//                                 {
//                                     case "x+":
//                                         f.offsetScale += new Vector3(.02f, 0, 0);
//                                         break;
//                                     case "x-":
//                                         f.offsetScale += new Vector3(-.02f, 0, 0);
//                                         break;
//                                     case "y+":
//                                         f.offsetScale += new Vector3(0, .02f, 0);
//                                         break;
//                                     case "y-":
//                                         f.offsetScale += new Vector3(0, -.02f, 0);
//                                         break;
//                                 }
//                                 f.Part.localScale = f.initialScale + f.offsetScale;
//                                 break;
//                             case "position":
//                                 switch (ep[3])
//                                 {
//                                     case "x+":
//                                         f.offsetPosition += new Vector3(3, 0, 0);
//                                         break;
//                                     case "x-":
//                                         f.offsetPosition += new Vector3(-3, 0, 0);
//                                         break;
//                                     case "y+":
//                                         f.offsetPosition += new Vector3(0, 3, 0);
//                                         break;
//                                     case "y-":
//                                         f.offsetPosition += new Vector3(0, -3, 0);
//                                         break;
//                                 }
//                                 f.Part.localPosition = f.initialPosition + f.offsetPosition;
//                                 break;

//                         }
//                         PlayerPrefs.SetString("calib_pos_" + f.Name, f.offsetPosition.ToString());
//                         PlayerPrefs.SetString("calib_scale_" + f.Name, f.offsetScale.ToString());
//                     }
//                 }
//                 else
//                 {
//                         Debug.Log(emotion);
//                         animator.SetEmotion(emotion);
//                     }
//                     isMessageReceived = false;
//                 }
//             }
//         }
//     }
