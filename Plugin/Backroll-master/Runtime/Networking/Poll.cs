using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace HouraiTeahouse.Backroll
{
	public unsafe interface IPollSink
	{
		bool OnHandlePoll(void* ptr)
		{ return true; }
		bool OnMsgPoll(void* ptr)
		{ return true; }
		bool OnPeriodicPoll(void* ptr, int interval)
		{ return true; }
		bool OnLoopPoll(void* ptr)
		{ return true; }
	}

	public unsafe class Poll
	{
		public const uint STATUS_WAIT_0 = 0x00000000U;
		public const uint WAIT_OBJECT_0 = STATUS_WAIT_0 + 0;
		private const int MAX_POLLABLE_HANDLES = 64;

		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct PollSinkCb
		{
			public IntPtr Sink; // IPollSink*
			public void* Cookie; // void*

			public PollSinkCb(IPollSink sink, void* cookie)
			{
				GCHandle handle = GCHandle.Alloc(sink, GCHandleType.Normal);
				Sink = GCHandle.ToIntPtr(handle);
				Cookie = cookie;
			}
		}

		private StaticBuffer<PollSinkCb> _loop_sinks = new StaticBuffer<PollSinkCb>(16);

		public Poll()
		{
		}

		public void RegisterLoop(ref IPollSink sink, void* cookie = default)
		{
			_loop_sinks.Push(new PollSinkCb(sink, cookie));
		}
		public unsafe bool Pump(int timeout)
		{
			int i;
			bool finished = false;

			for (i = 0; i < _loop_sinks.Size; i++)
			{
				IPollSink sink = PointerUtill.FromIntPtr<IPollSink>(_loop_sinks[i].Sink);
				finished = !sink.OnLoopPoll(_loop_sinks[i].Cookie) || finished;
			}

			return finished;
		}
	}

}