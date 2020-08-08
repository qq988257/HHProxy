using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HHProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            HHTcpListener hHTcpListener = new HHTcpListener(IPAddress.Any, 59000);
            hHTcpListener.OnAccept += OnAccept;
            hHTcpListener.OnError += OnError;
            if (hHTcpListener.Start())
            {
                Console.WriteLine("启动成功");
            }
            else
            {
                Console.WriteLine("启动失败");
            }
            Console.ReadLine();
        }

        private static void OnError(Socket client, Exception exc)
        {
            Console.WriteLine($"监听发送socket错误 {client.RemoteEndPoint?.ToString()} {exc.Message}");
        }

        private static void OnAccept(Socket client)
        {
            Console.WriteLine($"收到连接 {client.RemoteEndPoint.ToString()}");
            HttpProxySession httpProxySession = new HttpProxySession(client);
            httpProxySession.OnClose += OnPorxyOver;
            httpProxySession.Start();
        }

        private static void OnPorxyOver(HttpProxySessionInfo httpProxySessionInfo, ClientErrorType errorType)
        {
            string msg = $"本次代理结束:{httpProxySessionInfo.HttpRequestType} {httpProxySessionInfo.HttpPath} {httpProxySessionInfo.HttpVersion} {Environment.NewLine}"
              + $"Host: {httpProxySessionInfo.Host} {Environment.NewLine}"
              + $"Port: {httpProxySessionInfo.Port} {Environment.NewLine}"
              + $"累计转发到目的地: {httpProxySessionInfo.AllSendLength} {Environment.NewLine}"
              + $"累计转发到客户端: {httpProxySessionInfo.AllRetLength} {Environment.NewLine}"
              + $"结束原因: {errorType.ToString()} {Environment.NewLine}"
            + $"持续时间: {(httpProxySessionInfo.LastActionTime - httpProxySessionInfo.ConnectTime).TotalMilliseconds}ms {Environment.NewLine}"
            + $"当前时间: {DateTime.Now.ToLocalTime().ToString()} {Environment.NewLine}"
            + $"连接时间: {httpProxySessionInfo.ConnectTime.ToLocalTime().ToString()} {Environment.NewLine}"
            + $"最后连接时间: {httpProxySessionInfo.LastActionTime.ToLocalTime().ToString()} {Environment.NewLine}";
            Console.WriteLine(msg);
        }

    }


}
