using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class NamespaceExplorer
    {
        public static void WriteNamespaceExplorer(string assemblyName, IEnumerable<DeclaredSymbolInfo> types, string rootPath)
        {
            new NamespaceExplorer().WriteFile(assemblyName, types, rootPath, "../");
        }

        private void WriteFile(string assemblyName, IEnumerable<DeclaredSymbolInfo> types, string rootPath, string pathPrefix)
        {
            var fileName = Path.Combine(rootPath, Constants.Namespaces);
            NamespaceTreeNode root = ConstructTree(types);

            using (var sw = new StreamWriter(fileName))
            {
                Markup.WriteNamespaceExplorerPrefix(assemblyName, sw, pathPrefix);
                WriteChildren(root, sw, pathPrefix);
                Markup.WriteNamespaceExplorerSuffix(sw);
            }
        }

        public NamespaceTreeNode ConstructTree(IEnumerable<DeclaredSymbolInfo> types)
        {
            var root = new NamespaceTreeNode("");
            foreach (var type in types)
            {
                Insert(root, type);
            }

            return root;
        }

        private void WriteChildren(NamespaceTreeNode node, StreamWriter sw, string pathPrefix)
        {
            if (node.Children == null)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                WriteChild(child.Value, sw, pathPrefix);
            }
        }

        private void WriteChild(NamespaceTreeNode node, StreamWriter sw, string pathPrefix)
        {
            if (node.TypeDeclaration != null)
            {
                WriteType(node.TypeDeclaration, sw, node.Children != null ? "folderTitle" : "typeTitle", pathPrefix);
            }
            else
            {
                WriteNamespace(node.Title, sw);
            }

            if (node.Children != null)
            {
                sw.WriteLine("<div class=\"folder\">");
                WriteChildren(node, sw, pathPrefix);
                sw.Write("</div>");
            }

            sw.WriteLine();
        }

        private void WriteNamespace(string title, StreamWriter sw)
        {
            sw.Write(string.Format("<div class=\"folderTitle\">{0}</div>", Markup.HtmlEscape(title)));
        }

        private void WriteType(DeclaredSymbolInfo typeDeclaration, StreamWriter sw, string className, string pathPrefix)
        {
            string typeUrl = typeDeclaration.GetUrl();
            sw.Write(string.Format("<div class=\"{3}\"><a class=\"tDN\" href=\"{0}\" target=\"s\"><img class=\"tDNI\" src=\"{4}content/icons/{2}.png\" />{1}</a></div>",
                typeUrl,
                Markup.HtmlEscape(typeDeclaration.Name),
                typeDeclaration.Glyph,
                className,
                pathPrefix));
        }

        private void Insert(NamespaceTreeNode root, DeclaredSymbolInfo type)
        {
            var namespaceString = type.GetNamespace();
            var parts = namespaceString.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var nodeWhereToInsert = GetOrCreateNode(root, parts, 0);

            // { is to sort types after namespaces
            var inserted = nodeWhereToInsert.GetOrCreate("{" + type.Name);
            inserted.TypeDeclaration = type;
        }

        private NamespaceTreeNode GetOrCreateNode(NamespaceTreeNode node, string[] parts, int index)
        {
            if (index == parts.Length)
            {
                return node;
            }

            node = node.GetOrCreate(parts[index]);
            return GetOrCreateNode(node, parts, ++index);
        }

        public class NamespaceTreeNode
        {
            public SortedList<string, NamespaceTreeNode> Children;
            public DeclaredSymbolInfo TypeDeclaration { get; set; }
            public string Title { get; private set; }

            public NamespaceTreeNode(string namespacePart)
            {
                Title = namespacePart;
            }

            public void Add(NamespaceTreeNode node)
            {
                if (Children == null)
                {
                    Children = new SortedList<string, NamespaceTreeNode>(StringComparer.OrdinalIgnoreCase);
                }

                Children.Add(node.Title, node);
            }

            public NamespaceTreeNode GetOrCreate(string title)
            {
                if (Children == null)
                {
                    Children = new SortedList<string, NamespaceTreeNode>(StringComparer.OrdinalIgnoreCase);
                }

                // need to try finding both folders and files
                string other = title.StartsWith("{", StringComparison.Ordinal) ? title.TrimStart('{') : "{" + title;
                if (!Children.TryGetValue(title, out NamespaceTreeNode result) && !Children.TryGetValue(other, out result))
                {
                    result = new NamespaceTreeNode(title);
                    Children.Add(title, result);
                }

                return result;
            }
        }
    }
}
