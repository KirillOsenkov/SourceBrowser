using System;
using System.Diagnostics;

namespace Microsoft.SourceBrowser.Common
{
    public class Disposable : IDisposable
    {
        private readonly string actionName;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        private Disposable(string actionName)
        {
            this.actionName = actionName;
        }

        public static IDisposable Timing(string actionName)
        {
            Log.Write(actionName, ConsoleColor.DarkGray);
            return new Disposable(actionName);
        }

        public void Dispose()
        {
            var message = actionName + " complete. Took: " + stopwatch.Elapsed;
            Debug.WriteLine(message);
            Log.Write(message, ConsoleColor.Green);
        }
    }
}
