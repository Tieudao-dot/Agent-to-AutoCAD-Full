using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt G2 (docs/ban-giao/nang-cap-g2.md): check_drawing / fix_drawing logic, without AutoCAD.</summary>
    public class NangCapG2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Plugin(params string[] parts) => Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray());

        // ------------------------------------------------------------ groups

        [Fact]
        public void Every_group_has_a_vietnamese_name_a_level_and_a_hint()
        {
            var ids = DrawingCheckLogic.Groups.Select(g => g.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var g in DrawingCheckLogic.Groups)
            {
                Assert.False(string.IsNullOrWhiteSpace(g.Name), g.Id);
                Assert.Contains(g.Level, new[] { G2Numbers.LevelFix, G2Numbers.LevelConsider, G2Numbers.LevelHint });
                Assert.False(string.IsNullOrWhiteSpace(g.Hint), g.Id);
            }
            Assert.Equal(new[] { "layer_zero", "defpoints", "not_bylayer", "nonstandard_styles", "empty_text", "dim_override", "duplicates", "zero_length", "viewport_unlocked" },
                DrawingCheckLogic.FixableIds.ToArray());
            Assert.Equal(new[] { "nonstandard_layers", "nonstandard_styles", "text_height" }, DrawingCheckLogic.Groups.Where(g => g.NeedsStandard).Select(g => g.Id).ToArray());
            Assert.Equal("nen_sua", DrawingCheckLogic.LevelKey(G2Numbers.LevelFix));
            Assert.Equal("goi_y", DrawingCheckLogic.LevelKey(G2Numbers.LevelHint));
        }

        [Fact]
        public void The_agent_lists_the_same_groups()
        {
            var ts = Src("agent", "src", "tools", "drawingCheck.ts");
            foreach (var g in DrawingCheckLogic.Groups) Assert.Contains("'" + g.Id + "'", ts);
            int fixStart = ts.IndexOf("FIX_GROUPS", StringComparison.Ordinal);
            var fixPart = ts.Substring(fixStart, ts.IndexOf("as const", fixStart, StringComparison.Ordinal) - fixStart);
            foreach (var f in DrawingCheckLogic.FixableIds) Assert.Contains("'" + f + "'", fixPart);
            Assert.Equal(DrawingCheckLogic.FixableIds.Count(), fixPart.Split('\'').Length / 2);
        }

        // ------------------------------------------------------------ the standard

        [Fact]
        public void The_standard_is_found_from_its_text_style_and_its_scale_from_the_dimension_style()
        {
            Assert.Null(DrawingCheckLogic.StandardPrefix(new[] { "Standard", "Arial" }));
            Assert.Equal("VN", DrawingCheckLogic.StandardPrefix(new[] { "Standard", "vn-arial" }));
            Assert.Equal("KT", DrawingCheckLogic.StandardPrefix(new[] { "VN-ARIAL", "KT-ARIAL" }));
            Assert.Equal(100, DrawingCheckLogic.DimStyleScale("KT", "KT-100"));
            Assert.Equal(50, DrawingCheckLogic.DimStyleScale("KT", "kt-50-M"));
            Assert.Null(DrawingCheckLogic.DimStyleScale("KT", "VN-100"));
            Assert.Null(DrawingCheckLogic.DimStyleScale("KT", "KT-ARIAL"));
            Assert.True(DrawingCheckLogic.IsStandardTextStyle("KT", "KT-ARIAL"));
            Assert.True(DrawingCheckLogic.IsStandardTextStyle("KT", F2Numbers.TitleTextStyle));
            Assert.False(DrawingCheckLogic.IsStandardTextStyle("KT", "Standard"));
            Assert.False(DrawingCheckLogic.IsStandardTextStyle(null, "KT-ARIAL"));
        }

        [Theory]
        [InlineData("A-WALL", true)]
        [InlineData("a-door", true)]
        [InlineData("A-WALL-3D", true)]
        [InlineData("KHUNG", true)]
        [InlineData("KHUNG-VP", true)]
        [InlineData("L-PLNT", true)]
        [InlineData("0", true)]
        [InlineData("Defpoints", true)]
        [InlineData("NHA|A-WALL", true)]
        [InlineData("TUONG", false)]
        [InlineData("Layer1", false)]
        public void Standard_layer_names(string name, bool std) => Assert.Equal(std, DrawingCheckLogic.IsStandardLayer(name));

        [Fact]
        public void A_text_height_is_off_only_beyond_ten_percent_of_every_standard_height()
        {
            var hs = DrawingCheckLogic.StandardHeights(100, 1); // 500, 350, 250, 180 mm
            Assert.Equal(new[] { 500.0, 350, 250, 180 }, hs);
            Assert.False(DrawingCheckLogic.HeightOff(250, hs, out _));
            Assert.False(DrawingCheckLogic.HeightOff(270, hs, out _)); // +8 %
            Assert.True(DrawingCheckLogic.HeightOff(300, hs, out var near));
            Assert.Equal(350, near);
            Assert.True(DrawingCheckLogic.HeightOff(100, hs, out _));
            Assert.Equal(new[] { 0.5, 0.35, 0.25, 0.18 }, DrawingCheckLogic.StandardHeights(100, 1000)); // a drawing in metres
            Assert.False(DrawingCheckLogic.HeightOff(0, hs, out _));
        }

        // ------------------------------------------------------------ geometry

        [Fact]
        public void A_line_drawn_backwards_is_the_same_line_and_a_tiny_shift_too()
        {
            double tol = G2Numbers.DuplicateTolMm;
            var a = DrawingCheckLogic.ShapeKey("line", "A-WALL", DrawingCheckLogic.LineNumbers(new[] { 0.0, 0, 0 }, new[] { 3000.0, 0, 0 }), tol);
            var b = DrawingCheckLogic.ShapeKey("line", "a-wall", DrawingCheckLogic.LineNumbers(new[] { 3000.0, 0, 0 }, new[] { 0.0, 0, 0 }), tol);
            var c = DrawingCheckLogic.ShapeKey("line", "A-WALL", DrawingCheckLogic.LineNumbers(new[] { 0.0, 0.01, 0 }, new[] { 3000.0, 0, 0 }), tol);
            var d = DrawingCheckLogic.ShapeKey("line", "A-WALL", DrawingCheckLogic.LineNumbers(new[] { 0.0, 5, 0 }, new[] { 3000.0, 0, 0 }), tol);
            var e = DrawingCheckLogic.ShapeKey("line", "A-DOOR", DrawingCheckLogic.LineNumbers(new[] { 0.0, 0, 0 }, new[] { 3000.0, 0, 0 }), tol);
            Assert.Equal(a, b);
            Assert.Equal(a, c);
            Assert.NotEqual(a, d);
            Assert.NotEqual(a, e); // another layer is not a copy
            Assert.Equal(new List<int> { 1, 2 }, DrawingCheckLogic.DuplicateIndices(new[] { a, b, c, d, null, e }));
        }

        [Fact]
        public void An_open_polyline_drawn_backwards_is_the_same()
        {
            var v = new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 500 } };
            var bulges = new List<double> { 0.5, 0, 0 };
            var rv = new List<double[]> { v[2], v[1], v[0] };
            var rb = new List<double> { 0, -0.5, 0 };
            Assert.Equal(DrawingCheckLogic.PolylineNumbers(v, bulges, false), DrawingCheckLogic.PolylineNumbers(rv, rb, false));
            Assert.NotEqual(DrawingCheckLogic.PolylineNumbers(v, bulges, false), DrawingCheckLogic.PolylineNumbers(v, new List<double> { 0, 0, 0 }, false));
        }

        [Fact]
        public void Far_away_objects_are_found_and_a_small_drawing_keeps_its_title()
        {
            var boxes = new List<(double[] min, double[] max)>();
            for (int i = 0; i < 30; i++) boxes.Add((new[] { i * 300.0, 0 }, new[] { i * 300.0 + 200, 200 }));
            boxes.Add((new[] { 1e7, 1e7 }, new[] { 1e7 + 10, 1e7 + 10 }));      // lost far away
            boxes.Add((new[] { 15000.0, -8000 }, new[] { 20000.0, -7000 }));    // a title near the plan
            var far = DrawingCheckLogic.FarAway(boxes, 1);
            Assert.Equal(new List<int> { 30 }, far);
            Assert.Empty(DrawingCheckLogic.FarAway(boxes.Take(5).ToList(), 1)); // too few objects
            // the same drawing in metres: the same answer
            var m = boxes.Select(b => (b.min.Select(x => x / 1000).ToArray(), b.max.Select(x => x / 1000).ToArray())).ToList();
            Assert.Equal(new List<int> { 30 }, DrawingCheckLogic.FarAway(m, 1000));
        }

        [Fact]
        public void Viewport_scales_and_other_small_rules()
        {
            Assert.Equal(100, DrawingCheckLogic.ViewportDenominator(SheetLogic.CustomScale(100, 1), 1), 6);
            Assert.True(DrawingCheckLogic.StandardViewportScale(DrawingCheckLogic.ViewportDenominator(SheetLogic.CustomScale(50, 1000), 1000)));
            Assert.False(DrawingCheckLogic.StandardViewportScale(DrawingCheckLogic.ViewportDenominator(1.0 / 87.3, 1)));
            Assert.False(DrawingCheckLogic.StandardViewportScale(0));
            Assert.True(DrawingCheckLogic.IsDefaultLayoutName("Layout1"));
            Assert.True(DrawingCheckLogic.IsDefaultLayoutName("layout 2"));
            Assert.False(DrawingCheckLogic.IsDefaultLayoutName("BV-01"));
            Assert.True(DrawingCheckLogic.IsUnnamedBlock("A$C1A2B3C4D"));
            Assert.False(DrawingCheckLogic.IsUnnamedBlock("CUA-DI-900"));
            Assert.True(DrawingCheckLogic.HidesMeasurement("3000"));
            Assert.False(DrawingCheckLogic.HidesMeasurement("<> (thông thủy)"));
            Assert.False(DrawingCheckLogic.HidesMeasurement(""));
            Assert.True(DrawingCheckLogic.IsWallLayer("A-WALL"));
            Assert.True(DrawingCheckLogic.IsWallLayer("Tuong-220"));
            Assert.False(DrawingCheckLogic.IsWallLayer("A-WALL-PAT"));
            Assert.False(DrawingCheckLogic.IsWallLayer("A-DOOR"));
        }

        // ------------------------------------------------------------ fix_drawing

        [Fact]
        public void One_token_for_one_fix_on_exactly_these_objects()
        {
            var t = DrawingCheckLogic.FixToken("layer_zero", "A-WALL", new[] { "2F", "1A" });
            Assert.Equal(16, t.Length);
            Assert.Equal(t, DrawingCheckLogic.FixToken("LAYER_ZERO", "a-wall", new[] { "1a", "2F", "1A" })); // order / case free
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("layer_zero", "A-DOOR", new[] { "2F", "1A" }));   // another layer
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("defpoints", "A-WALL", new[] { "2F", "1A" }));    // another fix
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("layer_zero", "A-WALL", new[] { "2F", "1A", "3B" })); // one more object
            Assert.NotEqual(t, F5Confirm.FileToken("A-WALL"));
        }

        [Fact]
        public void Moving_off_layer_0_needs_a_real_target_layer()
        {
            Assert.NotNull(DrawingCheckLogic.TargetLayerError("layer_zero", null));
            Assert.NotNull(DrawingCheckLogic.TargetLayerError("defpoints", " 0 "));
            Assert.NotNull(DrawingCheckLogic.TargetLayerError("layer_zero", "defpoints"));
            Assert.Null(DrawingCheckLogic.TargetLayerError("layer_zero", "A-WALL"));
            Assert.Null(DrawingCheckLogic.TargetLayerError("duplicates", null));
        }

        [Fact]
        public void The_excel_list_has_one_row_per_group_and_no_total_row()
        {
            var rows = DrawingCheckLogic.ExcelRows(new[]
            {
                new Dictionary<string, object> { ["name"] = "Hình trùng nhau", ["level"] = G2Numbers.LevelFix, ["count"] = 3, ["examples"] = new List<object> { "2F", "30" }, ["fix"] = "duplicates" },
                new Dictionary<string, object> { ["name"] = "Hatch mất liên kết với biên", ["level"] = G2Numbers.LevelConsider, ["count"] = 1, ["examples"] = new List<object> { "4A" }, ["fix"] = null },
            });
            Assert.Equal(3, rows.Count);
            Assert.Equal("2F, 30", rows[1][4]);
            Assert.Equal("có", rows[1][5]);
            Assert.Equal("không", rows[2][5]);
            Assert.Equal(2, XlsxLogic.DataRows(XlsxLogic.ForExcel(rows)));
            Assert.Equal("soat-ban-ve", F4Numbers.DefaultSuffix["check_drawing"]);
            Assert.True(XlsxWriter.SafeSheetName(F4Numbers.SheetName["check_drawing"]).Length <= F4Numbers.SheetNameMax);
        }

        // ------------------------------------------------------------ wiring (source)

        [Fact]
        public void The_check_only_reads_and_the_fix_writes_after_the_card()
        {
            var t = Plugin("Drawing", "DrawingCheckTools.cs");
            Assert.Contains("new ToolDef(\"check_drawing\", false, CheckDrawing)", t);
            Assert.Contains("new ToolDef(\"fix_drawing\", true, FixDrawing)", t);
            int card = t.IndexOf("RequireCard(ctx, p, \"fix_drawing\"", StringComparison.Ordinal);
            int firstWrite = t.IndexOf("e.UpgradeOpen();", StringComparison.Ordinal);
            Assert.True(card > 0 && card < firstWrite, "the card before any change");
            Assert.Contains("if (ltr.IsLocked) { locked.Add(ltr.Name); lockedCount++; continue; }", t);
            Assert.Contains("s.Scanned >= G2Numbers.MaxScan", t);
            Assert.Contains("\"fix_drawing\"", Plugin("Core", "Macros.cs")); // never replayed by a macro
            var plans = Plugin("Core", "Plans.cs");
            Assert.Contains("\"check_drawing fix_drawing\"", plans);
            Assert.True(PlanLogic.Allowed("2d", "check_drawing"));
            Assert.True(PlanLogic.Allowed("2d3d", "fix_drawing"));
            Assert.False(PlanLogic.Allowed("3d", "check_drawing"));
        }
    }
}
