using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>3D phase 2 (docs/ban-giao/ve-3d-dot-2.md): edge / face picking, topology id, planes, align, UCS, limits, labels.</summary>
    public class Solid2Tests
    {
        private static string Root(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        /// <summary>A 1000 × 800 × 600 box at the origin as Brep would list it: 12 straight edges, 6 planar faces.</summary>
        private static (List<EdgeDesc> edges, List<FaceDesc> faces, double[] min, double[] max) Box(double ox = 0)
        {
            double l = 1000, w = 800, h = 600;
            double[] C(int i) => new[] { ox + ((i & 1) == 0 ? 0 : l), (i & 2) == 0 ? 0.0 : w, (i & 4) == 0 ? 0.0 : h };
            var pairs = new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) };
            var faces = new List<FaceDesc>
            {
                new FaceDesc { Index = 0, Planar = true, Normal = new double[] { 0, 0, -1 }, Min = C(0), Max = new[] { ox + l, w, 0.0 } },
                new FaceDesc { Index = 1, Planar = true, Normal = new double[] { 0, 0, 1 }, Min = new[] { ox, 0.0, h }, Max = C(7) },
                new FaceDesc { Index = 2, Planar = true, Normal = new double[] { 0, -1, 0 }, Min = C(0), Max = new[] { ox + l, 0.0, h } },
                new FaceDesc { Index = 3, Planar = true, Normal = new double[] { 0, 1, 0 }, Min = new[] { ox, w, 0.0 }, Max = C(7) },
                new FaceDesc { Index = 4, Planar = true, Normal = new double[] { -1, 0, 0 }, Min = C(0), Max = new[] { ox, w, h } },
                new FaceDesc { Index = 5, Planar = true, Normal = new double[] { 1, 0, 0 }, Min = new[] { ox + l, 0.0, 0.0 }, Max = C(7) },
            };
            foreach (var f in faces) f.Center = new[] { (f.Min[0] + f.Max[0]) / 2, (f.Min[1] + f.Max[1]) / 2, (f.Min[2] + f.Max[2]) / 2 };
            var edges = new List<EdgeDesc>();
            for (int i = 0; i < pairs.Length; i++)
            {
                var a = C(pairs[i].Item1); var b = C(pairs[i].Item2);
                var e = new EdgeDesc { Index = i, Start = a, End = b, Straight = true, Mid = new[] { (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2 } };
                foreach (var f in faces)
                {
                    bool On(double[] p) => Enumerable.Range(0, 3).All(k => p[k] >= f.Min[k] - 1e-9 && p[k] <= f.Max[k] + 1e-9);
                    if (On(a) && On(b)) e.Faces.Add(f.Index);
                }
                edges.Add(e);
            }
            return (edges, faces, C(0), C(7));
        }

        // ------------------------------------------------------------ edge / face filters

        [Fact]
        public void Edge_filters_pick_the_expected_box_edges()
        {
            var (e, _, min, max) = Box();
            Assert.Equal(new[] { 8, 9, 10, 11 }, Solid2Logic.FilterEdges(e, new[] { "parallel_z" }, min, max, "t"));
            Assert.Equal(4, Solid2Logic.FilterEdges(e, new[] { "parallel_x" }, min, max, "t").Count);
            Assert.Equal(4, Solid2Logic.FilterEdges(e, new[] { "top" }, min, max, "t").Count);
            Assert.Equal(12, Solid2Logic.FilterEdges(e, new[] { "all" }, min, max, "t").Count);
            // every filter must hold: the top edges along X
            Assert.Equal(new[] { 2, 3 }, Solid2Logic.FilterEdges(e, new[] { "top", "parallel_x" }, min, max, "t"));
            // front = min Y
            Assert.All(Solid2Logic.FilterEdges(e, new[] { "front" }, min, max, "t"), i => { Assert.Equal(0, e[i].Start[1]); Assert.Equal(0, e[i].End[1]); });
            Assert.Empty(Solid2Logic.FilterEdges(e, new[] { "curved" }, min, max, "t"));
            var ex = Assert.Throws<ToolException>(() => Solid2Logic.FilterEdges(e, new[] { "vertical" }, min, max, "fillet_edges"));
            Assert.Equal(ErrorCodes.InvalidParams, ex.Code);
            Assert.Contains("parallel_z", ex.Message);
        }

        [Fact]
        public void Face_filters_pick_the_side_faces()
        {
            var (_, f, min, max) = Box(5000);
            Assert.Equal(new[] { 1 }, Solid2Logic.FilterFaces(f, new[] { "top" }, min, max, "t"));
            Assert.Equal(new[] { 1, 2 }, Solid2Logic.FilterFaces(f, new[] { "top", "front" }, min, max, "t"));
            Assert.Equal(new[] { 5 }, Solid2Logic.FilterFaces(f, new[] { "right" }, min, max, "t"));
            // the sign of a Brep normal is not trusted
            f[1].Normal = new double[] { 0, 0, -1 };
            Assert.Equal(new[] { 1 }, Solid2Logic.FilterFaces(f, new[] { "top" }, min, max, "t"));
            Assert.Throws<ToolException>(() => Solid2Logic.FilterFaces(f, new[] { "up" }, min, max, "shell_solid"));
        }

        [Fact]
        public void Indices_are_checked()
        {
            Assert.Equal(new List<int> { 0, 11 }, Solid2Logic.CheckIndices(new[] { 0, 11 }, 12, "edge", "t", 500));
            Assert.Contains("out of range", Assert.Throws<ToolException>(() => Solid2Logic.CheckIndices(new[] { 12 }, 12, "edge", "t", 500)).Message);
            Assert.Contains("twice", Assert.Throws<ToolException>(() => Solid2Logic.CheckIndices(new[] { 1, 1 }, 12, "edge", "t", 500)).Message);
            Assert.Throws<ToolException>(() => Solid2Logic.CheckIndices(new[] { -1 }, 12, "edge", "t", 500));
            Assert.Throws<ToolException>(() => Solid2Logic.CheckIndices(new int[0], 12, "edge", "t", 500));
        }

        // ------------------------------------------------------------ topology id

        [Fact]
        public void Topology_id_is_stable_moves_with_the_solid_and_changes_with_the_shape()
        {
            var a = Box(); var b = Box(); var moved = Box(7000);
            var id = Solid2Logic.TopologyId(a.edges, a.faces, a.min);
            Assert.Equal(id, Solid2Logic.TopologyId(b.edges, b.faces, b.min));
            // moved as a whole (a macro replayed at another point): the same indices still mean the same edges
            Assert.Equal(id, Solid2Logic.TopologyId(moved.edges, moved.faces, moved.min));
            Assert.Matches("^[0-9a-f]{16}$", id);
            // another order of the same edges: other indices, so another id
            var swapped = Box();
            (swapped.edges[0], swapped.edges[1]) = (swapped.edges[1], swapped.edges[0]);
            Assert.NotEqual(id, Solid2Logic.TopologyId(swapped.edges, swapped.faces, swapped.min));
            // a changed shape (one edge longer)
            var longer = Box();
            longer.edges[0].End = new double[] { 1200, 0, 0 };
            Assert.NotEqual(id, Solid2Logic.TopologyId(longer.edges, longer.faces, longer.min));
            // floating-point noise is ignored
            var noisy = Box();
            noisy.edges[3].Start = noisy.edges[3].Start.Select(x => x + 1e-9).ToArray();
            Assert.Equal(id, Solid2Logic.TopologyId(noisy.edges, noisy.faces, noisy.min));
            Assert.Contains("get_solid_info again", Solid2Logic.StaleMessage("fillet_edges", "2F4A"));
        }

        // ------------------------------------------------------------ chamfer groups and re-matching

        [Fact]
        public void Chamfer_groups_cover_every_edge_with_few_base_faces()
        {
            var (e, _, _, _) = Box();
            // the 4 top edges share the top face: one call
            var top = Solid2Logic.ChamferGroups(e, new List<int> { 2, 3, 6, 7 }, null, "t");
            Assert.Single(top);
            Assert.Equal(1, top[0].face);
            // the 4 vertical edges need two side faces
            var vert = Solid2Logic.ChamferGroups(e, new List<int> { 8, 9, 10, 11 }, null, "t");
            Assert.Equal(2, vert.Count);
            Assert.Equal(new[] { 8, 9, 10, 11 }, vert.SelectMany(g => g.edges).OrderBy(i => i));
            // base_face that some edge does not bound
            Assert.Contains("base_face", Assert.Throws<ToolException>(() => Solid2Logic.ChamferGroups(e, new List<int> { 2, 8 }, 1, "t")).Message);
        }

        [Fact]
        public void An_edge_shortened_by_a_chamfer_is_found_again()
        {
            var (e, _, _, _) = Box();
            var old = e[8]; // vertical edge at x = 0, y = 0
            var shorter = new EdgeDesc { Index = 40, Start = new double[] { 0, 0, 50 }, End = new double[] { 0, 0, 600 }, Straight = true, Mid = new double[] { 0, 0, 325 } };
            var other = e[9];
            Assert.Equal(40, Solid2Logic.MatchEdge(old, new List<EdgeDesc> { other, shorter }, 0.01));
            Assert.Null(Solid2Logic.MatchEdge(old, new List<EdgeDesc> { other }, 0.01));
        }

        // ------------------------------------------------------------ planes, slice

        [Fact]
        public void Planes_from_points_normal_or_axis_plane()
        {
            var p = Solid2Logic.Plane(new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 } }, null, null, null, "t");
            Assert.Equal(new double[] { 0, 0, 1 }, p.Normal);
            Assert.Equal(new double[] { 1, 0, 0 }, Solid2Logic.Plane(null, new double[] { 5, 0 }, new double[] { 3, 0, 0 }, null, "t").Normal);
            Assert.Equal(new double[] { 0, 1, 0 }, Solid2Logic.Plane(null, new double[] { 0, 0, 0 }, null, "zx", "t").Normal);
            Assert.Contains("one line", Assert.Throws<ToolException>(() => Solid2Logic.Plane(new List<double[]> { new double[] { 0, 0 }, new double[] { 1, 1 }, new double[] { 2, 2 } }, null, null, null, "t")).Message);
            Assert.Throws<ToolException>(() => Solid2Logic.Plane(null, null, null, null, "t"));
            Assert.Throws<ToolException>(() => Solid2Logic.Plane(null, new double[] { 0, 0 }, new double[] { 0, 0, 1 }, "xy", "t"));
            Assert.Throws<ToolException>(() => Solid2Logic.Plane(null, null, new double[] { 0, 0, 1 }, null, "t"));
            Assert.Throws<ToolException>(() => Solid2Logic.Plane(null, new double[] { 0, 0 }, new double[] { 0, 0, 0 }, null, "t"));
        }

        [Fact]
        public void A_plane_must_pass_through_the_solid()
        {
            var (_, _, min, max) = Box();
            Assert.True(Solid2Logic.PlaneCutsBox(new PlaneDef { Point = new double[] { 500, 0, 0 }, Normal = new double[] { 1, 0, 0 } }, min, max));
            Assert.False(Solid2Logic.PlaneCutsBox(new PlaneDef { Point = new double[] { 1500, 0, 0 }, Normal = new double[] { 1, 0, 0 } }, min, max));
            // only touching a face is no cut
            Assert.False(Solid2Logic.PlaneCutsBox(new PlaneDef { Point = new double[] { 0, 0, 600 }, Normal = new double[] { 0, 0, 1 } }, min, max));
            Assert.Equal("both", Solid2Logic.SliceKeep(null));
            Assert.Equal("negative", Solid2Logic.SliceKeep("Negative"));
            Assert.Throws<ToolException>(() => Solid2Logic.SliceKeep("top"));
        }

        // ------------------------------------------------------------ align, rotate

        private static void Near(double[] a, double[] b, double tol = 1e-6)
        {
            for (int i = 0; i < 3; i++) Assert.True(Math.Abs(a[i] - b[i]) < tol, $"[{string.Join(", ", a)}] ≠ [{string.Join(", ", b)}]");
        }

        [Fact]
        public void Align_one_two_and_three_pairs()
        {
            // 1 pair: move
            var m1 = Solid2Logic.AlignMatrix(new List<double[]> { new double[] { 1, 2, 3 } }, new List<double[]> { new double[] { 11, 2, 3 } }, false);
            Near(Solid2Logic.Apply(m1, new double[] { 0, 0, 0 }), new double[] { 10, 0, 0 });
            // 2 pairs: the X direction onto Y (a quarter turn about Z), first point onto first point
            var s2 = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 100, 0, 0 } };
            var d2 = new List<double[]> { new double[] { 500, 500, 0 }, new double[] { 500, 600, 0 } };
            var m2 = Solid2Logic.AlignMatrix(s2, d2, false);
            Near(Solid2Logic.Apply(m2, s2[0]), d2[0]);
            Near(Solid2Logic.Apply(m2, s2[1]), d2[1]);
            Near(Solid2Logic.Apply(m2, new double[] { 0, 0, 50 }), new double[] { 500, 500, 50 }); // up stays up
            // scale: 100 → 300
            var m2s = Solid2Logic.AlignMatrix(s2, new List<double[]> { d2[0], new double[] { 500, 800, 0 } }, true);
            Near(Solid2Logic.Apply(m2s, s2[1]), new double[] { 500, 800, 0 });
            // 3 pairs: stand a plate in the XY plane up into the XZ plane
            var s3 = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 } };
            var d3 = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 0, 0, 1 } };
            var m3 = Solid2Logic.AlignMatrix(s3, d3, false);
            Near(Solid2Logic.Apply(m3, new double[] { 0, 5, 0 }), new double[] { 0, 0, 5 });
            Near(Solid2Logic.Apply(m3, new double[] { 0, 0, 1 }), new double[] { 0, -1, 0 });
            // opposite directions (half turn) stay in the plan
            var mo = Solid2Logic.AlignMatrix(s2, new List<double[]> { new double[] { 0, 0, 0 }, new double[] { -100, 0, 0 } }, false);
            Near(Solid2Logic.Apply(mo, new double[] { 0, 0, 10 }), new double[] { 0, 0, 10 });
            Near(Solid2Logic.Apply(mo, new double[] { 100, 0, 0 }), new double[] { -100, 0, 0 });
            // bad input
            Assert.Throws<ToolException>(() => Solid2Logic.AlignMatrix(s2, new List<double[]> { d2[0] }, false));
            Assert.Throws<ToolException>(() => Solid2Logic.AlignMatrix(new List<double[]> { s2[0], s2[0] }, d2, false));
            Assert.Contains("one line", Assert.Throws<ToolException>(() => Solid2Logic.AlignMatrix(
                new List<double[]> { new double[] { 0, 0 }, new double[] { 1, 0 }, new double[] { 2, 0 } }, s3, false)).Message);
        }

        [Fact]
        public void Rotation_axis_is_checked()
        {
            var (d, a) = Solid2Logic.RotationAxis(new double[] { 0, 0, 0 }, new double[] { 0, 0, 5 }, 90);
            Assert.Equal(new double[] { 0, 0, 1 }, d);
            Assert.Equal(Math.PI / 2, a, 9);
            Assert.Throws<ToolException>(() => Solid2Logic.RotationAxis(new double[] { 1, 1 }, new double[] { 1, 1 }, 90));
            Assert.Throws<ToolException>(() => Solid2Logic.RotationAxis(new double[] { 0, 0 }, new double[] { 1, 0 }, 4000));
        }

        // ------------------------------------------------------------ UCS

        [Fact]
        public void Ucs_presets_and_axes()
        {
            foreach (var n in Solid2Logic.UcsPresets)
            {
                var (x, y) = Solid2Logic.UcsPreset(n);
                Assert.Equal(0, SolidLogic.Dot(x, y), 9);
            }
            Assert.Equal(new double[] { 0, 0, 1 }, Solid2Logic.UcsPreset("front").y);
            var (ux, uy, uz) = Solid2Logic.UcsAxes(new double[] { 2, 0, 0 }, new double[] { 1, 1, 0 });
            Assert.Equal(new double[] { 1, 0, 0 }, ux);
            Assert.Equal(new double[] { 0, 1, 0 }, uy);
            Assert.Equal(new double[] { 0, 0, 1 }, uz);
            Assert.Throws<ToolException>(() => Solid2Logic.UcsAxes(new double[] { 1, 0, 0 }, new double[] { 3, 0, 0 }));
            Assert.Throws<ToolException>(() => Solid2Logic.UcsPreset("iso"));
            Assert.Equal("Mặt đứng A", Solid2Logic.UcsName(" Mặt đứng A "));
            Assert.Throws<ToolException>(() => Solid2Logic.UcsName("a:b"));
        }

        [Fact]
        public void Ucs_is_not_recorded_in_macros_and_every_tool_stays_wcs()
        {
            Assert.False(MacroLogic.ShouldRecord("set_ucs", true));
            Assert.False(MacroLogic.ShouldRecord("reset_ucs", true));
            Assert.True(MacroLogic.ShouldRecord("fillet_edges", true));
            // the tools never read the UCS: no Editor.CurrentUserCoordinateSystem outside set_ucs / reset_ucs
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            var uses = Regex.Matches(src, @"CurrentUserCoordinateSystem").Count;
            Assert.Equal(4, uses);
            foreach (var f in Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing")), "*.cs"))
                if (!f.EndsWith("Solid2Tools.cs", StringComparison.Ordinal))
                    Assert.DoesNotContain("CurrentUserCoordinateSystem =", File.ReadAllText(f));
        }

        // ------------------------------------------------------------ macros: handle params of the new tools

        [Fact]
        public void Macro_maps_the_handles_of_the_new_tools()
        {
            foreach (var k in new[] { "handle", "handles", "other_handles", "path_handle", "section_handles", "guide_handles", "profile_handles" })
                Assert.True(MacroLogic.IsHandleKey(k), k);
            // indices and the topology id are not handles: they are checked against the replayed solid
            Assert.False(MacroLogic.IsHandleKey("edges"));
            Assert.False(MacroLogic.IsHandleKey("topology_id"));
            var map = new Dictionary<string, string> { ["A1"] = "B1", ["A2"] = "B2" };
            var p = new Dictionary<string, object>
            {
                ["handle"] = "A1", ["edges"] = new List<object> { 1.0, 2.0 }, ["topology_id"] = "a1a1a1a1a1a1a1a1",
                ["section_handles"] = new List<object> { "A1", "A2" }, ["path_handle"] = "A2",
            };
            var r = (Dictionary<string, object>)MacroLogic.Remap(p, map);
            Assert.Equal("B1", r["handle"]);
            Assert.Equal("a1a1a1a1a1a1a1a1", r["topology_id"]);
            Assert.Equal(new List<object> { "B1", "B2" }, r["section_handles"]);
            Assert.Equal("B2", r["path_handle"]);
            // a slice keeping both halves returns [kept, new]: both are learnt in order
            var m = new Dictionary<string, string>();
            MacroLogic.Learn(m, new[] { "A1", "A9" }, new[] { "B1", "B9" });
            Assert.Equal("B9", m["A9"]);
        }

        [Fact]
        public void Fillet_after_build_waits_for_the_batch_that_built_the_solid()
        {
            var calls = new List<MacroCall>
            {
                new MacroCall { Tool = "draw_box", ResultHandles = { "A1" } },
                new MacroCall { Tool = "fillet_edges", Params = { ["handle"] = "A1", ["edge_filter"] = new List<object> { "parallel_z" }, ["radius"] = 50.0 }, ResultHandles = { "A1" } },
            };
            var groups = MacroLogic.PlanBatches(calls, _ => true);
            Assert.Equal(2, groups.Count);
        }

        // ------------------------------------------------------------ labels, limits shared with the assistant

        internal static readonly string[] Phase2 =
        {
            "sweep", "loft", "slice_solid", "fillet_edges", "chamfer_edges", "shell_solid", "rotate_3d", "mirror_3d", "align",
            "set_ucs", "reset_ucs", "check_interference",
        };

        [Fact]
        public void Phase2_tools_are_3D_only()
        {
            foreach (var t in Phase2)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.False(PlanLogic.Allowed(PlanLogic.Plan2D, t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.Plan3D, t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.PlanFull, t), t);
            }
            Assert.Contains("3D group", PlanLogic.BlockedMessage("fillet_edges", "2d", ""));
        }

        [Fact]
        public void Phase2_tools_are_registered_in_Solid2Tools()
        {
            var rx = new Regex("(?:Register\\(\\s*|new\\s+ToolDef\\(\\s*)\"([a-z0-9_]+)\"");
            var names = rx.Matches(Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs")).Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            Assert.Equal(Phase2.OrderBy(x => x).ToArray(), names);
        }

        [Fact]
        public void Limits_match_the_assistant_schema()
        {
            var ts = Root("agent", "src", "tools", "solids.ts");
            void Has(string name, double v) =>
                Assert.Matches(new Regex($@"\b{name}: {Regex.Escape(v.ToString(System.Globalization.CultureInfo.InvariantCulture))},"), ts);
            Has("DefaultListItems", Solid2Logic.DefaultListItems);
            Has("MaxListItems", Solid2Logic.MaxListItems);
            Has("MaxEdgeSelection", Solid2Logic.MaxEdgeSelection);
            Has("MaxFaceSelection", Solid2Logic.MaxFaceSelection);
            Has("MinLoftSections", Solid2Logic.MinLoftSections);
            Has("MaxLoftSections", Solid2Logic.MaxLoftSections);
            Has("MaxInterferenceSolids", Solid2Logic.MaxInterferenceSolids);
            Has("MaxInterferenceCandidates", Solid2Logic.MaxInterferenceCandidates);
            Has("MaxTwist", Solid2Logic.MaxTwist);
            Has("MinSweepScale", Solid2Logic.MinSweepScale);
            Has("MaxSweepScale", Solid2Logic.MaxSweepScale);
            foreach (var f in Solid2Logic.EdgeFilters) Assert.Contains($"'{f}'", ts);
            foreach (var u in Solid2Logic.UcsPresets) Assert.Contains($"'{u}'", ts);
            Solid2Logic.CheckSweep(3600, 100);
            Assert.Throws<ToolException>(() => Solid2Logic.CheckSweep(3601, 1));
            Assert.Throws<ToolException>(() => Solid2Logic.CheckSweep(0, 0.001));
        }

        [Fact]
        public void Filleted_box_volume()
        {
            // each filleted vertical edge removes (1 − π/4) r² h
            Assert.Equal(1000.0 * 800 * 600 - 4 * (1 - Math.PI / 4) * 100 * 100 * 600, Solid2Logic.FilletedBoxVolume(1000, 800, 600, 100, 4), 6);
        }

        // ------------------------------------------------------------ source checks of what only AutoCAD runs

        [Fact]
        public void Plugin_sources_keep_the_lessons_of_test_round_1()
        {
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            // AutoCAD refusals become INVALID_PARAMS with "Nothing was changed"
            foreach (var api in new[] { "FilletEdges", "ChamferEdges", "ShellBody", "CreateSweptSolid", "CreateLoftedSolid", ".Slice(" })
                Assert.Contains(api, src);
            Assert.True(Regex.Matches(src, "Nothing was changed").Count >= 10);
            // temporary objects are disposed: clones in using, sweep / loft temp lists dropped in finally
            Assert.Contains("using (var ca = (Solid3d)a.Clone())", src);
            Assert.True(Regex.Matches(src, @"DropTemp\(temp\)").Count >= 2);
            Assert.Contains("other?.Dispose();", src);
            // the UCS tools are not batchable (no transaction) and not view-only: "Chỉ xem" refuses them, U undoes them
            Assert.Contains("new ToolDef(\"set_ucs\", true, SetUcs, noTransaction: true)", src);
            var disp = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            Assert.DoesNotContain("\"set_ucs\"", disp);
        }

        // ------------------------------------------------------------ fixes after the independent review (12/11/2026)

        /// <summary>KT-13 (đợt 2): an edge with no start point (closed edge whose curve could not be read) must not crash a side filter.</summary>
        [Fact]
        public void KT13_edge_side_filter_does_not_crash_on_edge_without_points()
        {
            var edges = new List<EdgeDesc> { new EdgeDesc { Index = 0, Straight = false } };
            var ex = Record.Exception(() => Solid2Logic.FilterEdges(edges, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "fillet_edges"));
            Assert.True(ex == null || ex is ToolException, "FilterEdges threw " + ex?.GetType().Name + " (internal plugin error) instead of skipping the edge");
            // nor the axis filters on a straight edge without points
            var straight = new List<EdgeDesc> { new EdgeDesc { Index = 0, Straight = true } };
            Assert.Empty(Solid2Logic.FilterEdges(straight, new[] { "parallel_z" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "t"));
            Assert.Empty(Solid2Logic.FilterEdges(edges, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "t"));
        }

        /// <summary>KT-13 (đợt 2): a planar face with no bounds and no centre must not crash a face filter.</summary>
        [Fact]
        public void KT13_face_side_filter_does_not_crash_on_face_without_points()
        {
            var faces = new List<FaceDesc> { new FaceDesc { Index = 0, Planar = true, Normal = new double[] { 0, 0, 1 } } };
            var ex = Record.Exception(() => Solid2Logic.FilterFaces(faces, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "shell_solid"));
            Assert.True(ex == null || ex is ToolException, "FilterFaces threw " + ex?.GetType().Name + " (internal plugin error) instead of skipping the face");
            Assert.Empty(Solid2Logic.FilterFaces(faces, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "t"));
        }

        /// <summary>KT-27: moving the solid by an odd amount or turning it keeps the id; the topology id has no absolute position in it.</summary>
        [Fact]
        public void KT27_topology_id_survives_odd_moves_and_turns()
        {
            var a = Box();
            var id = Solid2Logic.TopologyId(a.edges, a.faces);
            Assert.Equal(id, Solid2Logic.TopologyId(a.edges, a.faces, a.min));
            foreach (var ox in new[] { 0.004999, 0.005, 0.005001, 12345.678901, -0.0049 })
            {
                var m = Box(ox);
                Assert.Equal(id, Solid2Logic.TopologyId(m.edges, m.faces, m.min));
            }
            // turned 37° about Z and moved: the same edges in the same order
            double c = Math.Cos(37 * Math.PI / 180), sn = Math.Sin(37 * Math.PI / 180);
            double[] T(double[] p) => p == null ? null : new[] { 500.25 + c * p[0] - sn * p[1], -77.7 + sn * p[0] + c * p[1], p[2] + 3.3 };
            var turned = Box();
            foreach (var e in turned.edges) { e.Start = T(e.Start); e.End = T(e.End); e.Mid = T(e.Mid); }
            Assert.Equal(id, Solid2Logic.TopologyId(turned.edges, turned.faces));
            // a stretched box is another shape
            var wide = Box();
            foreach (var e in wide.edges) { e.Start = new[] { e.Start[0] * 1.5, e.Start[1], e.Start[2] }; e.End = new[] { e.End[0] * 1.5, e.End[1], e.End[2] }; e.Mid = new[] { e.Mid[0] * 1.5, e.Mid[1], e.Mid[2] }; }
            Assert.NotEqual(id, Solid2Logic.TopologyId(wide.edges, wide.faces));
        }

        /// <summary>KT-24: a pair AutoCAD flags whose common volume cannot be measured stays a clash, never "touching".</summary>
        [Fact]
        public void KT24_unmeasured_overlap_is_still_a_clash()
        {
            Assert.Equal("clash_unmeasured", Solid2Logic.ClassifyInterference(null));
            Assert.Equal("clash_unmeasured", Solid2Logic.ClassifyInterference(double.NaN));
            Assert.Equal("touching", Solid2Logic.ClassifyInterference(0));
            Assert.Equal("clash", Solid2Logic.ClassifyInterference(5e8));
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            Assert.Contains("internal static double? OverlapVolume", src);
            Assert.Contains("catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }", src);
            Assert.Contains("[\"interfere\"] = clashes > 0", src);
        }

        /// <summary>KT-25: the work is capped (pairs past the bounding-box test, measured volumes) and Claude is told when it was cut.</summary>
        [Fact]
        public void KT25_interference_work_is_capped_and_reported()
        {
            Assert.Equal(2000, Solid2Logic.MaxInterferenceCandidates);
            Assert.True(Solid2Logic.MaxInterferencePairs < Solid2Logic.MaxInterferenceCandidates);
            var note = Solid2Logic.InterferenceTruncatedNote(4950, 2000);
            Assert.Contains("2000 of 4950", note);
            Assert.Contains("smaller groups", note);
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            Assert.Contains("Math.Min(candidates.Count, Solid2Logic.MaxInterferenceCandidates)", src);
            Assert.Contains("bool tried = measured < Solid2Logic.MaxInterferencePairs;", src);
            Assert.Contains("double? v = tried ? OverlapVolume(sa, sb) : (double?)null;", src);
            Assert.Contains("r[\"truncated\"] = true;", src);
        }

        /// <summary>KT-26: plan_view of a turned UCS turns the view so the UCS x axis points right; an unturned UCS needs no twist.</summary>
        [Fact]
        public void KT26_plan_twist_follows_the_ucs_x_axis()
        {
            // UCS turned 30° about Z: looking down (right = X, up = Y), PLAN gives VIEWTWIST 330°
            var x30 = new[] { Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0 };
            Assert.Equal(2 * Math.PI - Math.PI / 6, Solid2Logic.PlanTwist(x30, new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }), 9);
            Assert.Equal(0, Solid2Logic.PlanTwist(new double[] { 1, 0, 0 }, new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }), 9);
            // a tilted UCS: its x is checked on the screen axes of the same view direction
            foreach (var (xa, ya) in new[] { (new double[] { 1, 0, 0 }, new double[] { 0, 0.6, 0.8 }), (new double[] { 0.6, 0.8, 0 }, new double[] { 0, 0, 1 }), (new double[] { 0, 0.8, 0.6 }, new double[] { 1, 0, 0 }) })
            {
                var (ux, uy, uz) = Solid2Logic.UcsAxes(xa, ya);
                var view = SolidLogic.Custom(uz);
                var (right, up) = SolidLogic.ScreenAxes(view);
                double t = Solid2Logic.PlanTwist(ux, right, up);
                // turning the screen axes by the twist puts the UCS x on the screen right
                double sx = SolidLogic.Dot(ux, right), sy = SolidLogic.Dot(ux, up);
                Assert.Equal(1, Math.Cos(t) * sx - Math.Sin(t) * sy, 6);
                Assert.Equal(0, Math.Sin(t) * sx + Math.Cos(t) * sy, 6);
            }
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            Assert.Contains("Solid2Logic.PlanTwist(", src);
        }

        /// <summary>KT-40: pairs past the measuring limit are "unconfirmed": never a clash, never safe, and Claude is told.</summary>
        [Fact]
        public void KT40_unmeasured_pairs_past_the_limit_are_unconfirmed_not_clashes()
        {
            Assert.Equal("unconfirmed", Solid2Logic.InterferenceKind(false, null));
            Assert.Equal("clash_unmeasured", Solid2Logic.InterferenceKind(true, null));
            Assert.Equal("touching", Solid2Logic.InterferenceKind(true, 0));
            Assert.Equal("clash", Solid2Logic.InterferenceKind(true, 1e6));
            // a large model of walls that only touch: 200 measured as touching, the rest unconfirmed → no clash reported
            var kinds = Enumerable.Range(0, 500).Select(i => Solid2Logic.InterferenceKind(i < Solid2Logic.MaxInterferencePairs, i < Solid2Logic.MaxInterferencePairs ? 0.0 : (double?)null)).ToList();
            Assert.False(Solid2Logic.Interferes(kinds));
            Assert.Equal(300, kinds.Count(k => k == "unconfirmed"));
            // one real overlap, or one failed measurement (KT-24), still says interfere
            Assert.True(Solid2Logic.Interferes(kinds.Concat(new[] { "clash" })));
            Assert.True(Solid2Logic.Interferes(kinds.Concat(new[] { "clash_unmeasured" })));
            var note = Solid2Logic.InterferenceUnconfirmedNote(300);
            Assert.Contains("300 more pair(s)", note);
            Assert.Contains("not measured", note);
            Assert.Contains("smaller list", note);
            var src = Root("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");
            Assert.Contains("if (kind == \"unconfirmed\")", src);
            Assert.Contains("r[\"unconfirmed_note\"] = Solid2Logic.InterferenceUnconfirmedNote(unconfirmed);", src);
            Assert.DoesNotContain("not measured (only the first", src);
        }
    }
}
