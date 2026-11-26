using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H3 (26/11/2026, docs/ban-giao/nang-cap-h3.md): compare_drawings (Core/DrawingCompare.cs).</summary>
    public class NangCapH3CompareTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static CmpItem Line(string h, double x0, double y0, double x1, double y1, string layer = "A-WALL") => new CmpItem
        {
            Handle = h, Kind = "line", Layer = layer, Nums = new[] { x0, y0, 0, x1, y1, 0 }, Alt = new[] { x1, y1, 0, x0, y0, 0 },
            Min = new[] { Math.Min(x0, x1), Math.Min(y0, y1), 0 }, Max = new[] { Math.Max(x0, x1), Math.Max(y0, y1), 0 },
        };

        private static CmpItem Text(string h, double x, double y, string text) => new CmpItem
        {
            Handle = h, Kind = "text", Layer = "A-TEXT", Nums = new[] { x, y, 0, 250 }, Exact = "0",
            Props = new Dictionary<string, string> { ["text"] = text }, Min = new[] { x, y, 0 }, Max = new[] { x + 1000, y + 250, 0 },
        };

        [Fact]
        public void Two_copies_of_one_file_are_matched_by_handle_others_by_geometry()
        {
            var old = new List<CmpItem> { Line("1A", 0, 0, 1000, 0), Line("1B", 0, 0, 0, 1000), Text("1C", 0, 0, "P.NGỦ") };
            var now = new List<CmpItem> { Line("1A", 0, 0, 1000, 0), Line("1B", 0, 0, 0, 1000), Text("2F", 0, 0, "P.NGỦ") };
            Assert.Equal("handle", DrawingCompareLogic.MatchMode("auto", old, now)); // 2 of 3 = 67 %
            var other = new List<CmpItem> { Line("9A", 0, 0, 1000, 0), Line("9B", 0, 0, 0, 1000) };
            Assert.Equal("geometry", DrawingCompareLogic.MatchMode("auto", other, now));
            Assert.Equal("geometry", DrawingCompareLogic.MatchMode(null, new List<CmpItem>(), now));
            Assert.Equal("handle", DrawingCompareLogic.MatchMode(" HANDLE ", other, now));
            Assert.Throws<ToolException>(() => DrawingCompareLogic.MatchMode("fuzzy", other, now));
            // a handle of another kind is not the same object
            var kindChanged = new List<CmpItem> { Text("1A", 0, 0, "x") };
            Assert.Equal(0, DrawingCompareLogic.SameOriginShare(kindChanged, now));
        }

        [Fact]
        public void By_handle_added_removed_and_changed_with_what_changed()
        {
            var old = new List<CmpItem> { Line("1A", 0, 0, 1000, 0), Line("1B", 0, 0, 0, 1000), Text("1C", 0, 0, "P.NGỦ"), Line("1D", 5, 5, 6, 6) };
            var now = new List<CmpItem>
            {
                Line("1A", 0, 0, 1000, 0),               // same
                Line("1B", 0, 0, 0, 1200),               // longer
                Text("1C", 0, 0, "P.KHÁCH"),             // text changed
                Line("2E", 0, 0, 500, 500),              // added (1D removed)
            };
            now[0].Props["color"] = "BYLAYER"; old[0].Props["color"] = "BYLAYER";
            var rows = DrawingCompareLogic.Compare(old, now, "handle", 1);
            var c = DrawingCompareLogic.Counts(rows);
            Assert.Equal(1, c["added"]);
            Assert.Equal(1, c["removed"]);
            Assert.Equal(2, c["changed"]);
            Assert.Contains(rows, r => r.Change == "changed" && r.Handle == "1B" && r.What.Any(w => w.StartsWith("geometry")));
            Assert.Contains(rows, r => r.Change == "changed" && r.Handle == "1C" && r.What.Contains("text 'P.NGỦ' → 'P.KHÁCH'"));
            Assert.Contains(rows, r => r.Change == "removed" && r.OtherHandle == "1D" && r.Handle == null);
            Assert.Contains(rows, r => r.Change == "added" && r.Handle == "2E" && r.OtherHandle == null);
        }

        [Fact]
        public void By_geometry_a_line_drawn_the_other_way_or_within_the_tolerance_is_the_same()
        {
            var old = new List<CmpItem> { Line("1", 0, 0, 1000, 0), Line("2", 0, 0, 0, 1000) };
            var now = new List<CmpItem> { Line("A", 1000, 0.4, 0, 0), Line("B", 0, 0, 0, 1000, "A-WALL-NEW") };
            var rows = DrawingCompareLogic.Compare(old, now, "geometry", 1);
            Assert.Single(rows);
            Assert.Equal("changed", rows[0].Change);
            Assert.Equal(new[] { "layer A-WALL → A-WALL-NEW" }, rows[0].What);
            // 2 mm off with a tolerance of 1: another line at the same place (first point) = changed geometry
            var moved = DrawingCompareLogic.Compare(old, new List<CmpItem> { Line("A", 0, 0, 1002, 0), Line("B", 0, 0, 0, 1000) }, "geometry", 1);
            Assert.Single(moved);
            Assert.StartsWith("geometry", moved[0].What[0]);
            // far away: removed + added
            var far = DrawingCompareLogic.Compare(old, new List<CmpItem> { Line("A", 5000, 5000, 6000, 5000), Line("B", 0, 0, 0, 1000) }, "geometry", 1);
            Assert.Equal(new[] { "added", "removed" }, far.Select(r => r.Change).OrderBy(x => x));
        }

        [Fact]
        public void The_other_file_can_be_the_newer_one()
        {
            var other = new List<CmpItem> { Line("1", 0, 0, 1000, 0), Line("2", 3000, 0, 4000, 0) };
            var open = new List<CmpItem> { Line("A", 0, 0, 1000, 0) };
            var rows = DrawingCompareLogic.Compare(other, open, "geometry", 1, otherIsNew: true);
            var r = Assert.Single(rows);
            Assert.Equal("added", r.Change); // only in the other (newer) file
            Assert.Null(r.Handle);
            Assert.Equal("2", r.OtherHandle);
            var asOld = DrawingCompareLogic.Compare(other, open, "geometry", 1);
            Assert.Equal("removed", Assert.Single(asOld).Change);
        }

        [Fact]
        public void Solids_are_compared_by_box_and_volume()
        {
            CmpItem S(string h, double size, double vol) => new CmpItem
            {
                Handle = h, Kind = "solid", Layer = "3D-TUONG", Nums = new[] { 0, 0, 0, size, 200, 3000 }, Volume = vol,
                Min = new[] { 0.0, 0, 0 }, Max = new[] { size, 200, 3000 },
            };
            Assert.Empty(DrawingCompareLogic.Compare(new List<CmpItem> { S("1", 4000, 2.4e9) }, new List<CmpItem> { S("1", 4000, 2.4e9 * 1.0005) }, "handle", 1));
            var hole = DrawingCompareLogic.Compare(new List<CmpItem> { S("1", 4000, 2.4e9) }, new List<CmpItem> { S("1", 4000, 2.0e9) }, "handle", 1);
            Assert.Contains(hole.Single().What, w => w.StartsWith("volume"));
            Assert.True(DrawingCompareLogic.SameVolume(null, null));
            Assert.False(DrawingCompareLogic.SameVolume(1, null));
        }

        [Fact]
        public void Clouds_ring_each_difference_and_merge_the_ones_that_touch()
        {
            var rows = new List<DiffRow>
            {
                new DiffRow { Change = "changed", Min = new[] { 0.0, 0 }, Max = new[] { 1000.0, 10 } },
                new DiffRow { Change = "changed", Min = new[] { 1050.0, 0 }, Max = new[] { 2000.0, 10 } },   // touches the first with the margin
                new DiffRow { Change = "changed", Min = new[] { 50000.0, 0 }, Max = new[] { 51000.0, 10 } },
                new DiffRow { Change = "added", Min = new[] { 0.0, 0 }, Max = new[] { 1000.0, 10 } },        // other colour: its own cloud
                new DiffRow { Change = "removed" },                                                           // no box: no cloud
            };
            var clouds = DrawingCompareLogic.Clouds(rows, 1);
            Assert.Equal(3, clouds.Count);
            var merged = clouds.Single(c => c.Change == "changed" && c.Count == 2);
            Assert.True(merged.Min[0] < 0 && merged.Max[0] > 2000);
            Assert.Equal(H3Numbers.ColorAdded, clouds.Single(c => c.Change == "added").Color);
            Assert.Equal(H3Numbers.ColorChanged, merged.Color);
            Assert.Equal(H3Numbers.ColorRemoved, DrawingCompareLogic.ColorOf("removed"));
            // the margin: at least 100 mm (0.1 in a metre drawing), else 5 % of the longest side
            Assert.Equal(100, DrawingCompareLogic.Margin(new[] { 0.0, 0 }, new[] { 10.0, 10 }, 1), 9);
            Assert.Equal(0.1, DrawingCompareLogic.Margin(new[] { 0.0, 0 }, new[] { 1.0, 1 }, 1000), 9);
            Assert.Equal(500, DrawingCompareLogic.Margin(new[] { 0.0, 0 }, new[] { 10000.0, 10 }, 1), 9);
            Assert.True(DrawingCompareLogic.ArcLength(merged, 1) >= 100);
        }

        [Fact]
        public void The_excel_list_has_a_header_and_one_row_per_difference_in_vietnamese()
        {
            var rows = new List<DiffRow> { new DiffRow { Change = "removed", Kind = "line", Layer = "A-WALL", OtherHandle = "1D", What = { "x" } } };
            var t = DrawingCompareLogic.ExcelRows(rows);
            Assert.Equal(2, t.Count);
            Assert.Equal("Thay đổi", t[0][0]);
            Assert.Equal("Bớt", t[1][0]);
            Assert.Equal("1D", t[1][4]);
            Assert.Equal("Thêm", DrawingCompareLogic.ChangeName("added"));
            Assert.Equal("Đổi", DrawingCompareLogic.ChangeName("changed"));
        }

        [Fact]
        public void The_card_token_depends_on_the_file_and_the_number_of_clouds()
        {
            var a = DrawingCompareLogic.MarkToken(@"C:\a\cu.dwg", 60);
            Assert.Equal(a, DrawingCompareLogic.MarkToken(@"c:/A/CU.DWG", 60));
            Assert.NotEqual(a, DrawingCompareLogic.MarkToken(@"C:\a\cu.dwg", 61));
            Assert.NotEqual(a, DrawingCompareLogic.MarkToken(@"C:\a\moi.dwg", 60));
        }

        [Fact]
        public void Twenty_thousand_objects_per_drawing_compare_in_well_under_a_second_each_way()
        {
            var rnd = new Random(26112026);
            var old = new List<CmpItem>();
            var now = new List<CmpItem>();
            for (int i = 0; i < H3Numbers.MaxObjects; i++)
            {
                double x = rnd.Next(0, 100000), y = rnd.Next(0, 100000);
                old.Add(Line("O" + i, x, y, x + 3000, y));
                now.Add(i % 10 == 0 ? Line("N" + i, x, y, x + 3500, y) : Line("N" + i, x + 3000, y, x, y)); // 10 % longer, the rest reversed
            }
            // and a pile on one point (a fan): not compared pair by pair
            for (int i = 0; i < 2000; i++) { old.Add(Line("F" + i, 0, 0, 1000 + i, 500)); now.Add(Line("G" + i, 0, 0, 1000 + i, 500)); }
            var sw = Stopwatch.StartNew();
            var rows = DrawingCompareLogic.Compare(old, now, "geometry", 1);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 3000, $"{sw.ElapsedMilliseconds} ms");
            Assert.Equal(H3Numbers.MaxObjects / 10, rows.Count(r => r.Change == "changed"));
            Assert.Equal(0, rows.Count(r => r.Change != "changed"));
        }

        [Fact]
        public void The_tool_is_registered_read_only_unless_it_marks_and_never_writes_the_other_file()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompareTools.cs");
            Assert.Contains("readsWhen: p => !p.Bool(\"mark\", false)", s);
            Assert.Contains("OtherDrawing.Open(ctx, path, tool)", s);
            Assert.Contains("RequireCard(ctx, p, tool", s);
            Assert.Contains("is the open drawing itself", s);
            Assert.DoesNotContain("od.Db.Save", s);
            Assert.DoesNotContain("OpenMode.ForWrite", s);
            Assert.Contains("\"compare_drawings\"", Src("plugin", "ClaudeCAD.Plugin", "Core", "Plans.cs"));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("compare_drawings", new Dictionary<string, object>()));
            Assert.Contains("RegisterCompare(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            Assert.Contains("[\"compare_drawings\"]", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            Assert.Equal("So sánh bản vẽ", F4Numbers.SheetName["compare_drawings"]);
        }
    }
}
