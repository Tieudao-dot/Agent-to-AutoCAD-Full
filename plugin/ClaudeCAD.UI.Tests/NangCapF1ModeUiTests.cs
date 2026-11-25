using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// Nâng cấp đợt F1, việc 2: nút chế độ "Cả hai / 2D / 3D" — chỉ hiện ở máy gói Full, ở hàng trên cùng (vẫn thấy khi thu
    /// gọn), đổi chế độ báo một lần, nhớ lựa chọn trong cài đặt. Ảnh: mode-*.png trong palette-screenshots của CI.
    /// </summary>
    public class NangCapF1ModeUiTests
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

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Priv).GetValue(o);

        private static void Layout(FrameworkElement e)
        {
            e.Measure(new Size(e.Width, e.Height));
            e.Arrange(new Rect(0, 0, e.Width, e.Height));
            e.UpdateLayout();
            Pump();
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
            Layout(e);
            var bmp = new RenderTargetBitmap((int)e.Width * 2, (int)e.Height * 2, 192, 192, PixelFormats.Pbgra32);
            bmp.Render(e);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(Path.Combine(OutDir, name + ".png"))) enc.Save(fs);
        }

        private static ChatControl Palette(bool dark)
        {
            var c = new ChatControl { Width = 400, Height = 520 };
            c.ApplySettings(new PaletteSettings());
            c.SetDark(dark);
            c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
            c.SetPlan("Full");
            return c;
        }

        private static string Label(Button b) =>
            (b.Content as StackPanel)?.Children.OfType<TextBlock>().Select(t => t.Text).FirstOrDefault();

        /// <summary>Visible on screen: itself and every parent up to the palette not collapsed.</summary>
        private static bool Shown(FrameworkElement e, FrameworkElement root)
        {
            DependencyObject o = e;
            while (o != null && !ReferenceEquals(o, root))
            {
                if (o is UIElement u && u.Visibility != Visibility.Visible) return false;
                o = LogicalTreeHelper.GetParent(o) ?? VisualTreeHelper.GetParent(o);
            }
            return true;
        }

        [Fact]
        public void Hidden_until_the_machine_is_full()
        {
            Visibility before = Visibility.Visible, full = Visibility.Collapsed, after = Visibility.Visible;
            Sta(() =>
            {
                var c = Palette(true);
                var b = Field<Button>(c, "_modeBtn");
                before = b.Visibility;     // 2D / 3D machine, not activated: no switch
                c.SetWorkMode(true, "both");
                full = b.Visibility;
                c.SetWorkMode(false, "both"); // the owner moved the machine to the 2D plan
                after = b.Visibility;
            });
            Assert.Equal(Visibility.Collapsed, before);
            Assert.Equal(Visibility.Visible, full);
            Assert.Equal(Visibility.Collapsed, after);
        }

        [Fact]
        public void Picking_a_mode_reports_it_once_and_marks_the_menu()
        {
            var events = new List<string>();
            string label0 = null, label2d = null, label3d = null, tag = null;
            int menuItems = 0;
            bool checked2d = false, checkedBoth = true;
            Sta(() =>
            {
                var c = Palette(false);
                c.WorkModeChanged += m => events.Add(m);
                c.SetWorkMode(true, "both");
                var b = Field<Button>(c, "_modeBtn");
                label0 = Label(b);
                tag = ((b.Content as StackPanel)?.Children[0] as FrameworkElement)?.Tag as string;
                var pick = typeof(ChatControl).GetMethod("PickWorkMode", Priv);
                pick.Invoke(c, new object[] { "2d" });
                pick.Invoke(c, new object[] { "2d" }); // the same mode again: nothing to report
                label2d = Label(b);
                var menu = Field<ContextMenu>(c, "_modeMenu");
                menuItems = menu.Items.Count;
                foreach (MenuItem mi in menu.Items)
                {
                    if ((string)mi.Tag == "2d") checked2d = mi.IsChecked;
                    if ((string)mi.Tag == "both") checkedBoth = mi.IsChecked;
                }
                pick.Invoke(c, new object[] { "3d" });
                label3d = Label(b);
                // the palette's own copy of the settings follows, so a later settings change does not undo the mode
                Assert.Equal("3d", Field<PaletteSettings>(c, "_settings").WorkMode);
            });
            Assert.Equal("Cả hai", label0);
            Assert.Equal("mode", tag);
            Assert.True(Icons.Has(tag));
            Assert.Equal("2D", label2d);
            Assert.Equal("3D", label3d);
            Assert.Equal(new[] { "2d", "3d" }, events);
            Assert.Equal(3, menuItems);
            Assert.True(checked2d);
            Assert.False(checkedBoth);
        }

        [Fact]
        public void Saved_mode_shows_on_start_and_stays_in_compact_mode()
        {
            string label = null;
            bool shownFull = false, shownCompact = false, ctxHiddenCompact = false;
            Sta(() =>
            {
                var c = new ChatControl { Width = 400, Height = 700 };
                c.ApplySettings(new PaletteSettings { WorkMode = "3d" });
                c.SetConnection("Đã kết nối · Nha-pho.dwg", true);
                c.SetPlan("Full");
                c.SetWorkMode(true, "3d");
                var b = Field<Button>(c, "_modeBtn");
                Layout(c);
                label = Label(b);
                shownFull = Shown(b, c);
                c.SetCompact(true);
                Layout(c);
                shownCompact = Shown(b, c);
                ctxHiddenCompact = Field<WrapPanel>(c, "_ctxBar").Visibility == Visibility.Collapsed;
            });
            Assert.Equal("3D", label);
            Assert.True(shownFull);
            Assert.True(ctxHiddenCompact);
            Assert.True(shownCompact, "the mode switch must stay visible in compact mode");
        }

        [Fact]
        public void Screenshots_of_the_switch()
        {
            Sta(() =>
            {
                foreach (var dark in new[] { true, false })
                {
                    var c = Palette(dark);
                    c.SetWorkMode(true, "both");
                    c.AddUserMessage(new ChatEntry { Role = "user", Text = "Vẽ hộp 1000x500x300" });
                    c.AddNote(WorkModeLogic.ChangedNote("2d", false));
                    typeof(ChatControl).GetMethod("PickWorkMode", Priv).Invoke(c, new object[] { "2d" });
                    Shot(c, dark ? "mode-2d-dark" : "mode-2d-light");
                }
                var compact = Palette(true);
                compact.SetWorkMode(true, "3d");
                compact.SetCompact(true);
                Shot(compact, "mode-3d-compact");
                // the three choices as the menu lists them (a popup does not render off-screen)
                var menu = new StackPanel { Width = 220, Height = 90, Background = Brushes.White };
                foreach (var m in WorkModeLogic.Modes)
                    menu.Children.Add(new CheckBox
                    {
                        Content = m == WorkModeLogic.Both ? "Cả hai (2D và 3D)" : m == WorkModeLogic.TwoD ? "2D — chỉ lệnh 2D" : "3D — chỉ lệnh 3D",
                        IsChecked = m == WorkModeLogic.Both,
                        Margin = new Thickness(8, 6, 8, 0),
                    });
                Shot(menu, "mode-menu");
            });
            Assert.True(new FileInfo(Path.Combine(OutDir, "mode-2d-dark.png")).Length > 5_000);
            Assert.True(File.Exists(Path.Combine(OutDir, "mode-3d-compact.png")));
        }
    }
}
