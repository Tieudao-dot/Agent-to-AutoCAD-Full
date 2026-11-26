using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa KTP (kiểm tra độc lập đợt H phần 2), phần H6: docs/ban-giao/nang-cap-h-sua-ktp-c.md.</summary>
    [Collection("H6Numbers")]
    public class SuaKtpCTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void KTP08_four_columns_with_number_names_that_jump()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("1;1234012.5;567045.25;12.35\n4;1234020;567050;12.8\n9;1234030;567060;13.1\n15;1234040;567070;13.4\n"));
            Assert.Equal("4", f.Points[1].Name);
            Assert.Equal(1234020, f.Points[1].X, 6);
            Assert.Equal(12.8, f.Points[1].Z.Value, 6);
        }

        [Fact]
        public void KTP09_three_columns_name_x_y_with_real_coordinates()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("1,1234012.5,567045.25\n2,1234020,567050\n"));
            Assert.Equal("1", f.Points[0].Name);
            Assert.Equal(1234012.5, f.Points[0].X, 6);
            Assert.False(f.HasZ);
            // local X, Y, Z stays X, Y, Z
            var g = SurveyReader.Interpret(SurveyReader.ParseText("100 200 1.5\n101 201 1.6\n"));
            Assert.Equal(100, g.Points[0].X, 6);
        }

        [Fact]
        public void KTP10_11_vietnamese_thousands_and_text_numbers()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("Tên;X;Y;Z\nM1;1.234.012,500;567.045,250;12,350\nM2;1 234 020,0;567 050,0;12,8\n"));
            Assert.Equal(2, f.Points.Count);
            Assert.Equal(1234012.5, f.Points[0].X, 6);
            Assert.Equal(567045.25, f.Points[0].Y, 6);
            Assert.Equal(1234020, f.Points[1].X, 6);
            Assert.Equal(567050, f.Points[1].Y, 6);
            Assert.Equal("12.350", SurveyReader.NumberText("12.350")); // one dot stays a decimal point
            var bytes = XlsxWriter.Build(new List<IList<object>> { new List<object> { "Tên", "X", "Y", "Z" }, new List<object> { "P1", "1234012,5", "567045,25", "12,35" } }, "Diem");
            var x = SurveyReader.Interpret(SurveyReader.ReadXlsx(bytes));
            Assert.Single(x.Points);
            Assert.Equal(1234012.5, x.Points[0].X, 6);
        }

        [Fact]
        public void KTP12_space_separated_text_with_decimal_commas()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("P1 1234012,5 567045,25 12,35\nP2 1234020,0 567050,0 12,80\n"));
            Assert.Equal(2, f.Points.Count);
            Assert.Equal("P1", f.Points[0].Name);
            Assert.Equal(567045.25, f.Points[0].Y, 6);
            Assert.Equal(12.35, f.Points[0].Z.Value, 6);
            // a real CSV with commas stays comma separated
            Assert.Equal(',', SurveyReader.Delimiter(new[] { "P1,1,2,3", "P2,4,5,6" }));
            Assert.Equal(',', SurveyReader.Delimiter(new[] { "P1, 1.5, 2, 3", "P2, 4, 5, 6" }));
        }

        [Fact]
        public void KTP22_a_name_column_wins_over_stt()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("STT;Tên điểm;X;Y;Z\n1;GPS-01;1234012.5;567045.25;12.35\n"));
            Assert.Equal("GPS-01", f.Points[0].Name);
            var g = SurveyReader.Interpret(SurveyReader.ParseText("STT;X;Y;Z\n7;1234012.5;567045.25;12.35\n"));
            Assert.Equal("7", g.Points[0].Name); // no name column: STT names the point
        }

        [Fact]
        public void KTP23_skipped_rows_are_the_lines_of_the_file()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("Tên;X;Y;Z\n\nM1;1;2;3\n\nM2;x;2;3\n"));
            Assert.Single(f.Points);
            Assert.Equal(3, f.Points[0].Row);
            Assert.Equal("row 5: X / Y is not a number", f.Skipped.Single());
            // .xlsx: an empty row 2 of the sheet
            var bytes = XlsxWriter.Build(new List<IList<object>> { new List<object> { "Tên", "X", "Y", "Z" }, new List<object>(), new List<object> { "P1", "a", 2.0, 3.0 } }, "Diem");
            var x = SurveyReader.Interpret(SurveyReader.ReadXlsx(bytes));
            Assert.Equal("row 3: X / Y is not a number", x.Skipped.Single());
        }

        [Fact]
        public void KTP24_texts_follow_the_drawing_units()
        {
            Assert.DoesNotContain("m × 1000", Src("agent", "src", "tools", "terrain.ts"));
            Assert.DoesNotContain("WCS, mm", Src("agent", "src", "tools", "terrain.ts"));
            Assert.DoesNotContain("metres × 1000", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TerrainTools.cs"));
            Assert.DoesNotContain("m × 1000", Src("skills", "ve-ha-tang", "SKILL.md"));
            // Sửa KTP lượt 2: "nhân 1000", "x_cad = Y × 1000", "×1000" (only "× 1000 khi bản vẽ mm" may stay)
            var skill = Src("skills", "ve-ha-tang", "SKILL.md");
            Assert.DoesNotContain("nhân 1000", skill);
            Assert.Empty(System.Text.RegularExpressions.Regex.Matches(skill, @"×\s?1000(?! khi bản vẽ mm)"));
        }

        [Fact]
        public void KTP25_cross_section_breakpoints_never_drift_past_the_flatness()
        {
            // a slow curve: each sample 6 mm off the chord of its neighbours, but 3 cm off over the whole side
            var samples = Enumerable.Range(0, 16).Select(i => ((double)i, (double?)(0.0008 * i * i))).ToList();
            var bp = SectionLayout.Breakpoints(samples, 0.01);
            for (int i = 0; i < samples.Count; i++)
            {
                var a = bp.Last(p => p.off <= samples[i].Item1); var b = bp.First(p => p.off >= samples[i].Item1);
                double zl = a.off == b.off ? a.z : a.z + (samples[i].Item1 - a.off) / (b.off - a.off) * (b.z - a.z);
                Assert.True(Math.Abs(samples[i].Item2.Value - zl) <= 0.01 + 1e-9, $"sample {i}");
            }
        }

        [Fact]
        public void KTP26_a_vertex_on_the_end_stake_keeps_its_name()
        {
            var s = Stations.Stakes(250.005, new[] { 250.0 }, 0, 20);
            Assert.Equal("Cuối/Đ1", s[s.Count - 1].Name);
            Assert.Equal("end", s[s.Count - 1].Kind);
        }

        [Fact]
        public void Suspicions_and_the_alignment_length_in_drawing_units()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TerrainTools.cs");
            // lượt 2: clamped to the length measured on the curve (not metres × units)
            Assert.Contains("double len = LengthUnits;", src);
            Assert.Contains("a.LengthUnits = c.GetDistanceAtParameter(c.EndParam)", src);
            Assert.Contains("ctx.Db.UpdateExt(true);", src);
            Assert.Contains("Split('/').Any(want.Contains)", src);
            // a grid in VN-2000 millimetres keeps every hull triangle
            var pts = new List<(double, double, double)>();
            for (int i = 0; i < 6; i++) for (int j = 0; j < 6; j++) pts.Add((5.67e8 + i * 1000, 1.234e9 + j * 1000, i));
            Assert.Equal(2 * 5 * 5, Tin.Build(pts, 0).Triangles.Count);
        }
    
        [Fact]
        public void KTP37_header_after_blank_rows_is_still_found()
        {
            var text = "BẢNG TỌA ĐỘ ĐIỂM ĐO\n" + new string('\n', 10) + "STT;Tên;X;Y;Z\n1;P1;1234012.5;567045.25;12.35\n2;P2;1234020;567050;12.8\n";
            var f = SurveyReader.Interpret(SurveyReader.ParseText(text));
            Assert.StartsWith("from the header row", f.Columns);
            Assert.Equal("P1", f.Points[0].Name);
            Assert.Equal(1234012.5, f.Points[0].X, 6);
            Assert.Equal(12.35, f.Points[0].Z.Value, 6);
        }

        [Fact]
        public void KTP39_a_one_word_line_does_not_turn_decimal_commas_into_separators()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("SODIEM\nP1 1234012,5 567045,25 12,35\nP2 1234020,5 567050,75 12,8\n"));
            Assert.Equal(2, f.Points.Count);
            Assert.Equal(567045.25, f.Points[0].Y, 6);
            Assert.Equal(12.8, f.Points[1].Z.Value, 6);
        }

        [Fact]
        public void KTP48_two_word_header_in_a_space_separated_file()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("Tên điểm  X  Y  Cao độ\nP1 1234012.5 567045.25 12.35\nP2 1234020 567050 12.8\n"));
            Assert.StartsWith("from the header row", f.Columns);
            Assert.Equal(2, f.Points.Count);
            Assert.Equal("P2", f.Points[1].Name);
            Assert.Equal(567050, f.Points[1].Y, 6);
            Assert.Equal(12.8, f.Points[1].Z.Value, 6);
            // a delimited file with the same words in one cell is unchanged
            var g = SurveyReader.Interpret(SurveyReader.ParseText("Tên điểm;X;Y;Z\nP1;1234012.5;567045.25;12.35\n"));
            Assert.Equal("P1", g.Points[0].Name);
        }
}
}
