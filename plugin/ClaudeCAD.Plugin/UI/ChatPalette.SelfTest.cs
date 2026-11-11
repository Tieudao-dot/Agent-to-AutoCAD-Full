using System.Drawing;
using Autodesk.AutoCAD.Windows;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>What CLAUDECHATSELFTEST reads from the palette window. Read-only.</summary>
    public sealed partial class ChatPalette
    {
        /// <summary>The palette window exists (CLAUDECHAT was run at least once in this AutoCAD session).</summary>
        public bool Created => _ps != null;

        /// <summary>Floating (true) or docked left / right; false when the palette was never opened.</summary>
        public bool Floating
        {
            get
            {
                try { return _ps != null && _ps.Dock == DockSides.None; }
                catch { return false; }
            }
        }

        /// <summary>Current size of the palette window; empty when it was never opened.</summary>
        public Size CurrentSize
        {
            get
            {
                try { return _ps?.Size ?? Size.Empty; }
                catch { return Size.Empty; }
            }
        }
    }
}
