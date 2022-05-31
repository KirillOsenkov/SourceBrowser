using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class Markup
    {
        public static string Li(string content)
        {
            return Tag("li", content);
        }

        public static string A(string url)
        {
            return "<a href=\"" + url + "\" target=\"_blank\">" + url + "</a>";
        }

        public static string A(string url, string displayText, string target = "")
        {
            if (!string.IsNullOrEmpty(target))
            {
                target = string.Format(" target=\"{0}\"", target);
            }
            else
            {
                target = "";
            }

            string result = string.Format("<a class=\"blueLink\" href=\"{0}\"{2}>{1}</a>", url, displayText, target);
            return result;
        }

        public static string Note(string text)
        {
            return "<div class=\"note\">" + text + "</div>";
        }

        private static string GetGlyph(DeclaredSymbolInfo symbol)
        {
            var result = symbol.Glyph;
            if (result == 196)
            {
                return "CSharp";
            }
            else if (result == 195)
            {
                return "VB";
            }
            else if (result == 228)
            {
                return "TypeScript";
            }
            else if (result == 227)
            {
                return "xaml";
            }

            return result.ToString();
        }

        public static void WriteSymbol(DeclaredSymbolInfo symbol, StringBuilder sb)
        {
            var url = symbol.GetUrl();
            sb.AppendFormat("<a href=\"{0}\" target=\"s\"><div class=\"resultItem\" onClick=\"resultClick(this);\">", url);
            sb.Append("<div class=\"resultLine\">");
            sb.AppendFormat("<img role=\"presentation\" src=\"/content/icons/{0}\" height=\"16\" width=\"16\" />", GetGlyph(symbol) + ".png");
            sb.AppendFormat("<div class=\"resultKind\">{0}</div>", symbol.Kind);
            sb.AppendFormat("<div class=\"resultName\">{0}</div>", Markup.HtmlEscape(symbol.Name));
            sb.AppendLine("</div>");
            sb.AppendFormat("<div class=\"resultDescription\">{0}</div>", Markup.HtmlEscape(symbol.Description));
            sb.AppendLine();
            sb.AppendLine("</div></a>");
        }

        public static string P(string content)
        {
            return Tag("p", content);
        }

        public static string UrlEncodeAndHtmlEscape(string text)
        {
            text = UrlEncode(text);
            text = HtmlEscape(text);
            return text;
        }

        public static string HtmlEscape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            text = WebUtility.HtmlEncode(text);

            // HTML doesn't support XML's &apos;
            // need to use &#39; instead
            // http://blogs.msdn.com/kirillosenkov/archive/2010/03/19/apos-is-in-xml-in-html-use-39.aspx#comments
            // http://www.w3.org/TR/html4/sgml/entities.html
            // http://lists.whatwg.org/pipermail/whatwg-whatwg.org/2005-October/004973.html
            // http://en.wikipedia.org/wiki/List_of_XML_and_HTML_character_entity_references
            // http://fishbowl.pastiche.org/2003/07/01/the_curse_of_apos/
            // http://nedbatchelder.com/blog/200703/random_html_factoid_no_apos.html
            // Don't want to use System.Web.HttpUtility.HtmlEncode
            // because I don't want to take a dependency on System.Web
            text = text.Replace("&apos;", "&#39;");
            text = IntersperseLineBreaks(text);

            return text;
        }

        private static string IntersperseLineBreaks(string text)
        {
            text = text.Replace("\n\r", "\n \r");
            return text;
        }

        public static string UrlEncode(string value)
        {
            if (value == null)
            {
                return null;
            }

            int l = value.Length;
            var sb = new StringBuilder(l);

            for (int i = 0; i < l; i++)
            {
                char ch = value[i];

                if ((ch & 0xff80) == 0)
                {
                    // 7 bit?
                    if (IsUrlSafeChar(ch))
                    {
                        sb.Append(ch);
                    }
                    else if (ch == ' ')
                    {
                        sb.Append('+');
                    }
                    else
                    {
                        sb.Append('%');
                        sb.Append(IntToHex((ch >> 4) & 0xf));
                        sb.Append(IntToHex((ch) & 0xf));
                    }
                }
                else
                {
                    // arbitrary Unicode?
                    sb.Append("%u");
                    sb.Append(IntToHex((ch >> 12) & 0xf));
                    sb.Append(IntToHex((ch >> 8) & 0xf));
                    sb.Append(IntToHex((ch >> 4) & 0xf));
                    sb.Append(IntToHex((ch) & 0xf));
                }
            }

            return sb.ToString();
        }

        public static char IntToHex(int n)
        {
            if (n <= 9)
            {
                return (char)(n + '0');
            }
            else
            {
                return (char)(n - 10 + 'a');
            }
        }

        /// <summary>
        /// Set of safe chars, from RFC 1738.4 minus '+'
        /// </summary>
        public static bool IsUrlSafeChar(char ch)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                return true;
            }

            switch (ch)
            {
                case '-':
                case '_':
                case '.':
                case '!':
                case '*':
                case '(':
                case ')':
                    return true;
            }

            return false;
        }

        public static string Tag(string tag, string content, IEnumerable<KeyValuePair<string, string>> attributes = null)
        {
            var sb = new StringBuilder();

            sb.Append("<");
            sb.Append(tag);

            if (attributes != null && attributes.Any())
            {
                foreach (var kvp in attributes)
                {
                    sb.Append(" ");
                    sb.Append(kvp.Key);
                    sb.Append("=\"");
                    sb.Append(kvp.Value);
                    sb.Append("\"");
                }
            }

            sb.Append(">");
            sb.Append(content);
            sb.Append("</");
            sb.Append(tag);
            sb.Append(">");

            return sb.ToString();
        }
    }
}
