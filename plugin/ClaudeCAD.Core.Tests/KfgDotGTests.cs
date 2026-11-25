using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sau kiểm tra độc lập đợt F + G (docs/ban-giao/kiem-tra-doc-lap-dot-fg.md), phần thread giữ nhánh tích hợp đợt G:
    /// KFG-02, 03, 25, 26 (G3), KFG-19 … 24 + hai nghi vấn (G1), KFG-09 (F2), KFG-14 (prompt).
    /// </summary>
    public class KfgDotGTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static EstimateLogic.Row R(string item, double q = 10) => new EstimateLogic.Row { Item = item, Unit = "m²", Quantity = q };
        private static EstimateLogic.Price P(string item, double price) => new EstimateLogic.Price { Item = item, UnitPrice = price };

        // ---- KFG-03: accents count, separators only when not ambiguous ----

        [Fact]
        public void KFG03_an_accented_price_name_never_fits_another_accented_word()
        {
            var r = EstimateLogic.Build(new[] { R("Sàn"), R("Sân") }, new[] { P("Sân", 300_000) }, null);
            Assert.Null(r.Lines[0].UnitPrice);
            Assert.Equal(300_000, r.Lines[1].UnitPrice);
            Assert.Equal(new[] { "Sàn" }, r.Missing);
            // case and spaces still do not matter
            var c = EstimateLogic.Build(new[] { R("Phòng khách") }, new[] { P("  PHÒNG   KHÁCH ", 450_000) }, null);
            Assert.Equal(450_000, c.Lines[0].UnitPrice);
        }

        [Fact]
        public void KFG03_a_price_typed_without_accents_fits_only_when_one_item_fits()
        {
            var one = EstimateLogic.Build(new[] { R("Phòng khách"), R("Phòng ngủ") }, new[] { P("phong khach", 450_000) }, null);
            Assert.Equal(450_000, one.Lines[0].UnitPrice);
            Assert.Null(one.Lines[1].UnitPrice);
            Assert.Empty(one.Ambiguous);
            // "san" fits both "Sàn" and "Sân": neither gets a price, and the price is reported as ambiguous
            var two = EstimateLogic.Build(new[] { R("Sàn"), R("Sân") }, new[] { P("san", 300_000) }, null);
            Assert.All(two.Lines, l => Assert.Null(l.UnitPrice));
            Assert.Single(two.Ambiguous);
            Assert.Equal("san", two.Ambiguous[0].price);
            Assert.Equal(2, two.Ambiguous[0].items.Count);
        }

        [Fact]
        public void KFG03_separators_join_names_only_when_not_ambiguous()
        {
            var one = EstimateLogic.Build(new[] { R("A-WALL") }, new[] { P("A_WALL", 1_000_000) }, null);
            Assert.Equal(1_000_000, one.Lines[0].UnitPrice);
            // both "A-WALL" and "A_WALL" exist: the exact one wins for its row, the other row gets nothing
            var both = EstimateLogic.Build(new[] { R("A-WALL"), R("A_WALL") }, new[] { P("A-WALL", 1_000_000) }, null);
            Assert.Equal(1_000_000, both.Lines[0].UnitPrice);
            Assert.Null(both.Lines[1].UnitPrice);
            // a name that equals neither but fits both after separators: ambiguous
            var amb = EstimateLogic.Build(new[] { R("A-WALL"), R("A_WALL") }, new[] { P("A.WALL", 1_000_000) }, null);
            Assert.All(amb.Lines, l => Assert.Null(l.UnitPrice));
            Assert.Single(amb.Ambiguous);
        }

        [Fact]
        public void KFG30_an_unaccented_pattern_on_names_differing_only_in_accents_is_ambiguous()
        {
            var r = EstimateLogic.Build(new[] { R("Sân phơi"), R("Sàn gỗ") }, new[] { P("San*", 9) }, null);
            Assert.All(r.Lines, l => Assert.Null(l.UnitPrice));
            Assert.All(r.Lines, l => Assert.Contains("differ only in accents", l.Note));
            Assert.Single(r.Ambiguous);
            Assert.Equal("San*", r.Ambiguous[0].price);
            Assert.Equal(2, r.Ambiguous[0].items.Count);
            // one accent spelling only: the unaccented pattern fits as before
            var one = EstimateLogic.Build(new[] { R("Sàn gỗ"), R("Sàn đá") }, new[] { P("san*", 9) }, null);
            Assert.All(one.Lines, l => Assert.Equal(9, l.UnitPrice));
            Assert.Empty(one.Ambiguous);
            // the part under '*' may differ in accents ("Sàn gỗ" / "Sàn đá" above); an accented pattern keeps its accents
            var acc = EstimateLogic.Build(new[] { R("Sân phơi"), R("Sàn gỗ") }, new[] { P("Sàn*", 9) }, null);
            Assert.Null(acc.Lines[0].UnitPrice);
            Assert.Equal(9, acc.Lines[1].UnitPrice);
            Assert.Empty(acc.Ambiguous);
            // patterns without letters that differ: block names as before
            var doors = EstimateLogic.Build(new[] { R("CUA-DI-01"), R("CUA-DI-02") }, new[] { P("CUA-DI-*", 3_500_000) }, null);
            Assert.All(doors.Lines, l => Assert.Equal(3_500_000, l.UnitPrice));
            Assert.Equal("sân*", EstimateLogic.WildFixedPart("San*", "Sân phơi")); // the fixed parts, "*" between
            Assert.Null(EstimateLogic.WildFixedPart("Tuong*", "Sân phơi"));
        }

        [Fact]
        public void KFG30_an_unaccented_pattern_is_checked_once_not_once_per_row()
        {
            var rows = Enumerable.Range(0, 3000).Select(i => R((i % 2 == 0 ? "Sân " : "Sàn ") + i)).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = EstimateLogic.Build(rows, new[] { P("San*", 9) }, null);
            Assert.True(sw.Elapsed.TotalSeconds < 2, sw.Elapsed.ToString());
            Assert.All(r.Lines, l => Assert.Null(l.UnitPrice));
            Assert.Single(r.Ambiguous);
            Assert.Equal(3000, r.Ambiguous[0].items.Count);
        }

        [Fact]
        public void KFG03_the_description_and_skills_say_accents_count()
        {
            Assert.Contains("ambiguous_prices", Src("agent", "src", "tools", "estimate.ts"));
            Assert.Contains("ambiguous_prices", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "EstimateTools.cs"));
        }

        // ---- KFG-02: nested areas refused, source warnings passed on ----

        [Fact]
        public void KFG02_nested_areas_are_refused_and_source_warnings_are_passed_on()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "EstimateTools.cs");
            Assert.Contains("contains_others", t);
            Assert.Contains("allow_nested", t);
            Assert.Contains("source_warnings", t);
            Assert.Contains("left_out_layers", EstimateLogic.SourceWarningKeys);
            Assert.Contains("contains_others", EstimateLogic.SourceWarningKeys);
        }

        // ---- KFG-26: a unit price with decimals keeps them in Excel ----

        [Fact]
        public void KFG26_a_unit_price_with_decimals_gets_the_two_decimal_style()
        {
            var r = EstimateLogic.Build(new[] { R("A") }, new[] { P("A", 12.5) }, null);
            var rows = EstimateLogic.ExcelRows(r, null);
            var cells = rows.SelectMany(x => x).OfType<XlsxCell>().ToList();
            Assert.Contains(cells, c => c.Style == 4 && Equals(c.Value, 12.5));
            var whole = EstimateLogic.ExcelRows(EstimateLogic.Build(new[] { R("A") }, new[] { P("A", 12) }, null), null);
            Assert.DoesNotContain(whole.SelectMany(x => x).OfType<XlsxCell>(), c => c.Style == 4);
            Assert.Equal("#,##0.00", G3Numbers.ExcelPriceDecimalsFormat);
        }

        // ---- KFG-25: the quantities enum follows the plan ----

        [Fact]
        public void KFG25_the_quantities_choices_are_narrowed_by_the_plan()
        {
            var t = Src("agent", "src", "tools", "index.ts");
            Assert.Contains("t.name === 'cost_estimate'", t);
            Assert.Contains("z.enum(['volume'])", t);
            Assert.Contains("z.enum(['blocks', 'areas'])", t);
        }

        // ---- G1 ----

        private static DrawingSetLogic.Request House() => new DrawingSetLogic.Request
        {
            SolidMin = new double[] { 0, 0, -120 },
            SolidMax = new double[] { 12000, 9000, 6600 },
            Min2d = new double[] { 0, 0 },
            Max2d = new double[] { 12000, 9000 },
            Levels = new List<double> { 0, 3300 },
        };

        [Fact]
        public void KFG19_an_empty_list_is_refused_not_a_set_with_no_sheets()
        {
            var on = DrawingSetLogic.Abilities(PlanLogic.PlanFull, true, true);
            var e = House(); e.Elevations = new List<string>();
            Assert.Contains("elevations is empty", Assert.Throws<ToolException>(() => DrawingSetLogic.Build(e, on, null, PlanLogic.PlanFull)).Message);
            var s = House(); s.Sections = new List<(double[], double[], string)>();
            Assert.Contains("sections is empty", Assert.Throws<ToolException>(() => DrawingSetLogic.Build(s, on, null, PlanLogic.PlanFull)).Message);
            var a = House(); a.PlanAreas = new List<(double[], double[], string)>();
            Assert.Contains("plan_areas is empty", Assert.Throws<ToolException>(() => DrawingSetLogic.Build(a, on, null, PlanLogic.PlanFull)).Message);
            // the schema refuses them before the plugin (kinds and the three lists)
            Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(Src("agent", "src", "tools", "drawingset.ts"), @"\.min\(1\)\.max\(").Count);
        }

        [Fact]
        public void KFG23_wall_bottoms_close_to_their_neighbours_are_one_storey()
        {
            Assert.Single(DrawingSetLogic.Levels(new double[] { 0, 40, 80, 120 }, DrawingSetLogic.LevelTolMm));
            Assert.Equal(new double[] { 0, 3300 }, DrawingSetLogic.Levels(new double[] { 0, 40, 3300, 3330 }, DrawingSetLogic.LevelTolMm).ToArray());
            var walls = new[] { 0.0, 40, 80, 120 }.Select(z => (z, 3000.0));
            Assert.Equal(1, DrawingSetLogic.StoreyCount(walls, 1));
        }

        [Fact]
        public void KFG24_the_last_sets_sheets_are_known_by_their_handle()
        {
            var rec = new DrawingSetLogic.Record { Layouts = { "KT-01", "KT-02" }, LayoutHandles = { "1A", "1B" } };
            var back = DrawingSetLogic.Decode(DrawingSetLogic.Encode(rec));
            Assert.Equal(rec.Layouts, back.Layouts);
            Assert.Equal(rec.LayoutHandles, back.LayoutHandles);
            // an old record without handles still reads (matched by name)
            var old = DrawingSetLogic.Decode(new[] { "L:KT-01", "H:2A" });
            Assert.Equal(new[] { "KT-01" }, old.Layouts);
            Assert.Equal("", old.HandleOf(0));
            Assert.Equal(new[] { "KT-01" }, DrawingSetLogic.OurLayouts(new Dictionary<string, string> { ["KT-01"] = "99" }, old));
            // KT-01 is ours (same handle); KT-02 was erased and the user made a new layout of the same name: not ours, a clash
            var now = new Dictionary<string, string> { ["KT-01"] = "1A", ["KT-02"] = "5C" };
            Assert.Equal(new[] { "KT-01" }, DrawingSetLogic.OurLayouts(now, rec));
            Assert.Equal(new[] { "KT-02" }, DrawingSetLogic.Clashes(new[] { "KT-01", "KT-02" }, now, rec));
        }

        [Fact]
        public void KFG20_21_22_and_the_two_G1_suspicions_are_handled_in_the_tool()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingSetTools.cs");
            Assert.Contains("bool hadSet = oldLayouts.Count > 0 || aliveViews > 0;", t); // 21
            Assert.Contains("lines / hatches of the views it drew", t); // 20
            Assert.Contains("e is Table", t); // 22
            Assert.Contains("record.Layouts.Add(s.Number);", t); // record survives an error part way
            Assert.Contains("try { WriteSetRecord(doc, db, keep); }", t);
            Assert.Contains("a macro does not overwrite it", t); // macro + existing PDF
            // KFG-33: the error path keeps only our old sheets, and a sheet that is gone is never written with an empty handle
            Assert.Contains("record.Layouts.Concat(oldLayouts)", t);
            Assert.Contains("var alive = r.Layouts.Where(n => layouts.Contains(n))", t);
            Assert.DoesNotContain(": \"\");", t);
        }

        // ---- KFG-09: deleting a layer state has its own token ----

        [Fact]
        public void KFG09_deleting_a_layer_state_needs_another_token_than_overwriting_it()
        {
            Assert.NotEqual("2F", F5Confirm.DeleteLayerStateToken("2F"));
            Assert.Equal(F5Confirm.DeleteLayerStateToken("2f"), F5Confirm.DeleteLayerStateToken("2F"));
            Assert.NotEqual(F5Confirm.DeleteViewToken("2F"), F5Confirm.DeleteLayerStateToken("2F"));
            Assert.Contains("F5Confirm.DeleteLayerStateToken(Acad.H(stateId))", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs"));
        }

        // ---- KFG-14: the plan scope tells what each plan has of đợt G ----

        [Fact]
        public void KFG14_the_prompt_names_the_G_tools_per_plan_and_pictures_are_data()
        {
            var t = Src("agent", "src", "agent", "systemPrompt.ts");
            Assert.Contains("text read from an attached picture", t);
            Assert.Contains("find & replace of text, checking the drawing before printing, redrawing from a photo / scan.'", t);
            Assert.Contains("estimates from volume_schedule without a table", t);
        }
    }
}
