using System.Collections.Generic;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>The ✓ / ✗ rules of CLAUDECHATSELFTEST that do not need AutoCAD.</summary>
    public class SelfTestRulesTests
    {
        private const string Fx48 = ".NETFramework,Version=v4.8";
        private const string Core8 = ".NETCoreApp,Version=v8.0";
        private const string Core10 = ".NETCoreApp,Version=v10.0";
        private const string Bundle = @"C:\Users\a\AppData\Roaming\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\";

        // ------------------------------------------------------------ build ↔ AutoCAD series

        [Theory]
        [InlineData(24, 1, "net48")]   // 2022
        [InlineData(24, 2, "net48")]   // 2023
        [InlineData(24, 3, "net48")]   // 2024
        [InlineData(25, 0, "net8")]    // 2025
        [InlineData(25, 1, "net8")]    // 2026 shares the 2025 build
        [InlineData(26, 0, "net10")]   // 2027
        [InlineData(24, 0, null)]      // 2021: never checked
        [InlineData(26, 1, null)]      // a release after 2027
        [InlineData(23, 1, null)]
        public void EachAutoCadSeriesHasItsBuild(int major, int minor, string expected)
        {
            Assert.Equal(expected, SelfTestRules.ExpectedBuild(major, minor));
        }

        [Theory]
        [InlineData(Fx48, "net48")]
        [InlineData(Core8, "net8")]
        [InlineData(Core10, "net10")]
        [InlineData(".NETCoreApp,Version=v8.0,Profile=x", "net8")]
        [InlineData(".NETCoreApp,Version=v6.0", null)]
        [InlineData(".NETStandard,Version=v2.0", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void BuildIsReadFromTheTargetFramework(string framework, string expected)
        {
            Assert.Equal(expected, SelfTestRules.BuildOf(framework));
        }

        [Fact]
        public void RightBuildPasses()
        {
            Assert.True(SelfTestRules.CheckBuild(24, 1, Fx48, 4, Bundle + "ClaudeCAD.Plugin.dll").Ok);
            Assert.True(SelfTestRules.CheckBuild(25, 0, Core8, 8, Bundle + @"2025\ClaudeCAD.Plugin.dll").Ok);
            Assert.True(SelfTestRules.CheckBuild(26, 0, Core10, 10, Bundle + @"2027\ClaudeCAD.Plugin.dll").Ok);
        }

        [Fact]
        public void Net8BuildStillPassesAfterAutoCadMovesToNet10()
        {
            var v = SelfTestRules.CheckBuild(25, 1, Core8, 10, Bundle + @"2025\ClaudeCAD.Plugin.dll");
            Assert.True(v.Ok, v.Detail);
            Assert.Contains(".NET 10", v.Detail);
            Assert.Contains("net8", v.Detail);
        }

        [Fact]
        public void WrongBuildFailsAndNamesBoth()
        {
            var v = SelfTestRules.CheckBuild(26, 0, Core8, 10, Bundle + @"2025\ClaudeCAD.Plugin.dll");
            Assert.False(v.Ok);
            Assert.Contains("net10", v.Detail);
            Assert.Contains("net8", v.Detail);
            Assert.Contains("2027", v.Detail);

            Assert.False(SelfTestRules.CheckBuild(24, 3, Core8, 8, null).Ok);
            Assert.False(SelfTestRules.CheckBuild(25, 0, Core10, 10, null).Ok);
        }

        [Fact]
        public void UnknownAutoCadOrUnknownDllFails()
        {
            var newer = SelfTestRules.CheckBuild(27, 0, Core10, 10, null);
            Assert.False(newer.Ok);
            Assert.Contains("R27.0", newer.Detail);

            var odd = SelfTestRules.CheckBuild(25, 0, ".NETCoreApp,Version=v6.0", 8, null);
            Assert.False(odd.Ok);
            Assert.Contains("v6.0", odd.Detail);

            Assert.False(SelfTestRules.CheckBuild(25, 0, null, 8, null).Ok);
        }

        [Fact]
        public void RuntimeOlderThanTheBuildFails()
        {
            Assert.False(SelfTestRules.CheckBuild(26, 0, Core10, 8, null).Ok);
        }

        [Fact]
        public void PathIsOnlyANote()
        {
            // NETLOAD from a build folder: right build, so still a pass, with a remark
            var dev = SelfTestRules.CheckBuild(25, 0, Core8, 8, @"D:\src\plugin\bin\Release\acad2025\ClaudeCAD.Plugin.dll");
            Assert.True(dev.Ok);
            Assert.Contains("NETLOAD", dev.Detail);

            // the right build copied into the wrong bundle folder is pointed out
            var misplaced = SelfTestRules.CheckBuild(25, 0, Core8, 8, Bundle + "ClaudeCAD.Plugin.dll");
            Assert.True(misplaced.Ok);
            Assert.Contains(@"Contents\2025", misplaced.Detail);

            var right = SelfTestRules.CheckBuild(24, 2, Fx48, 4, Bundle + "ClaudeCAD.Plugin.dll");
            Assert.DoesNotContain("không phải chỗ", right.Detail);
            Assert.DoesNotContain("NETLOAD", right.Detail);
        }

        // ------------------------------------------------------------ theme

        [Fact]
        public void ContrastMatchesKnownValues()
        {
            Assert.Equal(21.0, SelfTestRules.Contrast(0, 0, 0, 255, 255, 255), 1);
            Assert.Equal(1.0, SelfTestRules.Contrast(40, 40, 40, 40, 40, 40), 3);
            // order does not matter
            Assert.Equal(SelfTestRules.Contrast(0xDF, 0xE2, 0xE5, 0x2E, 0x32, 0x38), SelfTestRules.Contrast(0x2E, 0x32, 0x38, 0xDF, 0xE2, 0xE5), 6);
            // AutoCAD's dark palette text on its background is comfortably readable
            Assert.True(SelfTestRules.Contrast(0xDF, 0xE2, 0xE5, 0x2E, 0x32, 0x38) > 7);
        }

        [Fact]
        public void ThemeNotReadFails()
        {
            var v = SelfTestRules.CheckTheme(false, null, true, 0, 15, null, 0, 0, false);
            Assert.False(v.Ok);
            Assert.Contains("COLORTHEME = 0", v.Detail);
            Assert.False(SelfTestRules.CheckTheme(true, true, true, 0, 15, "Tahoma", 12, 0, true).Ok);
        }

        [Fact]
        public void UnreadableTextFails()
        {
            var v = SelfTestRules.CheckTheme(true, true, true, 12, 15, "Tahoma", 12, 1.4, true);
            Assert.False(v.Ok);
            Assert.Contains("1.4", v.Detail);
        }

        [Fact]
        public void GoodThemePassesAndSaysWhatItRead()
        {
            var v = SelfTestRules.CheckTheme(true, false, false, 12, 15, "Segoe UI", 12, 11.2, true);
            Assert.True(v.Ok);
            Assert.Contains("theme sáng", v.Detail);
            Assert.Contains("12/15 màu", v.Detail);
            Assert.Contains("Segoe UI 12", v.Detail);
            Assert.Contains("sự kiện", v.Detail);
            Assert.DoesNotContain("khác nhau", v.Detail);
        }

        [Fact]
        public void ThemeAndVariableDisagreeingIsANoteNotAFailure()
        {
            var v = SelfTestRules.CheckTheme(true, true, false, 12, 15, null, 0, 0, false);
            Assert.True(v.Ok);
            Assert.Contains("khác nhau", v.Detail);
            Assert.Contains("COLORTHEME", v.Detail);
            Assert.Contains("font mặc định", v.Detail);
        }

        // ------------------------------------------------------------ compact mode

        [Fact]
        public void CompactKeepsEveryMessage()
        {
            Assert.True(SelfTestRules.CheckCompact(5, 5, 5, true, true).Ok);
            Assert.True(SelfTestRules.CheckCompact(0, 0, 0, true, true).Ok);
        }

        [Theory]
        [InlineData(5, 4, 5, true, true)]    // lost while compact
        [InlineData(5, 5, 3, true, true)]    // lost on expand
        [InlineData(5, 5, 5, false, true)]   // did not collapse
        [InlineData(5, 5, 5, true, false)]   // did not come back
        public void CompactProblemsFail(int before, int mid, int after, bool hidden, bool shown)
        {
            Assert.False(SelfTestRules.CheckCompact(before, mid, after, hidden, shown).Ok);
        }

        [Fact]
        public void PaletteSizeAllowsDpiRounding()
        {
            Assert.True(SelfTestRules.SameSize(380, 640, 381, 638));
            Assert.False(SelfTestRules.SameSize(380, 640, 380, 170));
        }

        // ------------------------------------------------------------ queue

        [Fact]
        public void QueueRules()
        {
            Assert.True(SelfTestRules.CheckQueue(false, false, true, true).Ok);
            Assert.True(SelfTestRules.CheckQueue(true, true, true, true).Ok);
            Assert.False(SelfTestRules.CheckQueue(false, true, true, true).Ok);   // stuck: nothing will send it
            Assert.False(SelfTestRules.CheckQueue(false, false, false, true).Ok);
            Assert.False(SelfTestRules.CheckQueue(false, false, true, false).Ok);
        }

        // ------------------------------------------------------------ skills

        private static SkillInfo Skill(string name, bool builtin = false, string error = null) =>
            new SkillInfo { Name = name, Builtin = builtin, Error = error };

        [Fact]
        public void SkillsPassWithLibraryAndChatCounts()
        {
            var lib = new List<SkillInfo> { Skill("ve-kien-truc", true), Skill("ve-mep", true), Skill("cong-ty") };
            var v = SelfTestRules.CheckSkills(true, @"C:\skills", lib, new[] { "cong-ty" }, new[] { "cong-ty" });
            Assert.True(v.Ok);
            Assert.Contains("thư viện 3 skill", v.Detail);
            Assert.Contains("dùng 1 (cong-ty)", v.Detail);
        }

        [Fact]
        public void MissingFolderOrBrokenShippedSkillFails()
        {
            Assert.False(SelfTestRules.CheckSkills(false, @"C:\skills", null, null, null).Ok);
            var v = SelfTestRules.CheckSkills(true, @"C:\skills", new List<SkillInfo> { Skill("ve-mep", true, "thiếu description") }, null, null);
            Assert.False(v.Ok);
            Assert.Contains("ve-mep", v.Detail);
        }

        [Fact]
        public void BrokenOwnSkillAndRemovedChoiceAreNotes()
        {
            var lib = new List<SkillInfo> { Skill("ve-mep", true), Skill("hong", false, "thiếu name") };
            var v = SelfTestRules.CheckSkills(true, @"C:\skills", lib, new[] { "ve-mep", "da-xoa" }, new[] { "ve-mep" });
            Assert.True(v.Ok);
            Assert.Contains("hong", v.Detail);
            Assert.Contains("da-xoa", v.Detail);
        }

        [Fact]
        public void ChatUsingAnUnreadableSkillFails()
        {
            var lib = new List<SkillInfo> { Skill("hong", false, "thiếu name") };
            Assert.False(SelfTestRules.CheckSkills(true, @"C:\skills", lib, new[] { "hong" }, new[] { "hong" }).Ok);
        }

        // ------------------------------------------------------------ the user's drawing

        [Fact]
        public void UntouchedDrawingPasses()
        {
            var v = SelfTestRules.CheckUntouched(@"D:\du-an\mat-bang.dwg", "2F1", "2f1", 120, 120, 0, 0);
            Assert.True(v.Ok);
            Assert.Contains("mat-bang.dwg", v.Detail);
            Assert.DoesNotContain("du-an", v.Detail);   // file name only, not the folder
        }

        [Fact]
        public void AnyChangeToTheDrawingFails()
        {
            Assert.False(SelfTestRules.CheckUntouched("a.dwg", "2F1", "2F4", 120, 120, 0, 0).Ok);
            Assert.False(SelfTestRules.CheckUntouched("a.dwg", "2F1", "2F1", 120, 119, 0, 0).Ok);
            Assert.False(SelfTestRules.CheckUntouched("a.dwg", "2F1", "2F1", 120, 120, 0, 5).Ok);
        }

        [Fact]
        public void UnreadableDbmodIsNotCompared()
        {
            var v = SelfTestRules.CheckUntouched("a.dwg", "2F1", "2F1", 120, 120, 0, -1);
            Assert.True(v.Ok);
            Assert.Contains("DBMOD", v.Detail);
        }
    }
}
