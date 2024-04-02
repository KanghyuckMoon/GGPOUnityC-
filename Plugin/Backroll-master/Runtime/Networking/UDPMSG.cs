using HouraiTeahouse.Backroll;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HouraiTeahouse.Backroll
{

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UdpMsg
	{
		public enum MsgType
		{
			Invalid = 0,
			SyncRequest = 1,
			SyncReply = 2,
			Input = 3,
			QualityReport = 4,
			QualityReply = 5,
			KeepAlive = 6,
			InputAck = 7,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct Header
		{
			public ushort magic;
			public ushort sequence_number;
			public byte type; // packet type
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct U
		{
			[FieldOffset(0)]
			public SyncRequestMessage sync_request; //5
			[FieldOffset(0)]
			public SyncReplyMessage sync_reply; //4
			[FieldOffset(0)]
			public QualityReportMessage quality_report; //5
			[FieldOffset(0)]
			public QualityReplyMessage quality_reply; //4
			[FieldOffset(0)]
			public InputMessage input;
			[FieldOffset(0)]
			public InputAckMessage input_ack; //4
		}

		public Header hdr;
		public U u;

		public const int MAX_COMPRESSED_BITS = 4096;
		public const int UDP_MSG_MAX_PLAYERS = 4;

		public static UdpMsg ByteArrayToUdpMsg(byte[] bytes)
		{
			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			try
			{
				IntPtr ptr = handle.AddrOfPinnedObject();
				return (UdpMsg)Marshal.PtrToStructure(ptr, typeof(UdpMsg));
			}
			finally
			{
				if (handle.IsAllocated)
					handle.Free();
			}
		}
		public byte[] PacketData()
		{
			int size = Marshal.SizeOf(typeof(UdpMsg));

			IntPtr ptr = Marshal.AllocHGlobal(size);

			try
			{
				Marshal.StructureToPtr(this, ptr, false);

				byte[] byteArray = new byte[size];

				Marshal.Copy(ptr, byteArray, 0, size);

				return byteArray;
			}
			finally
			{
				Marshal.FreeHGlobal(ptr);
			}
		}

		public UdpMsg(MsgType t)
		{
			hdr = new Header();
			u = new U();
			hdr.type = (byte)t;
		}
	}
}