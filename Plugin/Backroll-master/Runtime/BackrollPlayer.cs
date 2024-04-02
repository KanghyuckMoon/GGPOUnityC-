
namespace HouraiTeahouse.Backroll
{

	public struct BackrollPlayerHandle
	{
		public int Id;
	}

	public enum BackrollPlayerType
	{
		Local,
		Remote,
		Spectator
	}
	public struct BackrollPlayer
	{
		public BackrollPlayerType Type;
		public int Player_id;
		public string ip;
		public ushort port;
	}

}
