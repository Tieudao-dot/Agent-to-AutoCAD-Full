using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sau kiểm tra độc lập đợt F (KTF-xx, 25/11/2026, docs/ban-giao/test-tong-the-dot-f.md mục "Sau kiểm tra độc lập"):
    /// the F2 / F3 / document findings fixed on the integration branch, each kept as a test.
    /// </summary>
    public class KiemTraDocLapDotFTests
    {
        private static List<double[]> P(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new[] { xy[2 * i], xy[2 * i + 1] }).ToList();

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        // T: bar 12 × 4 m on top, stem 4 m wide (the house of KTF-01)
        private static readonly List<double[]> Tee = P(4000, 0, 8000, 0, 8000, 6000, 12000, 6000, 12000, 10000, 0, 10000, 0, 6000, 4000, 6000);
        // U: arms 5 m wide, bar 3 m deep
        private static readonly List<double[]> You = P(0, 0, 14000, 0, 14000, 9000, 9000, 9000, 9000, 3000, 5000, 3000, 5000, 9000, 0, 9000);
        // L with a narrow (1 m) wing
        private static readonly List<double[]> NarrowL = P(0, 0, 10000, 0, 10000, 5000, 1000, 5000, 1000, 12000, 0, 12000);

        /// <summary>Turned by deg about (1000, 2000) and rounded to whole mm, like a drawing turned with ROTATE then snapped.</summary>
        private static List<double[]> TurnRound(List<double[]> pts, double deg)
        {
            double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180);
            return pts.Select(q => new[] { Math.Round(1000 + q[0] * c - q[1] * s), Math.Round(2000 + q[0] * s + q[1] * c) }).ToList();
        }

        private static double MinPieceSide(RoofLogic.Layout lay) => lay.Pieces.Count == 0 ? double.MaxValue :
            lay.Pieces.Min(k => Math.Min(Dist(k.Rect[0], k.Rect[1]), Dist(k.Rect[1], k.Rect[2])));

        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

        // ------------------------------------------------------------ KTF-07 / 08 and the checked suspicions

        [Fact]
        public void Ktf07_metre_dimensions_are_documented_as_the_code_shows_them()
        {
            // the dimension style drops trailing zeros (DIMZIN 8): a 4 m line reads "4", not "4.00"
            Assert.Contains("rec.Dimzin = 8", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StyleTools.cs"));
            foreach (var f in new[] { new[] { "docs", "dot-f-cho-duyet.md" }, new[] { "docs", "checklist-tong-the.md" }, new[] { "docs", "checklist-dot-f2.md" } })
                Assert.DoesNotContain("**4.00**", Src(f));
            Assert.DoesNotContain("(vd 4.00)", Src("docs", "dot-f-cho-duyet.md"));
        }

        [Fact]
        public void Ktf08_the_mep_skill_steps_are_numbered_once()
        {
            var lines = Src("skills", "ve-mep", "SKILL.md").Split('\n');
            int a = Array.FindIndex(lines, l => l.StartsWith("## 1."));
            int b = Array.FindIndex(lines, a + 1, l => l.StartsWith("## "));
            var nums = lines.Skip(a).Take(b - a).Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d+\. ")).Select(l => int.Parse(l.Split('.')[0])).ToList();
            Assert.Equal(Enumerable.Range(1, nums.Count), nums);
        }

        [Fact]
        public void A_library_block_is_bylayer_and_a_same_name_block_of_another_size_is_not_reused()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");
            Assert.Contains("ByLayer(br);\n                ids.Add(Acad.Add(ctx, br, pp, defaults: false));", t.Replace("\r\n", "\n"));
            Assert.Contains("LibraryBlocks.OtherSizeName(name)", t);
            Assert.True(LibraryBlocks.SameSize(900, 900, 900, 900));
            Assert.True(LibraryBlocks.SameSize(1210, 224, 1200, 220)); // within 2 %
            Assert.True(LibraryBlocks.SameSize(220, 1200, 1200, 220)); // drawn turned
            Assert.False(LibraryBlocks.SameSize(0.9, 0.9, 900, 900)); // the same door drawn in metres
            Assert.False(LibraryBlocks.SameSize(800, 800, 900, 900));
            Assert.Equal("CUA-DI-900-TV", LibraryBlocks.OtherSizeName("CUA-DI-900"));
            Assert.Equal("door", FloorPlanLogic.KindOfBlockName("CUA-DI-900-TV"));
            Assert.Equal("window", FloorPlanLogic.KindOfBlockName("CUA-SO-1200-TV"));
        }

        [Fact]
        public void A_material_of_the_users_with_a_preset_meaning_is_used_as_it_is()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");
            Assert.Contains("if (preset != null && dict.Contains(requested.Trim()) && !string.Equals(requested.Trim(), preset.Name, StringComparison.OrdinalIgnoreCase))", t);
            // "Glass", "Metal", "tile", "granite" are aliases of presets: without the check they would be swapped
            foreach (var n in new[] { "Glass", "Metal", "tile", "granite" }) Assert.NotNull(MaterialLogic.Find(n));
            // the picture of a saved view says it keeps the direction only
            Assert.Contains("its zoom is not kept", Src("agent", "src", "tools", "render.ts"));
        }

        // ------------------------------------------------------------ KTF-01: outlines a little off right angles

        [Fact]
        public void Ktf01_a_t_house_one_corner_off_by_1_mm_is_the_same_gable_roof()
        {
            var exact = RoofLogic.Build(Tee, 3000, new RoofLogic.Options { Type = "gable" });
            Assert.Equal(2, exact.Pieces.Count);
            Assert.Equal(4293, exact.TopZ, 0);
            var off = Tee.Select(q => (double[])q.Clone()).ToList();
            off[4] = new[] { 12000.0, 10001 }; // 12000,10000 → 12000,10001
            var lay = RoofLogic.Build(off, 3000, new RoofLogic.Options { Type = "gable" });
            Assert.Equal(2, lay.Pieces.Count);
            Assert.True(Math.Abs(lay.TopZ - exact.TopZ) < 2, $"TopZ {lay.TopZ:0.#} (exact {exact.TopZ:0.#})");
        }

        [Theory]
        [InlineData("tee", 37)]
        [InlineData("tee", 133)]
        [InlineData("you", 13)]
        [InlineData("you", 133)]
        [InlineData("narrowL", 37)]
        [InlineData("tee", 90)]
        [InlineData("you", 270)]
        public void Ktf01_turned_and_rounded_outlines_give_the_roof_of_the_exact_outline(string house, double deg)
        {
            var src = house == "tee" ? Tee : house == "you" ? You : NarrowL;
            foreach (var type in new[] { "gable", "hip" })
            {
                var exact = RoofLogic.Build(src, 3000, new RoofLogic.Options { Type = type });
                var lay = RoofLogic.Build(TurnRound(src, deg), 3000, new RoofLogic.Options { Type = type });
                Assert.Equal(exact.Pieces.Count, lay.Pieces.Count);
                Assert.True(Math.Abs(lay.TopZ - exact.TopZ) < 3, $"{type} {house} {deg}°: TopZ {lay.TopZ:0.#} (exact {exact.TopZ:0.#})");
                // no sliver pieces (they break AutoCAD's booleans)
                Assert.True(MinPieceSide(lay) > 500, $"{type} {house} {deg}°: a piece {MinPieceSide(lay):0.#} mm wide");
            }
        }

        [Fact]
        public void Ktf01_squaring_keeps_an_exact_outline_and_moves_a_corner_only_by_the_drawing_error()
        {
            var sq = RoofLogic.SquareUp(Tee);
            for (int i = 0; i < Tee.Count; i++)
            {
                Assert.Equal(Tee[i][0], sq[i][0], 6);
                Assert.Equal(Tee[i][1], sq[i][1], 6);
            }
            var turned = TurnRound(You, 133);
            var s2 = RoofLogic.SquareUp(turned);
            for (int i = 0; i < turned.Count; i++) Assert.True(Dist(turned[i], s2[i]) < 2, $"corner {i} moved {Dist(turned[i], s2[i]):0.##} mm");
            Assert.True(RoofLogic.IsRightAngled(s2));
            // not alternating along / across: not squared
            Assert.Null(RoofLogic.SquareUp(P(0, 0, 5000, 0, 10000, 1, 10000, 5000, 0, 5000, 0, 2500)));
            Assert.Contains("RoofLogic.SquareUp", Src("plugin", "ClaudeCAD.Plugin", "Core", "RoofRightAngled.cs") + Src("plugin", "ClaudeCAD.Plugin", "Core", "StairRoof.cs").Replace("SquareUp(pts)", "RoofLogic.SquareUp(pts)"));
        }
    }
}
