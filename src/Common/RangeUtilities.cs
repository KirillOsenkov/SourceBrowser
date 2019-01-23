using System;
using System.Collections.Generic;

namespace Microsoft.SourceBrowser.Common
{
    public static class RangeUtilities
    {
        public static T[] FillGaps<T>(
            string text,
            IList<T> ranges,
            Func<T, int> startGetter,
            Func<T, int> lengthGetter,
            Func<int, int, string, T> gapFactory)
        {
            var result = new List<T>(ranges.Count);
            int current = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                var start = startGetter(ranges[i]);
                FillGapIfNeeded(current, start, text, result, gapFactory);

                result.Add(ranges[i]);
                current = start + lengthGetter(ranges[i]);
            }

            FillGapIfNeeded(current, text.Length, text, result, gapFactory);

            return result.ToArray();
        }

        private static void FillGapIfNeeded<T>(
            int current,
            int start,
            string sourceText,
            List<T> result,
            Func<int, int, string, T> gapFactory)
        {
            if (start > current)
            {
                var gapStart = current;
                var gapLength = start - gapStart;
                result.Add(gapFactory(gapStart, gapLength, sourceText));
            }
        }
    }
}
