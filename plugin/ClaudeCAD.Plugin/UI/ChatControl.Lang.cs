using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Nâng cấp đợt H9: a changed language shows at once. The palette's fixed texts (buttons, tool tips, ⚙ rows,
    /// panels, menus) were set once when it was built, so they are walked and each whole text found in the English
    /// table is swapped (Vietnamese ↔ English). Texts with numbers in them (status, counts) are written again by the
    /// code that shows them; the chat itself is drawn again by the controller (LoadTranscript).
    /// </summary>
    public sealed partial class ChatControl
    {
        /// <summary>Swaps every fixed text of the palette from one language to the other.</summary>
        public void Relabel(string from, string to) => Ui(() =>
        {
            from = L.Normalize(from);
            to = L.Normalize(to);
            if (from == to) return;
            var seen = new HashSet<object>();
            _suppressSettingsEvents = true;
            try
            {
                Walk(this, from, to, seen);
                // popups and menus live outside the tree while they are closed
                foreach (var extra in new object[] { _settingsPopup, _skillsPopup, _macrosPopup, _mentionPopup, _slashPopup, _modeMenu, _notesPanel })
                    if (extra is DependencyObject d) Walk(d, from, to, seen);
            }
            finally
            {
                _suppressSettingsEvents = false;
            }
            PaintWorkMode();
        });

        private static void Walk(DependencyObject node, string from, string to, HashSet<object> seen)
        {
            if (node == null || !seen.Add(node)) return;
            switch (node)
            {
                case TextBlock tb:
                    if (tb.Inlines.Count > 1 || (tb.Inlines.FirstInline != null && !(tb.Inlines.FirstInline is Run)))
                        foreach (var inl in tb.Inlines.ToList()) WalkInline(inl, from, to);
                    else
                    {
                        var t = L.Retranslate(tb.Text, from, to);
                        if (t != null) tb.Text = t;
                    }
                    break;
                case TextBox _:
                    return; // what the user typed is never touched
                case HeaderedItemsControl hic when hic.Header is string h:
                    var nh = L.Retranslate(h, from, to);
                    if (nh != null) hic.Header = nh;
                    break;
                case HeaderedContentControl hcc when hcc.Header is string h2:
                    var nh2 = L.Retranslate(h2, from, to);
                    if (nh2 != null) hcc.Header = nh2;
                    break;
            }
            if (node is ContentControl cc && cc.Content is string cs)
            {
                var t = L.Retranslate(cs, from, to);
                if (t != null) cc.Content = t;
            }
            if (node is FrameworkElement fe)
            {
                if (fe.ToolTip is string tip)
                {
                    var t = L.Retranslate(tip, from, to);
                    if (t != null) fe.ToolTip = t;
                }
                else if (fe.ToolTip is DependencyObject tipObj) Walk(tipObj, from, to, seen);
                if (fe.ContextMenu != null) Walk(fe.ContextMenu, from, to, seen);
            }
            if (node is ComboBox combo)
            {
                int sel = combo.SelectedIndex;
                for (int i = 0; i < combo.Items.Count; i++)
                    if (combo.Items[i] is string item)
                    {
                        var t = L.Retranslate(item, from, to);
                        if (t != null) combo.Items[i] = t;
                    }
                combo.SelectedIndex = sel;
            }
            if (node is Popup pop && pop.Child != null) Walk(pop.Child, from, to, seen);
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().ToList())
                Walk(child, from, to, seen);
        }

        private static void WalkInline(Inline inl, string from, string to)
        {
            switch (inl)
            {
                case Run r:
                    var t = L.Retranslate(r.Text, from, to);
                    if (t != null) r.Text = t;
                    break;
                case Span sp:
                    foreach (var c in sp.Inlines.ToList()) WalkInline(c, from, to);
                    break;
            }
        }
    }
}
