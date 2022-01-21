using System;
using System.Text;
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

            Console.ReadKey();
        }
    }
}