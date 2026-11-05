using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Render();
            }
        }

        public void Render()
        {
            var doc = new FlowDocument { PagePadding = new Thickness(0), FontFamily = FontFamily, FontSize = FontSize };
            Theme.Ref(doc, FlowDocument.ForegroundProperty, Theme.Fg);
            List listBlock = null;
            foreach (var b in MarkdownLite.Parse(_markdown))
            {
                if (b.Kind != MarkdownLite.BlockKind.ListItem) listBlock = null;
                switch (b.Kind)
                {
                    case MarkdownLite.BlockKind.Heading:
                        {
                            var p = Para(b.Inlines);
                            p.FontWeight = FontWeights.SemiBold;
                            p.FontSize = FontSize * (b.Level <= 1 ? 1.35 : b.Level == 2 ? 1.2 : 1.08);
                            p.Margin = new Thickness(0, 6, 0, 2);
                            doc.Blocks.Add(p);
                            break;
                        }
                    case MarkdownLite.BlockKind.ListItem:
                        {
                            if (listBlock == null)
                            {
                                listBlock = new List { MarkerStyle = b.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(18, 0, 0, 0) };
                                doc.Blocks.Add(listBlock);
                            }
                            var p = Para(b.Inlines);
                            p.Margin = new Thickness(b.Level * 14, 0, 0, 0);
                            listBlock.ListItems.Add(new ListItem(p));
                            break;
                        }
                    case MarkdownLite.BlockKind.Code:
                        {
                            var p = new Paragraph(new Run(b.Code)) { FontFamily = new FontFamily("Consolas"), FontSize = FontSize * 0.92, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 4, 0, 4) };
                            Theme.Ref(p, Block.BackgroundProperty, Theme.CodeBg);
                            doc.Blocks.Add(p);
                            break;
                        }
                    case MarkdownLite.BlockKind.Table:
                        doc.Blocks.Add(TableBlock(b));
                        break;
                    case MarkdownLite.BlockKind.Quote:
                        {
                            var p = Para(b.Inlines);
                            p.Margin = new Thickness(0, 2, 0, 2);
                            p.Padding = new Thickness(8, 0, 0, 0);
                            p.BorderThickness = new Thickness(3, 0, 0, 0);
                            Theme.Ref(p, Block.BorderBrushProperty, Theme.Border);
                            Theme.Ref(p, TextElement.ForegroundProperty, Theme.MutedFg);
                            doc.Blocks.Add(p);
                            break;
                        }
                    case MarkdownLite.BlockKind.Rule:
                        {
                            var p = new Paragraph { Margin = new Thickness(0, 4, 0, 4), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 2 };
                            Theme.Ref(p, Block.BorderBrushProperty, Theme.Border);
                            doc.Blocks.Add(p);
                            break;
                        }
                    default:
                        {
                            var p = Para(b.Inlines);
                            p.Margin = new Thickness(0, 2, 0, 4);
                            doc.Blocks.Add(p);
                            break;
                        }
                }
            }
            Document = doc;
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
