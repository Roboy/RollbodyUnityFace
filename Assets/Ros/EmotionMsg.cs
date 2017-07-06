using System;

namespace Ros
{
	public class EmotionMsg : IRosMessage
	{
		public HeaderMsg header;
		public string emotion;

		public EmotionMsg()
		{
			header = new HeaderMsg ();
            emotion = "";
		}

		public string MessageType ()
		{
			return "roboy_communication_control/Emotion";
		}
	}
}

