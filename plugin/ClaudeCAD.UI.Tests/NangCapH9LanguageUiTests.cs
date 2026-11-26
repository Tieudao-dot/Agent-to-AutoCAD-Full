using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// Nâng cấp đợt H9: the palette in English. A palette built in English shows English; one already open swaps its
    /// fixed texts at once (Relabel) and back; what the user typed is never touched.
    /// </summary>
    [Collection("H9Lang")] // L.Lang is changed: the other UI tests find buttons by their Vietnamese text
    public class NangCapH9LanguageUiTests
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

        private static ChatControl Open()
        {
            var c = new ChatControl { Width = 380, Height = 640 };
            c.ApplySettings(new PaletteSettings());
            c.Measure(new Size(c.Width, c.Height));
            c.Arrange(new Rect(0, 0, c.Width, c.Height));
            c.UpdateLayout();
            Pump();
            return c;
        }

        [Fact]
        public void An_open_palette_switches_to_english_at_once_and_back_without_touching_what_was_typed() => Sta(() =>
        {
            try
            {
                L.Lang = L.Vi;
                var c = Open();
                var send = Field<Button>(c, "_send");
                var settings = Field<Button>(c, "_settingsBtn");
                var input = Field<TextBox>(c, "_input");
                var lang = Field<ComboBox>(c, "_language");
                Assert.Equal("Gửi", send.Content);
                Assert.StartsWith("Cài đặt", settings.ToolTip as string);
                Assert.Equal(new[] { "Tiếng Việt", "English" }, lang.Items.Cast<object>().Select(x => x.ToString()).ToArray());
                input.Text = "Gửi bản vẽ này";
                var theme = Field<ComboBox>(c, "_theme");
                theme.SelectedIndex = 2; // "Sáng": swapping the item text must keep the choice and not report a change
                Pump();
                int reported = 0;
                c.SettingsChanged += _ => reported++;

                L.Lang = L.En;
                c.Relabel(L.Vi, L.En);
                Pump();
                Assert.Equal(2, theme.SelectedIndex);
                Assert.Equal("Light", theme.SelectedItem as string);
                Assert.Equal(0, reported);
                Assert.Equal("Send", send.Content);
                Assert.StartsWith("Settings", settings.ToolTip as string);
                Assert.Equal("Gửi bản vẽ này", input.Text); // the user's own words stay
                Assert.Equal(new[] { "Tiếng Việt", "English" }, lang.Items.Cast<object>().Select(x => x.ToString()).ToArray());

                L.Lang = L.Vi;
                c.Relabel(L.En, L.Vi);
                Pump();
                Assert.Equal("Gửi", send.Content);
                Assert.StartsWith("Cài đặt", settings.ToolTip as string);
            }
            finally
            {
                L.Lang = L.Vi;
            }
        });

        [Fact]
        public void Picking_english_in_the_list_reaches_the_controller() => Sta(() =>
        {
            // sửa KTP-A: the list had no SelectionChanged, so picking English did nothing
            try
            {
                L.Lang = L.Vi;
                var c = Open();
                PaletteSettings got = null;
                c.SettingsChanged += s => got = s;
                Field<ComboBox>(c, "_language").SelectedIndex = 1;
                Pump();
                Assert.NotNull(got);
                Assert.Equal(L.En, got.Language);
                got = null;
                Field<ComboBox>(c, "_language").SelectedIndex = 0;
                Pump();
                Assert.Equal(L.Vi, got?.Language);
            }
            finally
            {
                L.Lang = L.Vi;
            }
        });

        [Fact]
        public void A_palette_opened_in_english_is_english() => Sta(() =>
        {
            try
            {
                L.Lang = L.En;
                var c = Open();
                Assert.Equal("Send", Field<Button>(c, "_send").Content);
                Assert.StartsWith("New chat", Field<Button>(c, "_new").ToolTip as string);
                c.AddNote(L.Tr("Đã lưu ghi chú — Claude nhận kèm từ tin nhắn tiếp theo."));
                Pump();
            }
            finally
            {
                L.Lang = L.Vi;
            }
        });
    }

    [CollectionDefinition("H9Lang", DisableParallelization = true)]
    public class H9LangCollection { }
}
