using System;

namespace HouraiTeahouse.Backroll
{
	public delegate void LogDelegate(string text);

	public delegate bool BeginGameDelegate(string text);

	public delegate bool AdvanceFrameDelegate(int flags);

	public unsafe delegate bool LoadGameStateDelegate(void* buffer, int length);

	public unsafe delegate bool LogGameStateDelegate(string filename, void* buffer, int length);

	public unsafe delegate bool SaveGameStateDelegate(void** buffer, int* len, int* checksum, int frame);

	public unsafe delegate void FreeBufferDelegate(void* buffer);

	public delegate bool OnEventDelegate(IntPtr evt);

	public unsafe class BackrollSessionCallbacks
	{
		public AdvanceFrameDelegate advance_frame;
		public BeginGameDelegate begin_game;
		public LoadGameStateDelegate load_game_state;
		public LogGameStateDelegate log_game_state;
		public SaveGameStateDelegate save_game_state;
		public FreeBufferDelegate free_buffer;
		public OnEventDelegate on_event;
	}
}