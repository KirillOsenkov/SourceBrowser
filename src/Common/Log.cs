using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.SourceBrowser.Common
{
    public static class Log
    {
        public const string ErrorLogFile = "Errors.txt";
        public const string MessageLogFile = "Messages.txt";
        private const string SeparatorBar = "===============================================";

        private static string errorLogFilePath = Path.GetFullPath(ErrorLogFile);
        private static string messageLogFilePath = Path.GetFullPath(MessageLogFile);

        public delegate void MessageHandler(string message, ConsoleColor color);
        public delegate void FileMessageHandler(string message, string filePath);
        public static event MessageHandler RaiseMessage;
        public static event FileMessageHandler RaiseMessageFile;

        private static bool logIsActivated;
        public static void Activate()
        {
            if (logIsActivated)
                return;
            var threadLogger = new Thread(() =>
            {
                RaiseMessage += InnerWrite;
                RaiseMessageFile += InnerWriteToFile;
            });
            threadLogger.IsBackground = true;
            threadLogger.Name = "threadLogger";
            threadLogger.Start();
            logIsActivated = true;
        }

        public static void Exception(Exception e, string message, bool isSevere = true)
        {
            var text = message + Environment.NewLine + e.ToString();
            Exception(text, isSevere);
        }

        public static void Exception(string message, bool isSevere = true)
        {
            Write(message, isSevere ? ConsoleColor.Red : ConsoleColor.Yellow);
            WriteToFile(message, ErrorLogFilePath);
        }

        public static void Message(string message)
        {
            Write(message, ConsoleColor.Blue);
            WriteToFile(message, MessageLogFilePath);
        }
        
        private static void WriteToFile(string message, string filePath)
        {
            RaiseMessageFile?.Invoke(message, filePath);
        }

        private static void InnerWriteToFile(string message, string filePath)
        {
            try
            {
                File.AppendAllText(filePath, SeparatorBar + Environment.NewLine + message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Write($"Failed to write to ${filePath}: ${ex}.", ConsoleColor.Red);
            }
        }

        public static void Write(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            RaiseMessage?.Invoke(message, color);
        }

        private static void InnerWrite(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static string ErrorLogFilePath
        {
            get { return errorLogFilePath; }
            set { errorLogFilePath = value.MustBeAbsolute(); }
        }

        public static string MessageLogFilePath
        {
            get { return messageLogFilePath; }
            set { messageLogFilePath = value.MustBeAbsolute(); }
        }
    }
}
