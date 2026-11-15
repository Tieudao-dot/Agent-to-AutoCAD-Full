using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>The dockable ClaudeCAD palette (opened with CLAUDECHAT).</summary>
    public sealed partial class ChatPalette
    {
        private static readonly Guid PaletteGuid = new Guid("6F1B2C9A-3D4E-4B7A-9C51-2E8F0A6D7B13");
        private const int MinWidth = 300;
        private const int FullMinHeight = 400;
        /// <summary>Toolbar row + status line + one-line input + send row, plus the palette's own title bar.</summary>
        private const int CompactHeight = 170;

        private PaletteSet _ps;
        private Size _fullSize;

        public ChatControl Control { get; } = new ChatControl();

        public ChatPalette()
        {
            Control.CompactChanged += OnCompactChanged;
            Control.CompactLayoutChanged += FitCompact;
        }

        public void Show()
        {
            if (_ps == null)
            {
                // the name people see; the GUID (AutoCAD remembers the palette's place by it) and the command stay ClaudeCAD's
                _ps = new PaletteSet(Core.ProductInfo.DisplayName, "CLAUDECHAT", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new Size(MinWidth, FullMinHeight),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };
                _ps.AddVisual("Chat", Control);
                try
                {
                    // the palette's icon (rolled-up title bar, palette tabs) in both AutoCAD themes
                    _ps.DarkThemedIcon = StatusTray.GlyphIcon(true);
                    _ps.LightThemedIcon = StatusTray.GlyphIcon(false);
                }
                catch (Exception ex)
                {
                    Core.Logger.Warn("palette icon not set: " + ex.Message);
                }
                _ps.KeepFocus = true;
                _ps.Size = new Size(380, 640);
            }
            _ps.Visible = true;
            Control.FocusInput();
        }

        public bool Visible => _ps != null && _ps.Visible;

        /// <summary>
        /// The user can see the chat right now: the palette is open and not rolled up (auto-hide).
        /// False means a finished turn should be reported somewhere else (the status bar bubble).
        /// </summary>
        public bool InView
        {
            get
            {
                if (_ps == null) return false;
                try { return _ps.Visible && !_ps.RolledUp; }
                catch { return _ps.Visible; } // RolledUp is not readable for every docking state
            }
        }

        /// <summary>Gives the palette window the keyboard focus (after picking in the drawing).</summary>
        public void FocusInput()
        {
            try { _ps?.Activate(0); } catch { }
            Control.FocusInput();
        }

        /// <summary>
        /// Floating + compact: palette height = toolbar + news line + input row + AutoCAD's own frame, measured, so the
        /// input is the last thing in the palette (no empty strip under it). Docked palettes keep AutoCAD's size.
        /// </summary>
        private void FitCompact()
        {
            if (_ps == null || !Control.IsCompact) return;
            try
            {
                if (_ps.Dock != DockSides.None || Control.ActualHeight <= 0) return;
                double content = Control.CompactContentHeight();
                if (content <= 0) return;
                var source = System.Windows.PresentationSource.FromVisual(Control);
                double scale = source?.CompositionTarget?.TransformToDevice.M22 ?? 1;
                // AutoCAD's frame around the WPF content (title strip, borders), in pixels
                int frame = Math.Max(0, _ps.Size.Height - (int)Math.Round(Control.ActualHeight * scale));
                int height = Math.Max(90, Math.Min(320, (int)Math.Ceiling(content * scale) + frame + 1));
                if (Math.Abs(height - _ps.Size.Height) <= 1) return;
                _ps.MinimumSize = new Size(MinWidth, Math.Min(height, CompactHeight));
                _ps.Size = new Size(_ps.Size.Width, height);
            }
            catch (Exception ex)
            {
                Core.Logger.Warn("compact palette fit failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Compact mode shrinks the palette where it stands. A floating palette gets a low height and its
        /// old height back on expand; a docked one keeps its column (AutoCAD owns that size) and only the
        /// content collapses.
        /// </summary>
        private void OnCompactChanged(bool compact)
        {
            if (_ps == null) return;
            try
            {
                bool floating = _ps.Dock == DockSides.None;
                if (compact)
                {
                    _fullSize = _ps.Size;
                    _ps.MinimumSize = new Size(MinWidth, CompactHeight);
                    if (floating)
                    {
                        _ps.Size = new Size(_ps.Size.Width, CompactHeight);
                        // then shrink / grow to exactly what is shown once WPF has laid the compact content out
                        Control.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(FitCompact));
                    }
                }
                else
                {
                    _ps.MinimumSize = new Size(MinWidth, FullMinHeight);
                    if (floating)
                    {
                        int height = Math.Max(FullMinHeight, _fullSize.Height > 0 ? _fullSize.Height : 640);
                        _ps.Size = new Size(_ps.Size.Width, height);
                    }
                }
            }
            catch (Exception ex)
            {
                // Sizing is cosmetic: the content already collapsed / expanded inside the control.
                Core.Logger.Warn("palette resize failed: " + ex.Message);
            }
        }
    }
}
