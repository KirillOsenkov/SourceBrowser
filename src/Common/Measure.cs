using System;
using System.Diagnostics;

namespace Microsoft.SourceBrowser.Common
{
    public class Measure : IDisposable
    {
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private string title;

        public static IDisposable Time(string title)
        {
            return new Measure(title);
        }

        private Measure(string title)
        {
            this.title = title;
        }

        public void Dispose()
        {
            Debug.WriteLine(title + ": " + stopwatch.Elapsed);
        }
    }
}