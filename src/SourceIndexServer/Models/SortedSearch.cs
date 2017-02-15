using System;
using System.Collections.Generic;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class SortedSearch
    {
        private Func<int, string> item;
        private int count;

        public SortedSearch(Func<int, string> listItem, int listCount)
        {
            this.item = listItem;
            this.count = listCount;
        }

        public static int FindItem<T>(IList<T> list, string word, Func<T, string> keySelector)
        {
            var search = new SortedSearch(i => keySelector(list[i]), list.Count);
            int low;
            int high;
            search.FindBounds(word, out low, out high);
            return low;
        }

        public void FindBounds(string word, out int low, out int high)
        {
            low = 0;
            high = this.count - 1;
            word = word.ToUpperInvariant();

            for (int charIndex = 0; charIndex < word.Length; charIndex++)
            {
                int letterStart = FindLetterStart(low, high, word[charIndex], charIndex);
                if (letterStart == -1)
                {
                    high = low - 1;
                    break;
                }

                int letterEnd = FindLetterEnd(low, high, word[charIndex], charIndex);
                low = letterStart;
                high = letterEnd;

                if (high < low)
                {
                    break;
                }
            }
        }

        private int FindLetterStart(int low, int high, char ch, int index)
        {
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                string name = this.item(mid);
                if (name.Length <= index || char.ToUpperInvariant(name[index]) < ch)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            if (low >= this.count ||
                this.item(low).Length <= index ||
                char.ToUpperInvariant(item(low)[index]) != ch)
            {
                return -1;
            }

            return low;
        }

        private int FindLetterEnd(int low, int high, char ch, int index)
        {
            while (low < high)
            {
                int mid = low + (high - low + 1) / 2;
                string name = item(mid);
                if (name.Length <= index || char.ToUpperInvariant(item(mid)[index]) <= ch)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return high;
        }
    }
}