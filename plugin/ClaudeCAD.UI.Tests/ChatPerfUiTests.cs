using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;
using Xunit.Abstractions;

// The perf test below switches the two speed-ups off and on (static switches) and times both ways: no other UI
// test may run at the same time, or its numbers and the other test would disturb each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// Khung chat dài (~300 tin có bảng, khối code, danh sách, dòng công cụ, ghi chú): đo thời gian cuộn, gõ và
    /// Claude đang viết, lần lượt với hai cách tối ưu tắt và bật, in số đo ra log CI. Kiểm thêm rằng bật tối ưu
    /// không đổi gì người dùng nhìn thấy (chiều cao, vị trí cuộn, chữ của câu trả lời).
    /// </summary>
    public class ChatPerfUiTests
    {
        private readonly ITestOutputHelper _out;
        public ChatPerfUiTests(ITestOutputHelper output) { _out = output; }

        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrivStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static void Sta(Action a)
        {
            Exception error = null;
            var t = new Thread(() =>
            {
                try { a(); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw new Exception("UI test failed: " + error.Message, error);
        }

        private static void SetFast(bool on)
        {
            typeof(ChatControl).GetField("HideOffscreen", PrivStatic).SetValue(null, on);
            typeof(MarkdownView).GetField("Incremental", PrivStatic).SetValue(null, on);
        }

        private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Priv).GetValue(o);

        private static string Reply(int i)
        {
            var sb = new StringBuilder();
            sb.Append("## Kết quả lượt ").Append(i).Append("\n\nĐã vẽ **lưới trục** và *tường bao* theo yêu cầu, xem `layer A-WALL`.\n\n");
            if (i % 3 == 0)
            {
                sb.Append("| Phòng | Diện tích (m²) | Ghi chú |\n|---|---|---|\n");
                for (int r = 0; r < 6; r++) sb.Append("| P").Append(r + 1).Append(" | ").Append(12 + r * 3.5).Append(" | phòng ngủ ").Append(r).Append(" |\n");
                sb.Append('\n');
            }
            if (i % 4 == 1) sb.Append("```\n(command \"_LINE\" '(0 0) '(4000 0) \"\")\n(command \"_OFFSET\" 200 (entlast) '(0 100) \"\")\n```\n\n");
            sb.Append("1. Lưới trục 5x4 bước 4000\n2. Tường dày 200\n   - tường ngoài\n   - tường trong\n3. Cửa đi D1 [xem](handle:2F4A,2F4B)\n\n");
            sb.Append("> Lưu ý: kiểm lại cao độ trước khi dựng 3D.\n\nCâu trả lời dài thêm vài dòng để mỗi tin cao như thật: ");
            for (int k = 0; k < 6; k++) sb.Append("đoạn chữ mẫu số ").Append(k).Append(", ");
            return sb.ToString();
        }

        /// <summary>A palette with <paramref name="turns"/> user + assistant pairs (and a note every 10 turns).</summary>
        private static ChatControl LongChat(int turns, out double buildMs)
        {
            var sw = Stopwatch.StartNew();
            var c = new ChatControl { Width = 420, Height = 800 };
            c.ApplySettings(new PaletteSettings { Standard = "ve-kien-truc", Scale = "1:100" });
            for (int i = 0; i < turns; i++)
            {
                c.AddUserMessage(new ChatEntry { Role = "user", Text = "vẽ mặt bằng tầng " + i + " có lưới trục 5x4, tường 200, cửa đi D1" });
                c.BeginAssistant();
                var tools = new List<ToolLogEntry>
                {
                    new ToolLogEntry { Tool = "draw_grid", State = "done", Ms = 120, Summary = "lưới trục 5x4" },
                    new ToolLogEntry { Tool = "draw_walls", State = "done", Ms = 340, Summary = "18 tường" },
                };
                foreach (var t in tools) c.UpdateTool(t);
                c.EndAssistant(new ChatEntry { Role = "assistant", Text = Reply(i), Tools = tools, Handles = new List<string> { "2F4A" } }, i == turns - 1);
                if (i % 10 == 9) c.AddNote("Đã lưu bản vẽ lúc " + i);
            }
            Layout(c);
            Field<ScrollViewer>(c, "_scroll").ScrollToEnd();
            Layout(c);
            buildMs = sw.Elapsed.TotalMilliseconds;
            return c;
        }

        private static void Layout(FrameworkElement e)
        {
            e.Measure(new Size(e.Width, e.Height));
            e.Arrange(new Rect(0, 0, e.Width, e.Height));
            e.UpdateLayout();
        }

        /// <summary>One frame: layout, then draw the whole palette (what WPF does after a scroll step or key press).</summary>
        private static void Frame(FrameworkElement e)
        {
            Layout(e);
            var bmp = new RenderTargetBitmap((int)e.Width, (int)e.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(e);
        }

        private sealed class Numbers
        {
            public double Build, ScrollPerStep, TypePerKey, StreamPerUpdate, Extent;
            public int Hidden, Messages;
        }

        private static Numbers Measure(bool fast, int turns)
        {
            SetFast(fast);
            var n = new Numbers();
            var c = LongChat(turns, out n.Build);
            var scroll = Field<ScrollViewer>(c, "_scroll");
            var messages = Field<StackPanel>(c, "_messages");
            n.Messages = messages.Children.Count;
            n.Extent = scroll.ExtentHeight;

            // scrolling: 60 steps from the bottom up, one frame each
            Frame(c);
            double step = Math.Max(40, scroll.ScrollableHeight / 60);
            var sw = Stopwatch.StartNew();
            int steps = 0;
            for (double off = scroll.ScrollableHeight; off > 0 && steps < 60; off -= step, steps++)
            {
                scroll.ScrollToVerticalOffset(off);
                Frame(c);
            }
            n.ScrollPerStep = sw.Elapsed.TotalMilliseconds / Math.Max(1, steps);
            n.Hidden = (int)typeof(ChatControl).GetProperty("OffscreenCount", Priv).GetValue(c);

            // typing at the bottom: 80 keys (a new line every 20, the box grows), one frame each
            scroll.ScrollToEnd();
            Frame(c);
            var input = Field<TextBox>(c, "_input");
            sw.Restart();
            for (int k = 0; k < 80; k++)
            {
                input.AppendText(k % 20 == 19 ? "\n" : "a");
                Frame(c);
            }
            n.TypePerKey = sw.Elapsed.TotalMilliseconds / 80;

            // Claude writing a long reply with a table: 120 updates of the growing text, one frame each
            c.AddUserMessage(new ChatEntry { Role = "user", Text = "thống kê phòng" });
            c.BeginAssistant();
            var current = Field<object>(c, "_current");
            var md = (MarkdownView)current.GetType().GetField("_md", Priv).GetValue(current);
            var text = new StringBuilder("## Bảng thống kê\n\nĐây là bảng diện tích các phòng của tầng 1:\n\n| Phòng | Diện tích | Ghi chú |\n|---|---|---|\n");
            sw.Restart();
            for (int u = 0; u < 120; u++)
            {
                text.Append("| P").Append(u).Append(" | ").Append(10 + u).Append(" | phòng ").Append(u).Append(" |\n");
                if (u % 30 == 29) text.Append("\nĐoạn giữa bảng số ").Append(u).Append(".\n\n| Phòng | Diện tích | Ghi chú |\n|---|---|---|\n");
                md.Markdown = text.ToString();
                scroll.ScrollToEnd();
                Frame(c);
            }
            n.StreamPerUpdate = sw.Elapsed.TotalMilliseconds / 120;
            return n;
        }

        [Fact]
        public void LongChatScrollsTypesAndStreamsFasterWithoutChangingWhatIsShown()
        {
            Numbers off = null, on = null;
            string report = null;
            try
            {
                Sta(() =>
                {
                    Measure(false, 10); // warm-up (JIT, fonts) so neither way pays for it
                    Measure(true, 10);
                    off = Measure(false, 150);
                    on = Measure(true, 150);
                });
            }
            finally { SetFast(true); }

            report = string.Join(Environment.NewLine, new[]
            {
                $"Khung chat dài: {on.Messages} tin (150 lượt hỏi + trả lời, bảng, khối code, danh sách, dòng công cụ, ghi chú), khung 420x800",
                $"                         trước (tắt)   sau (bật)",
                $"Dựng 300 tin + layout:   {off.Build,8:F0} ms   {on.Build,8:F0} ms",
                $"Cuộn, mỗi bước:          {off.ScrollPerStep,8:F1} ms   {on.ScrollPerStep,8:F1} ms",
                $"Gõ, mỗi phím:            {off.TypePerKey,8:F1} ms   {on.TypePerKey,8:F1} ms",
                $"Claude đang viết, mỗi lần cập nhật: {off.StreamPerUpdate,8:F1} ms   {on.StreamPerUpdate,8:F1} ms",
                $"Tin đang ẩn tạm (ngoài màn hình): {off.Hidden} / {on.Hidden}",
            });
            _out.WriteLine(report);
            try { File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("CLAUDECAD_UI_SHOTS") ?? AppContext.BaseDirectory, "perf-khung-chat.txt"), report); } catch { }

            // nothing the user sees moves: same messages, same total height (so the scroll bar and positions match)
            Assert.Equal(off.Messages, on.Messages);
            Assert.True(Math.Abs(off.Extent - on.Extent) < 1, $"height changed: {off.Extent} vs {on.Extent}");
            Assert.Equal(0, off.Hidden);
            Assert.True(on.Hidden > on.Messages / 2, "far messages were not hidden: " + on.Hidden);
            // the speed-ups must not be slower (generous margin for a noisy CI machine)
            Assert.True(on.ScrollPerStep <= off.ScrollPerStep * 1.3 + 3, report);
            Assert.True(on.TypePerKey <= off.TypePerKey * 1.3 + 3, report);
            Assert.True(on.StreamPerUpdate <= off.StreamPerUpdate * 1.3 + 3, report);
        }

        [Fact]
        public void HiddenMessagesComeBackBeforeTheyScrollIntoView()
        {
            int hiddenNearTop = -1, hiddenAtBottom = -1, hiddenAfterOff = -1;
            bool firstVisibleAtTop = false, lastVisibleAtBottom = false;
            Sta(() =>
            {
                SetFast(true);
                var c = LongChat(60, out _);
                var scroll = Field<ScrollViewer>(c, "_scroll");
                var messages = Field<StackPanel>(c, "_messages");
                var children = messages.Children.Cast<UIElement>().ToList();
                Layout(c);
                lastVisibleAtBottom = children.Last().Visibility == Visibility.Visible;
                hiddenAtBottom = children.Count(x => x.Visibility == Visibility.Hidden);

                scroll.ScrollToTop();
                Layout(c);
                Layout(c);
                firstVisibleAtTop = children.First().Visibility == Visibility.Visible;
                // everything on screen and within one more screen below it is shown. Positions are in the scroll
                // content: the message list starts after its own margin, like what the user scrolls.
                double y = messages.Margin.Top;
                hiddenNearTop = 0;
                foreach (var x in children)
                {
                    if (y > scroll.VerticalOffset + scroll.ViewportHeight * 2) break;
                    if (x.Visibility == Visibility.Hidden) hiddenNearTop++;
                    y += x.DesiredSize.Height;
                }

                // switching the speed-up off shows everything again
                typeof(ChatControl).GetField("HideOffscreen", PrivStatic).SetValue(null, false);
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 10);
                Layout(c);
                Layout(c);
                hiddenAfterOff = children.Count(x => x.Visibility == Visibility.Hidden);
                SetFast(true);
            });
            Assert.True(lastVisibleAtBottom);
            Assert.True(hiddenAtBottom > 0);
            Assert.True(firstVisibleAtTop);
            Assert.Equal(0, hiddenNearTop);
            Assert.Equal(0, hiddenAfterOff);
        }

        [Fact]
        public void StreamedReplyEndsWithTheSameTextAsARebuild()
        {
            string streamed = null, rebuilt = null;
            int streamedBlocks = 0, rebuiltBlocks = 0;
            Sta(() =>
            {
                SetFast(true);
                var md = "# Tiêu đề\n\nĐoạn *nghiêng* **đậm** `code` [link](handle:AB,CD)\n\n1. một\n2. hai\n   - con\n\n```\ncode block\n```\n\n> trích dẫn\n\n---\n\n| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n\n- cuối 1\n- cuối 2\n\nĐoạn kết.";
                var live = new MarkdownView { FontSize = 13 };
                for (int i = 1; i <= md.Length; i += 3) live.Markdown = md.Substring(0, i); // as if streamed
                live.Markdown = md;
                var full = new MarkdownView { FontSize = 13, Markdown = md };
                full.Render();
                streamed = new TextRange(live.Document.ContentStart, live.Document.ContentEnd).Text;
                rebuilt = new TextRange(full.Document.ContentStart, full.Document.ContentEnd).Text;
                streamedBlocks = live.Document.Blocks.Count;
                rebuiltBlocks = full.Document.Blocks.Count;
                // a font change rebuilds everything at the new size
                live.FontSize = 16;
                live.Render();
                Assert.Equal(16, live.Document.FontSize);
            });
            Assert.Equal(rebuilt, streamed);
            Assert.Equal(rebuiltBlocks, streamedBlocks);
        }
    }
}
