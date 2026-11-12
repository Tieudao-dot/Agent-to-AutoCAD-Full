using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Windows;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// ClaudeCAD's icon in AutoCAD's status bar tray (bottom right, next to the Xref / plot icons):
    /// where an AutoCAD user already looks to see whether something is running. It shows Claude's
    /// state, opens the palette on click, and reports a finished turn in a bubble when the palette is
    /// closed or rolled up — the way AutoCAD itself reports a changed Xref.
    /// </summary>
    public static partial class StatusTray
    {
        public enum State
        {
            /// <summary>Assistant not connected (or the palette was never opened): dimmed glyph.</summary>
            Offline,
            /// <summary>Connected and idle.</summary>
            Ready,
            /// <summary>A turn is running: blue dot.</summary>
            Busy,
            /// <summary>Claude waits for the user (confirmation, sign-in, key) or a turn failed: yellow dot.</summary>
            Attention,
        }

        /// <summary>The icon (or the link in its bubble) was clicked: open the palette.</summary>
        public static event Action Clicked;

        private static TrayItem _item;
        private static State _state = State.Offline;
        private static string _detail = "";
        private static bool _dark = true;
        private static readonly Dictionary<string, Icon> Icons = new Dictionary<string, Icon>(StringComparer.Ordinal);
        private static readonly List<IntPtr> Handles = new List<IntPtr>();

        private static bool _gaveUp;

        /// <summary>
        /// Adds the icon to the status bar. Safe to call again. Returns false only while AutoCAD has no
        /// status bar yet (call again later); true once the icon is there or cannot be added at all.
        /// </summary>
        public static bool Install()
        {
            if (_item != null || _gaveUp) return true;
            try
            {
                var bar = AcApp.StatusBar;
                if (bar == null) return false;
                _dark = Theme.AutoCadIsDark();
                var item = new TrayItem { Visible = true, Enabled = true };
                item.MouseDown += OnMouseDown;
                bar.TrayItems.Add(item);
                _item = item;
                Apply();
                bar.Update();
                return true;
            }
            catch (Exception ex)
            {
                // An AutoCAD build without a usable tray: say so once and stop trying.
                Logger.Warn("status bar icon not added: " + ex.Message);
                _item = null;
                _gaveUp = true;
                return true;
            }
        }

        /// <summary>Removes the icon and frees the icons drawn for it (plugin shutdown).</summary>
        public static void Remove()
        {
            try
            {
                if (_item != null)
                {
                    _item.MouseDown -= OnMouseDown;
                    try { _item.CloseBubbleWindows(); } catch { }
                    var bar = AcApp.StatusBar;
                    if (bar != null && bar.TrayItems.Contains(_item)) { bar.TrayItems.Remove(_item); bar.Update(); }
                }
            }
            catch
            {
                // AutoCAD is closing: the status bar may already be gone.
            }
            _item = null;
            foreach (var icon in Icons.Values) { try { icon.Dispose(); } catch { } }
            Icons.Clear();
            foreach (var h in Handles) { try { DestroyIcon(h); } catch { } }
            Handles.Clear();
        }

        /// <summary>Sets what the icon shows. <paramref name="detail"/> is appended to the tooltip.</summary>
        public static void Set(State state, string detail = null)
        {
            _state = state;
            _detail = detail ?? "";
            OnMain(Apply);
        }

        /// <summary>
        /// The status bar belongs to AutoCAD's main thread; connection events arrive on pipe threads.
        /// </summary>
        private static void OnMain(Action action)
        {
            if (MainThread.IsMainThread) action();
            else MainThread.Post(action);
        }

        public static State Current => _state;

        /// <summary>The AutoCAD color theme changed: the glyph must stay readable on the status bar.</summary>
        public static void OnThemeChanged()
        {
            bool dark = Theme.AutoCadIsDark();
            if (dark == _dark) return;
            _dark = dark;
            OnMain(Apply);
        }

        /// <summary>
        /// A balloon over the icon. Call only when the palette is not in view (closed or rolled up):
        /// with the palette open the news is already on screen.
        /// </summary>
        public static void Bubble(string title, string text)
        {
            if (_item == null || string.IsNullOrWhiteSpace(text)) return;
            OnMain(() => ShowBubble(title, text));
        }

        private static void ShowBubble(string title, string text)
        {
            if (_item == null) return;
            try
            {
                _item.CloseBubbleWindows();
                var bubble = new TrayItemBubbleWindow
                {
                    Title = string.IsNullOrEmpty(title) ? ProductInfo.DisplayName : title,
                    Text = text,
                    HyperText = "Mở khung chat",
                    HyperLink = "",
                };
                bubble.Closed += OnBubbleClosed;
                _item.ShowBubbleWindow(bubble);
            }
            catch (Exception ex)
            {
                Logger.Warn("status bar bubble failed: " + ex.Message);
            }
        }

        private static void OnBubbleClosed(object sender, TrayItemBubbleWindowClosedEventArgs e)
        {
            try
            {
                if (sender is TrayItemBubbleWindow b) b.Closed -= OnBubbleClosed;
                if (e.CloseReason == TrayItemBubbleWindowCloseReason.HyperlinkClicked) RaiseClicked();
            }
            catch (Exception ex)
            {
                Logger.Warn("status bar bubble close failed: " + ex.Message);
            }
        }

        private static void OnMouseDown(object sender, StatusBarMouseDownEventArgs e)
        {
            RaiseClicked();
        }

        private static void RaiseClicked()
        {
            try { Clicked?.Invoke(); }
            catch (Exception ex) { Logger.Error("status bar click failed", ex); }
        }

        // ============================================================== tooltip + icon

        /// <summary>Tooltip for a state, in the palette's "Tên — giải thích ngắn" form.</summary>
        public static string Tip(State state, string detail)
        {
            string what;
            switch (state)
            {
                case State.Busy: what = "Claude đang làm"; break;
                case State.Attention: what = "cần cậu xem"; break;
                case State.Ready: what = "sẵn sàng"; break;
                default: what = "chưa kết nối"; break;
            }
            return ProductInfo.DisplayName + " — " + what + (string.IsNullOrWhiteSpace(detail) ? "" : ": " + detail.Trim()) + ". Bấm để mở khung chat";
        }

        private static void Apply()
        {
            if (_item == null) return;
            try
            {
                _item.ToolTipText = Tip(_state, _detail);
                _item.Icon = IconFor(_state, _dark);
                AcApp.StatusBar?.Update();
            }
            catch (Exception ex)
            {
                Logger.Warn("status bar icon not updated: " + ex.Message);
            }
        }

        /// <summary>The plain glyph for a dark or light background (the palette set's own themed icon).</summary>
        public static Icon GlyphIcon(bool dark) => IconFor(State.Ready, dark);

        private static Icon IconFor(State state, bool dark)
        {
            string key = state + (dark ? "/dark" : "/light");
            if (Icons.TryGetValue(key, out var cached)) return cached;
            var icon = Draw(state, dark);
            Icons[key] = icon;
            return icon;
        }

        /// <summary>16 px monochrome "C" (like the other tray glyphs) with a state dot in the corner.</summary>
        private static Icon Draw(State state, bool dark)
        {
            using (var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    var glyph = dark ? Color.FromArgb(0xDF, 0xE2, 0xE5) : Color.FromArgb(0x33, 0x38, 0x40);
                    if (state == State.Offline) glyph = Color.FromArgb(0x70, 0x76, 0x7E);
                    using (var pen = new Pen(glyph, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        // an open ring: the letter C
                        g.DrawArc(pen, 3f, 3f, 10f, 10f, 45f, 270f);
                    }
                    Color? dot = state == State.Busy ? Color.FromArgb(0x4C, 0x9A, 0xFF)
                        : state == State.Attention ? Color.FromArgb(0xE8, 0xC1, 0x5A)
                        : (Color?)null;
                    if (dot.HasValue)
                    {
                        using (var ring = new SolidBrush(dark ? Color.FromArgb(0x34, 0x39, 0x41) : Color.FromArgb(0xF0, 0xF0, 0xF0)))
                            g.FillEllipse(ring, 9f, 0f, 7f, 7f);
                        using (var fill = new SolidBrush(dot.Value))
                            g.FillEllipse(fill, 10f, 1f, 5f, 5f);
                    }
                }
                IntPtr h = bmp.GetHicon();
                Handles.Add(h);
                return Icon.FromHandle(h);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
