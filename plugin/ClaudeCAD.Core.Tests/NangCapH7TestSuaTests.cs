using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt H7: docs/ban-giao/nang-cap-h7-test.md.</summary>
    public class NangCapH7TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static List<double[]> R(params double[][] p) => p.ToList();

        [Fact]
        public void H7T01_every_run_that_is_not_vertical_keeps_the_duct_width_level()
        {
            // along x, up, then along y (the route of the thread's own test): the top run must not get a vertical width
            var pcs = MepLogic.Layout(R(new[] { 0.0, 0, 3000 }, new[] { 3000.0, 0, 3000 }, new[] { 3000.0, 0, 6000 }, new[] { 3000.0, 3000, 6000 }), 400);
            foreach (var p in pcs.Where(x => x.Kind == "pipe"))
            {
                var d = MepLogic.Unit(MepLogic.Sub(p.B, p.A));
                if (!MepLogic.IsVertical(d)) Assert.Equal(0, p.W[2], 9);
                Assert.Equal(0, MepLogic.Dot(p.W, d), 9);
            }
            var elbows = pcs.Where(x => x.Kind == "elbow").ToList();
            Assert.False(elbows[0].Twist);
            Assert.True(elbows[1].Twist); // the section turns at the top of the riser: reported
            // up and back the same way: no twist
            var same = MepLogic.Layout(R(new[] { 0.0, 0, 3000 }, new[] { 3000.0, 0, 3000 }, new[] { 3000.0, 0, 6000 }, new[] { 6000.0, 0, 6000 }), 400);
            Assert.DoesNotContain(same, x => x.Twist);
            Assert.All(same.Where(x => x.Kind == "pipe" && !MepLogic.IsVertical(MepLogic.Sub(x.B, x.A))), p => Assert.Equal(0, p.W[2], 9));
            // a plan route with odd angles: level everywhere, no twist
            var plan = MepLogic.Layout(R(new[] { 0.0, 0, 3000 }, new[] { 4000.0, 0, 3000 }, new[] { 6000.0, 3500, 3000 }, new[] { 1000.0, 5000, 3000 }), 300);
            Assert.DoesNotContain(plan, x => x.Twist);
            Assert.All(plan.Where(x => x.W != null), p => Assert.Equal(0, p.W[2], 9));
            Assert.Contains("pieces.Any(x => x.Twist)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void H7T02_a_drain_given_with_heights_is_checked_on_its_own_fall()
        {
            var (least, rises) = MepLogic.SlopesOf(R(new[] { 0.0, 0, 3000 }, new[] { 4000.0, 0, 2960 }, new[] { 4000.0, 0, 1000 }, new[] { 4000.0, 2000, 950 }));
            Assert.Equal(1.0, least.Value, 9); // 40 mm over 4 m; the stack is left out; then 50 over 2 m = 2.5 %
            Assert.False(rises);
            Assert.True(MepLogic.SlopesOf(R(new[] { 0.0, 0, 3000 }, new[] { 1000.0, 0, 3010 })).rises);
            Assert.Null(MepLogic.SlopesOf(R(new[] { 0.0, 0, 3000 }, new[] { 0.0, 0, 0 })).least);
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("MepLogic.SlopesOf(pts)", src);
            Assert.Contains("least_slope_percent", src);
        }

        [Fact]
        public void H7T03_a_tiny_turn_is_no_elbow()
        {
            double a = 5e-4; // 0.03°
            var pcs = MepLogic.Layout(R(new[] { 0.0, 0, 0 }, new[] { 5000.0, 0, 0 }, new[] { 10000.0, 5000 * Math.Tan(a), 0 }), 100);
            Assert.DoesNotContain(pcs, x => x.Kind == "elbow");
            var real = MepLogic.Layout(R(new[] { 0.0, 0, 0 }, new[] { 5000.0, 0, 0 }, new[] { 10000.0, 5000 * Math.Tan(0.01), 0 }), 100);
            Assert.Single(real, x => x.Kind == "elbow");
        }

        [Fact]
        public void H7T04_under_beams_says_when_beams_of_two_floors_are_over_the_route()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("Beams at several heights over the route", src);
            Assert.Contains("the UCS is tilted out of plan: give each point as x, y, z", src); // H7T05: plan points under a tilted UCS
        }

        [Fact]
        public void H7T05_the_fixes_of_the_writer_stand()
        {
            Assert.Equal(2.5, MepLogic.DefaultSlope(60));
            Assert.Equal(2.0, MepLogic.DefaultSlope(110));
            Assert.Equal(2.0, MepLogic.DefaultSlope(40)); // under the table: the default 2 %
            // a slanted pipe far from a column box: its axis gap, not the boxes' 0
            double gap = MepLogic.PipeBoxGap(new[] { 0.0, 0, 0 }, new[] { 10000.0, 10000, 0 }, 50, new[] { 8000.0, 0, -500 }, new[] { 8400.0, 400, 500 });
            Assert.True(gap > 4000, gap.ToString());
            Assert.Equal(0, MepLogic.PipePipeGap(new[] { 0.0, 0, 0 }, new[] { 1000.0, 0, 0 }, 50, new[] { 500.0, -500, 0 }, new[] { 500.0, 500, 0 }, 50), 9);
            Assert.Equal(100, MepLogic.PipePipeGap(new[] { 0.0, 0, 0 }, new[] { 1000.0, 0, 0 }, 50, new[] { 500.0, -500, 200 }, new[] { 500.0, 500, 200 }, 50), 9);
        }
    }
}
