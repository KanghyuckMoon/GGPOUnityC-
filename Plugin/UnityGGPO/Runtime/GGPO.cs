using HouraiTeahouse.Backroll;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.UIElements;
using static HouraiTeahouse.Backroll.Sync;
using static HouraiTeahouse.Backroll.BackrollSessionCallbacks;
using System.Diagnostics;
using UnityEngine;

namespace UnityGGPO
{
	public class GGPONetworkStats
	{
		public int send_queue_len;
		public int recv_queue_len;
		public int ping;
		public int kbps_sent;
		public int local_frames_behind;
		public int remote_frames_behind;
	}
	public enum GGPOPlayerType
	{
		GGPO_PLAYERTYPE_LOCAL,
		GGPO_PLAYERTYPE_REMOTE,
		GGPO_PLAYERTYPE_SPECTATOR,
	}

	[Serializable]
	public struct GGPOPlayer
	{
		public GGPOPlayerType type;
		public int player_num;
		public string ip_address;
		public ushort port;

		public override string ToString()
		{
			return $"({type},{player_num},{ip_address},{port})";
		}
	}

	public static partial class GGPO
	{
		private const string libraryName = "UnityGGPO";

		public const int MAX_PLAYERS = 4;
		public const int MAX_PREDICTION_FRAMES = 8;
		public const int MAX_SPECTATORS = 32;

		public const int EVENTCODE_CONNECTED_TO_PEER = 1000;
		public const int EVENTCODE_SYNCHRONIZING_WITH_PEER = 1001;
		public const int EVENTCODE_SYNCHRONIZED_WITH_PEER = 1002;
		public const int EVENTCODE_RUNNING = 1003;
		public const int EVENTCODE_DISCONNECTED_FROM_PEER = 1004;
		public const int EVENTCODE_TIMESYNC = 1005;
		public const int EVENTCODE_CONNECTION_INTERRUPTED = 1006;
		public const int EVENTCODE_CONNECTION_RESUMED = 1007;

		public static bool SUCCEEDED(int result)
		{
			return result == GGPOERRORCODE.ERRORCODE_SUCCESS;
		}

		public static string GetErrorCodeMessage(int result)
		{
			switch (result)
			{
				case GGPOERRORCODE.ERRORCODE_SUCCESS:
					return "ERRORCODE_SUCCESS";

				case GGPOERRORCODE.ERRORCODE_GENERAL_FAILURE:
					return "ERRORCODE_GENERAL_FAILURE";

				case GGPOERRORCODE.ERRORCODE_INVALID_SESSION:
					return "ERRORCODE_INVALID_SESSION";

				case GGPOERRORCODE.ERRORCODE_INVALID_PLAYER_HANDLE:
					return "ERRORCODE_INVALID_PLAYER_HANDLE";

				case GGPOERRORCODE.ERRORCODE_PLAYER_OUT_OF_RANGE:
					return "ERRORCODE_PLAYER_OUT_OF_RANGE";

				case GGPOERRORCODE.ERRORCODE_PREDICTION_THRESHOLD:
					return "ERRORCODE_PREDICTION_THRESHOLD";

				case GGPOERRORCODE.ERRORCODE_UNSUPPORTED:
					return "ERRORCODE_UNSUPPORTED";

				case GGPOERRORCODE.ERRORCODE_NOT_SYNCHRONIZED:
					return "ERRORCODE_NOT_SYNCHRONIZED";

				case GGPOERRORCODE.ERRORCODE_IN_ROLLBACK:
					return "ERRORCODE_IN_ROLLBACK";

				case GGPOERRORCODE.ERRORCODE_INPUT_DROPPED:
					return "ERRORCODE_INPUT_DROPPED";

				case GGPOERRORCODE.ERRORCODE_PLAYER_DISCONNECTED:
					return "ERRORCODE_PLAYER_DISCONNECTED";

				case GGPOERRORCODE.ERRORCODE_TOO_MANY_SPECTATORS:
					return "ERRORCODE_TOO_MANY_SPECTATORS";

				case GGPOERRORCODE.ERRORCODE_INVALID_REQUEST:
					return "ERRORCODE_INVALID_REQUEST";

				default:
					return "INVALID_ERRORCODE";
			}
		}

		public static void UggSleep(int ms)
		{
			Thread.Sleep(ms);
		}

		public static int UggTimeGetTime()
		{
			return (int)BackrollTime.GetTime();
		}

		private static LogDelegate uggLogCallback;
		private static unsafe void UggSetLogDelegate(IntPtr callback)
		{
			uggLogCallback = Marshal.GetDelegateForFunctionPointer<LogDelegate>(callback);
		}

		//[DllImport(libraryName)]
		//public static extern int UggTestStartSession(out IntPtr session,
		//	IntPtr beginGame,
		//	IntPtr advanceFrame,
		//	IntPtr loadGameState,
		//	IntPtr logGameState,
		//	IntPtr saveGameState,
		//	IntPtr freeBuffer,
		//	IntPtr onEvent,
		//	string game, int num_players, int localport);

		public static unsafe int UggTestStartSession(out IntPtr session,
			IntPtr beginGame,
			IntPtr advanceFrame,
			IntPtr loadGameState,
			IntPtr logGameState,
			IntPtr saveGameState,
			IntPtr freeBuffer,
			IntPtr onEvent,
			string game, int num_players, int localport)
		{
			SyncTestsBackrollSession p2pSession;
			BackrollSessionCallbacks callbacks = new BackrollSessionCallbacks();
			//Callback Setting

			callbacks.begin_game = Marshal.GetDelegateForFunctionPointer<BeginGameDelegate>(beginGame);
			callbacks.advance_frame = Marshal.GetDelegateForFunctionPointer<AdvanceFrameDelegate>(advanceFrame);
			callbacks.load_game_state = Marshal.GetDelegateForFunctionPointer<LoadGameStateDelegate>(loadGameState);
			callbacks.log_game_state = Marshal.GetDelegateForFunctionPointer<LogGameStateDelegate>(logGameState);
			callbacks.save_game_state = Marshal.GetDelegateForFunctionPointer<SaveGameStateDelegate>(saveGameState);
			callbacks.free_buffer = Marshal.GetDelegateForFunctionPointer<FreeBufferDelegate>(freeBuffer);
			callbacks.on_event = Marshal.GetDelegateForFunctionPointer<OnEventDelegate>(onEvent);

			int result = Backroll.SyncStartSession(out p2pSession, ref callbacks, game, num_players, sizeof(long), (ushort)localport);
			session = PointerUtill.ToIntPtr(p2pSession);
			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static unsafe int UggStartSession(out IntPtr session,
			IntPtr beginGame,
			IntPtr advanceFrame,
			IntPtr loadGameState,
			IntPtr logGameState,
			IntPtr saveGameState,
			IntPtr freeBuffer,
			IntPtr onEvent,
			string game, int num_players, int localport)
		{
			
			P2PBackrollSession p2pSession;
			BackrollSessionCallbacks callbacks = new BackrollSessionCallbacks();
			//Callback Setting

			callbacks.begin_game = Marshal.GetDelegateForFunctionPointer<BeginGameDelegate>(beginGame);
			callbacks.advance_frame = Marshal.GetDelegateForFunctionPointer<AdvanceFrameDelegate>(advanceFrame);
			callbacks.load_game_state = Marshal.GetDelegateForFunctionPointer<LoadGameStateDelegate>(loadGameState);
			callbacks.log_game_state = Marshal.GetDelegateForFunctionPointer<LogGameStateDelegate>(logGameState);
			callbacks.save_game_state = Marshal.GetDelegateForFunctionPointer<SaveGameStateDelegate>(saveGameState);
			callbacks.free_buffer = Marshal.GetDelegateForFunctionPointer<FreeBufferDelegate>(freeBuffer);
			callbacks.on_event = Marshal.GetDelegateForFunctionPointer<OnEventDelegate>(onEvent);

			int result = Backroll.StartSession(out p2pSession, ref callbacks, game, num_players, sizeof(long), (ushort)localport);
			//GCHandle handle = GCHandle.Alloc(p2pSession);
			//, GCHandleType.Pinned
			session = PointerUtill.ToIntPtr(p2pSession); //GCHandle.ToIntPtr(handle);
			if(result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		//[DllImport(libraryName)]
		//private static extern int UggStartSpectating(out IntPtr session,
		//	IntPtr beginGame,
		//	IntPtr advanceFrame,
		//	IntPtr loadGameState,
		//	IntPtr logGameState,
		//	IntPtr saveGameState,
		//	IntPtr freeBuffer,
		//	IntPtr onEvent,
		//	string game, int num_players, int localport, string host_ip, int host_port);

		private static int UggSetDisconnectNotifyStart(IntPtr ggpo, int timeout)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.SetDisconnectNotifyStart(timeout);

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static int UggSetDisconnectTimeout(IntPtr ggpo, int timeout)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.SetDisconnectTimeout(timeout);

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static unsafe int UggSynchronizeInput(IntPtr ggpo, IntPtr inputs, int length, out int disconnect_flags)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);
			disconnect_flags = 0;
			int result = session.SyncInput(inputs.ToPointer(), sizeof(long) * length, ref disconnect_flags);


			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static int UggAddLocalInput(IntPtr ggpo, int local_player_handle, long input)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.AddLocalInput(new BackrollPlayerHandle { Id = local_player_handle }, ref input);

			//if (result != 0)
			//{
			//	UnityEngine.Debug.LogError("Error");
			//}
			return result;
		}

		private static int UggCloseSession(IntPtr ggpo)
		{
			Marshal.FreeHGlobal(ggpo);

			return GGPOERRORCODE.OK;
		}

		private static unsafe int UggIdle(IntPtr ggpo, int timeout)
		{
			int result = 0;

			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			result = session.DoPoll(timeout);

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}

			return result;
		}

		private static int UggAddPlayer(IntPtr ggpo, int player_type, int player_num, string player_ip_address, ushort player_port, ref BackrollPlayerHandle phandle)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			BackrollPlayer player = new BackrollPlayer();
			player.Player_id = player_num;
			player.port = player_port;
			player.ip = player_ip_address;
			player.Type = (BackrollPlayerType)player_type;

			int result = session.AddPlayer(ref player, ref phandle);

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static int UggDisconnectPlayer(IntPtr ggpo, int phandle)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.DisconnectPlayer(new BackrollPlayerHandle { Id = phandle });

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static unsafe int UggSetFrameDelay(IntPtr ggpo, int phandle, int frame_delay)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.SetFrameDelay(new BackrollPlayerHandle { Id = phandle }, frame_delay);

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static unsafe int UggAdvanceFrame(IntPtr ggpo)
		{
			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);

			int result = session.IncrementFrame();
			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		private static unsafe int UggGetNetworkStats(IntPtr ggpo, int phandle,
			out int send_queue_len,
			out int recv_queue_len,
			out int ping,
			out int kbps_sent,
			out int local_frames_behind,
			out int remote_frames_behind)
		{

			BackrollSession session = FromIntPtr<BackrollSession>(ggpo);
			BackrollNetworkStats stats = new BackrollNetworkStats();
			int result = session.GetNetworkStats(ref stats, new BackrollPlayerHandle { Id = phandle });
			send_queue_len = stats.send_queue_len;
			recv_queue_len = stats.ReceiveQueueLength;
			ping = stats.ping;
			kbps_sent = stats.kbps_sent;
			local_frames_behind = stats.local_frames_behind;
			remote_frames_behind = stats.remote_frames_behind;

			if (result != 0)
			{
				UnityEngine.Debug.LogError("Error");
			}
			return result;
		}

		// Access

		private static IntPtr _logDelegate;

		public static void SetLogDelegate(LogDelegate callback)
		{
			_logDelegate = callback != null ? Marshal.GetFunctionPointerForDelegate(callback) : IntPtr.Zero;
			UggSetLogDelegate(_logDelegate);
		}

		public static int StartSession(out IntPtr session,
				IntPtr beginGame,
				IntPtr advanceFrame,
				IntPtr loadGameState,
				IntPtr logGameState,
				IntPtr saveGameState,
				IntPtr freeBuffer,
				IntPtr onEvent,
				string game, int num_players, int localport)
		{
			return UggStartSession(out session, beginGame, advanceFrame, loadGameState, logGameState, saveGameState, freeBuffer, onEvent, game, num_players, localport);
		}

		public static int TestStartSession(out IntPtr session,
				IntPtr beginGame,
				IntPtr advanceFrame,
				IntPtr loadGameState,
				IntPtr logGameState,
				IntPtr saveGameState,
				IntPtr freeBuffer,
				IntPtr onEvent,
				string game, int num_players, int localport)
		{
			return UggTestStartSession(out session, beginGame, advanceFrame, loadGameState, logGameState, saveGameState, freeBuffer, onEvent, game, num_players, localport);
		}

		//public static int StartSpectating(out IntPtr session,
		//		IntPtr beginGame,
		//		IntPtr advanceFrame,
		//		IntPtr loadGameState,
		//		IntPtr logGameState,
		//		IntPtr saveGameState,
		//		IntPtr freeBuffer,
		//		IntPtr onEvent,
		//		string game, int num_players, int localport, string host_ip, int host_port)
		//{
		//	return UggStartSpectating(out session, beginGame, advanceFrame, loadGameState, logGameState, saveGameState, freeBuffer, onEvent, game, num_players, localport, host_ip, host_port);
		//}

		public static int SetDisconnectNotifyStart(IntPtr ggpo, int timeout)
		{
			return UggSetDisconnectNotifyStart(ggpo, timeout);
		}

		public static int SetDisconnectTimeout(IntPtr ggpo, int timeout)
		{
			return UggSetDisconnectTimeout(ggpo, timeout);
		}

		public static long[] SynchronizeInput(IntPtr ggpo, int length, out int disconnect_flags)
		{
			IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(long)) * length);
			var result = UggSynchronizeInput(ggpo, pnt, length, out disconnect_flags);
			var inputs = new long[length];
			Marshal.Copy(pnt, inputs, 0, length);
			Marshal.FreeHGlobal(pnt);
			if (!SUCCEEDED(result))
			{
				throw new Exception(GetErrorCodeMessage(result));
			}
			return inputs;
		}

		public static int AddLocalInput(IntPtr ggpo, int local_player_handle, long input)
		{
			return UggAddLocalInput(ggpo, local_player_handle, input);
		}

		public static int CloseSession(IntPtr ggpo)
		{
			return UggCloseSession(ggpo);
		}

		public static int Idle(IntPtr ggpo, int timeout)
		{
			return UggIdle(ggpo, timeout);
		}

		public static int AddPlayer(IntPtr ggpo, int player_type, int player_num, string player_ip_address, ushort player_port, ref BackrollPlayerHandle phandle)
		{
			return UggAddPlayer(ggpo, player_type, player_num, player_ip_address, player_port, ref phandle);
		}

		public static int DisconnectPlayer(IntPtr ggpo, int phandle)
		{
			return UggDisconnectPlayer(ggpo, phandle);
		}

		public static int SetFrameDelay(IntPtr ggpo, int phandle, int frame_delay)
		{
			return UggSetFrameDelay(ggpo, phandle, frame_delay);
		}

		public static int AdvanceFrame(IntPtr ggpo)
		{
			return UggAdvanceFrame(ggpo);
		}

		public static int GetNetworkStats(IntPtr ggpo, int phandle,
				out int send_queue_len,
				out int recv_queue_len,
				out int ping,
				out int kbps_sent,
				out int local_frames_behind,
				out int remote_frames_behind)
		{
			return UggGetNetworkStats(ggpo, phandle, out send_queue_len, out recv_queue_len, out ping, out kbps_sent, out local_frames_behind, out remote_frames_behind);
		}

		//public static int TestStartSession(out IntPtr session,
		//		IntPtr beginGame,
		//		IntPtr advanceFrame,
		//		IntPtr loadGameState,
		//		IntPtr logGameState,
		//		IntPtr saveGameState,
		//		IntPtr freeBuffer,
		//		IntPtr onEvent,
		//		string game, int num_players, int localport)
		//{
		//	return UggTestStartSession(out session, beginGame, advanceFrame, loadGameState, logGameState, saveGameState, freeBuffer, onEvent, game, num_players, localport);
		//}

		public static IntPtr ToIntPtr(object obj)
		{
			GCHandle handle = GCHandle.Alloc(obj, GCHandleType.WeakTrackResurrection);
			return GCHandle.ToIntPtr(handle);
		}

		public static T FromIntPtr<T>(IntPtr ptr) where T : class
		{
			GCHandle handle = GCHandle.FromIntPtr(ptr);
			return handle.Target as T;
		}
	}

}