using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        public long DocumentCount = 0;
        public long LinesOfCode = 0;
        public long BytesOfCode = 0;

        private void GenerateProjectInfo()
        {
            Log.Write("Project info...");
            var projectInfoFile = Path.Combine(ProjectDestinationFolder, Constants.ProjectInfoFileName) + ".txt";
            var namedTypes = this.DeclaredSymbols.Keys.OfType<INamedTypeSymbol>();
            var sb = new StringBuilder();
            sb.AppendLine("ProjectSourcePath=" + ProjectSourcePath);
            sb.AppendLine("DocumentCount=" + DocumentCount);
            sb.AppendLine("LinesOfCode=" + LinesOfCode);
            sb.AppendLine("BytesOfCode=" + BytesOfCode);
            sb.AppendLine("DeclaredSymbols=" + this.DeclaredSymbols.Count);
            sb.AppendLine("DeclaredTypes=" + namedTypes.Count());
            sb.AppendLine("PublicTypes=" + namedTypes.Where(t => t.DeclaredAccessibility == Accessibility.Public).Count());
            File.WriteAllText(projectInfoFile, sb.ToString());
        }
    }
}
