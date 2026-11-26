using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H3 (docs/ban-giao/nang-cap-h3.md): the numbers of compare_drawings, in one place (table "Con số chờ chủ dự
    /// án duyệt" of the handover).
    /// </summary>
    public static class H3Numbers
    {
        /// <summary>Two positions / lengths within this (mm) are the same.</summary>
        public const double ToleranceMm = 1.0;
        /// <summary>Two 3D solids differ in volume when it changes by more than this share.</summary>
        public const double VolumeShare = 0.001;
        /// <summary>Objects read in each drawing (model space); more are left out and the result says so.</summary>
        public const int MaxObjects = 20000;
        /// <summary>Handles of the other file found in the open drawing (same kind): from this share up, both have one origin.</summary>
        public const double SameOriginShare = 0.5;
        /// <summary>Rows of differences in the result (the Excel file has them all).</summary>
        public const int MaxRows = 100;
        /// <summary>More clouds than this need the card; more than MaxClouds are not drawn at all (the list / Excel only).</summary>
        public const int CardAboveClouds = 50;
        public const int MaxClouds = 500;
        /// <summary>Sửa KTH-21: more differences than this are not ringed at all (the clouds are not even worked out).</summary>
        public const int MaxRowsForClouds = 5000;
        /// <summary>A cloud goes this share of the longest side of what it rings outside it, at least CloudMinMarginMm.</summary>
        public const double CloudMarginShare = 0.05;
        public const double CloudMinMarginMm = 100;
        /// <summary>Layer of the marks (deleting it removes them all) and the colours (ACI): added green, removed red, changed yellow.</summary>
        public const string Layer = "X-SO-SANH";
        public const short ColorAdded = 3, ColorRemoved = 1, ColorChanged = 2;
    }

    /// <summary>One object of a drawing for compare_drawings (drawing units of the open drawing).</summary>
    public sealed class CmpItem
    {
        public string Handle;
        /// <summary>"line", "arc", "circle", "pline", "text", "mtext", "insert", "dim", "hatch", "solid", or the class name.</summary>
        public string Kind;
        public string Layer;
        /// <summary>The geometry compared within the tolerance (points, lengths).</summary>
        public double[] Nums;
        /// <summary>The same shape read the other way (a line / open polyline end to start), or null.</summary>
        public double[] Alt;
        /// <summary>Parts compared exactly (angles, bulges, scales), already written rounded.</summary>
        public string Exact = "";
        /// <summary>Properties compared as text: color, linetype, lineweight, text, block, attributes, pattern…</summary>
        public Dictionary<string, string> Props = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>3D solid: its volume (compared with H3Numbers.VolumeShare), else null.</summary>
        public double? Volume;
        /// <summary>Test & sửa H3: sizes compared within the tolerance (dimension value, polyline width), not as text.</summary>
        public Dictionary<string, double> Sizes = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>Test & sửa H3: areas compared like volumes (H3Numbers.VolumeShare), not as text.</summary>
        public Dictionary<string, double> Areas = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>
        /// Sửa KTH-17: angles (radians) compared within the tolerance where it shows: |difference| × Reach ≤ tolerance (a text
        /// turned 0.0001 rad moves its far end 0.25 mm on 2.5 m), not as exact text.
        /// </summary>
        public Dictionary<string, double> Angles = new Dictionary<string, double>(StringComparer.Ordinal);
        public double Reach = 1;
        /// <summary>Sửa KTH-16: the point a changed shape is found by (its first point), when not the first three numbers.</summary>
        public double[] AnchorAt, AltAnchorAt;
        public double[] Min, Max;
    }

    public sealed class DiffRow
    {
        /// <summary>"added" (only in the open drawing), "removed" (only in the other file), "changed".</summary>
        public string Change;
        public string Kind, Layer, Handle, OtherHandle;
        public List<string> What = new List<string>();
        public double[] Min, Max;
        /// <summary>Sửa KTH-22: a changed object's box in the old drawing too (its cloud rings both places).</summary>
        public double[] OldMin, OldMax;
    }

    public static class DrawingCompareLogic
    {
        public const string Tool = "compare_drawings";

        /// <summary>"handle" / "geometry" for the asked match ("auto" = handle when both drawings come from one file).</summary>
        public static string MatchMode(string asked, IList<CmpItem> other, IList<CmpItem> open)
        {
            var a = (asked ?? "auto").Trim().ToLowerInvariant();
            if (a == "handle" || a == "geometry") return a;
            if (a != "auto") throw ToolException.Invalid($"{Tool}: match must be auto, handle or geometry.");
            return SameOriginShare(other, open) >= H3Numbers.SameOriginShare ? "handle" : "geometry";
        }

        /// <summary>Share of the objects of the other file whose handle is in the open drawing with the same kind.</summary>
        public static double SameOriginShare(IList<CmpItem> other, IList<CmpItem> open)
        {
            if (other == null || other.Count == 0 || open == null || open.Count == 0) return 0;
            var kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in open) if (it.Handle != null) kinds[it.Handle] = it.Kind;
            int same = other.Count(it => it.Handle != null && kinds.TryGetValue(it.Handle, out var k) && k == it.Kind);
            return same / (double)other.Count;
        }

        /// <summary>
        /// Test & sửa H3: the match asked and its differences. In auto, many shared handles are not enough (two drawings started
        /// from one template, or numbered from the same start, share handles without being one file): handle is kept only when
        /// it keeps at least as many objects unchanged as geometry does (a revision keeps most objects as they were under their
        /// handle; two unrelated drawings sharing handles keep none).
        /// </summary>
        public static List<DiffRow> Run(IList<CmpItem> other, IList<CmpItem> open, string asked, double tol, bool otherIsNew, out string mode)
        {
            mode = MatchMode(asked, other, open);
            var rows = Compare(other, open, mode, tol, otherIsNew);
            bool auto = string.IsNullOrWhiteSpace(asked) || asked.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
            if (auto && mode == "handle")
            {
                var geo = Compare(other, open, "geometry", tol, otherIsNew);
                int oldCount = (otherIsNew ? open : other).Count;
                if (Unchanged(geo, oldCount) > Unchanged(rows, oldCount)) { mode = "geometry"; rows = geo; }
            }
            return rows;
        }

        /// <summary>Objects of the old drawing found unchanged: the old ones neither removed nor changed.</summary>
        private static int Unchanged(List<DiffRow> rows, int oldCount)
        {
            var c = Counts(rows);
            return oldCount - c["removed"] - c["changed"];
        }

        /// <summary>
        /// Test & sửa H3: the factor from the other file's units to the open drawing's (INSUNITS codes). When either drawing
        /// has no unit (0) or one not known, nothing is converted and the note says so (a unitless old file in metres against
        /// a new one set to metres would else be 1000 times off).
        /// </summary>
        public static double UnitFactor(int openIns, int otherIns, out string note)
        {
            note = null;
            if (openIns == otherIns) return 1;
            bool known(int u) => u == 1 || u == 2 || u == 4 || u == 5 || u == 6 || u == 14;
            double openMm = FloorPlanLogic.MmPerUnit(openIns, out var openName), otherMm = FloorPlanLogic.MmPerUnit(otherIns, out var otherName);
            if (!known(openIns) || !known(otherIns))
            {
                note = $"The two drawings have different unit settings (open drawing: {openName}, other file: {otherName}); one has no unit, so nothing was converted. If its sizes look 10 / 1000 times off, ask the user which unit it was drawn in.";
                return 1;
            }
            double f = otherMm / openMm;
            note = $"The other file uses other units ({otherName}, the open drawing {openName}): its sizes were converted (× {f.ToString("0.######", CultureInfo.InvariantCulture)}).";
            return f;
        }

        /// <summary>The differences: the other file is the old one unless otherIsNew (then "added" means only in the other file).</summary>
        public static List<DiffRow> Compare(IList<CmpItem> other, IList<CmpItem> open, string mode, double tol, bool otherIsNew = false)
        {
            var oldItems = otherIsNew ? open : other;
            var newItems = otherIsNew ? other : open;
            var rows = mode == "handle" ? ByHandle(oldItems, newItems, tol) : ByGeometry(oldItems, newItems, tol);
            if (otherIsNew)
                foreach (var r in rows) { var h = r.Handle; r.Handle = r.OtherHandle; r.OtherHandle = h; }
            return rows;
        }

        private static List<DiffRow> ByHandle(IList<CmpItem> oldItems, IList<CmpItem> newItems, double tol)
        {
            var rows = new List<DiffRow>();
            var byHandle = new Dictionary<string, CmpItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in newItems) if (n.Handle != null && !byHandle.ContainsKey(n.Handle)) byHandle[n.Handle] = n;
            var used = new HashSet<CmpItem>();
            foreach (var o in oldItems)
            {
                if (o.Handle != null && byHandle.TryGetValue(o.Handle, out var n) && n.Kind == o.Kind)
                {
                    used.Add(n);
                    var what = Differences(o, n, tol, true);
                    if (what.Count > 0) rows.Add(Row("changed", n, o, what));
                }
                else rows.Add(Row("removed", null, o, null));
            }
            foreach (var n in newItems) if (!used.Contains(n)) rows.Add(Row("added", n, null, null));
            return rows;
        }

        private static List<DiffRow> ByGeometry(IList<CmpItem> oldItems, IList<CmpItem> newItems, double tol)
        {
            var rows = new List<DiffRow>();
            double t = tol > 0 ? tol : 1e-9;
            var usedNew = new bool[newItems.Count];
            var oldMatched = new int[oldItems.Count];
            for (int i = 0; i < oldMatched.Length; i++) oldMatched[i] = -1;

            // 1. the same shape (within tol): unchanged, or changed in layer / properties only
            var shapes = new CellIndex(t);
            for (int j = 0; j < newItems.Count; j++) shapes.Add(ShapeKey(newItems[j]), newItems[j].Nums, j);
            for (int i = 0; i < oldItems.Count; i++)
            {
                var o = oldItems[i];
                int j = shapes.Find(ShapeKey(o), o.Nums, k => !usedNew[k] && SameShape(o, newItems[k], t));
                if (j < 0 && o.Alt != null) j = shapes.Find(ShapeKey(o), o.Alt, k => !usedNew[k] && SameShape(o, newItems[k], t));
                if (j < 0) continue;
                usedNew[j] = true; oldMatched[i] = j;
                var what = Differences(o, newItems[j], t, false);
                if (what.Count > 0) rows.Add(Row("changed", newItems[j], o, what));
            }
            // 2. the same kind at the same place (first point within tol): changed shape
            var anchors = new CellIndex(t);
            for (int j = 0; j < newItems.Count; j++) if (!usedNew[j]) anchors.Add(newItems[j].Kind, Anchor(newItems[j]), j);
            for (int i = 0; i < oldItems.Count; i++)
            {
                if (oldMatched[i] >= 0) continue;
                var o = oldItems[i];
                int j = anchors.Find(o.Kind, Anchor(o), k => !usedNew[k]);
                if (j < 0 && o.Alt != null) j = anchors.Find(o.Kind, o.AltAnchorAt ?? Anchor(o.Alt), k => !usedNew[k]);
                if (j < 0) continue;
                usedNew[j] = true; oldMatched[i] = j;
                rows.Add(Row("changed", newItems[j], o, Differences(o, newItems[j], t, true)));
            }
            for (int i = 0; i < oldItems.Count; i++) if (oldMatched[i] < 0) rows.Add(Row("removed", null, oldItems[i], null));
            for (int j = 0; j < newItems.Count; j++) if (!usedNew[j]) rows.Add(Row("added", newItems[j], null, null));
            return rows;
        }

        private static string ShapeKey(CmpItem it) => it.Kind + "|" + it.Exact;

        private static double[] Anchor(CmpItem it) => it.AnchorAt ?? Anchor(it.Nums);
        private static double[] Anchor(double[] nums) => nums == null ? new double[0] : nums.Take(Math.Min(3, nums.Length)).ToArray();

        private static bool SameShape(CmpItem a, CmpItem b, double t) =>
            a.Kind == b.Kind && a.Exact == b.Exact && (Close(a.Nums, b.Nums, t) || (a.Alt != null && Close(a.Alt, b.Nums, t))) && SameVolume(a.Volume, b.Volume) &&
            SameAngles(a, b, t);

        /// <summary>Sửa KTH-17: the angles of two objects agree within the tolerance at their reach.</summary>
        public static bool SameAngles(CmpItem a, CmpItem b, double t)
        {
            foreach (var k in a.Angles.Keys.Union(b.Angles.Keys))
            {
                if (!a.Angles.TryGetValue(k, out var x) || !b.Angles.TryGetValue(k, out var y)) return false;
                double d = Math.Abs(Math.IEEERemainder(x - y, 2 * Math.PI));
                if (d * Math.Max(a.Reach, b.Reach) > t * (1 + 1e-9)) return false;
            }
            return true;
        }

        /// <summary>
        /// Sửa KTH-15: a closed outline read the same way whatever its first vertex and direction: from its lowest-x (then y)
        /// vertex, counter-clockwise. verts: x, y each; bulges: of the segment from each vertex. Returns the numbers (x, y of
        /// each vertex, then the elevation) and the bulges in that order.
        /// </summary>
        public static (double[] nums, double[] bulges) CanonicalLoop(IList<double[]> verts, IList<double> bulges, double elevation)
        {
            int n = verts.Count;
            var v = verts.Select(p => new[] { p[0], p[1] }).ToList();
            var b = Enumerable.Range(0, n).Select(i => i < bulges.Count ? bulges[i] : 0).ToList();
            double area = 0;
            for (int i = 0; i < n; i++) { var p = v[i]; var q = v[(i + 1) % n]; area += p[0] * q[1] - q[0] * p[1]; }
            if (area < 0)
            {
                // reversed: vertex k is the old n-1-k; the segment k → k+1 is the old one (n-2-k) read backwards
                var rv = new List<double[]>(); var rb = new List<double>();
                for (int k = 0; k < n; k++) { rv.Add(v[n - 1 - k]); rb.Add(-b[((n - 2 - k) % n + n) % n]); }
                v = rv; b = rb;
            }
            int s = 0;
            for (int i = 1; i < n; i++) if (v[i][0] < v[s][0] || (v[i][0] == v[s][0] && v[i][1] < v[s][1])) s = i;
            var nums = new List<double>(); var bs = new List<double>();
            for (int k = 0; k < n; k++) { var p = v[(k + s) % n]; nums.Add(p[0]); nums.Add(p[1]); bs.Add(b[(k + s) % n] + 0.0); }
            nums.Add(elevation);
            return (nums.ToArray(), bs.ToArray());
        }

        public static bool Close(double[] a, double[] b, double t)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int n = 0; n < a.Length; n++) if (Math.Abs(a[n] - b[n]) > t * (1 + 1e-9)) return false;
            return true;
        }

        public static bool SameVolume(double? a, double? b)
        {
            if (!a.HasValue || !b.HasValue) return a.HasValue == b.HasValue;
            double m = Math.Max(Math.Abs(a.Value), Math.Abs(b.Value));
            return m == 0 || Math.Abs(a.Value - b.Value) <= m * H3Numbers.VolumeShare;
        }

        /// <summary>What differs between two matched objects ("layer A → B", "geometry", "text 'x' → 'y'"…), empty when nothing.</summary>
        public static List<string> Differences(CmpItem o, CmpItem n, double tol, bool geometry)
        {
            var what = new List<string>();
            if (geometry && !(o.Exact == n.Exact && (Close(o.Nums, n.Nums, tol) || (o.Alt != null && Close(o.Alt, n.Nums, tol))) && SameAngles(o, n, tol)))
                what.Add("geometry" + Moved(o, n));
            if (!SameVolume(o.Volume, n.Volume))
                what.Add($"volume {Fmt(o.Volume)} → {Fmt(n.Volume)}");
            foreach (var k in o.Sizes.Keys.Union(n.Sizes.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                double? a = o.Sizes.TryGetValue(k, out var x) ? x : (double?)null, b = n.Sizes.TryGetValue(k, out var y) ? y : (double?)null;
                if (a.HasValue != b.HasValue || (a.HasValue && Math.Abs(a.Value - b.Value) > tol * (1 + 1e-9))) what.Add($"{k} {Fmt(a)} → {Fmt(b)}");
            }
            foreach (var k in o.Areas.Keys.Union(n.Areas.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                double? a = o.Areas.TryGetValue(k, out var x) ? x : (double?)null, b = n.Areas.TryGetValue(k, out var y) ? y : (double?)null;
                if (!SameVolume(a, b)) what.Add($"{k} {Fmt(a)} → {Fmt(b)}");
            }
            if (!string.Equals(o.Layer ?? "", n.Layer ?? "", StringComparison.OrdinalIgnoreCase)) what.Add($"layer {o.Layer} → {n.Layer}");
            foreach (var k in o.Props.Keys.Union(n.Props.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                o.Props.TryGetValue(k, out var a);
                n.Props.TryGetValue(k, out var b);
                if (!string.Equals(a ?? "", b ?? "", StringComparison.Ordinal)) what.Add($"{k} {Short(a)} → {Short(b)}");
            }
            return what;
        }

        private static string Moved(CmpItem o, CmpItem n)
        {
            if (o.Min == null || n.Min == null || o.Max == null || n.Max == null || o.Min.Length < 2 || n.Min.Length < 2) return "";
            double dx = (n.Min[0] + n.Max[0] - o.Min[0] - o.Max[0]) / 2, dy = (n.Min[1] + n.Max[1] - o.Min[1] - o.Max[1]) / 2;
            double d = Math.Sqrt(dx * dx + dy * dy);
            return d > 0 ? " (centre moved " + d.ToString("0.##", CultureInfo.InvariantCulture) + ")" : "";
        }

        private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(none)";
            return "'" + (s.Length > 60 ? s.Substring(0, 57) + "…" : s) + "'";
        }

        private static DiffRow Row(string change, CmpItem n, CmpItem o, List<string> what)
        {
            var src = n ?? o;
            return new DiffRow
            {
                Change = change, Kind = src.Kind, Layer = src.Layer, Handle = n?.Handle, OtherHandle = o?.Handle,
                What = what ?? new List<string>(), Min = src.Min, Max = src.Max,
                // Sửa KTH-22: a changed object is ringed where it was too
                OldMin = n != null && o != null ? o.Min : null, OldMax = n != null && o != null ? o.Max : null,
            };
        }

        /// <summary>Counts by change.</summary>
        public static Dictionary<string, int> Counts(IEnumerable<DiffRow> rows)
        {
            var d = new Dictionary<string, int> { ["added"] = 0, ["removed"] = 0, ["changed"] = 0 };
            foreach (var r in rows) d[r.Change]++;
            return d;
        }

        // ------------------------------------------------------------ marks

        /// <summary>One cloud: its change, the box it rings (with the margin) and its colour.</summary>
        public sealed class Cloud { public string Change; public double[] Min, Max; public short Color; public int Count; }

        public static short ColorOf(string change) =>
            change == "added" ? H3Numbers.ColorAdded : change == "removed" ? H3Numbers.ColorRemoved : H3Numbers.ColorChanged;

        /// <summary>The margin (drawing units) around a box: a share of its longest side, at least CloudMinMarginMm.</summary>
        public static double Margin(double[] min, double[] max, double mmPerUnit)
        {
            double side = Math.Max(max[0] - min[0], max[1] - min[1]);
            double least = H3Numbers.CloudMinMarginMm / (mmPerUnit > 0 ? mmPerUnit : 1);
            return Math.Max(least, side * H3Numbers.CloudMarginShare);
        }

        /// <summary>
        /// The clouds of the differences: one box per row grown by its margin, boxes of the same change that overlap merged
        /// (so a wall of 20 changed lines gets one cloud). Rows without a box are left out.
        /// </summary>
        public static List<Cloud> Clouds(IEnumerable<DiffRow> rows, double mmPerUnit)
        {
            var clouds = new List<Cloud>();
            foreach (var g in rows.Where(r => r.Min != null && r.Max != null && r.Min.Length >= 2 && r.Max.Length >= 2).GroupBy(r => r.Change))
            {
                Cloud Box(double[] min, double[] max)
                {
                    double m = Margin(min, max, mmPerUnit);
                    return new Cloud { Change = g.Key, Color = ColorOf(g.Key), Count = 1, Min = new[] { min[0] - m, min[1] - m }, Max = new[] { max[0] + m, max[1] + m } };
                }
                var boxes = new List<Cloud>();
                foreach (var r in g)
                {
                    boxes.Add(Box(r.Min, r.Max));
                    // Sửa KTH-22: the old place of a changed object as well (merged with the new one when they touch)
                    if (r.OldMin != null && r.OldMax != null && r.OldMin.Length >= 2 && r.OldMax.Length >= 2 &&
                        !(r.OldMin[0] == r.Min[0] && r.OldMin[1] == r.Min[1] && r.OldMax[0] == r.Max[0] && r.OldMax[1] == r.Max[1]))
                    {
                        var old = Box(r.OldMin, r.OldMax);
                        old.Count = 0; // one difference, two places
                        boxes.Add(old);
                    }
                }
                bool merged = true;
                while (merged)
                {
                    merged = false;
                    var sorted = boxes.OrderBy(b => b.Min[0]).ToList();
                    var next = new List<Cloud>();
                    foreach (var b in sorted)
                    {
                        var hit = next.FirstOrDefault(c => c.Max[0] >= b.Min[0] && c.Min[0] <= b.Max[0] && c.Max[1] >= b.Min[1] && c.Min[1] <= b.Max[1]);
                        if (hit == null) { next.Add(b); continue; }
                        hit.Min = new[] { Math.Min(hit.Min[0], b.Min[0]), Math.Min(hit.Min[1], b.Min[1]) };
                        hit.Max = new[] { Math.Max(hit.Max[0], b.Max[0]), Math.Max(hit.Max[1], b.Max[1]) };
                        hit.Count += b.Count;
                        merged = true;
                    }
                    boxes = next;
                }
                clouds.AddRange(boxes);
            }
            return clouds;
        }

        /// <summary>The arc length of a cloud: about a tenth of its shorter side, at least the margin's minimum.</summary>
        public static double ArcLength(Cloud c, double mmPerUnit)
        {
            double least = H3Numbers.CloudMinMarginMm / (mmPerUnit > 0 ? mmPerUnit : 1);
            return Math.Max(least, Math.Min(c.Max[0] - c.Min[0], c.Max[1] - c.Min[1]) / 10);
        }

        /// <summary>The card token of drawing these marks (one per other file and number of clouds).</summary>
        public static string MarkToken(string otherPath, int clouds) => F5Confirm.FileToken("compare:" + (otherPath ?? "") + ":" + clouds.ToString(CultureInfo.InvariantCulture));

        /// <summary>The rows of the Excel list (header, one row per difference).</summary>
        public static List<List<object>> ExcelRows(IEnumerable<DiffRow> rows)
        {
            var t = new List<List<object>> { new List<object> { "Thay đổi", "Loại", "Layer", "Handle (bản đang mở)", "Handle (file kia)", "Khác ở đâu" } };
            foreach (var r in rows)
                t.Add(new List<object> { ChangeName(r.Change), r.Kind, r.Layer ?? "", r.Handle ?? "", r.OtherHandle ?? "", string.Join("; ", r.What) });
            return t;
        }

        public static string ChangeName(string change) => change == "added" ? "Thêm" : change == "removed" ? "Bớt" : "Đổi";

        /// <summary>
        /// Candidates by the cell of the first two numbers (and their neighbours), then a check: nothing compared pair by pair.
        /// </summary>
        private sealed class CellIndex
        {
            private readonly double _t;
            private readonly Dictionary<(string, long, long), List<int>> _cells = new Dictionary<(string, long, long), List<int>>();
            public CellIndex(double t) { _t = t; }
            private long C(double v) => (long)Math.Floor(v / _t);
            private (long, long) Cell(double[] v) => (v != null && v.Length > 0 ? C(v[0]) : 0, v != null && v.Length > 1 ? C(v[1]) : 0);
            private readonly Dictionary<int, double[]> _nums = new Dictionary<int, double[]>();
            public void Add(string key, double[] nums, int index)
            {
                var (a, b) = Cell(nums);
                if (!_cells.TryGetValue((key, a, b), out var list)) _cells[(key, a, b)] = list = new List<int>();
                list.Add(index);
                _nums[index] = nums;
            }
            public int Find(string key, double[] nums, Func<int, bool> ok)
            {
                var (a, b) = Cell(nums);
                for (long i = a - 1; i <= a + 1; i++)
                for (long j = b - 1; j <= b + 1; j++)
                {
                    if (!_cells.TryGetValue((key, i, j), out var list)) continue;
                    foreach (int k in list) if (Near(nums, _nums[k]) && ok(k)) return k;
                }
                return -1;
            }
            /// <summary>The first three numbers (the first point) within the tolerance.</summary>
            private bool Near(double[] a, double[] b)
            {
                if (a == null || b == null) return false;
                int n = Math.Min(3, Math.Min(a.Length, b.Length));
                for (int i = 0; i < n; i++) if (Math.Abs(a[i] - b[i]) > _t * (1 + 1e-9)) return false;
                return true;
            }
        }
    }
}
