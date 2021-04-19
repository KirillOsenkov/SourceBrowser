using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.BuildLogParser
{
    public class CompilerInvocation
    {
        public static readonly CompilerInvocationComparer Comparer = new CompilerInvocationComparer();

        public string ProjectFilePath { get; set; }
        public string OutputAssemblyPath { get; set; }
        public string CommandLine { get; set; }

        public string CompilerPath { get; private set; }
        public string IntermediateAssemblyPath { get; private set; }
        public string AssemblyName { get; set; }

        public readonly List<string> ReferencedBinaries = new List<string>();

        //  /resource:(("[^"]*")|([^", ]*))(,(("[^"]*")|([^", ]*)))*
        // A regex to match /resource:A as well as /resource:"B C",D
        private static readonly Regex resourcesRegex = new Regex(@" /(?:resource|win32res|win32resource):((""[^""]*"")|([^"", ]*))(,((""[^""]*"")|([^"", ]*)))*");

        private static readonly Regex referenceRegex = new Regex(@" /(?:reference|r|link):((""[^""]*"")|([^"", ]*))(,((""[^""]*"")|([^"", ]*)))*");
        internal bool UnknownIntermediatePath;

        public CompilerInvocation(string line)
        {
            line = CutOutTemp(line);
            line = CutOutResources(line);
            line = CutOutOutputPath(line);
            line = CutOutDocPath(line);
            line = CutOutIntermediateFiles(line);

            line = CutOutUnresolvedReferencedBinaries(line);

            int space = line.IndexOf(' ');
            CompilerPath = line.Substring(0, space);
            CommandLine = line.Substring(space + 1, line.Length - space - 1);
        }

        public CompilerInvocation()
        {
        }

        public string Language
        {
            get
            {
                if (CompilerPath.IndexOf("vbc", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return "Visual Basic";
                }
                else if (CompilerPath.IndexOf("tsc.exe", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return "TypeScript";
                }
                else
                {
                    return "C#";
                }
            }
        }

        public List<string> TypeScriptFiles { get; private set; }

        private string CutOutDocPath(string line)
        {
            int index = line.IndexOf("/doc:", StringComparison.Ordinal);
            if (index == -1)
            {
                return line;
            }

            index += 5;
            int endOfOut = line.IndexOf(' ', index + 1);
            if (line[index] == '"')
            {
                index++;
                endOfOut = line.IndexOf('"', index);
            }

            var prefix = line.Substring(0, index);
            var suffix = line.Substring(endOfOut, line.Length - endOfOut);
            var fullPath = line.Substring(index, endOfOut - index);
            var filePath = Path.GetFileName(fullPath);

            line = prefix + filePath + suffix;
            return line;
        }

        private string CutOutIntermediateFiles(string line)
        {
            var parts = line.Split(' ');
            var sb = new StringBuilder(line.Length);
            foreach (var part in parts)
            {
                string noQuotes = part;
                if (noQuotes[0] == '"' && noQuotes[noQuotes.Length - 1] == '"')
                {
                    noQuotes = part.Substring(1, part.Length - 2);
                }

                if (noQuotes[0] == '@')
                {
                    noQuotes = noQuotes.Substring(1);
                }

                sb.Append(part);
                sb.Append(' ');
            }

            sb.Length--;

            return sb.ToString();
        }

        private string CutOutUnresolvedReferencedBinaries(string commandLine)
        {
            // need to iterate end to start because otherwise we'd need to update the spans of matches
            // after every delete
            var matches = referenceRegex.Matches(commandLine)
                .Cast<Match>()
                .OrderByDescending(m => m.Index);
            foreach (Match match in matches)
            {
                if (!AddReferencedBinaries(match.Value))
                {
                    commandLine = commandLine.Remove(match.Index, match.Length);
                }
            }

            return commandLine;
        }

        private bool AddReferencedBinaries(string reference)
        {
            reference = reference.Trim();

            int chopOffStart = 0;
            if (reference.StartsWith("/reference:", StringComparison.OrdinalIgnoreCase))
            {
                chopOffStart = 11;
            }
            else if (reference.StartsWith("/r:", StringComparison.OrdinalIgnoreCase))
            {
                chopOffStart = 3;
            }
            else if (reference.StartsWith("/link:", StringComparison.OrdinalIgnoreCase))
            {
                chopOffStart = 6;
            }
            else
            {
                return true;
            }

            reference = reference.Substring(chopOffStart);

            int equals = reference.IndexOf('=');
            if (equals == reference.Length - 1)
            {
                return true;
            }

            if (equals > -1)
            {
                reference = reference.Substring(equals + 1);
            }

            var commaParts = reference.Split(',');
            foreach (var commaPart in commaParts)
            {
                if (!AddReferencedBinary(commaPart))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AddReferencedBinary(string reference)
        {
            string noQuotes = reference;
            if (noQuotes[0] == '"' && noQuotes[noQuotes.Length - 1] == '"')
            {
                noQuotes = reference.Substring(1, reference.Length - 2);
            }

            bool fileExists = false;
            lock (LogAnalyzer.cacheOfKnownExistingBinaries)
            {
                fileExists = LogAnalyzer.cacheOfKnownExistingBinaries.Contains(noQuotes);
            }

            if (fileExists || File.Exists(noQuotes))
            {
                ReferencedBinaries.Add(noQuotes);
                return true;
            }
            else
            {
                LogAnalyzer.AddNonExistingReference(this, noQuotes);
                return false;
            }
        }

        private string CutOutOutputPath(string line)
        {
            int index = line.IndexOf("/out:", StringComparison.Ordinal);
            index += 5;
            int endOfOut = line.IndexOf(' ', index + 1);
            if (line[index] == '"')
            {
                index++;
                endOfOut = line.IndexOf('"', index);
            }

            var prefix = line.Substring(0, index);
            var suffix = line.Substring(endOfOut, line.Length - endOfOut);
            var fullPath = line.Substring(index, endOfOut - index);

            var filePath = Path.GetFileName(fullPath);

            AssemblyName = Path.GetFileNameWithoutExtension(filePath);

            var normalizedFullPath = fullPath.Replace(@"\\", @"\");
            IntermediateAssemblyPath = normalizedFullPath;
            OutputAssemblyPath = normalizedFullPath;

            line = prefix + filePath + suffix;
            return line;
        }

        private string CutOutResources(string line)
        {
            var newLine = resourcesRegex.Replace(line, "");
            return newLine;
        }

        private static string CutOutTemp(string line)
        {
            int start = line.IndexOf(@"D:\Temp", StringComparison.OrdinalIgnoreCase);
            int end = start;
            if (start != -1)
            {
                if (line[start - 1] == '"')
                {
                    end = line.IndexOf('"', start) + 1;
                    start--;
                }
                else
                {
                    end = line.IndexOf(' ', start);
                }

                line = line.Remove(start - 1, end - start + 1);
            }

            return line;
        }

        public override string ToString()
        {
            return
                ProjectFilePath + Environment.NewLine +
                OutputAssemblyPath + Environment.NewLine +
                CommandLine;
        }

        public class CompilerInvocationComparer : IEqualityComparer<CompilerInvocation>
        {
            public bool Equals(CompilerInvocation x, CompilerInvocation y)
            {
                if (x == null || y == null)
                {
                    return false;
                }

                if (x.ProjectFilePath == "-")
                {
                    return y.ProjectFilePath == "-" && string.Equals(x.AssemblyName, y.AssemblyName, StringComparison.OrdinalIgnoreCase);
                }

                return string.Equals(x.ProjectFilePath, y.ProjectFilePath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(CompilerInvocation obj)
            {
                return
                    obj?.ProjectFilePath == "-" ? obj.AssemblyName.GetHashCode() :
                    obj?.ProjectFilePath != null ? obj.ProjectFilePath.GetHashCode() :
                    0;
            }
        }

        public static CompilerInvocation CreateTypeScript(string line)
        {
            var invocation = new CompilerInvocation();
            line = line.Trim();
            int space = line.IndexOf(' ');
            invocation.CompilerPath = line.Substring(0, space);
            invocation.CommandLine = line.Substring(space + 1, line.Length - space - 1);
            invocation.TypeScriptFiles = GetFilesFromCommandLine(invocation.CommandLine);
            if (invocation.TypeScriptFiles != null &&
                !invocation.TypeScriptFiles.Any(f => string.Equals(Path.GetFileName(f), "lib.d.ts", StringComparison.OrdinalIgnoreCase)))
            {
                var libdts = Path.Combine(Path.GetDirectoryName(invocation.CompilerPath), "lib.d.ts");
                if (File.Exists(libdts))
                {
                    invocation.TypeScriptFiles.Add(libdts);
                }
            }

            return invocation;
        }

        private static List<string> GetFilesFromCommandLine(string commandLine)
        {
            var parts = commandLine.SplitBySpacesConsideringQuotes();
            return GetFilesFromCommandLineParts(parts);
        }

        private static List<string> GetFilesFromCommandLineParts(IEnumerable<string> parts)
        {
            var results = new List<string>();
            string previousPart = null;
            foreach (var part in parts)
            {
                if (part.StartsWith("--", StringComparison.Ordinal))
                {
                    previousPart = part;
                    continue;
                }

                if (previousPart == "--module" || previousPart == "--target")
                {
                    previousPart = part;
                    continue;
                }

                if (part.StartsWith("@", StringComparison.Ordinal))
                {
                    var responseFile = part.Substring(1);
                    if (File.Exists(responseFile))
                    {
                        var lines = File.ReadAllLines(responseFile);
                        var responseFileIncludes = GetFilesFromCommandLineParts(lines);
                        foreach (var responseFileInclude in responseFileIncludes)
                        {
                            results.Add(responseFileInclude);
                        }
                    }

                    previousPart = part;
                    continue;
                }

                var file = part.StripQuotes();
                results.Add(file);

                previousPart = part;
            }

            return results;
        }
    }
}
