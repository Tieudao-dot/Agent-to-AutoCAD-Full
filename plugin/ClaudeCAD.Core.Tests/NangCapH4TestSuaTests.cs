using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt H4 (26/11/2026, docs/ban-giao/nang-cap-h4-test.md): the fixes of the review of company_standard and the
    /// cases the H4 tests did not cover (damaged store, device names, the copy of an open template, layers that differ,
    /// the company title block on another paper, fix_drawing after a company check).
    /// </summary>
    public class NangCapH4TestSuaTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "claudecad-h4t-" + Guid.NewGuid().ToString("N"));

        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompanyStandardTools.cs");

        private static CompanyStandard Std(string name) => new CompanyStandard
        {
            Name = name, Source = @"C:\mau\abc.dwt", Layers = { new CompanyStandard.Layer { Name = "ABC-TƯỜNG", Color = "1" } },
        };

        private void Damage(string name)
        {
            CompanyStandardLogic.Save(_root, Std(name));
            File.WriteAllText(Path.Combine(CompanyStandardLogic.FolderOf(_root, name), CompanyStandardLogic.DataFile), "{ \"name\": ");
        }

        // ------------------------------------------------------------ H4T-1: a damaged standard.json

        [Fact]
        public void A_damaged_standard_is_a_clear_refusal_not_a_plugin_error()
        {
            Damage("Công ty ABC");
            Assert.True(CompanyStandardLogic.IsDamaged(_root, "cong ty abc"));
            var ex = Assert.Throws<ToolException>(() => CompanyStandardLogic.Require(_root, "Công ty ABC", "check_drawing"));
            Assert.StartsWith("check_drawing:", ex.Message);
            Assert.Contains("damaged", ex.Message);
            Assert.Contains("learn", ex.Message);
            Assert.Throws<ToolException>(() => CompanyStandardLogic.Load(_root, "Công ty ABC"));
            Assert.False(CompanyStandardLogic.IsDamaged(_root, "khác"));
        }

        [Fact]
        public void The_list_names_a_damaged_standard_and_it_still_takes_a_place()
        {
            Damage("Hỏng");
            for (int i = 0; i < H4Numbers.MaxStandards - 1; i++) CompanyStandardLogic.Save(_root, Std("Cty " + i));
            var damaged = new List<string>();
            Assert.Equal(H4Numbers.MaxStandards - 1, CompanyStandardLogic.List(_root, damaged).Count);
            Assert.Equal(new[] { "hong" }, damaged);
            Assert.NotNull(CompanyStandardLogic.RoomRefusal(_root, "Cty mới")); // 19 good + 1 damaged = full
            Assert.Null(CompanyStandardLogic.RoomRefusal(_root, "HỎNG")); // learning it again is allowed
            // delete and learn of a damaged one go through the card like a good one
            var t = Tools;
            Assert.Contains("CompanyStandardLogic.IsDamaged(root, dname) ? dname", t);
            Assert.Contains("$\"replace the damaged company standard '{name}'\"", t);
            Assert.Contains("res[\"damaged\"]", t);
        }

        // ------------------------------------------------------------ H4T-2: names Windows keeps for devices

        [Theory]
        [InlineData("CON", "con-chuan")]
        [InlineData("nul", "nul-chuan")]
        [InlineData("Com1", "com1-chuan")]
        [InlineData("LPT9", "lpt9-chuan")]
        [InlineData("Công ty", "cong-ty")]
        [InlineData("console", "console")]
        [InlineData("..", "chuan")]
        [InlineData("a.. b", "a-b")]
        public void A_device_name_or_dots_never_become_the_folder(string name, string slug)
        {
            Assert.Equal(slug, CompanyStandardLogic.Slug(CompanyStandardLogic.ValidName(name)));
            var dir = Path.GetFullPath(CompanyStandardLogic.FolderOf(_root, name));
            Assert.Equal(Path.GetFullPath(_root), Path.GetDirectoryName(dir)); // always one level inside the store
        }

        // ------------------------------------------------------------ H4T-3: a template open elsewhere is still copied

        [Fact]
        public void The_copy_of_the_template_shares_the_file_with_the_program_that_has_it_open()
        {
            var t = Tools;
            Assert.Contains("CopyShared(od.Path, tmp)", t);
            Assert.Contains("FileShare.ReadWrite | FileShare.Delete", t);
            Assert.DoesNotContain("File.Copy(od.Path", t);
            // the copy of a template learnt before is removed when this one is not kept
            int big = t.IndexOf("more than {H4Numbers.MaxTemplateMb} MB", StringComparison.Ordinal);
            Assert.True(big > 0 && t.IndexOf("File.Delete(copy)", big, StringComparison.Ordinal) > big);
        }

        // ------------------------------------------------------------ H4T-4: a layer that differs is changed, not replaced

        [Fact]
        public void A_layer_that_differs_gets_the_properties_set_and_is_never_replaced_by_a_clone()
        {
            var t = Tools;
            Assert.DoesNotContain("ctx.Db.LayerTableId, true)", t);
            Assert.Contains("foreach (var name in replaceLayers) ApplyLayer(ctx", t);
            Assert.Contains("private static void ApplyLayer(ToolContext ctx, CompanyStandard.Layer l, List<object> notes)", t);
            Assert.Contains("LineWeight.ByLineWeightDefault", t); // a default lineweight of the standard is set back too
        }

        [Fact]
        public void A_template_in_other_units_says_its_dimension_styles_are_sized_for_them()
        {
            Assert.Contains("The template is in other units", Tools);
            // the text heights are converted (metre template, mm drawing: × 1000)
            var s = new CompanyStandard { MmPerUnit = 1000, TextHeights = { 0.25, 0.35 } };
            Assert.Equal(new[] { 250.0, 350.0 }, CompanyStandardLogic.Heights(s, 1).Select(h => Math.Round(h, 9)));
        }

        // ------------------------------------------------------------ H4T-5: create_sheet company

        [Fact]
        public void The_company_title_block_goes_on_the_frame_layer_and_another_paper_is_said()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");
            Assert.Contains("br.LayerId = frameLayer; // Test & sửa H4", t);
            Assert.DoesNotContain("if (cDef == null) br.LayerId = frameLayer;", t);
            Assert.Contains("it may sit off the frame", t);
        }

        [Fact]
        public void Paper_sizes_within_two_mm_are_named_and_odd_ones_are_not()
        {
            Assert.Equal(("A3", true), CompanyStandardLogic.PaperOf(421.5, 296));
            Assert.Equal(("A1", false), CompanyStandardLogic.PaperOf(594, 841));
            Assert.Null(CompanyStandardLogic.PaperOf(423, 297).name);
            Assert.Null(CompanyStandardLogic.PaperOf(279.4, 215.9).name); // Letter
        }

        // ------------------------------------------------------------ H4T-6: fix_drawing after a company check

        [Fact]
        public void Fix_drawing_never_applies_the_vietnamese_styles_after_a_company_check()
        {
            var ck = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingCheckTools.cs");
            Assert.Contains("if (p.Has(\"company\") && def.NeedsStandard)", ck);
            Assert.Contains("fix_drawing does not apply a company standard", ck);
            // the refusal comes before the scan (nothing is changed)
            int fix = ck.IndexOf("internal static object FixDrawing(", StringComparison.Ordinal);
            Assert.True(ck.IndexOf("if (p.Has(\"company\") && def.NeedsStandard)", fix, StringComparison.Ordinal) < ck.IndexOf("ScanDrawing(ctx", fix, StringComparison.Ordinal));
            // the only fixable group that needs the standard is nonstandard_styles
            Assert.Equal(new[] { "nonstandard_styles" }, DrawingCheckLogic.Groups.Where(g => g.Fixable && g.NeedsStandard).Select(g => g.Id));
        }

        // ------------------------------------------------------------ the store stays inside its folder

        [Fact]
        public void Delete_removes_only_the_folder_of_that_standard()
        {
            CompanyStandardLogic.Save(_root, Std("A"));
            CompanyStandardLogic.Save(_root, Std("B"));
            File.WriteAllText(Path.Combine(_root, "cua-khach.txt"), "x");
            Assert.True(CompanyStandardLogic.Delete(_root, "a"));
            Assert.False(CompanyStandardLogic.Delete(_root, "a"));
            Assert.NotNull(CompanyStandardLogic.Load(_root, "B"));
            Assert.True(File.Exists(Path.Combine(_root, "cua-khach.txt")));
        }

        [Fact]
        public void Vietnamese_layer_names_survive_the_json()
        {
            CompanyStandardLogic.Save(_root, Std("Việt"));
            Assert.Equal("ABC-TƯỜNG", CompanyStandardLogic.Load(_root, "viet").Layers.Single().Name);
        }
    }
}
