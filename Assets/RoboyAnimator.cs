using System.IO;
using System.Net.Sockets;
using UnityEngine;
using Ros;

[RequireComponent(typeof(Animator))]
public class RoboyAnimator : MonoBehaviour
{

    public Vector3 targetHeadEulerAngle;
    Animator anim;
    Node node;
    Subscriber sub1;
    Subscriber sub2;
    Publisher<Float64Msg> pub;
    TcpClient tcp;
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
        tcp.Close();
    }

    // Update is called once per frame
    void Update()
    {
   
        try
        {
            if (!IsAlive(tcp))
            {
                UnityEngine.Debug.LogWarning("Connection to the TCP ROS bridge with IP " + ROS_MASTER_IP + " failed, retrying...");
                if (tcp != null) tcp.Close();
                                
                if (System.Environment.GetEnvironmentVariable("ROS_MASTER_URI") == null)
                {
                    UnityEngine.Debug.LogWarning("Environmental variable ROS_MASTER_URI is not set. Assuming ROS is running on localhost.");
                    ROS_MASTER_IP = "127.0.0.1";
                }
                else
                {
                    System.Uri ROS_MASTER_URI = new System.Uri(System.Environment.GetEnvironmentVariable("ROS_MASTER_URI"));
                    ROS_MASTER_IP = ROS_MASTER_URI.DnsSafeHost;
                }
                                
                tcp = new TcpClient(ROS_MASTER_IP, 9091);
                node = new Node(new StreamReader(tcp.GetStream()), new StreamWriter(tcp.GetStream()));
                sub1 = node.Subscribe<SpeechMsg>("/roboy/cognition/speech/synthesis",
                                                msg => anim.SetBool("talking", !msg.phoneme.Equals("sil")));
                sub2 = node.Subscribe<EmotionMsg>("/roboy/cognition/face/emotion",
                                                msg => anim.SetTrigger(msg.emotion));
                pub = node.Advertise<Float64Msg>("/bla");
            }
            pub.Publish(new Float64Msg { data = 1.0 });
            node.SpinOnce();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning(e.Message + e.StackTrace);
        }
        
        if(Input.GetKeyDown(KeyCode.L))
            anim.SetTrigger("lookleft");
        if (Input.GetKeyDown(KeyCode.R))
            anim.SetTrigger("lookright");

        if (Random.value < 0.0001f)
            anim.SetTrigger("blink");
        //if (Random.value < 0.001f)
        //{
        //    anim.SetTrigger("shy");
        //}
        //   anim.SetTrigger("blink");
    }
}
