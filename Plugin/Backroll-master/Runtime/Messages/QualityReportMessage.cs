using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HouraiTeahouse.Backroll
{

	[Serializable]
	public struct QualityReportMessage
	{
		public byte frame_advantage;
		public uint ping;
	}

}