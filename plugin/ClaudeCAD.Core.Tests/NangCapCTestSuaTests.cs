using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c-test.md): the fixes NC-1 (find &amp; replace never cuts
    /// MTEXT / %% codes, the replacement is escaped in MTEXT), NC-2 (constant attributes in block_schedule), NC-3 (each
    /// layout current while its page is plotted), NC-4 (relative / bad output of plot_pdf), NC-5 (dim_floor_plan in metre
    /// drawings), NC-6 (polyline closed by its ends in area_schedule), plus edge cases the first tests did not cover.
    /// </summary>
    public class NangCapCTestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Util2DTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
        private static Util2DLogic.Seg S(double x1, double y1, double x2, double y2) => new Util2DLogic.Seg(x1, y1, x2, y2);

        // ------------------------------------------------------------ NC-1: formatting codes

        [Fact]
        public void Mtext_line_break_is_not_hit_by_a_find_of_P()
        {
            var after = Util2DLogic.ReplaceFormatted("PHONG\\PNGU", "P", "X", true, false, true, out int n);
            Assert.Equal("XHONG\\PNGU", after);
            Assert.Equal(1, n);
            // the old plain Replace would have broken the line break
            Assert.Equal("XHONG\\XNGU", Util2DLogic.Replace("PHONG\\PNGU", "P", "X", true, false, out _));
        }

        [Fact]
        public void Mtext_codes_with_arguments_and_groups_stay_whole()
        {
            const string s = "{\\fArial|b1|i0;\\C1;\\H2.5x;Arial C1} cao \\U+00D8";
            var after = Util2DLogic.ReplaceFormatted(s, "Arial", "Times", false, false, true, out int n);
            Assert.Equal("{\\fArial|b1|i0;\\C1;\\H2.5x;Times C1} cao \\U+00D8", after);
            Assert.Equal(1, n);
            Assert.Equal(s, Util2DLogic.ReplaceFormatted(s, "C1", "C2", true, true, true, out _).Replace("Arial C2", "Arial C1"));
            Assert.Equal(s, Util2DLogic.ReplaceFormatted(s, "00D8", "x", false, false, true, out int none));
            Assert.Equal(0, none);
        }

        [Fact]
        public void Percent_codes_of_TEXT_are_kept_unless_the_find_holds_them()
        {
            Assert.Equal("%%c100 X", Util2DLogic.ReplaceFormatted("%%c100 c", "c", "X", false, false, false, out int n));
            Assert.Equal(1, n);
            Assert.Equal("%%132 X", Util2DLogic.ReplaceFormatted("%%132 1", "1", "X", false, false, false, out _));
            // replacing the diameter code itself works
            Assert.Equal("Ø100", Util2DLogic.ReplaceFormatted("%%c100", "%%c", "Ø", false, false, false, out int m));
            Assert.Equal(1, m);
        }

        [Fact]
        public void Replacement_is_escaped_in_mtext_only()
        {
            Assert.Equal("D:\\\\BV\\{1\\}\\PB", Util2DLogic.ReplaceFormatted("A", "A", "D:\\BV{1}\nB", true, false, true, out _));
            Assert.Equal("D:\\BV{1}", Util2DLogic.ReplaceFormatted("A", "A", "D:\\BV{1}", true, false, false, out _));
            Assert.Equal("", Util2DLogic.EscapeMText(null));
        }

        [Fact]
        public void Segments_rebuild_the_text_exactly()
        {
            foreach (var s in new[] { "", "abc", "\\P", "{\\C1;x}", "\\fArial", "a\\", "%%", "%%c", "%%12", "\\U+", "x\\M+1234" })
            {
                Assert.Equal(s, string.Concat(Util2DLogic.FormatSegments(s, true).Select(t => t.Key)));
                Assert.Equal(s, string.Concat(Util2DLogic.FormatSegments(s, false).Select(t => t.Key)));
            }
        }

        [Fact]
        public void Whole_word_and_case_still_apply_inside_runs()
        {
            Assert.Equal("{\\C1;WC} WC1", Util2DLogic.ReplaceFormatted("{\\C1;wc} wc1", "wc", "WC", false, false, true, out int n));
            Assert.Equal(2, n);
            Assert.Equal("{\\C1;WC} wc1", Util2DLogic.ReplaceFormatted("{\\C1;wc} wc1", "wc", "WC", false, true, true, out int w));
            Assert.Equal(1, w);
            Assert.Equal("wc", Util2DLogic.ReplaceFormatted("wc", "WC", "X", true, false, false, out int none));
            Assert.Equal(0, none);
        }

        [Fact]
        public void Plugin_replaces_through_the_code_safe_path()
        {
            var src = Util2DTools;
            Assert.DoesNotContain("Util2DLogic.Replace(", src);
            Assert.Contains("hits.Add(Hit(mt, \"MTEXT\", mt.Contents, true));", src);
            Assert.Contains("hits.Add(Hit(t, \"TEXT\", t.TextString, false));", src);
            Assert.Contains("hits.Add(Hit(dim, \"DIMENSION\", dim.DimensionText, fmt));", src);
            Assert.Contains("hits.Add(Hit(ml, \"MULTILEADER\", m.Contents, true));", src);
            Assert.Contains("ReplaceFormatted(s, find, repl, matchCase, wholeWord, ar.IsMTextAttribute, out int n)", src);
        }

        // ------------------------------------------------------------ NC-2: constant attributes

        [Fact]
        public void Block_schedule_reads_constant_attributes_of_the_definition()
        {
            var src = Util2DTools;
            Assert.Contains("ad.Constant", src);
            Assert.Contains("ConstantAttributes(ctx, br.BlockTableRecord, constants)", src);
            Assert.Contains("if (!it.Attributes.ContainsKey(kv.Key))", src);
        }

        [Fact]
        public void Constant_value_counts_like_a_normal_attribute()
        {
            var items = new[]
            {
                new Util2DLogic.BlockItem { Name = "CUA", Attributes = { ["MA"] = "D1" } },
                new Util2DLogic.BlockItem { Name = "CUA", Attributes = { ["ma"] = "D1" } },
                new Util2DLogic.BlockItem { Name = "CUA" },
            };
            var rows = Util2DLogic.CountBlocks(items, "Ma");
            Assert.Equal(2, rows.Single(r => r.Value == "D1").Count);
            Assert.Equal(1, rows.Single(r => r.Value == "").Count);
        }

        // ------------------------------------------------------------ NC-3 / NC-4: plot_pdf

        [Fact]
        public void Each_page_layout_is_current_while_it_plots()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs");
            int make = src.IndexOf("lm.CurrentLayout = layouts[i];", StringComparison.Ordinal);
            int page = src.IndexOf("pe.BeginPage(ppi, infos[i]", StringComparison.Ordinal);
            Assert.True(make > 0 && page > make, "the layout must be made current before BeginPage");
            // restored afterwards
            Assert.Contains("if (!lm.CurrentLayout.Equals(previousLayout, StringComparison.OrdinalIgnoreCase)) lm.CurrentLayout = previousLayout; } catch { }", src);
        }

        [Fact]
        public void Relative_output_goes_next_to_the_drawing_and_bad_names_are_refused()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs");
            Assert.Contains("Path.IsPathRooted(output) ? output : Path.Combine(drawingDir, output)", src);
            Assert.Contains("Util2DLogic.BadFileName(Path.GetFileName(full))", src);
        }

        [Theory]
        [InlineData("Nha.pdf", true)]
        [InlineData("Mặt bằng tầng 1.pdf", true)]
        [InlineData("A1?.pdf", false)]
        [InlineData("a|b.pdf", false)]
        [InlineData("CON.pdf", false)]
        [InlineData("nul.pdf", false)]
        [InlineData("x.pdf.", false)]
        [InlineData("", false)]
        [InlineData("tab\tx.pdf", false)]
        public void Bad_file_names(string name, bool ok)
        {
            Assert.Equal(ok, Util2DLogic.BadFileName(name) == null);
        }

        [Fact]
        public void Safe_layout_name_is_always_a_good_file_name()
        {
            foreach (var n in new[] { "A1: MB", "CON", "a/b\\c", "  ", "x.", "LPT1.x", "<>", new string('a', 300) })
                Assert.Null(Util2DLogic.BadFileName(Util2DLogic.SafeFileName(n) + ".pdf"));
        }

        [Fact]
        public void Layouts_all_keeps_tab_order_and_skips_model()
        {
            var tabs = new[] { "Model", "MB-2", "MB-1", "Model2" };
            Assert.Equal(new[] { "MB-2", "MB-1", "Model2" }, Util2DLogic.ResolveLayouts(new[] { " ALL " }, tabs));
            Assert.Equal(new[] { "MB-2", "MB-1", "Model2" }, Util2DLogic.ResolveLayouts(new[] { "*" }, tabs));
            // "all" among other names is a layout name, not the keyword
            Assert.Throws<ToolException>(() => Util2DLogic.ResolveLayouts(new[] { "all", "MB-1" }, tabs));
        }

        // ------------------------------------------------------------ NC-5: metre drawings

        [Fact]
        public void Metre_plan_keeps_a_door_of_0_9()
        {
            // 10 × 6 m house drawn in metres, door from 1.0 to 1.9 in the bottom wall
            var walls = new List<Util2DLogic.Seg> { S(0, 0, 1, 0), S(1.9, 0, 10, 0), S(10, 0, 10, 6), S(10, 6, 0, 6), S(0, 6, 0, 0) };
            double tol = Util2DLogic.PieceTolerance(10);
            Assert.True(tol <= 0.001);
            var dims = Util2DLogic.FloorPlanDims(walls, null, new[] { "bottom" }, 0.8, 0.6, tol);
            var detail = dims.Where(d => d.Row == "detail").Select(d => d.P2[0] - d.P1[0]).ToArray();
            Assert.Equal(3, detail.Length);
            Assert.Equal(0.9, detail[1], 6);
            // with the old fixed 1 unit the door would disappear
            Assert.DoesNotContain(Util2DLogic.FloorPlanDims(walls, null, new[] { "bottom" }, 0.8, 0.6), d => d.Row == "detail" && d.P2[0] - d.P1[0] < 1);
        }

        [Fact]
        public void Piece_tolerance_is_one_unit_for_mm_plans()
        {
            Assert.Equal(1, Util2DLogic.PieceTolerance(12000), 9);
            Assert.Equal(1e-6, Util2DLogic.PieceTolerance(0), 12);
            Assert.Contains("Util2DLogic.PieceTolerance(size)", Util2DTools);
        }

        [Fact]
        public void Door_at_the_corner_and_slanted_axis()
        {
            // door from the corner (0 → 900): the corner and the jamb are both stations
            var walls = new List<Util2DLogic.Seg> { S(900, 0, 8000, 0), S(8000, 0, 8000, 5000), S(8000, 5000, 0, 5000), S(0, 5000, 0, 0) };
            var axes = new List<Util2DLogic.Seg> { S(0, -500, 0, 5500), S(4000, -500, 4100, 5500), S(8000, -500, 8000, 5500) };
            var dims = Util2DLogic.FloorPlanDims(walls, axes, new[] { "bottom" }, 800, 600);
            Assert.Equal(new[] { 0.0, 900 }, dims.Where(d => d.Row == "detail").Select(d => d.P1[0]).ToArray());
            // the slanted axis is not a vertical axis: the axis row has only the two outer axes = the overall row, so it is dropped
            Assert.DoesNotContain(dims, d => d.Row == "axes");
            Assert.Single(dims, d => d.Row == "overall");
        }

        // ------------------------------------------------------------ NC-6: polyline closed by its ends

        [Fact]
        public void Area_schedule_takes_polylines_closed_by_their_ends()
        {
            var src = Util2DTools;
            Assert.Contains("|| ClosedByEnds(c):", src);
            Assert.Contains("pl.NumberOfVertices > 3", src);
            Assert.Contains("if (nv > 3 && !pl.Closed", src);
        }

        [Fact]
        public void Hatch_twin_of_an_l_room_is_counted_once_and_names_go_to_the_inner_room()
        {
            var l = new List<double[]> { new[] { 0.0, 0 }, new[] { 6000.0, 0 }, new[] { 6000.0, 3000 }, new[] { 3000.0, 3000 }, new[] { 3000.0, 6000 }, new[] { 0.0, 6000 } };
            double area = 6000.0 * 3000 + 3000.0 * 3000;
            var items = new List<Util2DLogic.AreaItem>
            {
                new Util2DLogic.AreaItem { Handle = "A1", Layer = "PHONG", Area = area, Outline = l },
                new Util2DLogic.AreaItem { Handle = "H1", Type = "HATCH", Layer = "PHONG", Area = area, Outline = l.Select(p => (double[])p.Clone()).ToList() },
            };
            var skipped = new List<string>();
            var kept = Util2DLogic.Dedupe(items, skipped);
            Assert.Single(kept);
            Assert.Single(skipped);
            // text in the notch (outside the L) does not name the room; text in the leg does
            var texts = new List<Util2DLogic.TextItem>
            {
                new Util2DLogic.TextItem { Text = "SAN", Position = new[] { 4500.0, 4500 } },
                new Util2DLogic.TextItem { Text = "P.KHACH", Position = new[] { 1500.0, 4500 } },
            };
            Assert.Equal("P.KHACH", Util2DLogic.RoomNames(kept, texts)["A1"]);
        }

        [Fact]
        public void Area_table_total_and_decimal_comma()
        {
            var rows = new List<Util2DLogic.AreaRow>
            {
                new Util2DLogic.AreaRow { Name = "P1", Layer = "PHONG", Count = 1, Area = 12_345_678 },
                new Util2DLogic.AreaRow { Name = "", Layer = "PHONG", Count = 1, Area = 1_000_000 },
            };
            var t = Util2DLogic.AreaTable(rows, "room", 2);
            Assert.Equal("12,35", t[1][3]);
            Assert.Equal("(chưa có tên)", t[2][1]);
            Assert.Equal("13,35", t[3][3]);
        }

        // ------------------------------------------------------------ find & replace gate (plugin side)

        [Fact]
        public void Replace_card_check_comes_before_any_write()
        {
            var src = Util2DTools;
            int check = src.IndexOf("Util2DLogic.NotApproved(hs, approved)", StringComparison.Ordinal);
            int write = src.IndexOf("WriteText(w, hit);", StringComparison.Ordinal);
            Assert.True(check > 0 && write > check);
            Assert.Equal(new[] { "1A2", "3B" }, Util2DLogic.NotApproved(new[] { "1a2", "3B", "1A2", "4C", " " }, new HashSet<string> { "4C" }));
            Assert.Equal(new[] { "4C" }, Util2DLogic.NotApproved(new[] { "4c" }, null));
        }
    }
}
