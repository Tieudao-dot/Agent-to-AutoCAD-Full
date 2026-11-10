using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
            }, new[] { "ve-kien-truc", "ve-ket-cau" });
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
                light.SetVersions(new List<VersionInfo>
                {
                    new VersionInfo { Id = "v-2", Time = new DateTime(2026, 11, 6, 14, 35, 0), Label = "Thêm cửa sổ S1 cho các phòng", Bytes = 4_200_000 },
                    new VersionInfo { Id = "v-1", Time = new DateTime(2026, 11, 6, 14, 2, 0), Label = "Vẽ mặt bằng tầng 1 nhà phố 5x20m", Bytes = 3_100_000 },
                });
                light.ShowVersionBanner("Đang xem phiên bản lúc 14:02 06/11 của Nha-pho.dwg — trước lượt \"Vẽ mặt bằng tầng 1\". Chỉ xem: sửa ở đây không ảnh hưởng bản vẽ thật.");
                light.AddNote("✎ Bản vẽ đã được sửa ngoài khung chat từ lần chat trước: sửa 12, xóa 3, thêm 40 — Claude sẽ đọc lại các đối tượng này trước khi làm.");
                light.ShowNotes("Cao độ ±0.000 = +3.200\nTường bao 220, tường ngăn 110", "Bản vẽ tầng 1", @"D:\DuAn\ClaudeCAD-ghichu.md");
                Shot(light, "palette-v06-light");
            });
            Assert.True(new FileInfo(Path.Combine(OutDir, "palette-v06-dark.png")).Length > 10_000);
        }

        [Fact]
        public void RendersLayerScope()
        {
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 700 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(true);
                c.LoadTranscript(new ChatTranscript { DrawingName = "Nha-pho.dwg" }, false);
                c.LayerSource = () => new List<LayerRow>
                {
                    new LayerRow { Name = "0", Rgb = 0xFFFFFF, Count = 3 },
                    new LayerRow { Name = "A-WALL", Rgb = 0xFFFFFF, Count = 120, Current = true },
                    new LayerRow { Name = "A-DOOR", Rgb = 0xFFFF00, Count = 8 },
                    new LayerRow { Name = "A-TEXT", Rgb = 0x00FF00, Count = 45, Locked = true },
                    new LayerRow { Name = "A-TEMP", Rgb = 0xFF0000, Count = 0, On = false },
                };
                c.AddUserMessage(new ChatEntry { Role = "user", Text = "Đổi màu các layer này sang đỏ", Layers = { "A-WALL", "A-DOOR" } });
                c.SetSelectionChip(new PickedSelection
                {
                    Handles = { "1", "2", "3", "4" },
                    HandleLayers = { "A-WALL", "A-WALL", "A-DOOR", "A-TEXT" },
                    Summary = "3 LINE, 1 TEXT",
                }, true);
                c.SetLayerScopeForTest(new Dictionary<string, int> { ["A-WALL"] = 120, ["A-DOOR"] = 8 });
                c.ShowLayerPicker();
                Shot(c, "palette-layers");
            });
            Assert.True(new FileInfo(Path.Combine(OutDir, "palette-layers.png")).Length > 5_000);
        }

        /// <summary>Screen C: "Bắt đầu" card with three numbered steps; the step to do now carries the buttons.</summary>
        [Fact]
        public void WelcomeCardWalksThroughThreeSteps()
        {
            string[] marksBefore = null, marksAfter = null;
            int login = 0, loginButtons = 0, fillButtonsBefore = 0;
            string filled = null, missingLine = null;
            Sta(() =>
            {
                var checks = new List<(string, bool?, string)>
                {
                    ("Node.js", true, "đi kèm plugin"),
                    ("Trợ lý (sidecar)", false, "không thấy thư mục agent"),
                    ("Kích hoạt", true, "mã máy CC-3B28-85F3-67B7"),
                    ("Tài khoản Claude", false, "chưa đăng nhập Claude"),
                    ("Skill", true, "3 skill trong plugin"),
                };
                var c = new ChatControl { Width = 380, Height = 520 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(true);
                c.LoadTranscript(new ChatTranscript(), false);
                c.LoginRequested += () => login++;
                c.AddWelcomeCard(checks, true);
                Pump();
                Shot(c, "bat-dau-1-chua-dang-nhap");
                marksBefore = FindAll<TextBlock>(c).Where(t => (t.Tag as string ?? "").StartsWith("step-")).Select(t => (string)t.Tag).ToArray();
                var card = FindAll<Border>(c).First(b => (b.Tag as string) == "welcome");
                var loginBtn = FindAll<Button>(card).Where(b => (b.Content as string) == "Đăng nhập Claude").ToList();
                loginButtons = loginBtn.Count;
                loginBtn.First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                fillButtonsBefore = FindAll<Button>(card).Count(b => (b.Content as string) == "Điền vào ô nhập");
                missingLine = FindAll<TextBlock>(card).Select(t => t.Text).FirstOrDefault(t => t != null && t.StartsWith("Trợ lý (sidecar) — "));

                var d = new ChatControl { Width = 380, Height = 420 };
                d.ApplySettings(new PaletteSettings());
                d.SetDark(false);
                d.LoadTranscript(new ChatTranscript(), false);
                d.AddWelcomeCard(new List<(string, bool?, string)>
                {
                    ("Kích hoạt", true, "mã máy CC-3B28-85F3-67B7"),
                    ("Tài khoản Claude", true, "Claude Pro · cau@example.com"),
                }, false);
                Pump();
                marksAfter = FindAll<TextBlock>(d).Where(t => (t.Tag as string ?? "").StartsWith("step-")).Select(t => (string)t.Tag).ToArray();
                FindAll<Button>(d).First(b => (b.Content as string) == "Điền vào ô nhập").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump();
                filled = ((TextBox)typeof(ChatControl).GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(d)).Text;
                Shot(d, "bat-dau-2-da-dang-nhap");
            });
            Assert.Equal(new[] { "step-done", "step-now", "step-later" }, marksBefore);
            Assert.Equal(1, loginButtons);
            Assert.Equal(1, login);
            Assert.Equal(0, fillButtonsBefore); // step 3 has no "try it" until signed in
            Assert.Equal("Trợ lý (sidecar) — không thấy thư mục agent", missingLine);
            Assert.Equal(new[] { "step-done", "step-done", "step-now" }, marksAfter);
            Assert.Equal(ChatControl.WelcomeExample, filled);
        }

        /// <summary>Sign-in flow screens: first start card, settings ▸ Tài khoản AI, sign-in in progress, signed in, API key editor.</summary>
        [Fact]
        public void RendersLoginFlow()
        {
            Sta(() =>
            {
                // 1. first start: "Bắt đầu" card with "Đăng nhập Claude"
                var c1 = new ChatControl { Width = 400, Height = 520 };
                c1.ApplySettings(new PaletteSettings());
                c1.SetDark(true);
                c1.SetConnection("Đã kết nối · trợ lý sẵn sàng", true);
                c1.LoadTranscript(new ChatTranscript(), false);
                c1.AddWelcomeCard(new List<(string, bool?, string)>
                {
                    ("Node.js", true, "đi kèm plugin"),
                    ("Trợ lý (sidecar)", true, "sẵn sàng"),
                    ("Tài khoản Claude", false, "chưa đăng nhập Claude"),
                    ("Skill", true, "3 skill trong plugin"),
                }, true);
                Shot(c1, "login-1-bat-dau");

                // 2. ⚙ settings ▸ Tài khoản AI (the popup content rendered on its own)
                var c2 = new ChatControl { Width = 400, Height = 300 };
                c2.ApplySettings(new PaletteSettings());
                c2.SetDark(true);
                c2.SetAccountStatus("Đang dùng: chưa đăng nhập Claude");
                var popup = (System.Windows.Controls.Primitives.Popup)typeof(ChatControl)
                    .GetField("_settingsPopup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c2);
                var panel = (FrameworkElement)popup.Child;
                popup.Child = null;
                var host = new Border { Width = 320, Height = 640, Child = panel, Background = Brushes.Transparent };
                host.SetValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
                Theme.Apply(host.Resources, true);
                Shot(host, "login-2-cai-dat-tai-khoan");

                // 3. after clicking: note while the browser sign-in runs, then 4. signed in
                var c3 = new ChatControl { Width = 400, Height = 420 };
                c3.ApplySettings(new PaletteSettings());
                c3.SetDark(true);
                c3.SetConnection("Đã kết nối · trợ lý sẵn sàng", true);
                c3.LoadTranscript(new ChatTranscript(), false);
                c3.AddNote("Đang mở cửa sổ đăng nhập Claude: trình duyệt sẽ mở trang đăng nhập — chọn tài khoản Pro/Max, đồng ý, rồi quay lại AutoCAD. Cửa sổ đen tự đóng khi xong.");
                Shot(c3, "login-3-dang-dang-nhap");
                c3.AddNote("✓ Đã đăng nhập Claude. Trợ lý khởi động lại với tài khoản này — cậu gõ yêu cầu được rồi.");
                c3.SetConnection("Đã kết nối · claude-opus-5-5 · Claude login", true);
                Shot(c3, "login-4-da-dang-nhap");

                // 5. "Dùng API key" editor
                var c5 = new ChatControl { Width = 400, Height = 420 };
                c5.ApplySettings(new PaletteSettings());
                c5.SetDark(true);
                c5.LoadTranscript(new ChatTranscript(), false);
                c5.ShowApiKeyPanel(null);
                Shot(c5, "login-5-api-key");
            });
            Assert.True(File.Exists(Path.Combine(OutDir, "login-2-cai-dat-tai-khoan.png")));
        }

        [Fact]
        public void RendersSetupCards()
        {
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 600 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(false);
                c.LoadTranscript(new ChatTranscript(), false);
                c.AddWelcomeCard(new List<(string, bool?, string)>
                {
                    ("Node.js", true, "đi kèm plugin"),
                    ("Trợ lý (sidecar)", true, "sẵn sàng"),
                    ("Tài khoản Claude", false, "chưa đăng nhập Claude"),
                    ("Skill", true, "3 skill trong plugin"),
                }, true);
                c.AddLoginCard("Chưa đăng nhập Claude. Bấm nút \"Đăng nhập Claude\" bên dưới và đăng nhập tài khoản Pro/Max trên trình duyệt.");
                c.AddLimitCard("Đã chạm giới hạn sử dụng Claude", "Gói Claude đã dùng hết hạn mức 5 giờ. Được dùng lại lúc 14:30 06/11.", DateTime.Now.AddMinutes(95), true, false);
                c.ShowToast("Sắp hết hạn mức 5 giờ: đã dùng 85%, làm mới lúc 14:30.", ChatControl.ToastKind.Warning, 30);
                c.ShowToast("Đã lưu macro \"Khung tên A3\".", ChatControl.ToastKind.Success, 30);
                Shot(c, "palette-setup");
            });
            Assert.True(new FileInfo(Path.Combine(OutDir, "palette-setup.png")).Length > 5_000);
        }

        /// <summary>Every element of type T under root (logical tree: works before the control was laid out).</summary>
        private static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
        {
            var list = new List<T>();
            void Walk(object o)
            {
                if (o is T t) list.Add(t);
                if (o is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child);
            }
            Walk(root);
            return list;
        }

        [Fact]
        public void RendersActivationCard()
        {
            string submitted = null;
            int asked = 0;
            Sta(() =>
            {
                // 1: no licence on this machine — machine code, contact, key box; the request was emailed to the owner
                var c = new ChatControl { Width = 400, Height = 520 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(false);
                c.LoadTranscript(new ChatTranscript(), false);
                c.ActivationKeySubmitted += k => submitted = k;
                c.ActivationRequestRequested += () => asked++;
                c.AddActivationCard("CC-3B28-85F3-67B7", "LAPTOP-TUAN", "Liên hệ Zalo owner: 0344644415", "Đang gửi yêu cầu kích hoạt cho chủ plugin...");
                c.SetActivationStatus("Đã gửi yêu cầu kích hoạt cho chủ plugin. Liên hệ Zalo owner: 0344644415 để nhận key.", false);
                Shot(c, "activation-1-request");

                // 2: wrong key
                c.SetActivationStatus("Key không đúng với máy này (mã máy CC-3B28-85F3-67B7). Kiểm tra lại key, hoặc xin key mới.", true);
                Shot(c, "activation-2-wrong-key");

                // 3: activated
                c.SetActivationStatus("Đã kích hoạt ClaudeCAD cho máy này (vĩnh viễn). Cập nhật bản mới không cần nhập lại key.", false, true);
                c.ShowToast("Đã kích hoạt ClaudeCAD", ChatControl.ToastKind.Success, 30);
                Shot(c, "activation-3-done");

                // the buttons raise the events
                var buttons = FindAll<Button>(c);
                var ask = buttons.First(b => (b.Content as string) == "Gửi yêu cầu kích hoạt");
                c.SetActivationStatus("", false); // unlock again
                ask.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                var box = FindAll<TextBox>(c).Last(t => !t.IsReadOnly);
                box.Text = "7K2M-9QXR-4TBN-H8WZ";
                buttons.First(b => (b.Content as string) == "Kích hoạt").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            });
            Assert.Equal(1, asked);
            Assert.Equal("7K2M-9QXR-4TBN-H8WZ", submitted);
            Assert.True(new FileInfo(Path.Combine(OutDir, "activation-1-request.png")).Length > 5_000);
        }

        [Fact]
        public void LongTranscriptRendersTheLastMessagesAndOneActivationCard()
        {
            int buttons = 0, cards = 0, moreAfterClick = 0, before = 0, after = 0;
            bool cardKept = false;
            Sta(() =>
            {
                var t = new ChatTranscript { DrawingName = "A.dwg" };
                for (int i = 0; i < 150; i++) t.Entries.Add(new ChatEntry { Role = i % 2 == 0 ? "user" : "assistant", Text = "tin " + i });
                var c = new ChatControl { Width = 400, Height = 500 };
                c.LoadTranscript(t, false);
                var messages = (StackPanel)typeof(ChatControl).GetField("_messages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c);
                // a live card below the history (here the activation card) must survive "Xem thêm"
                c.AddActivationCard("CC-3B28-85F3-67B7", "PC", "Liên hệ Zalo owner: 0344644415", "");
                var card = messages.Children[messages.Children.Count - 1];
                before = messages.Children.Count;
                var more = FindAll<Button>(c).Where(b => (b.Content as string ?? "").StartsWith("Xem thêm")).ToList();
                buttons = more.Count;
                more[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                moreAfterClick = FindAll<Button>(c).Count(b => (b.Content as string ?? "").StartsWith("Xem thêm"));
                after = messages.Children.Count;
                cardKept = messages.Children.Contains(card) && messages.Children[messages.Children.Count - 1] == card;

                // pressing Enter again while not activated: the live card is reused, not added again
                c.AddActivationCard("CC-3B28-85F3-67B7", "PC", "Liên hệ Zalo owner: 0344644415", "");
                cards = FindAll<Button>(c).Count(b => (b.Content as string) == "Kích hoạt");
            });
            Assert.Equal(1, buttons);
            Assert.Equal(1, moreAfterClick); // 150 → 60 shown → 120 shown, still 30 older
            Assert.Equal(before + 60, after); // 60 older messages inserted (button replaced by the next one)
            Assert.True(cardKept, "the card below the history was rebuilt or lost");
            Assert.Equal(1, cards);
        }

        [Fact]
        public void RendersSkillsPerChat()
        {
            var toggles = new List<(string, bool)>();
            int inUse = 0, library = 0;
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 300 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(false);
                c.SkillToggled += (n, on) => toggles.Add((n, on));
                c.SetSkills(new List<SkillInfo>
                {
                    new SkillInfo { Name = "ve-kien-truc", Title = "Vẽ kiến trúc theo chuẩn", Description = "Chuẩn vẽ kiến trúc: layer A-*, tường 220/110, cửa, kích thước.", Builtin = true },
                    new SkillInfo { Name = "ve-mep", Title = "Vẽ điện nước", Description = "Chuẩn MEP: ống, thiết bị, ký hiệu.", Builtin = true },
                    new SkillInfo { Name = "khung-ten-a3", Title = "Khung tên công ty A3", Description = "Khung tên, logo, ô ký của công ty.", Added = new DateTime(2026, 11, 10), Source = @"D:\chuan\khung-ten.md" },
                    new SkillInfo { Name = "ve-ket-cau", Title = "Vẽ kết cấu", Description = "Chuẩn kết cấu: dầm, cột, móng.", Added = new DateTime(2026, 11, 8) },
                }, new[] { "khung-ten-a3", "ve-kien-truc" });
                var popup = (System.Windows.Controls.Primitives.Popup)typeof(ChatControl)
                    .GetField("_skillsPopup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c);
                var panel = (FrameworkElement)popup.Child;
                popup.Child = null;
                var host = new Border { Width = 362, Height = 520, Child = panel, Background = Brushes.Transparent };
                host.SetValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
                Theme.Apply(host.Resources, false);
                Shot(host, "skills-1-theo-doan-chat");
                var buttons = FindAll<Button>(panel);
                inUse = buttons.Count(b => (b.Content as string) == "−");
                library = buttons.Count(b => (b.Content as string) == "+");
                buttons.First(b => (b.Content as string) == "−").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                buttons.First(b => (b.Content as string) == "+").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.DoesNotContain(buttons, b => (b.Content as string) == "Xóa" || (b.Content as string) == "Mở thư mục");
            });
            Assert.Equal(2, inUse);
            Assert.Equal(2, library);
            Assert.Equal(("ve-kien-truc", false), toggles[0]); // in-use rows come in library order
            Assert.False(toggles[1].Item2 == false);
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

        /// <summary>
        /// The AutoCAD-style shell: usage meter, queued message row, busy line, confirmation card with the
        /// count as a figure, and the settings popup as collapsible two-column groups.
        /// </summary>
        [Fact]
        public void RendersAutoCadStyleShell()
        {
            bool meterShown = false, meterHidden = false, queuedShown = false, queuedHidden = false, busyShown = false, busyHidden = false;
            bool stylesLoaded = false, flatButton = false;
            int sendWhileBusy = 0, queueCancel = 0, stopOnEsc = 0, confirmYes = 0, preview = 0, categories = 0, inputIcons = 0;
            string sendLabelBusy = null, sendLabelIdle = null;
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 760 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(true);
                c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
                c.LoadTranscript(new ChatTranscript { DrawingName = "Nha-pho.dwg" }, false);
                // the flat control styles parsed, and a button really got the flat template (no Windows chrome)
                stylesLoaded = Theme.StylesLoaded;
                c.Measure(new Size(400, 760));
                c.Arrange(new Rect(0, 0, 400, 760));
                c.UpdateLayout();
                var anyButton = FindAll<Button>(c).First();
                anyButton.ApplyTemplate();
                flatButton = anyButton.Template != null && anyButton.Template.FindName("bd", anyButton) is Border;
                c.SendRequested += m => sendWhileBusy++;
                c.QueueCancelRequested += () => queueCancel++;
                c.StopRequested += () => stopOnEsc++;
                c.ConfirmAnswered += (id, yes) => { if (yes) confirmYes++; };
                c.ZoomRequested += h => preview += h.Count;

                T Field<T>(string name) => (T)typeof(ChatControl).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c);
                var meter = Field<DockPanel>("_usageMeter");
                var queued = Field<DockPanel>("_queuedRow");
                var busy = Field<TextBlock>("_busyLine");
                var send = Field<Button>("_send");
                var input = Field<TextBox>("_input");

                // usage meter: hidden until there is something to watch
                c.SetUsageMeter("Hạn mức 5 giờ", -1, "");
                Pump();
                meterHidden = meter.Visibility != Visibility.Visible;
                c.SetUsageMeter("Hạn mức 5 giờ", 0.41, "14:30");
                Pump();
                meterShown = meter.Visibility == Visibility.Visible;

                c.AddUserMessage(new ChatEntry { Role = "user", Text = "Xóa hết các đường nét đứt cũ trên layer A-TEMP rồi vẽ lại khung tên" });
                c.BeginAssistant();
                c.AppendAssistant("Tớ tìm thấy các đối tượng cần xóa trên layer A-TEMP. Cần cậu đồng ý trước khi xóa.");
                c.AddConfirmCard("c1", "Xóa 57 đối tượng trên layer A-TEMP (LINE 41 · POLYLINE 12 · TEXT 4). Các layer khác giữ nguyên.", 57, new List<string> { "1A", "1B", "1C" });
                Pump();

                // while Claude works: busy line on, the send button queues instead of being disabled
                c.SetBusy(true);
                Pump();
                busyShown = busy.Visibility == Visibility.Visible && busy.Text.Contains("Đang làm");
                sendLabelBusy = send.Content as string;
                input.Text = "À, tường ngăn phòng ngủ dày 110 thôi";
                send.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                c.SetQueued("À, tường ngăn phòng ngủ dày 110 thôi");
                Pump();
                queuedShown = queued.Visibility == Visibility.Visible;
                Shot(c, "shell-dang-lam");

                // "Bỏ" on the queued row raises the cancel; hiding it is the controller's answer
                FindAll<Button>(queued).First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                c.SetQueued(null);
                Pump();
                queuedHidden = queued.Visibility != Visibility.Visible;

                c.SetBusy(false);
                Pump();
                busyHidden = busy.Visibility != Visibility.Visible;
                sendLabelIdle = send.Content as string;

                // confirmation card: preview + the single accent "Đồng ý"
                FindAll<Button>(c).First(b => (b.Content as string ?? "").StartsWith("⌖ Xem trên bản vẽ")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                FindAll<Button>(c).First(b => (b.Content as string) == "Đồng ý").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Shot(c, "shell-xac-nhan");

                // the row under the input is icons too: glyph content, "Tên — …" tooltip
                var inputTips = new[] { "Chọn đối tượng — ", "Lấy điểm — ", "Ảnh — ", "Chụp — " };
                inputIcons = FindAll<Button>(c).Count(b => b.Content is TextBlock && inputTips.Any(t => (b.ToolTip as string ?? "").StartsWith(t)));

                // error + sign-in cards: glyph + plain text, no red card
                c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Tớ tìm thấy các đối tượng cần xóa trên layer A-TEMP." }, false, false);
                c.AddErrorCard("AutoCAD đang bận, Claude chưa vẽ được. Có thể một lệnh hoặc hộp thoại đang mở — bấm Esc trong AutoCAD rồi thử lại.", true);
                c.AddLoginCard("Phiên đăng nhập Claude đã hết hạn. Yêu cầu vừa rồi chưa được thực hiện.");
                Shot(c, "shell-loi");

                // settings popup: collapsible groups (Chung, Cách Claude làm việc, An toàn, Tài khoản AI, Trợ giúp)
                var popup = Field<System.Windows.Controls.Primitives.Popup>("_settingsPopup");
                var panel = (FrameworkElement)popup.Child;
                popup.Child = null;
                var host = new Border { Width = 320, Height = 560, Child = panel, Background = Brushes.Transparent };
                host.SetValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
                Theme.Apply(host.Resources, true);
                Shot(host, "shell-cai-dat");
                categories = FindAll<Button>(host).Count(b => (b.ToolTip as string) == "Bấm để mở / thu gọn");
            });
            Assert.True(stylesLoaded, "the palette's control styles did not parse");
            Assert.True(flatButton, "buttons still use the default Windows template");
            Assert.Equal(4, inputIcons); // Chọn đối tượng, Lấy điểm, Ảnh, Chụp
            Assert.True(meterHidden, "usage meter must stay hidden with nothing to show");
            Assert.True(meterShown, "usage meter did not appear");
            Assert.True(busyShown, "busy line did not appear while a turn runs");
            Assert.True(busyHidden, "busy line stayed after the turn");
            Assert.Equal("Gửi thêm", sendLabelBusy);
            Assert.Equal("Gửi", sendLabelIdle);
            Assert.Equal(1, sendWhileBusy); // the message typed while busy reaches the controller (which queues it)
            Assert.True(queuedShown, "queued row did not appear");
            Assert.Equal(1, queueCancel);
            Assert.True(queuedHidden, "queued row stayed after the cancel");
            Assert.Equal(3, preview);
            Assert.Equal(1, confirmYes);
            Assert.Equal(5, categories);
            Assert.Equal(0, stopOnEsc);
            Assert.True(new FileInfo(Path.Combine(OutDir, "shell-xac-nhan.png")).Length > 5_000);
            Assert.True(new FileInfo(Path.Combine(OutDir, "shell-cai-dat.png")).Length > 5_000);
        }

        /// <summary>
        /// Compact mode (⌄): only the toolbar row, a status line and the input. The conversation is hidden,
        /// not unloaded; a finished turn shows as one line; a card that needs a decision opens the palette.
        /// </summary>
        [Fact]
        public void CompactModeKeepsTheConversationAndOpensForDecisions()
        {
            var events = new List<bool>();
            bool hiddenWhenCompact = false, noteShown = false, noteCleared = false, backAfterExpand = false, openedByConfirm = false, openedByLine = false;
            int messagesBefore = 0, messagesAfter = 0;
            string note = null, glyphCompact = null, glyphFull = null;
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 640 };
                c.ApplySettings(new PaletteSettings());
                c.SetDark(true);
                c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
                var t = new ChatTranscript { DrawingName = "Nha-pho.dwg" };
                t.Entries.Add(new ChatEntry { Role = "user", Text = "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900" });
                t.Entries.Add(new ChatEntry { Role = "assistant", Text = "Tớ đã vẽ xong phòng 4×6 m, tường 220 và cửa đi 900." });
                c.LoadTranscript(t, false);
                c.CompactChanged += on => events.Add(on);

                T Field<T>(string name) => (T)typeof(ChatControl).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c);
                var middle = Field<Grid>("_middle");
                var messages = Field<StackPanel>("_messages");
                var line = Field<Button>("_compactLine");
                var toggle = Field<Button>("_compactBtn");
                messagesBefore = messages.Children.Count;

                // ⌄ on the toolbar
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump();
                hiddenWhenCompact = c.IsCompact && middle.Visibility != Visibility.Visible;
                glyphCompact = ((TextBlock)toggle.Content).Text;

                // a turn runs and ends while compact: one line of news, the palette stays compact
                c.AddUserMessage(new ChatEntry { Role = "user", Text = "Thêm kích thước 4 phía" });
                c.BeginAssistant();
                c.SetBusy(true);
                c.AppendAssistant("Đã thêm kích thước 4 phía trên layer A-DIM.\n\nChi tiết: 8 DIMENSION.");
                c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Đã thêm kích thước 4 phía trên layer A-DIM.\n\nChi tiết: 8 DIMENSION." }, true, false);
                c.SetBusy(false);
                Pump();
                noteShown = c.IsCompact && line.Visibility == Visibility.Visible;
                note = ((TextBlock)line.Content).Text;
                Shot(c, "shell-thu-gon");

                // the status line opens the palette: the conversation is all there
                line.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump();
                openedByLine = !c.IsCompact && middle.Visibility == Visibility.Visible;
                glyphFull = ((TextBlock)toggle.Content).Text;
                messagesAfter = messages.Children.Count;
                backAfterExpand = messagesAfter == messagesBefore + 2;
                noteCleared = line.Visibility != Visibility.Visible;

                // compact again, then Claude needs a decision: the palette opens by itself
                c.SetCompact(true);
                Pump();
                c.BeginAssistant();
                c.AddConfirmCard("c1", "Xóa 57 đối tượng trên layer A-TEMP.", 57, new List<string> { "1A" });
                Pump();
                openedByConfirm = !c.IsCompact && middle.Visibility == Visibility.Visible;
            });
            Assert.True(hiddenWhenCompact, "the conversation is still shown in compact mode");
            Assert.Equal("⌃", glyphCompact);
            Assert.Equal("⌄", glyphFull);
            Assert.True(noteShown, "no status line after a turn ended in compact mode");
            Assert.StartsWith("✓ Đã xong: Đã thêm kích thước 4 phía trên layer A-DIM.", note);
            Assert.EndsWith("bấm để xem", note);
            Assert.True(openedByLine, "clicking the status line did not expand the palette");
            Assert.True(backAfterExpand, $"messages lost or duplicated: {messagesBefore} → {messagesAfter}");
            Assert.True(noteCleared, "the status line stayed after expanding");
            Assert.True(openedByConfirm, "a confirmation card did not open the compact palette");
            Assert.Equal(new[] { true, false, true, false }, events);
        }

        [Fact]
        public void ElapsedReadsLikeAPerson()
        {
            var elapsed = typeof(ChatControl).GetMethod("Elapsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.Equal("0 giây", elapsed.Invoke(null, new object[] { TimeSpan.FromSeconds(-3) }));
            Assert.Equal("12 giây", elapsed.Invoke(null, new object[] { TimeSpan.FromSeconds(12.9) }));
            Assert.Equal("1 phút 12 giây", elapsed.Invoke(null, new object[] { TimeSpan.FromSeconds(72) }));
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
