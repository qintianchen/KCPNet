using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;

namespace KCPNet
{
    public class KCPSession
    {
        public enum SessionState
        {
            None,
            Connected,
            DisConnected
        }

        /// 会话ID
        public uint sid;
        /// 目标地址
        private IPEndPoint remoteIPEndPoint;
        /// 会话状态
        public SessionState sessionState;
        /// Kcp 算法实例
        public Kcp kcp;
        
        private KCPHandler kcpHandler;
        private CancellationTokenSource kcpUpdateCTS;
        private Action<byte[]> onKCPReceive;
        
        public KCPSession(uint sid, IPEndPoint remoteIPEndPoint, Action<byte[]> onKCPOutput, Action<byte[]> onKCPReceive)
        {
            // 初始化会话基本信息
            this.sid = sid;
            this.remoteIPEndPoint = remoteIPEndPoint;
            this.onKCPReceive = onKCPReceive;
            
            // 初始化 KCP
            kcpHandler = new KCPHandler(onKCPOutput);
            kcp = new Kcp(sid, kcpHandler);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(64, 64);
            kcp.SetMtu(512);

            // 开一个线程异步循环驱动KCP定时更新状态
            kcpUpdateCTS = new CancellationTokenSource();
            Task.Run(Update, kcpUpdateCTS.Token);
            
            sessionState = SessionState.Connected;
        }

        /// 关闭会话
        public void Close()
        {
            // 终止 KCP 驱动
            kcpUpdateCTS?.Cancel();

            // 清理状态
            sessionState = SessionState.DisConnected;
            sid = 0;
            remoteIPEndPoint = null;
            kcp = null;
            kcpHandler = null;
            onKCPReceive = null;
        }
        
        // 循环驱动KCP
        private async void Update()
        {
            try
            {
                while (true)
                {
                    DateTime now = DateTime.Now;
                    if (kcpUpdateCTS.Token.IsCancellationRequested)
                    {
                        KCPNetLogger.Info("KCPSession update task is cancelled");
                        break;
                    }
                    else
                    {
                        kcp.Update(now);
                        int len;
                        while ((len = kcp.PeekSize()) > 0)
                        {
                            byte[] buffer = new byte[len];
                            if (kcp.Recv(buffer) >= 0)
                            {
                                OnKCPReceive(buffer);
                            }
                        }

                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception e)
            {
                KCPNetLogger.Warning($"Session update exception {e}");
            }
        }

        // 处理从KCP发往上层应用的消息（是经过KCP解包处理的，来自UDP层的消息）
        private void OnKCPReceive(byte[] bytesReceived)
        {
            // 会话只负责解压消息，序列化的任务放权给上层应用
            var buffer = Utils.DeCompress(bytesReceived);
            onKCPReceive?.Invoke(buffer);
        }
    }

    public class KCPHandler : IKcpCallback
    {
        private Action<byte[]> onOutput;

        public KCPHandler(Action<byte[]> onOutput)
        {
            this.onOutput = onOutput;
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            using (buffer)
            {
                onOutput?.Invoke(buffer.Memory.Slice(0, avalidLength).ToArray());
            }
        }
    }
}