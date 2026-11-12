using System;
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
        public void Only_existing_solids_without_the_yes_are_refused()
        {
            var items = new[]
            {
                ("2f4a", true, false),  // a solid from before this turn (or an earlier turn of Claude's)
                ("2F4B", true, true),   // built in this turn
                ("2F4C", false, false), // a block / polyline: the usual > 20 rule only
                ("2F4A", true, false),  // the same solid twice
            };
            Assert.Equal(new[] { "2F4A" }, SolidLogic.ExplodeNeedsYes(items, false));
            Assert.Empty(SolidLogic.ExplodeNeedsYes(items, true));
            Assert.Empty(SolidLogic.ExplodeNeedsYes(new[] { ("1", false, false), ("2", true, true) }, false));
            Assert.Empty(SolidLogic.ExplodeNeedsYes(null, false));
        }

        [Fact]
        public void The_refusal_reads_like_the_assistants_gate()
        {
            var m = SolidLogic.ExplodeConfirmMessage(Enumerable.Range(0, 25).Select(i => "A" + i).ToList());
            Assert.StartsWith("Confirmation required:", m); // the assistant marks the turn as asking on this prefix
            Assert.Contains("25 3D solid", m);
            Assert.Contains("request_confirmation", m);
            Assert.Contains("Nothing was exploded", m);
            Assert.Contains("A19, …", m); // at most 20 handles listed
            Assert.DoesNotContain("A20", m);
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
        public void Explode_checks_the_type_and_the_turn_before_breaking_anything_up()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            var body = s.Substring(s.IndexOf("private static object ExplodeEntities", StringComparison.Ordinal));
            body = body.Substring(0, body.IndexOf("private static object JoinEntities", StringComparison.Ordinal));
            int check = body.IndexOf("SolidLogic.ExplodeNeedsYes", StringComparison.Ordinal);
            Assert.True(check > 0, "explode_entities does not check 3D solids");
            Assert.True(check < body.IndexOf(".Explode(", StringComparison.Ordinal), "the check must run before anything is exploded");
            Assert.True(check < body.IndexOf(".Erase()", StringComparison.Ordinal), "the check must run before anything is erased");
            Assert.Contains("p.Bool(SolidLogic.UserConfirmedParam, false)", body);
            Assert.Contains("ctx.CreatedThisTurn(h)", body);
            Assert.Contains("SolidLogic.ExplodeConfirmMessage(need)", body);
            // the type test walks the DatabaseServices base types, so every Surface subclass counts
            Assert.Matches(new Regex(@"t\.BaseType"), body);
            Assert.Contains("\"Autodesk.AutoCAD.DatabaseServices\"", body);
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
