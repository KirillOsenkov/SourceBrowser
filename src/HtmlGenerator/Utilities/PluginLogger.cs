using System;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator.Utilities
{
    public class PluginLogger : MEF.ILog
    {
        public void Critical(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log.Exception(ex, message, true);
            }

            Log.Exception(message, true);
        }

        public void Debug(string message, Exception ex = null)
        {
            Log.Write(message);
            if (ex != null)
            {
                Log.Write(ex.Message); //Don't print too much -- this is an unimportant log
            }
        }

        public void Error(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log.Exception(ex, message, true);
            }

            Log.Exception(message, true);
        }

        public void Fatal(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log.Exception(ex, message, true);
            }

            Log.Exception(message, true);
        }

        public void Info(string message, Exception ex = null)
        {
            Log.Write(message);
            if (ex != null)
            {
                Log.Write(ex.Message); //Don't print too much -- this is an unimportant log
            }
        }

        public void Status(string message, Exception ex = null)
        {
            Log.Message(message);
            if (ex != null)
            {
                Log.Message(ex.Message); //Don't print too much -- this is an unimportant log
            }
        }

        public void Verbose(string message, Exception ex = null)
        {
            Log.Write(message);
            if (ex != null)
            {
                Log.Write(ex.Message); //Don't print too much -- this is an unimportant log
            }
        }

        public void Warning(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log.Exception(ex, message, false);
            }

            Log.Exception(message, false);
        }
    }
}
