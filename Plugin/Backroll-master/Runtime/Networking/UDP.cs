using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HouraiTeahouse.Backroll
{

	public unsafe class Udp : IPollSink
	{
		public struct Stats
		{
			public int bytes_sent;
			public int packets_sent;
			public float kbps_sent;
		}

		public interface Callbacks
		{
			public void OnMsg(ref IPEndPoint from, ref UdpMsg msg, int len);
		}

		private const int MAX_UDP_PACKET_SIZE = 4096;
		public Socket _socket;
		private Callbacks _callbacks;
		private Poll _poll;
		public EndPoint endPoint;

		public Udp()
		{
			_socket = null;
			_callbacks = null;
		}

		~Udp()
		{
			if (_socket != null)
			{
				_socket.Dispose();
				_socket = null;
			}

			GC.SuppressFinalize(this);
		}

		public Socket CreateSocket(ushort bindPort, int retries)
		{
			endPoint = new IPEndPoint(IPAddress.Any, bindPort);
			Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			s.Blocking = false;
			s.Bind(endPoint);

			return s;
		}

		public void Init(ushort port, ref Poll poll, ref Callbacks callbacks)
		{
			_callbacks = callbacks;

			_poll = poll;
			IPollSink sink = this;
			_poll.RegisterLoop(ref sink);

			Debug.Log($"binding udp socket to port {port}.\n");
			_socket = CreateSocket(port, 0);
		}
		public void SendTo(byte[] buffer, IPEndPoint dst)
		{
			int res = 0;
			try
			{
				res = _socket.SendTo(buffer, dst);
			}
			catch(Exception ex)
			{
				//Debug.LogError($"unknown error in sendto (erro: {res} len : {buffer.Length} wsaerr: {ex}).\n");
			}
			string dst_ip = dst.Address.ToString();
			int dstPort = dst.Port;
			//Debug.Log($"sent packet length {buffer.Length} to {dst_ip}:{dstPort} (ret:{res}).");
		}
		public unsafe bool OnLoopPoll(void* cookie)
		{
			byte[] packet = new byte[MAX_UDP_PACKET_SIZE];
			EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
			int len = 0;

			while (true)
			{
				try
				{
					len = _socket.ReceiveFrom(packet, ref sender);
				}
				catch (SocketException ex)
				{
					//Debug.LogError($"에러코드 {ex.ErrorCode} {sender.ToString()}");
					break;
				}
			
				if (len > 0)
				{
					//Debug.Log($"Receive MSG {sender.ToString()}");
					UdpMsg msg = UdpMsg.ByteArrayToUdpMsg(packet);
					IPEndPoint ipEndPoint = (IPEndPoint)sender;
					_callbacks.OnMsg(ref ipEndPoint, ref msg, len);
				}
			}


			return true;
		}
	}

}