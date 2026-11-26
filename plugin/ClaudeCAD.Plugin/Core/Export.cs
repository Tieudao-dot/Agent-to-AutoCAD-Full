using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H2: the numbers and words of "xuất bản vẽ sang định dạng khác" (export_drawing), one place each
    /// (docs/ban-giao/nang-cap-h2.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class H2Numbers
    {
        /// <summary>The DXF / DWG version used when the user names none: AutoCAD 2013 (read by most CAD programs and SketchUp).</summary>
        public const string DefaultVersion = "2013";
        /// <summary>DXF decimal places (AutoCAD's DXFOUT default 16).</summary>
        public const int DxfPrecision = 16;
        /// <summary>Most 3D solids in one FBX (the same limit as export_stl).</summary>
        public const int MaxFbxSolids = 2000;
    }

    /// <summary>Nâng cấp đợt H2, AutoCAD-free part of export_drawing: formats, versions, file names and checks.</summary>
    public static class ExportLogic
    {
        /// <summary>
        /// Sửa KTH-23: an STL triangle (float[12], meshed near the origin after a move by +shift) back at its real place, in
        /// double: the normal as it is, each corner minus shift.
        /// </summary>
        public static double[] Unshift(float[] t, double[] shift)
        {
            var r = t.Select(x => (double)x).ToArray();
            for (int k = 0; k < 3; k++)
                for (int a = 0; a < 3; a++) r[3 + k * 3 + a] = t[3 + k * 3 + a] - shift[a];
            return r;
        }

        public static readonly string[] Formats = { "dxf", "dwg", "fbx" };

        /// <summary>The versions offered, newest first, with the DwgVersion name of AutoCAD's .NET API (AC1032 = 2018 format, read by 2018 – 2027).</summary>
        public static readonly IReadOnlyList<(string year, string acad, string covers)> Versions = new List<(string, string, string)>
        {
            ("2018", "AC1032", "AutoCAD 2018 – 2027"),
            ("2013", "AC1027", "AutoCAD 2013 – 2017"),
            ("2010", "AC1024", "AutoCAD 2010 – 2012"),
            ("2007", "AC1021", "AutoCAD 2007 – 2009"),
            ("2004", "AC1018", "AutoCAD 2004 – 2006"),
            ("2000", "AC1015", "AutoCAD 2000 – 2002"),
        };

        public static string FormatOf(string f, string tool = "export_drawing")
        {
            var s = (f ?? "").Trim().ToLowerInvariant().TrimStart('.');
            if (!Formats.Contains(s)) throw ToolException.Invalid($"{tool}: format must be one of {string.Join(", ", Formats)} (got '{f}').");
            return s;
        }

        /// <summary>The (year, DwgVersion name) of a version given as "2013", "AutoCAD 2013", "R2013", "2014" (→ the format that covers it)…</summary>
        public static (string year, string acad) VersionOf(string v, string tool = "export_drawing")
        {
            var s = new string((v ?? H2Numbers.DefaultVersion).Where(char.IsDigit).ToArray());
            if (s.Length == 0) s = H2Numbers.DefaultVersion;
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
                throw ToolException.Invalid($"{tool}: version must be a year like 2013 (got '{v}').");
            if (year >= 2018 && year <= 2099) return ("2018", "AC1032");
            foreach (var x in Versions)
            {
                int from = int.Parse(x.year, CultureInfo.InvariantCulture);
                if (year >= from) return (x.year, x.acad);
            }
            throw ToolException.Invalid($"{tool}: AutoCAD cannot save older than version 2000 (got '{v}'). Offer 2000 at the oldest.");
        }

        public static string Covers(string year) => Versions.First(x => x.year == year).covers;

        /// <summary>Default name: "&lt;drawing&gt;-acad2013.dwg" / "&lt;drawing&gt;-acad2013.dxf" / "&lt;drawing&gt;.fbx" (a part: "-mot-phan").</summary>
        public static string DefaultName(string stem, string format, string year, bool part)
        {
            var s = string.IsNullOrWhiteSpace(stem) ? "ban-ve" : stem.Trim();
            var tail = part ? "-mot-phan" : "";
            return format == "fbx" ? s + tail + ".fbx" : s + "-acad" + year + tail + "." + format;
        }

        /// <summary>
        /// The full path of the output (relative = next to the drawing). Refused: another extension, no name, a folder, and the
        /// open drawing itself (the drawing open stays the original file).
        /// </summary>
        public static string OutputPath(string output, string format, string drawingDir, string drawingStem, string year, bool part, string openDrawingPath, string tool = "export_drawing")
        {
            var o = string.IsNullOrWhiteSpace(output) ? DefaultName(drawingStem, format, year, part) : output.Trim().Trim('"');
            if (!o.EndsWith("." + format, StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid($"{tool}: 'output' must end with .{format} (format {format}).");
            string full;
            try { full = Path.GetFullPath(Path.IsPathRooted(o) ? o : Path.Combine(drawingDir ?? "", o)); }
            catch (Exception) { throw ToolException.Invalid($"{tool}: '{o}' is not a valid file path."); }
            var name = Path.GetFileName(full);
            var bad = string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(full)) ? "the file has no name" : Util2DLogic.BadFileName(name);
            if (bad != null) throw ToolException.Invalid($"{tool}: '{o}': {bad}. Give another file name ending in .{format}.");
            if (!string.IsNullOrEmpty(openDrawingPath))
            {
                string open;
                try { open = Path.GetFullPath(openDrawingPath); } catch (Exception) { open = openDrawingPath; }
                if (string.Equals(open, full, StringComparison.OrdinalIgnoreCase))
                    throw ToolException.Invalid($"{tool}: '{full}' is the drawing that is open. The export is always a new file: give another name (the default adds the version, e.g. -acad2013).");
            }
            return full;
        }

        /// <summary>
        /// Test & sửa H2: the G5 network rule's refusal reworded for a file to WRITE: dragging a file into the chat does not
        /// help an export (also in the device-path message), so the user picks a folder of this computer or of the drawing.
        /// </summary>
        public static string WriteRefusal(string refusal) => (refusal ?? "")
            .Replace(TraceLogic.Advice, "Ask the user for a folder on this computer (e.g. Desktop) or the folder of the drawing.")
            .Replace("Ask the user for the normal path of the file (e.g. C:\\…), or to drag the file into the chat.", "Ask the user for a normal folder path (e.g. C:\\…) on this computer or the folder of the drawing.")
            .Replace(" are opened", " are used");

        /// <summary>What the result tells Claude about who can open the file.</summary>
        public static string Note(string format, string year, bool part)
        {
            string p = part ? " Only the chosen objects were exported (model space; layouts are not included)." : "";
            switch (format)
            {
                case "dxf": return $"DXF version {year} ({Covers(year)} and most CAD programs).{p} The open drawing is unchanged (same file, same version).";
                case "dwg": return $"A copy saved as DWG {year}, opened by {Covers(year)} and newer.{p} The open drawing is unchanged (same file, same version).";
                default: return "FBX 7.4 (binary) of the 3D solids as meshes, in millimetres, Z up (3ds Max, Blender…). Only 3D solids of model space are exported (not those inside blocks); lines, texts and 2D objects are not. Materials: the colour of each solid.";
            }
        }
    }

    /// <summary>
    /// Nâng cấp đợt H2: a small binary FBX 7.4 writer for triangle meshes (no FBX SDK, no AutoCAD command whose questions
    /// could change between versions). One Model + Geometry + Material per mesh; units mm (UnitScaleFactor 0.1), Z up.
    /// </summary>
    public static class FbxWriter
    {
        public sealed class Mesh
        {
            public string Name;
            /// <summary>Vertices x, y, z (mm).</summary>
            public List<double[]> Vertices = new List<double[]>();
            /// <summary>Triangles as three vertex indices.</summary>
            public List<int[]> Triangles = new List<int[]>();
            /// <summary>Diffuse colour 0–1.</summary>
            public double R = 0.8, G = 0.8, B = 0.8;
        }

        /// <summary>A mesh from STL triangles (float[12]: normal, then three corners), corners merged when equal.</summary>
        public static Mesh FromTriangles(string name, IEnumerable<float[]> tris, double scale = 1) =>
            FromTriangles(name, tris.Select(t => t?.Select(x => (double)x).ToArray()), scale);

        /// <summary>
        /// Sửa KTH-23: the same from triangles in double (the corners moved back to their real place after the STL near the
        /// origin): a point at 500 000 000 keeps its 0.0001 mm, float would round it to 32 mm.
        /// </summary>
        public static Mesh FromTriangles(string name, IEnumerable<double[]> tris, double scale = 1)
        {
            var m = new Mesh { Name = name };
            var index = new Dictionary<(long, long, long), int>();
            int V(double x, double y, double z)
            {
                var k = ((long)Math.Round(x * 1e4), (long)Math.Round(y * 1e4), (long)Math.Round(z * 1e4));
                if (!index.TryGetValue(k, out var i)) { i = m.Vertices.Count; index[k] = i; m.Vertices.Add(new[] { x, y, z }); }
                return i;
            }
            foreach (var t in tris)
            {
                if (t == null || t.Length < 12) continue;
                var a = V(t[3] * scale, t[4] * scale, t[5] * scale);
                var b = V(t[6] * scale, t[7] * scale, t[8] * scale);
                var c = V(t[9] * scale, t[10] * scale, t[11] * scale);
                if (a != b && b != c && a != c) m.Triangles.Add(new[] { a, b, c });
            }
            return m;
        }

        // ------------------------------------------------------------ nodes

        private sealed class Node
        {
            public string Name;
            public readonly List<(char type, byte[] data)> Props = new List<(char, byte[])>();
            public readonly List<Node> Children = new List<Node>();
            public Node(string name) { Name = name; }
            public Node I(int v) { Props.Add(('I', BitConverter.GetBytes(v))); return this; }
            public Node L(long v) { Props.Add(('L', BitConverter.GetBytes(v))); return this; }
            public Node D(double v) { Props.Add(('D', BitConverter.GetBytes(v))); return this; }
            public Node C(bool v) { Props.Add(('C', new[] { (byte)(v ? 1 : 0) })); return this; }
            public Node S(string v) { var b = Encoding.UTF8.GetBytes(v ?? ""); Props.Add(('S', Len(b))); return this; }
            public Node R(byte[] v) { Props.Add(('R', Len(v))); return this; }
            public Node Ad(IReadOnlyList<double> v) { var b = new byte[v.Count * 8]; for (int i = 0; i < v.Count; i++) Buffer.BlockCopy(BitConverter.GetBytes(v[i]), 0, b, i * 8, 8); Props.Add(('d', Arr(v.Count, b))); return this; }
            public Node Ai(IReadOnlyList<int> v) { var b = new byte[v.Count * 4]; for (int i = 0; i < v.Count; i++) Buffer.BlockCopy(BitConverter.GetBytes(v[i]), 0, b, i * 4, 4); Props.Add(('i', Arr(v.Count, b))); return this; }
            public Node Add(Node c) { Children.Add(c); return this; }
            public Node Child(string name) { var c = new Node(name); Children.Add(c); return c; }

            private static byte[] Len(byte[] b) { var r = new byte[4 + b.Length]; Buffer.BlockCopy(BitConverter.GetBytes(b.Length), 0, r, 0, 4); Buffer.BlockCopy(b, 0, r, 4, b.Length); return r; }
            private static byte[] Arr(int count, byte[] b)
            {
                var r = new byte[12 + b.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(count), 0, r, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(0), 0, r, 4, 4); // not compressed
                Buffer.BlockCopy(BitConverter.GetBytes(b.Length), 0, r, 8, 4);
                Buffer.BlockCopy(b, 0, r, 12, b.Length);
                return r;
            }
        }

        private const int SentinelLength = 13; // FBX 7.4: 3 × uint32 + 1 byte, all zero

        private static long Size(Node n, bool isLast)
        {
            long s = 12 + 1 + Encoding.ASCII.GetByteCount(n.Name) + n.Props.Sum(p => 1 + p.data.Length);
            if (n.Children.Count > 0) s += n.Children.Select((c, i) => Size(c, i == n.Children.Count - 1)).Sum() + SentinelLength;
            else if (n.Props.Count == 0 && !isLast) s += SentinelLength;
            return s;
        }

        private static void Write(BinaryWriter w, Node n, bool isLast)
        {
            long start = w.BaseStream.Position;
            long end = start + Size(n, isLast);
            if (end > uint.MaxValue) throw new InvalidOperationException("FBX larger than 4 GB");
            w.Write((uint)end);
            w.Write((uint)n.Props.Count);
            w.Write((uint)n.Props.Sum(p => 1 + p.data.Length));
            var name = Encoding.ASCII.GetBytes(n.Name);
            w.Write((byte)name.Length);
            w.Write(name);
            foreach (var p in n.Props) { w.Write((byte)p.type); w.Write(p.data); }
            if (n.Children.Count > 0)
            {
                for (int i = 0; i < n.Children.Count; i++) Write(w, n.Children[i], i == n.Children.Count - 1);
                w.Write(new byte[SentinelLength]);
            }
            else if (n.Props.Count == 0 && !isLast) w.Write(new byte[SentinelLength]);
            if (w.BaseStream.Position != end) throw new InvalidOperationException("FBX node size mismatch: " + n.Name);
        }

        private static Node P(string name, string type, string label, string flags) => new Node("P").S(name).S(type).S(label).S(flags);

        private static readonly byte[] FileId = { 0x28, 0xb3, 0x2a, 0xeb, 0xb6, 0x24, 0xcc, 0xc2, 0xbf, 0xc8, 0xb0, 0x2a, 0xa9, 0x2b, 0xfc, 0xf1 };
        private static readonly byte[] FootId = { 0xfa, 0xbc, 0xab, 0x09, 0xd0, 0xc8, 0xd4, 0x66, 0xb1, 0x76, 0xfb, 0x83, 0x1c, 0xf7, 0x26, 0x7e };
        private static readonly byte[] FootMagic = { 0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b };

        /// <summary>The FBX file (FBX 7.4 binary) of the meshes.</summary>
        public static byte[] Write(IList<Mesh> meshes, string creator = "ClaudeCAD")
        {
            var top = new List<Node>();
            var header = new Node("FBXHeaderExtension");
            header.Child("FBXHeaderVersion").I(1003);
            header.Child("FBXVersion").I(7400);
            header.Child("Creator").S(creator);
            top.Add(header);
            top.Add(new Node("FileId").R(FileId));
            top.Add(new Node("CreationTime").S("1970-01-01 10:00:00:000"));
            top.Add(new Node("Creator").S(creator));

            var gs = new Node("GlobalSettings");
            gs.Child("Version").I(1000);
            var gp = gs.Child("Properties70");
            gp.Add(P("UpAxis", "int", "Integer", "").I(2));          // AutoCAD: Z up
            gp.Add(P("UpAxisSign", "int", "Integer", "").I(1));
            gp.Add(P("FrontAxis", "int", "Integer", "").I(1));       // Y
            gp.Add(P("FrontAxisSign", "int", "Integer", "").I(-1));
            gp.Add(P("CoordAxis", "int", "Integer", "").I(0));       // X
            gp.Add(P("CoordAxisSign", "int", "Integer", "").I(1));
            gp.Add(P("OriginalUpAxis", "int", "Integer", "").I(2));
            gp.Add(P("OriginalUpAxisSign", "int", "Integer", "").I(1));
            gp.Add(P("UnitScaleFactor", "double", "Number", "").D(0.1));   // 1 unit = 1 mm (FBX base: cm)
            gp.Add(P("OriginalUnitScaleFactor", "double", "Number", "").D(0.1));
            top.Add(gs);

            var defs = new Node("Definitions");
            defs.Child("Version").I(100);
            defs.Child("Count").I(1 + meshes.Count * 3);
            defs.Child("ObjectType").S("GlobalSettings").Add(new Node("Count").I(1));
            defs.Child("ObjectType").S("Model").Add(new Node("Count").I(meshes.Count));
            defs.Child("ObjectType").S("Geometry").Add(new Node("Count").I(meshes.Count));
            defs.Child("ObjectType").S("Material").Add(new Node("Count").I(meshes.Count));
            top.Add(defs);

            var objects = new Node("Objects");
            var conns = new Node("Connections");
            long id = 1000000;
            foreach (var m in meshes)
            {
                long gid = ++id, mid = ++id, matId = ++id;
                string name = Clean(m.Name);
                var g = objects.Child("Geometry").L(gid).S(name + "\0\u0001Geometry").S("Mesh");
                g.Child("Properties70");
                g.Child("GeometryVersion").I(124);
                g.Child("Vertices").Ad(m.Vertices.SelectMany(v => v).ToList());
                var idx = new List<int>(m.Triangles.Count * 3);
                foreach (var t in m.Triangles) { idx.Add(t[0]); idx.Add(t[1]); idx.Add(~t[2]); } // the last of a polygon: −(i + 1)
                g.Child("PolygonVertexIndex").Ai(idx);
                // Test & sửa H2: flat normals, one per corner (ByPolygonVertex). Without them each program makes its own, and the
                // corners merged by FromTriangles would come out smoothed (a box shaded like a ball in 3ds Max)
                var ln = g.Child("LayerElementNormal").I(0);
                ln.Child("Version").I(101);
                ln.Child("Name").S("");
                ln.Child("MappingInformationType").S("ByPolygonVertex");
                ln.Child("ReferenceInformationType").S("Direct");
                ln.Child("Normals").Ad(FaceNormals(m));
                var lm = g.Child("LayerElementMaterial").I(0);
                lm.Child("Version").I(101);
                lm.Child("Name").S("");
                lm.Child("MappingInformationType").S("AllSame");
                lm.Child("ReferenceInformationType").S("IndexToDirect");
                lm.Child("Materials").Ai(new[] { 0 });
                var layer = g.Child("Layer").I(0);
                layer.Child("Version").I(100);
                var lne = layer.Child("LayerElement");
                lne.Child("Type").S("LayerElementNormal");
                lne.Child("TypedIndex").I(0);
                var le = layer.Child("LayerElement");
                le.Child("Type").S("LayerElementMaterial");
                le.Child("TypedIndex").I(0);

                var model = objects.Child("Model").L(mid).S(name + "\0\u0001Model").S("Mesh");
                model.Child("Version").I(232);
                var mp = model.Child("Properties70");
                mp.Add(P("Lcl Translation", "Lcl Translation", "", "A").D(0).D(0).D(0));
                model.Child("Shading").C(true);
                model.Child("Culling").S("CullingOff");

                var mat = objects.Child("Material").L(matId).S(name + "\0\u0001Material").S("");
                mat.Child("Version").I(102);
                mat.Child("ShadingModel").S("phong");
                mat.Child("MultiLayer").I(0);
                var matp = mat.Child("Properties70");
                matp.Add(P("DiffuseColor", "Color", "", "A").D(m.R).D(m.G).D(m.B));
                matp.Add(P("Diffuse", "Vector3D", "Vector", "").D(m.R).D(m.G).D(m.B));

                conns.Child("C").S("OO").L(mid).L(0);
                conns.Child("C").S("OO").L(gid).L(mid);
                conns.Child("C").S("OO").L(matId).L(mid);
            }
            top.Add(objects);
            top.Add(conns);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("Kaydara FBX Binary  "));
                w.Write(new byte[] { 0x00, 0x1a, 0x00 });
                w.Write((uint)7400);
                for (int i = 0; i < top.Count; i++) Write(w, top[i], false);
                w.Write(new byte[SentinelLength]); // end of the top level
                w.Write(FootId);
                w.Write(new byte[4]);
                long ofs = ms.Position;
                long pad = ((ofs + 15) & ~15L) - ofs;
                if (pad == 0) pad = 16;
                w.Write(new byte[pad]);
                w.Write((uint)7400);
                w.Write(new byte[120]);
                w.Write(FootMagic);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Test & sửa H2: the normal of each triangle, repeated for its three corners (x, y, z each); a flat one is +Z.</summary>
        public static List<double> FaceNormals(Mesh m)
        {
            var n = new List<double>(m.Triangles.Count * 9);
            foreach (var t in m.Triangles)
            {
                double[] a = m.Vertices[t[0]], b = m.Vertices[t[1]], c = m.Vertices[t[2]];
                double ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx, l = Math.Sqrt(x * x + y * y + z * z);
                if (l < 1e-12) { x = 0; y = 0; z = 1; } else { x /= l; y /= l; z /= l; }
                for (int k = 0; k < 3; k++) { n.Add(x); n.Add(y); n.Add(z); }
            }
            return n;
        }

        /// <summary>A name FBX keeps (no control characters, at most 100 characters).</summary>
        public static string Clean(string name)
        {
            var s = new string((name ?? "").Where(ch => ch >= 32).ToArray()).Trim();
            if (s.Length == 0) s = "Solid";
            return s.Length > 100 ? s.Substring(0, 100) : s;
        }
    }
}
