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

        private static readonly Subject<IMessage> Messages = new Subject<IMessage>();

        private static void OnNext(IMessage msg)
        {
            switch (msg)
            {
                case ConsoleMessage consoleMessage:
                    InnerWrite(consoleMessage.Message, consoleMessage.Color);
                    break;
                case FileMessage fileMessage:
                    InnerWriteToFile(fileMessage.Message, fileMessage.FilePath);
                    break;
            }
        }

        private static Thread ThreadFactory(ThreadStart start)
        {
            var thread = new Thread(start);
            thread.IsBackground = true;
            thread.Name = "ThreadLogger";
            return thread;
        }

        static Log()
        {
            Messages.ObserveOn(new NewThreadScheduler(ThreadFactory)).Subscribe(OnNext, OnCompleted);
        }

        private static void OnCompleted()
        {
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
            Messages.OnNext(new FileMessage(message, filePath));
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
            Messages.OnNext(new ConsoleMessage(message, color));
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

        public static void Close()
        {
            Messages.OnCompleted();
        }
    }

    internal interface IMessage
    {
        string Message { get; }
    }

    internal class ConsoleMessage : IMessage
    {
        public string Message { get; }
        public ConsoleColor Color { get; }
        public ConsoleMessage(string message, ConsoleColor color)
        {
            Message = message;
            Color = color;
        }
    }

    internal class FileMessage : IMessage
    {
        public string Message { get; }
        public string FilePath { get; }
        public FileMessage(string message, string filePath)
        {
            Message = message;
            FilePath = filePath;
        }
    }

}
