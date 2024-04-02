using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using System.Net;

namespace HouraiTeahouse.Backroll
{
	public unsafe class P2PBackrollSession : BackrollSession, IPollSink, Udp.Callbacks
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

		BackrollSessionCallbacks _callbacks;
		Poll _poll = new Poll();
		Sync _sync;
		Udp _udp = new Udp();
		readonly UDPProtocol[] _endpoints;
		int _input_size;

		bool _synchronizing;
		int _num_players;
		int _next_recommended_sleep;
		
		int _disconnect_timeout;
		int _disconnect_notify_start;

		public BackrollConnectStatus[] _local_connect_status = new BackrollConnectStatus[UdpMsg.UDP_MSG_MAX_PLAYERS];

		public P2PBackrollSession(ref BackrollSessionCallbacks cb, string game, ushort localport, int num_players, int input_size)
		{
			_num_players = num_players;
			_input_size = input_size;
			_disconnect_timeout = DEFAULT_DISCONNECT_TIMEOUT;
			_disconnect_notify_start = DEFAULT_DISCONNECT_NOTIFY_START;
			_callbacks = cb;
			_synchronizing = true;
			_next_recommended_sleep = 0;

			Sync.Config config = new Sync.Config();
			config.num_players = num_players;
			config.input_size = input_size;
			config.callbacks = _callbacks;
			config.NumPredictionFrames = Sync.MAX_PREDICTION_FRAMES;
			_sync = new Sync(_local_connect_status, config);

			Udp.Callbacks call = this;
			_udp.Init(localport, ref _poll, ref call);

			_endpoints = new UDPProtocol[_num_players];
			for (int i = 0; i < _num_players; ++i)
			{
				_endpoints[i] = new UDPProtocol();
			}

			Array.Clear(_local_connect_status, 0, _local_connect_status.Length);

			for (int i = 0; i < _local_connect_status.Length; ++i)
			{
				_local_connect_status[i].last_frame = -1;
			}
		}

		public override int DoPoll(int timeout)
		{
			if(!_sync.InRollback())
			{
				_poll.Pump(0);

				PollUdpProtocolEvents();

				if (!_synchronizing)
				{
					_sync.CheckSimulation(timeout);

					int current_frame = _sync.GetFrameCount();

					for (int i = 0; i < _num_players; ++i)
					{
						_endpoints[i].SetLocalFrameNumber(current_frame);
					}

					int total_min_confirmed;
					if(_num_players <= UdpMsg.UDP_MSG_MAX_PLAYERS)
					{
						total_min_confirmed = Poll2Players(current_frame);
					}
					else
					{
						total_min_confirmed = PollNPlayers(current_frame);
					}

					//Debug.Log($"last confirmed frame in p2p backend is {total_min_confirmed}.\n");
					if (total_min_confirmed >= 0)
					{
						Debug.Assert(total_min_confirmed != 2147483647);
						_sync.SetLastConfirmedFrame(total_min_confirmed);
					}

					if (current_frame > _next_recommended_sleep)
					{
						int interval = 0;
						for (int i = 0; i < _num_players; ++i)
						{
							interval = Mathf.Max(interval, _endpoints[i].RecommendFrameDelay());
						}
					
						if (interval > 0)
						{
							//TimeSync
							OnEvent(EVENTCODE_TIMESYNC, interval, 0, 0);
							_next_recommended_sleep = current_frame + RECOMMENDATION_INTERVAL;
						}
					}

					if (timeout > 0)
					{
						Thread.Sleep(1);
					}
				}
			}

			return GGPOERRORCODE.OK;
		}

		public void OnEvent(int a, int b, int c, int d)
		{
			int[] eventData = new int[] { a, b, c, d };

			IntPtr evtPtr = Marshal.AllocHGlobal(eventData.Length * sizeof(int));
			Marshal.Copy(eventData, 0, evtPtr, eventData.Length);
			_callbacks.on_event?.Invoke(evtPtr);
		}

		public override int AddPlayer(ref BackrollPlayer player, ref BackrollPlayerHandle handle)
		{
			if (player.Type == BackrollPlayerType.Spectator)
			{
				//AddSpectator
				return GGPOERRORCODE.OK;
			}

			int queue = player.Player_id - 1;
			if(player.Player_id < 1 || player.Player_id > _num_players)
			{
				//Error
				return GGPOERRORCODE.ERRORCODE_PLAYER_OUT_OF_RANGE;
			}

			handle = QueueToPlayerHandle(queue);

			if (player.Type == BackrollPlayerType.Remote)
			{
				AddRemotePlayer(player.ip, player.port, queue);
			}

			return GGPOERRORCODE.OK;
		}
		public override int AddLocalInput(BackrollPlayerHandle player, ref long playerInput)
		{
			int queue = 0;
			GameInput input;
			int errorResult;

			if (_sync.InRollback())
			{
				return GGPOERRORCODE.ERRORCODE_IN_ROLLBACK;
			}
			if (_synchronizing)
			{
				return GGPOERRORCODE.ERRORCODE_NOT_SYNCHRONIZED;
			}

			errorResult = PlayerHandleToQueue(player, ref queue);
			if(!GGPOERRORCODE.ERROR_SUCCEEDED(errorResult))
			{
				return errorResult;
			}

			input = GameInput.Create(GameInput.NullFrame, ref playerInput);

			if (!_sync.AddLocalInput(queue, ref input))
			{
				return GGPOERRORCODE.ERRORCODE_PREDICTION_THRESHOLD;
			}

			if (input.frame != GameInput.NullFrame)
			{
				//Debug.Log($"setting local connect status for local queue {queue} to {input.frame}");
				_local_connect_status[queue].last_frame = input.frame;

				for (int i = 0; i < _num_players; i++)
				{
					if (_endpoints[i].IsInitialized())
					{
						_endpoints[i].SendInput(ref input);
					}
				}
			}

			return GGPOERRORCODE.OK;
		}
		public override int SyncInput(void* values, int size, ref int disconnect_flags)
		{
			int flags;
			if (_synchronizing)
			{
				return GGPOERRORCODE.ERRORCODE_NOT_SYNCHRONIZED;
			}

			flags = _sync.SynchronizeInputs(values, size);

			disconnect_flags = flags;

			return GGPOERRORCODE.OK;
		}
		public override int IncrementFrame()
		{
			//Debug.Log($"End of frame ({_sync._framecount})...");
			_sync.IncrementFrame();
			DoPoll(0);

			return GGPOERRORCODE.OK;
		}
		public override int DisconnectPlayer(BackrollPlayerHandle player)
		{
			int queue = 0;
			int errorResult;
			errorResult = PlayerHandleToQueue(player, ref queue);
			if (!GGPOERRORCODE.ERROR_SUCCEEDED(errorResult))
			{
				return errorResult;
			}

			if (_local_connect_status[queue].disconnected)
			{
				return GGPOERRORCODE.ERRORCODE_PLAYER_DISCONNECTED;
			}

			if (!_endpoints[queue].IsInitialized())
			{
				int current_frame = _sync.GetFrameCount();
				//Debug.Log($"Disconnecting local player {queue} at frame {_local_connect_status[queue].last_frame} by user request.");
				for (int i = 0; i < _num_players; i++)
				{
					if (_endpoints[i].IsInitialized())
					{
						DisconnectPlayerQueue(i, current_frame);
					}
				}
			}
			else
			{
				//Debug.LogFormat($"Disconnecting queue {queue} at frame {_local_connect_status[queue].last_frame} by user request.");
				DisconnectPlayerQueue(queue, _local_connect_status[queue].last_frame);
			}
			return GGPOERRORCODE.OK;
		}
		public override int GetNetworkStats(ref BackrollNetworkStats stats, BackrollPlayerHandle player)
		{
			int queue = 0;
			int errorResult;

			errorResult = PlayerHandleToQueue(player, ref queue);
			if (!GGPOERRORCODE.ERROR_SUCCEEDED(errorResult))
			{
				return errorResult;
			}

			unsafe
			{
				UnsafeUtility.MemSet(UnsafeUtility.AddressOf(ref stats), 0, UnsafeUtility.SizeOf<BackrollNetworkStats>());
			}

			_endpoints[queue].GetNetworkStats(ref stats);

			return GGPOERRORCODE.OK;
		}
		public override int SetFrameDelay(BackrollPlayerHandle player, int delay)
		{
			int queue = 0;
			int errorResult;
			errorResult = PlayerHandleToQueue(player, ref queue);

			if(!GGPOERRORCODE.ERROR_SUCCEEDED(errorResult))
			{
				return errorResult;
			}

			_sync.SetFrameDelay(queue, delay);
			return GGPOERRORCODE.OK;
		}
		public override int SetDisconnectTimeout(int timeout)
		{
			_disconnect_timeout = timeout;
			for(int i = 0; i < _num_players; ++i)
			{
				if (_endpoints[i].IsInitialized())
				{
					_endpoints[i].SetDisconnectTimeout((uint)timeout);
				}
			}
			return GGPOERRORCODE.OK;
		}
		public override int SetDisconnectNotifyStart(int timeout)
		{
			_disconnect_notify_start = timeout;

			for (int i = 0; i < _num_players; ++i)
			{
				if (_endpoints[i].IsInitialized())
				{
					_endpoints[i].SetDisconnectNotifyStart((uint)_disconnect_notify_start);
				}
			}
			return GGPOERRORCODE.OK;
		}

		public unsafe void OnMsg(ref IPEndPoint from, ref UdpMsg msg, int len)
		{
			for (int i = 0; i < _num_players; ++i)
			{
				if (_endpoints[i].HandlesMsg(ref from, ref msg))
				{
					_endpoints[i].OnMsg(ref msg, len);
					return;
				}
			}
		}

		protected int PlayerHandleToQueue(BackrollPlayerHandle player, ref int queue)
		{
			int offset = ((int)player.Id - 1);
			if (offset < 0 || offset >= _num_players)
			{
				return GGPOERRORCODE.ERRORCODE_INVALID_PLAYER_HANDLE;
			}
			queue = offset;
			return GGPOERRORCODE.OK;
		}
		protected BackrollPlayerHandle QueueToPlayerHandle(int queue)
		{
			return new BackrollPlayerHandle { Id = queue + 1 };
		}
		//QueueToSpectatorHandle
		protected void DisconnectPlayerQueue(int queue, int syncto)
		{
			int framecount = _sync.GetFrameCount();

			_endpoints[queue].Disconnect();

			//Debug.LogFormat($"Changing queue {queue} local connect status for last frame from {_local_connect_status[queue].last_frame} to {syncto} on disconnect request (current: {framecount}).");

			_local_connect_status[queue].disconnected = true;
			_local_connect_status[queue].last_frame = syncto;

			if (syncto < framecount)
			{
				Debug.Log($"adjusting simulation to account for the fact that {queue} Disconnected @ {syncto}.");
				_sync.AdjustSimulation(syncto);
				//Debug.Log($"finished adjusting simulation.");
			}

			//Disconnected
			Debug.Log($"OnEvent Dis");
			OnEvent(EVENTCODE_DISCONNECTED_FROM_PEER, QueueToPlayerHandle(queue).Id, 0, 0);
			CheckInitialSync();
		}
		void PollUdpProtocolEvents()
		{
			UDPProtocol.Event evt = new UDPProtocol.Event();
			for (int i = 0; i < _num_players; i++)
			{
				while (_endpoints[i].GetEvent(ref evt))
				{
					OnUdpProtocolPeerEvent(ref evt, i);
				}
			}
		}
		void CheckInitialSync()
		{
			int i;
			if (_synchronizing)
			{
				for (i = 0; i < _num_players; i++)
				{
					if (_endpoints[i].IsInitialized() && !_endpoints[i].IsSynchronized() && !_local_connect_status[i].disconnected)
					{
						return;
					}
				}
			}

			OnEvent(EVENTCODE_RUNNING, 0, 0, 0);
			_synchronizing = false;
		}
		protected int Poll2Players(int current_frame)
		{
			int i = 0;
			int total_min_confirmed = int.MaxValue;
			for (i = 0; i < _num_players; i++)
			{
				bool queue_connected = true;
				if (_endpoints[i].IsRunning())
				{
					int ignore = 0;
					queue_connected = _endpoints[i].GetPeerConnectStatus(i, ref ignore);
				}
				if (!_local_connect_status[i].disconnected)
				{
					total_min_confirmed = Math.Min(_local_connect_status[i].last_frame, total_min_confirmed);
				}
				//Debug.Log($"local endp: connected = {!_local_connect_status[i].disconnected}, last_received = {_local_connect_status[i].last_frame}, minFrame = {total_min_confirmed}.");
				if (!queue_connected && !_local_connect_status[i].disconnected)
				{
					//Debug.Log($"disconnecting i {i} by remote request.");
					DisconnectPlayerQueue(i, total_min_confirmed);
				}
				//Debug.Log($"minFrame = {total_min_confirmed}.");
			}
			return total_min_confirmed;
		}
		protected int PollNPlayers(int current_frame)
		{
			int i, queue, last_received = 0;

			// discard confirmed frames as appropriate
			int total_min_confirmed = Int32.MaxValue;
			for (queue = 0; queue < _num_players; queue++)
			{
				bool queue_connected = true;
				int queue_min_confirmed = Int32.MaxValue;
				//Debug.Log($"considering queue {queue}.");
				for (i = 0; i < _num_players; i++)
				{
					if (_endpoints[i].IsRunning())
					{
						bool connected = _endpoints[i].GetPeerConnectStatus(queue, ref last_received);
						queue_connected = queue_connected && connected;
						queue_min_confirmed = Mathf.Min(last_received, queue_min_confirmed);
						//Debug.Log($"endpoint {i}: connected = {queue_connected}, last_received = {last_received}, minConfirmed = {queue_min_confirmed}.");
					}
					else
					{
						//Debug.Log($"endpoint {i}: ignoring... not running.");
					}
				}
				if (!_local_connect_status[queue].disconnected)
				{
					queue_min_confirmed = (int)Math.Min(_local_connect_status[queue].last_frame, queue_min_confirmed);
				}
				//Debug.Log($"local endp: connected = {!_local_connect_status[queue].disconnected}, last_received = {_local_connect_status[queue].last_frame}, minConfirmed = {queue_min_confirmed}.");

				if (queue_connected)
				{
					total_min_confirmed = Math.Min(queue_min_confirmed, total_min_confirmed);
				}
				else
				{
					if (!_local_connect_status[queue].disconnected || _local_connect_status[queue].last_frame > queue_min_confirmed)
					{
						//Debug.Log($"disconnecting queue {queue} by remote request.");
						DisconnectPlayerQueue(queue, queue_min_confirmed);
					}
				}
				//Debug.Log($"minFrame = {total_min_confirmed}.");
			}
			return total_min_confirmed;
		}
		protected void AddRemotePlayer(string remoteIp, ushort reportport, int queue)
		{
			_synchronizing = true;
			IntPtr intPtr = IntPtr.Zero;
			PointerUtill.ToIntPtrStructArray(_local_connect_status, out intPtr);

			_endpoints[queue].Init(ref _udp, ref _poll, queue, remoteIp, reportport, intPtr);
			_endpoints[queue].SetDisconnectTimeout((uint)_disconnect_timeout);
			_endpoints[queue].SetDisconnectNotifyStart((uint)_disconnect_notify_start);
			_endpoints[queue].Synchronize();
		}
		void OnUdpProtocolEvent(ref UDPProtocol.Event evt, BackrollPlayerHandle handle)
		{
			switch (evt.type)
			{
				case UDPProtocol.Event.Type.Connected:
					OnEvent(EVENTCODE_CONNECTED_TO_PEER, handle.Id, 0, 0);
					break;
				case UDPProtocol.Event.Type.Synchronizing:
					OnEvent(EVENTCODE_SYNCHRONIZING_WITH_PEER, handle.Id, evt.synchronizing.count, evt.synchronizing.total);
					break;
				case UDPProtocol.Event.Type.Synchronzied:
					OnEvent(EVENTCODE_SYNCHRONIZED_WITH_PEER, handle.Id, 0, 0);
					CheckInitialSync();
					break;

				case UDPProtocol.Event.Type.NetworkInterrupted:
					OnEvent(EVENTCODE_CONNECTION_INTERRUPTED, handle.Id, evt.network_interrupted.disconnect_timeout, 0);
					break;

				case UDPProtocol.Event.Type.NetworkResumed:
					OnEvent(EVENTCODE_CONNECTION_RESUMED, handle.Id, 0, 0);
					break;
			}
		}
		void OnUdpProtocolPeerEvent(ref UDPProtocol.Event evt, int queue)
		{
			OnUdpProtocolEvent(ref evt, QueueToPlayerHandle(queue));
			switch (evt.type)
			{
				case UDPProtocol.Event.Type.Input:
					if (!_local_connect_status[queue].disconnected)
					{
						int current_remote_frame = _local_connect_status[queue].last_frame;
						int new_remote_frame = evt.input.input.frame;
						Debug.Assert(current_remote_frame == -1 || new_remote_frame == (current_remote_frame + 1));

						_sync.AddRemoteInput(queue, ref evt.input.input);

						//Debug.Log($"setting remote connect status for queue {queue} to {evt.input.input.frame}\n");
						_local_connect_status[queue].last_frame = evt.input.input.frame;
					}
					break;

				case UDPProtocol.Event.Type.Disconnected:
					DisconnectPlayer(QueueToPlayerHandle(queue));
					break;
			}
		}
	}

}


