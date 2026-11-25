using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt G1 (25/11/2026, docs/ban-giao/nang-cap-g1-test.md): the holes found in create_drawing_set (storeys past
    /// the limit, views that do not fit, the card of a set without sheets, an unknown title field) and the cases the review
    /// checked (work mode of a Full machine, limits, units, titles).
    /// </summary>
    public class NangCapG1TestSuaTests
    {
        private static string Plugin(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray())));

        private static DrawingSetLogic.Request House(double w = 12000, double d = 9000, int levels = 1, double f = 1) => new DrawingSetLogic.Request
        {
            SolidMin = new[] { 0, 0, 0.0 },
            SolidMax = new[] { w / f, d / f, 3300.0 * levels / f },
            Min2d = new[] { 0, 0.0 },
            Max2d = new[] { w / f, d / f },
            Levels = Enumerable.Range(0, levels).Select(i => i * 3300.0 / f).ToList(),
        };

        // ------------------------------------------------------------ TG1-1: storeys past the limit are said

        [Fact]
        public void Storeys_past_ten_are_counted_and_the_plugin_says_so()
        {
            var walls = Enumerable.Range(0, 12).Select(i => (i * 3300.0, 3300.0)).ToList();
            Assert.Equal(G1Numbers.MaxLevels, DrawingSetLogic.StoreyLevels(walls, 1).Count);
            Assert.Equal(12, DrawingSetLogic.StoreyCount(walls, 1));
            // a lintel piece starts no storey, a drawing in metres counts the same
            walls.Add((2.1 * 1000, 600));
            Assert.Equal(12, DrawingSetLogic.StoreyCount(walls, 1));
            Assert.Equal(12, DrawingSetLogic.StoreyCount(walls.Select(x => (x.Item1 / 1000, x.Item2 / 1000)), 1000));
            Assert.Equal(0, DrawingSetLogic.StoreyCount(new List<(double, double)>(), 1));
            Assert.Contains("int storeys = DrawingSetLogic.StoreyCount(walls, f);", Plugin("Drawing", "DrawingSetTools.cs"));
        }

        // ------------------------------------------------------------ TG1-2: a view larger than its sheet is said

        [Fact]
        public void A_view_that_does_not_fit_at_its_scale_is_listed()
        {
            var a3 = SheetLogic.Build("A3", null);
            var full = DrawingSetLogic.Abilities(PlanLogic.PlanFull, true, true);
            // the default rule always fits a 12 m house (1:100)
            var ok = DrawingSetLogic.Build(House(), full, null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(ok, a3, 1, 0);
            Assert.Empty(DrawingSetLogic.NotFitting(ok, a3, 1));
            // a scale given too small for it (1:20)
            var given = DrawingSetLogic.Build(House(), full, null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(given, a3, 1, 20);
            Assert.Contains("MẶT BẰNG", DrawingSetLogic.NotFitting(given, a3, 1));
            Assert.DoesNotContain("PHỐI CẢNH", DrawingSetLogic.NotFitting(given, a3, 1));
            // a 3 km site: even 1:5000 does not fit an A3
            var huge = DrawingSetLogic.Build(House(3_000_000, 2_000_000), full, null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(huge, a3, 1, 0);
            Assert.Equal(5000, huge.Sheets[0].Scale);
            Assert.NotEmpty(DrawingSetLogic.NotFitting(huge, a3, 1));
            Assert.Contains("DrawingSetLogic.NotFitting(set, sheet, f)", Plugin("Drawing", "DrawingSetTools.cs"));
        }

        [Fact]
        public void Units_do_not_change_the_scales()
        {
            var a3 = SheetLogic.Build("A3", null);
            var full = DrawingSetLogic.Abilities(PlanLogic.PlanFull, true, true);
            int[] ScalesFor(double f)
            {
                var big = DrawingSetLogic.Build(House(40000, 25000, 1, f), full, null, PlanLogic.PlanFull);
                DrawingSetLogic.ChooseScales(big, a3, f, 0);
                return big.Sheets.Where(s => s.Kind != SheetKind.Perspective).Select(s => s.Scale).ToArray();
            }
            var mm = ScalesFor(1);
            Assert.All(mm, n => Assert.True(n > G1Numbers.DefaultScale)); // a 40 m house needs a larger scale than 1:100
            Assert.Equal(mm, ScalesFor(10));
            Assert.Equal(mm, ScalesFor(1000));
        }

        // ------------------------------------------------------------ TG1-3: the card of a set without sheets

        [Fact]
        public void The_card_of_a_set_without_sheets_names_its_views()
        {
            var none = new string[0];
            var a = DrawingSetLogic.ReplaceToken(none, new[] { "2F", "30" });
            var b = DrawingSetLogic.ReplaceToken(none, new[] { "2F", "31" });
            Assert.NotEqual(a, b); // before: every set without layouts had the same token
            Assert.Equal(a, DrawingSetLogic.ReplaceToken(none, new[] { "30", "2f" }));
            Assert.NotEqual(DrawingSetLogic.ReplaceToken(new[] { "KT-01" }, none), DrawingSetLogic.ReplaceToken(new[] { "KT-01" }, new[] { "2F" }));
            Assert.Equal(DrawingSetLogic.ReplaceToken(new[] { "KT-01" }), DrawingSetLogic.ReplaceToken(new[] { "KT-01" }, none));
        }

        // ------------------------------------------------------------ TG1-4: an unknown title field before any change

        [Fact]
        public void An_unknown_title_field_is_refused_before_anything_changes()
        {
            Assert.Empty(DrawingSetLogic.UnknownFields(new[] { "company", "Project", "drawn_by", "date" }));
            Assert.Equal(new[] { "chu_dau_tu" }, DrawingSetLogic.UnknownFields(new[] { "company", "chu_dau_tu" }));
            var t = Plugin("Drawing", "DrawingSetTools.cs");
            int check = t.IndexOf("DrawingSetLogic.UnknownFields(userFields.Keys)", StringComparison.Ordinal);
            int remove = t.IndexOf("if (hadSet) RemoveSet(", StringComparison.Ordinal);
            Assert.True(check > 0 && check < remove, "the fields are checked before the old set is removed");
        }

        // ------------------------------------------------------------ checked, already right

        [Fact]
        public void A_full_machine_in_the_2d_mode_gets_the_2d_parts_and_is_told_to_switch()
        {
            // the dispatcher gives the effective plan of the mode (ChatController.Plan) and the limiting mode
            string plan = WorkModeLogic.EffectivePlan(PlanLogic.PlanFull, "2d");
            string mode = WorkModeLogic.Limiting(PlanLogic.PlanFull, "2d");
            var set = DrawingSetLogic.Build(House(), DrawingSetLogic.Abilities(plan, true, true), mode, plan);
            Assert.Equal("2d", set.PlanSource);
            Assert.Contains("Cả hai", set.Missing["elevation"]);
            Assert.DoesNotContain("plan (this machine", set.Missing["elevation"]);
            // the 3D mode: views but no sheets, the reason is the mode
            string p3 = WorkModeLogic.EffectivePlan(PlanLogic.PlanFull, "3d");
            string m3 = WorkModeLogic.Limiting(PlanLogic.PlanFull, "3d");
            var set3 = DrawingSetLogic.Build(House(), DrawingSetLogic.Abilities(p3, true, true), m3, p3);
            Assert.Equal("3d", set3.PlanSource);
            Assert.Contains("Cả hai", set3.Missing["perspective"]);
            Assert.Contains("Cả hai", DrawingSetLogic.SheetsMissing(m3, p3));
        }

        [Fact]
        public void Drawings_without_solids_or_without_anything()
        {
            var r = House();
            var no3d = DrawingSetLogic.Build(r, DrawingSetLogic.Abilities(PlanLogic.PlanFull, false, true), null, PlanLogic.PlanFull);
            Assert.Equal("2d", no3d.PlanSource);
            foreach (var k in new[] { "elevation", "section", "perspective" }) Assert.Contains("no 3D solids", no3d.Missing[k]);
            var empty = DrawingSetLogic.Build(r, DrawingSetLogic.Abilities(PlanLogic.PlanFull, false, false), null, PlanLogic.PlanFull);
            Assert.Empty(empty.Sheets);
            Assert.Contains("neither", empty.Missing["plan"]);
            // solids not on a wall layer: one plan at the bottom of the model
            Assert.Empty(DrawingSetLogic.StoreyLevels(new List<(double, double)>(), 1));
            Assert.False(DrawingSetLogic.IsWallLayer("A-SLAB-3D"));
        }

        [Fact]
        public void Thirty_sheets_at_most_and_nothing_is_made_beyond()
        {
            var r = House(levels: 10);
            r.Sections = Enumerable.Range(0, 8).Select(i => (new[] { 0.0, i * 1000.0 }, new[] { 12000.0, i * 1000.0 + 1 }, (string)null)).ToList();
            var full = DrawingSetLogic.Abilities(PlanLogic.PlanFull, true, true);
            Assert.Equal(23, DrawingSetLogic.Build(r, full, null, PlanLogic.PlanFull).Sheets.Count); // 10 + 4 + 8 + 1
            r.PlanAreas = null;
            var more = House(levels: 10);
            more.Sections = Enumerable.Range(0, 16).Select(i => (new[] { 0.0, i * 500.0 }, new[] { 12000.0, i * 500.0 + 1 }, (string)null)).ToList();
            var ex = Assert.Throws<ToolException>(() => DrawingSetLogic.Build(more, full, null, PlanLogic.PlanFull));
            Assert.Contains("Nothing was made", ex.Message);
        }

        [Fact]
        public void The_titles_are_written_right_in_vietnamese()
        {
            Assert.Equal("MẶT BẰNG TẦNG 3", string.Format(G1Numbers.TitlePlanStorey, 3));
            Assert.Equal("MẶT CẮT A-A", string.Format(G1Numbers.TitleSection, "A"));
            Assert.Equal(new[] { "MẶT ĐỨNG TRƯỚC", "MẶT ĐỨNG SAU", "MẶT ĐỨNG BÊN TRÁI", "MẶT ĐỨNG BÊN PHẢI" },
                DrawingSetLogic.Elevations.Select(e => G1Numbers.ElevationTitles[e]).ToArray());
            Assert.Equal("PHỐI CẢNH", G1Numbers.TitlePerspective);
            Assert.Equal("—", G1Numbers.NoScaleText);
            Assert.Equal("nha-pho-bo-ho-so.pdf", DrawingSetLogic.PdfName(@"D:\Du an\nha-pho.dwg"));
            Assert.Equal("ban-ve-bo-ho-so.pdf", DrawingSetLogic.PdfName(""));
            // the fields the plugin fills itself win over the user's
            var t = Plugin("Drawing", "DrawingSetTools.cs");
            Assert.Contains("foreach (var k in new[] { \"title\", \"sheet_no\", \"scale\", \"paper\" }) userFields.Remove(k);", t);
            Assert.Contains("if (s.Kind == SheetKind.Perspective) fields[\"scale\"] = G1Numbers.NoScaleText;", t);
        }
    }
}
