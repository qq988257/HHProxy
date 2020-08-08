using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HHProxy
{
    public delegate void ClientReceiveHandle(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType);
    public delegate void ClientSendEndHandle(HHTcpClient tcpClient, SocketAsyncEventArgs saea, ClientErrorType clientErrorType);
    //public delegate void ClientCloseHandle(HHTcpClient tcpClient, ClientErrorType clientCloseType);
    public delegate void ClientErrorHandle(HHTcpClient tcpClient, ClientErrorType clientErrorType, Exception exc);

    /// <summary>
    /// 已创建连接的客户端
    /// </summary>
    public class HHTcpClient
    {
        public HHTcpClient(Socket client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Handle = client.Handle;
        }
        public IntPtr Handle { get; private set; }
        public Socket Client { get; private set; }
        /// <summary>
        /// 当前运行状态 
        /// </summary>
        public bool IsRuning { get => isRuning && receiving != -1 && sending != -1; private set => isRuning = value; }
        bool isRuning = true;
        /// <summary>
        /// 遇到错误时触发事件
        /// </summary>
        public event ClientErrorHandle OnError;
        /// <summary>
        /// 收到数据时触发事件
        /// </summary>
        public event ClientReceiveHandle OnReceive;
        /// <summary>
        /// 发送数据完成后触发事件
        /// </summary>
        public event ClientSendEndHandle OnSendEnd;
        private int receiving;
        /// <summary>
        /// 接收中状态
        /// </summary>
        public int Receiving => receiving;
        /// <summary>
        /// 设置为接收中状态
        /// </summary>
        /// <returns></returns>
        public bool ToReceiving() => Interlocked.CompareExchange(ref receiving, 1, 0) == 0;
        /// <summary>
        /// 接收完毕
        /// </summary>
        /// <returns></returns>
        public bool EndReceive() => Interlocked.CompareExchange(ref receiving, 0, 1) == 1;
        /// <summary>
        /// 接收异常
        /// </summary>
        public void ReceiveError() => Interlocked.Exchange(ref receiving, -1);
        /// <summary>
        /// 开始接收
        /// </summary>
        /// <param name="saea"></param>
        /// <param name="clientErrorType"></param>
        /// <returns></returns>
        public bool BeginReceive(SocketAsyncEventArgs saea, out ClientErrorType clientErrorType)
        {
            if (!IsRuning)
            {
                clientErrorType = ClientErrorType.SocketError;
                return false;
            }
            if (saea == null)
            {
                throw new ArgumentNullException(nameof(saea));
            }
            if (!ToReceiving())
            {
                clientErrorType = ClientErrorType.ToReceiveError;
                return false;
            }
            if (saea.UserToken == null)
            {
                saea.UserToken = this;
                saea.Completed += new EventHandler<SocketAsyncEventArgs>(RsaeaOnReceive);
            }
            if (!Client.ReceiveAsync(saea))
            {
                ProcessReceive(saea);
            }
            clientErrorType = ClientErrorType.Success;
            return true;
        }


        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            HHTcpClient tcpClient = (HHTcpClient)e.UserToken;
            if (tcpClient == null)
            {

            }
            if (!tcpClient.Equals(this))
            {

            }
            if (e.LastOperation != SocketAsyncOperation.Receive)//不是接收事件
            {
                //throw new AppDomainUnloadedException();
            }

            if (e.SocketError == SocketError.Success)
            {
                //无socket错误
                if (e.BytesTransferred <= 0)
                {
                    tcpClient.ReceiveError();
                    //连接被关闭
                    tcpClient.OnReceive?.Invoke(tcpClient, e, ClientErrorType.PassiveClose);
                    return;
                }
                if (!tcpClient.EndReceive())
                {
                    tcpClient.ReceiveError();
                    tcpClient.OnReceive?.Invoke(tcpClient, e, ClientErrorType.EndReceiveError);
                    return;
                }
                tcpClient.OnReceive?.Invoke(tcpClient, e, ClientErrorType.Success);
                return;
            }
            else
            {
                tcpClient.ReceiveError();

                tcpClient.OnReceive?.Invoke(tcpClient, e, ClientErrorType.SocketError);

                tcpClient.OnError?.Invoke(tcpClient, ClientErrorType.SocketError, new SocketException((int)e.SocketError));
                //Error(connectClient, new SocketException((int)e.SocketError), "接收数据时异常");
                return;
            }
        }

        private void RsaeaOnReceive(object sender, SocketAsyncEventArgs e)
        {
            ProcessReceive(e);
        }
        /// <summary>
        /// 清理saea对象以方便重用
        /// </summary>
        /// <param name="saea"></param>
        /// <param name="isReceive"></param>
        public void ClearSaea(SocketAsyncEventArgs saea, bool isReceive)
        {
            if (saea == null)
            {
                return;
            }
            if (saea.UserToken == null)
            {
                return;
            }
            if (isReceive)
            {
                saea.Completed -= new EventHandler<SocketAsyncEventArgs>(((HHTcpClient)saea.UserToken).RsaeaOnReceive);
            }
            else
            {
                saea.Completed -= new EventHandler<SocketAsyncEventArgs>(((HHTcpClient)saea.UserToken).SsaeaOnSend);
            }
            saea.UserToken = null;
            if (!isReceive)
            {
                if (saea.Buffer != null)
                {
                    saea.SetBuffer(null, 0, 0);
                }
                if (saea.BufferList != null)
                {
                    saea.BufferList = null;
                }

            }
        }
        public bool BeginSend(SocketAsyncEventArgs saea, out ClientErrorType clientErrorType)
        {
            if (!IsRuning)
            {
                clientErrorType = ClientErrorType.SocketError;
                return false;
            }
            if (saea == null)
            {
                throw new ArgumentNullException(nameof(saea));
            }
            if (!ToSending())
            {
                clientErrorType = ClientErrorType.ToSendError;
                return false;
            }
            if (saea.UserToken == null)
            {
                saea.UserToken = this;
                saea.Completed += new EventHandler<SocketAsyncEventArgs>(SsaeaOnSend);
            }
            if (!Client.SendAsync(saea))
            {
                ProcessSend(saea);
            }
            clientErrorType = ClientErrorType.Success;
            return true;
        }

        private void SsaeaOnSend(object sender, SocketAsyncEventArgs e)
        {
            ProcessSend(e);
        }
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            HHTcpClient tcpClient = (HHTcpClient)e.UserToken;
            if (tcpClient == null)
            {

            }
            if (!tcpClient.Equals(this))
            {

            }
            if (e.LastOperation != SocketAsyncOperation.Send)//不是接收事件
            {
                //throw new AppDomainUnloadedException();
            }

            if (e.SocketError == SocketError.Success)
            {
                //无socket错误
                if (e.BytesTransferred <= 0)
                {
                    tcpClient.SendError();
                    //连接被关闭
                    tcpClient.OnSendEnd?.Invoke(tcpClient, e, ClientErrorType.PassiveClose);
                    return;
                }
                if (!tcpClient.EndSend())
                {
                    tcpClient.SendError();
                    //设置接收数据完毕状态失败
                    tcpClient.OnSendEnd?.Invoke(tcpClient, e, ClientErrorType.EndSendError);
                    return;
                }
                tcpClient.OnSendEnd?.Invoke(tcpClient, e, ClientErrorType.Success);
            }
            else
            {
                tcpClient.SendError();
                tcpClient.OnSendEnd?.Invoke(tcpClient, e, ClientErrorType.SocketError);
                tcpClient.OnError?.Invoke(tcpClient, ClientErrorType.SocketError, new SocketException((int)e.SocketError));
                //Error(connectClient, new SocketException((int)e.SocketError), "接收数据时异常");
                return;
            }
        }
        private int sending;
        /// <summary>
        /// 发送中状态
        /// </summary>
        public int Sending => sending;
        /// <summary>
        /// 设置为发送中状态
        /// </summary>
        /// <returns></returns>
        public bool ToSending() => Interlocked.CompareExchange(ref sending, 1, 0) == 0;
        /// <summary>
        /// 发送完毕
        /// </summary>
        /// <returns></returns>
        public bool EndSend() => Interlocked.CompareExchange(ref sending, 0, 1) == 1;
        /// <summary>
        /// 发送失败
        /// </summary>
        public void SendError() => Interlocked.Exchange(ref sending, -1);

        public void Close()
        {
            if (Client == null)
            {
                return;
            }
            IsRuning = false;
            try { Client.Shutdown(SocketShutdown.Both); } catch { }
            try { Client.Close(); } catch { }
            int wi = 0;
            while (sending != -1 || receiving != -1)
            {
                if (sending != -1)
                {
                    Interlocked.CompareExchange(ref sending, -1, 0);
                }
                if (receiving != -1)
                {
                    Interlocked.CompareExchange(ref receiving, -1, 0);
                }
                if ((sending != -1 || receiving != -1) && wi++ > 100)
                {
                    Thread.Sleep(10);
                    wi = 0;
                }

            }
            Client = null;
        }
    }

}
