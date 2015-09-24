using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class AnalyzisError
    {
        public string message { get; set; }
        public string stack { get; set; }
    }

    public class AnalyzedFile
    {
        public string fileName { get; set; }
        public ClassifiedRange[] syntacticClassifications { get; set; }
        public ClassifiedRange[] semanticClassifications { get; set; }
        public string fileSymbolId { get; set; }
    }

    public class ClassifiedRange
    {
        // deserialization
        public ClassifiedRange()
        {
        }

        public ClassifiedRange(string text, int start, int length, ClassifiedRange enclosingRange = null)
        {
            this.text = text.Substring(start, length);

            this.start = start;
            this.length = length;

            if (enclosingRange != null)
            {
                classification = enclosingRange.classification;
                hyperlinks = enclosingRange.hyperlinks;
                definitionSymbolId = enclosingRange.definitionSymbolId;
                definitionKind = enclosingRange.definitionKind;
                searchString = enclosingRange.searchString;
                fullName = enclosingRange.fullName;
            }
        }

        public string classification { get; set; }
        public int start { get; set; }
        public int length { get; set; }
        public int end { get { return start + length; } }
        public Hyperlink[] hyperlinks { get; set; }
        public string definitionSymbolId { get; set; }
        public string definitionKind { get; set; }
        public string searchString { get; set; }
        public string fullName { get; set; }

        public bool IsSemantic { get; set; }

        public string text { get; set; }
        public int lineNumber { get; set; }
        public int column { get; set; }
        public string lineText { get; set; }

        public bool IsSymbolLocalOnly()
        {
            return
                definitionKind == "variable" ||
                definitionKind == "parameter";
        }

        public override string ToString()
        {
            return string.Format("{0} ({1};{2}) {3}", text, start, length, classification);
        }
    }

    public class Hyperlink
    {
        public string sourceFile { get; set; }
        public int start { get; set; }
        public string symbolId { get; set; }
    }

    public class TypeScriptAnalyzer : IDisposable
    {
        [ComImport]
        [Guid("89747b18-17fb-405f-ba07-46a89c5e7be2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IJavaScriptExecutionEngine
        {
            object AddScript(string name, string text, int textLength);
            void Close();
            void DumpHeapSnapshot(string snapshotFileName);
        }

        private readonly Thread initialThread;
        private IJavaScriptExecutionEngine engine;
        private dynamic shim;

        public TypeScriptAnalyzer(string scriptFile)
        {
            initialThread = Thread.CurrentThread;
            var hr = CreateExecutionEngine("TSAnalyzer", enableDebugging: false, forceJScript9: false, engine: out engine);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            var scriptText = File.ReadAllText(scriptFile);
            shim = engine.AddScript("Analyze", scriptText, scriptText.Length);
        }

        public bool Analyze(IEnumerable<string> files)
        {
            VerifyThread();

            var array = files.ToList();
            var defaultLibIndex = array.FindIndex(f => string.Equals(Path.GetFileName(f), "lib.d.ts", StringComparison.OrdinalIgnoreCase));
            var defaultLib = array[defaultLibIndex];
            array.RemoveAt(defaultLibIndex);

            var json = JsonConvert.SerializeObject(new { fileNames = array, libFile = defaultLib });
            return shim(json);
        }

        public void Dispose()
        {
            VerifyThread();
            if (engine != null)
            {
                engine.Close();
                Marshal.ReleaseComObject(engine);
            }
        }

        private void VerifyThread()
        {
            if (Thread.CurrentThread != initialThread)
            {
                throw new InvalidOperationException("Attempt to access JS engine from the wrong thread");
            }
        }

        [DllImport("ScriptExecutionEnvironment.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CreateExecutionEngine([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Bool)] bool enableDebugging, [MarshalAs(UnmanagedType.Bool)] bool forceJScript9, out IJavaScriptExecutionEngine engine);
    }
}
