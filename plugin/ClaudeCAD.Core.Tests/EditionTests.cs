using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// The Full edition replaces the original ClaudeCAD as an upgrade (decided 12/11/2026, docs/ban-giao/test-tong-the.md):
    /// the technical identity is unchanged, the names people read are "Agent to AutoCAD Full", Contents\edition.json marks
    /// the edition, and the installer tells the original apart. The Pascal of installer/ClaudeCAD.iss cannot run here:
    /// EditionLogic is the same rules in C#, and the source checks keep the two in step.
    /// </summary>
    public class EditionTests
    {
        private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
        private static string Iss => Src("installer", "ClaudeCAD.iss");
        private static string Manifest => Src("bundle", "PackageContents.xml");

        private const string OriginalUpdate = "{\n  \"manifest_url\": \"https://github.com/Tieudao-dot/Agent-to-Autocad/releases/latest/download/latest.json\"\n}";
        private const string FullUpdate = "{\"manifest_url\": \"https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/releases/latest/download/latest.json\"}";

        [Fact]
        public void Edition_json_full_is_a_normal_upgrade()
        {
            Assert.Equal(InstalledEdition.Full, EditionLogic.Detect(true, Src("bundle", "edition.json"), null));
            Assert.Equal(InstalledEdition.Full, EditionLogic.Detect(true, "{ \"Edition\" :\r\n \"FULL\" }", OriginalUpdate)); // edition.json wins
            Assert.False(EditionLogic.NeedsWarning(InstalledEdition.Full));
            Assert.False(EditionLogic.NeedsWarning(InstalledEdition.None));
        }

        [Fact]
        public void The_original_is_told_by_its_update_link()
        {
            // v0.9.3 (released) and later builds of the original repository have update-config.json, no edition.json
            Assert.Equal(InstalledEdition.Original, EditionLogic.Detect(true, null, OriginalUpdate));
            Assert.Equal(InstalledEdition.Original, EditionLogic.Detect(true, "", OriginalUpdate.ToUpperInvariant()));
            Assert.True(EditionLogic.NeedsWarning(InstalledEdition.Original));
            // the Full repository's link does not look like the original's ("autocad-full/" vs "autocad/")
            Assert.Equal(InstalledEdition.Full, EditionLogic.Detect(true, null, FullUpdate));
            Assert.DoesNotContain(EditionLogic.OriginalRepoMarker, EditionLogic.Squeeze(FullUpdate));
        }

        [Fact]
        public void A_bundle_that_cannot_be_identified_is_treated_like_the_original()
        {
            Assert.Equal(InstalledEdition.Unknown, EditionLogic.Detect(true, null, null));
            Assert.Equal(InstalledEdition.Unknown, EditionLogic.Detect(true, "not json", "{\"manifest_url\": \"https://example.com/latest.json\"}"));
            Assert.Equal(InstalledEdition.Unknown, EditionLogic.Detect(true, "{\"edition\": \"2d\"}", FullUpdate)); // another edition
            Assert.True(EditionLogic.NeedsWarning(InstalledEdition.Unknown));
            Assert.Equal(InstalledEdition.None, EditionLogic.Detect(false, Src("bundle", "edition.json"), OriginalUpdate));
            Assert.Equal("original", EditionLogic.Label(InstalledEdition.Original));
            Assert.Equal("unknown", EditionLogic.Label(InstalledEdition.Unknown));
        }

        [Fact]
        public void Version_of_the_installed_bundle_is_read_from_PackageContents()
        {
            Assert.Equal("0.9.4", EditionLogic.VersionFromPackageContents(Manifest));
            Assert.Equal("0.9.3", EditionLogic.VersionFromPackageContents("<ApplicationPackage Name=\"ClaudeCAD\" AppVersion=\"0.9.3\" Author=\"ClaudeCAD\">"));
            Assert.Equal("", EditionLogic.VersionFromPackageContents("<ApplicationPackage Name=\"ClaudeCAD\">"));
            Assert.Equal("", EditionLogic.VersionFromPackageContents("AppVersion=\"" + new string('9', 40) + "\""));
            Assert.Equal("", EditionLogic.VersionFromPackageContents(null));
        }

        [Fact]
        public void Names_and_versions()
        {
            Assert.Equal("Agent to AutoCAD Full", ProductInfo.DisplayName);
            Assert.Equal("Agent to AutoCAD Full 0.9.4", ProductInfo.WithVersion(ProductInfo.ShortVersion(new Version(0, 9, 4, 0))));
            Assert.Equal("1.2", ProductInfo.ShortVersion(new Version(1, 2)));
            Assert.Equal("1.2.3.4", ProductInfo.ShortVersion(new Version(1, 2, 3, 4)));
            Assert.Equal("Agent to AutoCAD Full", ProductInfo.WithVersion(" "));
            var ed = Src("bundle", "edition.json");
            Assert.Contains($"\"edition\": \"{ProductInfo.Edition}\"", ed);
            Assert.Contains($"\"repo\": \"{ProductInfo.Repo}\"", ed);
        }

        [Fact]
        public void The_technical_identity_is_the_original_ClaudeCAD()
        {
            var iss = Iss;
            Assert.Contains("AppId={{8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40}", iss);
            Assert.Contains(@"DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\ClaudeCAD.bundle", iss);
            Assert.Contains("OutputBaseFilename=ClaudeCAD-Setup", iss);
            var m = Manifest;
            Assert.Contains("ProductCode=\"{8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40}\"", m);
            Assert.Contains("UpgradeCode=\"{2A7F5C19-0B8E-4D63-A1F4-6C9E2B7D5A08}\"", m);
            // component names (the autoloader's registry keys) and commands unchanged
            Assert.Equal(new[] { "ClaudeCAD", "ClaudeCAD2025", "ClaudeCAD2027" },
                Regex.Matches(m, "AppName=\"([^\"]+)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToArray());
            foreach (var c in new[] { "CLAUDECHAT", "CLAUDECHATRESTART", "CLAUDECHATUPDATE", "CLAUDECHATLOG" })
                Assert.Equal(3, Regex.Matches(m, $"Global=\"{c}\"").Count);
            Assert.Contains("GroupName=\"CLAUDECAD\"", m);
        }

        [Fact]
        public void The_names_people_read_are_Agent_to_AutoCAD_Full()
        {
            var iss = Iss;
            Assert.Contains($"#define AppDisplayName \"{ProductInfo.DisplayName}\"", iss);
            Assert.Contains("AppName={#AppDisplayName}", iss);
            Assert.Contains("AppVerName={#AppDisplayName} {#AppVersion}", iss);
            Assert.Contains("UninstallDisplayName={#AppDisplayName}", iss);
            Assert.Contains("'Kích hoạt {#AppDisplayName}'", iss);
            Assert.Contains("FinishedLabel={#AppDisplayName} đã được cài", iss);
            Assert.Contains($"Name=\"{ProductInfo.DisplayName}\"", Manifest);
            var plugin = Path.Combine("plugin", "ClaudeCAD.Plugin");
            Assert.Contains("new PaletteSet(Core.ProductInfo.DisplayName, \"CLAUDECHAT\", PaletteGuid)", Src(plugin, "UI", "ChatPalette.cs"));
            Assert.Contains("ProductInfo.WithVersion(ProductInfo.ShortVersion(typeof(ChatControl).Assembly.GetName().Version))", Src(plugin, "UI", "ChatControl.cs"));
            Assert.Contains("sb.AppendLine(ProductInfo.DisplayName + \" — tự kiểm tra \"", Src(plugin, "SelfTest.cs"));
            Assert.Contains("ProductInfo.DisplayName + \" — báo cáo lỗi \"", Src(plugin, "DiagnosticsBundle.cs"));
            // the bundle carries the edition mark
            Assert.Contains("path.join(contents, 'edition.json')", Src("bundle", "make-bundle.mjs"));
        }

        [Fact]
        public void The_installer_applies_the_same_rules_as_EditionLogic()
        {
            var iss = Iss;
            var detect = Body(iss, "function DetectEdition(): String;");
            Assert.Contains(@"'\Contents\" + EditionLogic.EditionFile + "'", detect);
            Assert.Contains(@"'\Contents\" + EditionLogic.UpdateConfigFile + "'", detect);
            Assert.Contains("'" + EditionLogic.EditionKeyMarker + "'", detect);
            Assert.Contains("'" + EditionLogic.FullEditionMarker + "'", detect);
            Assert.Contains("'" + EditionLogic.OriginalRepoMarker + "'", detect);
            Assert.Contains("'" + EditionLogic.FullRepoMarker + "'", detect);
            // the original's marker is checked before the Full one, like EditionLogic.Detect
            Assert.True(detect.IndexOf("'" + EditionLogic.OriginalRepoMarker + "'", StringComparison.Ordinal) < detect.IndexOf("'" + EditionLogic.FullRepoMarker + "'", StringComparison.Ordinal));
            // same squeeze: lower case, no spaces / tabs / line breaks
            var squeeze = Body(iss, "function ReadSqueezed(");
            foreach (var x in new[] { "Lowercase(", "' '", "#9", "#13", "#10" }) Assert.Contains(x, squeeze);
            Assert.Contains("Pos('AppVersion=\"', Text)", Body(iss, "function ExistingAppVersion(): String;"));
        }

        [Fact]
        public void The_installer_warns_once_asks_nothing_when_silent_and_asks_for_activation_again()
        {
            var iss = Iss;
            var confirm = Body(iss, "function ConfirmReplaceOriginal(): Boolean;");
            int silent = confirm.IndexOf("if WizardSilent() then", StringComparison.Ordinal), box = confirm.IndexOf("MsgBox(", StringComparison.Ordinal);
            Assert.True(silent > 0 && silent < box, "a silent install must not show the question");
            foreach (var t in new[] { "bản gốc, chỉ 2D", "phiên bản ' + ExistingVersion", "Lịch sử chat, cài đặt và đăng nhập Claude được giữ", "Key cũ không dùng được", "kích hoạt lại", "chọn gói", "OK để tiếp tục", "Cancel để hủy", "MB_OKCANCEL) = IDOK" })
                Assert.Contains(t, confirm);
            Assert.Contains("Log('silent install: replacing '", confirm);
            var init = Body(iss, "function InitializeSetup(): Boolean;");
            Assert.True(init.IndexOf("ConfirmReplaceOriginal()", StringComparison.Ordinal) < init.IndexOf("#ifdef BundledNode", StringComparison.Ordinal),
                "the check must run before the bundled-Node exit");
            // the original's licence is not this edition's: the key page asks the owner again
            Assert.Contains("if ReplacesOriginal() then exit;", Body(iss, "function HasLicense(): Boolean;"));
            // the original's staged self-update cannot come back over the Full edition when AutoCAD closes
            Assert.Contains("if (CurStep = ssInstall) and ReplacesOriginal() then DropOriginalStagedUpdate();", Body(iss, "procedure CurStepChanged(CurStep: TSetupStep);"));
            Assert.Contains(@"ExpandConstant('{localappdata}\ClaudeCAD\updates\stage')", Body(iss, "procedure DropOriginalStagedUpdate();"));
            // the folder the stage lives in is the one UpdateService uses
            Assert.Contains("\"ClaudeCAD\", \"updates\"", Src("plugin", "ClaudeCAD.Plugin", "Sidecar", "UpdateService.cs"));
            Assert.Contains("Path.Combine(StateDir, \"stage\")", Src("plugin", "ClaudeCAD.Plugin", "Sidecar", "UpdateService.cs"));
        }

        /// <summary>A Pascal routine from its header to the "end;" that closes it (the next line starting with "end;").</summary>
        private static string Body(string src, string header)
        {
            int a = src.IndexOf(header, StringComparison.Ordinal);
            Assert.True(a >= 0, "missing " + header);
            int b = src.IndexOf("\nend;", a, StringComparison.Ordinal);
            Assert.True(b > a, "no end of " + header);
            return src.Substring(a, b - a);
        }
    }
}
