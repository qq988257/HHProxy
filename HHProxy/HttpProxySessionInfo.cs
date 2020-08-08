using System;
using System.IO;
using System.Threading;

namespace HHProxy
{
    public class HttpProxySessionInfo
    {
        /// <summary>
        /// 访问类型
        /// </summary>
        public string HttpRequestType { get; set; }
        /// <summary>
        /// http版本
        /// </summary>
        public string HttpVersion { get; set; }
        /// <summary>
        /// http页面地址
        /// </summary>
        public string HttpPath { get; set; }
        /// <summary>
        /// 目标地址
        /// </summary>
        public string Host { get; set; }
        /// <summary>
        /// 目标端口
        /// </summary>
        public ushort Port { get; set; }
        /// <summary>
        /// 头部长度
        /// </summary>
        public int HeadLength { get; set; } = -1;
        public DateTime ConnectTime { get; set; }
        public DateTime LastActionTime { get; private set; }
        /// <summary>
        /// 发送的数据流(https只记录协议头)
        /// </summary>
        public MemoryStream MemoryStream { get; set; }
        /// <summary>
        /// 转发给目的地的长度
        /// </summary>
        public int AllSendLength { get => allSendLength; }

        int allSendLength = 0;
        internal void AddSendNum(int num)
        {
            Interlocked.Add(ref allSendLength, num);
            LastActionTime = DateTime.Now;
        }
        /// <summary>
        /// 收到的数据流(https不会记录)
        /// </summary>
        public MemoryStream MemoryStreamRe { get; set; }
        /// <summary>
        /// 转发给客户端数据长度
        /// </summary>
        public int AllRetLength { get => allRetLength; }

        int allRetLength = 0;

        public HttpProxySessionInfo()
        {
            ConnectTime = LastActionTime = DateTime.Now;
        }

        internal void AddRetNum(int num)
        {
            Interlocked.Add(ref allRetLength, num);
            LastActionTime = DateTime.Now;
        }
    }

}
