using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa KTH phần A (26/11/2026, docs/ban-giao/nang-cap-h-sua-kth-a.md): KTH-09 → 14 (H4) and KTH-27 of the independent check
    /// of đợt H phần 1 (docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md).
    /// </summary>
    public class SuaKthATests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "claudecad-ktha-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompanyStandardTools.cs");

        // ------------------------------------------------------------ KTH-09

        [Fact]
        public void Autocad_default_styles_are_the_companys_only_when_the_template_uses_them()
        {
            var none = new HashSet<string>();
            Assert.False(CompanyStandardLogic.KeepStyle("Standard", CompanyStandardLogic.DefaultTextStyles, none, "ABC-ARIAL"));
            Assert.False(CompanyStandardLogic.KeepStyle("ISO-25", CompanyStandardLogic.DefaultDimStyles, none, "ABC-100"));
            Assert.False(CompanyStandardLogic.KeepStyle("annotative", CompanyStandardLogic.DefaultMLeaderStyles, none, ""));
            Assert.True(CompanyStandardLogic.KeepStyle("Standard", CompanyStandardLogic.DefaultTextStyles, none, "standard")); // current
            Assert.True(CompanyStandardLogic.KeepStyle("Standard", CompanyStandardLogic.DefaultTextStyles, new HashSet<string> { "STANDARD" }, "x")); // used
            Assert.True(CompanyStandardLogic.KeepStyle("ABC-ARIAL", CompanyStandardLogic.DefaultTextStyles, none, "")); // the company's own
            var t = Tools;
            Assert.Contains("void Uses(Entity e)", t);
            Assert.Contains("foreach (var d in keptDims) usedText.Add(d.TextStyle);", t); // a kept dimension style keeps its text style
            // the check then reports text in "Standard"
            var s = new CompanyStandard { TextStyles = { new CompanyStandard.TextStyle { Name = "ABC-ARIAL" } } };
            Assert.False(CompanyStandardLogic.HasTextStyle(s, "Standard"));
        }

        // ------------------------------------------------------------ KTH-10

        [Fact]
        public void Layers_that_differ_are_asked_fifty_at_a_time()
        {
            var t = Tools;
            Assert.Contains("result[\"kept_handles\"] = kept.Take(perCall).ToList();", t);
            Assert.Contains("const int perCall = 50;", t);
            Assert.Contains("result[\"kept_more\"]", t);
            // the same 50 as the card's handles and the schema of setup_standards
            Assert.Contains("p.Handles(\"handles\", 50)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs"));
        }

        // ------------------------------------------------------------ KTH-11

        [Fact]
        public void A_company_sheet_reports_its_own_fields_as_written()
        {
            var st = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");
            Assert.Contains("if (cDef == null) result[\"title_fields\"]", st);
            Assert.Contains("result[\"filled\"] = atts.Where(", st);
            Assert.Contains("result[\"unknown_attributes\"]", st);
            Assert.Contains("result[\"fields_note\"]", st);
        }

        // ------------------------------------------------------------ KTH-12

        [Fact]
        public void The_frame_and_viewport_layers_of_a_company_sheet_are_not_reported()
        {
            var s = new CompanyStandard { Layers = { new CompanyStandard.Layer { Name = "ABC-TUONG" } } };
            Assert.True(CompanyStandardLogic.HasLayer(s, F2Numbers.FrameLayer));
            Assert.True(CompanyStandardLogic.HasLayer(s, "khung-vp"));
            Assert.False(CompanyStandardLogic.HasLayer(s, "A-WALL"));
        }

        // ------------------------------------------------------------ KTH-13

        [Fact]
        public void Names_without_latin_letters_get_their_own_folder()
        {
            Assert.NotEqual(CompanyStandardLogic.Slug("株式会社"), CompanyStandardLogic.Slug("東京建築"));
            Assert.StartsWith("chuan-", CompanyStandardLogic.Slug("株式会社"));
            Assert.Equal(CompanyStandardLogic.Slug("株式会社"), CompanyStandardLogic.Slug("株式会社 "));
            Assert.Equal("cong-ty-abc", CompanyStandardLogic.Slug("Công ty ABC")); // Vietnamese names keep their folder
            Assert.Equal("chuan", CompanyStandardLogic.Slug("!!!"));
        }

        [Fact]
        public void A_name_sharing_the_folder_of_another_standard_is_refused_not_used_in_its_place()
        {
            CompanyStandardLogic.Save(_root, new CompanyStandard { Name = "C++" });
            Assert.Equal(CompanyStandardLogic.Slug("C++"), CompanyStandardLogic.Slug("C#"));
            var ex = Assert.Throws<ToolException>(() => CompanyStandardLogic.Require(_root, "C#", "check_drawing"));
            Assert.Contains("'C++'", ex.Message);
            Assert.StartsWith("check_drawing:", ex.Message);
            Assert.Equal("C++", CompanyStandardLogic.Require(_root, "c++", "check_drawing").Name); // the same name in other case
            CompanyStandardLogic.Save(_root, new CompanyStandard { Name = "Công ty ABC" });
            Assert.Equal("Công ty ABC", CompanyStandardLogic.Load(_root, "CONG-TY abc").Name);
            Assert.True(CompanyStandardLogic.SameName("Đ&Đ", "đ&đ"));
            Assert.False(CompanyStandardLogic.SameName("Đ&Đ", "D D"));
            // learning under the other name is refused too (before: replaced the other company's standard after the card)
            Assert.Contains("var before = damagedBefore ? null : CompanyStandardLogic.Load(root, name);", Tools);
        }

        // ------------------------------------------------------------ KTH-14

        [Fact]
        public void The_fixed_height_of_a_text_style_made_without_the_template_copy_follows_the_units()
        {
            var t = Tools;
            Assert.Contains("double hk = s.MmPerUnit > 0 ? s.MmPerUnit / MmPerUnitOf(ctx, out _) : 1;", t);
            Assert.Contains("[\"height\"] = t.Height * hk", t);
        }

        // ------------------------------------------------------------ nghi vấn H4

        [Fact]
        public void The_layouts_own_viewport_and_the_title_block_are_found_like_the_drawing_check()
        {
            var t = Tools;
            Assert.Contains("lo.GetViewports()", t);
            Assert.Contains("vp.Width >= w * 0.98 && vp.Height >= h * 0.98", t);
            Assert.Contains("OrderByDescending(x => x.t.Tags.Count)", t);
        }

        // ------------------------------------------------------------ KTH-27

        [Fact]
        public void The_prompt_lets_the_file_tools_touch_other_files()
        {
            var sp = Src("agent", "src", "agent", "systemPrompt.ts");
            Assert.DoesNotContain("Never touch files other than the open drawing.\n", sp.Replace("\r\n", "\n"));
            Assert.Contains("except through the tools made for it when the user asks (export_drawing, compare_drawings, and on 2D the company standard and many-files tools", sp);
            // the 3D prompt names no 2D-only tool (crossplan test): company_standard / batch_files are said in words here
        }
    }
}
