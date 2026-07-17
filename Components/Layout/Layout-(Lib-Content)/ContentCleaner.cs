namespace MITANZ360Pro.Web.Services
{
    using HtmlAgilityPack;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class ContentCleaner
    {
        // Precompiled Regex for performance
        private static readonly Regex InlineBreakFix =
            new Regex(@"(?<=\w)\r?\n(?=\w)", RegexOptions.Compiled);

        private static readonly Regex MultiSpace =
            new Regex(@" {2,}", RegexOptions.Compiled);

        private static readonly Regex TrailingWhitespace =
            new Regex(@"[ \t]+\r?\n", RegexOptions.Compiled);

        private static readonly Regex EmojiRegex =
            new Regex(@"[\u2600-\u27BF]|[\uD83C-\uDBFF\uDC00-\uDFFF]+", RegexOptions.Compiled);

        private static readonly Regex EmptySymbolLines =
            new Regex(@"(?m)^[^\w\r\n]{1,}$", RegexOptions.Compiled);

        private static readonly Regex UnicodeEscape =
            new Regex(@"\\u[0-9a-fA-F]{4}", RegexOptions.Compiled);

        private static readonly Regex NormalizeNewLines =
            new Regex(@"(\r?\n\s*){2,}", RegexOptions.Compiled);

        // ✅ FINAL FIXED PIPELINE
        private static readonly List<Func<string, string>> TextPipeline = new()
        {
            // Fix inline breaks
            text => InlineBreakFix.Replace(text, " "),

            // Decode unicode
            text => UnicodeEscape.Replace(text, m =>
                ((char)Convert.ToInt32(m.Value.Substring(2), 16)).ToString()),

            // Remove emoji
            text => EmojiRegex.Replace(text, ""),

            // Remove markdown
            text => Regex.Replace(text, @"(?m)^#{1,6}\s*", ""),
            text => Regex.Replace(text, @"(?m)^\s*[-•]\s*", ""),
            text => Regex.Replace(text, @"(?m)^\s*-{2,}\s*$", ""), // ✅ FIXED HERE

            // Normalize whitespace
            text => TrailingWhitespace.Replace(text, Environment.NewLine),
            text => MultiSpace.Replace(text, " "),
            text => Regex.Replace(text, @"(\r?\n\s*){2,}", Environment.NewLine + Environment.NewLine),

            // Remove noise lines
            text => EmptySymbolLines.Replace(text, ""),

            // Final trim
            text => text.Trim()
        };
        public static string CleanForLms(string rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml))
                return string.Empty;

            // Decode HTML entities
            string html = WebUtility.HtmlDecode(rawHtml);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Clean DOM
            CleanDom(doc);

            // Convert DOM to text
            var sb = new StringBuilder(4096);
            ProcessNode(doc.DocumentNode, sb, 0);

            string result = sb.ToString();

            // Run pipeline
            foreach (var step in TextPipeline)
            {
                result = step(result);
            }

            return result;
        }

        private static void CleanDom(HtmlDocument doc)
        {
            foreach (var node in doc.DocumentNode.Descendants()
                .Where(n => n.NodeType == HtmlNodeType.Element))
            {
                node.Attributes.RemoveAll();
            }
        }

        private static void ProcessNode(HtmlNode node, StringBuilder sb, int indentLevel)
        {
            foreach (var child in node.ChildNodes)
            {
                switch (child.Name.ToLower())
                {
                    case "h1":
                    case "h2":
                    case "h3":
                        AppendLine(sb, CleanText(child.InnerText));
                        break;

                    case "p":
                        AppendLine(sb, CleanText(child.InnerText));
                        break;

                    case "ul":
                        ProcessList(child, sb, indentLevel);
                        break;

                    case "ol":
                        ProcessOrderedList(child, sb, indentLevel);
                        break;

                    case "table":
                        ProcessTable(child, sb);
                        break;

                    case "hr":
                        AppendLine(sb, "---");
                        break;

                    case "#text":
                        var text = CleanText(child.InnerText);
                        if (!string.IsNullOrWhiteSpace(text) &&
                            child.ParentNode.Name == "body")
                        {
                            AppendLine(sb, text);
                        }
                        break;

                    default:
                        ProcessNode(child, sb, indentLevel);
                        break;
                }
            }
        }

        private static void ProcessList(HtmlNode ul, StringBuilder sb, int level)
        {
            var items = ul.SelectNodes("./li");
            if (items == null) return;

            string indent = new string(' ', level * 2);

            foreach (var li in items)
            {
                sb.AppendLine($"{indent}- {CleanText(li.InnerText)}");

                foreach (var child in li.ChildNodes.Where(x => x.Name == "ul"))
                {
                    ProcessList(child, sb, level + 1);
                }
            }

            sb.AppendLine();
        }

        private static void ProcessOrderedList(HtmlNode ol, StringBuilder sb, int level)
        {
            var items = ol.SelectNodes("./li");
            if (items == null) return;

            string indent = new string(' ', level * 2);
            int count = 1;

            foreach (var li in items)
            {
                sb.AppendLine($"{indent}{count}. {CleanText(li.InnerText)}");
                count++;
            }

            sb.AppendLine();
        }

        private static void ProcessTable(HtmlNode table, StringBuilder sb)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0) return;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("./th|./td");
                if (cells == null) continue;

                var line = string.Join(" ",
                    cells.Select(c => CleanText(c.InnerText)));

                sb.AppendLine(line);
            }

            sb.AppendLine();
        }

        private static void AppendLine(StringBuilder sb, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            sb.AppendLine(text);
            sb.AppendLine();
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
    }
}