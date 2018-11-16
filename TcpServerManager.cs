using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SfTcp
{
	public class TcpServerManager
	{
		private List<TcpServer> list = new List<TcpServer>();
		public Action<TcpServer> ServerConnected;
		public Action<TcpServer> ServerDisconnected;
		public Action<TcpServer, string> NormalMessage;
		public Action<TcpHttpMessage, TcpHttpResponse> HttpRequest;
		public TcpServer this[string ip]
		{
			get => list.Find((x) => x.Ip == ip);
		}
		public TcpServerManager()
		{
			NewTcp();
		}
		private Action<TcpServer> Connected() {
			return new Action<TcpServer>((x) =>
			{
				ServerConnected?.Invoke(x);
				Console.WriteLine("新的用户连接"+x.client.Client.RemoteEndPoint.ToString());
				list.Add(x);
				NewTcp();//继续侦听其他客户端
			});
		}
		private TcpServer NewTcp(int port=8009)
		{
			return new TcpServer((x, s) => {
				NormalMessage?.Invoke(s,x);
				if (x.Contains("<mobile>"))
				{
					if (x.Contains("heartBeat"))
					{
						s.Send("<server.response>heartBeat</server.response>");
						return;
					}
					if (s.IsLocal)
					{
						foreach (var p in list)
						{
							if (!p.IsLocal) p.Send("<server.command>" + x + "</server.command>");
						}
					}
					else
						s.Send("<server.response>" + x + "</server.response>");
				}
			}, (x,s) => {
				HttpRequest.Invoke(x,s);
			}
			
			,port)
			{
				Connected = Connected(),
				Disconnected = (x) => {
					ServerDisconnected?.Invoke(x);
					list.Remove(x); }
			};
		}
		
	}
}
