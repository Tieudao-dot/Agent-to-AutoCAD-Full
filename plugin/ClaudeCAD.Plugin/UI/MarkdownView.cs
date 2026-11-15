using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClaudeCAD.Plugin.Core;
using MdBlock = ClaudeCAD.Plugin.Core.MarkdownLite.Block;
using MdInline = ClaudeCAD.Plugin.Core.MarkdownLite.Inline;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Renders assistant Markdown (feature 16) into a read-only RichTextBox. Links of
    /// the form handle:2F4A,2F4B call <see cref="HandleLinkClicked"/> (feature 3).
    /// </summary>
    public sealed class MarkdownView : RichTextBox
    {
        private string _markdown = "";

        /// <summary>
        /// While Claude is writing, the reply is set again every 150 ms. Off: the whole FlowDocument is rebuilt each
        /// time (a long reply with tables re-lays out from scratch). On: the blocks that did not change stay, only the
        /// first changed block and those after it are rebuilt. Rerender (font, theme) always rebuilds everything.
        /// Static so the UI perf test can measure both ways.
        /// </summary>
        internal static bool Incremental = true;

        // one key per top-level block of Document (a run of list items is one List block)
        private readonly List<string> _keys = new List<string>();
        private double _docFontSize;
        private FontFamily _docFont;

        public event Action<List<string>> HandleLinkClicked;

        public MarkdownView()
        {
            IsReadOnly = true;
            IsDocumentEnabled = true;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            Padding = new Thickness(0);
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Theme.Ref(this, ForegroundProperty, Theme.Fg);
            // read-only text set by code: an undo stack would only keep every streamed version alive
            IsUndoEnabled = false;
            Document = new FlowDocument { PagePadding = new Thickness(0) };
            // keep the mouse wheel for the outer chat scroller
            PreviewMouseWheel += (s, e) =>
            {
                if (e.Handled) return;
                e.Handled = true;
                var parent = Parent as UIElement;
                parent?.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
            };
        }

        public string Markdown
        {
            get => _markdown;
            set
            {
                _markdown = value ?? "";
                Render(full: !Incremental);
            }
        }

        /// <summary>Rebuilds the whole document (font size or theme changed).</summary>
        public void Render() => Render(full: true);

        private void Render(bool full)
        {
            var groups = Group(MarkdownLite.Parse(_markdown));
            var keys = groups.Select(Key).ToList();
            var doc = Document;
            bool reuse = !full && doc != null && _keys.Count > 0 && doc.Blocks.Count == _keys.Count
                && _docFontSize == FontSize && Equals(_docFont, FontFamily);
            if (!reuse)
            {
                doc = new FlowDocument { PagePadding = new Thickness(0), FontFamily = FontFamily, FontSize = FontSize };
                Theme.Ref(doc, FlowDocument.ForegroundProperty, Theme.Fg);
                foreach (var g in groups) doc.Blocks.Add(BuildBlock(g));
                _keys.Clear();
                _keys.AddRange(keys);
                _docFontSize = FontSize;
                _docFont = FontFamily;
                Document = doc;
                return;
            }
            int same = 0;
            while (same < keys.Count && same < _keys.Count && keys[same] == _keys[same]) same++;
            if (same == keys.Count && same == _keys.Count) return; // nothing changed
            foreach (var old in doc.Blocks.Skip(same).ToList()) doc.Blocks.Remove(old);
            for (int i = same; i < groups.Count; i++) doc.Blocks.Add(BuildBlock(groups[i]));
            _keys.RemoveRange(same, _keys.Count - same);
            _keys.AddRange(keys.Skip(same));
        }

        /// <summary>Consecutive list items become one group (one WPF List), every other block its own group.</summary>
        private static List<List<MdBlock>> Group(List<MdBlock> blocks)
        {
            var groups = new List<List<MdBlock>>();
            foreach (var b in blocks)
            {
                if (b.Kind == MarkdownLite.BlockKind.ListItem && groups.Count > 0 && groups[groups.Count - 1][0].Kind == MarkdownLite.BlockKind.ListItem)
                    groups[groups.Count - 1].Add(b);
                else
                    groups.Add(new List<MdBlock> { b });
            }
            return groups;
        }

        /// <summary>Everything a block is built from, so equal keys give the same WPF block.</summary>
        internal static string Key(List<MdBlock> group)
        {
            var sb = new StringBuilder();
            foreach (var b in group)
            {
                sb.Append((int)b.Kind).Append('\u0001').Append(b.Level).Append('\u0001').Append(b.Ordered ? '1' : '0').Append('\u0001').Append(b.Code).Append('\u0001');
                Inlines(sb, b.Inlines);
                if (b.Header != null) { sb.Append('\u0003'); foreach (var c in b.Header) Inlines(sb, c); }
                if (b.Rows != null) foreach (var r in b.Rows) { sb.Append('\u0004'); foreach (var c in r) Inlines(sb, c); }
                sb.Append('\u0005');
            }
            return sb.ToString();
        }

        private static void Inlines(StringBuilder sb, List<MdInline> inlines)
        {
            if (inlines == null) return;
            foreach (var i in inlines) sb.Append((int)i.Kind).Append('\u0002').Append(i.Text).Append('\u0002').Append(i.Url).Append('\u0002');
            sb.Append('\u0006');
        }

        private Block BuildBlock(List<MdBlock> group)
        {
            var b = group[0];
            if (b.Kind == MarkdownLite.BlockKind.ListItem)
            {
                var listBlock = new List { MarkerStyle = b.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(18, 0, 0, 0) };
                foreach (var item in group)
                {
                    var p = Para(item.Inlines);
                    p.Margin = new Thickness(item.Level * 14, 0, 0, 0);
                    listBlock.ListItems.Add(new ListItem(p));
                }
                return listBlock;
            }
            switch (b.Kind)
            {
                case MarkdownLite.BlockKind.Heading:
                    {
                        var p = Para(b.Inlines);
                        p.FontWeight = FontWeights.SemiBold;
                        p.FontSize = FontSize * (b.Level <= 1 ? 1.35 : b.Level == 2 ? 1.2 : 1.08);
                        p.Margin = new Thickness(0, 6, 0, 2);
                        return p;
                    }
                case MarkdownLite.BlockKind.Code:
                    {
                        var p = new Paragraph(new Run(b.Code)) { FontFamily = new FontFamily("Consolas"), FontSize = FontSize * 0.92, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 4, 0, 4) };
                        Theme.Ref(p, Block.BackgroundProperty, Theme.CodeBg);
                        return p;
                    }
                case MarkdownLite.BlockKind.Table:
                    return TableBlock(b);
                case MarkdownLite.BlockKind.Quote:
                    {
                        var p = Para(b.Inlines);
                        p.Margin = new Thickness(0, 2, 0, 2);
                        p.Padding = new Thickness(8, 0, 0, 0);
                        p.BorderThickness = new Thickness(3, 0, 0, 0);
                        Theme.Ref(p, Block.BorderBrushProperty, Theme.Border);
                        Theme.Ref(p, TextElement.ForegroundProperty, Theme.MutedFg);
                        return p;
                    }
                case MarkdownLite.BlockKind.Rule:
                    {
                        var p = new Paragraph { Margin = new Thickness(0, 4, 0, 4), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 2 };
                        Theme.Ref(p, Block.BorderBrushProperty, Theme.Border);
                        return p;
                    }
                default:
                    {
                        var p = Para(b.Inlines);
                        p.Margin = new Thickness(0, 2, 0, 4);
                        return p;
                    }
            }
        }

        private Paragraph Para(List<MdInline> inlines)
        {
            var p = new Paragraph { Margin = new Thickness(0) };
            foreach (var i in inlines) p.Inlines.Add(InlineFor(i));
            return p;
        }

        private System.Windows.Documents.Inline InlineFor(MdInline i)
        {
            switch (i.Kind)
            {
                case MarkdownLite.InlineKind.Bold: return new Bold(new Run(i.Text));
                case MarkdownLite.InlineKind.Italic: return new Italic(new Run(i.Text));
                case MarkdownLite.InlineKind.BoldItalic: return new Bold(new Italic(new Run(i.Text)));
                case MarkdownLite.InlineKind.LineBreak: return new LineBreak();
                case MarkdownLite.InlineKind.Code:
                    {
                        var r = new Run(i.Text) { FontFamily = new FontFamily("Consolas") };
                        Theme.Ref(r, TextElement.BackgroundProperty, Theme.CodeBg);
                        return r;
                    }
                case MarkdownLite.InlineKind.Link:
                    {
                        var link = new Hyperlink(new Run(i.Text)) { Cursor = System.Windows.Input.Cursors.Hand };
                        Theme.Ref(link, TextElement.ForegroundProperty, Theme.LinkFg);
                        var handles = MarkdownLite.HandlesFromUrl(i.Url);
                        if (handles != null)
                        {
                            link.ToolTip = "Bấm để zoom tới và tô sáng " + handles.Count + " đối tượng";
                            link.Click += (s, e) => HandleLinkClicked?.Invoke(handles);
                        }
                        else if (Uri.TryCreate(i.Url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                        {
                            link.ToolTip = i.Url;
                            link.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { } };
                        }
                        return link;
                    }
                default: return new Run(i.Text);
            }
        }

        private Table TableBlock(MdBlock b)
        {
            var t = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 4), BorderThickness = new Thickness(1, 1, 0, 0) };
            Theme.Ref(t, Block.BorderBrushProperty, Theme.Border);
            int cols = b.Header.Count;
            for (int c = 0; c < cols; c++) t.Columns.Add(new TableColumn());
            var group = new TableRowGroup();
            group.Rows.Add(Row(b.Header, cols, true));
            foreach (var r in b.Rows) group.Rows.Add(Row(r, cols, false));
            t.RowGroups.Add(group);
            return t;
        }

        private TableRow Row(List<List<MdInline>> cells, int cols, bool header)
        {
            var row = new TableRow();
            for (int c = 0; c < cols; c++)
            {
                var p = Para(c < cells.Count ? cells[c] : new List<MdInline>());
                if (header) p.FontWeight = FontWeights.SemiBold;
                var cell = new TableCell(p) { Padding = new Thickness(4, 1, 4, 1), BorderThickness = new Thickness(0, 0, 1, 1) };
                Theme.Ref(cell, TableCell.BorderBrushProperty, Theme.Border);
                if (header) Theme.Ref(cell, TableCell.BackgroundProperty, Theme.CodeBg);
                row.Cells.Add(cell);
            }
            return row;
        }
    }
}
