using System;
using System.Collections.Generic;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Folder<T>
    {
        public string Name { get; set; }
        public SortedList<string, Folder<T>> Folders { get; set; }
        public List<T> Items { get; set; }

        public void Add(T item)
        {
            if (Items == null)
            {
                Items = new List<T>();
            }

            Items.Add(item);
        }

        public void Sort(Comparison<T> comparison)
        {
            if (Items != null)
            {
                Items.Sort((l, r) => comparison(l, r));
            }

            if (Folders != null)
            {
                foreach (var subfolder in Folders.Values)
                {
                    subfolder.Sort(comparison);
                }
            }
        }

        public Folder<T> GetOrCreateFolder(string folderName)
        {
            if (Folders == null)
            {
                Folders = new SortedList<string, Folder<T>>(StringComparer.OrdinalIgnoreCase);
            }

            if (!Folders.TryGetValue(folderName, out Folder<T> result))
            {
                result = new Folder<T> { Name = folderName };
                Folders.Add(folderName, result);
            }

            return result;
        }
    }
}
