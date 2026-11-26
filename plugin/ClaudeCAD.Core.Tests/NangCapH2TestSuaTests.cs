using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt H2 (docs/ban-giao/nang-cap-h2-test.md): export_drawing after the review.</summary>
    public class NangCapH2TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static FbxWriter.Mesh Cube(double s)
        {
            var c = new[] { new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 1.0, 1, 0 }, new[] { 0.0, 1, 0 }, new[] { 0.0, 0, 1 }, new[] { 1.0, 0, 1 }, new[] { 1.0, 1, 1 }, new[] { 0.0, 1, 1 } };
            var faces = new[] { new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, new[] { 0, 1, 5 }, new[] { 0, 5, 4 }, new[] { 1, 2, 6 }, new[] { 1, 6, 5 }, new[] { 2, 3, 7 }, new[] { 2, 7, 6 }, new[] { 3, 0, 4 }, new[] { 3, 4, 7 } };
            var tris = faces.Select(f => { var t = new float[12]; for (int k = 0; k < 3; k++) for (int a = 0; a < 3; a++) t[3 + k * 3 + a] = (float)(c[f[k]][a] * s); return t; });
            return FbxWriter.FromTriangles("box", tris);
        }

        private sealed class Elem { public string Id; public List<object> Props = new List<object>(); public List<Elem> Children = new List<Elem>(); }

        private static Elem Read(BinaryReader r)
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
                    case 'S': case 'R': { int l = r.ReadInt32(); e.Props.Add(Encoding.UTF8.GetString(r.ReadBytes(l))); break; }
                    case 'd': { int c = r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); e.Props.Add(Enumerable.Range(0, c).Select(_ => r.ReadDouble()).ToArray()); break; }
                    case 'i': { int c = r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); e.Props.Add(Enumerable.Range(0, c).Select(_ => r.ReadInt32()).ToArray()); break; }
                    default: throw new Exception("type " + t);
                }
            }
            while (r.BaseStream.Position < end) { var c = Read(r); if (c == null) break; e.Children.Add(c); }
            Assert.Equal(end, r.BaseStream.Position);
            return e;
        }

        [Fact]
        public void Each_triangle_corner_has_the_flat_normal_of_its_face_pointing_out()
        {
            var m = Cube(1000);
            var n = FbxWriter.FaceNormals(m);
            Assert.Equal(m.Triangles.Count * 9, n.Count);
            for (int t = 0; t < m.Triangles.Count; t++)
            {
                double x = n[t * 9], y = n[t * 9 + 1], z = n[t * 9 + 2];
                Assert.Equal(1, Math.Sqrt(x * x + y * y + z * z), 6);
                Assert.Equal(1, Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))), 6); // a box: along an axis
                // outward: from the centre of the box to the triangle
                var c = m.Triangles[t].Select(i => m.Vertices[i]).ToList();
                double cx = c.Average(v => v[0]) - 500, cy = c.Average(v => v[1]) - 500, cz = c.Average(v => v[2]) - 500;
                Assert.True(cx * x + cy * y + cz * z > 0);
            }
        }

        [Fact]
        public void The_fbx_has_a_normal_layer_byPolygonVertex_listed_in_the_layer()
        {
            var bytes = FbxWriter.Write(new List<FbxWriter.Mesh> { Cube(1000) });
            var top = new List<Elem>();
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                r.BaseStream.Position = 27;
                for (Elem e; (e = Read(r)) != null;) top.Add(e);
            }
            var g = top.Single(e => e.Id == "Objects").Children.Single(o => o.Id == "Geometry");
            var ln = g.Children.Single(c => c.Id == "LayerElementNormal");
            Assert.Equal("ByPolygonVertex", ln.Children.Single(c => c.Id == "MappingInformationType").Props[0]);
            Assert.Equal("Direct", ln.Children.Single(c => c.Id == "ReferenceInformationType").Props[0]);
            var idx = (int[])g.Children.Single(c => c.Id == "PolygonVertexIndex").Props[0];
            Assert.Equal(idx.Length * 3, ((double[])ln.Children.Single(c => c.Id == "Normals").Props[0]).Length);
            var layerTypes = g.Children.Single(c => c.Id == "Layer").Children.Where(c => c.Id == "LayerElement")
                .Select(le => (string)le.Children.Single(c => c.Id == "Type").Props[0]).ToList();
            Assert.Equal(new[] { "LayerElementNormal", "LayerElementMaterial" }, layerTypes);
        }

        [Fact]
        public void Fbx_on_a_plan_without_3d_says_fbx_not_block_attributes_also_in_a_macro()
        {
            var msg = PlanLogic.BlockedParamMessage("export_drawing", "format", "2d", null);
            Assert.Contains("fbx", msg);
            Assert.Contains("dwg or dxf", msg);
            Assert.DoesNotContain("attribute", msg);
            var calls = new List<(string, IDictionary<string, object>)> { ("export_drawing", new Dictionary<string, object> { ["format"] = "fbx" }) };
            Assert.Equal(new[] { "export_drawing (FBX, phần 3D)" }, PlanLogic.BlockedCalls("2d", calls));
            Assert.Empty(PlanLogic.BlockedCalls("2d", new List<(string, IDictionary<string, object>)> { ("export_drawing", new Dictionary<string, object> { ["format"] = "dwg" }) }));
            // array_rect levels still say "số tầng"
            Assert.Equal(new[] { "array_rect (số tầng)" }, PlanLogic.BlockedCalls("2d", new List<(string, IDictionary<string, object>)> { ("array_rect", new Dictionary<string, object> { ["levels"] = 3 }) }));
        }

        [Fact]
        public void A_refused_network_folder_for_an_export_never_advises_dragging_into_the_chat()
        {
            var net = ExportLogic.WriteRefusal(TraceLogic.PathRefusal(@"\\other\share\a.dwg", @"C:\du an\nha.dwg", false, false, "export_drawing"));
            Assert.DoesNotContain("drag", net);
            Assert.Contains("folder on this computer", net);
            var dev = ExportLogic.WriteRefusal(TraceLogic.PathRefusal(@"\\?\GLOBALROOT\Device\x\a.dwg", @"C:\du an\nha.dwg", false, false, "export_drawing"));
            Assert.DoesNotContain("drag", dev);
            Assert.Contains("folder", dev);
        }

        [Fact]
        public void The_tool_refuses_handles_with_layers_and_leaves_empty_meshes_out()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExportTools.cs");
            Assert.Contains("give handles or layers, not both", t);
            Assert.Contains("if (mesh.Triangles.Count == 0) { empty.Add(handles[i]); continue; }", t);
            Assert.Contains("res[\"left_out\"]", t);
            Assert.Contains("ExportLogic.WriteRefusal(refusal)", t);
            Assert.Contains("not those inside blocks", ExportLogic.Note("fbx", "", false));
        }
    }
}
