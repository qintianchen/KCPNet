using System;
using System.Text;
using System.Threading.Tasks;
using KCPNet;

namespace KCPClientExample
{
    class Program
    {
        private static KCPClient client;
        static void Main(string[] args)
        {
            client = new KCPClient();
            client.Start("127.0.0.1", 17555);

            Task<bool> connectTask = client.TryConnectToServer();
            Task.Run(() =>
            {
                ConnectCheckAsync(connectTask);
            });
            
            Console.ReadKey();
        }

        private static async void ConnectCheckAsync(Task<bool> connectTask)
        {
            int failCount = 0;
            while (true)
            {
                if (connectTask != null && connectTask.IsCompleted)
                {
                    if (connectTask.Result)
                    {
                        connectTask = null;
                        OnTryConnectToServerEnd(true);
                    }
                    else
                    {
                        failCount++;
                        if (failCount > 5)
                        {
                            connectTask = null;
                            OnTryConnectToServerEnd(false);
                            break;
                        }
                        else
                        {
                            connectTask = client.TryConnectToServer();
                        }
                    }
                }
                
                await Task.Delay(1000);
            }
        }

        private static void OnTryConnectToServerEnd(bool isSuccess)
        {
            KCPNetLogger.Info($"尝试连接服务器：{isSuccess}");
            if (isSuccess)
            {
                for (int i = 0; i < 10; i++)
                {
                    var res = client.SendMessage(Encoding.ASCII.GetBytes($"Message {i} from client"));
                    KCPNetLogger.Info($"客户端发送消息: {res}");
                }
            }
        }
    }
}