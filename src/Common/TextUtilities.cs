using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.SourceBrowser.Common
{
    public static class TextUtilities
    {
        public static int[] GetLineLengths(this string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException();
            }

            if (text.Length == 0)
            {
                return new int[0];
            }

            var result = new List<int>();
            int currentLineLength = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        currentLineLength++;
                        result.Add(currentLineLength);
                        currentLineLength = 0;
                        previousWasCarriageReturn = false;
                    }
                    else
                    {
                        currentLineLength++;
                        previousWasCarriageReturn = true;
                    }
                }
                else if (text[i] == '\n')
                {
                    previousWasCarriageReturn = false;
                    currentLineLength++;
                    result.Add(currentLineLength);
                    currentLineLength = 0;
                }
                else
                {
                    currentLineLength++;
                    previousWasCarriageReturn = false;
                }
            }

            result.Add(currentLineLength);

            if (previousWasCarriageReturn)
            {
                result.Add(0);
            }

            return result.ToArray();
        }

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }

        public static Tuple<int, int> GetLineFromPosition(int position, string sourceText)
        {
            int lineStart = position;
            int lineEnd = position;

            for (; lineStart > 0; lineStart--)
            {
                if (IsLineBreakChar(sourceText[lineStart - 1]))
                {
                    break;
                }
            }

            for (; lineEnd < sourceText.Length - 1; lineEnd++)
            {
                if (IsLineBreakChar(sourceText[lineEnd + 1]))
                {
                    break;
                }
            }

            return Tuple.Create(lineStart, lineEnd - lineStart + 1);
        }

        public static int GetLineNumber(int start, int[] lineLengths)
        {
            for (int i = 0; i < lineLengths.Length; i++)
            {
                if (start < lineLengths[i])
                {
                    return i;
                }

                start -= lineLengths[i];
            }

            return 0;
        }

        private enum ChunkContentType
        {
            Whitespace,
            Text
        }

        public static IEnumerable<string> SplitSemicolonSeparatedList(this string text)
        {
            var result = new List<string>();
            var currentPart = new StringBuilder(text.Length);
            var currentContentType = ChunkContentType.Whitespace;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
                {
                    if (currentPart.Length > 0 && currentContentType != ChunkContentType.Whitespace)
                    {
                        result.Add(currentPart.ToString());
                        currentPart.Clear();
                    }

                    currentContentType = ChunkContentType.Whitespace;
                    currentPart.Append(c);
                }
                else if (c == ';')
                {
                    if (currentPart.Length > 0)
                    {
                        result.Add(currentPart.ToString());
                        currentPart.Clear();
                    }

                    result.Add(";");
                }
                else
                {
                    if (currentPart.Length > 0 && currentContentType != ChunkContentType.Text)
                    {
                        result.Add(currentPart.ToString());
                        currentPart.Clear();
                    }

                    currentContentType = ChunkContentType.Text;
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
            {
                result.Add(currentPart.ToString());
            }

            return result;
        }

        public static string WithThousandSeparators(this object i)
        {
            return string.Format("{0:#,0}", i);
        }

        public static string StripQuotes(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 2)
            {
                text = text.Substring(1, text.Length - 2);
            }

            if (text.StartsWith("'") && text.EndsWith("'") && text.Length > 2)
            {
                text = text.Substring(1, text.Length - 2);
            }

            return text;
        }

        public static IEnumerable<string> SplitBySpacesConsideringQuotes(this string sourceString)
        {
            var parts = new List<string>();
            var start = -1;
            bool isInQuotes = false;

            for (int i = 0; i < sourceString.Length; i++)
            {
                if (sourceString[i] == ' ' && !isInQuotes)
                {
                    if (start > -1)
                    {
                        if (start < i)
                        {
                            parts.Add(sourceString.Substring(start, i - start));
                        }

                        start = i + 1;
                    }
                }
                else if (sourceString[i] == '"')
                {
                    isInQuotes = !isInQuotes;
                    if (isInQuotes)
                    {
                        if (start == -1)
                        {
                            start = i;
                        }
                        else if (start < i)
                        {
                            parts.Add(sourceString.Substring(start, i - start));
                            start = i;
                        }
                    }
                    else
                    {
                        if (start > -1 && start < i)
                        {
                            parts.Add(sourceString.Substring(start, i - start + 1));
                        }

                        start = i + 1;
                    }
                }
                else if (start == -1)
                {
                    start = i;
                }
            }

            if (start > -1 && start < sourceString.Length)
            {
                parts.Add(sourceString.Substring(start, sourceString.Length - start));
            }

            return parts;
        }

        public static int MinimalUniquenessPreservingPrefixLength(IEnumerable<string> strings)
        {
            var firstString = strings.FirstOrDefault();
            if (string.IsNullOrEmpty(firstString))
            {
                return 0;
            }

            int min = 1;
            int max = firstString.Length;

            while (min < max)
            {
                // in case max == min + 1 this always chooses min
                int mid = min + ((max - min) / 2);
                if (DoesPrefixLengthCreateCollisions(strings, mid))
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid;
                }
            }

            return max;
        }

        private static bool DoesPrefixLengthCreateCollisions(IEnumerable<string> strings, int prefixLength)
        {
            var set = new HashSet<string>();
            foreach (var item in strings)
            {
                var prefix = item;
                if (prefixLength < prefix.Length)
                {
                    prefix = prefix.Substring(0, prefixLength);
                }

                if (!set.Add(prefix))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
