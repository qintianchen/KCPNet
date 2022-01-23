using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;

/*
KCP模块总共有四个对外数据接口，两个输出两个输入，分别对接上层表现应用层和下层UDP传输层。
来自上层的输入为 kcp.Send，输出到上层的输出为 onKCPReceive 回调，这个回调的调用依赖外部驱动 kcp 算法模块，定期从 kcp 内部缓存中读取接收自 UDP 层的数据
来自下层的输入为 kcp.Input，输出到下层 UDP 的输出为 onKCPOutput，在初始化 kcp 算法模块的时候需要显式指定

来自 UDP 的数据经由 Input 接口被放进 kcp 模块中解包处理，得到没有 kcp 包头的上层数据，这些数据被 kcp 缓存。外部定时更新 kcp 状态，并从 kcp 缓存中读取数据，通过 onKCPReceive 发往上层应用
上层应用的数据经由 Send 接口被放进 kcp 模块中处理，数据加入了 kcp 包头，并通过 onKCPOutput 发往 UDP。
*/

namespace KCPNet
{
    public class KCPSession
    {
        public enum SessionState
        {
            Connected,
            DisConnected
        }

        /// 会话ID
        public uint sid;
        /// 目标地址
        public IPEndPoint remoteIPEndPoint;

        /// 会话状态
        public SessionState sessionState;
        /// Kcp 算法实例
        public Kcp kcp;
        
        private KCPHandler kcpHandler;
        private CancellationTokenSource kcpUpdateCTS;
        private Action<byte[], KCPSession> onKCPReceive;
        
        public KCPSession(uint sid, IPEndPoint remoteIPEndPoint, Action<byte[]> onKCPOutput, Action<byte[], KCPSession> onKCPReceive)
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
                    DateTime now = DateTime.UtcNow;
                    if (kcpUpdateCTS.Token.IsCancellationRequested)
                    {
                        KCPNetLogger.Info("KCPSession update task is cancelled");
                        break;
                    }

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

                    await Task.Delay(10); // 10 ms，即 100 帧的速率更新KCP
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
            onKCPReceive?.Invoke(bytesReceived, this);
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