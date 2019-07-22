using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

        private static readonly Subject<(string message, ConsoleColor color)> ConsoleMessages = new Subject<(string message, ConsoleColor color)>();
        private static readonly Subject<(string message, string filePath)> FileMessages = new Subject<(string message, string filePath)>();

        static Log()
        {
            var threadLogger = new NewThreadScheduler(start =>
            {
                var thread = new Thread(start);
                thread.Name = "ThreadLogger";
                thread.IsBackground = true;
                return thread;
            });
            ConsoleMessages.ObserveOn(threadLogger).Subscribe(OnNextMessage);
            FileMessages.ObserveOn(threadLogger).Subscribe(OnNextMessage);
        }

        private static void OnNextMessage((string message, string filePath) obj)
        {
            InnerWriteToFile(obj.message, obj.filePath);
        }

        private static void OnNextMessage((string message, ConsoleColor color) obj)
        {
            InnerWrite(obj.message, obj.color);
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
            FileMessages.OnNext((message, filePath));
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
            ConsoleMessages.OnNext((message, color));
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
