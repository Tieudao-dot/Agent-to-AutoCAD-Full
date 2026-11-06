using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    public class PaletteUiTests
    {
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

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static string OutDir
        {
            get
            {
                var d = Environment.GetEnvironmentVariable("CLAUDECAD_UI_SHOTS") ?? Path.Combine(AppContext.BaseDirectory, "ui-screens");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private static void Shot(FrameworkElement e, string name)
        {
            e.Measure(new Size(e.Width, e.Height));
            e.Arrange(new Rect(0, 0, e.Width, e.Height));
            e.UpdateLayout();
            Pump();
            var bmp = new RenderTargetBitmap((int)e.Width * 2, (int)e.Height * 2, 192, 192, PixelFormats.Pbgra32);
            bmp.Render(e);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(Path.Combine(OutDir, name + ".png"))) enc.Save(fs);
        }

        private static ChatControl Conversation(bool dark)
        {
            var c = new ChatControl { Width = 400, Height = 900 };
            var s = new PaletteSettings { Standard = "ve-kien-truc", Scale = "1:100", Model = "claude-opus-5-5" };
            s.AddHistory("vẽ lưới trục 5x4 bước 4000");
            c.ApplySettings(s);
            c.SetDark(dark);
            c.SetTemplates(QuickTemplates.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates.json"))));
            c.SetSkills(new List<SkillInfo>
            {
                new SkillInfo { Name = "ve-kien-truc", Title = "Vẽ kiến trúc theo chuẩn", Description = "Chuẩn vẽ kiến trúc...", Builtin = true },
                new SkillInfo { Name = "ve-mep", Title = "Vẽ điện nước", Description = "Chuẩn MEP...", Builtin = true, Enabled = false },
                new SkillInfo { Name = "ve-ket-cau", Title = "Vẽ kết cấu", Description = "Chuẩn kết cấu của công ty", Added = DateTime.Now, Source = @"D:\chuan\ket-cau.md" },
                new SkillInfo { Name = "sai-ten", Error = "Thiếu dòng description" },
            });
            c.SetConnection("Đã kết nối · Opus 5.5 · Claude login", true);
            c.SetUsage("Lượt vừa rồi: ↑12.3k ↓1.1k token · phiên ≈ $0.08 · hạn mức 5 giờ 41%");

            var t = new ChatTranscript { DrawingName = "Nha-pho.dwg" };
            c.LoadTranscript(t, false);
            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước", SelectionCount = 0 });
            c.BeginAssistant();
            c.UpdateTool(new ToolLogEntry { Tool = "get_drawing_info", State = "running" });
            c.UpdateTool(new ToolLogEntry { Tool = "get_drawing_info", State = "done", Ms = 42, Summary = "0 kết quả" });
            c.UpdateTool(new ToolLogEntry { Tool = "batch_execute", State = "running" });
            c.UpdateTool(new ToolLogEntry { Tool = "batch_execute", State = "done", Ms = 380, Summary = "30 lệnh OK, 15 đối tượng" });
            c.UpdateTool(new ToolLogEntry { Tool = "zoom_extents", State = "error", Ms = 8, Error = "ACAD_ERROR: view unavailable" });
            var reply = "Tớ đã vẽ xong **phòng 4×6 m**, tường 220, tỷ lệ in 1:100 — tổng cộng 15 đối tượng.\n\n" +
                        "- **Tường** (layer `A-WALL`): hai nét liên tục, chừa lỗ cửa.\n" +
                        "- **Cửa đi 900** (layer `A-DOOR`): [cánh + cung mở](handle:2F4A,2F4B).\n" +
                        "- **Kích thước** (`A-DIM`, style `KT-100`): đủ 4 phía.\n\n" +
                        "| Layer | Số đối tượng |\n| --- | --- |\n| A-WALL | 4 |\n| A-DOOR | 2 |\n| A-DIM | 7 |\n\n" +
                        "Gõ U một lần nếu cậu muốn hoàn tác cả lượt này.";
            foreach (var chunk in Chunks(reply, 37)) c.AppendAssistant(chunk);
            var a = new ChatEntry { Role = "assistant", Text = reply, Handles = { "2F4A", "2F4B", "2F4C" } };
            c.EndAssistant(a, true);

            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Xóa toàn bộ đối tượng trên layer A-TEMP", SelectionCount = 0 });
            c.BeginAssistant();
            c.UpdateTool(new ToolLogEntry { Tool = "count_entities", State = "done", Ms = 15, Summary = "57 kết quả" });
            c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Trên layer **A-TEMP** có **57 đối tượng**. Cậu có đồng ý xóa không?" }, false);
            c.AddConfirmCard("confirm-1", "Xóa 57 đối tượng trên layer A-TEMP?", 57, new List<string> { "3A1", "3A2" });
            c.AddErrorCard("Đã chạm giới hạn sử dụng của gói Claude. Đợi một lúc rồi thử lại.", true);
            c.SetSelectionChip(new PickedSelection { Handles = { "1A", "1B", "1C" }, Summary = "2 LINE, 1 LWPOLYLINE" }, true);
            c.SetToolStatus("✓ Thực thi nhiều lệnh (batch_execute)");
            return c;
        }

        private static IEnumerable<string> Chunks(string s, int n)
        {
            for (int i = 0; i < s.Length; i += n) yield return s.Substring(i, Math.Min(n, s.Length - i));
        }

        [Fact]
        public void RendersFullConversationDarkAndLight()
        {
            File.Copy(FindTemplates(), Path.Combine(AppContext.BaseDirectory, "templates.json"), true);
            Sta(() =>
            {
                Shot(Conversation(true), "palette-dark");
                Shot(Conversation(false), "palette-light");
            });
            Assert.True(File.Exists(Path.Combine(OutDir, "palette-dark.png")));
            Assert.True(new FileInfo(Path.Combine(OutDir, "palette-light.png")).Length > 10_000);
        }

        /// <summary>v0.6: plan card, change summary, macro button, next-step buttons, skill draft, notes, read-only.</summary>
        private static ChatControl NewFeatures(bool dark)
        {
            var c = new ChatControl { Width = 400, Height = 1000 };
            c.ApplySettings(new PaletteSettings { Standard = "ve-kien-truc", Scale = "1:100" });
            c.SetDark(dark);
            c.SetConnection("Đã kết nối · Opus 5.5 · Claude login", true);
            c.SetMacros(new List<Macro>
            {
                new Macro { Name = "Khung tên A3", Calls = { new MacroCall { Tool = "draw_rectangle" }, new MacroCall { Tool = "add_text" } }, BasePoint = new[] { 0.0, 0.0 } },
            });
            c.MentionSource = () => new List<MentionItem> { new MentionItem { Name = "A-WALL", Kind = "layer" } };
            c.LoadTranscript(new ChatTranscript { DrawingName = "Nha-pho.dwg" }, false);
            c.SetReadOnly(false);
            c.SetNotesIndicator(true);

            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Vẽ mặt bằng tầng 1 nhà phố 5x20m" });
            c.BeginAssistant();
            c.UpdateTool(new ToolLogEntry { Tool = "get_drawing_info", State = "done", Ms = 40 });
            c.AppendAssistant("Việc này nhiều bước, tớ đề xuất kế hoạch trước:");
            c.AddPlanCard("plan-1", "Mặt bằng tầng 1 nhà phố 5×20 m, tỷ lệ 1:100", new List<string>
            {
                "Lưới trục A–D, 1–5 (bước 4 m / 5 m), layer A-AXIS",
                "Tường bao 220, tường ngăn 110 (theo ghi chú dự án)",
                "Cửa đi D1 900, cửa sổ S1 1200; ghi kích thước 2 lớp",
            });
            c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Việc này nhiều bước, tớ đề xuất kế hoạch trước:" }, false);

            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Làm theo kế hoạch." });
            c.BeginAssistant();
            c.UpdateTool(new ToolLogEntry { Tool = "batch_execute", State = "done", Ms = 900, Summary = "120 lệnh OK, 96 đối tượng" });
            c.UpdateTool(new ToolLogEntry { Tool = "capture_view", State = "done", Ms = 310, Summary = "ảnh 1024×410" });
            var a = new ChatEntry
            {
                Role = "assistant",
                Text = "Đã vẽ xong mặt bằng tầng 1; tớ đã nhìn lại bản vẽ và sửa 2 chữ bị đè. Gõ U một lần nếu cậu muốn hoàn tác cả lượt này.",
                Handles = { "2F4A" },
                Changes = ChangeSummary.Build(new[]
                {
                    ("added", "A-WALL", "1"), ("added", "A-WALL", "2"), ("added", "A-DOOR", "3"), ("added", "A-DIM", "4"),
                    ("modified", "A-TEXT", "5"), ("erased", "A-TEMP", "6"),
                }),
            };
            c.EndAssistant(a, true, true);
            c.AddSuggestions(new[] { "Ghi tên phòng và diện tích", "Bố trí thiết bị vệ sinh", "Vẽ mặt cắt A-A" });

            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Tạo skill từ cuộc chat này" });
            c.BeginAssistant();
            c.AppendAssistant("Tớ đã soạn skill **chuan-nha-pho** từ các chuẩn trong cuộc chat.");
            c.AddSkillDraftCard("chuan-nha-pho", "Chuẩn vẽ nhà phố: layer A-*, tường 220/110, chữ 250 cho 1:100.", @"C:\temp\SKILL.md");
            c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Tớ đã soạn skill **chuan-nha-pho** từ các chuẩn trong cuộc chat." }, false);
            return c;
        }

        [Fact]
        public void RendersNewFeatureCards()
        {
            Sta(() =>
            {
                Shot(NewFeatures(true), "palette-v06-dark");
                var light = NewFeatures(false);
                light.SetReadOnly(true);
                light.ShowNotes("Cao độ ±0.000 = +3.200\nTường bao 220, tường ngăn 110", "Bản vẽ tầng 1", @"D:\DuAn\ClaudeCAD-ghichu.md");
                Shot(light, "palette-v06-light");
            });
            Assert.True(new FileInfo(Path.Combine(OutDir, "palette-v06-dark.png")).Length > 10_000);
        }

        [Fact]
        public void ReloadsPersistedTranscript()
        {
            Sta(() =>
            {
                var t = new ChatTranscript { DrawingName = "A.dwg" };
                t.Entries.Add(new ChatEntry { Role = "user", Text = "vẽ lưới trục", ImageCount = 1, SelectionCount = 4 });
                var a = new ChatEntry { Role = "assistant", Text = "Đã vẽ **9 trục**." };
                a.Tools.Add(new ToolLogEntry { Tool = "batch_execute", State = "done", Ms = 200, Summary = "9 đối tượng" });
                t.Entries.Add(a);
                t.Entries.Add(new ChatEntry { Role = "error", Text = "Mất kết nối" });
                var back = ChatTranscript.FromJson(t.ToJson());
                var c = new ChatControl { Width = 400, Height = 500 };
                c.LoadTranscript(back, true);
                c.SetBusy(true);
                c.SetBusy(false);
                Shot(c, "palette-history");
            });
        }

        [Fact]
        public void MarkdownViewHandlesEveryBlockKind()
        {
            Sta(() =>
            {
                var md = new MarkdownView { Width = 360, Height = 400, FontSize = 13 };
                List<string> clicked = null;
                md.HandleLinkClicked += h => clicked = h;
                md.Markdown = "# Tiêu đề\n## Mục\nĐoạn *nghiêng* **đậm** ***cả hai*** `code` [link](handle:AB,CD)\n\n1. một\n2. hai\n   - con\n\n```\ncode block\n```\n\n> trích dẫn\n\n---\n\n| a | b |\n|---|---|\n| 1 | 2 |";
                Assert.True(md.Document.Blocks.Count >= 7);
                Shot(md, "markdown");
            });
        }

        private static string FindTemplates()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "templates", "templates.json"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "templates", "templates.json");
        }
    }
}
