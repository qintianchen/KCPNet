using System;
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

            Console.ReadKey();
        }
    }
}