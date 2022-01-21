using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace KCPNet
{
    // KCP 服务器
    public class KCPServer
    {
        public UdpClient udpClient;
        public IPEndPoint ipEndPoint;
        public Action<byte[], IPEndPoint> onKCPReceive;
        public Action<IPEndPoint> onClientSessionCreated;
        private CancellationTokenSource clientRecvCTS;

        private Dictionary<uint, KCPSession> map_sid_session = new Dictionary<uint, KCPSession>();
        private List<KCPSession> sessionList = new List<KCPSession>();
        private bool isSessionListDirty = false;
        
        /// 启动客户端，开始接收来自目标地址的消息
        public void Start(string ip, int port)
        {
            curSID = 0;
            map_sid_session.Clear();
            ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            udpClient = new UdpClient(ipEndPoint);

            clientRecvCTS = new CancellationTokenSource();
            Task.Run(ReceiveAsyc, clientRecvCTS.Token); // 异步从服务器接收消息，用 clientRecvCTS 来管理异步的生命周期
            
            KCPNetLogger.Info($"[Server] Server starts on: {ipEndPoint}");
        }

        /// 向服务器发送消息
        public bool SendMessage(byte[] bytesToSend, IPEndPoint remoteIPEndPoint)
        {
            if (udpClient == null || remoteIPEndPoint == null) return false;

            var buffer = Utils.Compress(bytesToSend);
            udpClient.SendAsync(buffer, buffer.Length, remoteIPEndPoint);
            return true;
        }

        /// 广播消息
        public bool BroadcastMessage(byte[] bytesToSend)
        {
            if (udpClient == null ) return false;

            lock (sessionList)
            {
                if (isSessionListDirty)
                {
                    sessionList = map_sid_session.Values.ToList();
                    isSessionListDirty = false;
                }
                
                var count = sessionList.Count;
                for (int i = 0; i < count; i++)
                {
                    var session = sessionList[i];
                    var buffer = Utils.Compress(bytesToSend);
                    udpClient.SendAsync(buffer, buffer.Length, session.remoteIPEndPoint);
                }
            }
            
            return true;
        }

        // 关闭与某客户端的会话
        public bool DisconnectBySID(uint sid)
        {
            lock (map_sid_session)
            {
                if (!map_sid_session.TryGetValue(sid, out var session))
                {
                    return false;
                }

                map_sid_session.Remove(sid);
                isSessionListDirty = true;
                return true;
            }
        }
        
        public void Close()
        {
            // 终止从客户端接收消息
            clientRecvCTS.Cancel();
            
            curSID = 0;
            map_sid_session.Clear();
            udpClient.Close();
            ipEndPoint = null;
            onKCPReceive = null;
        }

        #region private

        /// 异步循环从客户端接收消息
        private async void ReceiveAsyc()
        {
            UdpReceiveResult result;
            while (true)
            {
                try
                {
                    if (clientRecvCTS.IsCancellationRequested)
                    {
                        KCPNetLogger.Info("Client receive task is cancel");
                        break;
                    }

                    // 循环从UDP接收消息
                    result = await udpClient.ReceiveAsync();

                    // 处理收到的消息
                    var bytesReceived = result.Buffer;
                    OnReceive(bytesReceived, result.RemoteEndPoint);
                }
                catch (Exception e)
                {
                    KCPNetLogger.Warning($"Client receive data exception: {e}");
                }
            }
        }

        /// 处理从客户端接收到的消息
        private void OnReceive(byte[] bytes, IPEndPoint remoteIPEndPoint)
        {
            uint sid = BitConverter.ToUInt32(bytes, 0);
            if (sid == 0)
            {
                // 说明是一个新的客户端，并在请求分配SID
                KCPNetLogger.Info($"[Server] New Client is trying to connect: {remoteIPEndPoint}");
                sid = GenerateUniqueSID();
                byte[] sidBytes = BitConverter.GetBytes(sid);
                byte[] bytesToSend = new byte[8];
                Array.Copy(sidBytes, 0, bytesToSend, 4, 4);
                SendUDPMessage(bytesToSend, remoteIPEndPoint);
            }
            else
            {
                if (!map_sid_session.TryGetValue(sid, out var session))
                {
                    KCPNetLogger.Info($"[Server] Create a new session for client: {remoteIPEndPoint}");
                    // 客户端确认了服务器为其分配的SID，则服务器需要为其建立会话以供后续通信
                    session = new KCPSession(sid, remoteIPEndPoint, bytes2 =>
                    {
                        SendUDPMessage(bytes2, remoteIPEndPoint);
                    }, bytes3 =>
                    {
                        onKCPReceive?.Invoke(bytes3, remoteIPEndPoint);
                    });
                    lock (map_sid_session)
                    {
                        map_sid_session.Add(sid, session);
                        sessionList.Add(session);
                        onClientSessionCreated?.Invoke(remoteIPEndPoint);
                    }
                }
                else
                {
                    session = map_sid_session[sid];
                }

                session.kcp.Input(bytes);
            }
        }

        /// 使用 UDP 将一段字节序列发送往远端
        private void SendUDPMessage(byte[] bytesToSend, IPEndPoint remoteIPEndPoint)
        {
            udpClient?.SendAsync(bytesToSend, bytesToSend.Length, remoteIPEndPoint);
        }

        private uint curSID; // 当前分配的SID
        public uint GenerateUniqueSID()
        {
            lock (map_sid_session)
            {
                while (true)
                {
                    curSID++;
                    if (curSID == uint.MaxValue)
                    {
                        curSID = 1; // 如果当前连接的客户端数量超过 uint.MaxValue 就陷入死循环了
                    }

                    if (!map_sid_session.ContainsKey(curSID))
                    {
                        break;
                    }
                }

                return curSID;
            }
        }

        #endregion
    }
}