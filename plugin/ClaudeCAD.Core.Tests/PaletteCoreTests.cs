using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;
using K = ClaudeCAD.Plugin.Core.MarkdownLite.BlockKind;
using I = ClaudeCAD.Plugin.Core.MarkdownLite.InlineKind;

namespace ClaudeCAD.Core.Tests
{
    public class MarkdownLiteTests
    {
        [Fact]
        public void ParsesBlocks()
        {
            var md = "## Kết quả\n\nĐã vẽ **15** đối tượng:\n- tường *A-WALL*\n- cửa `A-DOOR`\n1. bước một\n\n```json\n{\"a\":1}\n```\n\n| Layer | Số |\n| --- | --- |\n| A-WALL | 4 |\n\n---\n> ghi chú";
            var b = MarkdownLite.Parse(md);
            Assert.Equal(new[] { K.Heading, K.Paragraph, K.ListItem, K.ListItem, K.ListItem, K.Code, K.Table, K.Rule, K.Quote }, b.Select(x => x.Kind).ToArray());
            Assert.Equal(2, b[0].Level);
            Assert.Contains(b[1].Inlines, i => i.Kind == I.Bold && i.Text == "15");
            Assert.Contains(b[2].Inlines, i => i.Kind == I.Italic && i.Text == "A-WALL");
            Assert.Contains(b[3].Inlines, i => i.Kind == I.Code && i.Text == "A-DOOR");
            Assert.True(b[4].Ordered);
            Assert.Equal("{\"a\":1}", b[5].Code);
            Assert.Equal("json", b[5].Language);
            Assert.Equal(2, b[6].Header.Count);
            Assert.Single(b[6].Rows);
        }

        [Fact]
        public void LinksAndHandles()
        {
            var inl = MarkdownLite.ParseInlines("xem [3 cửa](handle:2f4a,2F4B, 2F50) và [docs](https://x.y)");
            var link = inl.First(i => i.Kind == I.Link);
            Assert.Equal("3 cửa", link.Text);
            Assert.Equal(new List<string> { "2F4A", "2F4B", "2F50" }, MarkdownLite.HandlesFromUrl(link.Url));
            Assert.Null(MarkdownLite.HandlesFromUrl("https://x.y"));
        }

        [Fact]
        public void UnderscoresInsideNamesAreNotItalic()
        {
            var inl = MarkdownLite.ParseInlines("block CUA_DI_900 và layer E_LIGHT");
            Assert.All(inl, i => Assert.Equal(I.Text, i.Kind));
        }

        [Fact]
        public void UnclosedMarkersStayText()
        {
            var inl = MarkdownLite.ParseInlines("2 * 3 = 6 và **chưa đóng");
            Assert.Equal("2 * 3 = 6 và **chưa đóng", string.Concat(inl.Select(i => i.Text)));
        }

        [Fact]
        public void PlainTextForCopy()
        {
            Assert.Equal("Đã vẽ 15 đối tượng\n• tường", MarkdownLite.ToPlainText("Đã vẽ **15** đối tượng\n\n- tường").Replace("\r\n", "\n"));
        }
    }

    public class ChatStoreTests
    {
        [Fact]
        public void RoundTripsTranscriptOnDisk()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ccchat-" + Guid.NewGuid().ToString("N"));
            var store = new ChatStore(dir);
            var key = ChatTranscript.KeyFor(@"D:\Du an\Nha A.dwg");
            var t = store.Load(key, "Nha A.dwg");
            Assert.Empty(t.Entries);
            t.SessionId = "sess-123";
            t.Entries.Add(new ChatEntry { Role = "user", Text = "vẽ lưới trục", SelectionCount = 3, ImageCount = 1 });
            var a = new ChatEntry { Role = "assistant", Text = "Đã vẽ **9** trục", Handles = { "2F4A" } };
            a.Tools.Add(new ToolLogEntry { Tool = "batch_execute", State = "done", Ms = 180, Summary = "9 đối tượng" });
            t.Entries.Add(a);
            store.Save(t);

            var back = new ChatStore(dir).Load(key, "Nha A.dwg");
            Assert.Equal("sess-123", back.SessionId);
            Assert.Equal(2, back.Entries.Count);
            Assert.Equal(3, back.Entries[0].SelectionCount);
            Assert.Equal("batch_execute", back.Entries[1].Tools[0].Tool);
            Assert.Equal(180, back.Entries[1].Tools[0].Ms);
            Assert.Equal("2F4A", back.Entries[1].Handles[0]);
            Assert.Contains("Thao tác (1)", back.ToMarkdown());
            store.Delete(key);
            Assert.Empty(new ChatStore(dir).Load(key, "x").Entries);
        }

        [Fact]
        public void KeysAreCaseInsensitiveAndFileNamesStable()
        {
            Assert.Equal(ChatTranscript.KeyFor(@"C:\A\B.DWG"), ChatTranscript.KeyFor(@"c:\a\b.dwg"));
            Assert.Equal(ChatTranscript.FileNameFor("x"), ChatTranscript.FileNameFor("x"));
            Assert.NotEqual(ChatTranscript.FileNameFor("x"), ChatTranscript.FileNameFor("y"));
        }

        [Fact]
        public void TrimsLongHistories()
        {
            var t = new ChatTranscript();
            for (int i = 0; i < ChatTranscript.MaxEntries + 50; i++) t.Entries.Add(new ChatEntry { Role = "user", Text = i.ToString() });
            t.Trim();
            Assert.Equal(ChatTranscript.MaxEntries, t.Entries.Count);
            Assert.Equal("50", t.Entries[0].Text);
        }
    }

    public class PaletteSettingsTests
    {
        [Fact]
        public void RoundTripAndValidation()
        {
            var s = new PaletteSettings { Standard = "ve-mep", Scale = "1:50", Model = "claude-sonnet-5-5", Effort = "high", FontSize = 15, Theme = "light", Notify = false };
            s.AddHistory("a"); s.AddHistory("b"); s.AddHistory("a");
            var back = PaletteSettings.FromJson(s.ToJson());
            Assert.Equal("ve-mep", back.Standard);
            Assert.Equal("1:50", back.Scale);
            Assert.Equal("high", back.Effort);
            Assert.Equal(15, back.FontSize);
            Assert.False(back.Notify);
            Assert.Equal(new List<string> { "b", "a" }, back.History);
            var bad = PaletteSettings.FromJson("{\"standard\":\"Hack!\",\"effort\":\"insane\",\"font_size\":99}");
            Assert.Equal("", bad.Standard);
            Assert.Equal("", bad.Effort);
            Assert.Equal(22, bad.FontSize);
        }

        [Fact]
        public void HistoryCursorKeepsDraft()
        {
            var c = new InputHistoryCursor(new List<string> { "một", "hai" });
            Assert.Equal("hai", c.Previous("đang gõ"));
            Assert.Equal("một", c.Previous(null));
            Assert.Equal("một", c.Previous(null));
            Assert.Equal("hai", c.Next());
            Assert.Equal("đang gõ", c.Next());
            Assert.Null(c.Next());
        }
    }

    public class QuickTemplatesTests
    {
        [Fact]
        public void ShippedTemplatesFileIsValid()
        {
            var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "templates.json");
            var list = QuickTemplates.Parse(File.ReadAllText(file));
            Assert.True(list.Count >= 10);
            Assert.All(list, t => Assert.Matches("^[a-z0-9-]+$", t.Name));
        }

        [Fact]
        public void MergeFilterExpand()
        {
            var shipped = QuickTemplates.Parse("[{\"name\":\"luoi-truc\",\"title\":\"Lưới trục\",\"text\":\"Vẽ {{số trục=5}} trục bước {{bước=4000}}\"},{\"name\":\"mat-bang\",\"text\":\"x\"}]");
            var user = QuickTemplates.Parse("{\"templates\":[{\"name\":\"/luoi-truc\",\"title\":\"Lưới của tớ\",\"text\":\"Vẽ {{số trục}} trục\"},{\"name\":\"rieng\",\"text\":\"y\"}]}");
            var all = QuickTemplates.Merge(shipped, user);
            Assert.Equal(new[] { "luoi-truc", "mat-bang", "rieng" }, all.Select(t => t.Name).ToArray());
            Assert.Equal("Lưới của tớ", all[0].Title);
            Assert.Equal("mat-bang", QuickTemplates.Filter(all, "/mat")[0].Name);
            Assert.Equal(3, QuickTemplates.Filter(all, "/").Count);

            var text = QuickTemplates.Expand(shipped[0].Text, out var start, out var len);
            Assert.Equal("Vẽ 5 trục bước 4000", text);
            Assert.Equal(3, start);
            Assert.Equal(1, len);
            Assert.Equal("Vẽ số trục trục", QuickTemplates.Expand(all[0].Text, out _, out _));
        }

        [Fact]
        public void ExcelPasteBecomesMarkdownTable()
        {
            var md = ClipboardText.TsvToMarkdownTable("Điểm\tX (m)\tY (m)\r\nM1\t1234012.5\t567045.25\r\nM2\t1234020\t567050\r\n", out var rows, out var cols);
            Assert.Equal(3, rows);
            Assert.Equal(3, cols);
            Assert.StartsWith("| Điểm | X (m) | Y (m) |", md);
            Assert.Contains("| M2 | 1234020 | 567050 |", md);
            Assert.Null(ClipboardText.TsvToMarkdownTable("chỉ một dòng\tcó tab", out _, out _));
            Assert.Null(ClipboardText.TsvToMarkdownTable("không có tab\nhai dòng", out _, out _));
        }
    }
}
