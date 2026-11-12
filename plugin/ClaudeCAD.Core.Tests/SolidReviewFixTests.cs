using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>3D phase 1, fixes after the independent review (KT-28, KT-29, KT-30; docs/ban-giao/ve-3d-dot-1.md).</summary>
    public class SolidReviewFixTests
    {
        private static string SolidTools =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing", "SolidTools.cs")));

        // ------------------------------------------------------------ KT-28

        [Fact]
        public void Empty_intersection_message_names_the_solids()
        {
            var m = SolidLogic.EmptyIntersectionMessage("solid_intersect", new List<string> { "2F4A", "2F4B" });
            Assert.Contains("do not overlap", m);
            Assert.Contains("2F4A, 2F4B", m);
            Assert.Contains("Nothing was changed", m);
            Assert.DoesNotContain("touch", m);
        }

        [Fact]
        public void Intersect_checks_for_an_empty_result_after_every_step()
        {
            var src = SolidTools;
            int loop = src.IndexOf("foreach (var h in hs.Skip(1))", StringComparison.Ordinal);
            int check = src.IndexOf("EmptyIntersectionMessage", StringComparison.Ordinal);
            int loopEnd = src.IndexOf("if (IsEmpty(target))", loop, StringComparison.Ordinal);
            Assert.True(loop > 0 && check > loop && check < loopEnd, "the empty check must be inside the loop over the other solids");
        }

        // ------------------------------------------------------------ KT-29

        [Fact]
        public void Set_view_3d_refuses_handles_that_do_not_exist_like_capture_view()
        {
            var src = SolidTools;
            Assert.Contains("set_view_3d: none of the handles exists", src);
            Assert.Contains("ErrorCodes.EntityNotFound, \"set_view_3d", src);
        }

        // ------------------------------------------------------------ KT-30

        [Fact]
        public void Extrude_comment_matches_the_layer_rule()
        {
            var src = SolidTools;
            Assert.DoesNotContain("the profile layer / colour are the defaults", src);
            Assert.Contains("else the current layer", src);
        }

        private static readonly double[] A = { 0, 0, 0 };

        [Fact]
        public void Revolve_check_catches_an_arc_that_bulges_over_the_axis()
        {
            // a half disc right of the Y axis whose arc, sampled, crosses x = 100 where the axis is: vertices alone
            // (0,0) and (0,1000) are left of it, the arc points right of it
            var pts = new List<double[]>();
            for (int i = 0; i <= 32; i++)
            {
                double t = Math.PI * i / 32 - Math.PI / 2;
                pts.Add(new[] { 500 * Math.Cos(t), 500 + 500 * Math.Sin(t), 0 });
            }
            var verticesOnly = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 0, 1000, 0 } };
            Assert.Null(SolidLogic.ProfileCrossesAxis(verticesOnly, new double[] { 100, 0 }, new double[] { 100, 1 }));
            pts.AddRange(verticesOnly);
            Assert.NotNull(SolidLogic.ProfileCrossesAxis(pts, new double[] { 100, 0 }, new double[] { 100, 1 }));
            Assert.Null(SolidLogic.ProfileCrossesAxis(pts, new double[] { 0, 0 }, new double[] { 0, 1 }));
        }

        [Fact]
        public void Revolve_check_works_on_a_vertical_profile()
        {
            // a rectangle on the XZ plane (normal -Y), axis = the Z axis on its left edge: fine
            var rect = new[] { new double[] { 0, 0, 0 }, new double[] { 300, 0, 0 }, new double[] { 300, 0, 1000 }, new double[] { 0, 0, 1000 } };
            double[] n = { 0, -1, 0 };
            Assert.Null(SolidLogic.ProfileCrossesAxis(rect, A, new double[] { 0, 0, 1 }, n));
            // the axis through its middle: crosses (the old XY-only check did not see this)
            Assert.NotNull(SolidLogic.ProfileCrossesAxis(rect, new double[] { 150, 0, 0 }, new double[] { 150, 0, 1 }, n));
            // an axis along Y points out of that plane
            Assert.Contains("plane", SolidLogic.ProfileCrossesAxis(rect, A, new double[] { 0, 1, 0 }, n));
        }
    }
}
