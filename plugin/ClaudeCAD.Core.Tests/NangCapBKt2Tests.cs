using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Đợt B, sửa sau kiểm tra độc lập nâng cấp (docs/ban-giao/kiem-tra-doc-lap-nang-cap.md): KT2-03 (layer tắt trong khung
    /// kéo giãn), KT2-15 (đối tượng vừa tạo không tính vào giới hạn 20), KT2-23 (bỏ nhóm theo handle khi đối tượng ở nhiều
    /// nhóm), KT2-24 (kéo dài đường nghiêng / thẳng đứng theo 3D). KT2-14, KT2-25: agent/test/nangcapb-kt2.test.ts.
    /// </summary>
    public class NangCapBKt2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Edit2DTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Edit2DTools.cs");

        private static string Body(string src, string head, string next)
        {
            var b = src.Substring(src.IndexOf(head, StringComparison.Ordinal));
            return b.Substring(0, b.IndexOf(next, StringComparison.Ordinal));
        }

        // ------------------------------------------------------------ KT2-03

        [Theory]
        [InlineData(false, false, false, Edit2DLogic.WindowLayer.Take)]
        [InlineData(true, false, false, Edit2DLogic.WindowLayer.Hidden)]
        [InlineData(false, true, false, Edit2DLogic.WindowLayer.Hidden)]  // turned off: not selected (KT2-03)
        [InlineData(false, true, true, Edit2DLogic.WindowLayer.Hidden)]   // off and locked: hidden wins, nothing to report
        [InlineData(false, false, true, Edit2DLogic.WindowLayer.Locked)]
        [InlineData(true, true, true, Edit2DLogic.WindowLayer.Hidden)]
        public void KT2_03_window_skips_frozen_and_turned_off_layers(bool frozen, bool off, bool locked, Edit2DLogic.WindowLayer expected) =>
            Assert.Equal(expected, Edit2DLogic.WindowLayerRule(frozen, off, locked));

        [Fact]
        public void KT2_03_stretch_window_uses_the_layer_rule_with_IsOff()
        {
            var body = Body(Edit2DTools, "internal static object StretchEntities", "// ------------------------------------------------------------------ BREAK");
            Assert.Contains("Edit2DLogic.WindowLayerRule(ltr.IsFrozen, ltr.IsOff, ltr.IsLocked)", body);
            Assert.Contains("hidden_layer_skipped", body);
            Assert.DoesNotContain("if (ltr.IsFrozen) continue;", body);
        }

        // ------------------------------------------------------------ KT2-15

        [Fact]
        public void KT2_15_objects_created_this_turn_do_not_count_towards_the_limit()
        {
            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A1", "b2" };
            Assert.Equal(1, Edit2DLogic.CountedForLimit(new[] { "A1", "B2", "C3" }, created.Contains));
            // 25 lines drawn in this turn, stretched by a window: nothing to ask about
            var drawn = Enumerable.Range(0, 25).Select(i => "D" + i.ToString("X")).ToList();
            var all = new HashSet<string>(drawn, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(0, Edit2DLogic.CountedForLimit(drawn, all.Contains));
            Assert.True(Edit2DLogic.CountedForLimit(drawn, all.Contains) <= Edit2DLogic.WindowLimit(null));
            // no host (self-test): everything counts, the safe side
            Assert.Equal(3, Edit2DLogic.CountedForLimit(new[] { "A1", "B2", "C3" }, null));
        }

        [Fact]
        public void KT2_15_stretch_compares_only_existing_objects_with_the_limit()
        {
            var body = Body(Edit2DTools, "internal static object StretchEntities", "// ------------------------------------------------------------------ BREAK");
            Assert.Contains("Edit2DLogic.CountedForLimit(hs, ctx.CreatedThisTurn)", body);
            Assert.Contains("if (!named && existing > limit)", body);
            Assert.DoesNotContain("plan.Count > limit", body);
        }

        // ------------------------------------------------------------ KT2-23

        [Fact]
        public void KT2_23_ungroup_by_handles_needs_the_name_when_the_objects_are_in_several_groups()
        {
            Assert.Null(Edit2DLogic.UngroupRefusal(new[] { "BAN AN" }));
            Assert.Null(Edit2DLogic.UngroupRefusal(new string[0]));
            var msg = Edit2DLogic.UngroupRefusal(new[] { "BAN AN", "GHE" });
            Assert.NotNull(msg);
            Assert.Contains("2 groups", msg);
            Assert.Contains("BAN AN", msg);
            Assert.Contains("GHE", msg);
            Assert.Contains("'name'", msg);
            var many = Edit2DLogic.UngroupRefusal(Enumerable.Range(1, 30).Select(i => "G" + i).ToList());
            Assert.Contains("30 groups", many);
            Assert.Contains("…", many);
        }

        [Fact]
        public void KT2_23_ungroup_refuses_before_anything_is_removed()
        {
            var body = Body(Edit2DTools, "if (action == \"ungroup\")", "throw ToolException.Invalid(\"group_entities: 'action' must be create or ungroup.\");");
            int refuse = body.IndexOf("Edit2DLogic.UngroupRefusal(", StringComparison.Ordinal);
            Assert.True(refuse > 0, "ungroup does not check several groups");
            Assert.True(refuse < body.IndexOf("ctx.RequireWritable(\"ungroup\")", StringComparison.Ordinal), "the check must run before anything is removed");
            Assert.True(refuse < body.IndexOf("g.Erase()", StringComparison.Ordinal));
            Assert.Contains("[\"ungrouped\"]", body); // the names of the removed group(s) are returned
        }

        // ------------------------------------------------------------ KT2-24

        [Fact]
        public void KT2_24_sloped_line_is_lengthened_along_its_3D_direction()
        {
            // (0,0,0) → (0,30,40): 50 long; +10 at the end → (0,36,48), 60 long
            var (s, e) = Edit2DLogic.LengthenSegment(new[] { 0.0, 0, 0 }, new[] { 0.0, 30, 40 }, 10, true);
            Assert.Equal(new[] { 0.0, 0, 0 }, s);
            Assert.Equal(0, e[0], 9); Assert.Equal(36, e[1], 9); Assert.Equal(48, e[2], 9);
            Assert.Equal(60, Math.Sqrt(e[0] * e[0] + e[1] * e[1] + e[2] * e[2]), 9);
        }

        [Fact]
        public void KT2_24_vertical_line_works_and_shortens_at_the_start()
        {
            var (s, e) = Edit2DLogic.LengthenSegment(new[] { 5.0, 5, 0 }, new[] { 5.0, 5, 100 }, 20, true);
            Assert.Equal(120, e[2], 9);
            Assert.Equal(5, e[0], 9); Assert.Equal(5, e[1], 9);
            var (s2, e2) = Edit2DLogic.LengthenSegment(new[] { 5.0, 5, 0 }, new[] { 5.0, 5, 100 }, -30, false);
            Assert.Equal(30, s2[2], 9);
            Assert.Equal(100, e2[2], 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.LengthenSegment(new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 100 }, -100, true));
        }

        [Fact]
        public void KT2_24_a_polyline_2D_segment_is_unchanged()
        {
            // a polyline segment arrives as 2D points (no z): same result as before
            var (s, e) = Edit2DLogic.LengthenSegment(new[] { 0.0, 0 }, new[] { 100.0, 0 }, 25, true);
            Assert.Equal(125, e[0], 9);
            Assert.Equal(0, e[1], 9);
            Assert.Equal(0, e.Length > 2 ? e[2] : 0, 9);
            var (s2, _) = Edit2DLogic.LengthenSegment(new[] { 0.0, 0 }, new[] { 0.0, 100 }, -40, false);
            Assert.Equal(40, s2[1], 9);
        }
    }
}
