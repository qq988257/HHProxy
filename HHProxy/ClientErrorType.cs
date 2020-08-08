namespace HHProxy
{
    public enum ClientErrorType
    {
        /// <summary>
        /// 无错误
        /// </summary>
        Success = 0,
        /// <summary>
        /// 主动正常关闭 (自己正常断开连接)
        /// </summary>
        ActivelyClose,
        /// <summary>
        /// 被动正常关闭 (对方正常断开连接)
        /// </summary>
        PassiveClose,
        /// <summary>
        /// 设置开始接收中状态异常
        /// </summary>
        ToReceiveError,
        /// <summary>
        /// 设置结束接收中状态异常
        /// </summary>
        EndReceiveError,
        /// <summary>
        /// 设置开始接收中状态异常
        /// </summary>
        ToSendError,
        /// <summary>
        /// 设置结束接收中状态异常
        /// </summary>
        EndSendError,
        /// <summary>
        /// socket错误
        /// </summary>
        SocketError,

        /// <summary>
        /// 其他错误
        /// </summary>
        OtherError,
        /// <summary>
        /// 危险的本地访问
        /// </summary>
        Danger,
    }

}
