using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class Classification
    {
        public async Task<IEnumerable<Range>> Classify(Document document, SourceText text)
        {
            var span = TextSpan.FromBounds(0, text.Length);

            IEnumerable<ClassifiedSpan> classifiedSpans = null;
            try
            {
                classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, span);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Exception during Classification of document: " + document.FilePath);
                return null;
            }

            var ranges = classifiedSpans.Select(classifiedSpan =>
                new Range
                {
                    ClassifiedSpan = classifiedSpan,
                    Text = text.GetSubText(classifiedSpan.TextSpan).ToString()
                });
            ranges = Merge(text, ranges);
            ranges = FilterByClassification(ranges);
            ranges = FillGaps(text, ranges);
            return ranges;
        }

        private IEnumerable<Range> FilterByClassification(IEnumerable<Range> spans)
        {
            foreach (var span in spans)
            {
                string filtered = FilterClassificationType(span.ClassificationType);
                if (filtered != null)
                {
                    yield return new Range(filtered, span.TextSpan, span.Text);
                }
            }
        }

        private IEnumerable<Range> Merge(SourceText text, IEnumerable<Range> spans)
        {
            int mergeStart = -1;
            int mergeEnd = -1;

            foreach (var span in spans)
            {
                if (IsMergeable(span))
                {
                    if (mergeStart == -1)
                    {
                        mergeStart = span.TextSpan.Start;
                    }

                    mergeEnd = span.TextSpan.End;
                }
                else
                {
                    if (mergeStart != -1)
                    {
                        var textSpan = new TextSpan(mergeStart, mergeEnd - mergeStart);
                        yield return CreateRange(
                            text,
                            textSpan,
                            Constants.ClassificationKeyword);
                        mergeStart = -1;
                    }

                    yield return span;
                }
            }

            if (mergeStart != -1)
            {
                var textSpan = new TextSpan(mergeStart, mergeEnd - mergeStart);
                yield return CreateRange(
                    text,
                    textSpan,
                    Constants.ClassificationKeyword);
            }
        }

        private static bool IsMergeable(Range span)
        {
            return span.ClassificationType == Constants.RoslynClassificationKeyword &&
                span.Text != "this" &&
                span.Text != "base" &&
                span.Text != "New" &&
                span.Text != "new" &&
                span.Text != "var" &&
                span.Text != "partial" &&
                span.Text != "Partial";
        }

        private IEnumerable<Range> FillGaps(SourceText text, IEnumerable<Range> spans)
        {
            int current = 0;
            Range previous = null;

            foreach (var span in spans)
            {
                int start = span.TextSpan.Start;
                if (start > current)
                {
                    var textSpan = new TextSpan(current, start - current);
                    yield return CreateRange(text, textSpan, null);
                }

                // Filter out duplicate classifications with the same span (see bug 17602).
                if (previous == null || span.TextSpan != previous.TextSpan)
                {
                    yield return span;
                }

                previous = span;
                current = span.TextSpan.End;
            }

            if (current < text.Length)
            {
                var textSpan = new TextSpan(current, text.Length - current);
                yield return CreateRange(text, textSpan, null);
            }
        }

        private Range CreateRange(SourceText text, TextSpan span, string classification)
        {
            return new Range(classification, span, text.GetSubText(span).ToString());
        }

        private static readonly HashSet<string> ignoreClassifications = new HashSet<string>(new[]
            {
                "operator",
                "number",
                "punctuation",
                "preprocessor text",
                "xml literal - text"
            });

        private static readonly Dictionary<string, string> replaceClassifications = new Dictionary<string, string>
            {
                ["keyword"] = Constants.ClassificationKeyword,
                ["identifier"] = Constants.ClassificationIdentifier,
                ["field name"] = Constants.ClassificationIdentifier,
                ["enum member name"] = Constants.ClassificationIdentifier,
                ["constant name"] = Constants.ClassificationIdentifier,
                ["local name"] = Constants.ClassificationIdentifier,
                ["parameter name"] = Constants.ClassificationIdentifier,
                ["method name"] = Constants.ClassificationIdentifier,
                ["extension method name"] = Constants.ClassificationIdentifier,
                ["property name"] = Constants.ClassificationIdentifier,
                ["event name"] = Constants.ClassificationIdentifier,
                ["class name"] = Constants.ClassificationTypeName,
                ["struct name"] = Constants.ClassificationTypeName,
                ["interface name"] = Constants.ClassificationTypeName,
                ["enum name"] = Constants.ClassificationTypeName,
                ["delegate name"] = Constants.ClassificationTypeName,
                ["module name"] = Constants.ClassificationTypeName,
                ["type parameter name"] = Constants.ClassificationTypeName,
                ["preprocessor keyword"] = Constants.ClassificationPreprocessKeyword,
                ["xml doc comment - delimiter"] = Constants.ClassificationComment,
                ["xml doc comment - name"] = Constants.ClassificationComment,
                ["xml doc comment - text"] = Constants.ClassificationComment,
                ["xml doc comment - comment"] = Constants.ClassificationComment,
                ["xml doc comment - entity reference"] = Constants.ClassificationComment,
                ["xml doc comment - attribute name"] = Constants.ClassificationComment,
                ["xml doc comment - attribute quotes"] = Constants.ClassificationComment,
                ["xml doc comment - attribute value"] = Constants.ClassificationComment,
                ["xml doc comment - cdata section"] = Constants.ClassificationComment,
                ["xml literal - delimiter"] = Constants.ClassificationXmlLiteralDelimiter,
                ["xml literal - name"] = Constants.ClassificationXmlLiteralName,
                ["xml literal - attribute name"] = Constants.ClassificationXmlLiteralAttributeName,
                ["xml literal - attribute quotes"] = Constants.ClassificationXmlLiteralAttributeQuotes,
                ["xml literal - attribute value"] = Constants.ClassificationXmlLiteralAttributeValue,
                ["xml literal - entity reference"] = Constants.ClassificationXmlLiteralEntityReference,
                ["xml literal - cdata section"] = Constants.ClassificationXmlLiteralCDataSection,
                ["xml literal - processing instruction"] = Constants.ClassificationXmlLiteralProcessingInstruction,
                ["xml literal - embedded expression"] = Constants.ClassificationXmlLiteralEmbeddedExpression,
                ["xml literal - comment"] = Constants.ClassificationComment,
                ["comment"] = Constants.ClassificationComment,
                ["string"] = Constants.ClassificationLiteral,
                ["string - verbatim"] = Constants.ClassificationLiteral,
                ["excluded code"] = Constants.ClassificationExcludedCode,
            };

        public string FilterClassificationType(string classificationType)
        {
            if (classificationType == null || ignoreClassifications.Contains(classificationType))
            {
                return null;
            }

            if (classificationType == Constants.ClassificationKeyword)
            {
                return classificationType;
            }

            if (replaceClassifications.TryGetValue(classificationType, out string replacement))
            {
                return replacement;
            }

            return Constants.ClassificationUnknown;
        }
    }
}
