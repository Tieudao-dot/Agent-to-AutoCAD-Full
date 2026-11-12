using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Fixes of the 3D phase 2 test round (docs/ban-giao/ve-3d-test-2.md).</summary>
    public class Solid2FixTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");

        private static EdgeDesc Edge(int i, double[] a, double[] b) =>
            new EdgeDesc { Index = i, Start = a, End = b, Straight = true, Mid = new[] { (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2 } };

        [Fact]
        public void Topology_id_has_no_negative_zero_so_net48_and_net8_agree()
        {
            // a vertex a hair below the bounding box min rounds to -0: "-0" on .NET 8 / 10, "0" on .NET Framework
            var faces = new[] { new FaceDesc { Index = 0, Planar = true, Center = new double[] { 500, 0, 0 } } };
            var exact = new[] { Edge(0, new double[] { 0, 0, 0 }, new double[] { 1000, 0, 0 }) };
            var below = new[] { Edge(0, new double[] { -0.001, -1e-9, 0 }, new double[] { 1000, 0, 0 }) };
            var min = new double[] { 0, 0, 0 };
            Assert.Equal(Solid2Logic.TopologyId(exact, faces, min), Solid2Logic.TopologyId(below, faces, min));
        }

        [Fact]
        public void Interference_pairs_are_each_checked_once()
        {
            var all = Solid2Logic.InterferencePairs(new[] { "A", "B", "C" }, null);
            Assert.Equal(3, all.Count);
            // handles and other_handles overlap: A–B once (not again as B–A), never A–A
            var cross = Solid2Logic.InterferencePairs(new[] { "A", "B" }, new[] { "b", "A", "C" });
            Assert.Equal(new[] { ("A", "b"), ("A", "C"), ("B", "C") }, cross.ToArray());
            Assert.Empty(Solid2Logic.InterferencePairs(new[] { "A" }, new[] { "a" }));
            Assert.Contains("Solid2Logic.InterferencePairs(hs, others)", Tools);
        }

        [Fact]
        public void Shell_uses_the_documented_inward_sign_on_the_solid_itself()
        {
            // AcDbMgd.xml: positive = outside, negative = inside
            Assert.Equal(-50, Solid2Logic.ShellOffset(50));
            Assert.Equal(-50, Solid2Logic.ShellOffset(-50));
            double[] min = { 0, 0, 0 }, max = { 1000, 1000, 800 };
            // open tank 1000 × 1000 × 800, wall 50: outer size kept, volume down
            Assert.True(Solid2Logic.ShellLooksInward(0.8e9, 0.1925e9, min, max, min, max, 50));
            // outward: the box grew
            Assert.False(Solid2Logic.ShellLooksInward(0.8e9, 0.2e9, min, max, new double[] { -50, -50, -50 }, max, 50));
            // nothing removed / nothing left
            Assert.False(Solid2Logic.ShellLooksInward(0.8e9, 0.8e9, min, max, min, max, 50));
            Assert.False(Solid2Logic.ShellLooksInward(0.8e9, 0, min, max, min, max, 50));
            // no trial on a copy any more (its Brep order / subentity ids are not guaranteed to be the solid's)
            Assert.DoesNotContain("s.Clone()", Tools);
            Assert.Contains("Solid2Logic.ShellOffset(th)", Tools);
        }

        [Fact]
        public void Closed_shell_with_an_empty_face_list_needs_no_topology_id()
        {
            var src = Tools;
            int empty = src.IndexOf("if (p.Numbers(\"faces\").Count == 0) return new List<int>();", StringComparison.Ordinal);
            int require = src.IndexOf("RequireTopology(p, t, handle);\n                return Solid2Logic.CheckIndices(IntList(p, \"faces\")", StringComparison.Ordinal);
            if (require < 0) require = src.IndexOf("RequireTopology(p, t, handle);\r\n                return Solid2Logic.CheckIndices(IntList(p, \"faces\")", StringComparison.Ordinal);
            Assert.True(empty > 0 && require > empty, "faces: [] must return before the topology check");
        }

        [Fact]
        public void Sweep_and_loft_helper_curves_are_database_resident_on_the_result_layer()
        {
            var src = Tools;
            // path_points (2D and 3D) and sections_points go through AddTemp (Acad.Add with the tool's params, erased by DropTemp)
            Assert.True(Regex.Matches(src, @"AddTemp\(ctx, p, ").Count >= 3);
            Assert.DoesNotContain("Acad.Add(ctx, p3, null)", src);
            Assert.True(Regex.Matches(src, @"DropTemp\(temp\)").Count >= 2);
        }

        [Fact]
        public void Align_checks_the_matrix_convention_before_moving()
        {
            Assert.Contains("CheckedMatrix(m, src, \"align\")", Tools);
            // the row-major math the check compares against: translation in [3], [7], [11]
            var m = Solid2Logic.AlignMatrix(new[] { new double[] { 0, 0, 0 } }, new[] { new double[] { 10, 20, 30 } }, false);
            Assert.Equal(new double[] { 10, 20, 30 }, new[] { m[3], m[7], m[11] });
            Assert.Equal(new double[] { 11, 22, 33 }, Solid2Logic.Apply(m, new double[] { 1, 2, 3 }));
        }
    }
}
