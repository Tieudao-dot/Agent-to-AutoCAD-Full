using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c.md): the AutoCAD-free logic of the 2D utilities: hatch by a
    /// picked point (draw_hatch pick_point), dimensions of a whole floor plan (dim_floor_plan), block and area schedules
    /// (block_schedule, area_schedule), find &amp; replace of text (find_replace_text) and several layouts in one PDF
    /// (plot_pdf layouts). Points are [x, y(, z)] in drawing units. Drawing/Util2DTools.cs applies the results.
    /// </summary>
    public static class Util2DLogic
    {
        private const double Eps = 1e-9;

        // ================================================================== hatch by a picked point

        /// <summary>The loop traced around the picked point that encloses the others: the one with the largest area.</summary>
        public static int OuterLoopIndex(IList<double> areas)
        {
            if (areas == null || areas.Count == 0) return -1;
            int best = 0;
            for (int i = 1; i < areas.Count; i++) if (Math.Abs(areas[i]) > Math.Abs(areas[best])) best = i;
            return best;
        }

        /// <summary>What Claude gets when no closed boundary surrounds the point.</summary>
        public static string NoBoundaryMessage(double[] pt) =>
            $"draw_hatch: no closed boundary found around pick_point [{F(pt[0])}, {F(pt[1])}]. The point must lie inside an area fully closed by " +
            "lines / polylines / arcs (no gaps) and be visible on screen (zoom to it first). Tell the user in Vietnamese that the area is not " +
            "closed (or the point is outside), or close the gap / use boundary_handles or boundary_points.";

        // ================================================================== dimensions of a floor plan

        /// <summary>One straight piece of a wall (an arc is passed as its chord).</summary>
        public struct Seg
        {
            public double X1, Y1, X2, Y2;
            public Seg(double x1, double y1, double x2, double y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }
        }

        /// <summary>One linear dimension to create: horizontal (rotation 0) or vertical (rotation 90°).</summary>
        public sealed class DimSpec
        {
            public double[] P1, P2, DimLine;
            public bool Vertical;
            /// <summary>bottom / top / left / right.</summary>
            public string Side;
            /// <summary>detail (wall corners, openings), axes, overall.</summary>
            public string Row;
        }

        public static readonly string[] AllSides = { "bottom", "top", "left", "right" };

        /// <summary>
        /// Chains of dimensions around a floor plan, outside on each side asked: row 1 = the wall corners and openings
        /// visible from that side, row 2 = the axes (when axis lines are given), row 3 = the overall size. A row equal to
        /// the next one is left out. offset = distance of row 1 from the outline; spacing = between rows.
        /// </summary>
        public static List<DimSpec> FloorPlanDims(IList<Seg> walls, IList<Seg> axes, IEnumerable<string> sides, double offset, double spacing, double minPiece = 1)
        {
            if (walls == null || walls.Count == 0) throw ToolException.Invalid("dim_floor_plan: no wall lines found.");
            if (!(offset > 0) || !(spacing > 0)) throw ToolException.Invalid("dim_floor_plan: offset and spacing must be positive.");
            double minX = walls.Min(s => Math.Min(s.X1, s.X2)), maxX = walls.Max(s => Math.Max(s.X1, s.X2));
            double minY = walls.Min(s => Math.Min(s.Y1, s.Y2)), maxY = walls.Max(s => Math.Max(s.Y1, s.Y2));
            if (maxX - minX < minPiece && maxY - minY < minPiece) throw ToolException.Invalid("dim_floor_plan: the walls have no size.");
            var verts = walls.SelectMany(s => new[] { new[] { s.X1, s.Y1 }, new[] { s.X2, s.Y2 } }).ToList();
            var outList = new List<DimSpec>();
            foreach (var sideRaw in (sides ?? AllSides).Select(s => (s ?? "").Trim().ToLowerInvariant()).Distinct())
            {
                if (!AllSides.Contains(sideRaw)) throw ToolException.Invalid($"dim_floor_plan: unknown side '{sideRaw}' (bottom, top, left, right).");
                bool horizontalRow = sideRaw == "bottom" || sideRaw == "top";
                double sign = sideRaw == "bottom" || sideRaw == "left" ? -1 : 1;
                double edge = sideRaw == "bottom" ? minY : sideRaw == "top" ? maxY : sideRaw == "left" ? minX : maxX;
                // coordinate along the row (u) and across it (v, toward the side)
                double U(double[] p) => horizontalRow ? p[0] : p[1];
                double V(double[] p) => horizontalRow ? p[1] : p[0];

                // row 1: vertices nothing hides from this side
                var visible = verts.Where(p => Visible(p, walls, sideRaw)).ToList();
                var detail = Stations(visible, U, V, sign, minPiece);
                // row 2: axis lines across this row (vertical axes for bottom / top)
                var axisPts = new List<double[]>();
                foreach (var a in axes ?? new List<Seg>())
                {
                    bool vertical = Math.Abs(a.X1 - a.X2) < minPiece && Math.Abs(a.Y1 - a.Y2) >= minPiece;
                    bool horizontal = Math.Abs(a.Y1 - a.Y2) < minPiece && Math.Abs(a.X1 - a.X2) >= minPiece;
                    if (horizontalRow ? !vertical : !horizontal) continue;
                    var e1 = new[] { a.X1, a.Y1 };
                    var e2 = new[] { a.X2, a.Y2 };
                    axisPts.Add(sign * V(e1) >= sign * V(e2) ? e1 : e2); // the end toward the side
                }
                var axisRow = Stations(axisPts, U, V, sign, minPiece);
                // row 3: overall
                var lo = visible.Count > 0 ? visible : verts;
                var overall = Stations(new List<double[]> { lo.OrderBy(U).ThenBy(p => -sign * V(p)).First(), lo.OrderByDescending(U).ThenBy(p => -sign * V(p)).First() }, U, V, sign, minPiece);

                var rows = new List<(string name, List<double[]> pts)>();
                if (detail.Count >= 2) rows.Add(("detail", detail));
                if (axisRow.Count >= 2) rows.Add(("axes", axisRow));
                if (overall.Count >= 2) rows.Add(("overall", overall));
                // a row equal to the next one says nothing new
                for (int i = rows.Count - 2; i >= 0; i--)
                    if (SameStations(rows[i].pts, rows[i + 1].pts, U, minPiece)) rows.RemoveAt(i);

                for (int r = 0; r < rows.Count; r++)
                {
                    double line = edge + sign * (offset + r * spacing);
                    var pts = rows[r].pts;
                    for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        var a = pts[i];
                        var b = pts[i + 1];
                        double mid = (U(a) + U(b)) / 2;
                        outList.Add(new DimSpec
                        {
                            P1 = new[] { a[0], a[1], 0.0 },
                            P2 = new[] { b[0], b[1], 0.0 },
                            DimLine = horizontalRow ? new[] { mid, line, 0.0 } : new[] { line, mid, 0.0 },
                            Vertical = !horizontalRow,
                            Side = sideRaw,
                            Row = rows[r].name,
                        });
                    }
                }
            }
            return outList;
        }

        /// <summary>
        /// Distinct stations along the row (closer than minPiece = one), each at the point nearest the side, sorted.
        /// </summary>
        private static List<double[]> Stations(List<double[]> pts, Func<double[], double> u, Func<double[], double> v, double sign, double minPiece)
        {
            var sorted = pts.OrderBy(u).ThenBy(p => -sign * v(p)).ToList();
            var outList = new List<double[]>();
            foreach (var p in sorted)
            {
                if (outList.Count > 0 && u(p) - u(outList[outList.Count - 1]) < minPiece)
                {
                    if (sign * v(p) > sign * v(outList[outList.Count - 1])) outList[outList.Count - 1] = p;
                    continue;
                }
                outList.Add(p);
            }
            return outList;
        }

        private static bool SameStations(List<double[]> a, List<double[]> b, Func<double[], double> u, double tol) =>
            a.Count == b.Count && a.Zip(b, (x, y) => Math.Abs(u(x) - u(y)) < tol).All(t => t);

        /// <summary>
        /// A vertex is visible from a side when the ray from it toward that side crosses no wall line inside the line
        /// (touching a line's end or running along a line does not hide it).
        /// </summary>
        public static bool Visible(double[] p, IList<Seg> walls, string side, double tol = 1e-6)
        {
            bool vertRay = side == "bottom" || side == "top";
            double dir = side == "bottom" || side == "left" ? -1 : 1;
            foreach (var s in walls)
            {
                // along the ray: x (vertical ray) or y (horizontal ray); across: the other coordinate
                double a1 = vertRay ? s.X1 : s.Y1, a2 = vertRay ? s.X2 : s.Y2;
                double b1 = vertRay ? s.Y1 : s.X1, b2 = vertRay ? s.Y2 : s.X2;
                double pa = vertRay ? p[0] : p[1], pb = vertRay ? p[1] : p[0];
                double lo = Math.Min(a1, a2), hi = Math.Max(a1, a2);
                if (hi - lo < tol) continue; // parallel to the ray (runs along it or beside it)
                if (pa <= lo + tol || pa >= hi - tol) continue; // misses, or only touches an end
                double t = (pa - a1) / (a2 - a1);
                double b = b1 + t * (b2 - b1);
                if (dir * (b - pb) > tol) return false;
            }
            return true;
        }

        /// <summary>Default distance of the first row from the walls: 8 mm on paper (DIMSCALE), at least 5 % of the plan size.</summary>
        /// <remarks>KT2-04: planSize is in drawing units; the paper part is converted with mmPerUnit (INSUNITS, as the 3D tools).</remarks>
        public static double DefaultDimOffset(double dimscale, double planSize, double mmPerUnit = 1) =>
            Math.Max((dimscale > 0 ? dimscale : 1) * 8 / (mmPerUnit > 0 ? mmPerUnit : 1), planSize * 0.05);

        /// <summary>Schedule tables: 2.5 mm × DIMSCALE (at least 1) on paper, in drawing units (KT2-04).</summary>
        public static double DefaultTableTextHeight(double dimscale, double mmPerUnit = 1) =>
            Math.Max(dimscale > 0 ? dimscale : 1, 1) * 2.5 / (mmPerUnit > 0 ? mmPerUnit : 1);

        /// <summary>Drawing units² → m² (KT2-04).</summary>
        public static double ToM2(double area, double mmPerUnit = 1) => area * mmPerUnit * mmPerUnit / 1e6;

        /// <summary>m² → drawing units² (min_area, KT2-04).</summary>
        public static double FromM2(double m2, double mmPerUnit = 1) => m2 * 1e6 / (mmPerUnit * mmPerUnit);

        /// <summary>
        /// KT2-17: the paper of one page: the one asked for; else the layout's own when the device lists it (case-insensitive);
        /// else the fallback (A3).
        /// </summary>
        public static string PickMedia(string requested, string layoutMedia, IList<string> deviceMedia, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(requested)) return requested;
            if (!string.IsNullOrWhiteSpace(layoutMedia) && deviceMedia != null)
            {
                var hit = deviceMedia.FirstOrDefault(m => string.Equals(m, layoutMedia, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            return fallback;
        }

        /// <summary>KT2-16: the plot wrote the file: it exists now and is new, or newer than before the plot.</summary>
        public static bool PdfWritten(bool existedBefore, DateTime? before, bool existsAfter, DateTime? after) =>
            existsAfter && (!existedBefore || (before.HasValue && after.HasValue && after.Value > before.Value));

        public static string PdfLockedMessage(string path) =>
            $"plot_pdf: '{path}' is open in another program (a PDF viewer?) and cannot be replaced. Nothing was plotted. " +
            "Ask the user to close it, or plot to another file name.";

        public static string PdfNotWrittenMessage(bool existedBefore) =>
            existedBefore
                ? "The PDF file was not updated by this plot (it still has its old date). Tell the user the plot did not write the file; check the device / paper, or plot to another name."
                : "No PDF file was written. Tell the user the plot produced no file; check the device / paper and the folder.";

        /// <summary>
        /// KT2-32: the texts of one object that sit on a locked layer (layer null = not locked), each once: an attribute as
        /// "&lt;attribute handle&gt; (attribute TAG of block &lt;block handle&gt;)", other texts by their handle.
        /// </summary>
        public static List<(string handle, string layer)> LockedHits(IEnumerable<(string handle, string tag, string block, string layer)> hits)
        {
            var outList = new List<(string handle, string layer)>();
            foreach (var x in hits ?? Enumerable.Empty<(string, string, string, string)>())
            {
                if (x.layer == null) continue;
                var name = (x.handle ?? "").ToUpperInvariant() + (x.tag != null ? $" (attribute {x.tag} of block {(x.block ?? "").ToUpperInvariant()})" : "");
                if (!outList.Any(o => o.handle == name)) outList.Add((name, x.layer));
            }
            return outList;
        }

        /// <summary>KT2-02: why a replace was refused before any change: the texts on locked layers, by handle and layer.</summary>
        public static string LockedTextMessage(IList<(string handle, string layer)> locked) =>
            $"find_replace_text: nothing was replaced: {locked.Count} of the approved object(s) are on locked layers (" +
            string.Join(", ", locked.Take(20).Select(x => $"{x.handle} on {x.layer}")) + (locked.Count > 20 ? ", …" : "") +
            "). Tell the user which layers are locked. Either they unlock them and you replace again, or you replace the others: " +
            "preview again (locked texts are left out) and ask with a new card.";

        // ================================================================== block schedule

        public sealed class BlockItem
        {
            public string Name;
            public string Layer;
            /// <summary>Attribute tag (upper case) → value.</summary>
            public Dictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class CountRow
        {
            public string Name;
            /// <summary>The attribute value (group_by_attribute), or null.</summary>
            public string Value;
            public int Count;
        }

        /// <summary>Counts per block name (and per value of one attribute), sorted by name then value; blocks without the tag count under "".</summary>
        public static List<CountRow> CountBlocks(IEnumerable<BlockItem> items, string byAttribute)
        {
            var tag = string.IsNullOrWhiteSpace(byAttribute) ? null : byAttribute.Trim();
            var d = new Dictionary<(string, string), int>();
            foreach (var it in items ?? Enumerable.Empty<BlockItem>())
            {
                string v = null;
                if (tag != null) v = it.Attributes != null && it.Attributes.TryGetValue(tag, out var s) ? (s ?? "").Trim() : "";
                var key = (it.Name ?? "", v);
                d[key] = d.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            var cmp = StringComparer.Create(CultureInfo.InvariantCulture, true);
            return d.Select(kv => new CountRow { Name = kv.Key.Item1, Value = kv.Key.Item2, Count = kv.Value })
                .OrderBy(r => r.Name, cmp).ThenBy(r => r.Value ?? "", NaturalComparer.Instance).ToList();
        }

        /// <summary>The table of a block schedule: header, one row per count, a total row.</summary>
        public static List<List<object>> BlockTable(IList<CountRow> rows, string byAttribute)
        {
            bool tag = !string.IsNullOrWhiteSpace(byAttribute);
            var header = new List<object> { "STT", "Tên block" };
            if (tag) header.Add(byAttribute.Trim().ToUpperInvariant());
            header.Add("Số lượng");
            var data = new List<List<object>> { header };
            int i = 0;
            foreach (var r in rows)
            {
                var row = new List<object> { (++i).ToString(CultureInfo.InvariantCulture), r.Name };
                if (tag) row.Add(string.IsNullOrEmpty(r.Value) ? "(trống)" : r.Value);
                row.Add(r.Count.ToString(CultureInfo.InvariantCulture));
                data.Add(row);
            }
            var total = new List<object> { "", "Tổng" };
            if (tag) total.Add("");
            total.Add(rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture));
            data.Add(total);
            return data;
        }

        /// <summary>"D1" &lt; "D2" &lt; "D10" (numbers inside names compare as numbers).</summary>
        public sealed class NaturalComparer : IComparer<string>
        {
            public static readonly NaturalComparer Instance = new NaturalComparer();

            public int Compare(string a, string b)
            {
                a = a ?? ""; b = b ?? "";
                int i = 0, j = 0;
                while (i < a.Length && j < b.Length)
                {
                    if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                    {
                        int si = i, sj = j;
                        while (i < a.Length && char.IsDigit(a[i])) i++;
                        while (j < b.Length && char.IsDigit(b[j])) j++;
                        var na = a.Substring(si, i - si).TrimStart('0');
                        var nb = b.Substring(sj, j - sj).TrimStart('0');
                        if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                        int c = string.CompareOrdinal(na, nb);
                        if (c != 0) return c;
                    }
                    else
                    {
                        int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                        if (c != 0) return c;
                        i++; j++;
                    }
                }
                return (a.Length - i).CompareTo(b.Length - j);
            }
        }

        // ================================================================== area schedule

        public sealed class AreaItem
        {
            public string Handle;
            public string Type;
            public string Layer;
            /// <summary>Area in drawing units² (mm²).</summary>
            public double Area;
            /// <summary>The outline (closed, no repeat of the first point); null when unknown.</summary>
            public List<double[]> Outline;
        }

        public sealed class TextItem
        {
            public string Text;
            public double[] Position;
        }

        public sealed class AreaRow
        {
            /// <summary>Room name (group_by room) or layer (group_by layer).</summary>
            public string Name;
            public string Layer;
            public int Count;
            public double Area;
            public List<string> Handles = new List<string>();
        }

        /// <summary>
        /// The same area drawn twice (a room polyline and its hatch): same area within 0.1 % and the outlines' centres
        /// closer than 1 % of the room's size. The first one (polylines are listed before hatches) is kept.
        /// </summary>
        public static List<AreaItem> Dedupe(IList<AreaItem> items, List<string> skipped)
        {
            var kept = new List<AreaItem>();
            foreach (var it in items)
            {
                var c = Centre(it);
                var twin = kept.FirstOrDefault(k =>
                {
                    if (Math.Abs(k.Area - it.Area) > 0.001 * Math.Max(Math.Abs(k.Area), Math.Abs(it.Area))) return false;
                    var kc = Centre(k);
                    if (c == null || kc == null) return false;
                    return Math.Sqrt((c[0] - kc[0]) * (c[0] - kc[0]) + (c[1] - kc[1]) * (c[1] - kc[1])) <= 0.01 * Math.Sqrt(Math.Abs(it.Area)) + Eps;
                });
                if (twin != null) { skipped?.Add($"{it.Handle}: same area as {twin.Handle} (drawn twice), counted once"); continue; }
                kept.Add(it);
            }
            return kept;
        }

        private static double[] Centre(AreaItem it)
        {
            if (it.Outline == null || it.Outline.Count == 0) return null;
            return new[] { it.Outline.Average(p => p[0]), it.Outline.Average(p => p[1]) };
        }

        /// <summary>Point in polygon (even-odd); on the edge counts as inside.</summary>
        public static bool Inside(IList<double[]> poly, double[] p)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                // on the edge
                double cross = (xj - xi) * (p[1] - yi) - (yj - yi) * (p[0] - xi);
                if (Math.Abs(cross) <= 1e-9 * Math.Max(1, Math.Abs(xj - xi) + Math.Abs(yj - yi)) &&
                    p[0] >= Math.Min(xi, xj) - 1e-9 && p[0] <= Math.Max(xi, xj) + 1e-9 && p[1] >= Math.Min(yi, yj) - 1e-9 && p[1] <= Math.Max(yi, yj) + 1e-9)
                    return true;
                if ((yi > p[1]) != (yj > p[1]) && p[0] < (xj - xi) * (p[1] - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// The room name of each area: the texts whose insertion point lies inside it and not inside a smaller area of the
        /// list (a text belongs to the innermost room). Several texts: joined with " " in reading order (top to bottom,
        /// left to right). None: "".
        /// </summary>
        public static Dictionary<string, string> RoomNames(IList<AreaItem> areas, IList<TextItem> texts)
        {
            var names = areas.ToDictionary(a => a.Handle, a => new List<TextItem>(), StringComparer.OrdinalIgnoreCase);
            foreach (var t in texts ?? new List<TextItem>())
            {
                if (t?.Position == null || string.IsNullOrWhiteSpace(t.Text)) continue;
                var owner = areas.Where(a => a.Outline != null && Inside(a.Outline, t.Position)).OrderBy(a => Math.Abs(a.Area)).FirstOrDefault();
                if (owner != null) names[owner.Handle].Add(t);
            }
            return names.ToDictionary(kv => kv.Key,
                kv => string.Join(" ", kv.Value.OrderByDescending(t => Math.Round(t.Position[1], 3)).ThenBy(t => t.Position[0]).Select(t => CleanText(t.Text))),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Areas that contain another area of the list (its centre inside, smaller area): a building outline drawn on the
        /// room layer would add its area on top of the rooms. Reported so Claude can ask and leave it out.
        /// </summary>
        public static List<string> Containers(IList<AreaItem> areas)
        {
            var outList = new List<string>();
            foreach (var a in areas)
            {
                if (a.Outline == null) continue;
                if (areas.Any(b => !ReferenceEquals(a, b) && Math.Abs(b.Area) < Math.Abs(a.Area) && Centre(b) is double[] c && Inside(a.Outline, c)))
                    outList.Add(a.Handle);
            }
            return outList;
        }

        /// <summary>One line of plain text: MTEXT line breaks and formatting codes removed.</summary>
        public static string CleanText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var t = s.Replace("\\P", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Trim();
        }

        /// <summary>Rows by layer (count + area) or by room (one row per area, name from RoomNames), sorted by name.</summary>
        public static List<AreaRow> AreaRows(IList<AreaItem> areas, IList<TextItem> texts, string groupBy)
        {
            var g = (groupBy ?? "layer").Trim().ToLowerInvariant();
            var cmp = NaturalComparer.Instance;
            if (g == "layer")
            {
                return areas.GroupBy(a => a.Layer ?? "", StringComparer.OrdinalIgnoreCase)
                    .Select(x => new AreaRow { Name = x.Key, Layer = x.Key, Count = x.Count(), Area = x.Sum(a => Math.Abs(a.Area)), Handles = x.Select(a => a.Handle).ToList() })
                    .OrderBy(r => r.Name, cmp).ToList();
            }
            if (g != "room") throw ToolException.Invalid("area_schedule: group_by must be layer or room.");
            var names = RoomNames(areas, texts);
            return areas.Select(a => new AreaRow
            {
                Name = names.TryGetValue(a.Handle, out var n) ? n : "",
                Layer = a.Layer,
                Count = 1,
                Area = Math.Abs(a.Area),
                Handles = new List<string> { a.Handle },
            }).OrderBy(r => r.Name.Length == 0 ? 1 : 0).ThenBy(r => r.Name, cmp).ToList();
        }

        /// <summary>m² with the given decimals, Vietnamese style decimal comma ("12,50").</summary>
        public static string M2(double areaMm2, int decimals, double unitToMm = 1)
        {
            double m2 = areaMm2 * unitToMm * unitToMm / 1e6;
            return Math.Round(m2, decimals).ToString("F" + decimals, CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>The table of an area schedule: header, rows, total.</summary>
        public static List<List<object>> AreaTable(IList<AreaRow> rows, string groupBy, int decimals, double unitToMm = 1)
        {
            bool room = (groupBy ?? "layer").Trim().ToLowerInvariant() == "room";
            var data = new List<List<object>>();
            data.Add(room ? new List<object> { "STT", "Tên phòng", "Layer", "Diện tích (m²)" } : new List<object> { "STT", "Layer", "Số vùng", "Diện tích (m²)" });
            int i = 0;
            foreach (var r in rows)
            {
                var idx = (++i).ToString(CultureInfo.InvariantCulture);
                data.Add(room
                    ? new List<object> { idx, r.Name.Length == 0 ? "(chưa có tên)" : r.Name, r.Layer ?? "", M2(r.Area, decimals, unitToMm) }
                    : new List<object> { idx, r.Name, r.Count.ToString(CultureInfo.InvariantCulture), M2(r.Area, decimals, unitToMm) });
            }
            data.Add(new List<object> { "", "Tổng", room ? "" : rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture), M2(rows.Sum(r => r.Area), decimals, unitToMm) });
            return data;
        }

        // ================================================================== find & replace

        /// <summary>
        /// find_replace_text with handles changes objects: the plugin does it only for the handles the user approved on
        /// the confirmation card (user_confirmed, added by the assistant; a macro never records it).
        /// </summary>
        public const int MaxReplaceHandles = 5000;

        /// <summary>Replaces every match; count = how many. whole_word: letters / digits must not touch the match.</summary>
        public static string Replace(string s, string find, string replacement, bool matchCase, bool wholeWord, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(find)) return s;
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var sb = new StringBuilder();
            int pos = 0;
            int i = s.IndexOf(find, 0, cmp);
            while (i >= 0)
            {
                bool ok = !wholeWord ||
                          ((i == 0 || !char.IsLetterOrDigit(s[i - 1])) && (i + find.Length >= s.Length || !char.IsLetterOrDigit(s[i + find.Length])));
                if (ok)
                {
                    sb.Append(s, pos, i - pos).Append(replacement ?? "");
                    pos = i + find.Length;
                    count++;
                    i = pos >= s.Length ? -1 : s.IndexOf(find, pos, cmp);
                }
                else i = i + 1 >= s.Length ? -1 : s.IndexOf(find, i + 1, cmp);
            }
            if (count == 0) return s;
            sb.Append(s, pos, s.Length - pos);
            return sb.ToString();
        }

        /// <summary>MTEXT formatting codes the find text must not cut through: a find with "\", "{" or "}" is refused for MTEXT.</summary>
        public static bool SafeForMText(string find) => find != null && find.IndexOfAny(new[] { '\\', '{', '}' }) < 0;

        /// <summary>MTEXT codes followed by an argument up to ";" (\A1; \C3; \fArial|b0; \H2.5x; \Q15; \T1.2; \W0.8; \pxi1; \S1^2;).</summary>
        private const string MTextArgCodes = "ACcFfHQTWpS";

        /// <summary>
        /// Test & sửa đợt C (NC-1): the text split into plain runs and formatting codes, so a replace never cuts a code.
        /// TEXT (mtext false): %%c %%d %%p %%u %%o %%% and %%nnn. MTEXT / dimension text / table cells (mtext true): also
        /// \P, \~, \\, \{, \}, \U+xxxx, codes with an argument up to ";" and the { } groups. keepPercent: %% is plain text
        /// (the find itself holds "%%", e.g. replacing %%c).
        /// </summary>
        public static List<KeyValuePair<string, bool>> FormatSegments(string s, bool mtext, bool keepPercent = false)
        {
            var outList = new List<KeyValuePair<string, bool>>();
            var sb = new StringBuilder();
            void Flush() { if (sb.Length > 0) { outList.Add(new KeyValuePair<string, bool>(sb.ToString(), false)); sb.Clear(); } }
            void Code(string c) { Flush(); outList.Add(new KeyValuePair<string, bool>(c, true)); }
            s = s ?? "";
            int i = 0;
            while (i < s.Length)
            {
                char ch = s[i];
                if (!keepPercent && ch == '%' && i + 2 < s.Length && s[i + 1] == '%')
                {
                    int len = i + 4 < s.Length && char.IsDigit(s[i + 2]) && char.IsDigit(s[i + 3]) && char.IsDigit(s[i + 4]) ? 5 : 3;
                    Code(s.Substring(i, len));
                    i += len;
                    continue;
                }
                if (mtext && (ch == '{' || ch == '}')) { Code(ch.ToString()); i++; continue; }
                if (mtext && ch == '\\' && i + 1 < s.Length)
                {
                    char c = s[i + 1];
                    int end;
                    if (MTextArgCodes.IndexOf(c) >= 0)
                    {
                        int semi = s.IndexOf(';', i + 2);
                        end = semi < 0 ? s.Length : semi + 1;
                    }
                    else if ((c == 'U' || c == 'u') && i + 2 < s.Length && s[i + 2] == '+') end = Math.Min(s.Length, i + 7);
                    else if ((c == 'M' || c == 'm') && i + 2 < s.Length && s[i + 2] == '+') end = Math.Min(s.Length, i + 8);
                    else end = i + 2;
                    Code(s.Substring(i, end - i));
                    i = end;
                    continue;
                }
                sb.Append(ch);
                i++;
            }
            Flush();
            return outList;
        }

        /// <summary>A replacement written into MTEXT: \ { } shown as typed (escaped), a line break becomes \P.</summary>
        public static string EscapeMText(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\P");

        /// <summary>
        /// Test & sửa đợt C (NC-1): Replace on the plain runs only (FormatSegments), so "P" never hits the "\P" of an MTEXT
        /// line break nor "c" the "%%c" of a diameter sign; in MTEXT the replacement is escaped (EscapeMText).
        /// </summary>
        public static string ReplaceFormatted(string s, string find, string replacement, bool matchCase, bool wholeWord, bool mtext, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(find)) return s;
            var repl = mtext ? EscapeMText(replacement) : replacement ?? "";
            var sb = new StringBuilder();
            foreach (var seg in FormatSegments(s, mtext, find.Contains("%%")))
            {
                if (seg.Value) { sb.Append(seg.Key); continue; }
                sb.Append(Replace(seg.Key, find, repl, matchCase, wholeWord, out int n));
                count += n;
            }
            return count == 0 ? s : sb.ToString();
        }

        /// <summary>The handles of an apply call the user has not approved on the card (upper case), in order.</summary>
        public static List<string> NotApproved(IEnumerable<string> handles, ICollection<string> approved) =>
            (handles ?? Enumerable.Empty<string>()).Select(h => (h ?? "").Trim().ToUpperInvariant()).Where(h => h.Length > 0)
                .Where(h => approved == null || !approved.Contains(h)).Distinct().ToList();

        /// <summary>The refusal when find_replace_text would change objects without the card (starts like the assistant's gate).</summary>
        public static string ReplaceCardMessage(int count) =>
            $"Confirmation required: find_replace_text changes existing text, and {count} of these objects are not on a confirmation card the user approved. " +
            "Nothing was replaced. First call find_replace_text without handles (preview), then call request_confirmation with the preview's handles and " +
            "one Vietnamese sentence with the count, the find and the replace text and an example (e.g. \"Thay 'P.NGU' bằng 'PHÒNG NGỦ' ở 12 chữ (vd: P.NGU 1 → PHÒNG NGỦ 1)?\"), " +
            "and end your turn. Replace (with exactly those handles) only after the user presses Đồng ý.";

        // ================================================================== several layouts in one PDF

        private static readonly string[] Reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        /// <summary>A Windows-safe file name part: &lt;&gt;:"/\|?* and control characters become "_", no trailing dot / space, no reserved name.</summary>
        public static string SafeFileName(string name, string fallback = "layout")
        {
            var sb = new StringBuilder();
            foreach (var ch in name ?? "") sb.Append(ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0 ? '_' : ch);
            var s = sb.ToString().Trim().TrimEnd('.', ' ');
            if (s.Length == 0) s = fallback;
            var stem = s.Split('.')[0];
            if (Reserved.Contains(stem.ToUpperInvariant())) s = "_" + s;
            if (s.Length > 120) s = s.Substring(0, 120).TrimEnd('.', ' ');
            return s;
        }

        /// <summary>
        /// The layouts of a multi-page plot, in the order asked: "all" / "*" = every paper layout in tab order (no Model);
        /// otherwise each name (any case) once. Unknown names: error listing what exists.
        /// </summary>
        public static List<string> ResolveLayouts(IList<string> requested, IList<string> tabOrder)
        {
            var existing = (tabOrder ?? new List<string>()).ToList();
            if (requested == null || requested.Count == 0) throw ToolException.Invalid("plot_pdf: 'layouts' is empty.");
            if (requested.Count == 1 && (requested[0] ?? "").Trim() is var one && (one == "*" || one.Equals("all", StringComparison.OrdinalIgnoreCase)))
            {
                var paper = existing.Where(n => !n.Equals("Model", StringComparison.OrdinalIgnoreCase)).ToList();
                if (paper.Count == 0) throw ToolException.Invalid("plot_pdf: the drawing has no paper layouts.");
                return paper;
            }
            var outList = new List<string>();
            var missing = new List<string>();
            foreach (var r in requested)
            {
                var name = (r ?? "").Trim();
                var hit = existing.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (hit == null) { missing.Add(name); continue; }
                if (!outList.Contains(hit, StringComparer.OrdinalIgnoreCase)) outList.Add(hit);
            }
            if (missing.Count > 0)
                throw ToolException.Invalid($"plot_pdf: layout(s) not found: {string.Join(", ", missing)}. Layouts: {string.Join(", ", existing)}.");
            return outList;
        }

        /// <summary>
        /// Test & sửa đợt C (NC-4): why a file name given in 'output' cannot be written on Windows, or null when it can
        /// (characters &lt;&gt;:"/\|?* or control characters, a trailing dot / space, a reserved name such as CON or NUL).
        /// </summary>
        public static string BadFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "the file name is empty";
            var bad = fileName.Where(ch => ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0).Distinct().ToList();
            if (bad.Count > 0) return "the file name contains " + string.Join(" ", bad.Select(ch => ch < 32 ? "a control character" : ch.ToString()));
            if (fileName.EndsWith(".", StringComparison.Ordinal) || fileName.EndsWith(" ", StringComparison.Ordinal)) return "the file name ends with a dot or a space";
            if (Reserved.Contains(fileName.Split('.')[0].Trim().ToUpperInvariant())) return $"'{fileName.Split('.')[0]}' is a reserved name on Windows";
            return null;
        }

        /// <summary>
        /// Test & sửa đợt C (NC-5): stations closer than this are one (1 drawing unit in mm drawings; a plan drawn in metres
        /// keeps a 0.9 m door): 1 unit, at most 1/10 000 of the plan size, at least 1e-6.
        /// </summary>
        public static double PieceTolerance(double planSize) => Math.Max(1e-6, Math.Min(1, planSize * 1e-4));

        /// <summary>The default output of a multi-page plot: &lt;drawing&gt;-&lt;n&gt;layouts.pdf (or -&lt;layout&gt;.pdf for one).</summary>
        public static string DefaultPdfName(string drawingName, IList<string> layouts)
        {
            var stem = SafeFileName(string.IsNullOrEmpty(drawingName) ? "Drawing" : drawingName, "Drawing");
            return layouts.Count == 1 ? $"{stem}-{SafeFileName(layouts[0])}.pdf" : $"{stem}-{layouts.Count}-layouts.pdf";
        }

        private static string F(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
    }
}
