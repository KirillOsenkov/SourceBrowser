﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Language.Xml;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public abstract class XmlSupport
    {
        protected string sourceXmlFilePath;
        protected string destinationHtmlFilePath;
        protected string sourceText;
        protected int[] lineLengths;

        public void Generate(string sourceXmlFilePath, string destinationHtmlFilePath, string solutionDestinationFolder)
        {
            Log.Write(destinationHtmlFilePath);

            this.sourceXmlFilePath = Path.GetFullPath(sourceXmlFilePath);
            this.destinationHtmlFilePath = destinationHtmlFilePath;

            sourceText = File.ReadAllText(sourceXmlFilePath);
            var lines = File.ReadAllLines(sourceXmlFilePath);
            lineLengths = TextUtilities.GetLineLengths(sourceText);
            var lineCount = lines.Length;
            var root = Parser.ParseText(sourceText);

            var sb = new StringBuilder();

            var relativePathToRoot = Paths.CalculateRelativePathToRoot(destinationHtmlFilePath, solutionDestinationFolder);

            var prefix = Markup.GetDocumentPrefix(Path.GetFileName(sourceXmlFilePath), relativePathToRoot, lineCount, "ix");
            sb.Append(prefix);

            var displayName = GetDisplayName();
            var assemblyName = GetAssemblyName();

            var url = "/#" + assemblyName + "/" + displayName.Replace('\\', '/');

            var file = string.Format("File: <a id=\"filePath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a><br/>", url, displayName);
            var row = string.Format("<tr><td>{0}</td></tr>", file);
            Markup.WriteLinkPanel(s => sb.AppendLine(s), row);

            // pass a value larger than 0 to generate line numbers statically at HTML generation time
            var table = Markup.GetTablePrefix();
            sb.AppendLine(table);

            var ranges = new List<ClassifiedRange>();

            ClassifierVisitor.Visit(
                root,
                0,
                sourceText.Length,
                (start, length, node, classification) =>
                {
                    var line = TextUtilities.GetLineFromPosition(start, sourceText);
                    var lineText = sourceText.Substring(line.Item1, line.Item2);

                    ranges.Add(
                        new ClassifiedRange
                        {
                            Classification = classification,
                            Node = node,
                            Text = sourceText.Substring(start, length),
                            LineText = lineText,
                            LineStart = line.Item1,
                            LineNumber = TextUtilities.GetLineNumber(start, lineLengths),
                            Start = start,
                            Length = length
                        });
                });

            ranges = RangeUtilities.FillGaps(
                sourceText,
                ranges,
                r => r.Start,
                r => r.Length,
                (s, l, t) => new ClassifiedRange
                {
                    Start = s,
                    Length = l,
                    Text = t.Substring(s, l)
                }).ToList();
            foreach (var range in ranges)
            {
                GenerateRange(range, sb);
            }

            var suffix = Markup.GetDocumentSuffix();
            sb.AppendLine(suffix);

            var folder = Path.GetDirectoryName(destinationHtmlFilePath);
            Directory.CreateDirectory(folder);
            File.WriteAllText(destinationHtmlFilePath, sb.ToString());
        }

        protected abstract string GetDisplayName();

        protected abstract string GetAssemblyName();

        protected void GenerateRange(ClassifiedRange range, StringBuilder sb)
        {
            var text = range.Text;
            var spanClass = GetSpanClass(range.Classification);
            if (spanClass != null)
            {
                sb.Append("<span class=\"");
                sb.Append(spanClass);
                sb.Append("\">");
            }

            try
            {
                text = ProcessRange(range, text);
            }
            catch (Exception)
            {
                text = Markup.HtmlEscape(range.Text);
            }

            sb.Append(text);
            if (spanClass != null)
            {
                sb.Append("</span>");
            }
        }

        protected virtual string ProcessRange(ClassifiedRange range, string text)
        {
            text = Markup.HtmlEscape(text);
            return text;
        }

        protected string GetSpanClass(XmlClassificationTypes classification)
        {
            switch (classification)
            {
                case XmlClassificationTypes.XmlAttributeName:
                    return "xan";
                case XmlClassificationTypes.XmlAttributeValue:
                    return "xav";
                case XmlClassificationTypes.XmlCDataSection:
                    return "xcs";
                case XmlClassificationTypes.XmlComment:
                    return "c";
                case XmlClassificationTypes.XmlDelimiter:
                    return "xd";
                case XmlClassificationTypes.XmlEntityReference:
                    return "xer";
                case XmlClassificationTypes.XmlName:
                    return "xn";
                case XmlClassificationTypes.XmlProcessingInstruction:
                    return "xpi";
                case XmlClassificationTypes.XmlAttributeQuotes:
                case XmlClassificationTypes.None:
                case XmlClassificationTypes.XmlText:
                default:
                    return null;
            }
        }

        public class ClassifiedRange
        {
            public string Text { get; set; }
            public SyntaxNode Node { get; set; }
            public XmlClassificationTypes Classification { get; set; }
            public int Start { get; set; }
            public int Length { get; set; }
            public int LineStart { get; set; }
            public int LineNumber { get; set; }
            public string LineText { get; set; }
            public int Column
            {
                get
                {
                    return Start - LineStart;
                }
            }

            public override string ToString()
            {
                return string.Format("({0}, {1}) [{2}] '{3}' {4}", Start, Length, Classification, Text, Node);
            }
        }
    }
}