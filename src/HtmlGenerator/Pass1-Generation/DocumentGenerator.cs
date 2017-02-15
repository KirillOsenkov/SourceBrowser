﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        public ProjectGenerator projectGenerator;
        public Document Document;
        public string documentDestinationFilePath;
        public string relativePathToRoot;
        public string documentRelativeFilePathWithoutHtmlExtension;

        private Classification classifier;

        public SourceText Text;
        public SyntaxNode Root;
        public SemanticModel SemanticModel;
        public HashSet<ISymbol> DeclaredSymbols;
        public object SemanticFactsService;
        public object SyntaxFactsService;
        private Func<SemanticModel, SyntaxNode, CancellationToken, bool> isWrittenToDelegate;
        private Func<SyntaxToken, SyntaxNode> getBindableParentDelegate;

        public DocumentGenerator(
            ProjectGenerator projectGenerator,
            Document document)
        {
            this.projectGenerator = projectGenerator;
            this.Document = document;
        }

        public async Task Generate()
        {
            if (Configuration.CalculateRoslynSemantics)
            {
                this.Text = await Document.GetTextAsync();
                this.Root = await Document.GetSyntaxRootAsync();
                this.SemanticModel = await Document.GetSemanticModelAsync();
                this.SemanticFactsService = WorkspaceHacks.GetSemanticFactsService(this.Document);
                this.SyntaxFactsService = WorkspaceHacks.GetSyntaxFactsService(this.Document);

                var semanticFactsServiceType = SemanticFactsService.GetType();
                var isWrittenTo = semanticFactsServiceType.GetMethod("IsWrittenTo");
                this.isWrittenToDelegate = (Func<SemanticModel, SyntaxNode, CancellationToken, bool>)
                    Delegate.CreateDelegate(typeof(Func<SemanticModel, SyntaxNode, CancellationToken, bool>), SemanticFactsService, isWrittenTo);

                var syntaxFactsServiceType = SyntaxFactsService.GetType();
                var getBindableParent = syntaxFactsServiceType.GetMethod("GetBindableParent");
                this.getBindableParentDelegate = (Func<SyntaxToken, SyntaxNode>)
                    Delegate.CreateDelegate(typeof(Func<SyntaxToken, SyntaxNode>), SyntaxFactsService, getBindableParent);

                this.DeclaredSymbols = new HashSet<ISymbol>();

                Interlocked.Increment(ref projectGenerator.DocumentCount);
                Interlocked.Add(ref projectGenerator.LinesOfCode, Text.Lines.Count);
                Interlocked.Add(ref projectGenerator.BytesOfCode, Text.Length);
            }

            CalculateDocumentDestinationPath();
            CalculateRelativePathToRoot();

            // add the file itself as a "declared symbol", so that clicking on document in search
            // results redirects to the document
            ProjectGenerator.AddDeclaredSymbolToRedirectMap(
                this.projectGenerator.SymbolIDToListOfLocationsMap,
                SymbolIdService.GetId(this.Document),
                documentRelativeFilePathWithoutHtmlExtension,
                0);

            if (File.Exists(documentDestinationFilePath))
            {
                // someone already generated this file, likely a shared linked file from elsewhere
                return;
            }

            this.classifier = new Classification();

            Log.Write(documentDestinationFilePath);

            try
            {
                var directoryName = Path.GetDirectoryName(documentDestinationFilePath);
                var sanitized = Paths.SanitizeFolder(directoryName);
                if (directoryName != sanitized)
                {
                    Log.Exception("Illegal characters in path: " + directoryName + " Project: " + this.projectGenerator.AssemblyName);
                }

                if (Configuration.CreateFoldersOnDisk)
                {
                    Directory.CreateDirectory(directoryName);
                }
            }
            catch (PathTooLongException)
            {
                // there's one case where a path is too long - we don't care enough about it
                return;
            }

            if (Configuration.WriteDocumentsToDisk)
            {
                using (var streamWriter = new StreamWriter(
                    documentDestinationFilePath,
                    append: false,
                    encoding: Encoding.UTF8))
                {
                    await GenerateHtml(streamWriter);
                }
            }
            else
            {
                using (var memoryStream = new MemoryStream())
                using (var streamWriter = new StreamWriter(memoryStream))
                {
                    await GeneratePre(streamWriter);
                }
            }
        }

        private void CalculateDocumentDestinationPath()
        {
            documentRelativeFilePathWithoutHtmlExtension = Paths.GetRelativeFilePathInProject(Document);
            documentDestinationFilePath = Path.Combine(ProjectDestinationFolder, documentRelativeFilePathWithoutHtmlExtension) + ".html";
        }

        private void CalculateRelativePathToRoot()
        {
            this.relativePathToRoot = Paths.CalculateRelativePathToRoot(documentDestinationFilePath, SolutionDestinationFolder);
        }

        private async Task GenerateHtml(StreamWriter writer)
        {
            var title = Document.Name;
            var lineCount = Text.Lines.Count;

            // if the document is very long, pregenerate line numbers statically
            // to make the page load faster and avoid JavaScript cost
            bool pregenerateLineNumbers = IsLargeFile(lineCount);

            // pass a value larger than 0 to generate line numbers in JavaScript (to reduce HTML size)
            var prefix = Markup.GetDocumentPrefix(title, relativePathToRoot, pregenerateLineNumbers ? 0 : lineCount);
            writer.Write(prefix);
            GenerateHeader(writer.WriteLine);

            var ranges = (await classifier.Classify(Document, Text)).ToArray();

            // pass a value larger than 0 to generate line numbers statically at HTML generation time
            var table = Markup.GetTablePrefix(DocumentUrl, pregenerateLineNumbers ? lineCount : 0, GenerateGlyphs(ranges));
            writer.WriteLine(table);

            GeneratePre(ranges, writer, lineCount);
            var suffix = Markup.GetDocumentSuffix();
            writer.WriteLine(suffix);
        }

        private ISymbol GetSymbolForRange(Classification.Range r)
        {
            var position = r.ClassifiedSpan.TextSpan.Start;
            var token = Root.FindToken(position, findInsideTrivia: true);

            if (token != null)
            {
                return SemanticModel.GetDeclaredSymbol(token.Parent);
            }
            return null;
        }

        private string GenerateGlyphs(IEnumerable<Classification.Range> ranges)
        {
            var lines = new Dictionary<int, HashSet<string>>();
            int lineNumber = -1;
            ISymbol symbol = null;
            Dictionary<string, string> context = new Dictionary<string, string>
            {
                    { MEF.ContextKeys.FilePath, Document.FilePath },
                    { MEF.ContextKeys.LineNumber, "-1" }
            };

            Action<string> maybeLog = g =>
            {
                if (!string.IsNullOrWhiteSpace(g))
                {
                    HashSet<string> lineGlyphs;
                    if (!lines.TryGetValue(lineNumber, out lineGlyphs))
                    {
                        lineGlyphs = new HashSet<string>();
                        lines.Add(lineNumber, lineGlyphs);
                    }
                    lineGlyphs.Add(g);
                }
            };

            Func<MEF.ITextVisitor, string> VisitText = v =>
            {
                try
                {
                    return v.Visit(Text.Lines[lineNumber - 1].ToString(), context);
                }
                catch (Exception ex)
                {
                    Log.Write("Exception in text visitor: " + ex.Message);
                    return null;
                }
            };

            Func<MEF.ISymbolVisitor, string> VisitSymbol = v =>
            {
                try
                {
                    return symbol.Accept(new MEF.SymbolVisitorWrapper(v, context));
                }
                catch (Exception ex)
                {
                    Log.Write("Exception in symbol visitor: " + ex.Message);
                    return null;
                }
            };


            foreach (var r in ranges)
            {
                var pos = r.ClassifiedSpan.TextSpan.Start;
                var token = Root.FindToken(pos, true);
                var nextLineNumber = token.SyntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1;

                if (nextLineNumber != lineNumber)
                {
                    lineNumber = nextLineNumber;
                    context[MEF.ContextKeys.LineNumber] = lineNumber.ToString();
                    maybeLog(string.Concat(projectGenerator.PluginTextVisitors.Select(VisitText)));
                }
                symbol = SemanticModel.GetDeclaredSymbol(token.Parent);
                if (symbol != null)
                {
                    maybeLog(string.Concat(projectGenerator.PluginSymbolVisitors.Select(VisitSymbol)));
                }
            }
            if (lines.Any())
            {
                var sb = new StringBuilder();
                for (var i = 1; i <= lines.Keys.Max(); i++)
                {
                    HashSet<string> glyphs;
                    if (lines.TryGetValue(i, out glyphs))
                    {
                        foreach (var g in glyphs)
                        {
                            sb.Append(g);
                        }
                    }
                    sb.Append("<br/>");
                }
                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }



        private class ET4MethodVisitor : SymbolVisitor<string>
        {
        }

        private string DocumentUrl { get { return Document.Project.AssemblyName + "/" + documentRelativeFilePathWithoutHtmlExtension.Replace('\\', '/'); } }

        private void GenerateHeader(Action<string> writeLine)
        {
            string documentDisplayName = documentRelativeFilePathWithoutHtmlExtension;
            string projectDisplayName = projectGenerator.ProjectSourcePath;
            string projectUrl = "/#" + Document.Project.AssemblyName;

            string documentLink = string.Format("File: <a id=\"filePath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a><br/>", DocumentUrl, documentDisplayName);
            string projectLink = string.Format("Project: <a id=\"projectPath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a> ({2})", projectUrl, projectDisplayName, projectGenerator.AssemblyName);

            string fileShareLink = GetFileShareLink();
            if (fileShareLink != null)
            {
                fileShareLink = Markup.A(fileShareLink, "File", "_blank");
            }
            else
            {
                fileShareLink = "";
            }

            string webLink = GetWebLink();
            if (webLink != null)
            {
                webLink = Markup.A(webLink, "Web&nbsp;Access", "_blank");
            }
            else
            {
                webLink = "";
            }

            string firstRow = string.Format("<tr><td>{0}</td><td>{1}</td></tr>", documentLink, webLink);
            string secondRow = string.Format("<tr><td>{0}</td><td>{1}</td></tr>", projectLink, fileShareLink);

            Markup.WriteLinkPanel(writeLine, firstRow, secondRow);
        }

        private string GetWebLink()
        {
            var fullPath = Path.GetFullPath(Document.FilePath);
            var serverPathMapping =
                projectGenerator.SolutionGenerator.ServerPathMappings.FirstOrDefault(p => fullPath.StartsWith(p.Key));
            if (serverPathMapping.Key != null)
            {
                return serverPathMapping.Value + fullPath.Substring(serverPathMapping.Key.Length).Replace('\\', '/');
            }

            var serverPath = this.projectGenerator.SolutionGenerator.ServerPath;
            if (string.IsNullOrEmpty(serverPath))
            {
                return null;
            }

            string filePath = GetDocumentPathFromSourceSolutionRoot();
            filePath = filePath.Replace('\\', '/');

            string urlTemplate = @"{0}{1}";

            string url = string.Format(
                urlTemplate,
                serverPath,
                filePath);
            return url;
        }

        private string GetDocumentPathFromSourceSolutionRoot()
        {
            string projectPath = Path.GetDirectoryName(projectGenerator.ProjectSourcePath);
            string filePath = @"C:\" + Path.Combine(projectPath, documentRelativeFilePathWithoutHtmlExtension);
            filePath = Path.GetFullPath(filePath);
            filePath = filePath.Substring(3); // strip the artificial "C:\"
            return filePath;
        }

        private string GetFileShareLink()
        {
            var networkShare = this.projectGenerator.SolutionGenerator.NetworkShare;
            if (string.IsNullOrEmpty(networkShare))
            {
                return null;
            }

            string filePath = GetDocumentPathFromSourceSolutionRoot();
            filePath = Path.Combine(networkShare, filePath);
            return filePath;
        }

        private async Task GeneratePre(StreamWriter writer, int lineCount = 0)
        {
            var ranges = await classifier.Classify(Document, Text);
            GeneratePre(ranges, writer, lineCount);
        }

        private void GeneratePre(IEnumerable<Classification.Range> ranges, StreamWriter writer, int lineCount = 0)
        {
            if (ranges == null)
            {
                // if there was an error in Roslyn, don't fail the entire index, just return
                return;
            }

            foreach (var range in ranges)
            {
                string html = GenerateRange(writer, range, lineCount);
                writer.Write(html);
            }
        }

        private bool IsLargeFile(int lineCount)
        {
            return lineCount > 30000;
        }

        private string GenerateRange(StreamWriter writer, Classification.Range range, int lineCount = 0)
        {
            var html = range.Text;
            html = Markup.HtmlEscape(html);
            bool isLargeFile = IsLargeFile(lineCount);
            string classAttributeValue = GetClassAttribute(html, range);
            HtmlElementInfo hyperlinkInfo = GenerateLinks(range, isLargeFile);

            if (hyperlinkInfo == null)
            {
                if (classAttributeValue == null || isLargeFile)
                {
                    return html;
                }

                if (classAttributeValue == "k")
                {
                    return "<b>" + html + "</b>";
                }

            }

            var sb = new StringBuilder();

            var elementName = "span";
            if (hyperlinkInfo != null)
            {
                elementName = hyperlinkInfo.Name;
            }

            sb.Append("<" + elementName);
            bool overridingClassAttributeSpecified = false;
            if (hyperlinkInfo != null)
            {
                foreach (var attribute in hyperlinkInfo.Attributes)
                {
                    AddAttribute(sb, attribute.Key, attribute.Value);
                    if (attribute.Key == "class")
                    {
                        overridingClassAttributeSpecified = true;
                    }
                }
            }

            if (!overridingClassAttributeSpecified)
            {
                AddAttribute(sb, "class", classAttributeValue);
            }

            sb.Append('>');

            html = AddIdSpanForImplicitConstructorIfNecessary(hyperlinkInfo, html);

            sb.Append(html);
            sb.Append("</" + elementName + ">");

            html = sb.ToString();

            if (hyperlinkInfo != null && hyperlinkInfo.DeclaredSymbol != null)
            {
                writer.Flush();
                long streamPosition = writer.BaseStream.Length;

                streamPosition += html.IndexOf(hyperlinkInfo.Attributes["id"] + ".html");
                projectGenerator.AddDeclaredSymbol(
                    hyperlinkInfo.DeclaredSymbol,
                    hyperlinkInfo.DeclaredSymbolId,
                    documentRelativeFilePathWithoutHtmlExtension,
                    streamPosition);
            }

            return html;
        }

        private string AddIdSpanForImplicitConstructorIfNecessary(HtmlElementInfo hyperlinkInfo, string html)
        {
            if (hyperlinkInfo != null && hyperlinkInfo.DeclaredSymbol != null)
            {
                INamedTypeSymbol namedTypeSymbol = hyperlinkInfo.DeclaredSymbol as INamedTypeSymbol;
                if (namedTypeSymbol != null)
                {
                    var implicitInstanceConstructor = namedTypeSymbol.Constructors.FirstOrDefault(c => !c.IsStatic && c.IsImplicitlyDeclared);
                    if (implicitInstanceConstructor != null)
                    {
                        var symbolId = SymbolIdService.GetId(implicitInstanceConstructor);
                        html = Markup.Tag("span", html, new Dictionary<string, string> { { "id", symbolId } });
                        projectGenerator.AddDeclaredSymbol(
                            implicitInstanceConstructor,
                            symbolId,
                            documentRelativeFilePathWithoutHtmlExtension,
                            0);
                    }
                }
            }

            return html;
        }

        private void AddAttribute(StringBuilder sb, string name, string value)
        {
            if (value != null)
            {
                sb.Append(" " + name + "=\"" + value + "\"");
            }
        }
    }
}
