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
    private bool show = false;
    private string rosIP = "ROS IP";

    private void OnGUI()
    {
        if (show == true)
        {
            GUI.skin.textField.fontSize = 30;
            rosIP = GUI.TextField(new Rect(Screen.width - 250, Screen.height - 40, 250, 40), rosIP, 30);
        }
    }

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
        if (Input.GetKey(KeyCode.Escape))
        {
            show = !show;
        }
        char x = rosIP[rosIP.Length - 1];
        if (rosIP[rosIP.Length - 1] == '-')
        {

            try
            {
                if (!IsAlive(tcp))
                {
                    if (tcp != null) tcp.Close();
                    tcp = new TcpClient(rosIP.Remove(rosIP.Length - 1), 9091);
                    node = new Node(new StreamReader(tcp.GetStream()), new StreamWriter(tcp.GetStream()));
                    sub1 = node.Subscribe<SpeechMsg>("/roboy/cognition/speech/synthesis",
                                                    msg => anim.SetBool("talking", !msg.phoneme.Equals("sil")));
                    sub2 = node.Subscribe<EmotionMsg>("/roboy/control/face/emotion",
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
        }
        if(Input.GetKeyDown(KeyCode.L))
            anim.SetTrigger("lookleft");
        if (Input.GetKeyDown(KeyCode.R))
            anim.SetTrigger("lookright");

        //if (Random.value < 0.001f)
        //    anim.SetTrigger("smileblink");
        //if (Random.value < 0.001f)
        //{
        //    anim.SetTrigger("shy");
        //}
        //   anim.SetTrigger("blink");
    }
}
