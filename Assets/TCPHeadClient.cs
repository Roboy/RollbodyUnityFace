using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json; 
using System.Globalization;
using System.Linq;
using TMPro;

// from https://gist.github.com/danielbierwirth/0636650b005834204cb19ef5ae6ccedb


public class TCPHeadClient : MonoBehaviour {  	
	#region private members 	
	private TcpClient socketConnection; 	
	private Thread clientReceiveThread; 	
	private Boolean isConnected = false;
	private int updatesSinceReconnect = 0;
	private bool showText = false;
	private TextMeshPro opText;
	#endregion  	

	public float headPitch = 0.0F;
    public float headYaw = 0.0F;
    public float headRoll = 0.0F; 
	public Boolean isTalking = false;
	public Boolean isPresent = false;
	public String emotion = ""; 

	// Use this for initialization 	
	void Start () {
		ConnectToTcpServer();     
		opText = GameObject.Find("OpText").GetComponent<TextMeshPro>();
	}  	
	// Update is called once per frame
	void Update () {         
		// if (Input.GetKeyDown(KeyCode.Return)) {             
		//		SendMessage();         
		// }    
		if (!isConnected) {
			opText.text = "TCP connection broken. Reconnecting...";
			isPresent = false;
			updatesSinceReconnect++;
			if (updatesSinceReconnect > 100) {
				updatesSinceReconnect = 0;
				System.Threading.Thread.Sleep(1000);
				ConnectToTcpServer();
			}
		}
		else {
			opText.text = "Waiting for the Operator.";
		}
	}  	

	/// <summary> 	
	/// Setup socket connection. 	
	/// </summary> 	
	private void ConnectToTcpServer () { 		
		try {  			
			clientReceiveThread = new Thread (new ThreadStart(ListenForData)); 			
			clientReceiveThread.IsBackground = true; 			
			clientReceiveThread.Start();  		
		} 		
		catch (Exception e) { 			
			Debug.Log("On client connect exception " + e); 		
		} 	
	}  	

	/// <summary> 	
	/// Runs in background clientReceiveThread; Listens for incomming data. 	
	/// </summary>     
	private void ListenForData() { 	
		// This method must run as fast as possible to avoid broken pipe errors.	
		try { 			
			//socketConnection = new TcpClient("localhost", 8052); // non-privileged ports are >1023
			// IMPORTANT: CONFIGURE THE HOST ADDRESS
			socketConnection = new TcpClient("192.168.1.150", 8052); 

			Debug.Log("Successfully connected to the TCP server.");
			isConnected = true; 		
			Byte[] bytes = new Byte[1024];             
			while (true) { 				
				// Get a stream object for reading 				
				using (NetworkStream stream = socketConnection.GetStream()) { 					
					int length; 					
					// Read incomming stream into byte arrary. 					
					while ((length = stream.Read(bytes, 0, bytes.Length)) != 0) { 						
						var incommingData = new byte[length]; 						
						Array.Copy(bytes, 0, incommingData, 0, length); 						
						// Convert byte array to string message. 						
						string msg = Encoding.ASCII.GetString(incommingData);
						// Debug.Log("server message received as: " + msg); 	
						// msg has format '{"key1": value1, "key2": value2, ...}'
						var msgDict = msg.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)
               							  .Select(part => part.Split('='))
               							  .ToDictionary(split => split[0], split => split[1]);
						//isTalking = bool.Parse(msgDict["isTalking"]);
						headRoll = float.Parse(msgDict["headRoll"], CultureInfo.InvariantCulture.NumberFormat);
        				headYaw = float.Parse(msgDict["headYaw"], CultureInfo.InvariantCulture.NumberFormat);
        				headPitch = float.Parse(msgDict["headPitch"], CultureInfo.InvariantCulture.NumberFormat);
						isPresent = bool.Parse(msgDict["isPresent"]);
						emotion = msgDict["emotion"];
					} 				
				} 			
			}         
		}         
		catch (SocketException socketException) {             
			Debug.Log("Caught socket exception: " + socketException + ". Restarting the client now.");     
			isConnected = false;
			clientReceiveThread.Abort();  
		}
		catch (InvalidOperationException invalidOperationException) {
			Debug.Log("Caught socket exception: " + invalidOperationException + ". Restarting the client now.");  
			isConnected = false;
			clientReceiveThread.Abort();
		}
		catch (ArgumentException argException) {
			Debug.Log("Caught argument exception: " + argException + ". Restarting the client now.");  
			isConnected = false;
			clientReceiveThread.Abort(); 
		} 
	}  	

	/// <summary> 	
	/// Send message to server using socket connection. 	
	/// </summary> 	
	private void SendMessage() {         
		if (socketConnection == null) {             
			return;         
		}  		
		try { 			
			// Get a stream object for writing. 			
			NetworkStream stream = socketConnection.GetStream(); 			
			if (stream.CanWrite) {                 
				string clientMessage = "This is a message from one of your clients."; 				
				// Convert string message to byte array.                 
				byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(clientMessage); 				
				// Write byte array to socketConnection stream.                 
				stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);                 
				Debug.Log("Client sent his message - should be received by server");             
			}         
		} 		
		catch (SocketException socketException) {             
			Debug.Log("Socket exception: " + socketException);         
		}     
	} 
}