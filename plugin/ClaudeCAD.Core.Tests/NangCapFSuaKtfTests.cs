using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa lỗi kiểm tra độc lập đợt F (25/11/2026, docs/ban-giao/nang-cap-f-sua-ktf.md), phần F1 (UCS) và F4 (Excel,
    /// khởi động sẵn): KTF-02 … KTF-06 and the checked suspicions. Each test keeps one fix from coming back.
    /// </summary>
    public class NangCapFSuaKtfTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static List<IList<object>> Rows(List<List<object>> d) => d.Select(r => (IList<object>)r).ToList();

        // ------------------------------------------------------------ KTF-02: m² / m³ are numbers in Excel

        [Fact]
        public void Ktf02_area_column_is_a_number_in_excel_and_text_in_the_drawing_table()
        {
            var rows = new List<Util2DLogic.AreaRow>
            {
                new Util2DLogic.AreaRow { Name = "PHÒNG NGỦ 1", Layer = "A-ROOM", Count = 1, Area = 12.5e6 },
                new Util2DLogic.AreaRow { Name = "BẾP", Layer = "A-ROOM", Count = 1, Area = 8.25e6 },
            };
            var table = Util2DLogic.AreaTable(rows, "room", 2);
            Assert.Equal("12,50", table[1][3]); // the drawing table keeps the Vietnamese decimal comma
            var xl = XlsxLogic.ForExcel(table);
            Assert.Equal(12.5, (double)xl[1][3], 9);
            Assert.Equal(8.25, (double)xl[2][3], 9);
            Assert.Equal(20.75, (double)xl[3][3], 9); // the Tổng row too
            Assert.Equal("12,50", table[1][3]); // ForExcel does not change the drawing's table
            var sheet = XlsxWriter.SheetXml(Rows(xl));
            Assert.Contains("<c r=\"D2\"><v>12.5</v></c>", sheet);
            Assert.DoesNotContain(">12,50<", sheet);
        }

        [Fact]
        public void Ktf02_volume_and_clash_columns_are_numbers_and_their_text_stays_text()
        {
            var vr = new Solid3Logic.VolResult();
            vr.Rows.Add(new Solid3Logic.VolRow { Layer = "A-WALL-3D", Count = 2, Gross = 2.904e9, Deducted = 0.1e9, Area = 30e6 });
            var vol = XlsxLogic.ForExcel(Solid3Logic.VolumeTable(vr, 3, 1, true));
            for (int c = 3; c <= 6; c++) Assert.IsType<double>(vol[1][c]);
            Assert.Equal(2.904, (double)vol[1][3], 9);
            Assert.Equal(2.804, (double)vol[1][5], 9);
            var clash = XlsxLogic.ForExcel(Solid3Logic.ClashTable(new List<Solid3Logic.ClashRow>
            {
                new Solid3Logic.ClashRow { A = "1A", LayerA = "DAM", B = "2B", LayerB = "ONG", Volume = 0.05e9 },
                new Solid3Logic.ClashRow { A = "1A", LayerA = "DAM", B = "3C", LayerB = "ONG", Volume = null },
            }, 3, 1));
            Assert.Equal(0.05, (double)clash[1][5], 9);
            Assert.Equal("chưa đo được", clash[2][5]);
            Assert.Equal("2 cặp", clash[3][3]); // not a quantity column: text
            Assert.Equal(0.05, (double)clash[3][5], 9);
        }

        [Theory]
        [InlineData("Diện tích (m²)", true)]
        [InlineData("Thể tích thực (m³)", true)]
        [InlineData("Tên phòng", false)]
        [InlineData("Số lượng", false)]
        public void Ktf02_quantity_columns_are_found_by_their_unit(string header, bool want) =>
            Assert.Equal(want, XlsxLogic.IsQuantityColumn(header));

        [Fact]
        public void Ktf02_the_export_converts_before_writing()
        {
            Assert.Contains("XlsxLogic.ForExcel(table())", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExcelExport.cs"));
        }

        // ------------------------------------------------------------ KTF-06: no rows → no file; rows without Tổng

        [Fact]
        public void Ktf06_an_empty_block_schedule_writes_no_file_and_rows_leave_the_total_out()
        {
            var empty = Util2DLogic.BlockTable(new List<Util2DLogic.CountRow>(), null);
            Assert.Equal(0, XlsxLogic.DataRows(empty));
            var three = Util2DLogic.BlockTable(new List<Util2DLogic.CountRow>
            {
                new Util2DLogic.CountRow { Name = "CUA-DI-900", Count = 3 },
                new Util2DLogic.CountRow { Name = "CUA-SO-1200", Count = 2 },
                new Util2DLogic.CountRow { Name = "TB-LAVABO", Count = 1 },
            }, null);
            Assert.Equal(3, XlsxLogic.DataRows(three));
            Assert.Equal(0, XlsxLogic.DataRows(null));
            var x = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExcelExport.cs");
            Assert.Contains("if (dataRows == 0)", x);
            Assert.Contains("[\"rows\"] = dataRows", x);
        }

        // ------------------------------------------------------------ KTF-03: tight box in a UCS turned 30°

        [Fact]
        public void Ktf03_a_line_along_a_30_degree_ucs_is_inside_its_window()
        {
            double a = 30 * Math.PI / 180;
            var f = new UcsFrame(new double[3], new[] { Math.Cos(a), Math.Sin(a), 0 }, new[] { -Math.Sin(a), Math.Cos(a), 0 });
            var p0 = f.ToWcs(new[] { 0.0, 0 });
            var p1 = f.ToWcs(new[] { 100.0, 0 });
            double[] umin = { -10, -10 }, umax = { 110, 10 };
            // the old way: the WCS box re-boxed in the UCS grows to (0; −43.3)…(136.6; 43.3) and the line is "outside"
            var wmin = new[] { Math.Min(p0[0], p1[0]), Math.Min(p0[1], p1[1]), 0 };
            var wmax = new[] { Math.Max(p0[0], p1[0]), Math.Max(p0[1], p1[1]), 0 };
            Assert.False(f.PlanBoxInside(wmin, wmax, umin, umax));
            // the fix: the box of the shape itself in the UCS
            var (bmin, bmax) = f.BoxOfPoints(new[] { p0, p1 });
            Assert.Equal(0, bmin[1], 9);
            Assert.Equal(100, bmax[0], 9);
            Assert.True(UcsFrame.UcsBoxInside(bmin, bmax, umin, umax));
            Assert.False(UcsFrame.UcsBoxInside(bmin, bmax, new[] { -10.0, 5 }, new[] { 110.0, 10 })); // crossing / outside stay right
        }

        [Fact]
        public void Ktf03_the_window_and_area_filters_use_the_own_box()
        {
            Assert.Contains("Acad.ExtentsInUcs(e, WinFrame)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "QueryTools.cs"));
            var plan = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
            Assert.Contains("Acad.ExtentsInUcs(ent, areaFrame)", plan);
            Assert.Contains("UcsFrame.UcsBoxInside(ub.Value.min, ub.Value.max, amin, amax)", plan);
            Assert.Contains("e.GetTransformedCopy(ToWcsMatrix(f).Inverse())", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Acad.cs"));
        }

        // ------------------------------------------------------------ KTF-04 / KTF-05: heights in mm

        private static UcsFrame Raised(double z) => new UcsFrame(new[] { 0.0, 0, z }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 });

        [Fact]
        public void Ktf04_a_metre_drawing_adds_the_ucs_origin_z_in_mm()
        {
            using (UcsScope.Enter(Raised(3.3), UcsMode.Ref, "build_3d_from_plan", 1000))
                Assert.Equal(3300, new Params("build_3d_from_plan", new Dictionary<string, object> { ["base_z"] = 0.0 }).Num("base_z"), 6);
            using (UcsScope.Enter(Raised(3300), UcsMode.Ref, "build_roof", 1))
                Assert.Equal(6300, new Params("build_roof", new Dictionary<string, object> { ["wall_top_z"] = 3000.0 }).Num("wall_top_z"), 6);
            Assert.Equal(1, UcsScope.MmPerUnit); // restored outside the call
            Assert.Contains("UcsScope.Enter(frame, UcsMode.Ref, def.Name, mmPerUnit)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs"));
        }

        [Fact]
        public void Ktf05_an_omitted_height_follows_the_ucs_like_a_given_zero()
        {
            using (UcsScope.Enter(Raised(3300), UcsMode.Ref, "build_3d_from_plan", 1))
            {
                var empty = new Params("build_3d_from_plan", new Dictionary<string, object>());
                var zero = new Params("build_3d_from_plan", new Dictionary<string, object> { ["base_z"] = 0.0 });
                Assert.Equal(zero.Num("base_z"), empty.Num("base_z", 0), 6);
                Assert.Equal(3300, empty.Num("base_z", 0), 6);
            }
            // World: defaults as before
            Assert.Equal(0, new Params("build_3d_from_plan", new Dictionary<string, object>()).Num("base_z", 0));
        }

        [Theory]
        [InlineData("build_railing", "base_z")]
        [InlineData("build_stair", "base_z")]
        [InlineData("build_beam", "top_z")]
        public void Ktf05_the_ref_forms_of_railing_stair_beam_add_the_ucs_z(string tool, string param)
        {
            Assert.Equal(ParamKind.Height, UcsLogic.KindOf(tool, param));
            using (UcsScope.Enter(Raised(3300), UcsMode.Ref, tool, 1))
                Assert.Equal(4300, new Params(tool, new Dictionary<string, object> { [param] = 1000.0 }).Num(param), 6);
            // the Create forms are carried into the UCS by the transform: no conversion of the number
            using (UcsScope.Enter(Raised(3300), UcsMode.Create, tool, 1))
                Assert.Equal(1000, new Params(tool, new Dictionary<string, object> { [param] = 1000.0 }).Num(param), 6);
        }

        // ------------------------------------------------------------ suspicions checked

        [Fact]
        public void An_overwritten_excel_file_is_replaced_in_one_step()
        {
            Assert.Contains("File.Replace(temp, path, null)", Src("plugin", "ClaudeCAD.Plugin", "Core", "Xlsx.cs"));
            var dir = Path.Combine(Path.GetTempPath(), "claudecad-ktf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "a.xlsx");
                var t = new List<IList<object>> { new List<object> { "A" }, new List<object> { "1" } };
                XlsxWriter.Write(path, t, "x");
                XlsxWriter.Write(path, t, "x");
                Assert.Single(Directory.GetFiles(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Without_autocad_the_assistant_closes_claude_before_exiting()
        {
            var idx = Src("agent", "src", "index.ts");
            int gone = idx.IndexOf("parent process gone; exiting", StringComparison.Ordinal);
            Assert.True(gone > 0);
            Assert.Contains("session.close();", idx.Substring(gone, 400));
        }

        [Fact]
        public void Another_drawing_or_a_new_chat_is_prewarmed_with_its_own_skills()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("SchedulePrewarm(); // KTF (nghi vấn F4): the other drawing's chat waits ready with its own skills", c);
            Assert.Contains("SchedulePrewarm(); // KTF (nghi vấn F4): the new chat waits ready with its skills", c);
        }
    }
}
