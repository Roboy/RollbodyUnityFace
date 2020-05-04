using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;
using WebSocketSharp.Server;

using System;
using RosSharp.RosBridgeClient;
using std_msgs = RosSharp.RosBridgeClient.Messages.Standard;
using roboy_msgs = RosSharp.RosBridgeClient.Messages.Roboy;
using std_srvs = RosSharp.RosBridgeClient.Services.Standard;
using rosapi = RosSharp.RosBridgeClient.Services.RosApi;
using RosSharp.RosBridgeClient.Protocols;

[RequireComponent(typeof(Animator))]
public class RoboyAnimator : MonoBehaviour
{

    public Vector3 targetHeadEulerAngle;
    public List<Sprite> icons;
    public SpriteRenderer glasses;
    public Image emojiRight;
    public Image emojiLeft;
    public SpriteRenderer black;
    Animator anim;
    bool offlineToggle = false;
    int errorWait;
    string ROS_MASTER_IP;
    bool pirate = false;
    bool cryingRoboy = false;
    bool specs = false;
    bool sunglasses_on = false;
    bool moustache = false;
    bool talking = false;
    RosSocket rosSocket;
    string speech_subscription_id, emotion_subscription_id;
    WebSocketSharpProtocol ws;
    List<string> emotions = new List<string>();
    int frame = 0;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void ConnectSubscribe() {

        string uri = Environment.GetEnvironmentVariable("ROS_WEBSOCKET_URI");
        if (string.IsNullOrEmpty(uri)) {
            uri = "ws:192.168.0.110:9090";
        }
        ws = new WebSocketSharpProtocol(uri);
        rosSocket = new RosSocket(ws);
        speech_subscription_id = rosSocket.Subscribe<roboy_msgs.SpeechSynthesis>("/roboy/cognition/speech/synthesis", SpeechSubscriptionHandler);
        emotion_subscription_id = rosSocket.Subscribe<roboy_msgs.Emotion>("/roboy/cognition/face/emotion", EmotionSubscriptionHandler);
        UnityEngine.Debug.Log("Subscribed to: " + speech_subscription_id);
        UnityEngine.Debug.Log("Subscribed to: " + emotion_subscription_id);
    }

    private void SpeechSubscriptionHandler(roboy_msgs.SpeechSynthesis message)
        {
           UnityEngine.Debug.LogWarning("Speech message arrived:" + (message).phoneme);
           talking = "sil" == (message).phoneme ? false : true;
        }

    private void EmotionSubscriptionHandler(roboy_msgs.Emotion message)
    {
        UnityEngine.Debug.LogWarning("Emotion message arrived:" + (message).emotion);
        emotions.Add(message.emotion);
    }


    private void OnDestroy()
    {
        rosSocket.Unsubscribe(speech_subscription_id);
        rosSocket.Unsubscribe(emotion_subscription_id);
        rosSocket.Close();
    }

    IEnumerator WaitForConnection()
    {
        yield return new WaitUntil(() => rosSocket.protocol.IsAlive());
    }

    // Update is called once per frame
    void Update()
    {
        if (frame % 100 == 0 && false)
        {

            if (rosSocket ==  null) {
                ConnectSubscribe();
            }
            else {
                if (!rosSocket.protocol.IsAlive()) {
                    UnityEngine.Debug.Log("Lost connection. Retrying...");

                    while (!rosSocket.protocol.IsAlive())
                    {
                        rosSocket.protocol.Connect();
                        WaitForConnection();
                    }

                    rosSocket.Unsubscribe(speech_subscription_id);
                    rosSocket.Unsubscribe(emotion_subscription_id);
                    speech_subscription_id = rosSocket.Subscribe<roboy_msgs.SpeechSynthesis>("/roboy/cognition/speech/synthesis", SpeechSubscriptionHandler);
                    emotion_subscription_id = rosSocket.Subscribe<roboy_msgs.Emotion>("/roboy/cognition/face/emotion", EmotionSubscriptionHandler);
                    UnityEngine.Debug.Log("Subscribed to: " + speech_subscription_id);
                    UnityEngine.Debug.Log("Subscribed to: " + emotion_subscription_id);

                }
            }

        }
        frame += 1;

        if (Input.GetKeyDown(KeyCode.O))
        {
            offlineToggle = !offlineToggle;
        }
        anim.SetBool("talking", talking);
        anim.SetBool("cry", cryingRoboy);
        anim.SetBool("moustache", moustache);
        anim.SetBool("pirate", pirate);
        anim.SetBool("sunglasses_on", sunglasses_on);
        anim.SetBool("specs", specs);

        foreach (string e in emotions)
        {
            SetEmotion(e);
        }
        emotions.Clear();

        if (UnityEngine.Random.value < 0.001f)
            anim.SetTrigger("blink");

        KeyboardControls();

    }

    void KeyboardControls()
    {
         if (Input.GetKeyDown(KeyCode.T))
         {
             SetEmotion("url:https:upload.wikimedia.org/wikipedia/commons/7/7e/Cute-Ball-Favorites-icon.png");
         }
         if (Input.GetKeyDown(KeyCode.E))
         {
             SetEmotion("img:money");
         }

         if (Input.GetKeyDown(KeyCode.A))
             SetEmotion("angry_new");
         if (Input.GetKeyDown(KeyCode.S))
             SetEmotion("shy");
         if (Input.GetKeyDown(KeyCode.K))
             SetEmotion("kiss");
         if (Input.GetKeyDown(KeyCode.L))
             SetEmotion("lookleft");
         if (Input.GetKeyDown(KeyCode.R))
             SetEmotion("lookright");
         if (Input.GetKeyDown(KeyCode.B))
             SetEmotion("blink");
         if (Input.GetKeyDown(KeyCode.D))
             SetEmotion("tongue_out");
         if (Input.GetKeyDown(KeyCode.W))
             SetEmotion("smileblink");
         if (Input.GetKeyDown(KeyCode.Q))
             SetEmotion("happy");
         if (Input.GetKeyDown(KeyCode.Y))
             SetEmotion("happy2");
         if (Input.GetKeyDown(KeyCode.H))
             SetEmotion("hearts");
         if (Input.GetKeyDown(KeyCode.N))
             SetEmotion("angry");
         if (Input.GetKeyDown(KeyCode.X))
             SetEmotion("pissed");
         if (Input.GetKeyDown(KeyCode.V))
             SetEmotion("hypno");
         if (Input.GetKeyDown(KeyCode.U))
             SetEmotion("hypno_color");
         if (Input.GetKeyDown(KeyCode.I))
             SetEmotion("rolling");
         if (Input.GetKeyDown(KeyCode.Z))
             SetEmotion("surprise_mit_augen");

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            black.enabled = !black.enabled;
        }
         if (Input.GetKeyDown(KeyCode.P))
         {
             pirate = !pirate;
         }

         if (Input.GetKeyDown(KeyCode.C))
         {
             cryingRoboy = !cryingRoboy;
         }

         if (Input.GetKeyDown(KeyCode.G))
         {
             specs = !specs;
         }


         if (Input.GetKeyDown(KeyCode.M))
         {
             moustache = !moustache;
         }

         if (Input.GetKeyDown(KeyCode.F))
         {
             sunglasses_on = !sunglasses_on;
         }

        if (Input.GetKey(KeyCode.Space))
        {
            talking = !talking;
        }
        anim.SetBool("talking", Input.GetKey(KeyCode.Space));
    }



    void SetEmotion(string emotion)
    {
        switch (emotion)
        {
            case "hearts":
                emotion = "img:Heart";
                break;
            case "cry":
                cryingRoboy = !cryingRoboy;
                break;
            case "moustache":
                moustache = !moustache;
                break;
            case "pirate":
                pirate = !pirate;
                break;
            case "sunglasses_on":
                sunglasses_on = !sunglasses_on;
                break;
            case "toggleblack":
                black.enabled = !black.enabled;
                break;
            case "glasseson":
                glasses.color = Color.white;
                break;
            case "glassesoff":
                glasses.color = new Color(1, 1, 1, 0);
                break;
            default:
                break;
        }

        if (emotion.Contains("img:"))
        {
            string emoji = emotion.Substring(4);
            foreach (Sprite s in icons)
            {
                if (s.name == emoji)
                {
                    emojiLeft.sprite = s;
                    emojiRight.sprite = s;
                    anim.SetTrigger("hearts");
                    break;
                }
            }
        }
        else if (emotion.Contains("url:"))
        {
            string emoji = emotion.Substring(4);
            emojiLeft.sprite = IMG2Sprite.LoadNewSprite(emoji);
            emojiRight.sprite = IMG2Sprite.LoadNewSprite(emoji);
            anim.SetTrigger("hearts");
        }
        else
            anim.SetTrigger(emotion);
    }

}
