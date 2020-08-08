using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HHProxy
{
    public delegate void ProxySessionCloseHandl(HttpProxySessionInfo httpProxySessionInfo, ClientErrorType clientErrorType);
    public class HttpProxySession
    {
        /// <summary>
        /// 需要代理的客户端
        /// </summary>
        readonly HHTcpClient cTcpClient;
        public IntPtr Handle { get; private set; }
        /// <summary>
        /// 代理的目的客户端
        /// </summary>
        HHTcpClient desTcpCliet;
        /// <summary>
        /// 接收的数据缓冲区
        /// </summary>
        byte[] ReBuffer;
        byte[] DReBuffer;
        /// <summary>
        /// 本次代理的信息 
        /// </summary>
        public HttpProxySessionInfo HttpProxySessionInfo { get; private set; }
        /// <summary>
        /// 代理结束后触发事件
        /// </summary>
        public event ProxySessionCloseHandl OnClose;
        /// <summary>
        /// 客户端接收异步对象
        /// </summary>
        SocketAsyncEventArgs Rsaea;
        /// <summary>
        /// 客户端发送异步对象
        /// </summary>
        SocketAsyncEventArgs Ssaea;
        /// <summary>
        /// 目的地接收异步对象
        /// </summary>
        SocketAsyncEventArgs DRsaea;
        /// <summary>
        /// 目的地发送对象
        /// </summary>
        SocketAsyncEventArgs DSsaea;
        /// <summary>
        /// 连接是否建立完毕
        /// </summary>
        int connectedOver = 0;
        /// <summary>
        /// 已执行关闭
        /// </summary>
        int Closing = 0;
        readonly int ReceiveBufferSize;
        public HttpProxySession(Socket socket)
        {
            cTcpClient = new HHTcpClient(socket);
            Handle = socket.Handle;
            cTcpClient.OnError += OnError;
            cTcpClient.OnReceive += OnReceive;
            cTcpClient.OnSendEnd += OnSendEnd;
            HttpProxySessionInfo = new HttpProxySessionInfo();
            ReceiveBufferSize = socket.ReceiveBufferSize;
        }
        /// <summary>
        /// 开始执行代理转发操作
        /// </summary>
        public void Start()
        {
            if (ReBuffer != null)
            {
                return;
            }
            ReBuffer = new byte[cTcpClient.Client.ReceiveBufferSize];
            Rsaea = new SocketAsyncEventArgs();
            Rsaea.SetBuffer(ReBuffer, 0, ReBuffer.Length);
            if (!cTcpClient.BeginReceive(Rsaea, out ClientErrorType clientErrorType))
            {
                Close(clientErrorType);
            }
        }
        /// <summary>
        /// 客户端发送数据完成
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="saea"></param>
        /// <param name="clientErrorType"></param>
        private void OnSendEnd(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType)
        {
            if (clientErrorType != ClientErrorType.Success)
            {
                Close(clientErrorType);
            }
            else
            {
                ClientErrorType errorType;
                if (connectedOver == 0)
                {
                    connectedOver = 1;

                    if (!cTcpClient.BeginReceive(Rsaea, out errorType))
                    {
                        Close(errorType);
                    }
                    //Console.WriteLine($"连接已建立");
                }
                else
                {
                    HttpProxySessionInfo.AddRetNum(saea.BytesTransferred);
                }
                if (DRsaea == null)
                {
                    int l = ReceiveBufferSize;
                    DRsaea = new SocketAsyncEventArgs();
                    DReBuffer = new byte[l];
                    DRsaea.SetBuffer(DReBuffer, 0, DReBuffer.Length);
                }
                if (!desTcpCliet.BeginReceive(DRsaea, out errorType))
                {
                    Close(errorType);
                }


            }
        }
        /// <summary>
        /// 客户端接收到数据
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="saea"></param>
        /// <param name="clientErrorType"></param>
        private void OnReceive(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType)
        {
            if (clientErrorType != ClientErrorType.Success)
            {
                Close(clientErrorType);
                return;
            }
            if (HttpProxySessionInfo.HeadLength == -1)//尚为解析协议头
            {
                if (HttpProxySessionInfo.MemoryStream == null)
                {
                    HttpProxySessionInfo.MemoryStream = new System.IO.MemoryStream();
                }
                HttpProxySessionInfo.MemoryStream.Write(saea.Buffer, saea.Offset, saea.BytesTransferred);

                ToParseQuery();//解析协议头
            }
            else
            {
                if (DSsaea == null)
                {
                    DSsaea = new SocketAsyncEventArgs();
                }
                //直接转发就行了
                DSsaea.SetBuffer(saea.Buffer, saea.Offset, saea.BytesTransferred);
                if (!desTcpCliet.BeginSend(DSsaea, out var errorType))
                {
                    Close(errorType);
                }

            }
        }
        /// <summary>
        /// 解析协议头
        /// </summary>
        private void ToParseQuery()
        {
            #region 解析协议头
            string aBin = Encoding.UTF8.GetString(HttpProxySessionInfo.MemoryStream.ToArray());
            int index, indexbe, lastindex, onLine;
            index = aBin.IndexOf("\r\n\r\n");
            if (index == -1)
            {
                if (aBin.Length > 102400)//数据大于100k还没解析出协议头  关闭
                {
                    Close(ClientErrorType.OtherError);
                }
                else if (!cTcpClient.BeginReceive(Rsaea, out ClientErrorType clientErrorType))
                {
                    Close(ClientErrorType.OtherError);
                }
                return;
            }
            HttpProxySessionInfo.HeadLength = index + 4;
            if (aBin.Length != HttpProxySessionInfo.HeadLength)
            {
                aBin = Encoding.UTF8.GetString(HttpProxySessionInfo.MemoryStream.ToArray(), 0, HttpProxySessionInfo.HeadLength);
            }
            //解析访问方式
            index = aBin.IndexOf(' ');
            if (index == -1 || index > 10)
            {
                //解析访问方式错误
                Close(ClientErrorType.OtherError);
                return;
            }
            HttpProxySessionInfo.HttpRequestType = aBin.Substring(0, index);
            //解析协议版本
            lastindex = aBin.IndexOf("\r\n", index);
            if (lastindex == -1)
            {
                //协议头错误
                Close(ClientErrorType.OtherError);
                return;
            }
            onLine = lastindex;
            indexbe = aBin.LastIndexOf(' ', lastindex);
            if (indexbe == -1)
            {
                //协议头错误
                Close(ClientErrorType.OtherError);
                return;
            }
            HttpProxySessionInfo.HttpVersion = aBin.Substring(indexbe + 1, lastindex - indexbe - 1);
            HttpProxySessionInfo.HttpPath = aBin.Substring(index + 1, indexbe - index - 1);
            //解析host

            string hostUrl = string.Empty;
            //CONNECT
            if ("CONNECT" == HttpProxySessionInfo.HttpRequestType)
            {
                hostUrl = HttpProxySessionInfo.HttpPath;
            }
            else
            {
                index = aBin.IndexOf("Host: ");
                if (index == -1)
                {
                    //协议头错误
                    Close(ClientErrorType.OtherError);
                    return;
                }
                index += 6;
                lastindex = aBin.IndexOf("\r\n", index);
                if (lastindex == -1)
                {
                    //协议头错误
                    Close(ClientErrorType.OtherError);
                    return;
                }
                hostUrl = aBin.Substring(index, lastindex - index);
            }
            if (string.IsNullOrEmpty(hostUrl))
            {
                //协议头错误
                Close(ClientErrorType.OtherError);
                return;
            }
            string[] ho = hostUrl.Split(':');
            HttpProxySessionInfo.Host = ho[0];
            if (ho.Length > 2)
            {
                //协议头错误
                Close(ClientErrorType.OtherError);
                return;
            }
            if (ho.Length == 2)
            {
                if (ushort.TryParse(ho[1], out ushort pro))
                {
                    HttpProxySessionInfo.Port = pro;
                }
                else//错误的端口
                {
                    Close(ClientErrorType.OtherError);
                    return;
                }
            }
            else
            {
                if ("CONNECT" == HttpProxySessionInfo.HttpRequestType)
                {
                    HttpProxySessionInfo.Port = 443;
                }
                else
                {
                    HttpProxySessionInfo.Port = 80;
                }
            }
            #endregion

            if (!IPAddress.TryParse(HttpProxySessionInfo.Host, out IPAddress ipaddr))
            {
                try
                {
                    IPAddress[] iplist = Dns.GetHostAddresses(HttpProxySessionInfo.Host);
                    if (iplist != null && iplist.Length > 0)
                    {
                        ipaddr = iplist[0];
                    }
                    else
                    {
                        //无法解析host
                        Close(ClientErrorType.OtherError);
                        return;
                    }
                }
                catch (Exception)
                {
                    //无法解析host
                    Close(ClientErrorType.OtherError);
                    return;
                }

            }
            byte[] ipBytes = ipaddr.GetAddressBytes();//尝试访问局域网
            if (ipBytes[0] == 10 || (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || (ipBytes[0] == 192 && ipBytes[1] == 168))
            {
                //尝试访问局域网
                Close(ClientErrorType.Danger);
                return;
            }
            if (ipBytes[0] == 127 && ipBytes[1] == 0 && ipBytes[2] == 0 && ipBytes[3] == 1 && HttpProxySessionInfo.Port != 57869)
            {
                //尝试访问本地其他端口
                Close(ClientErrorType.Danger);
                return;
            }
            IPEndPoint localEndPoint = new IPEndPoint(ipaddr, HttpProxySessionInfo.Port);
            Socket socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }
            //if (aBin.IndexOf("\r\nProxy-Connection: Keep-Alive\r\n", StringComparison.OrdinalIgnoreCase) != -1 || aBin.IndexOf("\r\nConnection: Keep-Alive\r\n") != -1)
            //{
            //    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            //}
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

            IAsyncResult waitcon = socket.BeginConnect(localEndPoint, null, null);

            if (!waitcon.AsyncWaitHandle.WaitOne(15000))
            {
                //无法连接host
                Close(ClientErrorType.OtherError);
                return;
            }
            desTcpCliet = new HHTcpClient(socket);
            desTcpCliet.OnError += DOnError;
            desTcpCliet.OnReceive += DOnReceive;
            desTcpCliet.OnSendEnd += DOnSendEnd;
            if ("CONNECT" == HttpProxySessionInfo.HttpRequestType)
            {
                //先发送
                if (Ssaea == null)
                {
                    Ssaea = new SocketAsyncEventArgs();
                }
                byte[] bin = Encoding.UTF8.GetBytes($"{HttpProxySessionInfo.HttpVersion} 200 Connection established\r\nProxy-Agent: Mentalis Proxy Server\r\n\r\n");
                Ssaea.SetBuffer(bin, 0, bin.Length);
                if (!cTcpClient.BeginSend(Ssaea, out _))
                {
                    Close(ClientErrorType.OtherError);
                    return;
                }
            }
            else
            {

                //http协议 重组包
                if (HttpProxySessionInfo.HttpPath.Length >= 7 && HttpProxySessionInfo.HttpPath.Substring(0, 7).ToLower().Equals("http://"))
                {
                    index = HttpProxySessionInfo.HttpPath.IndexOf('/', 7);
                    if (index == -1)
                        HttpProxySessionInfo.HttpPath = "/";
                    else
                        HttpProxySessionInfo.HttpPath = HttpProxySessionInfo.HttpPath.Substring(index);
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append($"{HttpProxySessionInfo.HttpRequestType} {HttpProxySessionInfo.HttpPath} {HttpProxySessionInfo.HttpVersion}\r\n");
                string[] heads = aBin.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                //排除掉第一行
                for (int i = 1; i < heads.Length; i++)
                {
                    if (heads[i].Length > 18 && heads[i].Substring(0, 16).ToLower() == "proxy-connection")
                    {
                        if (heads[i].Substring(18).ToLower() == "keep-alive")
                        {
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        }
                        heads[i] = $"Connection: " + heads[i].Substring(18);
                    }
                    else if (heads[i].Length > 12 && heads[i].Substring(0, 10).ToLower() == "connection")
                    {
                        if (heads[i].Substring(12).ToLower() == "keep-alive")
                        {
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        }
                    }
                    stringBuilder.Append(heads[i]);
                    stringBuilder.Append("\r\n");
                }
                stringBuilder.Append("\r\n");

                //if (aBin.IndexOf("\r\nProxy-Connection: Keep-Alive\r\n") == -1)
                //{
                //    stringBuilder.Append(aBin.Substring(onLine, HttpProxySessionInfo.HeadLength - onLine));
                //}
                //else
                //{
                //    stringBuilder.Append(aBin.Substring(onLine, HttpProxySessionInfo.HeadLength - onLine).Replace("\r\nProxy-Connection: Keep-Alive\r\n", "\r\nConnection: Keep-Alive\r\n"));

                //}
                //是否有非头数据
                byte[] bin;
                if (HttpProxySessionInfo.MemoryStream.Length > HttpProxySessionInfo.HeadLength)
                {
                    bin = new byte[stringBuilder.Length + HttpProxySessionInfo.MemoryStream.Length - HttpProxySessionInfo.HeadLength];
                    Encoding.UTF8.GetBytes(stringBuilder.ToString()).CopyTo(bin, 0);
                    HttpProxySessionInfo.MemoryStream.Position = HttpProxySessionInfo.HeadLength;

                    HttpProxySessionInfo.MemoryStream.Read(bin, stringBuilder.Length, (int)HttpProxySessionInfo.MemoryStream.Length - HttpProxySessionInfo.HeadLength);
                }
                else
                {
                    bin = Encoding.UTF8.GetBytes(stringBuilder.ToString());
                }
                Console.WriteLine(Encoding.UTF8.GetString(bin));
                //if (HttpProxySessionInfo.HttpPath.IndexOf("http://") == 0)//需要重组头部
                //{
                //    index = HttpProxySessionInfo.HttpPath.IndexOf('/', 7);
                //    if (index == -1)
                //        HttpProxySessionInfo.HttpPath = "/";
                //    else
                //        HttpProxySessionInfo.HttpPath = HttpProxySessionInfo.HttpPath.Substring(index);
                //    bin = Encoding.UTF8.GetBytes($"{HttpProxySessionInfo.HttpRequestType} {HttpProxySessionInfo.HttpPath} {HttpProxySessionInfo.HttpVersion}{aBin.Substring(onLine, HttpProxySessionInfo.HeadLength - onLine)}");
                //}
                //else
                //{
                //    //Proxy-Connection: Keep-Alive
                //    if (aBin.IndexOf("\r\nProxy-Connection: Keep-Alive\r\n") != -1)
                //    {
                //        bin = Encoding.UTF8.GetBytes(aBin.Replace("\r\nProxy-Connection: Keep-Alive\r\n", "\r\nConnection: Keep-Alive\r\n"));
                //    }
                //    else
                //    {
                //        bin = HttpProxySessionInfo.MemoryStream.ToArray();
                //    }

                //}



                //http 发送所有数据给目标
                if (DSsaea == null)
                {
                    DSsaea = new SocketAsyncEventArgs();
                }
                DSsaea.SetBuffer(bin, 0, bin.Length);
                if (!desTcpCliet.BeginSend(DSsaea, out var errorType))
                {
                    Close(errorType);
                    return;
                }
            }
        }
        /// <summary>
        /// 目的地发送数据结束
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="saea"></param>
        /// <param name="clientErrorType"></param>
        private void DOnSendEnd(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType)
        {
            if (clientErrorType != ClientErrorType.Success)
            {
                Close(clientErrorType);
            }
            else
            {
                if (connectedOver == 0)
                {
                    connectedOver = 1;
                    if (DRsaea == null)
                    {
                        int l = ReceiveBufferSize;
                        DRsaea = new SocketAsyncEventArgs();
                        DReBuffer = new byte[l];
                        DRsaea.SetBuffer(DReBuffer, 0, DReBuffer.Length);
                    }
                    if (!desTcpCliet.BeginReceive(DRsaea, out var errorType))
                    {
                        Close(errorType);
                    }
                    //Console.WriteLine($"连接已建立");
                }
                HttpProxySessionInfo.AddSendNum(saea.BytesTransferred);
                //Console.WriteLine($"发送给目的地 {saea.BytesTransferred}");
                if (!cTcpClient.BeginReceive(Rsaea, out var errorType1))
                {
                    Close(errorType1);
                }
            }
        }
        /// <summary>
        /// 目的地接收到数据
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="saea"></param>
        /// <param name="clientErrorType"></param>
        private void DOnReceive(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType)
        {
            if (clientErrorType != ClientErrorType.Success)
            {
                Close(clientErrorType);
            }
            else
            {
                if (Ssaea == null)
                {
                    Ssaea = new SocketAsyncEventArgs();
                }
                //直接转发就行了
                Ssaea.SetBuffer(saea.Buffer, saea.Offset, saea.BytesTransferred);
                if (!cTcpClient.BeginSend(Ssaea, out var errorType))
                {
                    Close(errorType);
                }
            }
        }
        /// <summary>
        /// 目的地错误时
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="clientErrorType"></param>
        /// <param name="exc"></param>
        private void DOnError(HHTcpClient tcpClient, ClientErrorType clientErrorType, Exception exc)
        {
            Close(clientErrorType);
        }

        /// <summary>
        /// 客户端异常时
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="clientErrorType"></param>
        /// <param name="exc"></param>
        private void OnError(HHTcpClient tcpClient, ClientErrorType clientErrorType, Exception exc)
        {
            Close(clientErrorType);
        }
        /// <summary>
        /// 结束本地代理会话
        /// </summary>
        public void Close(ClientErrorType clientErrorType)
        {
            if (Interlocked.CompareExchange(ref Closing, 1, 0) == 0)
            {
                desTcpCliet?.Close();
                cTcpClient.Close();

                Rsaea?.Dispose();
                DRsaea?.Dispose();

                Ssaea?.Dispose();
                DSsaea?.Dispose();

                OnClose?.Invoke(HttpProxySessionInfo, clientErrorType);
            }
        }
    }

}
