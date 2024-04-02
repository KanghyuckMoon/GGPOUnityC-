using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace HouraiTeahouse.Backroll
{

	public unsafe class Sync
	{

		public const int MAX_PREDICTION_FRAMES = 8;
		public unsafe struct SavedFrame
		{
			public byte* buf;
			public int cbuf;
			public int frame;
			public int checksum;

			public static SavedFrame Create()
			{
				return new SavedFrame { frame = GameInput.NullFrame, buf = (byte*)IntPtr.Zero, cbuf = 0, checksum = 0 };
			}
		}

		public struct SavedState
		{
			public SavedFrame[] frames;
			public int head;

			public SavedState(int maxPredictionFrames)
			{
				head = 0;
				frames = new SavedFrame[maxPredictionFrames + 2];
				for (var i = 0; i < frames.Length; i++)
				{
					frames[i] = SavedFrame.Create();
				}
			}
		}

		public struct Config
		{
			public int num_players;
			public int input_size;
			public int NumPredictionFrames;
			public BackrollSessionCallbacks callbacks;
		}

		BackrollSessionCallbacks _callbacks;
		public SavedState _savedstate;
		Config _config;
		InputQueue[] _inputQueues;
		RingBuffer<Event> _event_queue = new RingBuffer<Event>(32);
		BackrollConnectStatus[] _local_connect_status;

		public bool _rollingback;
		public int _framecount;
		public int _lastConfirmedFrame;
		public int _maxPredictionFrames;

		public Sync(BackrollConnectStatus[] connect_status, Config config)
		{
			_local_connect_status = connect_status;
			_lastConfirmedFrame = -1;
			_maxPredictionFrames = 0;

			_savedstate = new SavedState(8);
			_config = config;
			_callbacks = config.callbacks;
			_framecount = 0;
			_rollingback = false;

			_maxPredictionFrames = config.NumPredictionFrames;

			CreateQueues(config);
		}

		~Sync()
		{
			// Delete frames manually here rather than in a destructor of the SavedFrame
			// structure so we can efficently copy frames via weak references.
			for (int i = 0; i < _savedstate.frames.Length; i++)
			{
				//fixed(byte* buf = _savedstate.frames[i].buf)
				//{
				//}
				_callbacks.free_buffer?.Invoke(_savedstate.frames[i].buf);
			}
			_inputQueues = null;
		}

		public void SetLastConfirmedFrame(int frame)
		{
			_lastConfirmedFrame = frame;
			if (_lastConfirmedFrame > 0)
			{
				for (int i = 0; i < _config.num_players; i++)
				{
					_inputQueues[i].DiscardConfirmedFrames(frame - 1);
				}
			}
		}
		public void SetFrameDelay(int queue, int delay)
		{
			_inputQueues[queue]._frame_delay = delay;
		}
		public bool AddLocalInput(int queue, ref GameInput input)
		{
			int frames_behind = _framecount - _lastConfirmedFrame;
			if (_framecount >= _maxPredictionFrames && frames_behind >= _maxPredictionFrames)
			{
				//Debug.Log($"Rejecting input from emulator: reached prediction barrier. Frame {_framecount} Last {_lastConfirmedFrame}");
				return false;
			}

			if (_framecount == 0)
			{
				SaveCurrentFrame();
			}

			//Debug.Log($"Sending undelayed local frame {_framecount} to queue {queue}.");
			input.frame = _framecount;
			_inputQueues[queue].AddInput(ref input);

			return true;
		}
		public void AddRemoteInput(int queue, ref GameInput input)
		{
			_inputQueues[queue].AddInput(ref input);
		}
		public unsafe int SynchronizeInputs(void* values, int size)
		{
			int disconnect_flags = 0;
			var output = (byte*)values;

			Debug.Assert(size >= _config.num_players * _config.input_size);

			UnsafeUtility.MemSet(output, 0, size);
			for (int i = 0; i < _config.num_players; i++)
			{
				var input = new GameInput(GameInput.NullFrame, null, (uint)_config.input_size);
				if (_local_connect_status[i].disconnected &&
					_framecount > _local_connect_status[i].last_frame)
				{
					disconnect_flags |= (1 << i);
					input.erase();
				}
				else
				{
					_inputQueues[i].GetInput(_framecount, ref input);
				}
				UnsafeUtility.MemCpy(output + (i * _config.input_size), input.bits, _config.input_size);
			}
			return disconnect_flags;
		}

		public void CheckSimulation(int timeout)
		{
			int seek_to;
			if (!CheckSimulationConsistency(&seek_to))
			{
				AdjustSimulation(seek_to);
			}
		}
		public void AdjustSimulation(int seek_to)
		{
			int framecount = _framecount;
			int count = _framecount - seek_to;

			Debug.Log("Catching up");
			_rollingback = true;

			LoadFrame(seek_to);
			Debug.Assert(_framecount == seek_to);

			ResetPrediction(_framecount);
			for (int i = 0; i < count; i++)
			{
				_callbacks.advance_frame.Invoke(0);
			}
			Debug.Assert(_framecount == framecount);

			_rollingback = false;

			Debug.Log("---");
		}
		public void IncrementFrame()
		{
			_framecount++;
			SaveCurrentFrame();
		}

		public int GetFrameCount() { return _framecount; }
		public bool InRollback() { return _rollingback; }
		public void LoadFrame(int frame)
		{
			if (frame == _framecount)
			{
				//Debug.Log("Skipping NOP.");
				return;
			}

			_savedstate.head = FindSavedFrameIndex(frame);
			ref SavedFrame state = ref _savedstate.frames[_savedstate.head];

			//Debug.Log($"=== Loading frame info {state.frame} (size: {state.cbuf}  checksum: {state.checksum}).");

			Debug.Assert(state.buf != (byte*)IntPtr.Zero && state.cbuf != 0);

			//Debug.Log($"Load State {state.frame} Length : {state.cbuf}");
			_callbacks.load_game_state.Invoke(state.buf, state.cbuf);
			_framecount = state.frame;
			_savedstate.head = (_savedstate.head + 1) % _savedstate.frames.Length;
		}
		public unsafe void SaveCurrentFrame()
		{
			ref SavedFrame state = ref _savedstate.frames[_savedstate.head];
			if (state.buf != (byte*)IntPtr.Zero)
			{
				_callbacks.free_buffer(state.buf);
				state.buf = (byte*)IntPtr.Zero;
			}
			state.frame = _framecount;

			fixed (int* size = &state.cbuf)
			{
				fixed (int* checkSum = &state.checksum)
				{
					void* intermediatePtr = state.buf;
					void** resultPtr = &intermediatePtr;
					_callbacks.save_game_state.Invoke(resultPtr, size, checkSum, state.frame);
					//Debug.Log($"Save State {_framecount} Length : {state.cbuf} Size {*size}");
					state.buf = (byte*)intermediatePtr;
				}
			}
			_savedstate.head = (_savedstate.head + 1) % _savedstate.frames.Length;
		}
		public int FindSavedFrameIndex(int frame)
		{
			int i, count = _savedstate.frames.Length;
			for (i = 0; i < count; i++)
			{
				if (_savedstate.frames[i].frame == frame)
				{
					break;
				}
			}
			if(i == count)
			{
				Debug.Assert(false);
				return -1;
			}
			return i;
		}
		public ref SavedFrame GetLastSavedFrame()
		{
			int i = _savedstate.head - 1;
			if (i < 0)
			{
				i = _savedstate.frames.Length - 1;
			}
			return ref _savedstate.frames[i];
		}

		bool CreateQueues(in Config config)
		{
			_inputQueues = new InputQueue[_config.num_players];
			for (int i = 0; i < _config.num_players; i++)
			{
				_inputQueues[i] = new InputQueue((uint)_config.input_size, i);
			}
			return true;
		}
		bool CheckSimulationConsistency(int* seekTo)
		{
			int first_incorrect = GameInput.NullFrame;
			for (int i = 0; i < _config.num_players; i++)
			{
				int incorrect = _inputQueues[i].GetFirstIncorrectFrame();
				//Debug.LogFormat("considering incorrect frame {} reported by queue {}.", incorrect, i);

				if (incorrect != GameInput.NullFrame &&
					(first_incorrect == GameInput.NullFrame ||
					 incorrect < first_incorrect))
				{
					first_incorrect = incorrect;
				}
			}

			if (first_incorrect == GameInput.NullFrame)
			{
				//Debug.Log("prediction ok.  proceeding.");
				return true;
			}
			//Debug.Log("prediction not ok.  proceeding.");

			*seekTo = first_incorrect;
			return false;
		}
		protected void ResetPrediction(int frameNumber)
		{
			for (int i = 0; i < _inputQueues.Length; i++)
			{
				_inputQueues[i].ResetPrediction(frameNumber);
			}
		}
	}


}
