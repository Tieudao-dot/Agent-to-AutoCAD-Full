using System;
using System.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// What CLAUDECHATSELFTEST reads from the chat control (ProbeForSelfTest), on the real WPF control:
    /// the same sequence the self-test runs inside AutoCAD, minus AutoCAD.
    /// </summary>
    public class SelfTestProbeTests
    {
        private static void Sta(Action a)
        {
            Exception error = null;
            var t = new Thread(() =>
            {
                try { a(); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw new Exception("UI test failed: " + error.Message, error);
        }

        [Fact]
        public void ProbeSeesStylesBrushesAndMessages()
        {
            ChatControl.SelfTestProbe empty = null, filled = null;
            bool stylesLoaded = false;
            Sta(() =>
            {
                var c = new ChatControl();
                stylesLoaded = Theme.StylesLoaded;
                empty = c.ProbeForSelfTest();
                for (int i = 1; i <= 3; i++) c.AddUserMessage(new ChatEntry { Role = "user", Text = "tự kiểm tra " + i });
                filled = c.ProbeForSelfTest();
            });
            Assert.True(stylesLoaded, "the flat control styles did not parse");
            Assert.True(empty.StylesMerged);
            Assert.True(empty.BrushesSet);
            Assert.False(empty.Compact);
            Assert.True(empty.ConversationShown);
            Assert.False(empty.QueuedShown);
            Assert.Equal(3, filled.Messages - empty.Messages);
        }

        [Fact]
        public void CompactRoundTripPassesTheSelfTestRule()
        {
            ChatControl.SelfTestProbe p0 = null, p1 = null, p2 = null;
            Sta(() =>
            {
                var c = new ChatControl();
                for (int i = 1; i <= 3; i++) c.AddUserMessage(new ChatEntry { Role = "user", Text = "tự kiểm tra " + i });
                p0 = c.ProbeForSelfTest();
                c.SetCompact(true);
                p1 = c.ProbeForSelfTest();
                c.SetCompact(false);
                p2 = c.ProbeForSelfTest();
            });
            var v = SelfTestRules.CheckCompact(p0.Messages, p1.Messages, p2.Messages,
                p1.Compact && !p1.ConversationShown, !p2.Compact && p2.ConversationShown);
            Assert.True(v.Ok, v.Detail);
        }

        [Fact]
        public void QueuedRowShowsAndHides()
        {
            bool shows = false, hides = false;
            Sta(() =>
            {
                var c = new ChatControl();
                c.SetQueued("thêm cửa sổ 1200");
                shows = c.ProbeForSelfTest().QueuedShown;
                c.SetQueued(null);
                hides = !c.ProbeForSelfTest().QueuedShown;
            });
            Assert.True(SelfTestRules.CheckQueue(false, false, shows, hides).Ok);
        }
    }
}
