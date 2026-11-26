using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt H3 (docs/ban-giao/nang-cap-h3-test.md): the bugs found in the review of compare_drawings.</summary>
    public class NangCapH3TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static CmpItem Line(string h, double x0, double y0, double x1, double y1) => new CmpItem
        {
            Handle = h, Kind = "line", Layer = "0", Nums = new[] { x0, y0, 0, x1, y1, 0 }, Alt = new[] { x1, y1, 0, x0, y0, 0 },
            Min = new[] { Math.Min(x0, x1), Math.Min(y0, y1), 0 }, Max = new[] { Math.Max(x0, x1), Math.Max(y0, y1), 0 },
        };

        // ------------------------------------------------------------ H3T-01: shared handles by chance

        [Fact]
        public void Two_drawings_from_one_template_share_handles_but_are_matched_by_geometry()
        {
            // both started from the same template: their own lines got handles 2A0, 2A1… in each file, other shapes
            var a = new List<CmpItem>();
            var b = new List<CmpItem>();
            for (int i = 0; i < 40; i++)
            {
                a.Add(Line((0x2A0 + i).ToString("X"), i * 1000, 0, i * 1000, 3000));          // a grid of 40 lines
                b.Add(Line((0x2A0 + i).ToString("X"), i * 1000 + 30000, 500, i * 1000 + 30000, 3500));
            }
            b.Add(Line("400", 0, 0, 0, 3000)); // the first line of A, drawn in B too (another handle)
            Assert.Equal("handle", DrawingCompareLogic.MatchMode("auto", a, b)); // 40 of 40 handles shared
            var rows = DrawingCompareLogic.Run(a, b, "auto", 1, false, out var mode);
            Assert.Equal("geometry", mode);
            var c = DrawingCompareLogic.Counts(rows);
            Assert.Equal(39, c["removed"]);
            Assert.Equal(40, c["added"]);
            Assert.Equal(0, c["changed"]);
            // asked handle stays handle
            DrawingCompareLogic.Run(a, b, "handle", 1, false, out var asked);
            Assert.Equal("handle", asked);
        }

        [Fact]
        public void A_real_revision_stays_matched_by_handle()
        {
            var old = Enumerable.Range(0, 30).Select(i => Line((0x100 + i).ToString("X"), i * 1000, 0, i * 1000, 3000)).ToList();
            var now = old.Take(25).Select(l => Line(l.Handle, l.Nums[0], 0, l.Nums[3], 3000)).ToList();
            now[3] = Line(now[3].Handle, 3000, 0, 3000, 3600); // lengthened: one "changed"
            now[4] = Line(now[4].Handle, 4700, 0, 4700, 3000); // moved 700: one "changed" by handle (2 rows by geometry)
            now.Add(Line("900", 50000, 0, 50000, 3000));
            var rows = DrawingCompareLogic.Run(old, now, null, 1, false, out var mode);
            Assert.Equal("handle", mode);
            var c = DrawingCompareLogic.Counts(rows);
            Assert.Equal(5, c["removed"]);
            Assert.Equal(1, c["added"]);
            Assert.Equal(2, c["changed"]);
        }

        [Fact]
        public void Moved_half_a_millimetre_is_the_same_one_and_a_half_is_changed()
        {
            var old = new List<CmpItem> { Line("A", 0, 0, 1000, 0), Line("B", 0, 500, 1000, 500) };
            var now = new List<CmpItem> { Line("X", 0.5, 0, 1000.5, 0), Line("Y", 1.5, 500, 1001.5, 500) };
            var rows = DrawingCompareLogic.Run(old, now, "auto", 1, false, out var mode);
            Assert.Equal("geometry", mode);
            // 1.5 mm: no shape within 1 mm and the first point is 1.5 off: a removed + an added
            Assert.Equal(1, DrawingCompareLogic.Counts(rows)["removed"]);
            Assert.Equal(1, DrawingCompareLogic.Counts(rows)["added"]);
            Assert.DoesNotContain(rows, r => r.OtherHandle == "A");
        }

        // ------------------------------------------------------------ H3T-02: units

        [Fact]
        public void A_drawing_without_a_unit_is_never_converted()
        {
            Assert.Equal(1, DrawingCompareLogic.UnitFactor(6, 6, out var same));
            Assert.Null(same);
            Assert.Equal(1000, DrawingCompareLogic.UnitFactor(4, 6, out var m2mm)); // other in metres, open in mm
            Assert.Contains("converted", m2mm);
            Assert.Equal(0.001, DrawingCompareLogic.UnitFactor(6, 4, out _), 12);
            Assert.Equal(1, DrawingCompareLogic.UnitFactor(6, 0, out var unitless)); // an old unitless file against metres
            Assert.Contains("nothing was converted", unitless);
            Assert.Equal(1, DrawingCompareLogic.UnitFactor(0, 4, out var u2));
            Assert.NotNull(u2);
            Assert.Equal(1, DrawingCompareLogic.UnitFactor(4, 3, out _)); // miles: not a unit of these drawings, not converted
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("DrawingCompareLogic.UnitFactor(openIns, otherIns, out var unitNote)", t);
            Assert.DoesNotContain("od.MmPerUnit / openMm", t);
        }

        // ------------------------------------------------------------ H3T-03: sizes within the tolerance

        [Fact]
        public void Dimension_values_widths_and_areas_are_compared_within_their_tolerance()
        {
            CmpItem Dim(string h, double value) => new CmpItem
            {
                Handle = h, Kind = "dim", Layer = "DIM", Nums = new[] { 500.0, 200, 0 }, Sizes = new Dictionary<string, double> { ["value"] = value },
                Min = new[] { 0.0, 0, 0 }, Max = new[] { 1000.0, 300, 0 },
            };
            Assert.Empty(DrawingCompareLogic.Differences(Dim("1", 3000), Dim("1", 3000.0001), 1, true)); // units / rounding noise
            Assert.Empty(DrawingCompareLogic.Differences(Dim("1", 3000), Dim("1", 3000.9), 1, true));
            var d = DrawingCompareLogic.Differences(Dim("1", 3000), Dim("1", 3050), 1, true);
            Assert.Equal(new[] { "value 3000 → 3050" }, d);
            CmpItem Hatch(double area) => new CmpItem
            {
                Handle = "H", Kind = "hatch", Layer = "HA", Nums = new[] { 0.0, 0, 0, 4000, 3000, 0 }, Areas = new Dictionary<string, double> { ["area"] = area },
            };
            Assert.Empty(DrawingCompareLogic.Differences(Hatch(12e6), Hatch(12e6 + 0.37), 1, true));
            Assert.Single(DrawingCompareLogic.Differences(Hatch(12e6), Hatch(13e6), 1, true));
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("it.Sizes[\"value\"] = d.Measurement * f;", t);
            Assert.Contains("it.Areas[\"area\"] = h.Area * f * f;", t);
            Assert.Contains("it.Sizes[\"width\"] = pl.ConstantWidth * f;", t);
        }

        // ------------------------------------------------------------ H3T-04: clouds of an earlier compare, the layer

        [Fact]
        public void Earlier_clouds_and_an_off_layer_are_said_and_a_locked_layer_is_refused_before_the_card()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            int locked = t.IndexOf("is locked or frozen", StringComparison.Ordinal);
            int card = t.IndexOf("RequireCard(ctx, p, tool, $\"draw {clouds.Count} revision clouds", StringComparison.Ordinal);
            int ensure = t.IndexOf("EnsureLayerF2(ctx, H3Numbers.Layer", StringComparison.Ordinal);
            Assert.True(locked > 0 && card > locked && ensure > card, "refuse a locked layer, then the card, then create the layer");
            Assert.Contains("objects of an earlier compare are still on layer", t);
            Assert.Contains("is turned off: the clouds are drawn but not seen", t);
            // the clouds themselves are never compared (both files)
            Assert.Contains("if (string.Equals(e.Layer, H3Numbers.Layer, StringComparison.OrdinalIgnoreCase)) return null;", t);
        }
    }
}
