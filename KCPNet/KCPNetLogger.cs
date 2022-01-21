using System;
using System.Threading;

namespace KCPNet
{
    public class KCPNetLogger
    {
        public enum LogColor
        {
            White,
            Red,
            Yellow,
            Blue,
            Green,
            Orange
        }

        public static Action<string, LogColor> onInfo;
        public static Action<string, LogColor> onWarning;
        public static Action<string, LogColor> onError;

        public static void Info(string msg, LogColor color = LogColor.White)
        {
            if (onInfo != null)
            {
                onInfo.Invoke(msg, color);
            }
            else
            {
                Log($"[INFO] {msg}", color);
            }
        }

        public static void Warning(string msg, LogColor color = LogColor.Orange)
        {
            if (onWarning != null)
            {
                onWarning.Invoke(msg, color);
            }
            else
            {
                Log($"[WARNING] {msg}", color);
            }
        }

        public static void Error(string msg, LogColor color = LogColor.Red)
        {
            if (onError != null)
            {
                onError.Invoke(msg, color);
            }
            else
            {
                Log($"[ERROR] {msg}", color);
            }
        }

        private static void Log(string msg, LogColor color = LogColor.White)
        {
            msg = $"{Thread.CurrentThread.ManagedThreadId} {msg}";
            switch (color)
            {
                case LogColor.White:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Red:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Yellow:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Blue:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Green:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Orange:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(color), color, null);
            }
        }
    }
}