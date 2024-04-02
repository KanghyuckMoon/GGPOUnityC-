namespace HouraiTeahouse.Backroll
{

	public struct BackrollNetworkStats
	{
		public int send_queue_len;
		public int ReceiveQueueLength;
		public int ping;
		public int kbps_sent;
		public int local_frames_behind;
		public int remote_frames_behind;
	}

}
