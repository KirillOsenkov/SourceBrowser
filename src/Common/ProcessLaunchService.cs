using System.Diagnostics;
using System.Text;

namespace Microsoft.SourceBrowser.Common
{
    public class ProcessLaunchService
    {
        private bool errorDataReceived = false;
        private readonly StringBuilder output = new StringBuilder();

        public ProcessStartInfo CreateProcessStartInfo(string filePath, string arguments = null)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(arguments))
            {
                processStartInfo.Arguments = arguments;
            }

            return processStartInfo;
        }

        public Process StartProcess(
            ProcessStartInfo processStartInfo,
            DataReceivedEventHandler outputDataReceived,
            DataReceivedEventHandler errorDataReceived)
        {
            Process executable = Process.Start(processStartInfo);
            if (outputDataReceived != null)
            {
                executable.OutputDataReceived += outputDataReceived;
            }

            if (errorDataReceived != null)
            {
                executable.ErrorDataReceived += errorDataReceived;
            }

            executable.BeginOutputReadLine();
            executable.BeginErrorReadLine();
            executable.WaitForExit();
            return executable;
        }

        public ProcessRunResult RunAndRedirectOutput(string executableName, string arguments = null)
        {
            ProcessStartInfo processStartInfo = CreateProcessStartInfo(executableName, arguments);
            return RunAndRedirectOutput(processStartInfo);
        }

        public class ProcessRunResult
        {
            public Process Process { get; set; }
            public int ExitCode { get; set; }
            public bool ErrorDataReceived { get; set; }
            public string Output { get; set; }
        }

        public ProcessRunResult RunAndRedirectOutput(ProcessStartInfo processStartInfo)
        {
            errorDataReceived = false;
            var process = StartProcess(processStartInfo, OnOutputDataReceived, OnErrorDataReceived);
            return new ProcessRunResult
            {
                Process = process,
                ExitCode = process.ExitCode,
                ErrorDataReceived = errorDataReceived,
                Output = output.ToString()
            };
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Output(e.Data);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            errorDataReceived = true;
            Output(e.Data);
        }

        private void Output(object o)
        {
            if (o == null)
            {
                return;
            }

            lock (this)
            {
                output.AppendLine(o.ToString());
            }
        }
    }
}
