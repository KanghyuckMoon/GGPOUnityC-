using System;
using System.Net;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Match;

namespace HouraiTeahouse.Backroll
{

	public unsafe class SyncTestsBackrollSession : BackrollSession, IPollSink, Udp.Callbacks
	{
		public const int EVENTCODE_CONNECTED_TO_PEER = 1000;
		public const int EVENTCODE_SYNCHRONIZING_WITH_PEER = 1001;
		public const int EVENTCODE_SYNCHRONIZED_WITH_PEER = 1002;
		public const int EVENTCODE_RUNNING = 1003;
		public const int EVENTCODE_DISCONNECTED_FROM_PEER = 1004;
		public const int EVENTCODE_TIMESYNC = 1005;
		public const int EVENTCODE_CONNECTION_INTERRUPTED = 1006;
		public const int EVENTCODE_CONNECTION_RESUMED = 1007;

		const int RECOMMENDATION_INTERVAL = 240;
		const int DEFAULT_DISCONNECT_TIMEOUT = 5000;
		const int DEFAULT_DISCONNECT_NOTIFY_START = 750;

		public unsafe struct SavedInfo
		{
			public int Frame;
			public int Checksum;
			public byte* Buffer;
			public int Size;
			public GameInput Input;
		};

		readonly BackrollSessionCallbacks _callbacks;
		readonly Sync _sync;
		int _num_players;
		int _check_distance;
		int _last_verified;
		bool _rollingback;
		bool _running;

		GameInput _current_input;
		GameInput _last_input;
		public RingBuffer<SavedInfo> _saved_frames = new RingBuffer<SavedInfo>(32);

		public SyncTestsBackrollSession(ref BackrollSessionCallbacks cb, string game, int num_players, int frames)
		{
			_callbacks = cb;
			_num_players = num_players;
			_check_distance = frames;
			_last_verified = 0;
			_rollingback = false;
			_running = false;
			_current_input.erase();

			Sync.Config config = new Sync.Config();
			config.NumPredictionFrames = BackrollConstants.kMaxPredictionFrames;
			config.callbacks = cb;

			_sync = new Sync(null, config);

			cb.begin_game.Invoke(game);
		}

		public override int DoPoll(int timeout)
		{
			if (_running) return 0;

			OnEvent(EVENTCODE_RUNNING, 0, 0, 0);
			_running = true;
			return 0;
		}

		public override int AddPlayer(ref BackrollPlayer player, ref BackrollPlayerHandle handle)
		{
			if (player.Player_id < 1 || player.Player_id > _num_players)
			{
				return GGPOERRORCODE.ERRORCODE_PLAYER_OUT_OF_RANGE;
			}
			handle = new BackrollPlayerHandle() { Id = player.Player_id - 1 };
			return GGPOERRORCODE.OK;
		}

		public override int AddLocalInput(BackrollPlayerHandle player, ref long input)
		{
			if (!_running)
			{
				return GGPOERRORCODE.ERRORCODE_NOT_SYNCHRONIZED;
			}

			int index = player.Id;
			int size = sizeof(long);

			var bytes = BitConverter.GetBytes(input);

			for (int i = 0; i < size; i++)
			{
				_current_input.bits[(index * size) + i] = bytes[i];
			}
			return GGPOERRORCODE.OK;
		}

		public override int SyncInput(void* values, int size, ref int disconnect_flags)
		{
			if (_rollingback)
			{
				_last_input = _saved_frames.Peek().Input;
			}
			else
			{
				if (_sync._framecount == 0)
				{
					_sync.SaveCurrentFrame();
				}
				_last_input = _current_input;
			}
			fixed (byte* ptr = _last_input.bits)
			{
				UnsafeUtility.MemCpy(values, ptr, size);
			}
			return 0;
		}

		public override int IncrementFrame()
		{
			_sync.IncrementFrame();
			_current_input.erase();

			//Debug.Log($"End of frame({_sync._framecount})...");

			if (_rollingback) return 0;

			int frame = _sync._framecount;
			// Hold onto the current frame in our queue of saved states.  We'll need
			// the Checksum later to verify that our replay of the same frame got the
			// same results.
			var info = new SavedInfo
			{
				Frame = frame,
				Input = _last_input,
				Size = _sync.GetLastSavedFrame().cbuf,
				Buffer = (byte*)UnsafeUtility.Malloc(_sync.GetLastSavedFrame().cbuf,
												  UnsafeUtility.AlignOf<byte>(),
												  Allocator.Temp),
				Checksum = _sync.GetLastSavedFrame().checksum,
			};
			UnsafeUtility.MemCpy(info.Buffer, _sync.GetLastSavedFrame().buf, info.Size);
			_saved_frames.Push(info);

			if (frame - _last_verified == _check_distance)
			{
				// We've gone far enough ahead and should now start replaying frames.
				// Load the last verified frame and set the rollback flag to true.
				_sync.LoadFrame(_last_verified);

				_rollingback = true;
				while (!_saved_frames.IsEmpty)
				{
					_callbacks.advance_frame.Invoke(0);

					// Verify that the Checksumn of this frame is the same as the one in our
					// list.
					info = _saved_frames.Peek();
					_saved_frames.Pop();

					//Debug.Log($"Process {info.Frame}");

					if (info.Frame != _sync._framecount)
					{
						Debug.LogWarning($"SyncTest: Frame number {info.Frame} does not match saved frame number {frame}");
					}
					int Checksum = _sync.GetLastSavedFrame().checksum;
					
					if(info.Frame != _sync.GetLastSavedFrame().frame)
					{
						Debug.LogWarning($"SyncTest: Sync Frame number {info.Frame} does not match saved frame number {_sync.GetLastSavedFrame().frame}");
					}

					if (info.Checksum != Checksum)
					{
						MatchIndicator.misMatchFrames.Add(new MatchIndicator.MisMatchFrame() { frame = frame });
						if (frame != _sync._framecount)
						{
							var exists = MatchIndicator.misMatchFrames.Find(item => item.frame == _sync._framecount);
							if(exists != null)
							{
								MatchIndicator.misMatchFrames.Add(new MatchIndicator.MisMatchFrame() {frame = _sync._framecount });
							}
						}
						_callbacks.log_game_state?.Invoke($"Original_f{_sync._framecount}", info.Buffer, info.Size);
						_callbacks.log_game_state?.Invoke($"Replay_f{_sync._framecount}", _sync.GetLastSavedFrame().buf, _sync.GetLastSavedFrame().cbuf);
						Debug.LogWarning($"SyncTest: Checksum for frame {frame} does not match saved ({info.Checksum} != {Checksum})");
						MatchIndicator.isMatch = false;
					}
					else
					{
						MatchIndicator.isMatch = true;
					}
					UnsafeUtility.Free(info.Buffer, Allocator.Temp);
				}
				_last_verified = frame;
				_rollingback = false;
			}
			return 0;
		}

		public override int DisconnectPlayer(BackrollPlayerHandle player)
		{
			return GGPOERRORCODE.ERRORCODE_UNSUPPORTED;
		}

		public override int GetNetworkStats(ref BackrollNetworkStats stats, BackrollPlayerHandle player)
		{
			return GGPOERRORCODE.ERRORCODE_UNSUPPORTED;
		}

		public override int SetFrameDelay(BackrollPlayerHandle player, int Frame_delay)
		{
			return GGPOERRORCODE.ERRORCODE_UNSUPPORTED;
		}

		public override int SetDisconnectNotifyStart(int timeout)
		{
			return GGPOERRORCODE.ERRORCODE_UNSUPPORTED;
		}

		public override int SetDisconnectTimeout(int timeout)
		{
			return GGPOERRORCODE.ERRORCODE_UNSUPPORTED;
		}


		public void OnEvent(int a, int b, int c, int d)
		{
			int[] eventData = new int[] { a, b, c, d };

			IntPtr evtPtr = Marshal.AllocHGlobal(eventData.Length * sizeof(int));
			Marshal.Copy(eventData, 0, evtPtr, eventData.Length);
			_callbacks.on_event?.Invoke(evtPtr);
		}

		public void OnMsg(ref IPEndPoint from, ref UdpMsg msg, int len)
		{
			throw new NotImplementedException();
		}
	}

}