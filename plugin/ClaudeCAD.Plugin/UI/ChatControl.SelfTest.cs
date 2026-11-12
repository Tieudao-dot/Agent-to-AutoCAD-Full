using System.Windows;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>What CLAUDECHATSELFTEST reads from the chat control. Read-only: it changes nothing.</summary>
    public sealed partial class ChatControl
    {
        /// <summary>The control's state at one moment (UI thread only).</summary>
        public sealed class SelfTestProbe
        {
            /// <summary>Elements in the conversation (messages, cards, notes).</summary>
            public int Messages;
            public bool Compact;
            /// <summary>The conversation area is shown (false while compact).</summary>
            public bool ConversationShown;
            /// <summary>The "⏳ Chờ gửi…" row is shown.</summary>
            public bool QueuedShown;
            /// <summary>The flat control styles are merged into this control's resources.</summary>
            public bool StylesMerged;
            /// <summary>The palette brushes are in this control's resources.</summary>
            public bool BrushesSet;
            /// <summary>Text of the "Gói: …" badge in the top bar ("" when hidden).</summary>
            public string PlanBadge;
        }

        public SelfTestProbe ProbeForSelfTest() => new SelfTestProbe
        {
            Messages = _messages.Children.Count,
            Compact = _compact,
            ConversationShown = _middle != null && _middle.Visibility == Visibility.Visible,
            QueuedShown = _queuedRow != null && _queuedRow.Visibility == Visibility.Visible,
            StylesMerged = Resources.MergedDictionaries.Count > 0,
            BrushesSet = Resources.Contains(Theme.PanelBg) && Resources.Contains(Theme.Fg),
            PlanBadge = _planBadge != null && _planBadge.Visibility == Visibility.Visible ? _planBadge.Text : "",
        };
    }
}
