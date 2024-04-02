using System;
using System.Net;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace HouraiTeahouse.Backroll
{
	[Serializable]
	public struct BackrollConnectStatus
	{
		public bool disconnected;
		public int last_frame;
	}

	public unsafe class UDPProtocol : IPollSink
	{
		[StructLayout(LayoutKind.Explicit, Size = 12)]
		public struct ConnectionState
		{
			public struct SyncState
			{
				public uint roundtrips_remaining;
				public uint random;
			}
			public struct RunningState
			{
				public uint last_quality_report_time;
				public uint last_network_stats_interval;
				public uint last_input_packet_recv_time;
			}
			[FieldOffset(0)] public SyncState sync;
			[FieldOffset(0)] public RunningState running;
		}

		const int UDP_HEADER_SIZE = 28;     
		const int NUM_SYNC_PACKETS = 5;
		const int SYNC_RETRY_INTERVAL = 2000;
		const int SYNC_FIRST_RETRY_INTERVAL = 500;
		const int RUNNING_RETRY_INTERVAL = 200;
		const int KEEP_ALIVE_INTERVAL = 200;
		const int QUALITY_REPORT_INTERVAL = 1000;
		const int NETWORK_STATS_INTERVAL = 1000;
		const int UDP_SHUTDOWN_TIMER = 5000;
		const int MAX_SEQ_DISTANCE = (1 << 15);

		[StructLayout(LayoutKind.Explicit)]
		public struct Event
		{
			[FieldOffset(0)]
			public Type type;

			[FieldOffset(4)]
			public InputEvent input;

			[FieldOffset(4)]
			public SynchronizingEvent synchronizing;

			[FieldOffset(4)]
			public NetworkInterruptedEvent network_interrupted;

			public enum Type
			{
				Unknown = -1,
				Connected,
				Synchronizing,
				Synchronzied,
				Input,
				Disconnected,
				NetworkInterrupted,
				NetworkResumed,
			};

			public Event(Type t = Type.Unknown)
			{
				type = t;
				input = default;
				synchronizing = default;
				network_interrupted = default;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct InputEvent
			{
				public GameInput input;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct SynchronizingEvent
			{
				public int total;
				public int count;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct NetworkInterruptedEvent
			{
				public int disconnect_timeout;
			}
		}

		delegate bool DispatchFn(ref UdpMsg msg, int len);

		public unsafe bool OnLoopPoll(void* cookie)
		{
			if (_udp == null)
			{
				return true;
			}

			uint now = BackrollTime.GetTime();
			uint next_interval;

			PumpSendQueue();
			switch (_current_state)
			{
				case State.Syncing:
					next_interval = (_state.sync.roundtrips_remaining == NUM_SYNC_PACKETS) ? (uint)SYNC_FIRST_RETRY_INTERVAL : (uint)SYNC_RETRY_INTERVAL;
					if (_last_send_time > 0 && _last_send_time + next_interval < now)
					{
						//Debug.Log($"No luck syncing after {next_interval} ms... Re-queueing sync packet.\n");
						SendSyncRequest();
					}
					break;

				case State.Running:
					if (_state.running.last_input_packet_recv_time <= 0 || _state.running.last_input_packet_recv_time + RUNNING_RETRY_INTERVAL < now)
					{
						//Debug.Log($"Haven't exchanged packets in a while (last received:{_last_received_input.frame}  last sent:{_last_sent_input.frame}).  Resending.\n");
						SendPendingOutput();
						_state.running.last_input_packet_recv_time = now;
					}

					if (_state.running.last_quality_report_time == 0 || _state.running.last_quality_report_time + QUALITY_REPORT_INTERVAL < now)
					{
						UdpMsg msg = new UdpMsg(UdpMsg.MsgType.QualityReport);
						msg.u.quality_report.ping = BackrollTime.GetTime();
						msg.u.quality_report.frame_advantage = (byte)_local_frame_advantage;
						SendMsg(ref msg);
						_state.running.last_quality_report_time = now;
					}

					if (_state.running.last_network_stats_interval <= 0 || _state.running.last_network_stats_interval + NETWORK_STATS_INTERVAL < now)
					{
						_state.running.last_network_stats_interval = now;
					}

					if (_last_send_time > 0 && _last_send_time + KEEP_ALIVE_INTERVAL < now)
					{
						//Debug.Log("Sending keep alive packet\n");
						UdpMsg msg = new UdpMsg(UdpMsg.MsgType.KeepAlive);
						SendMsg(ref msg);
					}

					if (_disconnect_timeout > 0 && _disconnect_notify_start > 0 &&
					   !_disconnect_notify_sent && (_last_recv_time + _disconnect_notify_start < now))
					{
						//Debug.Log($"Endpoint has stopped receiving packets for {_disconnect_notify_start} ms.  Sending notification.\n");
						Event e = new Event(Event.Type.NetworkInterrupted);
						e.network_interrupted.disconnect_timeout = (int)_disconnect_timeout - (int)_disconnect_notify_start;
						QueueEvent(e);
						_disconnect_notify_sent = true;
					}

					if (_disconnect_timeout > 0 && (_last_recv_time + _disconnect_timeout < now))
					{
						if (!_disconnect_event_sent)
						{
							//Debug.Log($"Endpoint has stopped receiving packets for {_disconnect_timeout} ms.  Disconnecting.\n");
							QueueEvent(new Event(Event.Type.Disconnected));
							_disconnect_event_sent = true;
						}
					}
					break;

				case State.Disconnected:
					if (_shutdown_timeout < now)
					{
						//Debug.Log("Shutting down udp connection.\n");
						_udp = null;
						_shutdown_timeout = 0;
					}
					break;
			}



			return true;
		}

		public UDPProtocol()
		{
			_local_frame_advantage = 0;
			_remote_frame_advantage = 0;
			_queue = -1;
			_magic_number = 0;
			_remote_magic_number = 0;
			_last_send_time = 0;
			_shutdown_timeout = 0;
			_disconnect_timeout = 0;
			_disconnect_notify_start = 0;
			_disconnect_notify_sent = false;
			_disconnect_event_sent = false;
			_connected = false;
			_next_recv_seq = 0;
			_next_recv_seq = 0;
			_udp = null;

			_last_sent_input = new GameInput(-1, null, 1);
			_last_received_input = new GameInput(-1, null, 1);
			_last_acked_input = new GameInput(-1, null, 1);

			_state = new ConnectionState();
			Array.Clear(_peer_connect_status, 0, _peer_connect_status.Length);
			for (int i = 0; i < _peer_connect_status.Length; ++i)
			{
				_peer_connect_status[i].last_frame = -1;
			}
			_peer_addr = new IPEndPoint(IPAddress.Any, 0);
			_oo_packet.msg = IntPtr.Zero;

			_send_latency = GetConfigInt("ggpo.network.delay");
			_oop_percent = GetConfigInt("ggpo.oop.percent");
		}
		~UDPProtocol()
		{
			ClearSendQueue();
		}

		public void Init(ref Udp udp, ref Poll p, int queue, string ip, ushort port, IntPtr status)
		{
			_udp = udp;
			_queue = queue;
			_local_connect_status = status;

			_peer_addr = new IPEndPoint(IPAddress.Parse(ip), port);

			System.Random rand = new System.Random();
			do
			{
				_magic_number = (ushort)rand.Next(1, ushort.MaxValue + 1); // Generate a random number between 1 and ushort.MaxValue
			} while (_magic_number == 0);

			IPollSink sink = this;
			p.RegisterLoop(ref sink);
		}
		public void Synchronize()
		{
			if (_udp != null)
			{
				_current_state = State.Syncing;
				_state.sync.roundtrips_remaining = NUM_SYNC_PACKETS;
				SendSyncRequest();
			}
		}
		public bool GetPeerConnectStatus(int id, ref int frame)
		{
			frame = _peer_connect_status[id].last_frame;
			return !_peer_connect_status[id].disconnected;
		}
		public bool IsInitialized() { return _udp != null; }
		public bool IsSynchronized() { return _current_state == State.Running; }
		public bool IsRunning() { return _current_state == State.Running; }
		public void SendInput(ref GameInput input)
		{
			if (_udp != null)
			{
				if (_current_state == State.Running)
				{
					_timesync.AdvanceFrame(ref input, _local_frame_advantage, _remote_frame_advantage);
					_pending_output.Push(input);
				}
				SendPendingOutput();
			}
		}
		public bool HandlesMsg(ref IPEndPoint from, ref UdpMsg msg)
		{
			if (_udp == null)
			{
				return false;
			}

			return _peer_addr.Address.Equals(from.Address) &&
				   _peer_addr.Port == from.Port;
		}
		public void OnMsg(ref UdpMsg msg, int len)
		{
			bool handled = false;
			DispatchFn[] table = {
			OnInvalid,
			OnSyncRequest,
			OnSyncReply,
			OnInput,
			OnQualityReport,
			OnQualityReply,
			OnKeepAlive
		};


			ushort seq = msg.hdr.sequence_number;
			if (msg.hdr.type != (byte)UdpMsg.MsgType.SyncRequest &&
				msg.hdr.type != (byte)UdpMsg.MsgType.SyncReply)
			{
				if (msg.hdr.magic != _remote_magic_number)
				{
					//LogMsg("recv rejecting", msg);
					return;
				}
				
				ushort skipped = (ushort)((int)seq - (int)_next_recv_seq);
				//Debug.Log($"checking sequence number -> next - seq : {seq} - {_next_recv_seq} = {skipped}\n");
				if (skipped > MAX_SEQ_DISTANCE)
				{
					//Debug.Log($"dropping out of order packet (seq: {seq}, last seq: {_next_recv_seq})\n");
					return;
				}
			}

			_next_recv_seq = seq;
			if (msg.hdr.type >= table.Length)
			{
				OnInvalid(ref msg, len);
			}
			else
			{
				handled = table[msg.hdr.type](ref msg, len);
			}
			
			if (handled)
			{
				_last_recv_time = BackrollTime.GetTime(); 
				if (_disconnect_notify_sent && _current_state == State.Running)
				{
					QueueEvent(new Event(Event.Type.NetworkResumed)); 
					_disconnect_notify_sent = false;
				}
			}
		}
		public void Disconnect()
		{
			_current_state = State.Disconnected;
			_shutdown_timeout = BackrollTime.GetTime() + UDP_SHUTDOWN_TIMER;
		}

		public void GetNetworkStats(ref BackrollNetworkStats stats)
		{
			stats.ping = _round_trip_time;
			stats.send_queue_len = _pending_output._size;
			stats.kbps_sent = _kbps_sent;
			stats.remote_frames_behind = _remote_frame_advantage;
			stats.local_frames_behind = _local_frame_advantage;
		}
		public bool GetEvent(ref UDPProtocol.Event e)
		{
			if (_event_queue._size == 0)
			{
				return false;
			}
			e = _event_queue.Peek();
			_event_queue.Pop();
			return true;
		}
		//GGPONetworkStats
		public void SetLocalFrameNumber(int num)
		{
			int remoteFame = _last_received_input.frame + (_round_trip_time * 60 / 1000);
			_local_frame_advantage = remoteFame - num;
		}
		public int RecommendFrameDelay()
		{
			return _timesync.RecommendFrameWaitDuration(false);
		}

		public void SetDisconnectTimeout(uint timeout)
		{
			_disconnect_timeout = timeout;
		}
		public void SetDisconnectNotifyStart(uint timeout)
		{
			_disconnect_notify_start = timeout;
		}

		protected enum State
		{
			Syncing,
			Synchronzied,
			Running,
			Disconnected
		};
		protected unsafe struct QueueEntry
		{
			public int queue_time;
			public IPEndPoint dest_addr;
			public IntPtr msg;

			public QueueEntry(int time, ref IPEndPoint dst, IntPtr m)
			{
				queue_time = time;
				dest_addr = dst;
				msg = m;
			}
		};

		//CreateSocket
		protected void QueueEvent(in UDPProtocol.Event evt)
		{
			_event_queue.Push(evt);
		}
		protected void ClearSendQueue()
		{
			while (!_send_queue.IsEmpty)
			{
				_send_queue.Peek().msg = IntPtr.Zero;
				_send_queue.Pop();
			}
		}
		//Log
		//LogMsg
		//LogEvent
		protected void SendSyncRequest()
		{
			_state.sync.random = (uint)UnityEngine.Random.Range(0, 65536) & 0xFFFF;
			UdpMsg msg = new UdpMsg(UdpMsg.MsgType.SyncRequest);
			msg.u.sync_request.random_request = _state.sync.random;
			SendMsg(ref msg);
		}
		protected void SendMsg(ref UdpMsg msg)
		{
			//Debug.Log($"send {msg.hdr.type} EXinputType : {UdpMsg.MsgType.Input}");

			_last_send_time = BackrollTime.GetTime();

			msg.hdr.magic = _magic_number;
			msg.hdr.sequence_number = _next_send_seq++;

			_send_queue.Push(new QueueEntry((int)BackrollTime.GetTime(), ref _peer_addr, PointerUtill.ToIntPtrStruct(msg)));
			PumpSendQueue();
		}
		protected unsafe void PumpSendQueue()
		{
			System.Random rand = new System.Random();
			while (!_send_queue.IsEmpty)
			{
				ref QueueEntry entry = ref _send_queue.Peek();
				if (_send_latency != 0)
				{
					int jitter = (_send_latency * 2 / 3) + (rand.Next(_send_latency) / 3);
					if (BackrollTime.GetTime() < _send_queue.Peek().queue_time + jitter)
					{
						break;
					}
				}
				if (_oop_percent > 0 && _oo_packet.msg == IntPtr.Zero && (rand.Next(100) < _oop_percent))
				{
					int delay = rand.Next(_send_latency * 10 + 1000);
					//Debug.Log("creating rogue oop (seq: %d  delay: %d)\n", entry.msg, delay);
					_oo_packet.send_time = (int)BackrollTime.GetTime() + delay;
					_oo_packet.msg = entry.msg;
					_oo_packet.dest_addr = entry.dest_addr;
				}
				else
				{
					Debug.Assert(!string.IsNullOrEmpty(entry.dest_addr.Address.ToString()));
					var msg = PointerUtill.FromIntPtrStruct<UdpMsg>(entry.msg);
					if (msg.hdr.type == 3)
					{
						string str = $"Type : {msg.hdr.type} Bits {msg.u.input.num_bits} Input {msg.u.input[0].last_frame} {msg.u.input[1].last_frame} {msg.u.input[0].disconnected} {msg.u.input[1].disconnected} abcd";
						//Debug.Log("Send : " + str);
					}
					byte[] data = msg.PacketData();
					_udp.SendTo(data, entry.dest_addr);
					//Debug.Log($"Send {msg.hdr.type}");
				}
				_send_queue.Pop();
			}

			if (_oo_packet.msg != IntPtr.Zero && _oo_packet.send_time < BackrollTime.GetTime())
			{
				//Debug.Log("sending rougue oop!");
				var msg = PointerUtill.FromIntPtrStruct<UdpMsg>(_oo_packet.msg);
				if (msg.hdr.type == 3)
				{
					string str = $"Type : {msg.hdr.type} Bits {msg.u.input.num_bits} Input {msg.u.input[0].last_frame} {msg.u.input[1].last_frame} {msg.u.input[0].disconnected} {msg.u.input[1].disconnected} abcd";
					//Debug.Log("oop Send : " + str);
				}
				byte[] data = msg.PacketData();
				_udp.SendTo(data, _oo_packet.dest_addr);
				_oo_packet.msg = IntPtr.Zero;
			}

		}
		//DispatchMsg
		protected unsafe void SendPendingOutput()
		{
			UdpMsg msg = new UdpMsg(UdpMsg.MsgType.Input);
			int i, j, offset = 0;
			byte* bits;
			GameInput last;

			if (_pending_output._size > 0)
			{
				last = _last_acked_input;
				bits = msg.u.input.bits;

				msg.u.input.start_frame = (uint)_pending_output.Peek().frame;
				msg.u.input.input_size = (byte)_pending_output.Peek().size;

				//Debug.Log($"Stop {last.frame} {msg.u.input.start_frame}");
				Debug.Assert(last.frame == -1 || last.frame + 1 == msg.u.input.start_frame);
				for (j = 0; j < _pending_output._size; j++)
				{
					ref GameInput current = ref _pending_output[j];
					fixed(byte* currentbits = current.bits)
					{
						if (UnsafeUtility.MemCmp(currentbits, last.bits, _pending_output[j].size) != 0)
						{
							Debug.Assert((GameInput.kMaxBytes * BackrollConstants.kMaxPlayers) < (1 << BitVector.kNibbleSize));
							for (i = 0; i < current.size * 8; i++)
							{
								Debug.Assert(i < (1 << BitVector.kNibbleSize));

								if(current.value(i) != last.value(i))
								{
									BitVector.SetBit(msg.u.input.bits, ref offset);
									if (current.value(i))
									{
										BitVector.SetBit(bits, ref offset);
									}
									else
									{
										BitVector.ClearBit(bits, ref offset);
									}
									BitVector.WriteNibblet(bits, i, ref offset);
								}
							}
						}
						BitVector.ClearBit(msg.u.input.bits, ref offset);
						last = _last_sent_input = current;
					}
				}
			}
			else
			{
				msg.u.input.start_frame = 0;
				msg.u.input.input_size = 0;
			}

			msg.u.input.ack_frame = _last_received_input.frame;
			msg.u.input.num_bits = (ushort)offset;

			msg.u.input.disconnect_requested = _current_state == State.Disconnected;
			var size = UnsafeUtility.SizeOf<BackrollConnectStatus>() * BackrollConstants.kMaxPlayers;
			if (_local_connect_status != IntPtr.Zero)
			{
				BackrollConnectStatus[] backrollConnectStatuses = PointerUtill.IntPtrToArray<BackrollConnectStatus>(_local_connect_status, BackrollConstants.kMaxPlayers);

				for (i = 0; i < BackrollConstants.kMaxPlayers; ++i)
				{
					msg.u.input[i] = backrollConnectStatuses[i];
				}
			}
			else
			{
				for (i = 0; i < BackrollConstants.kMaxPlayers; ++i)
				{
					msg.u.input[i] = new BackrollConnectStatus() { disconnected = false, last_frame = 0 };
				}
			}

			Debug.Assert(offset < InputMessage.kMaxCompressedBits);

			SendMsg(ref msg);
		}
		protected bool OnInvalid(ref UdpMsg msg, int len)
		{
			return false;
		}
		protected bool OnSyncRequest(ref UdpMsg msg, int len)
		{
			if (_remote_magic_number != 0 && msg.hdr.magic != _remote_magic_number)
			{
				//Debug.Log($"Ignoring sync request from unknown endpoint ({msg.hdr.magic} != {_remote_magic_number}).\n");
				return false;
			}
			UdpMsg reply = new UdpMsg(UdpMsg.MsgType.SyncReply);
			reply.u.sync_reply.random_reply = msg.u.sync_request.random_request;
			SendMsg(ref reply);
			return true;
		}
		protected bool OnSyncReply(ref UdpMsg msg, int len)
		{
			if (_current_state != State.Syncing)
			{
				//Debug.Log("Ignoring SyncReply while not synching.\n");
				return msg.hdr.magic == _remote_magic_number;
			}

			if (msg.u.sync_reply.random_reply != _state.sync.random)
			{
				//Debug.Log($"sync reply {msg.u.sync_reply.random_reply} != {_state.sync.random}.  Keep looking...\n");
				return false;
			}


			if (!_connected)
			{
				QueueEvent(new Event(Event.Type.Connected));
				_connected = true;
			}


			//Debug.Log("Checking sync state ({_state.sync.roundtrips_remaining} round trips remaining).\n");
			if (--_state.sync.roundtrips_remaining == 0)
			{
				//Debug.Log("Synchronized!\n");
				QueueEvent(new Event(Event.Type.Synchronzied));
				_current_state = State.Running;
				_last_received_input.frame = -1;
				_remote_magic_number = msg.hdr.magic;
			}
			else
			{
				Event evt = new Event(Event.Type.Synchronizing);
				evt.synchronizing.total = NUM_SYNC_PACKETS;
				evt.synchronizing.count = NUM_SYNC_PACKETS - (int)_state.sync.roundtrips_remaining;
				QueueEvent(evt);
				SendSyncRequest();
			}

			return true;
		}
		protected bool OnInput(ref UdpMsg msg, int len)
		{
			bool disconnect_requested = msg.u.input.disconnect_requested;
			if (disconnect_requested)
			{
				if (_current_state != State.Disconnected && !_disconnect_event_sent)
				{
					//Debug.Log("Disconnecting endpoint on remote request.");
					QueueEvent(new Event(Event.Type.Disconnected));
					_disconnect_event_sent = true;
				}
			}
			else
			{
				for (int i = 0; i < _peer_connect_status.Length; i++)
				{
					Debug.Assert(msg.u.input[i].last_frame >= _peer_connect_status[i].last_frame);
					_peer_connect_status[i].disconnected = _peer_connect_status[i].disconnected || msg.u.input[i].disconnected;
					_peer_connect_status[i].last_frame = Math.Max(_peer_connect_status[i].last_frame, msg.u.input[i].last_frame);
				}
			}

			int last_received_frame_number = _last_received_input.frame;
			if (msg.u.input.num_bits > 0)
			{
				int offset = 0;
				int numBits = msg.u.input.num_bits;
				int currentFrame = (int)msg.u.input.start_frame;

				_last_received_input.size = msg.u.input.input_size;
				if (_last_received_input.frame < 0)
				{
					_last_received_input.frame = (int)msg.u.input.start_frame - 1;
				}
				fixed (byte* ptr = msg.u.input.bits)
				{
					while (offset < numBits)
					{
						Debug.Assert(currentFrame <= (_last_received_input.frame + 1));
						bool useInputs = currentFrame == _last_received_input.frame + 1;

						while (BitVector.ReadBit(ptr, ref offset))
						{
							bool bit = BitVector.ReadBit(ptr, ref offset);
							int button = BitVector.ReadNibblet(ptr, ref offset);
							if (useInputs)
							{
								if (bit)
								{
									_last_received_input.set(button);
								}
								else
								{
									_last_received_input.clear(button);
								}
							}
						}
						Debug.Assert(offset <= numBits);

						if (useInputs)
						{
							Debug.Assert(currentFrame == _last_received_input.frame + 1);
							_last_received_input.frame = currentFrame;

							Event evt = new Event(Event.Type.Input);
							evt.input.input = _last_received_input;
							//_last_received_input.ToString(desc, ARRAY_SIZE(desc));

							_state.running.last_input_packet_recv_time = BackrollTime.GetTime();

							//Debug.LogFormat("Sending frame {} to emu queue {} ({}).",
							//   _last_received_input.frame, _queue, _last_received_input);

							QueueEvent(evt);
						}
						else
						{
							//Debug.Log($"Skipping past frame:({currentFrame}) current is {_last_received_input.frame}.");
						}

						currentFrame++;
					}
				}
			}
			Debug.Assert(_last_received_input.frame >= last_received_frame_number);

			while (!_pending_output.IsEmpty && _pending_output.Peek().frame < msg.u.input.ack_frame)
			{
				//Debug.Log($"Throwing away pending output frame {_pending_output.Peek().frame}");
				_last_acked_input = _pending_output.Peek();
				_pending_output.Pop();
			}
			return true;
		}

		protected bool OnQualityReport(ref UdpMsg msg, int len)
		{
			UdpMsg reply = new UdpMsg(UdpMsg.MsgType.QualityReport);
			reply.u.quality_reply.pong = msg.u.quality_report.ping;
			_remote_frame_advantage = msg.u.quality_report.frame_advantage;
			
			return true;
		}
		protected bool OnQualityReply(ref UdpMsg msg, int len)
		{
			_round_trip_time = (int)BackrollTime.GetTime() - (int)msg.u.quality_reply.pong;
			return true;
		}
		protected bool OnKeepAlive(ref UdpMsg msg, int len)
		{
			return true;
		}

		protected Udp _udp;
		protected IPEndPoint _peer_addr;
		protected ushort _magic_number;
		protected int _queue;
		protected ushort _remote_magic_number;
		protected bool _connected;
		protected int _send_latency;
		protected int _oop_percent;

		protected struct OOPacket
		{
			public int send_time;
			public IPEndPoint dest_addr;
			public IntPtr msg;
		}

		protected OOPacket _oo_packet;

		protected RingBuffer<QueueEntry> _send_queue = new RingBuffer<QueueEntry>(64);

		protected int _round_trip_time;
		protected int _kbps_sent;

		protected IntPtr _local_connect_status;
		protected BackrollConnectStatus[] _peer_connect_status = new BackrollConnectStatus[UdpMsg.UDP_MSG_MAX_PLAYERS];

		protected State _current_state;
		protected ConnectionState _state;

		protected int _local_frame_advantage;
		protected int _remote_frame_advantage;

		protected RingBuffer<GameInput> _pending_output = new RingBuffer<GameInput>(64);
		protected GameInput _last_received_input;
		protected GameInput _last_sent_input;
		protected GameInput _last_acked_input;
		protected uint _last_send_time;
		protected uint _last_recv_time;
		protected uint _shutdown_timeout;
		protected bool _disconnect_event_sent;
		protected uint _disconnect_timeout;
		protected uint _disconnect_notify_start;
		protected bool _disconnect_notify_sent;

		ushort _next_send_seq;
		ushort _next_recv_seq;

		TimeSync _timesync = new TimeSync();

		RingBuffer<UDPProtocol.Event> _event_queue = new RingBuffer<Event>(64);


		public static int GetConfigInt(string name)
		{
			string value = Environment.GetEnvironmentVariable(name);
			if (value == null)
			{
				return 0;
			}

			if (int.TryParse(value, out int result))
			{
				return result;
			}
			else
			{
				// Handle the case where the environment variable value cannot be parsed as an integer
				// You can throw an exception, log an error, or return a default value
				return 0;
			}
		}
	}
}

