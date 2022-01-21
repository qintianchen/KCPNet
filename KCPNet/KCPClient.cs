using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// 我们使用UDP和远端服务器通信，并尝试建立与远端服务器的连接
// 因为客户端和服务器建立连接和通信的过程为：
// 客户端向服务器发送一个前四个字节为0的消息
// 服务器收到消息，判断前四个字节为0，则认为此时为一个新的客户端请求连接，并为其分配一个全局唯一的 sid > 0，返回的消息前四个字节为0，sid则紧随其后
// 客户端收到消息，判断前四个字节为0，则认为此时服务器尝试分配sid，客户端以该sid创建 KCPSession，从此以 Kcp 为基础与服务器通信。
//
// 我们为 KCPSession 分配的 sid 同时也是Kcp实例的 conv_id，Kcp算法会将该ID作为KCP包头的前四个字节，因此，往后服务器和客户端在以 KCPSession 相互通信的时候，前四个字节一定是 SID

namespace KCPNet
{
    // KCP的客户端
    public class KCPClient
    {
        public UdpClient udpClient;
        public IPEndPoint remoteIPEndPoint;
        public Action<byte[]> onKCPReceive;
        private KCPSession kcpSession;
        private CancellationTokenSource clientRecvCTS;

        /// 启动客户端，开始接收来自目标地址的消息
        public void Start(string ip, int port)
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            remoteIPEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            clientRecvCTS = new CancellationTokenSource();
            Task.Run(ReceiveAsyc, clientRecvCTS.Token); // 异步从服务器接收消息，用 clientRecvCTS 来管理异步的生命周期
        }

        /// 连接到服务器，并建立起一个 KCP 会话
        public Task<bool> TryConnectToServer(int interval = 200, int timeout = 5000)
        {
            SendUDPMessage(new byte[4]);
            int totalTime = 0;

            Task<bool> task = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);
                    totalTime += interval;
                    if (kcpSession != null && kcpSession.sessionState == KCPSession.SessionState.Connected)
                    {
                        return true;
                    }

                    if (totalTime < timeout) continue;
                    
                    // 连接超时
                    KCPNetLogger.Warning($"连接到服务器: {remoteIPEndPoint} 超时");
                    return false;
                }
            });

            return task;
        }

        /// 向服务器发送消息
        public bool SendMessage(byte[] bytesToSend)
        {
            if (udpClient == null || remoteIPEndPoint == null) return false;

            var buffer = Utils.Compress(bytesToSend);
            udpClient.SendAsync(buffer, buffer.Length, remoteIPEndPoint);
            return true;
        }

        public void Close()
        {
            kcpSession?.Close();
            udpClient.Close();
            remoteIPEndPoint = null;
            onKCPReceive = null;
            
            // 终止从服务器接收消息
            clientRecvCTS.Cancel();
        }

        #region private

        /// 异步循环从服务器接收消息 
        private async void ReceiveAsyc()
        {
            UdpReceiveResult result;
            while (true)
            {
                try
                {
                    if (clientRecvCTS.IsCancellationRequested)
                    {
                        KCPNetLogger.Info("[Client] Client receive task is cancel");
                        break;
                    }

                    // 循环从UDP接收消息
                    result = await udpClient.ReceiveAsync();
                    if (Equals(remoteIPEndPoint, result.RemoteEndPoint))
                    {
                        // 处理服务器收到的消息
                        var bytesReceived = result.Buffer;
                        OnReceive(bytesReceived);
                    }
                    else
                    {
                        // 拦截非法远端发来的消息
                        KCPNetLogger.Warning("Client udp receive data from an illegal target");
                    }
                }
                catch (Exception e)
                {
                    KCPNetLogger.Warning($"Client receive data exception: {e}");
                }
            }
        }

        /// 处理从服务器接收到的消息
        private void OnReceive(byte[] bytes)
        {
            uint sid = BitConverter.ToUInt32(bytes, 0);
            if (sid == 0)
            {
                if (kcpSession != null && kcpSession.sessionState == KCPSession.SessionState.Connected)
                {
                    // 会话已经建立过了，直接抛弃掉消息
                    KCPNetLogger.Info("[Client] 会话已经建立，抛弃消息");
                    return;
                }

                // sid == 0，此时，客户端认为服务器在尝试为自己分配 sid，解析出 sid，并建立通信会话
                sid = BitConverter.ToUInt32(bytes, 4);
                kcpSession = new KCPSession(sid, remoteIPEndPoint, SendUDPMessage, onKCPReceive);
                KCPNetLogger.Info("[Client] 建立会话ing...");
            }
            else
            {
                // 服务器之前已经和客户端建立过会话，并正常通信
                if (kcpSession == null || kcpSession.sessionState != KCPSession.SessionState.Connected)
                {
                    // 会话并不存在，收到的消息作废
                    KCPNetLogger.Warning("[Client] KCPSession does not exist");
                    return;
                }

                // 将来自UDP的消息Push进Kcp进行解包处理，处理后的消息可以在 KCP Update 中获取到
                kcpSession.kcp.Input(bytes);
            }
        }

        /// 使用 UDP 将一段字节序列发送往远端
        private void SendUDPMessage(byte[] bytesToSend)
        {
            udpClient?.SendAsync(bytesToSend, bytesToSend.Length, remoteIPEndPoint);
        }

        #endregion
    }
}