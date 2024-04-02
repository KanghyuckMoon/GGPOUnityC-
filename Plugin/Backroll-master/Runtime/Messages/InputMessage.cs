using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Runtime.InteropServices;

namespace HouraiTeahouse.Backroll
{

	[Serializable]
	public unsafe struct InputMessage 
	{

		public const int kMaxCompressedBits = 4096;

		public fixed bool disconnected[BackrollConstants.kMaxPlayers];
		public fixed int last_frame[BackrollConstants.kMaxPlayers];

		public uint start_frame;

		public bool disconnect_requested;
		public int ack_frame;

		public ushort num_bits;
		public byte input_size; 
		public fixed byte bits[kMaxCompressedBits / 8];


		public BackrollConnectStatus this[int index]
		{
			get
			{
				return new BackrollConnectStatus() { last_frame = last_frame[index], disconnected = disconnected[index] };
			}
			set
			{
				disconnected[index] = value.disconnected;
				last_frame[index] = value.last_frame;
			}
		}
	}
}