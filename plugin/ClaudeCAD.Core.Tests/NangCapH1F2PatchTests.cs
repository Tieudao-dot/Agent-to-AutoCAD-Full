using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt H1 (26/11/2026, docs/ban-giao/nang-cap-h1.md, part 4): the edge tests of the unsaved patch of the first
    /// F2 review thread (docs/ban-giao/f2-test-thread-con-ban-va.md) that the merged F2 review did not already have, run
    /// against the current code. Tests whose idea is already in NangCapF2Tests / NangCapF2TestSuaTests were left out.
    /// </summary>
    public class NangCapH1F2PatchTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");

        [Theory]
        [InlineData("1:99.6", 100)] // rounded to a whole denominator
        [InlineData("1:10000", 10000)]
        [InlineData("1 / 75", 75)]
        [InlineData("1", 1)]
        public void Scale_edge_strings(string s, int n) => Assert.Equal(n, StandardsLogic.ScaleDenominator(s));

        [Fact]
        public void Scale_edge_errors_name_the_tool()
        {
            foreach (var bad in new object[] { "1:10001", "1:", ":100", "1:100:2", "NaN", 0.00001 })
            {
                var ex = Assert.Throws<ToolException>(() => StandardsLogic.ScaleDenominator(bad, "create_sheet"));
                Assert.Contains("create_sheet", ex.Message);
            }
        }

        [Fact]
        public void Ltscale_is_only_set_while_it_is_the_default()
        {
            Assert.True(StandardsLogic.ShouldSetLtScale(1));
            Assert.False(StandardsLogic.ShouldSetLtScale(50));
            Assert.False(StandardsLogic.ShouldSetLtScale(1.0001));
            Assert.Equal(0.05, StandardsLogic.LtScale(100, 1000), 12);
            Assert.Equal(5, StandardsLogic.LtScale(100, 10), 12);
        }

        [Fact]
        public void A_changed_dim_style_lists_each_difference_once()
        {
            var wanted = StandardsLogic.DimStyleParams("VN", 100, 1);
            var have = new Dictionary<string, object>(wanted) { ["scale"] = 50.0, ["arrow"] = "closed_filled", ["text_style"] = "vn-arial" };
            var d = StandardsLogic.Differences(wanted, have);
            Assert.Equal(2, d.Count); // text style compares case-insensitively
            Assert.Contains(d, x => x.StartsWith("scale 50 → 100"));
            Assert.Contains(d, x => x.StartsWith("arrow closed_filled → archtick"));
            // a value a style does not report is not a difference
            Assert.Empty(StandardsLogic.Differences(wanted, new Dictionary<string, object>()));
            Assert.Empty(StandardsLogic.Differences(wanted, null));
        }

        [Fact]
        public void Fit_scale_falls_back_to_the_biggest_scale_and_is_never_finer_than_needed()
        {
            var s = SheetLogic.Build("A4", null);
            Assert.Equal(F2Numbers.StandardScales.Last(), SheetLogic.FitScale(s, 1, 1e9, 1e9));
            Assert.Equal(1, SheetLogic.FitScale(s, 1, 1, 1));
            // metres: a 30 m × 20 m plan on A1 at 1:100
            var a1 = SheetLogic.Build("A1", null);
            int n = SheetLogic.FitScale(a1, 1000, 30, 20);
            Assert.True(SheetLogic.Fits(a1, n, 1000, 30, 20));
            int i = Array.IndexOf(F2Numbers.StandardScales, n);
            Assert.True(i == 0 || !SheetLogic.Fits(a1, F2Numbers.StandardScales[i - 1], 1000, 30, 20));
        }

        [Fact]
        public void Title_fields_given_override_the_defaults_and_long_values_are_reported()
        {
            var v = SheetLogic.FieldValues(new Dictionary<string, object> { ["scale"] = "1:50 / 1:20", ["DATE"] = "01/12/2026", ["title"] = null }, 100, "A3", new DateTime(2026, 11, 25));
            Assert.Equal("1:50 / 1:20", v["TI_LE"]);
            Assert.Equal("01/12/2026", v["NGAY"]);
            Assert.Equal("", v["TEN_BAN_VE"]);
            Assert.Equal("A3", v["KHO_GIAY"]);
            var tb = SheetLogic.BuildTitleBlock();
            Assert.Empty(SheetLogic.TooLong(tb, v));
            v["SO_BAN_VE"] = new string('X', 40);
            Assert.Equal(new[] { "sheet_no" }, SheetLogic.TooLong(tb, v));
        }

        [Fact]
        public void Paper_names_and_orientation_edges()
        {
            Assert.Equal("A1", SheetLogic.NormalizePaper(" a1 "));
            Assert.Equal("A3", SheetLogic.NormalizePaper(null));
            Assert.Throws<ToolException>(() => SheetLogic.NormalizePaper("A5"));
            Assert.Throws<ToolException>(() => SheetLogic.NormalizePaper("Letter"));
            var p = SheetLogic.Build("A4", true);
            Assert.True(p.Landscape);
            Assert.Equal(297, p.W);
            Assert.Equal("ISO_full_bleed_A4_(297.00_x_210.00_MM)", p.Media);
            // the viewport never overlaps the title block or the frame
            foreach (var paper in F2Numbers.Papers.Select(x => x.name))
                foreach (var land in new bool?[] { true, false, null })
                {
                    var s = SheetLogic.Build(paper, land);
                    Assert.True(s.VpY0 >= s.Y0 + s.TitleH + F2Numbers.ViewportGap - 1e-9);
                    Assert.True(s.VpX0 >= s.X0 && s.VpX1 <= s.X1 && s.VpY1 <= s.Y1);
                    Assert.True(s.VpW > 0 && s.VpH > 0);
                }
        }

        [Fact]
        public void Library_blocks_stay_inside_their_size_at_any_allowed_size()
        {
            var rnd = new Random(25112026);
            foreach (var it in LibraryBlocks.Items)
                for (int k = 0; k < 40; k++)
                {
                    double w = Math.Round(it.MinWidth + rnd.NextDouble() * (it.MaxWidth - it.MinWidth));
                    double d = it.HasDepth ? Math.Round(it.MinDepth + rnd.NextDouble() * (it.MaxDepth - it.MinDepth)) : 0;
                    var prims = LibraryBlocks.Build(it, w, d);
                    Assert.NotEmpty(prims);
                    var b = LibraryBlocks.Box(prims);
                    double tol = 1e-6;
                    switch (it.Key)
                    {
                        case "cua_di": Assert.Equal(new[] { 0, 0, w, w }, b.Select(x => Math.Round(x, 6)).ToArray()); break;
                        case "cua_di_2_canh": Assert.Equal(new[] { 0, 0, w, w / 2 }, b.Select(x => Math.Round(x, 6)).ToArray()); break;
                        case "cay": Assert.Equal(w / 2, b[2], 6); Assert.Equal(-w / 2, b[0], 6); break;
                        case "bon_cau": case "lavabo":
                            Assert.True(b[0] >= -w / 2 - tol && b[2] <= w / 2 + tol && b[1] >= -tol && b[3] <= d + tol, it.Key); break;
                        default:
                            Assert.True(b[0] >= -tol && b[1] >= -tol && b[2] <= w + tol && b[3] <= d + tol, $"{it.Key} {w}x{d}: {string.Join(",", b)}"); break;
                    }
                    foreach (var p in prims)
                    {
                        Assert.All(p.Pts, q => Assert.True(!double.IsNaN(q[0]) && !double.IsNaN(q[1])));
                        if (p.Kind == "ellipse") Assert.True(p.Rx > 0 && p.Ry > 0, it.Key);
                        if (p.Kind == "circle" || p.Kind == "arc") Assert.True(p.R > 0, it.Key);
                        if (p.Kind == "pline") Assert.True(p.Pts.Count >= 2 && (p.Bulges == null || p.Bulges.Count == p.Pts.Count), it.Key);
                    }
                    // the block name is a valid AutoCAD symbol name and depends on the sizes only
                    var name = LibraryBlocks.BlockName(it, w, d);
                    Assert.DoesNotContain(name, c => "<>/\\\":;?*|,=`".IndexOf(c) >= 0);
                    Assert.Equal(name, LibraryBlocks.BlockName(it, w, d));
                }
        }

        [Fact]
        public void Library_sizes_at_the_edges()
        {
            var door = LibraryBlocks.Require("Cửa đi");
            Assert.Equal((500.0, 0.0), LibraryBlocks.Sizes(door, 500, null));
            Assert.Equal((1500.0, 0.0), LibraryBlocks.Sizes(door, 1500.4, null)); // rounded first
            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, 499, null));
            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, 0.9, null)); // metres given as mm: refused, not a tiny door
            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, null, 100)); // a door has no depth
            var win = LibraryBlocks.Require("window");
            Assert.Equal("CUA-SO-1200", LibraryBlocks.BlockName(win, 1200, 220));
            Assert.Equal("CUA-SO-1200x110", LibraryBlocks.BlockName(win, 1200, 110));
            Assert.Throws<ToolException>(() => LibraryBlocks.Require("thang may"));
            Assert.Null(LibraryBlocks.Find(""));
            Assert.Null(LibraryBlocks.Find(null));
        }

        [Fact]
        public void Layer_state_names_at_the_edges()
        {
            Assert.Equal("Tầng 1 - in", LayerStateLogic.CheckName("  Tầng 1 - in "));
            Assert.Equal(new string('a', 255), LayerStateLogic.CheckName(new string('a', 255)));
            Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(new string('a', 256)));
            foreach (var bad in new[] { "", "   ", null, "a/b", "a:b", "a*", "a,b", "a=b" })
                Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(bad));
            Assert.Equal("save", LayerStateLogic.NormalizeAction(" LƯU "));
            Assert.Throws<ToolException>(() => LayerStateLogic.NormalizeAction(null));
        }

        [Fact]
        public void Overwrite_refusals_never_change_anything_and_name_the_handles()
        {
            var m = F2Confirm.CardMessage("layer_state", "delete the layer state 'A'", new[] { "1F2", "1F3" });
            Assert.Contains("Nothing was changed", m);
            Assert.Contains("1F2, 1F3", m);
            Assert.Contains("request_confirmation", m);
            // the plugin refuses before deleting / saving over: the check comes first in the source
            var t = Tools;
            int del = t.IndexOf("lsm.DeleteLayerState(name);\n                    return", StringComparison.Ordinal);
            int refuse = t.LastIndexOf("delete the layer state", del, StringComparison.Ordinal);
            Assert.True(refuse > 0 && refuse < del);
        }
    }
}
