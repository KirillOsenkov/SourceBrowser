using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.SourceBrowser.Common
{
#if NET46
    public class AssertTraceListener : TraceListener
    {
        public static void Register()
        {
            foreach (var existingListener in Debug.Listeners.OfType<TraceListener>().ToArray())
            {
                if (existingListener is DefaultTraceListener)
                {
                    Debug.Listeners.Remove(existingListener);
                }
            }

            Debug.Listeners.Add(new AssertTraceListener());
        }

        public override void Fail(string message, string detailMessage)
        {
            if (message.Contains("This is a soft assert - I don't think this can happen"))
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                message = "ASSERT FAILED";
            }

            if (detailMessage == null)
            {
                detailMessage = string.Empty;
            }

            string stackTrace = new StackTrace(true).ToString();

            if (stackTrace.Contains("OverriddenOrHiddenMembersHelpers.FindOverriddenOrHiddenMembersInType"))
            {
                // bug 661370
                return;
            }

            base.Fail(message, detailMessage);
            Log.Exception(message + "\r\n" + detailMessage + "\r\n" + stackTrace);
        }

        public override void Write(string message)
        {
            Log.Write(message);
        }

        public override void WriteLine(string message)
        {
            Log.Write(message);
        }
    }
#endif
}
