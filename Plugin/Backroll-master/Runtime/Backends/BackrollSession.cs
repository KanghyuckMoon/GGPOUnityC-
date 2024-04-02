using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace HouraiTeahouse.Backroll
{

	public static class Backroll
	{
		public static int StartSession(out P2PBackrollSession session, ref BackrollSessionCallbacks cb, string game, int num_players, int input_size, ushort localport)
		{
			session = new P2PBackrollSession(ref cb, game, localport, num_players, input_size);
			return GGPOERRORCODE.OK;
		}
		public static int SyncStartSession(out SyncTestsBackrollSession session, ref BackrollSessionCallbacks cb, string game, int num_players, int input_size, ushort localport)
		{
			int checkFrameDistance = 8;
			session = new SyncTestsBackrollSession(ref cb, game, num_players, checkFrameDistance);
			return GGPOERRORCODE.OK;
		}

	}

	public abstract class BackrollSession
	{
		public abstract int DoPoll(int timeout);
		public abstract int AddPlayer(ref BackrollPlayer player, ref BackrollPlayerHandle handle);
		public abstract int AddLocalInput(BackrollPlayerHandle player, ref long input);
		public unsafe abstract int SyncInput(void* values, int size, ref int disconnect_flags);
		public abstract int IncrementFrame();
		//Chat
		public abstract int DisconnectPlayer(BackrollPlayerHandle player);
		public abstract int GetNetworkStats(ref BackrollNetworkStats stats, BackrollPlayerHandle player);
		//Logv

		public abstract int SetFrameDelay(BackrollPlayerHandle player, int frame_delay);
		public abstract int SetDisconnectTimeout(int timeout);
		public abstract int SetDisconnectNotifyStart(int timeout);
	}

}
