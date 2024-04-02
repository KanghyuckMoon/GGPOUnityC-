using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Match
{
	public static class Match
	{
		public static void AddOriginal(int frame, string original)
		{
			MatchIndicator.misMatchFrames.Find(x => x.frame == frame).original = original;
		}
		public static void AddReply(int frame, string reply)
		{
			try
			{
				MatchIndicator.misMatchFrames.Find(x => x.frame == frame).reply = reply;
			}
			catch
			{
				MatchIndicator.misMatchFrames.Add(new MatchIndicator.MisMatchFrame() { frame = frame });
				MatchIndicator.misMatchFrames.Find(x => x.frame == frame).reply = reply;
			}
		}
	}
}
