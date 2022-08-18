using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json; 
using System.Globalization;

// from https://gist.github.com/danielbierwirth/0636650b005834204cb19ef5ae6ccedb


public class TCPClient : MonoBehaviour {  	
	#region private members 	
	private TcpClient socketConnection; 	
	private Thread clientReceiveThread; 	
	#endregion  	

	public float headPitch = 0.0F;
    public float headYaw = 0.0F;
    public float headRoll = 0.0F; 
	public Boolean isTalking = false;
	public String emotion = ""; 

	// Use this for initialization 	
	void Start () {
		ConnectToTcpServer();     
	}  	
	// Update is called once per frame
	void Update () {         
		// if (Input.GetKeyDown(KeyCode.Return)) {             
		//SendMessage();         
		// }     
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
		try { 			
			socketConnection = new TcpClient("localhost", 8052);  			
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
						Debug.Log("server message received as: " + msg); 	
						// msg has format '{"key1": value1, "key2": value2, ...}'
						var msgDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(msg);
						headRoll = float.Parse(msgDict["headRoll"], CultureInfo.InvariantCulture.NumberFormat);
						headYaw = float.Parse(msgDict["headYaw"], CultureInfo.InvariantCulture.NumberFormat);
						headPitch = float.Parse(msgDict["headPitch"], CultureInfo.InvariantCulture.NumberFormat);
						isTalking = bool.Parse(msgDict["isTalking"]);
						emotion = msgDict["emotion"];
					} 				
				} 			
			}         
		}         
		catch (SocketException socketException) {             
			Debug.Log("Socket exception: " + socketException);         
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