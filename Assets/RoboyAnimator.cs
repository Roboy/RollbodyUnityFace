using Ros;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public class RoboyAnimator : MonoBehaviour
{

    public Vector3 targetHeadEulerAngle;
    public List<Sprite> icons;
    public SpriteRenderer glasses;
    public Image emojiRight;
    public Image emojiLeft;
    Animator anim;
    Node node;
    Subscriber sub1;
    Subscriber sub2;
    Publisher<EmotionMsg> pub;
    TcpClient tcp;
    bool offlineToggle = true;
    int errorWait;
    string ROS_MASTER_IP;

    // Use this for initialization
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    bool IsAlive(TcpClient client)
    {
        if (client == null) return false;
        try
        {
            bool connected = !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);

            return connected;
        }
        catch
        {
            return false;
        }
    }

    private void OnDestroy()
    {
        if(tcp != null)
            tcp.Close();
    }

    // Update is called once per frame
    void Update()
    {

        //*
        if (!offlineToggle && errorWait <= 0)
        {
            try
            {
                if (!IsAlive(tcp))
                {
                    UnityEngine.Debug.LogWarning("Connection to the TCP ROS bridge with IP " + ROS_MASTER_IP + " failed, retrying...");
                    if (tcp != null) tcp.Close();

                    if (Application.platform == RuntimePlatform.Android)
                    {
                        UnityEngine.Debug.LogWarning("Running on Android, assuming ROS_MASTER is on Magic IP (10.42.0.1)");
                        ROS_MASTER_IP = "10.42.0.225";
                    }
                    else if (System.Environment.GetEnvironmentVariable("ROS_MASTER_URI") == null)
                    {
                        UnityEngine.Debug.LogWarning("Environmental variable ROS_MASTER_URI is not set. Assuming ROS is running on localhost.");
                        ROS_MASTER_IP = "10.183.90.43";
                    }
                    else
                    {
                        System.Uri ROS_MASTER_URI = new System.Uri(System.Environment.GetEnvironmentVariable("ROS_MASTER_URI"));
                        ROS_MASTER_IP = ROS_MASTER_URI.DnsSafeHost;
                    }
                    //ROS_MASTER_IP = "192.168.0.103";
                    tcp = new TcpClient(ROS_MASTER_IP, 9091);
                    node = new Node(new StreamReader(tcp.GetStream()), new StreamWriter(tcp.GetStream()));
                    sub1 = node.Subscribe<SpeechMsg>("/roboy/cognition/speech/synthesis",
                                                    msg => anim.SetBool("talking", !msg.phoneme.Equals("sil")));
                    sub2 = node.Subscribe<EmotionMsg>("/roboy/cognition/face/emotion",
                                                    msg => SetEmotion(msg));
                    pub = node.Advertise<EmotionMsg>("/bla");
                    pub.Publish(new EmotionMsg { emotion = "just_started" });
                }
                node.SpinOnce();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning(e.Message + e.StackTrace);
                errorWait = 20;
            }//*/
        }
        if (errorWait > 0) errorWait--;

        if (Input.GetKeyDown(KeyCode.O))
        {
            offlineToggle = !offlineToggle;
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetEmotion("url:https://upload.wikimedia.org/wikipedia/commons/7/7e/Cute-Ball-Favorites-icon.png");
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            SetEmotion("img:money");
        }
        
        if (Input.GetKeyDown(KeyCode.L))
            SetEmotion("lookleft");
        if (Input.GetKeyDown(KeyCode.R))
            SetEmotion("lookright");

        if (Input.GetKeyDown(KeyCode.S))
            SetEmotion("shy");
        if (Input.GetKeyDown(KeyCode.B))
            SetEmotion("blink");
        if (Input.GetKeyDown(KeyCode.K))
            SetEmotion("kiss");
        if (Input.GetKeyDown(KeyCode.K))
            SetEmotion("hearts");
        if (Input.GetKeyDown(KeyCode.W))
            SetEmotion("smileblink");
        if (Input.GetKeyDown(KeyCode.G))
            SetEmotion("glasseson");
        if (Input.GetKeyDown(KeyCode.H))
            SetEmotion("glassesoff");

        //anim.SetBool("talking", Input.GetKey(KeyCode.Space));

        if (Random.value < 0.001f)
            anim.SetTrigger("blink");
        //if (Random.value < 0.001f)
        //{
        //    anim.SetTrigger("shy");
        //}
        //   anim.SetTrigger("blink");
        //*/
    }

    void SetEmotion(EmotionMsg e)
    {
        pub.Publish(e);
        SetEmotion(e.emotion);
    }

    void SetEmotion ( string emotion )
    {
        StartCoroutine(SetEmotionInternal(emotion));
    }

    IEnumerator SetEmotionInternal(string emotion) {
        if (emotion == "tears")
        {
            anim.SetBool("cryingRoboy", true);
            // pause 2 sec
            yield return new WaitForSeconds(6);
            anim.SetBool("cryingRoboy", false);
            // trigger to idle
        }
        if (emotion == "pissed")
            anim.SetTrigger("rolling_eyes");
        if (emotion == "sunglasses")
        {
            anim.SetBool("sunglasses_on", true);
            yield return new WaitForSeconds(4);
            anim.SetBool("sunglasses_on", false);             
        }

        if (emotion == "suprised")
            anim.SetTrigger("surprise_mit_augen"); 

           
        if (emotion == "glasseson")
            glasses.color = Color.white;
        else if (emotion == "glassesoff")
            glasses.color = new Color(1,1,1,0);
        else if (emotion == "hearts") emotion = "img:Heart";
        else if (emotion.Contains("img:"))
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
