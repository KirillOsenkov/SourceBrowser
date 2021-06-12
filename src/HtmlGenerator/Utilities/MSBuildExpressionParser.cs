using System;
using System.Collections.Generic;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class MSBuildExpressionParser
    {
        /// <summary>
        /// Splits abc$(def)ghi into {"abc", "$(def)", "ghi" }
        /// </summary>
        public static IEnumerable<string> SplitStringByPropertiesAndItems(string text)
        {
            var result = new List<string>();
            int current = 0;
            int nextExpressionStart = FindNextExpressionStart(text);

            while (nextExpressionStart != -1)
            {
                if (nextExpressionStart > current)
                {
                    result.Add(text.Substring(current, nextExpressionStart - current));
                }

                current = nextExpressionStart;

                var expressionLength = FindExpressionLength(text, nextExpressionStart);
                if (expressionLength == 0)
                {
                    break;
                }

                var currentExpressionText = text.Substring(nextExpressionStart, expressionLength);

                var nestedExpressionStart = FindNextExpressionStart(currentExpressionText, 2);
                if (nestedExpressionStart != -1)
                {
                    expressionLength = nestedExpressionStart;
                    currentExpressionText = text.Substring(nextExpressionStart, expressionLength);
                }

                result.Add(currentExpressionText);
                current += expressionLength;

                if (current < text.Length)
                {
                    nextExpressionStart = FindNextExpressionStart(text, current);
                }
                else
                {
                    break;
                }
            }

            if (current < text.Length)
            {
                result.Add(text.Substring(current, text.Length - current));
            }

            return result;
        }

        private static int FindNextExpressionStart(string text, int startFrom = 0)
        {
            var result = text.IndexOf("$(", startFrom, StringComparison.Ordinal);
            int itemStart = text.IndexOf("@(", startFrom, StringComparison.Ordinal);
            if (itemStart >= 0 && (result == -1 || itemStart < result))
            {
                result = itemStart;
            }

            return result;
        }

        private static int FindExpressionLength(string text, int expressionStart)
        {
            bool isItem = text.Substring(expressionStart, 2) == "@(";
            int parenthesesBalance = 1;
            for (int i = expressionStart + 2; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    parenthesesBalance++;
                }
                else if (text[i] == ')')
                {
                    parenthesesBalance--;
                    if (parenthesesBalance == 0)
                    {
                        return i - expressionStart + 1;
                    }
                }
                else if (isItem)
                {
                    if (text[i] == ',' || text[i] == '-')
                    {
                        return i - expressionStart + 1;
                    }
                }
            }

            return 0;
        }
    }
}
