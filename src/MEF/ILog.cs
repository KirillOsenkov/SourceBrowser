using System;

namespace MEF
{
    public interface ILog
    {
        void Debug(string message, Exception ex = null);
        void Verbose(string message, Exception ex = null);
        void Info(string message, Exception ex = null);
        void Status(string message, Exception ex = null);
        void Warning(string message, Exception ex = null);
        void Error(string message, Exception ex = null);
        void Critical(string message, Exception ex = null);
        void Fatal(string message, Exception ex = null);
    }
}
