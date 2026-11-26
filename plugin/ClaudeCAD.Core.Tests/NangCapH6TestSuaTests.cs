using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt H6 (26/11/2026, docs/ban-giao/nang-cap-h6-test.md): the fixes of the review of <c>terrain</c> (H6Numbers
    /// / SurveyReader / Stations / SectionLayout) and its cross checks with the older parts.
    /// </summary>
    [Collection("H6Numbers")] // UnitsPerMetre is changed for a call: these tests never run beside each other
    public class NangCapH6TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        // ------------------------------------------------------------ H6T-1: Vietnamese code page and UTF-16 files

        [Fact]
        public void A_windows_1258_file_is_read_with_its_vietnamese_header()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // "Tên;Y;X;Cao độ" as a Windows-1258 file writes it: ê = EA, đ = F0, ô = F4, then the dot below apart (F2)
            var head = new byte[] { (byte)'T', 0xEA, (byte)'n', (byte)';', (byte)'Y', (byte)';', (byte)'X', (byte)';', (byte)'C', (byte)'a', (byte)'o', (byte)' ', 0xF0, 0xF4, 0xF2 };
            var bytes = head.Concat(Encoding.ASCII.GetBytes("\r\nM1;567045,25;1234012,5;12,35\r\n")).ToArray();
            var text = SurveyReader.DecodeText(bytes);
            Assert.Contains("Cao độ", text.Normalize(NormalizationForm.FormC));
            var f = SurveyReader.Interpret(SurveyReader.ParseText(text));
            var p = Assert.Single(f.Points);
            Assert.Equal(1234012.5, p.X, 6); // the header, not the position: "Y; X" in that order
            Assert.Equal(12.35, p.Z.Value, 6);
            Assert.Contains("NETCOREAPP", Src("plugin", "ClaudeCAD.Plugin", "Core", "Terrain.cs"));
        }

        [Fact]
        public void An_excel_unicode_text_file_utf16_is_read()
        {
            var text = "Tên\tX\tY\tCao độ\r\nM1\t1234012.5\t567045.25\t12.35\r\n";
            var le = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            var be = new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray();
            foreach (var b in new[] { le, be })
            {
                var f = SurveyReader.Interpret(SurveyReader.ParseText(SurveyReader.DecodeText(b)));
                Assert.Equal("M1", Assert.Single(f.Points).Name);
                Assert.True(f.HasZ);
            }
        }

        // ------------------------------------------------------------ H6T-2: four columns without a header

        [Fact]
        public void X_Y_Z_and_a_code_without_a_header_are_not_read_as_a_name_first()
        {
            var f = SurveyReader.Interpret(SurveyReader.ParseText("1234012.5,567045.25,12.35,TN\n1234020,567050,12.8,coc\n"));
            Assert.Equal(2, f.Points.Count);
            Assert.Equal(1234012.5, f.Points[0].X, 6);
            Assert.Equal(12.35, f.Points[0].Z.Value, 6);
            Assert.Equal("TN", f.Points[0].Code);
            // four numbers, the first one row numbers 1, 2, 3: STT, X, Y, Z (as before)
            var s = SurveyReader.Interpret(SurveyReader.ParseText("1,100.5,200.25,3.1\n2,110,205,3.4\n3,120,210,3.6\n"));
            Assert.Equal("1", s.Points[0].Name);
            Assert.Equal(100.5, s.Points[0].X, 6);
            // four numbers, the first one coordinates: X, Y, Z, (code)
            var x = SurveyReader.Interpret(SurveyReader.ParseText("1234012.5,567045.25,12.35,1\n1234020.1,567050.7,12.8,2\n"));
            Assert.Equal(1234012.5, x.Points[0].X, 6);
            Assert.True(SurveyReader.LooksLikeRowNumbers(new List<List<string>> { new List<string> { "101" }, new List<string> { "102" }, new List<string> { "103" } }));
            Assert.False(SurveyReader.LooksLikeRowNumbers(new List<List<string>> { new List<string> { "100" }, new List<string> { "250" }, new List<string> { "400" } }));
        }

        // ------------------------------------------------------------ H6T-3: a drawing in metres

        [Fact]
        public void A_drawing_in_metres_gets_metres_and_paper_sizes_in_metres()
        {
            Assert.Equal(1000, H6Numbers.UnitsPerMetre);
            using (H6Numbers.ForDrawing(1000)) // INSUNITS = metres
            {
                Assert.Equal(1, H6Numbers.UnitsPerMetre);
                Assert.Equal(1.25, H6Numbers.Paper(H6Numbers.TextMm, 500), 9); // 2,5 mm at 1:500 = 1,25 m
                var d = SurveyReader.ToDrawing(new SurveyPoint { X = 1234012.5, Y = 567045.25, Z = 12.35 }, 1234000, 567000, true);
                Assert.Equal(45.25, d.x, 6);
                Assert.Equal(12.5, d.y, 6);
                Assert.Equal(12.35, d.z, 6);
                var stakes = Stations.Stakes(100, null, 0, 20);
                var prof = SectionLayout.Profile(stakes, stakes.Select(s => (double?)10).ToList(), null, 0, 0, 1000, 100, null, "T");
                Assert.InRange(prof.Width, 100, 200); // 100 m long + label column 45 m, not 100 000
            }
            Assert.Equal(1000, H6Numbers.UnitsPerMetre); // restored after the call
            using (H6Numbers.ForDrawing(1)) Assert.Equal(1250, H6Numbers.Paper(H6Numbers.TextMm, 500), 9);
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TerrainTools.cs");
            Assert.Contains("using (H6Numbers.ForDrawing(mmPerUnit))", t);
            Assert.DoesNotMatch(@"H6Numbers\.(TextMm|TitleMm|BigTitleMm|PlanTickMm|GapMm|CrossLabelMm) \*", t);
            Assert.DoesNotMatch(@"H6Numbers\.(TextMm|TitleMm|BigTitleMm|ProfileRowMm|ProfileLabelMm|CrossRowMm|CrossLabelMm) \*", Src("plugin", "ClaudeCAD.Plugin", "Core", "Terrain.cs"));
        }

        // ------------------------------------------------------------ H6T-4: civil layers are standard for the drawing check

        [Fact]
        public void The_civil_layers_of_terrain_are_standard_layers_for_check_drawing()
        {
            foreach (var l in new[] { H6Numbers.LayerPoint.name, H6Numbers.LayerMinor.name, H6Numbers.LayerMajor.name, H6Numbers.LayerStation.name, H6Numbers.LayerGrid.name, H6Numbers.LayerText.name, "C-LEVEL" })
                Assert.True(DrawingCheckLogic.IsStandardLayer(l), l);
            Assert.False(DrawingCheckLogic.IsStandardLayer("TUONG"));
        }

        // ------------------------------------------------------------ H6T-5: one name, one place

        [Fact]
        public void A_plan_tick_at_50_m_never_takes_the_name_of_a_20_m_stake()
        {
            var plan = Stations.Stakes(200, new[] { 150.0 }, 0, 50);
            var labels = plan.Select(s => Stations.PlanLabel(s, 50)).ToList();
            Assert.Contains("Km0+050.00", labels);       // a detail tick: chainage only
            Assert.DoesNotContain(labels, l => l.StartsWith("C", StringComparison.Ordinal));
            Assert.Contains(labels, l => l.StartsWith("H1 ", StringComparison.Ordinal));
            Assert.Contains(labels, l => l.StartsWith("Đ1 ", StringComparison.Ordinal)); // a vertex on a 50 m tick keeps its name
            var sec = Stations.Stakes(100, null, 0, 20);
            Assert.Equal("C1 Km0+020.00", Stations.PlanLabel(sec.First(s => s.Kind == "detail"), 20)); // at 20 m: the same names
        }

        // ------------------------------------------------------------ the approved numbers

        [Fact]
        public void The_numbers_approved_26_11_are_in_the_code()
        {
            Assert.Equal(1000, H6Numbers.ProfileScaleH);
            Assert.Equal(100, H6Numbers.ProfileScaleV);
            Assert.Equal(200, H6Numbers.CrossScale);
            Assert.Equal(2, H6Numbers.DatumMarginM);
            Assert.Equal(20, H6Numbers.StakeDetailM);
            Assert.Equal(100, H6Numbers.Stake100M);
            Assert.Equal(1000, H6Numbers.StakeKmM);
            Assert.Equal(50, H6Numbers.PlanTickM);
            Assert.Equal(15, H6Numbers.CrossHalfWidthM);
            Assert.Equal(new[] { (500, 0.5), (1000, 1.0), (2000, 1.0) }, H6Numbers.ContourIntervals);
            Assert.Equal(5, H6Numbers.MajorEvery);
            Assert.Equal((3, 2, 2, 2), (H6Numbers.DecPoint, H6Numbers.DecSection, H6Numbers.DecDistance, H6Numbers.DecSlope));
            Assert.Equal((2.5, 3.5, 5.0), (H6Numbers.TextMm, H6Numbers.TitleMm, H6Numbers.BigTitleMm));
            // the two "not sure" ones say: the engineer checks the current TCVN
            var skill = Src("skills", "ve-ha-tang", "SKILL.md");
            var desc = Src("agent", "src", "tools", "terrain.ts");
            Assert.Contains("TCVN", skill);
            Assert.Contains("TCVN", desc);
        }

        [Fact]
        public void The_datum_is_whole_metres_at_least_two_below_ground_and_design()
        {
            Assert.Equal(7, Stations.Datum(new double?[] { 9.4, 10.2, null }));
            Assert.Equal(6, Stations.Datum(new double?[] { 8.0 }));
            var stakes = Stations.Stakes(40, null, 0, 20);
            var prof = SectionLayout.Profile(stakes, new double?[] { 10, 10.5, 11 }, new double?[] { 8.2, 8.4, 8.6 }, 0, 0, 1000, 100, null, "T");
            Assert.Equal(6, prof.Datum); // the design (8.2) is lower than the ground: the datum is under both
        }
    }

    [CollectionDefinition("H6Numbers", DisableParallelization = true)]
    public class H6NumbersCollection { }
}
