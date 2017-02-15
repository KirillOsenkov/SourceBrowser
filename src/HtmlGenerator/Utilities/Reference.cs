using System.IO;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public enum ReferenceKind
    {
        DerivedType,
        InterfaceInheritance,
        InterfaceImplementation,
        Override,
        InterfaceMemberImplementation,
        Instantiation,
        Write,
        Read,
        Reference,
        GuidUsage,
        EmptyArrayAllocation,
        MSBuildPropertyAssignment,
        MSBuildPropertyUsage,
        MSBuildItemAssignment,
        MSBuildItemUsage,
        MSBuildTargetDeclaration,
        MSBuildTargetUsage,
        MSBuildTaskDeclaration,
        MSBuildTaskUsage
    }

    public class Reference
    {
        public string ToAssemblyId { get; set; }
        public string FromAssemblyId { get; set; }
        public string ToSymbolId { get; set; }
        public string FromLocalPath { get; set; }
        public string Url { get; set; }
        public string ReferenceLineText { get; set; }
        public int ReferenceColumnStart { get; set; }
        public int ReferenceColumnEnd { get; set; }
        public int ReferenceLineNumber { get; set; }
        public string ToSymbolName { get; set; }
        public ReferenceKind Kind { get; set; }

        public Reference()
        {
        }

        public Reference(string separatedLine, string sourceLine)
        {
            var parts = separatedLine.Split(';');
            FromAssemblyId = string.Intern(parts[0]);
            Url = parts[1];
            FromLocalPath = parts[2];
            ReferenceLineNumber = int.Parse(parts[3]);
            ReferenceColumnStart = int.Parse(parts[4]);
            ReferenceColumnEnd = int.Parse(parts[5]);
            if (parts.Length >= 7)
            {
                Kind = (ReferenceKind)int.Parse(parts[6]);
            }

            ReferenceLineText = sourceLine;
            ToSymbolName = ReferenceLineText.Substring(ReferenceColumnStart, ReferenceColumnEnd - ReferenceColumnStart);
        }

        public void WriteTo(TextWriter writer)
        {
            writer.Write(FromAssemblyId);
            writer.Write(';');
            writer.Write(Url);
            writer.Write(';');
            writer.Write(FromLocalPath);
            writer.Write(';');
            writer.Write(ReferenceLineNumber);
            writer.Write(';');
            writer.Write(ReferenceColumnStart);
            writer.Write(';');
            writer.Write(ReferenceColumnEnd);
            writer.Write(';');
            writer.Write((int)Kind);
            writer.WriteLine();
            writer.WriteLine(ReferenceLineText);
        }
    }
}
