using System;
using System.Text;
using System.Threading.Tasks;
using KCPNet;

namespace KCPServerExample
{
    class Program
    {
        private static KCPServer server;
        static void Main(string[] args)
        {
            server = new KCPServer();
            server.Start("127.0.0.1", 17555);

            server.onClientSessionCreated = point =>
            {
                KCPNetLogger.Info($"收到一个客户端链接：{point}");
            };

            server.onKCPReceive = (bytes, point) =>
            {
                KCPNetLogger.Info($"Receive msg from {point}, len = {bytes.Length}: {Encoding.ASCII.GetString(bytes)}");
            };

            Task.Run(Close);
            
            Console.ReadKey();
        }

        private static async void Close()
        {
            await Task.Delay(5000);
            server.Close();
        }
    }
}