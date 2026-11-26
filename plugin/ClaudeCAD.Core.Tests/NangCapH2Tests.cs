using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H2: export_drawing — DXF, older DWG, FBX of the 3D solids.</summary>
    public class NangCapH2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExportTools.cs");

        // ------------------------------------------------------------ formats and versions

        [Theory]
        [InlineData("dxf", "dxf")]
        [InlineData(" DWG ", "dwg")]
        [InlineData(".fbx", "fbx")]
        public void Formats(string f, string want) => Assert.Equal(want, ExportLogic.FormatOf(f));

        [Fact]
        public void Other_formats_are_refused() => Assert.Throws<ToolException>(() => ExportLogic.FormatOf("skp"));

        [Theory]
        [InlineData(null, "2013", "AC1027")]
        [InlineData("2013", "2013", "AC1027")]
        [InlineData("AutoCAD 2016", "2013", "AC1027")]   // 2013 – 2017 read the 2013 format
        [InlineData("2018", "2018", "AC1032")]
        [InlineData("2027", "2018", "AC1032")]
        [InlineData("R2010", "2010", "AC1024")]
        [InlineData("2008", "2007", "AC1021")]
        [InlineData("2004", "2004", "AC1018")]
        [InlineData("2000", "2000", "AC1015")]
        public void Versions_map_to_the_format_that_covers_them(string v, string year, string acad) =>
            Assert.Equal((year, acad), ExportLogic.VersionOf(v));

        [Fact]
        public void Older_than_2000_is_refused_and_every_version_is_an_api_name()
        {
            Assert.Contains("2000", Assert.Throws<ToolException>(() => ExportLogic.VersionOf("R14")).Message);
            Assert.Throws<ToolException>(() => ExportLogic.VersionOf("1998"));
            Assert.Equal(H2Numbers.DefaultVersion, ExportLogic.VersionOf(null).year);
            // the names exist in AutoCAD's DwgVersion (2022 – 2027): AC1015 … AC1032
            foreach (var x in ExportLogic.Versions) Assert.Matches("^AC10(15|18|21|24|27|32)$", x.acad);
            Assert.Equal("AutoCAD 2013 – 2017", ExportLogic.Covers("2013"));
        }

        // ------------------------------------------------------------ file names

        [Fact]
        public void Default_names_and_paths()
        {
            Assert.Equal("nha-acad2013.dwg", ExportLogic.DefaultName("nha", "dwg", "2013", false));
            Assert.Equal("nha-acad2010-mot-phan.dxf", ExportLogic.DefaultName("nha", "dxf", "2010", true));
            Assert.Equal("nha.fbx", ExportLogic.DefaultName("nha", "fbx", "", false));
            Assert.Equal("ban-ve-acad2013.dxf", ExportLogic.DefaultName(null, "dxf", "2013", false));
            var dir = Path.GetTempPath();
            Assert.Equal(Path.Combine(dir, "nha-acad2013.dwg"), ExportLogic.OutputPath(null, "dwg", dir, "nha", "2013", false, null));
            Assert.Equal(Path.Combine(dir, "doi-tac.dxf"), ExportLogic.OutputPath("doi-tac.dxf", "dxf", dir, "nha", "2013", false, null));
            Assert.Throws<ToolException>(() => ExportLogic.OutputPath("a.dwg", "dxf", dir, "nha", "2013", false, null));
            Assert.Throws<ToolException>(() => ExportLogic.OutputPath(".dwg", "dwg", dir, "nha", "2013", false, null));
        }

        [Fact]
        public void The_open_drawing_itself_is_never_overwritten()
        {
            var dir = Path.GetTempPath();
            var open = Path.Combine(dir, "nha.dwg");
            var ex = Assert.Throws<ToolException>(() => ExportLogic.OutputPath("nha.dwg", "dwg", dir, "nha", "2013", false, open));
            Assert.Contains("is the drawing that is open", ex.Message);
            Assert.Throws<ToolException>(() => ExportLogic.OutputPath(open.ToUpperInvariant(), "dwg", dir, "nha", "2013", false, open));
            Assert.NotNull(ExportLogic.OutputPath(null, "dwg", dir, "nha", "2013", false, open)); // default adds -acad2013
        }

        [Fact]
        public void Notes_say_the_drawing_is_unchanged_and_what_fbx_holds()
        {
            Assert.Contains("open drawing is unchanged", ExportLogic.Note("dwg", "2013", false));
            Assert.Contains("layouts are not included", ExportLogic.Note("dxf", "2013", true));
            Assert.Contains("Only 3D solids", ExportLogic.Note("fbx", "", false));
        }

        // ------------------------------------------------------------ FBX writer

        private static FbxWriter.Mesh Cube(string name, double s, double x0 = 0)
        {
            var tris = new List<float[]>();
            double[][] c = {
                new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 1.0, 1, 0 }, new[] { 0.0, 1, 0 },
                new[] { 0.0, 0, 1 }, new[] { 1.0, 0, 1 }, new[] { 1.0, 1, 1 }, new[] { 0.0, 1, 1 } };
            int[][] faces = { new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, new[] { 0, 1, 5 }, new[] { 0, 5, 4 },
                new[] { 1, 2, 6 }, new[] { 1, 6, 5 }, new[] { 2, 3, 7 }, new[] { 2, 7, 6 }, new[] { 3, 0, 4 }, new[] { 3, 4, 7 } };
            foreach (var f in faces)
            {
                var t = new float[12];
                for (int k = 0; k < 3; k++) for (int a = 0; a < 3; a++) t[3 + k * 3 + a] = (float)(c[f[k]][a] * s + (a == 0 ? x0 : 0));
                tris.Add(t);
            }
            return FbxWriter.FromTriangles(name, tris);
        }

        [Fact]
        public void Triangles_become_a_mesh_with_shared_corners()
        {
            var m = Cube("A-WALL-3D 2F", 1000);
            Assert.Equal(8, m.Vertices.Count);
            Assert.Equal(12, m.Triangles.Count);
            // a degenerate triangle is left out
            var flat = FbxWriter.FromTriangles("x", new[] { new float[] { 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2 } });
            Assert.Empty(flat.Triangles);
            Assert.Equal("Solid", FbxWriter.Clean("\u0001 "));
        }

        /// <summary>A small reader of the FBX 7.4 binary layout (the same rules as the importers: nodes, end offsets, the null record).</summary>
        private sealed class Elem { public string Id; public List<object> Props = new List<object>(); public List<Elem> Children = new List<Elem>(); }

        private static Elem ReadElem(BinaryReader r)
        {
            uint end = r.ReadUInt32(), n = r.ReadUInt32();
            r.ReadUInt32();
            int len = r.ReadByte();
            if (end == 0) return null;
            var e = new Elem { Id = Encoding.ASCII.GetString(r.ReadBytes(len)) };
            for (int i = 0; i < n; i++)
            {
                char t = (char)r.ReadByte();
                switch (t)
                {
                    case 'I': e.Props.Add(r.ReadInt32()); break;
                    case 'L': e.Props.Add(r.ReadInt64()); break;
                    case 'D': e.Props.Add(r.ReadDouble()); break;
                    case 'C': e.Props.Add(r.ReadByte() != 0); break;
                    case 'S': case 'R': { int l = r.ReadInt32(); e.Props.Add(r.ReadBytes(l)); break; }
                    case 'd': { int c = r.ReadInt32(); Assert.Equal(0, r.ReadInt32()); int b = r.ReadInt32(); Assert.Equal(c * 8, b); e.Props.Add(Enumerable.Range(0, c).Select(_ => r.ReadDouble()).ToArray()); break; }
                    case 'i': { int c = r.ReadInt32(); Assert.Equal(0, r.ReadInt32()); int b = r.ReadInt32(); Assert.Equal(c * 4, b); e.Props.Add(Enumerable.Range(0, c).Select(_ => r.ReadInt32()).ToArray()); break; }
                    default: throw new Exception("type " + t);
                }
            }
            while (r.BaseStream.Position < end)
            {
                var c = ReadElem(r);
                if (c == null) break;
                e.Children.Add(c);
            }
            Assert.Equal(end, r.BaseStream.Position);
            return e;
        }

        [Fact]
        public void Fbx_is_a_valid_74_binary_with_meshes_materials_and_connections()
        {
            var meshes = new List<FbxWriter.Mesh> { Cube("A-WALL-3D 2F", 1000), Cube("A-ROOF-3D 30", 500, 2000) };
            meshes[1].R = 0.7; meshes[1].G = 0.3; meshes[1].B = 0.2;
            var bytes = FbxWriter.Write(meshes);
            Assert.Equal("Kaydara FBX Binary  \0", Encoding.ASCII.GetString(bytes, 0, 21));
            Assert.Equal(0x1a, bytes[21]);
            Assert.Equal(7400u, BitConverter.ToUInt32(bytes, 23));
            var top = new List<Elem>();
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                r.BaseStream.Position = 27;
                for (Elem e; (e = ReadElem(r)) != null;) top.Add(e);
            }
            Assert.Equal(new[] { "FBXHeaderExtension", "FileId", "CreationTime", "Creator", "GlobalSettings", "Definitions", "Objects", "Connections" }, top.Select(e => e.Id));
            var gs = top.Single(e => e.Id == "GlobalSettings").Children.Single(c => c.Id == "Properties70").Children;
            object Prop(string name) => gs.Single(p => Encoding.UTF8.GetString((byte[])p.Props[0]) == name).Props[4];
            Assert.Equal(2, Prop("UpAxis"));
            Assert.Equal(0.1, Prop("UnitScaleFactor"));
            var objects = top.Single(e => e.Id == "Objects").Children;
            Assert.Equal(2, objects.Count(o => o.Id == "Geometry"));
            Assert.Equal(2, objects.Count(o => o.Id == "Model"));
            Assert.Equal(2, objects.Count(o => o.Id == "Material"));
            var g = objects.First(o => o.Id == "Geometry");
            Assert.Equal("A-WALL-3D 2F\0\u0001Geometry", Encoding.UTF8.GetString((byte[])g.Props[1]));
            var verts = (double[])g.Children.Single(c => c.Id == "Vertices").Props[0];
            var idx = (int[])g.Children.Single(c => c.Id == "PolygonVertexIndex").Props[0];
            Assert.Equal(24, verts.Length);
            Assert.Equal(36, idx.Length);
            Assert.True(idx.Where((v, i) => i % 3 == 2).All(v => v < 0), "the last index of each triangle is negative");
            Assert.Equal(1000, verts.Max());
            var conns = top.Single(e => e.Id == "Connections").Children;
            Assert.Equal(6, conns.Count);
            Assert.All(conns, c => Assert.Equal("OO", Encoding.UTF8.GetString((byte[])c.Props[0])));
            // footer: the id, then the version and the magic at the very end
            Assert.Equal(new byte[] { 0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b }, bytes.Skip(bytes.Length - 16).ToArray());
            Assert.Equal(7400u, BitConverter.ToUInt32(bytes, bytes.Length - 16 - 120 - 4));
            Assert.Equal(0, (bytes.Length - 16 - 120 - 4) % 16);
        }

        // ------------------------------------------------------------ plans, read-only, the AutoCAD side

        [Fact]
        public void Dxf_dwg_on_every_plan_fbx_on_3d_and_full()
        {
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("export_drawing"));
            Assert.True(PlanLogic.CallAllowed("2d", "export_drawing", new Dictionary<string, object> { ["format"] = "dwg" }));
            Assert.True(PlanLogic.CallAllowed("3d", "export_drawing", new Dictionary<string, object> { ["format"] = "dxf" }));
            Assert.False(PlanLogic.CallAllowed("2d", "export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }));
            Assert.False(PlanLogic.CallAllowed("2d", "export_drawing", new Dictionary<string, object> { ["format"] = " FBX" }));
            Assert.True(PlanLogic.CallAllowed("3d", "export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }));
            Assert.True(PlanLogic.CallAllowed("2d3d", "export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }));
            Assert.False(PlanLogic.CallAllowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), "export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }));
            Assert.Equal("format", PlanLogic.BlockedParam("2d", "export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }));
            Assert.False(MacroLogic.ShouldRecord("export_drawing", false));
        }

        [Fact]
        public void The_open_drawing_is_never_saved_over_or_renamed()
        {
            var t = Tools();
            Assert.Contains("copy = db.Wblock(ids, Point3d.Origin);", t);
            Assert.Contains("else copy = db.Wblock();", t);
            Assert.Contains("copy.SaveAs(temp, version);", t);
            Assert.Contains("copy.DxfOut(temp, H2Numbers.DxfPrecision, version);", t);
            Assert.DoesNotContain("db.SaveAs(", t);
            Assert.DoesNotContain("db.DxfOut(", t);
            // the card before anything is written; FBX moved copies never kept
            int card = t.IndexOf("RequireOverwriteCard(ctx, p, tool, file", StringComparison.Ordinal);
            Assert.True(card > 0 && card < t.IndexOf("copy.SaveAs", StringComparison.Ordinal) && card < t.IndexOf("ExportFbx(ctx, p, file, tool)", StringComparison.Ordinal));
            Assert.Contains("tr.Abort(); // the moved copies are never kept", t);
            Assert.Contains("TraceLogic.PathRefusal(file, openPath", t);
            Assert.Contains("d.Register(new ToolDef(\"export_drawing\", false, ExportDrawing, noTransaction: true));", t);
        }
    }
}
