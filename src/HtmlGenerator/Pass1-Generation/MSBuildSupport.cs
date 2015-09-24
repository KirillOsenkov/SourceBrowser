using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Language.Xml;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class MSBuildSupport : XmlSupport
    {
        private ProjectGenerator projectGenerator;
        private Project project;
        private bool isRootProject;

        private static readonly char[] complexCharsInProperties = new char[] { '$', ':', '[', ']' };

        public MSBuildSupport(ProjectGenerator projectGenerator)
        {
            this.projectGenerator = projectGenerator;
        }

        public string EnsureFileGeneratedAndGetUrl(string localFileSystemPath, Project project)
        {
            var url = localFileSystemPath + ".html";
            url = url.Replace(":", "");
            url = url.Replace(" ", "");
            url = url.Replace(@"\bin\", @"\bin_\");
            if (url.StartsWith(@"\\"))
            {
                url = url.Substring(2);
            }

            url = Constants.MSBuildFiles + @"\" + url;

            var htmlFilePath = Path.Combine(SolutionDestinationFolder, url);

            if (!File.Exists(htmlFilePath))
            {
                var msbuildSupport = new MSBuildSupport(this.projectGenerator);
                msbuildSupport.Generate(localFileSystemPath, htmlFilePath, project, false);
            }

            url = "/" + url.Replace('\\', '/');
            return url;
        }

        public void Generate(string localFileSystemFilePath, string htmlFilePath, Project project, bool isRootProject)
        {
            this.project = project;
            this.isRootProject = isRootProject;
            base.Generate(localFileSystemFilePath, htmlFilePath, SolutionDestinationFolder);
        }

        protected override string GetAssemblyName()
        {
            var result = projectGenerator.AssemblyName;
            if (!isRootProject)
            {
                result = Constants.MSBuildFiles;
            }

            return result;
        }

        protected override string GetDisplayName()
        {
            var result = Path.GetFileNameWithoutExtension(destinationHtmlFilePath);
            if (!isRootProject)
            {
                var lengthOfPrefixToTrim = SolutionDestinationFolder.Length + Constants.MSBuildFiles.Length + 2;
                result = destinationHtmlFilePath.Substring(lengthOfPrefixToTrim, destinationHtmlFilePath.Length - lengthOfPrefixToTrim - 5); // strip ".html"
            }

            return result;
        }

        protected override string ProcessRange(ClassifiedRange range, string text)
        {
            if (range.Classification == XmlClassificationTypes.XmlAttributeName)
            {
                text = ProcessAttributeName(range, text, isRootProject);
            }
            else if (range.Classification == XmlClassificationTypes.XmlAttributeValue)
            {
                text = ProcessAttributeValue(range, text, isRootProject);
            }
            else if (range.Classification == XmlClassificationTypes.XmlText || range.Classification == XmlClassificationTypes.None)
            {
                text = ProcessXmlText(range, text, isRootProject);
            }
            else if (range.Classification == XmlClassificationTypes.XmlName)
            {
                text = ProcessXmlName(range, text, isRootProject);
            }
            else
            {
                text = base.ProcessRange(range, text);
            }

            return text;
        }

        private string ProcessXmlName(ClassifiedRange range, string text, bool isRootProject)
        {
            var node = range.Node;
            var parent = node.Parent;
            if (parent is XmlElementStartTagSyntax)
            {
                var element = parent.Parent as IXmlElement;
                if (element != null)
                {
                    return ProcessXmlElementName(range, text, isRootProject, element);
                }
            }
            else if (parent is XmlEmptyElementSyntax)
            {
                var emptyElement = parent as IXmlElement;
                if (emptyElement != null)
                {
                    return ProcessXmlElementName(range, text, isRootProject, emptyElement);
                }
            }

            return text;
        }

        private string ProcessXmlElementName(ClassifiedRange range, string text, bool isRootProject, IXmlElement element)
        {
            if (element.Name == "UsingTask")
            {
                var taskName = element.Attributes.FirstOrDefault(a => a.Key == "TaskName").Value;
                if (taskName != null)
                {
                    int lastDot = taskName.LastIndexOf(".");
                    if (lastDot > -1)
                    {
                        taskName = taskName.Substring(lastDot + 1);
                    }

                    return ProcessTaskName(
                        range.LineText,
                        range.LineNumber + 1,
                        range.Column,
                        text,
                        taskName,
                        isRootProject,
                        isUsage: false);
                }
            }

            var parentElement = element.Parent;
            if (parentElement != null)
            {
                if (parentElement.Name == "PropertyGroup")
                {
                    return ProcessPropertyName(
                        range.LineText,
                        range.LineNumber + 1,
                        range.Column,
                        text,
                        isRootProject,
                        isUsage: false);
                }
                else if (parentElement.Name == "ItemGroup" && !ExcludeItem(element.Name))
                {
                    return ProcessItemName(
                        range.LineText,
                        range.LineNumber + 1,
                        range.Column,
                        text,
                        isRootProject,
                        isUsage: false);
                }
                else if (parentElement.Name == "Target" && element.Name != "ItemGroup" && element.Name != "PropertyGroup")
                {
                    return ProcessTaskName(
                        range.LineText,
                        range.LineNumber + 1,
                        range.Column,
                        text,
                        text.Trim(),
                        isRootProject,
                        isUsage: true);
                }
            }

            return text;
        }

        private bool ExcludeItem(string name)
        {
            return
                name == "Compile" ||
                name == "Reference";
        }

        private string ProcessPropertyName(
            string lineText,
            int lineNumber,
            int startPositionOnLine,
            string text,
            bool isRootProject,
            bool isUsage)
        {
            var propertyName = text.Trim();
            var leadingTriviaWidth = text.IndexOf(propertyName);
            var trailingTriviaWidth = text.Length - propertyName.Length - leadingTriviaWidth;

            if (propertyName.IndexOfAny(complexCharsInProperties) != -1)
            {
                return Markup.HtmlEscape(text);
            }

            if (propertyName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                Log.Exception(string.Format("Invalid property name {0} in project {1}", propertyName, this.sourceXmlFilePath));
                return Markup.HtmlEscape(text);
            }

            var href =
                "/" +
                Constants.MSBuildPropertiesAssembly +
                "/" +
                Constants.ReferencesFileName +
                "/" +
                propertyName +
                ".html";

            text = string.Format(
                "{2}<a href=\"{1}\" target=\"n\" class=\"msbuildlink\">{0}</a>{3}",
                propertyName,
                href,
                text.Substring(0, leadingTriviaWidth),
                text.Substring(text.Length - trailingTriviaWidth));

            projectGenerator.AddReference(
                destinationHtmlFilePath,
                lineText,
                startPositionOnLine,
                propertyName.Length,
                lineNumber,
                isRootProject ? this.projectGenerator.AssemblyName : Constants.MSBuildFiles,
                Constants.MSBuildPropertiesAssembly,
                null,
                propertyName,
                isUsage ? ReferenceKind.MSBuildPropertyUsage : ReferenceKind.MSBuildPropertyAssignment);

            return text;
        }

        private string ProcessItemName(
            string lineText,
            int lineNumber,
            int startPositionOnLine,
            string text,
            bool isRootProject,
            bool isUsage)
        {
            var itemName = text.Trim();

            var leadingTriviaWidth = text.IndexOf(itemName);
            var trailingTriviaWidth = text.Length - itemName.Length - leadingTriviaWidth;

            if (itemName.IndexOfAny(complexCharsInProperties) != -1)
            {
                return Markup.HtmlEscape(text);
            }

            var href =
                "/" +
                Constants.MSBuildItemsAssembly +
                "/" +
                Constants.ReferencesFileName +
                "/" +
                itemName +
                ".html";

            text = string.Format(
                "{2}<a href=\"{1}\" target=\"n\" class=\"msbuildlink\">{0}</a>{3}",
                itemName,
                href,
                text.Substring(0, leadingTriviaWidth),
                text.Substring(text.Length - trailingTriviaWidth));

            projectGenerator.AddReference(
                destinationHtmlFilePath,
                lineText,
                startPositionOnLine,
                itemName.Length,
                lineNumber,
                isRootProject ? this.projectGenerator.AssemblyName : Constants.MSBuildFiles,
                Constants.MSBuildItemsAssembly,
                null,
                itemName,
                isUsage ? ReferenceKind.MSBuildItemUsage : ReferenceKind.MSBuildItemAssignment);

            return text;
        }

        private string ProcessTargetName(
            string lineText,
            int lineNumber,
            int startPositionOnLine,
            string text,
            bool isRootProject,
            bool isUsage)
        {
            var targetName = text.Trim();
            var leadingTriviaWidth = text.IndexOf(targetName);
            var trailingTriviaWidth = text.Length - targetName.Length - leadingTriviaWidth;

            var href =
                "/" +
                Constants.MSBuildTargetsAssembly +
                "/" +
                Constants.ReferencesFileName +
                "/" +
                targetName +
                ".html";

            text = string.Format(
                "{2}<a href=\"{1}\" target=\"n\" class=\"msbuildlink\">{0}</a>{3}",
                targetName,
                href,
                text.Substring(0, leadingTriviaWidth),
                text.Substring(text.Length - trailingTriviaWidth));

            projectGenerator.AddReference(
                destinationHtmlFilePath,
                lineText,
                startPositionOnLine,
                targetName.Length,
                lineNumber,
                isRootProject ? this.projectGenerator.AssemblyName : Constants.MSBuildFiles,
                Constants.MSBuildTargetsAssembly,
                null,
                targetName,
                isUsage ? ReferenceKind.MSBuildTargetUsage : ReferenceKind.MSBuildTargetDeclaration);

            return text;
        }

        private string ProcessTaskName(
            string lineText,
            int lineNumber,
            int startPositionOnLine,
            string text,
            string taskName,
            bool isRootProject,
            bool isUsage)
        {
            var trimmedText = text.Trim();
            var leadingTriviaWidth = text.IndexOf(trimmedText);
            var trailingTriviaWidth = text.Length - trimmedText.Length - leadingTriviaWidth;

            var href =
                "/" +
                Constants.MSBuildTasksAssembly +
                "/" +
                Constants.ReferencesFileName +
                "/" +
                taskName +
                ".html";

            text = string.Format(
                "{2}<a href=\"{1}\" target=\"n\" class=\"msbuildlink\">{0}</a>{3}",
                trimmedText,
                href,
                text.Substring(0, leadingTriviaWidth),
                text.Substring(text.Length - trailingTriviaWidth));

            projectGenerator.AddReference(
                destinationHtmlFilePath,
                lineText,
                startPositionOnLine,
                trimmedText.Length,
                lineNumber,
                isRootProject ? this.projectGenerator.AssemblyName : Constants.MSBuildFiles,
                Constants.MSBuildTasksAssembly,
                null,
                taskName,
                isUsage ? ReferenceKind.MSBuildTaskUsage : ReferenceKind.MSBuildTaskDeclaration);

            return text;
        }

        private string ProcessXmlText(ClassifiedRange range, string text, bool isRootProject)
        {
            var element = range.Node == null ? null : range.Node.ParentElement;
            if (element != null &&
                element.Name != null &&
                element.Name.EndsWith("DependsOn") &&
                element.Parent != null &&
                element.Parent.Name == "PropertyGroup")
            {
                return ProcessExpressions(range, text, isRootProject, ProcessSemicolonSeparatedList);
            }

            return ProcessExpressions(range, text, isRootProject);
        }

        private string ProcessAttributeName(ClassifiedRange range, string text, bool isRootProject)
        {
            var node = range.Node;
            if (node is XmlNameTokenSyntax)
            {
                node = node.Parent;
            }

            var element = node.GetParent(2) as IXmlElement ?? node.GetParent(3) as IXmlElement;
            if (element != null)
            {
                if (element.Name == "Import" && text == "Project")
                {
                    return ProcessImportAttributeName(range, text, element["Project"]);
                }
            }

            return text;
        }

        private string ProcessAttributeValue(ClassifiedRange range, string text, bool isRootProject)
        {
            var node = range.Node;
            var attributeSyntax = node.GetParent(2) as XmlAttributeSyntax;
            if (attributeSyntax != null)
            {
                var parentElement = attributeSyntax.ParentElement;

                if (parentElement != null &&
                    parentElement.Name == "Output" &&
                    (attributeSyntax.Name == "ItemName" || attributeSyntax.Name == "PropertyName") &&
                    !text.Contains("%"))
                {
                    if (attributeSyntax.Name == "ItemName")
                    {
                        return ProcessItemName(
                            range.LineText,
                            range.LineNumber + 1,
                            range.Column,
                            text,
                            isRootProject,
                            isUsage: false);
                    }
                    else
                    {
                        return ProcessPropertyName(
                            range.LineText,
                            range.LineNumber + 1,
                            range.Column,
                            text,
                            isRootProject,
                            isUsage: false);
                    }
                }

                if (parentElement != null && parentElement.Name == "Target")
                {
                    if (attributeSyntax.Name == "Name")
                    {
                        return ProcessTargetName(
                            range.LineText,
                            range.LineNumber + 1,
                            range.Column,
                            text,
                            isRootProject,
                            isUsage: false);
                    }

                    if (attributeSyntax.Name == "DependsOnTargets" ||
                        attributeSyntax.Name == "BeforeTargets" ||
                        attributeSyntax.Name == "AfterTargets")
                    {
                        return ProcessExpressions(range, text, isRootProject, ProcessSemicolonSeparatedList);
                    }
                }

                if (parentElement != null && parentElement.Name == "CallTarget" && attributeSyntax.Name == "Targets")
                {
                    return ProcessExpressions(range, text, isRootProject, ProcessSemicolonSeparatedList);
                }

                if (parentElement != null && parentElement.Name == "UsingTask" && attributeSyntax.Name == "TaskName")
                {
                    var taskName = attributeSyntax.Value;
                    var assemblyFileAttribute = parentElement.Attributes.FirstOrDefault(a => a.Key == "AssemblyFile");
                    var assemblyNameAttribute = parentElement.Attributes.FirstOrDefault(a => a.Key == "AssemblyName");
                    string assemblyName = null;
                    if (!string.IsNullOrWhiteSpace(assemblyFileAttribute.Value))
                    {
                        var assemblyFilePath = assemblyFileAttribute.Value;
                        assemblyFilePath = project.ExpandString(assemblyFilePath);
                        assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);
                    }
                    else if (!string.IsNullOrWhiteSpace(assemblyNameAttribute.Value))
                    {
                        assemblyName = assemblyNameAttribute.Value;
                        assemblyName = project.ExpandString(assemblyName);
                        int comma = assemblyName.IndexOf(',');
                        if (comma > -1)
                        {
                            assemblyName = assemblyName.Substring(0, comma);
                        }
                    }

                    if (assemblyName != null)
                    {
                        var symbolId = SymbolIdService.GetId("T:" + taskName);
                        projectGenerator.AddReference(
                            destinationHtmlFilePath,
                            range.LineText,
                            range.Column,
                            taskName.Length,
                            range.LineNumber,
                            isRootProject ? this.projectGenerator.AssemblyName : Constants.MSBuildFiles,
                            assemblyName,
                            null,
                            symbolId,
                            ReferenceKind.Instantiation);

                        var url = string.Format("/{0}/A.html#{1}", assemblyName, symbolId);
                        var result = string.Format("<a href=\"{0}\" class=\"msbuildlink\">{1}</a>", url, text);
                        return result;
                    }
                }
            }

            return ProcessExpressions(range, text, isRootProject);
        }

        private string ProcessExpressions(ClassifiedRange range, string text, bool isRootProject, Func<ClassifiedRange, string, bool, int, string> customStringProcessor = null)
        {
            var parts = MSBuildExpressionParser.SplitStringByPropertiesAndItems(text);
            if (parts.Count() == 1 && !text.StartsWith("$(") && !text.StartsWith("@("))
            {
                var processed = text;
                if (customStringProcessor != null)
                {
                    processed = customStringProcessor(range, processed, isRootProject, range.Start);
                }
                else
                {
                    processed = Markup.HtmlEscape(processed);
                }

                return processed;
            }

            var sb = new StringBuilder();
            int lengthSoFar = 0;
            foreach (var part in parts)
            {
                if (part.StartsWith("$(") && part.EndsWith(")"))
                {
                    var propertyName = part.Substring(2, part.Length - 3);
                    string suffix = "";
                    int dot = propertyName.IndexOf('.');
                    if (dot > -1)
                    {
                        suffix = propertyName.Substring(dot);
                        propertyName = propertyName.Substring(0, dot);
                    }

                    var currentPosition = range.Start + lengthSoFar;
                    var line = TextUtilities.GetLineFromPosition(currentPosition, sourceText);
                    var lineNumber = TextUtilities.GetLineNumber(currentPosition, this.lineLengths);
                    var lineText = sourceText.Substring(line.Item1, line.Item2);
                    var url = ProcessPropertyName(
                        lineText,
                        lineNumber + 1,
                        currentPosition - line.Item1 + 2,
                        propertyName,
                        isRootProject,
                        isUsage: true);

                    sb.Append("$(" + url + Markup.HtmlEscape(suffix) + ")");
                }
                else if (
                    part.StartsWith("@(") &&
                    (part.EndsWith(")") || part.EndsWith("-") || part.EndsWith(",")) &&
                    !part.Contains("%"))
                {
                    int suffixLength = 1;
                    var itemName = part.Substring(2, part.Length - 2 - suffixLength);
                    string suffix = part.Substring(part.Length - suffixLength, suffixLength);

                    var currentPosition = range.Start + lengthSoFar;
                    var line = TextUtilities.GetLineFromPosition(currentPosition, sourceText);
                    var lineNumber = TextUtilities.GetLineNumber(currentPosition, this.lineLengths);
                    var lineText = sourceText.Substring(line.Item1, line.Item2);
                    var url = ProcessItemName(
                        lineText,
                        lineNumber + 1,
                        currentPosition - line.Item1 + 2,
                        itemName,
                        isRootProject,
                        isUsage: true);

                    sb.Append("@(" + url + Markup.HtmlEscape(suffix));
                }
                else
                {
                    var processed = part;
                    if (customStringProcessor != null)
                    {
                        var currentPosition = range.Start + lengthSoFar;
                        processed = customStringProcessor(range, processed, isRootProject, currentPosition);
                    }
                    else
                    {
                        processed = Markup.HtmlEscape(processed);
                    }

                    sb.Append(processed);
                }

                lengthSoFar += part.Length;
            }

            return sb.ToString();
        }

        private string ProcessSemicolonSeparatedList(ClassifiedRange range, string text, bool isRootProject, int currentPosition)
        {
            var sb = new StringBuilder();

            var parts = TextUtilities.SplitSemicolonSeparatedList(text);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) || part == ";")
                {
                    sb.Append(part);
                }
                else
                {
                    var line = TextUtilities.GetLineFromPosition(currentPosition, sourceText);
                    var lineNumber = TextUtilities.GetLineNumber(currentPosition, this.lineLengths);
                    var lineText = sourceText.Substring(line.Item1, line.Item2);
                    var url = ProcessTargetName(
                        lineText,
                        lineNumber + 1,
                        currentPosition - line.Item1,
                        part,
                        isRootProject,
                        isUsage: true);

                    sb.Append(url);
                }

                currentPosition += part.Length;
            }

            return sb.ToString();
        }

        private string ProcessImportAttributeName(ClassifiedRange range, string text, string importedProjectString)
        {
            foreach (var import in project.Imports)
            {
                if (import.ImportingElement.Project == importedProjectString && Path.GetFullPath(import.ImportingElement.ProjectLocation.File) == this.sourceXmlFilePath)
                {
                    var path = import.ImportedProject.FullPath;
                    var url = EnsureFileGeneratedAndGetUrl(path, project);
                    text = string.Format("<a href=\"{0}\" class=\"msbuildlink\">{1}</a>", url, text);
                    return text;
                }
            }

            return text;
        }

        private string SolutionDestinationFolder
        {
            get
            {
                return SolutionGenerator.SolutionDestinationFolder;
            }
        }

        private SolutionGenerator SolutionGenerator
        {
            get
            {
                return projectGenerator.SolutionGenerator;
            }
        }
    }
}