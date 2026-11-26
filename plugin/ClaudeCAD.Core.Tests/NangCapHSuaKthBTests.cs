using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa KTH phần H1 / H2 / H3 (docs/ban-giao/nang-cap-h-sua-kth-b.md): KTH-15 → KTH-26 and the read-only hint of compare.</summary>
    public class NangCapHSuaKthBTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ccad-kthb-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        /// <summary>A polyline as CompareTools reads it (closed: the canonical loop; the anchor at its first vertex).</summary>
        private static CmpItem Pline(string h, bool closed, params double[] xy)
        {
            var verts = new List<double[]>();
            for (int i = 0; i + 1 < xy.Length; i += 2) verts.Add(new[] { xy[i], xy[i + 1] });
            var it = new CmpItem { Handle = h, Kind = "pline", Layer = "A-WALL" };
            if (closed) DrawingCompareLogic.SetLoop(it, verts, verts.Select(_ => 0.0).ToList(), 0);
            else
            {
                it.Nums = xy.Concat(new[] { 0.0 }).ToArray();
                it.Exact = "o|" + string.Join(",", verts.Select(_ => "0"));
                it.AnchorAt = new[] { it.Nums[0], it.Nums[1], it.Nums[it.Nums.Length - 1] };
            }
            it.Min = new[] { xy.Where((_, i) => i % 2 == 0).Min(), xy.Where((_, i) => i % 2 == 1).Min(), 0 };
            it.Max = new[] { xy.Where((_, i) => i % 2 == 0).Max(), xy.Where((_, i) => i % 2 == 1).Max(), 0 };
            return it;
        }

        // ------------------------------------------------------------ KTH-15 / KTH-16: closed polylines

        [Fact]
        public void Kth15_a_room_drawn_the_other_way_or_from_another_corner_is_the_same()
        {
            var ccw = Pline("A", true, 0, 0, 4000, 0, 4000, 3000, 0, 3000);
            var cw = Pline("B", true, 0, 0, 0, 3000, 4000, 3000, 4000, 0);
            var other = Pline("C", true, 4000, 0, 4000, 3000, 0, 3000, 0, 0);
            Assert.Equal(ccw.Nums, cw.Nums);
            Assert.Equal(ccw.Nums, other.Nums);
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { ccw }, new List<CmpItem> { cw }, "geometry", 1));
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { ccw }, new List<CmpItem> { other }, "geometry", 1));
        }

        [Fact]
        public void Kth15_the_bulges_follow_the_vertices_when_the_loop_is_turned_round()
        {
            // a room with one arched side (bulge 0.5 on the side 4000,0 → 4000,3000), drawn the other way from another corner
            var v1 = new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, 3000 }, new[] { 0.0, 3000 } };
            var b1 = new List<double> { 0, 0.5, 0, 0 };
            var v2 = new List<double[]> { new[] { 4000.0, 3000 }, new[] { 4000.0, 0 }, new[] { 0.0, 0 }, new[] { 0.0, 3000 } };
            var b2 = new List<double> { -0.5, 0, 0, 0 }; // the same side read backwards
            var a = DrawingCompareLogic.CanonicalLoop(v1, b1, 100);
            var b = DrawingCompareLogic.CanonicalLoop(v2, b2, 100);
            Assert.Equal(a.nums, b.nums);
            Assert.Equal(a.bulges, b.bulges);
            Assert.Equal(100, a.nums.Last());
            Assert.Equal(new[] { 0.0, 0.5, 0, 0 }, a.bulges);
        }

        [Fact]
        public void Kth16_a_stretched_room_is_one_change_not_removed_and_added()
        {
            var old = Pline("A", true, 0, 0, 4000, 0, 4000, 3000, 0, 3000);
            var now = Pline("B", true, 0, 0, 5000, 0, 5000, 3000, 0, 3000);
            var rows = DrawingCompareLogic.Compare(new List<CmpItem> { old }, new List<CmpItem> { now }, "geometry", 1);
            var row = Assert.Single(rows);
            Assert.Equal("changed", row.Change);
            Assert.StartsWith("geometry", row.What[0]);
            // an open wall lengthened at its far end too
            var w0 = Pline("C", false, 0, 0, 4000, 0, 4000, 3000);
            var w1 = Pline("D", false, 0, 0, 4500, 0, 4500, 3000);
            Assert.Equal("changed", Assert.Single(DrawingCompareLogic.Compare(new List<CmpItem> { w0 }, new List<CmpItem> { w1 }, "geometry", 1)).Change);
            Assert.Contains("it.AnchorAt = new[] { it.Nums[0], it.Nums[1], it.Nums[it.Nums.Length - 1] };", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs"));
        }

        // ------------------------------------------------------------ KTH-28: near-equal x of two vertices

        [Fact]
        public void Kth28_two_vertices_with_nearly_the_same_x_still_match()
        {
            var a = Pline("A", true, 0, 0, 4000, 0, 4000, 3000, 0.3, 3000);
            var b = Pline("B", true, 0.3, 0, 4000, 0, 4000, 3000, 0, 3000);
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { a }, new List<CmpItem> { b }, "geometry", 1));
            // drawn the other way round and from another corner as well
            var c = Pline("C", true, 4000, 3000, 4000, 0, 0.3, 0, 0, 3000);
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { a }, new List<CmpItem> { c }, "geometry", 1));
            // 1.5 mm off: a change of shape, found by its anchor
            var d = Pline("D", true, 1.5, 0, 4000, 0, 4000, 3000, 0, 3000);
            var r = DrawingCompareLogic.Compare(new List<CmpItem> { b }, new List<CmpItem> { d }, "geometry", 1);
            Assert.NotEmpty(r);
            Assert.DoesNotContain(r, x => x.Change == "changed" && x.What.Count == 0);
        }

        [Fact]
        public void Kth28_a_room_read_from_metres_with_rounding_noise_matches_the_one_in_millimetres()
        {
            // the old file in metres (× 1000 after the unit factor, with the float noise of the conversion)
            double f = 1000;
            var m = new[] { 0.0, 0, 4.0000003, 0, 4.0000003, 2.9999998, 0.0000004, 2.9999998 }.Select(v => v * f).ToArray();
            var mm = Pline("MM", true, 0, 0, 4000, 0, 4000, 3000, 0, 3000);
            var fromM = Pline("M", true, m);
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { fromM }, new List<CmpItem> { mm }, "geometry", 1));
        }

        [Fact]
        public void Kth28_same_loop_handles_bulges_both_ways_and_refuses_other_shapes()
        {
            // a room with one arched side; the same room from another corner, the other way round
            var a = new CmpItem(); DrawingCompareLogic.SetLoop(a, new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, 3000 }, new[] { 0.3, 3000 } }, new List<double> { 0, 0.5, 0, 0 }, 0);
            var b = new CmpItem(); DrawingCompareLogic.SetLoop(b, new List<double[]> { new[] { 4000.0, 3000 }, new[] { 4000.0, 0 }, new[] { 0.3, 0 }, new[] { 0.0, 3000 } }, new List<double> { -0.5, 0, 0, 0 }, 0);
            Assert.True(DrawingCompareLogic.SameLoop(a.Loop, a.LoopBulges, b.Loop, b.LoopBulges, 1));
            var c = new CmpItem(); DrawingCompareLogic.SetLoop(c, new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, 3000 }, new[] { 0.0, 3000 } }, new List<double> { 0, 0, 0.5, 0 }, 0);
            Assert.False(DrawingCompareLogic.SameLoop(a.Loop, a.LoopBulges, c.Loop, c.LoopBulges, 1), "the arch on another side");
            Assert.False(DrawingCompareLogic.SameLoop(a.Loop, a.LoopBulges, null, null, 1));
            Assert.True(DrawingCompareLogic.SameLoop(null, null, null, null, 1));
            Assert.Equal("c|4", a.Exact);
            Assert.Equal(3, a.Nums.Length); // centre + elevation: the cell it is found by
        }

        // ------------------------------------------------------------ KTH-17: angles within the tolerance

        [Fact]
        public void Kth17_a_turn_is_compared_where_it_shows()
        {
            CmpItem T(double rot) => new CmpItem
            {
                Handle = "T", Kind = "text", Layer = "A", Nums = new[] { 0.0, 0, 0, 250 }, Angles = new Dictionary<string, double> { ["rotation"] = rot }, Reach = 2500,
                Min = new[] { 0.0, 0, 0 }, Max = new[] { 2500.0, 250, 0 },
            };
            Assert.True(DrawingCompareLogic.SameAngles(T(0), T(0.0001), 1));    // 0.25 mm at the far end
            Assert.False(DrawingCompareLogic.SameAngles(T(0), T(0.001), 1));    // 2.5 mm
            Assert.True(DrawingCompareLogic.SameAngles(T(2 * Math.PI - 0.00001), T(0), 1), "0 and 360° are one turn");
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { T(0) }, new List<CmpItem> { T(0.0001) }, "geometry", 1));
            var r = Assert.Single(DrawingCompareLogic.Compare(new List<CmpItem> { T(0) }, new List<CmpItem> { T(0.5) }, "geometry", 1));
            Assert.Equal("changed", r.Change);
            // an arc by its ends within the tolerance, not its angles as text
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("Concat(P(a.StartPoint)).Concat(P(a.EndPoint))", t);
            Assert.DoesNotContain("R(a.StartAngle)", t);
            Assert.DoesNotContain("it.Exact = R(t.Rotation)", t);
            Assert.Contains("TurnedBy(it, br.Rotation);", t);
        }

        // ------------------------------------------------------------ KTH-18 / 19 / 20: what was left out

        [Fact]
        public void Kth18_19_20_dynamic_blocks_mleaders_tables_splines_hatch_and_dim_style_are_read()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("br.DynamicBlockReferencePropertyCollection", t);
            Assert.Contains("it.Props[\"dynamic\"]", t);
            Assert.Contains("case MLeader ml:", t);
            Assert.Contains("ml.MText.Contents", t);
            int table = t.IndexOf("case Table tb:", StringComparison.Ordinal);
            int block = t.IndexOf("case BlockReference br:", StringComparison.Ordinal);
            Assert.True(table > 0 && block > table, "a table is a block reference: read before it");
            Assert.Contains("tb.Cells[r, c].TextString", t);
            Assert.Contains("sp.GetControlPointAt(i)", t);
            Assert.Contains("case Ellipse el:", t);
            Assert.Contains("case Leader ld:", t);
            Assert.Contains("it.Areas[\"pattern_scale\"]", t);
            Assert.Contains("it.Props[\"pattern_angle\"]", t);
            Assert.Contains("it.Props[\"dim_style\"] = d.DimensionStyleName;", t);
            // a pattern scale is compared as a share (like an area), not to the tolerance in mm
            CmpItem H(double s) => new CmpItem { Handle = "H", Kind = "hatch", Layer = "HA", Nums = new[] { 0.0, 0, 0, 1, 1, 0 }, Areas = new Dictionary<string, double> { ["pattern_scale"] = s } };
            Assert.Empty(DrawingCompareLogic.Differences(H(50), H(50.00001), 1, true));
            Assert.Single(DrawingCompareLogic.Differences(H(50), H(100), 1, true));
        }

        // ------------------------------------------------------------ KTH-21 / KTH-22: clouds

        [Fact]
        public void Kth21_more_than_5000_differences_has_its_own_message_and_number()
        {
            Assert.Equal(5000, H3Numbers.MaxRowsForClouds);
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("rows.Count > H3Numbers.MaxRowsForClouds ? null", t);
            Assert.Contains("more than {H3Numbers.MaxRowsForClouds}): nothing was drawn", t);
            Assert.Contains("({clouds.Count} clouds, more than {H3Numbers.MaxClouds})", t);
            Assert.DoesNotContain("H3Numbers.MaxClouds * 10", t);
        }

        [Fact]
        public void Kth22_a_moved_object_is_ringed_where_it_was_and_where_it_is()
        {
            var o = new CmpItem { Handle = "1", Kind = "circle", Layer = "A", Nums = new[] { 0.0, 0, 0, 500 }, Min = new[] { -500.0, -500, 0 }, Max = new[] { 500.0, 500, 0 } };
            var far = new CmpItem { Handle = "1", Kind = "circle", Layer = "A", Nums = new[] { 50000.0, 0, 0, 500 }, Min = new[] { 49500.0, -500, 0 }, Max = new[] { 50500.0, 500, 0 } };
            var rows = DrawingCompareLogic.Compare(new List<CmpItem> { o }, new List<CmpItem> { far }, "handle", 1);
            var row = Assert.Single(rows);
            Assert.Equal(-500, row.OldMin[0]);
            var clouds = DrawingCompareLogic.Clouds(rows, 1);
            Assert.Equal(2, clouds.Count);
            Assert.Contains(clouds, c => c.Min[0] < 0 && c.Max[0] < 1000);
            Assert.Contains(clouds, c => c.Min[0] > 40000);
            Assert.Equal(1, clouds.Sum(c => c.Count)); // one difference
            // moved a little: one cloud round both
            var near = new CmpItem { Handle = "1", Kind = "circle", Layer = "A", Nums = new[] { 300.0, 0, 0, 500 }, Min = new[] { -200.0, -500, 0 }, Max = new[] { 800.0, 500, 0 } };
            Assert.Single(DrawingCompareLogic.Clouds(DrawingCompareLogic.Compare(new List<CmpItem> { o }, new List<CmpItem> { near }, "handle", 1), 1));
        }

        // ------------------------------------------------------------ KTH-23: FBX keeps far coordinates

        [Fact]
        public void Kth23_far_corners_keep_their_place_in_double()
        {
            var shift = new[] { -500000000.0, -2000000.0, 0 };
            // the STL corner near the origin after the move (float is exact there)
            var tri = new float[] { 0, 0, 1, 12.3f, 0, 0, 1000, 0, 0, 0, 1000, 0 };
            var back = ExportLogic.Unshift(tri, shift);
            Assert.Equal(500000012.3, back[3], 3); // float would give 500000000 (steps of 32 mm there)
            Assert.Equal(2000000, back[4], 6);
            var mesh = FbxWriter.FromTriangles("x", new[] { back });
            Assert.Equal(500000012.3, mesh.Vertices[0][0], 3);
            Assert.Contains("ExportLogic.Unshift(t, shift)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExportTools.cs"));
            Assert.DoesNotContain("(float)(t[3 + k * 3 + a] - shift[a])", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExportTools.cs"));
        }

        // ------------------------------------------------------------ KTH-24 / KTH-25: skills

        [Fact]
        public void Kth24_the_bundle_itself_carries_the_waiting_skills()
        {
            var mb = Src("bundle", "make-bundle.mjs");
            Assert.Contains("path.join(contents, 'skills-ban-moi')", mb);
            var iss = Src("installer", "ClaudeCAD.iss");
            Assert.Contains("Excludes: \"Contents\\skills\\*,Contents\\skills-ban-moi\\*\"", iss);
            // the old update script mirrors everything but Contents\skills: skills-ban-moi comes along
            var script = UpdateLogic.ApplyScript(1, @"C:\staged\ClaudeCAD.bundle", @"C:\app\ClaudeCAD.bundle", @"C:\log.txt");
            Assert.Contains("/XD", script);
            Assert.DoesNotContain(@"/XD ""C:\staged\ClaudeCAD.bundle\Contents\skills-ban-moi""", script);
        }

        private void Put(string dir, string rel, string text)
        {
            var p = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text, new UTF8Encoding(false));
        }

        [Fact]
        public void Kth25_a_skill_the_user_deleted_stays_deleted_until_a_new_version_comes()
        {
            var staged = Path.Combine(_root, SkillSync.StagedFolder);
            var skills = Path.Combine(_root, "skills");
            Put(staged, "ve-mep/SKILL.md", "mep v1");
            Put(staged, "ve-kien-truc/SKILL.md", "kt v1");
            SkillSync.Sync(staged, skills, new HashSet<string>());
            Directory.Delete(Path.Combine(skills, "ve-mep"), true); // the user removes the skill
            var r = SkillSync.Sync(staged, skills, new HashSet<string>());
            Assert.False(File.Exists(Path.Combine(skills, "ve-mep", "SKILL.md")), "not brought back at the next start");
            Assert.Equal(new[] { "ve-mep/SKILL.md" }, r.LeftDeleted);
            Assert.Empty(r.Added);
            SkillSync.Sync(staged, skills, new HashSet<string>());
            Assert.False(File.Exists(Path.Combine(skills, "ve-mep", "SKILL.md")));
            // a new version of it: it comes back (new content the user has not seen)
            Put(staged, "ve-mep/SKILL.md", "mep v2");
            var r2 = SkillSync.Sync(staged, skills, new HashSet<string>());
            Assert.Equal("mep v2", File.ReadAllText(Path.Combine(skills, "ve-mep", "SKILL.md")));
            Assert.Contains("ve-mep/SKILL.md", r2.Added);
            // a machine that never had it gets it at once
            var skills2 = Path.Combine(_root, "skills2");
            Assert.Equal(2, SkillSync.Sync(staged, skills2, new HashSet<string>()).Added.Count);
        }

        // ------------------------------------------------------------ compare is not declared read-only (it may draw)

        [Fact]
        public void Compare_is_not_declared_read_only_for_the_assistant()
        {
            var ts = Src("agent", "src", "tools", "compare.ts");
            Assert.Contains("{ readOnly: false }", ts);
            Assert.DoesNotContain("{ readOnly: true }", ts);
        }
    }
}
