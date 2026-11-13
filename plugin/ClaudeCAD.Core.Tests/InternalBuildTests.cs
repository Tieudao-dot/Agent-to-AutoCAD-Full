using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// BẢN THỬ NỘI BỘ (branch full/ban-thu-noi-bo only, decided 13/11/2026): the expiry date logic, the name people see,
    /// and the source checks that keep this build from calling an activation service, updating or being published.
    /// These tests compile Core without BAN_THU_NOI_BO (like the integration branch); the plugin build defines it.
    /// </summary>
    public class InternalBuildTests
    {
        private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
        private static string Plugin(params string[] parts) => Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray());

        private static DateTime Utc(int y, int mo, int d, int h = 0, int mi = 0, int s = 0) => new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc);

        [Fact]
        public void Label_and_messages_read_as_decided()
        {
            Assert.Equal("Agent to AutoCAD Full: BẢN THỬ NỘI BỘ (hết hạn 10/12/2026)", InternalBuild.Label);
            Assert.Equal("Bản thử nội bộ đã hết hạn ngày 10/12/2026. Cài bản chính thức để dùng tiếp.", InternalBuild.ExpiredMessage);
            Assert.StartsWith("Agent to AutoCAD Full", InternalBuild.Label);
        }

        [Fact]
        public void Expires_at_midnight_on_11_12_2026_Vietnam_time()
        {
            Assert.Equal(Utc(2026, 12, 10, 17), InternalBuild.ExpiresUtc);
            Assert.False(InternalBuild.IsExpired(Utc(2026, 11, 13, 3), null));
            Assert.False(InternalBuild.IsExpired(Utc(2026, 12, 10, 16, 59, 59), null)); // 23:59:59 on 10/12 in Vietnam
            Assert.True(InternalBuild.IsExpired(Utc(2026, 12, 10, 17), null));           // 00:00 on 11/12 in Vietnam
            Assert.True(InternalBuild.IsExpired(Utc(2027, 1, 1), null));
        }

        [Fact]
        public void Local_and_unspecified_times_are_read_correctly()
        {
            var local = Utc(2026, 12, 10, 17).ToLocalTime();
            Assert.True(InternalBuild.IsExpired(local, null));
            Assert.False(InternalBuild.IsExpired(local.AddSeconds(-1), null));
            Assert.True(InternalBuild.IsExpired(DateTime.SpecifyKind(Utc(2026, 12, 10, 17), DateTimeKind.Unspecified), null));
        }

        [Fact]
        public void Setting_the_clock_back_does_not_bring_the_build_back()
        {
            // the machine already showed 12/12/2026: a clock set to 01/12 is a clock set back
            Assert.True(LicenseLogic.ClockRolledBack(Utc(2026, 12, 1), Utc(2026, 12, 12)));
            Assert.True(InternalBuild.IsExpired(Utc(2026, 12, 1), Utc(2026, 12, 12)));
            // a few hours back across midnight (inside the day of slack): the later time still counts
            Assert.True(InternalBuild.IsExpired(Utc(2026, 12, 10, 14), Utc(2026, 12, 10, 17, 30)));
            // a seen time before the expiry with a right clock: still usable
            Assert.False(InternalBuild.IsExpired(Utc(2026, 12, 5), Utc(2026, 12, 4)));
            Assert.Equal(Utc(2026, 12, 12), InternalBuild.EffectiveUtc(Utc(2026, 12, 1), Utc(2026, 12, 12)));
            Assert.Equal(Utc(2026, 12, 5), InternalBuild.EffectiveUtc(Utc(2026, 12, 5), Utc(2026, 12, 4)));
        }

        [Fact]
        public void Days_left_count_today_in_Vietnam()
        {
            Assert.Equal(1, InternalBuild.DaysLeft(Utc(2026, 12, 10, 16), null));  // 23:00 on 10/12 VN: the last day
            Assert.Equal(1, InternalBuild.DaysLeft(Utc(2026, 12, 9, 17), null));   // 00:00 on 10/12 VN
            Assert.Equal(2, InternalBuild.DaysLeft(Utc(2026, 12, 9, 16), null));   // 23:00 on 09/12 VN
            Assert.Equal(28, InternalBuild.DaysLeft(Utc(2026, 11, 13, 2), null));  // 13/11 → 10/12
            Assert.Equal(0, InternalBuild.DaysLeft(Utc(2026, 12, 10, 17), null));
            Assert.Equal(0, InternalBuild.DaysLeft(Utc(2026, 12, 1), Utc(2026, 12, 12)));
        }

        [Fact]
        public void Status_text_tells_usable_from_expired()
        {
            Assert.Contains("còn 28 ngày", InternalBuild.StatusText(Utc(2026, 11, 13, 2), null));
            Assert.Contains("gói Full", InternalBuild.StatusText(Utc(2026, 11, 13, 2), null));
            Assert.Contains("đã hết hạn", InternalBuild.StatusText(Utc(2026, 12, 11), null));
        }

        [Fact]
        public void Plugin_is_built_with_BAN_THU_NOI_BO_and_no_run_time_switch()
        {
            Assert.Contains("<DefineConstants>$(DefineConstants);BAN_THU_NOI_BO</DefineConstants>", Plugin("ClaudeCAD.Plugin.csproj"));
            var ed = Plugin("Core", "Edition.cs");
            Assert.Contains("#if BAN_THU_NOI_BO\n        /// <summary>Bản thử nội bộ (branch full/ban-thu-noi-bo only): the name says so everywhere people read it.</summary>\n        public const string DisplayName = InternalBuild.Label;", ed.Replace("\r\n", "\n"));
            var act = Plugin("Sidecar", "Activation.cs").Replace("\r\n", "\n");
            Assert.Contains("#if BAN_THU_NOI_BO\n        public static string Plan => PlanLogic.PlanFull;", act);
            Assert.Contains("public static string ServiceUrl => null;", act);
            Assert.Contains("public static bool IsActivated => true;", act);
            Assert.Contains("return !InternalExpired;", act);
            Assert.Contains("return InternalBuild.ExpiredMessage;", act);
            Assert.Contains("var expired = InternalBuild.IsExpired(now, seen);", act);
            // the internal path of Activation reads no environment variable (CLAUDECAD_ACTIVATION_URL is never used)
            var internalPart = act.Substring(act.IndexOf("#if BAN_THU_NOI_BO", StringComparison.Ordinal));
            internalPart = internalPart.Substring(0, internalPart.IndexOf("#else", StringComparison.Ordinal));
            Assert.DoesNotContain("GetEnvironmentVariable", internalPart);
            // no update: the manifest link is never read
            var upd = Plugin("Sidecar", "UpdateService.cs").Replace("\r\n", "\n");
            Assert.Contains("#if BAN_THU_NOI_BO\n                return null; // bản thử nội bộ: no automatic update", upd);
            Assert.Contains("Bản thử nội bộ không tự cập nhật.", upd);
        }

        [Fact]
        public void No_activation_service_url_on_this_branch()
        {
            var json = Src("activation", "activation.json");
            Assert.Contains("\"url\": \"\"", json);
            Assert.DoesNotContain("script.google.com", json);
        }

        [Fact]
        public void Installer_has_the_label_its_own_file_name_and_no_activation_page()
        {
            var iss = Src("installer", "ClaudeCAD.iss").Replace("\r\n", "\n");
            Assert.Contains("#define BanThuNoiBo\n", iss);
            Assert.Contains($"#define AppDisplayName \"{InternalBuild.Label}\"", iss);
            Assert.Contains("OutputBaseFilename=AgentToAutoCADFull-ThuNoiBo-Setup", iss);
            Assert.Contains("#ifdef BanThuNoiBo\n  { bản thử nội bộ: no activation page at all (nothing is requested or sent) }\n  Result := (KeyPage <> nil) and (PageID = KeyPage.ID);", iss);
            Assert.Contains("#ifdef BanThuNoiBo\n  ; never an activation service in the bản thử nội bộ, whatever activation.json says\n  #ifdef ActivationUrl\n    #undef ActivationUrl", iss);
            Assert.Contains("WelcomeLabel2=ĐÂY LÀ BẢN THỬ NỘI BỘ", iss);
            // the technical identity stays the Full edition's (a real installer installs over it later)
            Assert.Contains("AppId={{8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40}", iss);
            Assert.Contains("DefaultDirName={userappdata}\\Autodesk\\ApplicationPlugins\\ClaudeCAD.bundle", iss);
        }

        [Fact]
        public void CI_refuses_to_publish_and_ships_no_update_link()
        {
            var yml = Src(".github", "workflows", "build.yml");
            Assert.Contains("name: Refuse to publish the bản thử nội bộ", yml);
            Assert.Contains("if ($csproj -match 'BAN_THU_NOI_BO' -or '${{ github.ref_name }}' -eq 'full/ban-thu-noi-bo')", yml);
            Assert.Contains("&& github.ref_name != 'full/ban-thu-noi-bo'", yml);
            Assert.Contains("bundle/out/AgentToAutoCADFull-ThuNoiBo-Setup.exe", yml);
            Assert.DoesNotContain("--update-url", yml);
            // the refusal comes before the release step
            Assert.True(yml.IndexOf("name: Refuse to publish", StringComparison.Ordinal) < yml.IndexOf("uses: softprops/action-gh-release", StringComparison.Ordinal));
        }
    }
}
