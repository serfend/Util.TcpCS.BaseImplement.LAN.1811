using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SfTcp
{
	public class TcpHttpMessage
	{
		private string method;
		private string param;
		private string httpVersion;
		public TcpHttpMessage(string method, string param, string httpVersion)
		{
			Method = method;
			this.Param = param;
			this.HttpVersion = httpVersion;
			Console.WriteLine(param);
		}
		public string Param { get => param; set => param = value; }
		public string HttpVersion { get => httpVersion; set => httpVersion = value; }
		public string Method { get => method; set => method = value; }
	}
	public class TcpHttpResponse
	{
		private TcpServer server;
		public TcpHttpResponse(TcpServer server)
		{
			this.server = server;
		}
		public void Response(string info,string title="Serfend")
		{
			var cstr = new StringBuilder();
			cstr.AppendLine("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n\r\n");
			cstr.AppendLine("<html lang=\"zh-cn\"><head><meta charset = \"utf-8\" />");
			cstr.AppendLine(string.Format("<title>{0}</title>",title));
			cstr.AppendLine("</head><body>");
			cstr.AppendLine(info);
			cstr.AppendLine("</body></html>");
			server.Send(Encoding.UTF8.GetBytes(cstr.ToString()));
			server.client.Close();
		}
	}
	public class TcpServer:IDisposable
	{
		#region 属性
		TcpListener listener;
		private Thread thread;
		private Thread reporter;
		public TcpClient client;
		private BinaryWriter writter;
		private BinaryReader reader;
		private Action<string, TcpServer> Receive;//收到信息回调
		public Action<TcpServer> Connected;//连接成功回调
		public Action<TcpServer> Disconnected;//连接成功回调
		public Action<TcpHttpMessage, TcpHttpResponse> OnHttpRequest;//当来源为http方式时
		public bool IsLocal = false;
		public string Ip;
		public string ID = "null";
		public string clientName = "...";
		#endregion
		public TcpServer( Action<string,TcpServer> ReceiveInfo = null,Action<TcpHttpMessage, TcpHttpResponse> ReceiveHttp=null,int port=8009)
		{
			listener = new TcpListener(IPAddress.Any, port);
			this.Receive = ReceiveInfo;
			this.OnHttpRequest = ReceiveHttp;
			this.Connect();
		}
		public void Connect()
		{
			var t = new Thread(() =>
			{
				try
				{

					listener.Start();
					client = listener.AcceptTcpClient();
					listener.Stop();
					var ip = this.client.Client.RemoteEndPoint.ToString();
					if (ip.Contains("127.0.0.1")) IsLocal = true;
					this.Ip = this.client.Client.RemoteEndPoint.ToString();
					Connected?.BeginInvoke(this, (x) => { }, null);
					var stream = client.GetStream();
					writter = new BinaryWriter(stream);
					reader = new BinaryReader(stream);
					thread = new Thread(Reciving) { IsBackground = true };
					thread.Start();
					reporter = new Thread(() => {
						while (true)
						{
							var thisLen = cstr.Length;
							if (thisLen == lastLength && thisLen > 0 && reporterCounter++ > 50)
							{
								RecieveComplete();
							}
							else
							{
								lastLength = thisLen;
							}
							Thread.Sleep(10);
						}
					})
					{ IsBackground = true };
					reporter.Start();
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
				}
			})
			{ IsBackground=true};
			t.Start();
			
		}
		private void RecieveComplete(bool getEndPoint=false)
		{
			if (getEndPoint) cstr.Replace(this.TcpComplete,"");
;			ReceiveInfo(cstr.ToString());
			//Receive.Invoke(cstr.ToString(), this);
			cstr.Clear();
			lastLength = 0;
			reporterCounter = 0;
			nowCheckIndex = 0;
			reader.BaseStream.Flush();
		}
		private int reporterCounter = 0;
		private int lastLength;
		private void ReceiveInfo(string info)
		{
			var firstLineIndex = info.IndexOf("\n");
			if (firstLineIndex > 0)
			{
				var firstLine = info.Substring(0, firstLineIndex - 1);
				var lineInfo = firstLine.Split(' ');
				if (lineInfo.Length == 3)
				{
					OnHttpRequest?.BeginInvoke(new TcpHttpMessage(lineInfo[0], lineInfo[1].Substring(1), lineInfo[2]), new TcpHttpResponse(this),(x)=> { },null);
					return;
				}
			}
			Receive?.BeginInvoke(info,this,(x)=> { },null);
		}
		public void Disconnect()
		{
			listener.Stop();
			if(client.Connected) client.Close();

		}
		private string TcpComplete {
			get => "#$%&'";
		}
		public bool Send(string info)
		{
			return Send(Encoding.UTF8.GetBytes(info+TcpComplete));
		}
		public bool Send(byte[] info)
		{
			if (client.Connected)
			{
				try
				{
					writter.Write(info);
					writter.Flush();
				}
				catch (Exception ex)
				{
					Console.WriteLine("Tcp.Send()"+ex.Message);
					Disconnected?.BeginInvoke(this,(x)=> { },null);
					return false;
				}
				return true;
			}
			else return false;
		}
		StringBuilder cstr = new StringBuilder();
		private int nowCheckIndex=0;
		private void Reciving()
		{
			while (true)
			{
				if (client.Connected)
				{
					try
					{
						var c = reader.ReadChar();
						cstr.Append(c);
						if (c == ('#' + nowCheckIndex))
						{
							nowCheckIndex++;
							if (nowCheckIndex == 5)
							{
								RecieveComplete(true);
								continue;
							}
						}
						
					}
					catch  (Exception ex)
					{
						Disconnected?.BeginInvoke(this, (x) => { }, null);
						Console.WriteLine("Tcp.Reciving()"+ex.Message);
						break;
					}
					
				}
				else
				{
					Console.WriteLine("已断开");
					Disconnected?.BeginInvoke(this, (x) => { }, null);
					break;
				}
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // 要检测冗余调用

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (client != null) client.Close();
					if (writter != null) writter.Dispose();
					if (reader != null) reader.Dispose();
				}
				client = null;
				writter = null;
				reader = null;
				disposedValue = true;
			}
		}


		// 添加此代码以正确实现可处置模式。
		public void Dispose()
		{
			// 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
			Dispose(true);

		}
		#endregion
	}
}
