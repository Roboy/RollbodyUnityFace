// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using NetMQ;
using NetMQ.Sockets;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements NetMQ transport for AudioStreamNetworkClient
    /// </summary>
    public class AudioStreamNetMQClient : AudioStreamNetworkClient
    {
        // ========================================================================================================================================
        #region Required implementation
        /// <summary>
        /// Provides network buffer for base
        /// </summary>
        protected override ThreadSafeQueue<byte[]> networkQueue { get; set; }
        #endregion

        // ========================================================================================================================================
        #region Editor
        [Header("[Network client setup]")]
        [Tooltip("IP address of the AudioStreamNetworkSource to connect to")]
        public string serverIP = "0.0.0.0";
        [Tooltip("Port to connect to")]
        public int serverTransferPort = AudioStreamNetworkSource.listenPortDefault;
        [Tooltip("Automatically connect and play on Start with given parameters")]
        public bool autoConnect = true;
        #endregion

        // ========================================================================================================================================
        #region Non editor
        public int networkQueueSize
        {
            get
            {
                if (this.networkQueue != null)
                    return this.networkQueue.Size();
                else
                    return 0;
            }
        }
#if UNITY_WSA
        Task
#else
        Thread
#endif
        clientThread;
        /// <summary>
        /// ms
        /// </summary>
        public int clientThreadSleepTimeout = 10;
        /// <summary>
        /// 
        /// </summary>
        public bool isConnected { get; protected set; }
        #endregion

        // ========================================================================================================================================
        #region Networking

        public void Connect()
        {
            AsyncIO.ForceDotNet.Force();
            NetMQConfig.Cleanup();

            // networkQueue with max capacity
            this.networkQueue = new ThreadSafeQueue<byte[]>(100);

            // NetMQ for 3.5 runtime has problems connecting to // and non reachable IPs resulting in deadlock
            // TODO: verify with version for 4.x once 3.5. is no longer being used 
            if (string.IsNullOrEmpty(this.serverIP)
                || this.serverIP == "0.0.0.0")
            {
                Debug.LogWarningFormat("Won't by trying connecting to: {0}", this.serverIP);
                return;
            }

            // start a thread not tied to the Unity framerate
            this.clientThread =
#if UNITY_WSA
                new Task(new System.Action(this.ClientLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.ClientLoop));
            this.clientThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.clientLoopRunning = true;
            this.clientThread.Start();

            // TODO: xchange the situation
            this.serverSampleRate = 48000;
            this.serverChannels = 2;
            base.StartDecoder();
        }

        public void Disconnect()
        {
            base.StopDecoder();

            if (this.clientThread != null)
            {
                this.clientLoopRunning = false;
#if !UNITY_WSA
                this.clientThread.Join();
#endif
                this.clientThread = null;
            }

            NetMQConfig.Cleanup();
        }
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle, networking

        protected override void Start()
        {
            base.Start();

            if (this.autoConnect)
                this.Connect();
        }

        protected override void OnDestroy()
        {
            this.Disconnect();

            base.OnDestroy();
        }

        bool clientLoopRunning = false;

        void ClientLoop()
        {
            // does not compute BufferPool.SetBufferManagerBufferPool(1024 * 1024, 1024);

            // There are 10,000 ticks in a millisecond
            // timeout on socket read 1 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(10000);

            using (var subSocket = new SubscriberSocket(string.Format(">tcp://{0}:{1}", this.serverIP, this.serverTransferPort)))
            {
                // subscribe to 'any' topic
                subSocket.SubscribeToAnyTopic();

                this.isConnected = true;

                while (this.clientLoopRunning)
                {
                    // try to pull a packet
                    var msg = new Msg();
                    msg.InitEmpty();

                    if (subSocket.TryReceive(ref msg, msgTimeout))
                    {
                        var packet = msg.Data;
                        if (packet != null)
                        {
                            // Debug.LogFormat("Had a packet: {0} | {1} |", packet, msg.Size);

                            var barr = new byte[msg.Size];
                            System.Array.Copy(packet, barr, barr.Length);

                            this.networkQueue.Enqueue(barr);
                        }
                        else
                        {
                            Debug.LogFormat("Had a no packet /");
                        }
                    }
                    else
                    {
                        Debug.LogFormat("Got nothing");
                    }
#if UNITY_WSA
                    this.clientThread.Wait(this.clientThreadSleepTimeout);
#else
                    Thread.Sleep(this.clientThreadSleepTimeout);
#endif
                }

                subSocket.Disconnect(string.Format("tcp://{0}:{1}", this.serverIP, this.serverTransferPort));
            }

            this.isConnected = false;
        }

        #endregion

        // ========================================================================================================================================
        #region Support
        #endregion
    }
}