// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using NetMQ;
using NetMQ.Sockets;
using System.Linq;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements NetMQ transport for AudioStreamNetworkSource
    /// </summary>
    public class AudioStreamNetMQSource : AudioStreamNetworkSource
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Network source setup]")]
        [Tooltip("Port to listen to for client connections")]
        public int listenPort = AudioStreamNetMQSource.listenPortDefault;
        [Tooltip("Maximum number of client connected simultaneously")]
        public int maxConnections = 10;
        #endregion

        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// Host IP as reported by .NET
        /// </summary>
        public string listenIP
        {
            get;
            protected set;
        }
#if UNITY_WSA
        Task
#else
        Thread
#endif
        sourceThread;
        /// <summary>
        /// ms
        /// </summary>
        public int sourceThreadSleepTimeout = 10;
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle, networking

        protected override void Start()
        {
            base.Start();

            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                this.listenIP = host.AddressList.FirstOrDefault(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
            }

            // start a thread not tied to the Unity framerate
            this.sourceThread =
#if UNITY_WSA
                new Task(new System.Action(this.SourceLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.SourceLoop));
                this.sourceThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.sourceLoopRunning = true;
            this.sourceThread.Start();
        }

        protected override void OnDestroy()
        {
            if (this.sourceThread != null)
            {
                this.sourceLoopRunning = false;
#if UNITY_WSA
                this.sourceThread.Wait(sourceThreadSleepTimeout);
#else
                Thread.Sleep(this.sourceThreadSleepTimeout);
                this.sourceThread.Join();
#endif
                this.sourceThread = null;
            }

            // stop the encode loop ( w networkQueue )
            base.OnDestroy();
        }

        bool sourceLoopRunning = false;

        void SourceLoop()
        {
            AsyncIO.ForceDotNet.Force();
            NetMQConfig.Cleanup();

            // does not compute BufferPool.SetBufferManagerBufferPool(1024 * 1024, 1024);

            // There are 10,000 ticks in a millisecond
            // timeout on socket read 1 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(10000);

            using (var pubSocket = new PublisherSocket(string.Format("@tcp://*:{0}", this.listenPort)))
            {
                while (this.sourceLoopRunning)
                {
                    // send output to all subscribers
                    byte[] packet = this.networkQueue.Dequeue();
                    if (packet != null)
                    {
                        var msg = new Msg();
                        msg.InitEmpty();
                        msg.InitPool(packet.Length);

                        msg.Put(packet, 0, packet.Length);

                        if (pubSocket.TrySend(ref msg, msgTimeout, false))
                        {
                            // Debug.LogFormat("Sent a packet: {0}", packet.Length);
                        }
                        else
                        {
                            Debug.LogFormat("Didn't send a packet: {0}", packet.Length);
                        }
                    }
#if UNITY_WSA
                    this.sourceThread.Wait(this.sourceThreadSleepTimeout);
#else
                    Thread.Sleep(this.sourceThreadSleepTimeout);
#endif
                }
            }

            NetMQConfig.Cleanup();
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        #endregion
    }
}