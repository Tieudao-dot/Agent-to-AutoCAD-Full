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
    /// Test tổng thể (docs/ban-giao/test-tong-the.md): where the service plans, the macros and the three 3D phases
    /// meet. The handle parameters are read from the sources (the assistant's tool schemas and the plugin's
    /// Params reads), so a tool added later with a handle parameter the macro would not remap turns this red.
    /// </summary>
    public class CrossPlanTests
    {
        private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        private static IEnumerable<(string file, string text)> Files(string dir, string pattern) =>
            Directory.GetFiles(Path.Combine(Root, dir), pattern).Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

        /// <summary>Parameters typed as entity handles in the assistant's tool schemas (`name: handle` / `name: handles`).</summary>
        private static SortedSet<string> SchemaHandleKeys()
        {
            var rx = new Regex(@"\b([a-z0-9_]+)\??:\s*(?:handles?|z\.array\(\s*handle)\b");
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (_, text) in Files(Path.Combine("agent", "src", "tools"), "*.ts"))
                foreach (Match m in rx.Matches(text)) keys.Add(m.Groups[1].Value);
            return keys;
        }

        /// <summary>Parameters the plugin reads as handles (p.Handle / p.Handles, build_3d_from_plan's Explicit, solid_subtract's target).</summary>
        private static SortedSet<string> PluginHandleKeys()
        {
            var rx = new Regex(@"(?:\.Handles?\(|\bExplicit\()\s*""([a-z0-9_]+)""");
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (_, text) in Files(Path.Combine("plugin", "ClaudeCAD.Plugin", "Drawing"), "*.cs"))
                foreach (Match m in rx.Matches(text)) keys.Add(m.Groups[1].Value);
            return keys;
        }

        [Fact]
        public void Every_handle_parameter_of_every_tool_is_remapped_on_macro_replay()
        {
            var schema = SchemaHandleKeys();
            var plugin = PluginHandleKeys();
            // the scan itself works: the 3D handle parameters of the three phases are found
            foreach (var k in new[] { "target", "subtract", "profile_handles", "path_handle", "section_handles", "guide_handles", "other_handles",
                                      "wall_handles", "centerline_handles", "door_handles", "window_handles", "slab_handles", "handle", "handles" })
                Assert.True(schema.Contains(k), "schema scan missed " + k);
            Assert.Contains("wall_handles", plugin);
            Assert.Contains("section_handles", plugin);

            var notMapped = schema.Union(plugin).Where(k => !MacroLogic.IsHandleKey(k)).ToList();
            Assert.True(notMapped.Count == 0, "handle parameters a macro would replay with the recorded (old) handles: " + string.Join(", ", notMapped));
        }

        [Fact]
        public void Plugin_reads_handles_only_from_parameters_the_assistant_declares_as_handles()
        {
            var extra = PluginHandleKeys().Except(SchemaHandleKeys()).ToList();
            Assert.True(extra.Count == 0, "read as handles by the plugin but not typed handle in the assistant: " + string.Join(", ", extra));
        }

        [Fact]
        public void No_plain_parameter_looks_like_a_handle_parameter()
        {
            // a key IsHandleKey matches but whose value is not a handle would be rewritten on replay
            var rx = new Regex(@"^\s+([a-z0-9_]+)\??:\s*(.+)$", RegexOptions.Multiline);
            var bad = new List<string>();
            foreach (var (file, text) in Files(Path.Combine("agent", "src", "tools"), "*.ts"))
                foreach (Match m in rx.Matches(text))
                {
                    var k = m.Groups[1].Value;
                    if (!MacroLogic.IsHandleKey(k)) continue;
                    var v = m.Groups[2].Value.TrimStart();
                    // a TypeScript type annotation (interface field "handles?: string[]") is not a schema
                    if (!Regex.IsMatch(v, @"^(?:handles?\b|z\.array\(\s*handle|string\b)")) bad.Add($"{file}: {k}");
                }
            Assert.True(bad.Count == 0, "keys named like handles but typed otherwise: " + string.Join(", ", bad));
        }

        [Fact]
        public void Two_D_machine_is_refused_every_3D_tool_and_3D_machine_every_2D_group_tool()
        {
            var threeD = PlanLogic.Groups.Where(kv => kv.Value == ToolGroup.ThreeD).Select(kv => kv.Key).ToList();
            var twoD = PlanLogic.Groups.Where(kv => kv.Value == ToolGroup.TwoD).Select(kv => kv.Key).ToList();
            Assert.Equal(31, threeD.Count); // 14 + 12 + 3 tools of the three phases + draw_solid, build_railing (nâng cấp đợt A)
            foreach (var t in threeD)
            {
                Assert.False(PlanLogic.Allowed("2d", t), t);
                Assert.False(PlanLogic.Allowed(null, t), t); // a new machine (default plan 2D)
                Assert.True(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d3d", t), t);
            }
            foreach (var t in twoD)
            {
                Assert.False(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d", t), t);
            }
            Assert.Equal(PlanLogic.Groups.Keys.OrderBy(x => x, StringComparer.Ordinal), PlanLogic.AllowedTools("2d3d"));
            Assert.DoesNotContain(PlanLogic.AllowedTools("2d"), threeD.Contains);
            Assert.DoesNotContain(PlanLogic.AllowedTools("3d"), twoD.Contains);
        }

        [Fact]
        public void The_plan_gate_runs_for_a_single_call_and_for_every_call_of_a_batch()
        {
            // macros run through HandleAsync: one tool alone, or several as one batch_execute
            var s = File.ReadAllText(Path.Combine(Root, "plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs"));
            Assert.Equal(2, Regex.Matches(s, @"RequireInPlan\(tool\);").Count);
            var batch = s.Substring(s.IndexOf("internal List<object> ExecuteBatch", StringComparison.Ordinal));
            Assert.True(batch.IndexOf("RequireInPlan(tool);", StringComparison.Ordinal) < batch.IndexOf("def.BatchForbidden", StringComparison.Ordinal),
                "the plan is checked before 'cannot run inside a batch', so a 2D machine always gets NOT_IN_PLAN for a 3D tool");
            var macro = File.ReadAllText(Path.Combine(Root, "plugin", "ClaudeCAD.Plugin", "ChatController.cs"));
            Assert.Equal(2, Regex.Matches(macro, @"_dispatcher\.HandleAsync\(new Dictionary<string, object> \{ \[""id""\] = ""macro-""").Count);
        }
    }
}
