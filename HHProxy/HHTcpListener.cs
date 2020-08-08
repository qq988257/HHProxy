using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HHProxy
{
    public delegate void TcpAcceptHandle(Socket client);
    public delegate void TcpErrorHandle(Socket client, Exception exc);
    public class HHTcpListener
    {
        private SocketAsyncEventArgs Msaea;
        public HHTcpListener(IPAddress address, ushort port)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
        }
        public IPAddress Address { get; private set; }
        public ushort Port { get; private set; }
        public bool IsRuning { get; private set; } = false;
        public Socket Socket { get; private set; }
        public event TcpAcceptHandle OnAccept;
        public event TcpErrorHandle OnError;
        /// <summary>
        /// 已执行关闭
        /// </summary>
        int Closing = 0;
        public bool Start(int backLog = 10)
        {
            if (IsRuning)
            {
                return false;
            }
            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(Address, Port);
                Socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    Socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                    Socket.Bind(new IPEndPoint(IPAddress.IPv6Any, localEndPoint.Port));
                }
                else
                {
                    Socket.Bind(localEndPoint);
                }
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

                Socket.Listen(backLog);

                Msaea = new SocketAsyncEventArgs();
                Msaea.Completed += new EventHandler<SocketAsyncEventArgs>(Msaea_Completed);
                IsRuning = true;
                if (!Socket.AcceptAsync(Msaea))
                {
                    ProcessAccept(Msaea);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Socket socket = null;
            if (e.SocketError != SocketError.Success)
            {
                OnError?.Invoke(this.Socket, new SocketException((int)e.SocketError));
                //var errorCode = (int)e.SocketError;

                //The listen socket was closed
                //if (errorCode == 995 || errorCode == 10004 || errorCode == 10038)
                //{
                //    Error(new SocketException(errorCode), "接受客户连接时异常");
                //    return;
                //}
                //this.SerConfig.Error(new SocketException(errorCode));
            }
            else
            {
                socket = e.AcceptSocket;
            }
            e.AcceptSocket = null;//下一次开始接受需要设置null;
            bool canContinue;
            try
            {
                canContinue = Socket.AcceptAsync(e);
            }
            catch (ObjectDisposedException)
            {
                canContinue = true;
            }
            catch (NullReferenceException)
            {
                canContinue = true;
            }
            catch (Exception exc)
            {
                OnError?.Invoke(this.Socket, exc);
                //this.SerConfig.Error(exc);
                canContinue = true;
            }
            if (socket != null)
            {
                OnAccept?.Invoke(socket);
                //CreateConSession(socket);
            }
            if (!canContinue)
            {
                ProcessAccept(e);
            }
        }

        private void Msaea_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }
        /// <summary>
        /// 结束本地代理会话
        /// </summary>
        public void Close()
        {
            IsRuning = false;
            if (Socket == null)
            {
                return;
            }
            if (Interlocked.CompareExchange(ref Closing, 1, 0) == 0)
            {
                //desTcpCliet?.Close();
                //cTcpClient.Close();
                try { Socket.Shutdown(SocketShutdown.Both); } catch { }
                try { Socket.Close(); } catch { }
                Msaea?.Dispose();

            }
        }
    }


}
