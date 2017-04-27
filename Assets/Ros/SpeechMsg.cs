using System;

namespace Ros
{
	public class SpeechMsg : IRosMessage
	{
		public HeaderMsg header;
		public string phoneme;
		public double duration;

		public SpeechMsg ()
		{
			header = new HeaderMsg ();
			phoneme = "sil";
			duration = 0;
		}

		public string MessageType ()
		{
			return "speech_synthesis_msgs/SpeechSynthMsg";
		}
	}
}

