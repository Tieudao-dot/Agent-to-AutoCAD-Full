using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Small Markdown subset for the chat palette (feature 16): headings, paragraphs,
    /// bullet / numbered lists, fenced code, tables, quotes, rules, and inline bold,
    /// italic, code and links. The UI turns this model into a WPF FlowDocument.
    /// </summary>
    public static class MarkdownLite
    {
        public enum InlineKind { Text, Bold, Italic, BoldItalic, Code, Link, LineBreak }

        public sealed class Inline
        {
            public InlineKind Kind;
            public string Text;
            public string Url;
            public override string ToString() => Kind + ":" + Text + (Url != null ? "->" + Url : "");
        }

        public enum BlockKind { Paragraph, Heading, ListItem, Code, Table, Quote, Rule }

        public sealed class Block
        {
            public BlockKind Kind;
            public int Level;            // heading level, list indent level
            public bool Ordered;         // list item
            public string Marker;        // "1." / "•"
            public string Language;      // code
            public string Code;          // code text
            public List<Inline> Inlines = new List<Inline>();
            public List<List<Inline>> Header;            // table
            public List<List<List<Inline>>> Rows;        // table
        }

        private static readonly Regex HeadingRx = new Regex(@"^(#{1,6})\s+(.*)$");
        private static readonly Regex BulletRx = new Regex(@"^(\s*)[-*+]\s+(.*)$");
        private static readonly Regex OrderedRx = new Regex(@"^(\s*)(\d+)[.)]\s+(.*)$");
        private static readonly Regex RuleRx = new Regex(@"^\s*([-*_])(\s*\1){2,}\s*$");
        private static readonly Regex TableSepRx = new Regex(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$");

        public static List<Block> Parse(string md)
        {
            var blocks = new List<Block>();
            if (string.IsNullOrEmpty(md)) return blocks;
            var lines = md.Replace("\r\n", "\n").Split('\n');
            var para = new StringBuilder();

            void FlushPara()
            {
                if (para.Length == 0) return;
                blocks.Add(new Block { Kind = BlockKind.Paragraph, Inlines = ParseInlines(para.ToString().TrimEnd('\n')) });
                para.Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```"))
                {
                    FlushPara();
                    var lang = trimmed.Substring(3).Trim();
                    var code = new StringBuilder();
                    i++;
                    while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                    {
                        code.Append(lines[i]).Append('\n');
                        i++;
                    }
                    blocks.Add(new Block { Kind = BlockKind.Code, Language = lang, Code = code.ToString().TrimEnd('\n') });
                    continue;
                }
                if (trimmed.Length == 0)
                {
                    FlushPara();
                    continue;
                }
                if (RuleRx.IsMatch(line) && para.Length == 0)
                {
                    blocks.Add(new Block { Kind = BlockKind.Rule });
                    continue;
                }
                var h = HeadingRx.Match(trimmed);
                if (h.Success)
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.Heading, Level = h.Groups[1].Length, Inlines = ParseInlines(h.Groups[2].Value.TrimEnd('#', ' ')) });
                    continue;
                }
                if (trimmed.StartsWith("|") && i + 1 < lines.Length && TableSepRx.IsMatch(lines[i + 1]))
                {
                    FlushPara();
                    var table = new Block { Kind = BlockKind.Table, Header = SplitRow(trimmed), Rows = new List<List<List<Inline>>>() };
                    i += 2;
                    while (i < lines.Length && lines[i].Trim().StartsWith("|"))
                    {
                        table.Rows.Add(SplitRow(lines[i].Trim()));
                        i++;
                    }
                    i--;
                    blocks.Add(table);
                    continue;
                }
                var b = BulletRx.Match(line);
                if (b.Success)
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.ListItem, Level = b.Groups[1].Value.Replace("\t", "  ").Length / 2, Marker = "•", Inlines = ParseInlines(b.Groups[2].Value) });
                    continue;
                }
                var o = OrderedRx.Match(line);
                if (o.Success)
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.ListItem, Ordered = true, Level = o.Groups[1].Value.Replace("\t", "  ").Length / 2, Marker = o.Groups[2].Value + ".", Inlines = ParseInlines(o.Groups[3].Value) });
                    continue;
                }
                if (trimmed.StartsWith(">"))
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.Quote, Inlines = ParseInlines(trimmed.TrimStart('>').TrimStart()) });
                    continue;
                }
                para.Append(line.TrimEnd()).Append('\n');
            }
            FlushPara();
            return blocks;
        }

        private static List<List<Inline>> SplitRow(string row)
        {
            var t = row.Trim();
            if (t.StartsWith("|")) t = t.Substring(1);
            if (t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
            var cells = new List<List<Inline>>();
            foreach (var c in t.Split('|')) cells.Add(ParseInlines(c.Trim()));
            return cells;
        }

        /// <summary>Inline parser: **bold**, *italic* / _italic_, ***both***, `code`, [text](url), line breaks.</summary>
        public static List<Inline> ParseInlines(string s)
        {
            var result = new List<Inline>();
            var text = new StringBuilder();

            void Flush()
            {
                if (text.Length == 0) return;
                result.Add(new Inline { Kind = InlineKind.Text, Text = text.ToString() });
                text.Clear();
            }

            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\n')
                {
                    Flush();
                    result.Add(new Inline { Kind = InlineKind.LineBreak, Text = "\n" });
                    i++;
                    continue;
                }
                if (c == '\\' && i + 1 < s.Length && "*_`[]()#\\|".IndexOf(s[i + 1]) >= 0)
                {
                    text.Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '`')
                {
                    int end = s.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        Flush();
                        result.Add(new Inline { Kind = InlineKind.Code, Text = s.Substring(i + 1, end - i - 1) });
                        i = end + 1;
                        continue;
                    }
                }
                if (c == '[')
                {
                    int close = s.IndexOf("](", i + 1, StringComparison.Ordinal);
                    int paren = close > 0 ? s.IndexOf(')', close + 2) : -1;
                    if (close > i && paren > close && s.IndexOf('\n', i, paren - i) < 0)
                    {
                        Flush();
                        result.Add(new Inline { Kind = InlineKind.Link, Text = s.Substring(i + 1, close - i - 1), Url = s.Substring(close + 2, paren - close - 2).Trim() });
                        i = paren + 1;
                        continue;
                    }
                }
                if (c == '*' || c == '_')
                {
                    int run = 1;
                    while (i + run < s.Length && s[i + run] == c && run < 3) run++;
                    // '_' inside words (snake_case, layer names) is not emphasis
                    bool wordLeft = i > 0 && char.IsLetterOrDigit(s[i - 1]);
                    if (!(c == '_' && wordLeft))
                    {
                        string delim = new string(c, run);
                        int end = s.IndexOf(delim, i + run, StringComparison.Ordinal);
                        if (end > i + run && !char.IsWhiteSpace(s[i + run]) && !char.IsWhiteSpace(s[end - 1]))
                        {
                            Flush();
                            var kind = run == 3 ? InlineKind.BoldItalic : run == 2 ? InlineKind.Bold : InlineKind.Italic;
                            result.Add(new Inline { Kind = kind, Text = s.Substring(i + run, end - i - run) });
                            i = end + run;
                            continue;
                        }
                    }
                    text.Append(s, i, run);
                    i += run;
                    continue;
                }
                text.Append(c);
                i++;
            }
            Flush();
            return result;
        }

        /// <summary>Handles in a "handle:2F4A,2F4B" link; null for other URLs.</summary>
        public static List<string> HandlesFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("handle:", StringComparison.OrdinalIgnoreCase)) return null;
            var list = new List<string>();
            foreach (var part in url.Substring(7).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (Params.IsHex(part.Trim())) list.Add(part.Trim().ToUpperInvariant());
            return list;
        }

        /// <summary>Plain text of a message (for copy): drops markdown markers but keeps structure.</summary>
        public static string ToPlainText(string md)
        {
            var sb = new StringBuilder();
            foreach (var b in Parse(md))
            {
                switch (b.Kind)
                {
                    case BlockKind.Code: sb.AppendLine(b.Code); break;
                    case BlockKind.Rule: sb.AppendLine("———"); break;
                    case BlockKind.Table:
                        sb.AppendLine(string.Join("\t", b.Header.ConvertAll(Join)));
                        foreach (var r in b.Rows) sb.AppendLine(string.Join("\t", r.ConvertAll(Join)));
                        break;
                    case BlockKind.ListItem:
                        sb.Append(new string(' ', b.Level * 2)).Append(b.Marker).Append(' ').AppendLine(Join(b.Inlines));
                        break;
                    default: sb.AppendLine(Join(b.Inlines)); break;
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string Join(List<Inline> inlines)
        {
            var sb = new StringBuilder();
            foreach (var i in inlines) sb.Append(i.Text);
            return sb.ToString();
        }
    }
}
