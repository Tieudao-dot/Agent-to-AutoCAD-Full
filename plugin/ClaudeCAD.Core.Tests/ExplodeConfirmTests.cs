using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// explode_entities on 3D solids (decided 12/11/2026, docs/ban-giao/test-tong-the.md): a solid / surface / mesh that
    /// existed before the turn is broken up only after the user's yes; one built in this turn, and every 2D object, keep
    /// the old rule. The rule (Core) is unit-tested; the plugin wiring that needs AutoCAD is checked in the sources.
    /// </summary>
    public class ExplodeConfirmTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Theory]
        [InlineData("Solid3d")]
        [InlineData("Body")]
        [InlineData("SubDMesh")]
        [InlineData("PolyFaceMesh")]
        [InlineData("PolygonMesh")]
        public void Solids_bodies_and_meshes_need_the_yes(string type) =>
            Assert.True(SolidLogic.IsExplodeConfirmType(new[] { type }));

        [Fact]
        public void Every_surface_kind_needs_the_yes_through_its_base_type()
        {
            // the plugin passes the type and its DatabaseServices base types: NurbSurface → Surface
            foreach (var t in new[] { "PlaneSurface", "NurbSurface", "ExtrudedSurface", "LoftedSurface", "RevolvedSurface", "SweptSurface" })
                Assert.True(SolidLogic.IsExplodeConfirmType(new[] { t, "Surface" }), t);
        }

        [Fact]
        public void Two_D_objects_keep_the_old_rule()
        {
            foreach (var chain in new[]
            {
                new[] { "BlockReference" }, new[] { "Polyline", "Curve" }, new[] { "Polyline2d", "Curve" }, new[] { "Polyline3d", "Curve" },
                new[] { "Region" }, new[] { "Hatch" }, new[] { "AlignedDimension", "Dimension" }, new[] { "MText" }, new string[0],
            })
                Assert.False(SolidLogic.IsExplodeConfirmType(chain), string.Join("/", chain));
            Assert.False(SolidLogic.IsExplodeConfirmType(null));
        }

        [Fact]
        public void Only_existing_solids_not_on_the_approved_card_are_refused()
        {
            var items = new[]
            {
                ("2f4a", true, false),  // a solid from before this turn (or an earlier turn of Claude's)
                ("2F4B", true, true),   // built in this turn
                ("2F4C", false, false), // a block / polyline: the usual > 20 rule only
                ("2F4A", true, false),  // the same solid twice
                ("2F4D", true, false),  // another older solid
            };
            Assert.Equal(new[] { "2F4A", "2F4D" }, SolidLogic.SolidsNeedingCard(items, null));
            // C2: the card's Đồng ý approves exactly its handles
            Assert.Equal(new[] { "2F4D" }, SolidLogic.SolidsNeedingCard(items, SolidLogic.ConfirmedHandles(new List<object> { "2f4a" })));
            Assert.Empty(SolidLogic.SolidsNeedingCard(items, new[] { "2F4A", "2f4d" }));
            Assert.Empty(SolidLogic.SolidsNeedingCard(new[] { ("1", false, false), ("2", true, true) }, null));
            Assert.Empty(SolidLogic.SolidsNeedingCard(null, null));
        }

        [Fact]
        public void The_approved_handles_come_only_as_a_list()
        {
            Assert.Equal(new[] { "2F4A", "2F4B" }, SolidLogic.ConfirmedHandles(new List<object> { "2f4a", " 2F4B ", 7, "", null }).OrderBy(x => x).ToArray());
            Assert.Empty(SolidLogic.ConfirmedHandles(true));     // the old boolean flag unlocks nothing
            Assert.Empty(SolidLogic.ConfirmedHandles("2F4A"));   // a string is not a list
            Assert.Empty(SolidLogic.ConfirmedHandles(null));
            Assert.Equal(new[] { "explode_entities", "erase_entities" }, SolidLogic.CardConfirmTools);
        }

        [Fact]
        public void The_refusal_reads_like_the_assistants_gate()
        {
            var m = SolidLogic.CardConfirmMessage("explode_entities", Enumerable.Range(0, 25).Select(i => "A" + i).ToList());
            Assert.StartsWith("Confirmation required:", m); // the assistant marks the turn as asking on this prefix
            Assert.Contains("25 3D solid", m);
            Assert.Contains("request_confirmation", m);
            Assert.Contains("Nothing was exploded", m);
            Assert.Contains("Đồng ý button", m);
            Assert.Contains("A19, …", m); // at most 20 handles listed
            Assert.DoesNotContain("A20", m);
            var e = SolidLogic.CardConfirmMessage("erase_entities", new[] { "2F4A" });
            Assert.Contains("would erase 1 3D solid", e);
            Assert.Contains("Nothing was erased", e);
        }

        [Fact]
        public void A_new_object_is_known_until_it_is_erased_and_the_tracker_errs_on_the_safe_side()
        {
            var t = new ChangeTracker();
            t.Appended("2F4B");
            Assert.True(t.WasAdded("2f4b"));
            Assert.False(t.WasAdded("2F4A"));
            Assert.False(t.WasAdded(null));
            t.Erased("2F4B", true);
            Assert.False(t.WasAdded("2F4B"));
            t.Clear();
            Assert.False(t.WasAdded("2F4B"));
        }

        [Fact]
        public void Explode_and_erase_check_the_type_and_the_turn_before_changing_anything()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            string Body(string head, string next)
            {
                var b = s.Substring(s.IndexOf(head, StringComparison.Ordinal));
                return b.Substring(0, b.IndexOf(next, StringComparison.Ordinal));
            }
            var explode = Body("private static object ExplodeEntities", "private static void RequireCardForOlderSolids");
            int check = explode.IndexOf("RequireCardForOlderSolids(ctx, p, \"explode_entities\", hs);", StringComparison.Ordinal);
            Assert.True(check > 0, "explode_entities does not check 3D solids");
            Assert.True(check < explode.IndexOf(".Explode(", StringComparison.Ordinal), "the check must run before anything is exploded");
            Assert.True(check < explode.IndexOf(".Erase()", StringComparison.Ordinal), "the check must run before anything is erased");
            // C1 (KT-12): erase_entities too, before it opens anything for write
            var erase = Body("private static object EraseEntities", "private static object SetEntityProps");
            int echeck = erase.IndexOf("RequireCardForOlderSolids(ctx, p, \"erase_entities\", hs);", StringComparison.Ordinal);
            Assert.True(echeck > 0 && echeck < erase.IndexOf("OpenForWrite(", StringComparison.Ordinal), "erase_entities must check 3D solids first");
            var req = Body("private static void RequireCardForOlderSolids", "private static bool IsExplodeConfirmEntity");
            Assert.Contains("SolidLogic.ConfirmedHandles(p.Get(SolidLogic.UserConfirmedParam))", req);
            Assert.Contains("ctx.CreatedThisTurn(h)", req);
            Assert.Contains("SolidLogic.SolidsNeedingCard(items, confirmed)", req);
            Assert.Contains("SolidLogic.CardConfirmMessage(tool, need)", req);
            // the type test walks the DatabaseServices base types, so every Surface subclass counts
            var type = Body("private static bool IsExplodeConfirmEntity", "private static object JoinEntities");
            Assert.Matches(new Regex(@"t\.BaseType"), type);
            Assert.Contains("\"Autodesk.AutoCAD.DatabaseServices\"", type);
        }

        /// <summary>KT-10 (Kiểm tra độc lập; test copied from full/kiem-tra-doc-lap): a saved macro never keeps the card's yes.</summary>
        [Fact]
        public void KT10_macro_recorder_drops_user_confirmed()
        {
            var rec = new MacroRecorder();
            rec.Record("explode_entities", new Dictionary<string, object>
            {
                ["handles"] = new List<object> { "2F4A" },
                [SolidLogic.UserConfirmedParam] = new List<object> { "2F4A" },
            }, null);
            var calls = rec.Take();
            Assert.False(calls[0].Params.ContainsKey(SolidLogic.UserConfirmedParam), "the macro keeps user_confirmed: replay skips the confirmation");
            Assert.True(calls[0].Params.ContainsKey("handles"));
        }

        [Fact]
        public void KT10_a_macro_that_changes_solids_it_did_not_build_is_not_run()
        {
            var m = new Macro
            {
                Name = "khoet cua",
                Calls = new List<MacroCall>
                {
                    new MacroCall { Tool = "draw_box", Params = new Dictionary<string, object> { ["length"] = 900.0 }, ResultHandles = new List<string> { "B1" } },
                    // into a wall the macro did not build: refused
                    new MacroCall { Tool = "solid_subtract", Params = new Dictionary<string, object> { ["target"] = "2F4A", ["subtract"] = new List<object> { "B1" } }, ResultHandles = new List<string> { "2F4A" } },
                },
            };
            var ext = MacroLogic.ExternalSolidEdits(m);
            Assert.Single(ext);
            Assert.StartsWith("solid_subtract (2F4A)", ext[0]);
            Assert.Contains("không chạy", MacroLogic.ExternalSolidEditsMessage(ext));
            // a macro that builds its own wall and cuts it runs
            m.Calls.Insert(0, new MacroCall { Tool = "draw_box", Params = new Dictionary<string, object>(), ResultHandles = new List<string> { "2F4A" } });
            Assert.Empty(MacroLogic.ExternalSolidEdits(m));
            Assert.Subset(new HashSet<string> { "solid_union", "solid_subtract", "solid_intersect", "slice_solid", "fillet_edges", "chamfer_edges", "shell_solid" }, MacroLogic.AlwaysConfirmTools);
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("MacroLogic.ExternalSolidEdits(m)", c);
            Assert.True(c.IndexOf("MacroLogic.ExternalSolidEdits(m)", StringComparison.Ordinal) < c.IndexOf("_macroRunning = true;", StringComparison.Ordinal),
                "the macro is checked before it starts");
        }

        [Fact]
        public void The_card_button_tells_the_assistant_which_card_it_answers()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("if (confirmId != null) payload[\"confirm_id\"] = confirmId;", c);
            Assert.Contains("yes ? \"yes\" : \"no\", null, id)", c);
        }

        [Fact]
        public void The_turn_tracker_answers_created_this_turn()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("public bool CreatedThisTurn(string handle) => _busy && _changes.WasAdded(handle);", c);
            // a macro run is a turn too (StartTurn clears the tracker), so solids a macro builds can be exploded by it
            Assert.Contains("StartTurn(doc, null, entry, toAgent: false);", c);
            Assert.Contains("_changes.Clear();", c);
            var d = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            Assert.Contains("bool CreatedThisTurn(string handle);", d);
            Assert.Contains("_host != null && _host.CreatedThisTurn(handle)", d);
        }

        [Fact]
        public void The_flag_is_not_a_handle_parameter_and_matches_the_assistant()
        {
            Assert.False(MacroLogic.IsHandleKey(SolidLogic.UserConfirmedParam));
            var a = Src("agent", "src", "agent", "confirmation.ts");
            Assert.Contains($"USER_CONFIRMED_PARAM = '{SolidLogic.UserConfirmedParam}'", a);
            // Claude never sees the flag: not in any tool schema
            foreach (var f in Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agent", "src", "tools")), "*.ts"))
                Assert.DoesNotContain(SolidLogic.UserConfirmedParam, File.ReadAllText(f));
        }
    }
}
