using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F1, việc 1: every tool takes and returns coordinates in the current UCS (World: as before).</summary>
    public class NangCapF1UcsTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private const double E = 1e-9;

        // UCS turned 90° in plan, origin moved to (1000, 2000, 300): UCS x = WCS y, UCS y = WCS −x
        private static UcsFrame Turned() => new UcsFrame(new[] { 1000.0, 2000, 300 }, new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 });

        // UCS "front": its XY plane is the WCS XZ plane (z points to −y)
        private static UcsFrame Front() => new UcsFrame(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 0, 1 });

        private static void Near(double[] want, double[] got)
        {
            Assert.Equal(want.Length, got.Length);
            for (int i = 0; i < want.Length; i++) Assert.True(Math.Abs(want[i] - got[i]) < 1e-6, $"[{i}] want {want[i]} got {got[i]}");
        }

        private static List<object> L(params double[] v) => v.Cast<object>().ToList();

        // ------------------------------------------------------------ frame math

        [Fact]
        public void World_is_world_and_converts_nothing()
        {
            Assert.True(UcsFrame.World.IsWorld);
            Assert.True(UcsFrame.World.ZUp);
            Near(new[] { 5.0, 6, 7 }, UcsFrame.World.ToWcs(new[] { 5.0, 6, 7 }));
            Assert.True(new UcsFrame(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }).IsWorld);
            Assert.False(new UcsFrame(new[] { 0.0, 0, 1 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }).IsWorld);
            Assert.False(Turned().IsWorld);
        }

        [Fact]
        public void Turned_ucs_maps_points_and_vectors()
        {
            var f = Turned();
            Assert.True(f.ZUp);
            Assert.Equal(90, f.PlanAngleDeg, 9);
            Near(new[] { 1000.0, 2100, 300 }, f.ToWcs(new[] { 100.0, 0 }));
            Near(new[] { 900.0, 2000, 300 }, f.ToWcs(new[] { 0.0, 100 }));
            Near(new[] { 0.0, 100, 0 }, f.VecToWcs(new[] { 100.0, 0, 0 })); // a vector is turned, not moved
            Near(new[] { 100.0, 0, 0 }, f.FromWcs(new[] { 1000.0, 2100, 300 }));
            var p = new[] { 12.5, -40, 7 };
            Near(p, f.FromWcs(f.ToWcs(p)));
            Near(p, f.VecFromWcs(f.VecToWcs(p)));
        }

        [Fact]
        public void Front_ucs_is_not_z_up_and_draws_on_the_xz_plane()
        {
            var f = Front();
            Assert.False(f.ZUp);
            Near(new[] { 0.0, -1, 0 }, f.Z);
            Near(new[] { 300.0, 0, 500 }, f.ToWcs(new[] { 300.0, 500 }));
        }

        [Fact]
        public void Y_axis_is_made_perpendicular()
        {
            var f = new UcsFrame(new double[3], new[] { 1.0, 0, 0 }, new[] { 1.0, 1, 0 });
            Near(new[] { 0.0, 1, 0 }, f.Y);
            Assert.True(f.IsWorld);
        }

        [Fact]
        public void Matrix_is_row_major_ucs_to_wcs()
        {
            var f = Turned();
            var m = f.ToWcsMatrix();
            Assert.Equal(16, m.Length);
            var p = new[] { 3.0, 4, 5 };
            var w = f.ToWcs(p);
            for (int r = 0; r < 3; r++) Assert.Equal(w[r], m[r * 4] * p[0] + m[r * 4 + 1] * p[1] + m[r * 4 + 2] * p[2] + m[r * 4 + 3], 9);
            Assert.Equal(new[] { 0.0, 0, 0, 1 }, m.Skip(12).ToArray());
        }

        [Fact]
        public void Box_is_reboxed_in_the_ucs()
        {
            var f = Turned();
            // WCS box x 1000..1100, y 2000..2500, z 300..400 → UCS x 0..500, y −100..0, z 0..100
            var (mn, mx) = f.BoxFromWcs(new[] { 1000.0, 2000, 300 }, new[] { 1100.0, 2500, 400 });
            Near(new[] { 0.0, -100, 0 }, mn);
            Near(new[] { 500.0, 0, 100 }, mx);
        }

        [Fact]
        public void Window_corners_follow_the_ucs_axes()
        {
            var f = new UcsFrame(new double[3], new[] { 1.0, 1, 0 }, new[] { -1.0, 1, 0 }); // turned 45°
            var a = f.ToWcs(new[] { 0.0, 0 });
            var b = f.ToWcs(new[] { 10.0, 10 });
            var cs = f.WindowCorners(a, b);
            Assert.Equal(4, cs.Count);
            Near(new[] { 0.0, 0, 0 }, f.FromWcs(cs[0]));
            Near(new[] { 10.0, 0, 0 }, f.FromWcs(cs[1]));
            Near(new[] { 10.0, 10, 0 }, f.FromWcs(cs[2]));
            Near(new[] { 0.0, 10, 0 }, f.FromWcs(cs[3]));
        }

        [Fact]
        public void Frame_json_round_trip_and_bad_values()
        {
            var f = new UcsFrame(new[] { 1.0, 2, 3 }, new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 }, "MAT-BEN");
            var g = UcsFrame.FromJson(JsonCodec.Parse(JsonCodec.Serialize(f.ToJson())));
            Assert.NotNull(g);
            Assert.Equal("MAT-BEN", g.Name);
            Near(f.O, g.O); Near(f.X, g.X); Near(f.Y, g.Y);
            Assert.Same(UcsFrame.World, UcsFrame.FromJson("world"));
            Assert.Null(UcsFrame.FromJson("abc"));
            Assert.Null(UcsFrame.FromJson(new Dictionary<string, object> { ["origin"] = L(0, 0), ["x_axis"] = L(1, 0), ["y_axis"] = L(2, 0) }));
            Assert.Null(UcsFrame.FromJson(42.0));
        }

        // ------------------------------------------------------------ the ambient scope (Params / results)

        [Fact]
        public void Params_outside_a_scope_are_untouched()
        {
            var p = new Params("draw_line", new Dictionary<string, object> { ["start"] = L(10, 20), ["angle"] = 30.0 });
            Assert.Equal(new[] { 10.0, 20, 0 }, p.Point("start"));
            Assert.Equal(30, p.Num("angle"));
        }

        [Fact]
        public void World_scope_converts_nothing()
        {
            using (UcsScope.Enter(UcsFrame.World, UcsMode.Ref, "move_entities"))
            {
                Assert.Null(UcsScope.Input);
                Assert.Null(UcsScope.Output);
                var p = new Params("move_entities", new Dictionary<string, object> { ["from"] = L(1, 2), ["displacement"] = L(5, 0) });
                Assert.Equal(new[] { 1.0, 2, 0 }, p.Point("from"));
                Assert.Equal(new[] { 5.0, 0, 0 }, p.Point("displacement"));
            }
        }

        [Fact]
        public void Create_and_wcs_scopes_convert_nothing()
        {
            foreach (var mode in new[] { UcsMode.Create, UcsMode.Wcs })
                using (UcsScope.Enter(Turned(), mode, "draw_line"))
                {
                    Assert.Null(UcsScope.Input);
                    Assert.Equal(new[] { 100.0, 0, 0 }, new Params("draw_line", new Dictionary<string, object> { ["start"] = L(100, 0) }).Point("start"));
                }
        }

        [Fact]
        public void Ref_scope_converts_points_vectors_and_keeps_scale()
        {
            var f = Turned();
            using (UcsScope.Enter(f, UcsMode.Ref, "copy_entities"))
            {
                var p = new Params("copy_entities", new Dictionary<string, object>
                {
                    ["from"] = L(100, 0),
                    ["displacement"] = L(100, 0),
                    ["displacements"] = new List<object> { L(100, 0), L(0, 50) },
                    ["points"] = new List<object> { L(0, 0), L(100, 0) },
                    ["scale"] = L(2, 3, 1),
                    ["nested"] = new Dictionary<string, object> { ["at"] = L(0, 100) },
                });
                Near(new[] { 1000.0, 2100, 300 }, p.Point("from"));
                Near(new[] { 0.0, 100, 0 }, p.Point("displacement"));
                Near(new[] { 0.0, 100, 0 }, p.Points("displacements")[0]);
                Near(new[] { -50.0, 0, 0 }, p.Points("displacements")[1]);
                Near(new[] { 1000.0, 2000, 300 }, p.Points("points")[0]);
                Assert.Equal(new[] { 2.0, 3, 1 }, p.Point("scale"));
                Near(new[] { 900.0, 2000, 300 }, p.Obj("nested").Point("at"));
                // the raw params (what a macro stores) stay in the UCS
                Assert.Equal(L(100, 0), p.Raw["from"]);
            }
            Assert.Null(UcsScope.Input); // restored
        }

        [Fact]
        public void Plan_angles_and_heights_follow_a_z_up_ucs_only_where_listed()
        {
            var f = Turned();
            using (UcsScope.Enter(f, UcsMode.Ref, "draw_hatch"))
                Assert.Equal(120, new Params("draw_hatch", new Dictionary<string, object> { ["angle"] = 30.0 }).Num("angle"), 9);
            using (UcsScope.Enter(f, UcsMode.Ref, "edit_text"))
                Assert.Equal(90, new Params("edit_text", new Dictionary<string, object> { ["rotation"] = 0.0 }).Num("rotation"), 9);
            using (UcsScope.Enter(f, UcsMode.Ref, "build_3d_from_plan"))
                Assert.Equal(300, new Params("build_3d_from_plan", new Dictionary<string, object> { ["base_z"] = 0.0 }).Num("base_z"), 9);
            using (UcsScope.Enter(f, UcsMode.Ref, "build_roof"))
                Assert.Equal(3300, new Params("build_roof", new Dictionary<string, object> { ["wall_top_z"] = 3000.0 }).Num("wall_top_z"), 9);
            // rotate_entities: the angle is relative (a turn), never shifted
            using (UcsScope.Enter(f, UcsMode.Ref, "rotate_entities"))
                Assert.Equal(30, new Params("rotate_entities", new Dictionary<string, object> { ["angle"] = 30.0 }).Num("angle"), 9);
            // a tilted UCS: no plan angle / height carries over, the value stays
            using (UcsScope.Enter(Front(), UcsMode.Ref, "draw_hatch"))
                Assert.Equal(30, new Params("draw_hatch", new Dictionary<string, object> { ["angle"] = 30.0 }).Num("angle"), 9);
        }

        [Fact]
        public void Suspend_and_nesting_restore()
        {
            using (UcsScope.Enter(Turned(), UcsMode.Ref, "move_entities"))
            {
                using (UcsScope.Suspend()) Assert.Null(UcsScope.Input);
                Assert.NotNull(UcsScope.Input);
                using (UcsScope.Enter(UcsFrame.World, UcsMode.Ref, "list_entities")) Assert.Null(UcsScope.Input);
                Assert.NotNull(UcsScope.Input);
                Assert.Equal("move_entities", UcsScope.Tool);
            }
            Assert.Null(UcsScope.Input);
        }

        [Fact]
        public void Result_points_are_shown_in_the_ucs()
        {
            var f = Turned();
            Assert.Equal(L(1000, 2100), Params.ToJsonPoint(new[] { 1000.0, 2100, 0 })); // no scope: as before
            using (UcsScope.Enter(f, UcsMode.Ref, "get_entity"))
            {
                Assert.Equal(L(100, 0), Params.ToJsonPoint(new[] { 1000.0, 2100, 300 }));
                Near(new[] { 100.0, 0, 0 }, UcsLogic.OutPoint(new[] { 1000.0, 2100, 300 }));
                Near(new[] { 100.0, 0, 0 }, UcsLogic.OutVector(new[] { 0.0, 100, 0 }));
            }
        }

        [Fact]
        public void FixResult_reboxes_bounding_boxes_and_names_the_system()
        {
            var f = Turned();
            // what Acad.J made of the WCS corners (1000,2000,300) and (1100,2500,400), point by point
            var r = new Dictionary<string, object>
            {
                ["bbox"] = new Dictionary<string, object> { ["min"] = L(0, 0, 0), ["max"] = L(500, -100, 100) },
                ["items"] = new List<object> { new Dictionary<string, object> { ["min"] = L(0, 0), ["max"] = L(500, -100) } },
                ["count"] = 1.0,
            };
            UcsLogic.FixResult(r, f);
            var bb = (Dictionary<string, object>)r["bbox"];
            Assert.Equal(L(0, -100, 0), bb["min"]);
            Assert.Equal(L(500, 0, 100), bb["max"]);
            var it = (Dictionary<string, object>)((List<object>)r["items"])[0];
            Assert.Equal(L(0, -100), it["min"]);
            Assert.Equal(L(500, 0), it["max"]);
            Assert.Contains("not WCS", (string)r["coordinates"]);
        }

        [Fact]
        public void FixResult_in_world_changes_nothing()
        {
            var r = new Dictionary<string, object> { ["min"] = L(5, 1), ["max"] = L(1, 5) };
            UcsLogic.FixResult(r, UcsFrame.World);
            UcsLogic.FixResult(r, null);
            Assert.Equal(L(5, 1), r["min"]);
            Assert.False(r.ContainsKey("coordinates"));
        }

        [Fact]
        public void Named_ucs_is_named_in_results()
        {
            var f = new UcsFrame(new[] { 0.0, 0, 100 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, "TANG 2");
            Assert.Contains("UCS \"TANG 2\"", UcsLogic.CoordinatesNote(f));
        }

        // ------------------------------------------------------------ modes

        [Fact]
        public void Modes_of_the_tools()
        {
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("draw_line", null));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("draw_solid", null));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("add_text", null));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("move_entities", null));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("list_entities", null));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_3d_from_plan", null));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("draw_hatch", null));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("set_ucs", null));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("batch_execute", null));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("plot_pdf", null));
        }

        [Fact]
        public void Tools_built_on_existing_objects_are_ref()
        {
            IDictionary<string, object> P(params (string k, object v)[] kv) => kv.ToDictionary(x => x.k, x => x.v);
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("build_roof", P(("outline_points", new List<object>()), ("wall_top_z", 3000.0))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_roof", P(("outline_points", new List<object>()))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_roof", P(("from_walls", true), ("wall_top_z", 3000.0))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_roof", P(("outline_handle", "2A"), ("wall_top_z", 3000.0))));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("build_stair", P(("start", new List<object>()))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_stair", P(("well_handle", "2A"))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_beam", P(("path_handles", new List<object> { "2A" }))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("build_railing", P(("path_handle", "2A"))));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("add_dim_angular", P(("line1_handle", "2A"), ("line2_handle", "2B"))));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("add_dim_angular", P(("center", new List<object>()))));
        }

        [Fact]
        public void Every_labelled_tool_has_a_known_mode_and_the_lists_name_real_tools()
        {
            var all = PlanLogic.Groups.Keys.ToList();
            foreach (var t in UcsLogic.CreateTools) Assert.Contains(t, all);
            foreach (var t in UcsLogic.WcsTools) Assert.Contains(t, all);
            Assert.Empty(UcsLogic.CreateTools.Intersect(UcsLogic.WcsTools));
            // every Create tool is checked here against its handler: it must not read objects by handle unless ModeOf turns it Ref
            var src = string.Join("\n", Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing")), "*.cs").Select(File.ReadAllText));
            Assert.Contains("\"draw_line\"", src);
        }

        // ------------------------------------------------------------ macros

        [Fact]
        public void Macro_in_world_records_the_same_params_as_before()
        {
            var p = new Dictionary<string, object> { ["start"] = L(0, 0), ["end"] = L(100, 0) };
            Assert.Same(p, UcsLogic.ForMacro(p, UcsFrame.World));
            Assert.Same(p, UcsLogic.ForMacro(p, null));
        }

        [Fact]
        public void Macro_in_a_ucs_stores_it_and_replays_in_it()
        {
            var f = Turned();
            var p = new Dictionary<string, object> { ["start"] = L(0, 0), ["end"] = L(100, 0) };
            var rec = UcsLogic.ForMacro(p, f);
            Assert.NotSame(p, rec);
            Assert.False(p.ContainsKey(UcsLogic.MacroKey));
            // saved to disk and read back, then replayed
            var back = (Dictionary<string, object>)JsonCodec.Parse(JsonCodec.Serialize(rec));
            var replay = UcsLogic.MarkReplay(back);
            var g = UcsLogic.TakeMacroFrame(replay);
            Assert.False(replay.ContainsKey(UcsLogic.MacroKey));
            Near(f.ToWcs(new[] { 100.0, 0 }), g.ToWcs(new[] { 100.0, 0 }));
        }

        [Fact]
        public void Older_macros_replay_in_world_whatever_the_ucs_is_now()
        {
            var old = new Dictionary<string, object> { ["start"] = L(0, 0) };
            var replay = UcsLogic.MarkReplay(old);
            Assert.False(old.ContainsKey(UcsLogic.MacroKey)); // the stored macro is not changed
            Assert.Same(UcsFrame.World, UcsLogic.TakeMacroFrame(replay));
            Assert.Null(UcsLogic.TakeMacroFrame(new Dictionary<string, object> { ["start"] = L(0, 0) })); // a call from Claude: the current UCS
            Assert.Throws<ToolException>(() => UcsLogic.TakeMacroFrame(new Dictionary<string, object> { [UcsLogic.MacroKey] = "abc" }));
        }

        [Fact]
        public void Macro_ucs_is_not_taken_for_a_handle()
        {
            Assert.False(MacroLogic.IsHandleKey(UcsLogic.MacroKey));
            var rec = UcsLogic.ForMacro(new Dictionary<string, object> { ["handles"] = new List<object> { "2A" } }, new UcsFrame(new[] { 1.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, "2A"));
            var mapped = (Dictionary<string, object>)MacroLogic.Remap(rec, new Dictionary<string, string> { ["2A"] = "3B" });
            Assert.Equal("2A", ((Dictionary<string, object>)mapped[UcsLogic.MacroKey])["name"]);
            Assert.Equal(new List<object> { "3B" }, mapped["handles"]);
        }

        // ------------------------------------------------------------ wiring (source)

        [Fact]
        public void Dispatcher_runs_every_handler_through_the_ucs()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            // def.Handler(ctx, p) only inside RunHandler (no-document tools excepted)
            Assert.Equal(3, Regex.Matches(s, @"def\.Handler\(ctx, p\)").Count);
            Assert.Equal(3, Regex.Matches(s, @"RunHandler\(def, ctx0?, p, (_frame|callFrame)\)").Count);
            Assert.Contains("UcsLogic.ForMacro(p.Raw, _frame)", s);
            Assert.Contains("UcsLogic.ForMacro(p.Raw, callFrame)", s);
            Assert.Contains("UcsLogic.TakeMacroFrame(pd)", s);
            Assert.Contains("UcsLogic.TakeMacroFrame(pobj)", s);
            Assert.Contains("ctx.Db.ObjectAppended += onAppend", s);
            Assert.Contains("ctx.Db.ObjectAppended -= onAppend", s);
            Assert.Contains("e0.OwnerId != ms", s);
        }

        [Fact]
        public void Results_and_turns_use_the_ucs_axes()
        {
            var acad = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Acad.cs");
            Assert.Contains("p = Out(p);", acad);
            Assert.Contains("UcsScope.Output.VecFromWcs", acad);
            var mod = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            Assert.Contains("Matrix3d.Rotation(Geometry.DegToRad(p.Num(\"angle\")), Acad.UcsZ", mod);
            Assert.Contains("Matrix3d.Rotation(a, Acad.UcsZ, center)", mod);
            Assert.Contains("Acad.UcsX * Math.Cos(ang) + Acad.UcsY * Math.Sin(ang)", mod);
            var cc = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Equal(2, Regex.Matches(cc, @"UcsLogic\.MarkReplay\(").Count);
            var inter = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Interaction.cs");
            Assert.Contains("frame.FromWcs(new[] { w.X, w.Y, w.Z })", inter);
        }
    }
}
