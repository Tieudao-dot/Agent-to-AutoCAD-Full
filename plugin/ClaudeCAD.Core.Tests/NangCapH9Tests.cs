using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt H9 (26/11/2026, docs/ban-giao/nang-cap-h9.md): the palette in English. Vietnamese is the key and the
    /// default; every Vietnamese text a person sees has its English (these tests list the missing ones); texts drawn into
    /// the drawing stay Vietnamese (chủ dự án chốt 26/11/2026).
    /// </summary>
    [Collection("Lang")] // L.Lang is changed: never beside another test
    public class NangCapH9Tests
    {
        private static string RootDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RootDir }.Concat(parts).ToArray()));
        private static string Plugin => Path.Combine(RootDir, "plugin", "ClaudeCAD.Plugin");

        private static readonly Regex Vietnamese = new Regex("[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐ]", RegexOptions.IgnoreCase);
        private static readonly Regex Literal = new Regex("(?<pre>\\$?@?)\"(?<body>(?:[^\"\\\\\\n]|\\\\.)*)\"");
        private static readonly Regex TrCall = new Regex("\\bTr\\(\\s*\"(?<body>(?:[^\"\\\\]|\\\\.)*)\"");

        /// <summary>The files whose every Vietnamese text is shown through Tr (a line that must stay Vietnamese says "// vi").</summary>
        private static readonly string[] Translated =
        {
            "UI/ChatControl.cs", "UI/ChatControl.Lang.cs", "ChatController.cs", "UI/StatusTray.cs", "UI/MarkdownView.cs", "UI/ImageAttachment.cs",
            "PluginApp.cs", "Drawing/Interaction.cs",
            "Sidecar/Activation.cs", "Sidecar/SidecarManager.cs", "Sidecar/UpdateService.cs", "Sidecar/ClaudeAuth.cs", "Sidecar/ApiKeyStore.cs",
            "Core/License.cs", "Core/Plans.cs", "Core/SkillStore.cs", "Core/SkillSync.cs", "Core/ChatStore.cs", "Core/ChatExtras.cs",
            "Core/Macros.cs", "Core/QuickTemplates.cs", "Core/DrawingHistoryCore.cs", "Core/F5.cs", "Core/PaletteSettings.cs",
        };

        private static string Unescape(string body)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '\\' && i + 1 < body.Length)
                {
                    char n = body[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '0': sb.Append('\0'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(body.Substring(i + 1, 4), 16)); i += 4; break;
                        default: sb.Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static IEnumerable<string> PluginFiles() =>
            Directory.GetFiles(Plugin, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(f => !f.EndsWith("LangEn.cs", StringComparison.Ordinal));

        private static IEnumerable<string> CodeLines(string file)
        {
            foreach (var raw in File.ReadAllLines(file))
            {
                var t = raw.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal)) continue;
                yield return raw;
            }
        }

        // ------------------------------------------------------------ "có test bắt chuỗi thiếu bản dịch"

        [Fact]
        public void Every_text_given_to_Tr_has_its_English()
        {
            var missing = new List<string>();
            foreach (var f in PluginFiles())
                foreach (Match m in TrCall.Matches(File.ReadAllText(f)))
                {
                    var key = Unescape(m.Groups["body"].Value);
                    if (Vietnamese.IsMatch(key) && !L.HasEnglish(key)) missing.Add(Path.GetFileName(f) + ": " + key);
                }
            Assert.True(missing.Count == 0, "Chưa có bản tiếng Anh (thêm vào Core/LangEn.cs):\n" + string.Join("\n", missing.Distinct()));
        }

        [Fact]
        public void In_the_palette_files_no_vietnamese_text_is_shown_without_Tr()
        {
            var bare = new List<string>();
            foreach (var rel in Translated)
            {
                var file = Path.Combine(Plugin, rel.Replace('/', Path.DirectorySeparatorChar));
                foreach (var line in CodeLines(file))
                {
                    if (line.Contains("// vi") || line.Contains("Logger.")) continue;
                    int cut = line.IndexOf(" // ", StringComparison.Ordinal);
                    var code = cut >= 0 ? line.Substring(0, cut) : line;
                    foreach (Match m in Literal.Matches(code))
                    {
                        if (!Vietnamese.IsMatch(m.Groups["body"].Value)) continue;
                        var before = code.Substring(0, m.Index);
                        if (Regex.IsMatch(before, "\\bTr\\(\\s*$")) continue;
                        bare.Add(rel + ": " + line.Trim());
                    }
                }
            }
            Assert.True(bare.Count == 0, "Chữ tiếng Việt chưa qua Tr (bọc Tr(...) hoặc ghi \"// vi\" nếu phải giữ):\n" + string.Join("\n", bare));
        }

        [Fact]
        public void Every_tool_label_has_its_English()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            var labels = Regex.Matches(src, "\\[\"[a-z0-9_]+\"\\]\\s*=\\s*\"(?<v>[^\"]*)\"").Cast<Match>().Select(m => m.Groups["v"].Value).ToList();
            Assert.True(labels.Count > 140);
            Assert.Empty(labels.Where(l => !L.HasEnglish(l)));
            Assert.Contains("Core.L.Tr(l)", src);
        }

        [Fact]
        public void Each_english_text_keeps_the_blanks_and_the_edge_spaces_of_its_vietnamese()
        {
            var rx = new Regex("\\{\\d+(:[^}]*)?\\}");
            foreach (var k in L.Keys)
            {
                var e = L.English(k);
                Assert.False(string.IsNullOrWhiteSpace(e), k);
                Assert.Equal(rx.Matches(k).Cast<Match>().Select(m => m.Value).OrderBy(x => x), rx.Matches(e).Cast<Match>().Select(m => m.Value).OrderBy(x => x));
                Assert.Equal(k.StartsWith(" ", StringComparison.Ordinal), e.StartsWith(" ", StringComparison.Ordinal));
                Assert.Equal(k.EndsWith(" ", StringComparison.Ordinal), e.EndsWith(" ", StringComparison.Ordinal));
                Assert.DoesNotContain("cậu", e);
            }
            Assert.True(L.Keys.Count() > 900);
        }

        // ------------------------------------------------------------ the switch

        [Fact]
        public void Vietnamese_is_the_default_and_english_is_one_switch_away()
        {
            try
            {
                Assert.Equal(L.Vi, L.Lang);
                Assert.Equal("Đồng ý", L.Tr("Đồng ý"));
                Assert.Equal("Bước 2/5: vẽ tường", L.Tr("Bước {0}/{1}: {2}", 2, 5, "vẽ tường"));
                int changed = 0;
                Action on = () => changed++;
                L.Changed += on;
                L.Lang = "EN";
                L.Lang = "en"; // no second event
                L.Changed -= on;
                Assert.Equal(1, changed);
                Assert.True(L.IsEnglish);
                Assert.Equal("Yes", L.Tr("Đồng ý"));
                Assert.Equal("Step 2/5: vẽ tường", L.Tr("Bước {0}/{1}: {2}", 2, 5, "vẽ tường"));
                Assert.Equal("chữ chưa có bản dịch", L.Tr("chữ chưa có bản dịch")); // falls back to the Vietnamese
                Assert.Equal("Both", WorkModeLogic.Label(WorkModeLogic.Both));
                Assert.Equal("Architecture", PaletteSettings.StandardLabel("ve-kien-truc"));
                Assert.Equal("Không đủ {0} {1}", L.Tr("Không đủ {0} {1}", "x")); // a missing argument never throws
            }
            finally
            {
                L.Lang = L.Vi;
            }
            Assert.Equal("Cả hai", WorkModeLogic.Label(WorkModeLogic.Both));
            Assert.Equal(L.Vi, L.Normalize("fr"));
            Assert.Equal("Tiếng Việt", L.Label(L.Vi));
            Assert.Equal("English", L.Label(L.En));
        }

        [Fact]
        public void A_shown_text_swaps_both_ways_and_an_unknown_one_is_left()
        {
            Assert.Equal("Send", L.Retranslate("Gửi", L.Vi, L.En));
            Assert.Equal("Gửi", L.Retranslate("Send", L.En, L.Vi));
            Assert.Null(L.Retranslate("Gửi", L.Vi, L.Vi));
            Assert.Null(L.Retranslate("tên layer A-WALL", L.Vi, L.En)); // the user's own words, a layer name…
            Assert.Null(L.Retranslate("", L.Vi, L.En));
            var relabel = Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatControl.Lang.cs");
            Assert.Contains("case TextBox _:", relabel); // what the user typed is never touched
            foreach (var popup in new[] { "_settingsPopup", "_skillsPopup", "_macrosPopup", "_mentionPopup", "_slashPopup", "_modeMenu", "_notesPanel" })
                Assert.Contains(popup, relabel);
        }

        [Fact]
        public void The_language_is_kept_in_the_settings()
        {
            var s = new PaletteSettings();
            Assert.Equal(L.Vi, s.Language);
            Assert.Contains("\"language\":\"vi\"", s.ToJson().Replace(" ", ""));
            s.Language = "en";
            Assert.Equal(L.En, PaletteSettings.FromJson(s.ToJson()).Language);
            Assert.Equal(L.Vi, PaletteSettings.FromJson("{\"language\":\"de\"}").Language);
            Assert.Equal(L.Vi, PaletteSettings.FromJson("{}").Language); // a settings file of an older version
        }

        [Fact]
        public void The_palette_applies_the_language_at_once_and_tells_the_assistant()
        {
            var ctl = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("if (_firstRun) _settings.Language = InstallerLanguage();", ctl);
            Assert.Contains("L.Lang = _settings.Language;", ctl);
            Assert.Contains("if (oldLang != _settings.Language) OnLanguageChanged(oldLang, _settings.Language);", ctl);
            Assert.Contains("Ui.Relabel(from, to);", ctl);
            Assert.Contains("Ui.LoadTranscript(_transcript, false);", ctl);
            Assert.Contains("if (L.IsEnglish) ctx[\"language\"] = L.En;", ctl); // Vietnamese sends nothing: an older assistant is fine
            Assert.Contains("Path.Combine(dir, \"language.txt\")", ctl);
            var ui = Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatControl.cs");
            Assert.Contains("_language = MakeCombo(L.Languages.Select(L.Label).ToArray(), double.NaN);", ui);
            Assert.Contains("_settings.Language = L.Languages[Math.Max(0, _language.SelectedIndex)];", ui);
            // the welcome card's ids stay Vietnamese, only their display goes through Tr
            Assert.Contains("var act = Find(\"Kích hoạt\");", ui);
            Assert.Contains("Tr(name) + (", ui);
            Assert.Contains("SetInputText(Tr(WelcomeExample))", ui);
        }

        // ------------------------------------------------------------ texts drawn into the drawing stay Vietnamese

        [Fact]
        public void Texts_drawn_into_the_drawing_and_the_excel_heads_stay_vietnamese()
        {
            foreach (var rel in new[] { "Core/DrawingSet.cs", "Core/Xlsx.cs", "Core/Standards2D.cs", "Core/Terrain.cs", "Core/Rebar.cs", "Core/Mep.cs", "Core/StairRoof.cs", "Core/Estimate.cs", "Drawing/StructureTools.cs", "Drawing/Util2DTools.cs" })
            {
                var src = File.ReadAllText(Path.Combine(Plugin, rel.Replace('/', Path.DirectorySeparatorChar)));
                Assert.DoesNotMatch("\\bTr\\(", src);
            }
            Assert.Contains("\"MẶT BẰNG\"", Src("plugin", "ClaudeCAD.Plugin", "Core", "DrawingSet.cs"));
            Assert.Contains("\"BẢNG THỐNG KÊ THÉP\"", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StructureTools.cs"));
            var doc = Src("bo-cai", "README.md");
            Assert.Contains("English chỉ đổi giao diện và câu trả lời, chữ trên bản vẽ giữ tiếng Việt", doc);
        }

        // ------------------------------------------------------------ installer

        [Fact]
        public void The_installer_asks_the_language_and_hands_it_to_the_plugin()
        {
            var iss = Src("installer", "ClaudeCAD.iss").Replace("\r\n", "\n"); // a Windows checkout has CRLF
            Assert.Contains("Name: \"vietnamese\"; MessagesFile: \"compiler:Default.isl\"", iss);
            Assert.Contains("Name: \"english\"; MessagesFile: \"compiler:Default.isl\"", iss);
            Assert.True(iss.IndexOf("Name: \"vietnamese\"", StringComparison.Ordinal) < iss.IndexOf("Name: \"english\"", StringComparison.Ordinal)); // Vietnamese first = default
            Assert.Contains("ShowLanguageDialog=yes", iss);
            Assert.Contains("vietnamese.LanguageName=Tiếng Việt", iss);
            Assert.Contains("SaveStringToFile(ExpandConstant('{app}\\Contents\\language.txt'), Lang, False)", iss);
            Assert.Contains("Description: \"{cm:H9LoginNow}\"", iss);
            // every message the code uses exists in both languages
            var used = Regex.Matches(iss, "CustomMessage\\('(?<n>H9Msg\\d+)'\\)").Cast<Match>().Select(m => m.Groups["n"].Value).Distinct().ToList();
            Assert.True(used.Count > 50);
            foreach (var n in used.Concat(new[] { "H9LoginNow" }))
            {
                Assert.Contains("\nvietnamese." + n + "=", iss);
                Assert.Contains("\nenglish." + n + "=", iss);
            }
            Assert.Contains("\nenglish.FinishedLabel=", iss);
            Assert.Contains("\nvietnamese.FinishedLabel=", iss);
            // no Vietnamese text is left in a Pascal string of the code (it would show in English too)
            var code = iss.Substring(iss.IndexOf("\n[Code]\n", StringComparison.Ordinal));
            var pas = Regex.Matches(code, "'(?:[^'\\n]|'')*'").Cast<Match>().Select(m => m.Value).Where(v => Vietnamese.IsMatch(v)).ToList();
            Assert.True(pas.Count == 0, string.Join("\n", pas));
        }
    }

    /// <summary>
    /// Nâng cấp đợt H9: the installer's texts moved from the Pascal code to [CustomMessages] (vietnamese.H9MsgNN /
    /// english.H9MsgNN); the code reads them with CustomMessage('H9MsgNN'). Older tests look a Vietnamese text up here.
    /// </summary>
    public static class InnoMessages
    {
        /// <summary>CustomMessage('…') of the Vietnamese message equal to (else containing) the text.</summary>
        public static string Cm(string iss, string vi)
        {
            var lines = iss.Split('\n').Where(l => l.StartsWith("vietnamese.H9Msg", StringComparison.Ordinal)).ToList();
            var hit = lines.FirstOrDefault(l => l.Substring(l.IndexOf('=') + 1).TrimEnd('\r') == vi) ?? lines.FirstOrDefault(l => l.Contains(vi));
            Assert.True(hit != null, "no installer message with: " + vi);
            return "CustomMessage('" + hit.Substring("vietnamese.".Length, hit.IndexOf('=') - "vietnamese.".Length) + "')";
        }
    }

    [CollectionDefinition("Lang", DisableParallelization = true)]
    public class LangCollection { }
}
