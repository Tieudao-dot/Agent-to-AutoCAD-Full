using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H6 (hạ tầng): docs/ban-giao/nang-cap-h6.md.</summary>
    public class NangCapH6Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void Csv_with_a_Vietnamese_header_semicolons_and_decimal_commas()
        {
            var rows = SurveyReader.ParseText("STT;X (m);Y (m);Cao độ;Mã\r\n1;1234012,500;567045,250;12,350;TN\r\n2;1234020,000;567050,000;12,800;\r\nx;y;z;;\r\n");
            var f = SurveyReader.Interpret(rows);
            Assert.Equal(2, f.Points.Count);
            Assert.Equal(1234012.5, f.Points[0].X, 6);
            Assert.Equal(567045.25, f.Points[0].Y, 6);
            Assert.Equal(12.35, f.Points[0].Z.Value, 6);
            Assert.Equal("1", f.Points[0].Name);
            Assert.Equal("TN", f.Points[0].Code);
            Assert.True(f.HasZ);
            Assert.Single(f.Skipped);
            Assert.Contains("header", f.Columns);
        }

        [Fact]
        public void Csv_without_a_header_by_position_and_space_separated_text()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("M1,100.5,200.25,3.1\nM2,110,205,3.4,coc\n"));
            Assert.Equal(new[] { "M1", "M2" }, f.Points.Select(p => p.Name));
            Assert.Equal(3.4, f.Points[1].Z.Value, 6);
            var g = SurveyReader.Interpret(SurveyReader.ParseText("100 200 1.5\n101   201 1.6\n"));
            Assert.Equal(2, g.Points.Count);
            Assert.Equal("2", g.Points[1].Name);
            Assert.Equal(201, g.Points[1].Y, 6);
            var two = SurveyReader.Interpret(SurveyReader.ParseText("A,1,2\nB,3,4\n"));
            Assert.False(two.HasZ);
            Assert.Null(two.Points[0].Z);
        }

        [Fact]
        public void Given_columns_win_and_a_quoted_name_keeps_its_comma()
        {
            var rows = SurveyReader.ParseText("\"Mốc 1, cổng\",5,10,20,1.2\n");
            var f = SurveyReader.Interpret(rows, new SurveyReader.Columns { Name = 0, X = 3, Y = 2, Z = 4 });
            Assert.Equal("Mốc 1, cổng", f.Points[0].Name);
            Assert.Equal(20, f.Points[0].X, 6);
            Assert.Equal(10, f.Points[0].Y, 6);
        }

        [Fact]
        public void Xlsx_written_by_the_plugin_reads_back()
        {
            var bytes = XlsxWriter.Build(new List<IList<object>>
            {
                new List<object> { "Tên điểm", "X", "Y", "Z" },
                new List<object> { "P1", 1234000.125, 567000.5, 10.25 },
                new List<object> { "P2", 1234010.0, 567005.0, 11.0 },
            }, "Diem");
            var f = SurveyReader.Interpret(SurveyReader.ReadXlsx(bytes));
            Assert.Equal(2, f.Points.Count);
            Assert.Equal("P1", f.Points[0].Name);
            Assert.Equal(1234000.125, f.Points[0].X, 6);
            Assert.Equal(11.0, f.Points[1].Z.Value, 6);
            Assert.Equal(2, SurveyReader.ColumnIndex("C7"));
            Assert.Equal(27, SurveyReader.ColumnIndex("AB1"));
        }

        [Fact]
        public void Vn2000_axes_are_swapped_and_swapped_columns_are_warned()
        {
            var p = new SurveyPoint { X = 1234012.5, Y = 567045.25, Z = 12.35 };
            var d = SurveyReader.ToDrawing(p, 1234000, 567000, true);
            Assert.Equal(45250, d.x, 6);
            Assert.Equal(12500, d.y, 6);
            Assert.Equal(12350, d.z, 6);
            var nd = SurveyReader.ToDrawing(p, 1234000, 567000, false);
            Assert.Equal(12500, nd.x, 6);
            Assert.Equal((1234000, 567000), SurveyReader.AutoOrigin(new[] { p, new SurveyPoint { X = 1234099, Y = 567150 } }));
            Assert.True(SurveyReader.IsReal(new[] { p }));
            Assert.False(SurveyReader.IsReal(new[] { new SurveyPoint { X = 100, Y = 200 } }));
            Assert.Null(SurveyReader.LooksSwapped(new[] { p }));
            Assert.NotNull(SurveyReader.LooksSwapped(new[] { new SurveyPoint { X = 567045, Y = 1234012 } }));
            Assert.True(H6Numbers.AxisSwap);
        }

        [Fact]
        public void Duplicates_are_found()
        {
            var d = SurveyReader.Duplicates(new List<(double, double)> { (0, 0), (5, 5), (0.0004, 0), (5, 5.0009) }, 1);
            Assert.Equal(new[] { 2, 3 }, d);
        }

        private static List<(double x, double y, double z)> Grid(int n, double step, Func<double, double, double> z)
        {
            var r = new List<(double, double, double)>();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) r.Add((i * step, j * step, z(i * step, j * step)));
            return r;
        }

        [Fact]
        public void Tin_of_a_grid_has_every_triangle_and_interpolates_a_plane_exactly()
        {
            var pts = Grid(10, 1000, (x, y) => 0.05 * x + 0.02 * y);
            var t = Tin.Build(pts, 0);
            Assert.Equal(2 * 9 * 9, t.Triangles.Count);
            Assert.Equal(0.05 * 4321 + 0.02 * 777, t.ZAt(4321, 777).Value, 6);
            Assert.Null(t.ZAt(-10, 500));
            Assert.Null(t.ZAt(9001, 500));
        }

        [Fact]
        public void Tin_of_random_points_is_delaunay_and_long_edges_are_dropped()
        {
            var rnd = new Random(7);
            var pts = Enumerable.Range(0, 400).Select(_ => (rnd.NextDouble() * 1e5 + 5e8, rnd.NextDouble() * 1e5 + 1.2e9, rnd.NextDouble() * 1000)).ToList();
            var t = Tin.Build(pts, 0);
            // empty circumcircles
            foreach (var tr in t.Triangles.Take(200))
            {
                double ax = t.X[tr[0]], ay = t.Y[tr[0]], bx = t.X[tr[1]], by = t.Y[tr[1]], cx = t.X[tr[2]], cy = t.Y[tr[2]];
                double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
                double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
                double r2 = (ax - ux) * (ax - ux) + (ay - uy) * (ay - uy);
                for (int i = 0; i < t.X.Length; i++)
                    if (i != tr[0] && i != tr[1] && i != tr[2]) Assert.True((t.X[i] - ux) * (t.X[i] - ux) + (t.Y[i] - uy) * (t.Y[i] - uy) >= r2 * (1 - 1e-6));
            }
            // Euler: a triangulation of n points with h on the hull has 2n - 2 - h triangles: at least n
            Assert.InRange(t.Triangles.Count, 400, 2 * 400);
            var auto = Tin.Build(pts);
            Assert.True(auto.Triangles.Count <= t.Triangles.Count);
            Assert.Throws<ToolException>(() => Tin.Build(new List<(double, double, double)> { (0, 0, 1), (1, 1, 1), (2, 2, 1) }, 0));
            Assert.Throws<ToolException>(() => Tin.Build(new List<(double, double, double)> { (0, 0, 1), (1, 1, 1) }, 0));
        }

        [Fact]
        public void Contours_of_a_plane_are_straight_lines_one_per_level_with_majors()
        {
            // z = x / 1000 m, 0 … 9 m over 9 000 mm: levels every 1 m (in mm: 1000)
            var t = Tin.Build(Grid(10, 1000, (x, y) => x), 0);
            var lines = Contouring.Trace(t, 1000, 5);
            Assert.Equal(9, lines.Count); // 1 … 9: level 0 is the lowest edge (a vertex on a level counts as above it)
            foreach (var l in lines.Where(l => l.Level > 0 && l.Level < 9000))
            {
                Assert.All(l.Points, q => Assert.Equal(l.Level, q.x, 6));
                Assert.False(l.Closed);
                Assert.Equal(0, l.Points.Min(q => q.y), 6);
                Assert.Equal(9000, l.Points.Max(q => q.y), 6);
            }
            Assert.Equal(new[] { 5000.0 }, lines.Where(l => l.Major).Select(l => l.Level));
        }

        [Fact]
        public void A_hill_gives_closed_contours()
        {
            var t = Tin.Build(Grid(21, 500, (x, y) => 10000 - Math.Sqrt((x - 5000) * (x - 5000) + (y - 5000) * (y - 5000))), 0);
            var lines = Contouring.Trace(t, 1000, 5);
            var at8 = lines.Where(l => Math.Abs(l.Level - 8000) < 1e-6).ToList();
            Assert.Single(at8);
            Assert.True(at8[0].Closed);
            Assert.All(at8[0].Points, q => Assert.InRange(Math.Sqrt((q.x - 5000) * (q.x - 5000) + (q.y - 5000) * (q.y - 5000)), 1900, 2100));
        }

        [Fact]
        public void Levels_and_their_limit()
        {
            var l = Contouring.Levels(1.2, 4.9, 0.5, 5);
            Assert.Equal(new[] { 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5 }, l.Select(v => v.level));
            Assert.Equal(new[] { 2.5 }, l.Where(v => v.major).Select(v => v.level));
            Assert.Throws<ToolException>(() => Contouring.Levels(0, 1e6, 0.1, 5));
            Assert.Throws<ToolException>(() => Contouring.Levels(0, 1, 0, 5));
        }

        [Fact]
        public void Labels_stay_readable()
        {
            Assert.Equal(0, Contouring.Readable(Math.PI), 9);
            Assert.Equal(Math.PI / 2, Contouring.Readable(Math.PI / 2), 9);
            Assert.Equal(Math.PI / 2, Contouring.Readable(-Math.PI / 2), 9);
            var s = Contouring.LabelSpot(new List<(double, double)> { (0, 0), (10, 0), (10, -100) });
            Assert.Equal(10, s.x, 6); Assert.Equal(-50, s.y, 6);
            Assert.Equal(110, s.length, 6);
        }

        [Fact]
        public void Chainage_and_level_texts()
        {
            Assert.Equal("Km0+125.50", Stations.Chainage(125.5));
            Assert.Equal("Km1+000.00", Stations.Chainage(999.999));
            Assert.Equal("Km12+040.00", Stations.Chainage(12040));
            Assert.Equal("+12.350", Stations.Level(12.35, 3));
            Assert.Equal("-0.20", Stations.Level(-0.2, 2));
            Assert.Equal("+0.000", Stations.Level(-0.0001, 3));
        }

        [Fact]
        public void Stakes_named_Km_H_C_with_vertices_start_and_end()
        {
            var s = Stations.Stakes(250, new[] { 130.0, 200.0 }, 0, 20);
            Assert.Equal("Km0", s[0].Name);
            Assert.Equal("C1", s[1].Name);
            Assert.Equal(20, s[1].Ch, 6);
            Assert.Equal("H1", s.Single(x => Math.Abs(x.Ch - 100) < 1e-6).Name);
            Assert.Equal("Đ1", s.Single(x => Math.Abs(x.Ch - 130) < 1e-6).Name);
            Assert.Equal("H2/Đ2", s.Single(x => Math.Abs(x.Ch - 200) < 1e-6).Name);
            Assert.Equal("Cuối", s[s.Count - 1].Name);
            Assert.Equal(250, s[s.Count - 1].S, 6);
            Assert.True(s.Zip(s.Skip(1), (a, b) => b.S > a.S).All(x => x));
            var mid = Stations.Stakes(50, null, 990, 20);
            Assert.Equal("Đầu", mid[0].Name);
            Assert.Equal("Km1", mid.Single(x => Math.Abs(x.Ch - 1000) < 1e-6).Name);
            Assert.Throws<ToolException>(() => Stations.Stakes(1e6, null, 0, 1));
        }

        [Fact]
        public void Datum_design_and_grades()
        {
            Assert.Equal(8, Stations.Datum(new double?[] { 10.4, null, 12 }));
            Assert.Equal(7, Stations.Datum(new double?[] { 9.99 }));
            Assert.Throws<ToolException>(() => Stations.Datum(new double?[] { null }));
            var d = new List<(double, double)> { (0, 10), (100, 12) };
            Assert.Equal(11, Stations.DesignAt(d, 50).Value, 6);
            Assert.Null(Stations.DesignAt(d, 150));
            Assert.Equal(2, Stations.Grades(d)[0].pct, 6);
        }

        [Fact]
        public void Profile_frame_rows_scales_and_lines()
        {
            var st = Stations.Stakes(100, null, 0, 20);
            var g = st.Select(x => (double?)(10 + x.S / 100)).ToList();
            g[2] = null;
            var d = SectionLayout.Profile(st, g, null, 0, 0, 1000, 100, null, "TRẮC DỌC", null);
            Assert.Equal(8, d.Datum, 6);
            double rowH = H6Numbers.ProfileRowMm * 1000, labelW = H6Numbers.ProfileLabelMm * 1000;
            Assert.Equal(labelW + 100 * 1000, d.Width, 6);
            var ground = d.Prims.Where(p => p.Layer == H6Numbers.LayerGround.name).ToList();
            Assert.Equal(2, ground.Count); // broken at the stake off the surface
            // vertical × 10: 10 m above a datum of 8 at 4 rows → y = 4 rows + 2 m × 1000 × 10
            Assert.Equal(4 * rowH + 2 * 1000 * 10, ground[0].Pts[0].y, 6);
            Assert.Contains(d.Prims, p => p.Kind == "text" && p.Text == "MSS +8.00");
            Assert.Contains(d.Prims, p => p.Kind == "text" && p.Text == "-" );
            Assert.Contains(d.Prims, p => p.Kind == "text" && p.Text == "Tỉ lệ ngang 1:1000, tỉ lệ đứng 1:100");
            var withDesign = SectionLayout.Profile(st, g, st.Select(x => (double?)11).ToList(), 0, 0, 1000, 100, null, "T", Stations.Grades(new List<(double, double)> { (0, 11), (100, 11) }));
            Assert.Contains(withDesign.Prims, p => p.Layer == H6Numbers.LayerDesign.name);
            Assert.Contains(withDesign.Prims, p => p.Text == "Chênh cao TK - TN (m)");
            Assert.Contains(withDesign.Prims, p => p.Text == "i = 0.00%");
        }

        [Fact]
        public void Cross_section_breakpoints_and_frame()
        {
            var samples = Enumerable.Range(-15, 31).Select(i => ((double)i, (double?)(i < 0 ? 10 : 10 + i * 0.1))).ToList();
            samples[0] = (-15, null);
            var bp = SectionLayout.Breakpoints(samples);
            Assert.Equal(new[] { -14.0, 0.0, 15.0 }, bp.Select(p => p.off));
            var st = new Stake { S = 40, Ch = 40, Name = "C2" };
            var d = SectionLayout.Cross(st, bp, 15, 0, 0, 200, null);
            Assert.Equal(8, d.Datum, 6);
            Assert.Contains(d.Prims, p => p.Kind == "text" && p.Text == "Cọc C2  Km0+040.00");
            var line = d.Prims.Single(p => p.Kind == "pline");
            Assert.Equal(-14000, line.Pts[0].x, 6);
            Assert.Equal(2 * H6Numbers.CrossRowMm * 200 + 2000, line.Pts[0].y, 6); // true scale: 2 m above the datum
        }

        [Fact]
        public void Approved_standard_numbers_in_one_place_match_the_skill_and_the_description()
        {
            Assert.Equal(1000, H6Numbers.ProfileScaleH);
            Assert.Equal(100, H6Numbers.ProfileScaleV);
            Assert.Equal(200, H6Numbers.CrossScale);
            Assert.Equal(20, H6Numbers.StakeDetailM);
            Assert.Equal(0.5, H6Numbers.ContourIntervalFor(500));
            Assert.Equal(1.0, H6Numbers.ContourIntervalFor(1000));
            Assert.Equal(1.0, H6Numbers.ContourIntervalFor(2500));
            var desc = Src("agent", "src", "tools", "terrain.ts");
            var prompt = Src("agent", "src", "agent", "systemPrompt.ts");
            var skill = Src("skills", "ve-ha-tang", "SKILL.md");
            // approved 26/11: the skill and the description say the same numbers as H6Numbers
            foreach (var n in new[] { "1:1000", "1:100", "1:200", "15 m", "20 m", "50 m", "2 m" })
                Assert.Contains(n, desc);
            foreach (var n in new[] { "1:1000", "đứng 1:100", "1:200", "15 m", "20 m", "50 m", "0,5 m", "Cứ 5 đường", "terrain" })
                Assert.Contains(n, skill);
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", skill);
            Assert.Contains("TCVN in force", desc);
            Assert.Contains("terrain", prompt);
            Assert.DoesNotContain("trắc dọc 1:", prompt);
        }

        [Fact]
        public void Terrain_is_registered_2d_labelled_wcs_and_not_a_card_tool()
        {
            Assert.Contains("terrain", Src("plugin", "ClaudeCAD.Plugin", "Core", "Plans.cs"));
            Assert.Contains("\"terrain\"", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            Assert.Contains("RegisterTerrain(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            Assert.Contains("terrain", UcsLogicWcs());
            Assert.DoesNotContain("'terrain'", Src("agent", "src", "agent", "confirmation.ts"));
        }

        private static string UcsLogicWcs() => Src("plugin", "ClaudeCAD.Plugin", "Core", "Ucs.cs");
    }
}
