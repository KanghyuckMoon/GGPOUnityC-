using System;
using System.Runtime.InteropServices;

namespace HouraiTeahouse.Backroll
{
	[Serializable]
	public struct SyncRequestMessage
	{
		public uint random_request;
		public byte RemoteEndpoint;
	}

}