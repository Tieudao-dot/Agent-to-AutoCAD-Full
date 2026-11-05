using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>The dockable ClaudeCAD palette (opened with CLAUDECHAT).</summary>
    public sealed class ChatPalette
    {
        private static readonly Guid PaletteGuid = new Guid("6F1B2C9A-3D4E-4B7A-9C51-2E8F0A6D7B13");
        private PaletteSet _ps;

        public ChatControl Control { get; } = new ChatControl();

        public void Show()
        {
            if (_ps == null)
            {
                _ps = new PaletteSet("ClaudeCAD", "CLAUDECHAT", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new Size(300, 400),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };
                _ps.AddVisual("Chat", Control);
                _ps.KeepFocus = true;
                _ps.Size = new Size(380, 640);
            }
            _ps.Visible = true;
            Control.FocusInput();
        }

        public bool Visible => _ps != null && _ps.Visible;
    }
}
