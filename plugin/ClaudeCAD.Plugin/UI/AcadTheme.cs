using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Reads the colors and fonts AutoCAD itself uses for its palettes (Properties, Layer, ...)
    /// so the chat palette matches the host instead of guessing hex codes.
    ///
    /// The values live in Autodesk.Windows.Palettes.PaletteTheme (AdUiPalettes.dll), reached by
    /// reflection on purpose: the plugin must build and load without that assembly (the csproj only
    /// references acmgd/acdbmgd/accoremgd/AdWindows in install mode, and the UI tests run with no
    /// AutoCAD at all). Anything missing is simply left to the hardcoded fallback in <see cref="Theme"/>.
    /// </summary>
    public static partial class AcadTheme
    {
        /// <summary>What a palette theme gave us. Any member may be absent.</summary>
        public sealed class Info
        {
            /// <summary>Theme's own dark flag; null when the theme did not say.</summary>
            public bool? IsDark;
            public FontFamily Font;
            /// <summary>Font size in WPF device-independent pixels; 0 when unknown.</summary>
            public double FontSize;
            public FontWeight? FontWeight;
            public FontStyle? FontStyle;
            /// <summary>Theme key (<see cref="Theme"/> constants) to brush, only for what was read.</summary>
            public readonly Dictionary<string, Brush> Brushes = new Dictionary<string, Brush>(StringComparer.Ordinal);

            public bool IsEmpty => Brushes.Count == 0 && Font == null && FontSize <= 0;

            /// <summary>
            /// Same font, no colors — for when the user forced dark/light against AutoCAD's own theme:
            /// its colors would fight the chosen one, its font still fits.
            /// </summary>
            public Info FontOnly() => new Info
            {
                IsDark = IsDark,
                Font = Font,
                FontSize = FontSize,
                FontWeight = FontWeight,
                FontStyle = FontStyle,
            };
        }

        /// <summary>
        /// Theme key to the PaletteTheme properties to try, in order. First one that reads wins, so a
        /// property renamed or dropped in a future AutoCAD falls through to the next (then to the
        /// hardcoded fallback).
        /// </summary>
        private static readonly (string Key, string[] Props)[] Map =
        {
            (Theme.PanelBg,     new[] { "PaletteContainerBackgroundColor", "PaletteBackgroundColor" }),
            (Theme.HeaderBg,    new[] { "CategoryBackgroundColor", "PropertyInspectorCategoryBackgroundColor" }),
            (Theme.InputBg,     new[] { "TextInputBackgroundColor", "TextInputBackgroundBrush" }),
            (Theme.CodeBg,      new[] { "TextInputBackgroundColor", "TextInputBackgroundBrush" }),
            (Theme.Fg,          new[] { "ControlTextColor", "PaletteTextActiveColor" }),
            (Theme.MutedFg,     new[] { "TextInputInactiveTextColor", "ListBoxInactiveTextColor" }),
            (Theme.Border,      new[] { "CategoryBorderColor", "TextInputBorderColor" }),
            (Theme.AssistantBg, new[] { "PaletteContainerBackgroundColor", "PaletteBackgroundColor" }),
            (Theme.UserBg,      new[] { "SquareButtonBackgroundColor", "CategoryBackgroundColor" }),
            (Theme.CardBg,      new[] { "CategoryBackgroundColor", "SquareButtonBackgroundColor" }),
            (Theme.ChipBg,      new[] { "SquareButtonBackgroundColor", "CategoryBackgroundColor" }),
            (Theme.HoverBg,     new[] { "SquareButtonHoverBackgroundBrush", "ListBoxHoverBackgroundBrush" }),
            (Theme.PressBg,     new[] { "SquareButtonClickBackgroundBrush", "ListBoxSelectBackgroundBrush" }),
            (Theme.Accent,      new[] { "TextInputActiveBorderColor", "ListBoxSelectBorderColor" }),
            (Theme.LinkFg,      new[] { "PaletteHyperlinkTextColor" }),
        };

        /// <summary>Reads the palette theme AutoCAD is using now, or null when it cannot be reached.</summary>
        public static Info Read()
        {
            try
            {
                var theme = CurrentTheme();
                return theme == null ? null : ReadFrom(theme);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads an object that exposes PaletteTheme's property names. Public so the UI tests can feed
        /// a stub theme without AutoCAD.
        /// </summary>
        public static Info ReadFrom(object theme)
        {
            if (theme == null) return null;
            var t = theme.GetType();
            var info = new Info();

            if (Get(t, theme, "IsDark") is bool dark) info.IsDark = dark;

            foreach (var (key, props) in Map)
            {
                foreach (var name in props)
                {
                    var brush = ToBrush(Get(t, theme, name));
                    if (brush == null) continue;
                    info.Brushes[key] = brush;
                    break;
                }
            }

            ReadFont(Get(t, theme, "NormalFont"), info);
            return info.IsEmpty ? null : info;
        }

        /// <summary>FontInfo: FontFamily / FontSize / FontWeight / FontStyle, any of them optional.</summary>
        private static void ReadFont(object font, Info info)
        {
            if (font == null) return;
            var t = font.GetType();

            switch (Get(t, font, "FontFamily"))
            {
                case FontFamily family:
                    info.Font = family;
                    break;
                case string name when !string.IsNullOrWhiteSpace(name):
                    try { info.Font = new FontFamily(name); } catch { }
                    break;
            }

            // AutoCAD reports the size in DIPs like WPF; ignore anything outside a sane palette range
            // so a points-vs-pixels surprise cannot produce unreadable text.
            if (Get(t, font, "FontSize") is var size && size != null)
            {
                try
                {
                    var v = Convert.ToDouble(size);
                    if (v >= 8 && v <= 24) info.FontSize = v;
                }
                catch { }
            }

            if (Get(t, font, "FontWeight") is FontWeight weight) info.FontWeight = weight;
            if (Get(t, font, "FontStyle") is FontStyle style) info.FontStyle = style;
        }

        private static object Get(Type t, object target, string name)
        {
            try
            {
                return t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Accepts a WPF color or brush, or a System.Drawing color, and returns a frozen brush.</summary>
        private static Brush ToBrush(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case Brush brush:
                    if (brush.IsFrozen) return brush;
                    var copy = brush.Clone();
                    copy.Freeze();
                    return copy;
                case Color color:
                    return Frozen(color);
                case System.Drawing.Color gdi:
                    return Frozen(Color.FromArgb(gdi.A, gdi.R, gdi.G, gdi.B));
                default:
                    return null;
            }
        }

        private static SolidColorBrush Frozen(Color c)
        {
            // A fully transparent color means "not set" in several AutoCAD themes; treat it as absent
            // so the key keeps its fallback instead of rendering invisible text.
            if (c.A == 0) return null;
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }

        // ============================================================== ThemeManager (reflection)

        private static object CurrentTheme()
        {
            var manager = ManagerType();
            if (manager == null) return null;
            var instance = manager.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null) return null;
            return instance.GetType().GetProperty("CurrentPaletteTheme", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }

        private static Type ManagerType()
        {
            var type = Type.GetType("Autodesk.Windows.Themes.ThemeManager, AdUiPalettes", false);
            if (type != null) return type;
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => { try { return a.GetType("Autodesk.Windows.Themes.ThemeManager", false); } catch { return null; } })
                    .FirstOrDefault(t => t != null);
            }
            catch
            {
                return null;
            }
        }

        private static Delegate _handler;
        private static EventInfo _event;
        private static object _eventTarget;
        private static Action _onChanged;

        /// <summary>
        /// Calls <paramref name="onChanged"/> whenever AutoCAD's palette theme changes. Returns false when
        /// the event is not reachable — the caller still has the COLORTHEME system variable to fall back on.
        /// </summary>
        public static bool Subscribe(Action onChanged)
        {
            if (onChanged == null) return false;
            Unsubscribe();
            try
            {
                var manager = ManagerType();
                if (manager == null) return false;

                var ev = manager.GetEvent("PropertyChanged", BindingFlags.Public | BindingFlags.Static);
                object target = null;
                if (ev == null)
                {
                    ev = manager.GetEvent("PropertyChanged", BindingFlags.Public | BindingFlags.Instance);
                    target = manager.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (ev == null || target == null) return false;
                }

                var method = typeof(AcadTheme).GetMethod(nameof(OnThemePropertyChanged), BindingFlags.NonPublic | BindingFlags.Static);
                var handler = Delegate.CreateDelegate(ev.EventHandlerType, null, method, false);
                if (handler == null) return false;

                ev.AddEventHandler(target, handler);
                _event = ev;
                _eventTarget = target;
                _handler = handler;
                _onChanged = onChanged;
                return true;
            }
            catch
            {
                _event = null;
                _eventTarget = null;
                _handler = null;
                _onChanged = null;
                return false;
            }
        }

        /// <summary>Drops the theme subscription (called from the controller's Dispose).</summary>
        public static void Unsubscribe()
        {
            try
            {
                if (_event != null && _handler != null) _event.RemoveEventHandler(_eventTarget, _handler);
            }
            catch
            {
                // AutoCAD shutting down: nothing useful left to do.
            }
            _event = null;
            _eventTarget = null;
            _handler = null;
            _onChanged = null;
        }

        private static void OnThemePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var onChanged = _onChanged;
            if (onChanged == null) return;
            try
            {
                onChanged();
            }
            catch
            {
                // A theme repaint must never take AutoCAD down.
            }
        }
    }
}
