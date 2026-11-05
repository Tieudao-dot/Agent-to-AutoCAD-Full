using System;
using System.IO;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    public class UpdateLogicTests
    {
        private const string Sha = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"; // sha256("test")

        [Fact]
        public void ParsesManifest()
        {
            var m = UpdateLogic.ParseManifest("{\"version\":\"v0.4.0\",\"url\":\"https://example.com/ClaudeCAD-0.4.0.zip\",\"sha256\":\"" + Sha.ToUpperInvariant() + "\",\"notes\":\"Sửa lỗi\"}");
            Assert.Equal(new Version(0, 4, 0), m.Version);
            Assert.Equal(Sha, m.Sha256);
            Assert.Equal("Sửa lỗi", m.Notes);
        }

        [Theory]
        [InlineData("{\"url\":\"https://x/y.zip\",\"sha256\":\"" + Sha + "\"}")]
        [InlineData("{\"version\":\"1.0\",\"url\":\"http://x/y.zip\",\"sha256\":\"" + Sha + "\"}")]
        [InlineData("{\"version\":\"1.0\",\"url\":\"https://x/y.zip\",\"sha256\":\"abc\"}")]
        public void RejectsBadManifest(string json)
        {
            Assert.Throws<FormatException>(() => UpdateLogic.ParseManifest(json));
        }

        [Fact]
        public void ComparesVersionsIgnoringRevision()
        {
            Assert.True(UpdateLogic.IsNewer(new Version(0, 3, 0, 0), new Version(0, 4, 0)));
            Assert.False(UpdateLogic.IsNewer(new Version(0, 3, 0, 0), new Version(0, 3, 0)));
            Assert.False(UpdateLogic.IsNewer(new Version(1, 0, 0, 0), new Version(0, 9, 9)));
            Assert.Equal("1.2.0", UpdateLogic.Normalize("1.2.0-beta.1"));
            Assert.Equal("2.0", UpdateLogic.Normalize("2"));
        }

        [Fact]
        public void ChecksAtMostDaily()
        {
            var now = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
            Assert.True(UpdateLogic.ShouldCheck(null, now));
            Assert.False(UpdateLogic.ShouldCheck(now.AddHours(-3), now));
            Assert.True(UpdateLogic.ShouldCheck(now.AddHours(-25), now));
            Assert.True(UpdateLogic.ShouldCheck(now.AddDays(2), now)); // clock moved back
        }

        [Fact]
        public void Sha256OfStream()
        {
            Assert.Equal(Sha, UpdateLogic.Sha256Hex(new MemoryStream(Encoding.ASCII.GetBytes("test"))));
        }

        [Fact]
        public void ApplyScriptWaitsForAutoCadAndKeepsUserSkills()
        {
            var s = UpdateLogic.ApplyScript(4242, @"C:\st\ClaudeCAD.bundle", @"C:\Users\u\AppData\Roaming\Autodesk\ApplicationPlugins\ClaudeCAD.bundle", @"C:\log.txt");
            Assert.Contains("PID eq 4242", s);
            Assert.Contains("/PURGE", s);
            Assert.Contains("/XC /XN /XO", s); // skills: only new files
            Assert.Contains("/XD", s);
        }
    }
}
