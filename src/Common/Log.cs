using System;
using System.IO;
using System.Text;

namespace Microsoft.SourceBrowser.Common
{
    public class Log
    {
        private static readonly object consoleLock = new object();
        public const string ErrorLogFile = "Errors.txt";
        public const string MessageLogFile = "Messages.txt";
        private const string SeparatorBar = "===============================================";

        public static string ErrorLogFilePath = ErrorLogFile;
        public static string MessageLogFilePath = MessageLogFile;

        public static void Exception(Exception e, string message)
        {
            WriteToFile(message + Environment.NewLine + e.ToString(), ErrorLogFilePath);
        }

        public static void Exception(string message)
        {
            Write(message, ConsoleColor.Red);
            WriteToFile(message, ErrorLogFilePath);
        }

        public static void Message(string message)
        {
            Write(message, ConsoleColor.Blue);
            WriteToFile(message, MessageLogFilePath);
        }

        private static void WriteToFile(string message, string filePath)
        {
            lock (consoleLock)
            {
                File.AppendAllText(filePath, SeparatorBar + Environment.NewLine + message + Environment.NewLine, Encoding.UTF8);
            }
        }

        public static void Write(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(DateTime.Now.ToString("HH:mm:ss") + " ");
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                if (color != ConsoleColor.Gray)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
        }
    }
}
