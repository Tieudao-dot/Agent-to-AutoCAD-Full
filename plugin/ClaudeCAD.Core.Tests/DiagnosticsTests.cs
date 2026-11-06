using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>v0.9.2 "Gửi báo cáo lỗi": secrets never leave the computer, the right files are picked.</summary>
    public class DiagnosticsTests
    {
        [Fact]
        public void Redacts_keys_and_tokens()
        {
            var text = "key sk-ant-api03-AbCdEf1234567890_xyz-QQ end\n" +
                       "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig\n" +
                       "{\"accessToken\": \"abcdefghijklmnop\", \"refresh_token\":\"qrstuvwxyz123456\"}\n" +
                       "set ANTHROPIC_API_KEY=sk-ant-zzzzzzzzzzzz\nCLAUDE_CODE_OAUTH_TOKEN: abcdef123456789\n" +
                       "tool draw_line ok in 12 ms";
            var r = Diagnostics.Redact(text);
            Assert.DoesNotContain("AbCdEf1234567890", r);
            Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", r);
            Assert.DoesNotContain("abcdefghijklmnop", r);
            Assert.DoesNotContain("qrstuvwxyz123456", r);
            Assert.DoesNotContain("zzzzzzzzzzzz", r);
            Assert.DoesNotContain("abcdef123456789", r);
            Assert.Contains("sk-ant-***", r);
            Assert.Contains("Bearer ***", r);
            Assert.Contains("tool draw_line ok in 12 ms", r); // ordinary log lines stay
            Assert.Equal("", Diagnostics.Redact(null));
        }

        [Fact]
        public void Picks_recent_files_newest_first()
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0);
            var files = new[]
            {
                ("a.log", now.AddDays(-1)), ("b.log", now.AddHours(-1)), ("old.log", now.AddDays(-10)), ("c.log", now.AddDays(-2)),
            };
            Assert.Equal(new[] { "b.log", "a.log", "c.log" }, Diagnostics.RecentFiles(files, now, 3, 10).ToArray());
            Assert.Equal(new[] { "b.log" }, Diagnostics.RecentFiles(files, now, 3, 1).ToArray());
        }

        [Fact]
        public void Keeps_only_ClaudeCAD_setup_logs()
        {
            Assert.True(Diagnostics.IsClaudeCadSetupLog("Setup Log 2026-11-06 #001.txt", "Setup application started\nSetup version: Inno Setup 6\nAppName: ClaudeCAD"));
            Assert.False(Diagnostics.IsClaudeCadSetupLog("Setup Log 2026-11-06 #002.txt", "AppName: SomethingElse"));
            Assert.False(Diagnostics.IsClaudeCadSetupLog("other.txt", "ClaudeCAD"));
        }

        [Fact]
        public void Reads_the_end_of_big_logs()
        {
            var f = Path.Combine(Path.GetTempPath(), "ccdiag-" + Guid.NewGuid().ToString("N") + ".log");
            File.WriteAllText(f, new string('a', 5000) + "LAST-ERROR");
            try
            {
                var tail = Diagnostics.ReadTail(f, 100);
                Assert.EndsWith("LAST-ERROR", tail);
                Assert.StartsWith("[… phần đầu log đã bỏ bớt …]", tail);
                Assert.Equal(5010, Diagnostics.ReadTail(f).Length);
            }
            finally { File.Delete(f); }
        }
    }
}
