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
            sb.Append("ProjectSourcePath=").AppendLine(ProjectSourcePath)
                .Append("DocumentCount=").Append(DocumentCount).AppendLine()
                .Append("LinesOfCode=").Append(LinesOfCode).AppendLine()
                .Append("BytesOfCode=").Append(BytesOfCode).AppendLine()
                .Append("DeclaredSymbols=").Append(DeclaredSymbols.Count).AppendLine()
                .Append("DeclaredTypes=").Append(namedTypes.Count()).AppendLine()
                .Append("PublicTypes=").Append(namedTypes.Count(t => t.DeclaredAccessibility == Accessibility.Public)).AppendLine();
            File.WriteAllText(projectInfoFile, sb.ToString());
        }
    }
}
