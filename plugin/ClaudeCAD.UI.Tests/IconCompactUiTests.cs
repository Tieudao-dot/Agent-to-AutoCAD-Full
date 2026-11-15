using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// Bộ icon kiểu palette AutoCAD (không emoji) và chế độ thu gọn cân đối: nút Gửi cùng hàng ô nhập, không có dải trống.
    /// </summary>
    public class IconCompactUiTests
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private static void Sta(Action a)
        {
            Exception error = null;
            var t = new Thread(() => { try { a(); } catch (Exception ex) { error = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw new Exception("UI test failed: " + error.Message, error);
        }

        private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Priv).GetValue(o);

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

        private static void Layout(FrameworkElement e)
        {
            e.Measure(new Size(e.Width, e.Height));
            e.Arrange(new Rect(0, 0, e.Width, e.Height));
            e.UpdateLayout();
        }

        /// <summary>Emoji and the pictograph symbols that were used as icons before.</summary>
        private static bool HasEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsSurrogate(s[i])) return true; // every emoji used (🔒 💾 📍 👁 …) is outside the BMP
                if ("⚙⛶✦⏳⌖☆⤓⊞⟲▨⬚✛▷⌄⌃＋".IndexOf(s[i]) >= 0) return true;
            }
            return false;
        }

        private static ChatControl Busy()
        {
            var c = new ChatControl { Width = 400, Height = 700 };
            c.ApplySettings(new PaletteSettings());
            c.SetDark(true);
            c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
            c.AddUserMessage(new ChatEntry { Role = "user", Text = "Vẽ mặt bằng phòng 4x6m" });
            c.BeginAssistant();
            c.UpdateTool(new ToolLogEntry { Tool = "draw_walls", State = "done", Ms = 200, Summary = "4 tường" });
            c.EndAssistant(new ChatEntry { Role = "assistant", Text = "Đã vẽ xong.", Handles = new List<string> { "1A" } }, true, true);
            c.AddConfirmCard("c1", "Xóa 57 đối tượng trên layer A-TEMP.", 57, new List<string> { "1A" });
            c.AddErrorCard("Lỗi thử", true);
            c.ShowToast("Đã lưu", ChatControl.ToastKind.Success);
            c.SetReadOnly(true);
            c.SetQueued("vẽ thêm cửa sổ");
            return c;
        }

        [Fact]
        public void ToolbarUsesTheLineIconSetAndNoEmoji()
        {
            var bad = new List<string>();
            int iconButtons = 0;
            Sta(() =>
            {
                var c = Busy();
                Layout(c);
                foreach (var name in new[] { "_settingsBtn", "_export", "_new", "_stop", "_compactBtn", "_addSkill", "_skillsBtn", "_layersBtn", "_macrosBtn", "_notesBtn", "_versionsBtn", "_pickSel", "_pickPt", "_addImg", "_capture" })
                {
                    var b = Field<Button>(c, name);
                    var tag = (b.Content as FrameworkElement)?.Tag as string;
                    if (tag == null || !Icons.Has(tag)) bad.Add(name + " has no line icon");
                    else iconButtons++;
                }
                foreach (var t in FindAll<TextBlock>(c)) if (HasEmoji(t.Text)) bad.Add("text: " + t.Text);
                foreach (var b in FindAll<ContentControl>(c)) if (HasEmoji(b.Content as string)) bad.Add("button: " + b.Content);
            });
            Assert.True(bad.Count == 0, string.Join("\n", bad));
            Assert.Equal(15, iconButtons);
        }

        [Fact]
        public void CompactPutsSendOnTheInputRowWithoutEmptyStrips()
        {
            Dock actionsCompact = Dock.Bottom, actionsFull = Dock.Top;
            Visibility statusCompact = Visibility.Visible, statusFull = Visibility.Collapsed;
            double compactHeight = 0, fullHeight = 0, inputTop = 0, sendTop = 0, inputBottom = 0, sendBottom = 0;
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 700 };
                c.ApplySettings(new PaletteSettings());
                c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
                Layout(c);
                fullHeight = c.CompactContentHeight();
                c.SetCompact(true);
                Layout(c);
                var actions = Field<DockPanel>(c, "_actionsRow");
                var input = Field<TextBox>(c, "_input");
                var send = Field<Button>(c, "_send");
                actionsCompact = DockPanel.GetDock(actions);
                statusCompact = Field<TextBlock>(c, "_toolStatus").Visibility;
                compactHeight = c.CompactContentHeight();
                var ip = input.TranslatePoint(new Point(0, 0), c);
                var sp = send.TranslatePoint(new Point(0, 0), c);
                inputTop = ip.Y; sendTop = sp.Y;
                inputBottom = ip.Y + input.ActualHeight; sendBottom = sp.Y + send.ActualHeight;
                c.SetCompact(false);
                Layout(c);
                actionsFull = DockPanel.GetDock(actions);
                statusFull = Field<TextBlock>(c, "_toolStatus").Visibility;
            });
            Assert.Equal(Dock.Right, actionsCompact);
            Assert.Equal(Dock.Bottom, actionsFull);
            Assert.Equal(Visibility.Collapsed, statusCompact); // no blank hint line between the toolbar and the input
            Assert.Equal(Visibility.Visible, statusFull);
            Assert.True(Math.Abs(inputTop - sendTop) < 2 && Math.Abs(inputBottom - sendBottom) < 2, $"Gửi not on the input row: input {inputTop}–{inputBottom}, send {sendTop}–{sendBottom}");
            Assert.True(compactHeight > 50 && compactHeight < 130, "compact content height " + compactHeight);
            Assert.True(compactHeight < fullHeight, $"compact {compactHeight} vs full {fullHeight}");
        }
    }
}
