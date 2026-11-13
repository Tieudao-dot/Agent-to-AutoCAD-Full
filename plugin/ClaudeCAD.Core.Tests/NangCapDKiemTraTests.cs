using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt D, sửa sau kiểm tra độc lập nâng cấp (13/11/2026, docs/ban-giao/kiem-tra-doc-lap-nang-cap.md):
    /// KT2-06 STL at survey coordinates, KT2-07 beam-under-slab guidance, KT2-12 beam loop drawn back to its start,
    /// KT2-13 PRESSPULL distance in mm, KT2-18 interference with no pair, KT2-19 build_beam total limit.
    /// </summary>
    public class NangCapDKiemTraTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void KT2_06_survey_coordinates_are_moved_to_zero()
        {
            // VN-2000 in mm: X ≈ 5.8e8, Y ≈ 2.3e9
            var shift = Solid3Logic.StlShift(new[] { 5.8e8, 2.3e9, 0 }, new[] { 5.8e8 + 12000, 2.3e9 + 9000, 7000 });
            Assert.Equal(new[] { -5.8e8, -2.3e9, 0 }, shift);
            // a small model in the positive octant keeps its coordinates; a negative one moves only what is negative
            Assert.Equal(new[] { 0.0, 0, 0 }, Solid3Logic.StlShift(new[] { 100.0, 200, 0 }, new[] { 12000.0, 9000, 7000 }));
            Assert.Equal(new[] { 500.0, 0, 0 }, Solid3Logic.StlShift(new[] { -500.0, 10, 0 }, new[] { 12000.0, 9000, 7000 }));
            // after the shift a 1 mm detail survives 32-bit floats
            float far = (float)(2.3e9 + 1), near = (float)(2.3e9 + 1 + shift[1]);
            Assert.NotEqual(1f, far - (float)2.3e9);
            Assert.Equal(1f, near);
            Assert.Contains("Solid3Logic.StlShift(", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs"));
        }

        [Fact]
        public void KT2_07_beam_under_slab_guidance_matches_the_code()
        {
            // the code: top of the beam = top_z − slab_thickness
            Assert.Equal(3180, Solid3Logic.BeamTop(3300, 0, 120), 9);
            var skill = Src("skills", "ve-3d-kien-truc", "SKILL.md");
            Assert.Contains("đỉnh dầm = `top_z` − `slab_thickness`", skill);
            Assert.DoesNotContain("`top_z` = cao độ mặt sàn + `slab_thickness`", skill);
            var tong = Src("docs", "checklist-tong-the.md");
            Assert.Contains("dầm đỉnh 3150, đáy 2750", tong);
            Assert.DoesNotContain("dầm đỉnh ở 3300", tong);
        }

        [Fact]
        public void KT2_12_open_path_back_to_its_start_is_a_closed_beam()
        {
            var loop = new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, 3000 }, new[] { 0.0, 3000 }, new[] { 0.0, 0 } };
            var open = Solid3Logic.Beam(loop, null, false, 220);
            var closed = Solid3Logic.Beam(loop.Take(4).ToList(), null, true, 220);
            Assert.Equal(4, open.Joints);           // the start corner gets its filler too
            Assert.Equal(closed.Joints, open.Joints);
            Assert.Equal(closed.Pieces.Sum(o => Math.Abs(o.Area)), open.Pieces.Sum(o => Math.Abs(o.Area)), 6);
            // a tiny float gap (snapped endpoint) still counts as the start
            var near = loop.Take(4).Concat(new[] { new[] { 1e-7, -1e-7 } }).ToList();
            Assert.Equal(4, Solid3Logic.Beam(near, null, false, 220).Joints);
            // a really open L stays open
            Assert.Equal(1, Solid3Logic.Beam(loop.Take(3).ToList(), null, false, 220).Joints);
        }

        [Fact]
        public void KT2_13_presspull_distance_is_mm()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs");
            Assert.Contains("double dist = p.Num(\"distance\") / fmm;", s);
            Assert.Contains("FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);", s);
            // a drawing in metres: 200 (mm) is 0.2 units
            Assert.Equal(1000, FloorPlanLogic.MmPerUnit(6, out _), 9);
        }

        [Fact]
        public void KT2_18_no_pair_is_refused_not_reported_as_no_clash()
        {
            Assert.Empty(Solid2Logic.InterferencePairs(new[] { "A" }, new[] { "A" }));
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            int pairs = s.IndexOf("var pairs = Solid2Logic.InterferencePairs(hs, others);", StringComparison.Ordinal);
            int refuse = s.IndexOf("if (pairs.Count == 0)", StringComparison.Ordinal);
            Assert.True(pairs > 0 && refuse > pairs);
            Assert.Contains("no pair of two different solids", s);
        }

        [Fact]
        public void KT2_19_build_beam_has_a_total_limit()
        {
            Solid3Logic.CheckBeamTotal(Solid3Logic.MaxBeamPieces);
            var e = Assert.Throws<ToolException>(() => Solid3Logic.CheckBeamTotal(Solid3Logic.MaxBeamPaths * Solid3Logic.MaxBeamSegments));
            Assert.Contains("Nothing was built", e.Message);
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs");
            int check = s.IndexOf("Solid3Logic.CheckBeamTotal(", StringComparison.Ordinal);
            int layouts = s.IndexOf("var layouts = paths.Select(bp => Solid3Logic.Beam(", StringComparison.Ordinal);
            Assert.True(check > 0 && layouts > check);
        }
    }
}
