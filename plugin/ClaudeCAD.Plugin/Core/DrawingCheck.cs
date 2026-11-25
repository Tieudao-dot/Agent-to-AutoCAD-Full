using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt G2: the numbers and words of the drawing check the user meets, one place each
    /// (docs/ban-giao/nang-cap-g2.md, "Con số chờ chủ dự án duyệt"). Lengths are millimetres, turned into drawing units.
    /// </summary>
    public static class G2Numbers
    {
        /// <summary>Two objects of the same kind and layer are "trùng" when their points differ by at most this (mm).</summary>
        public const double DuplicateTolMm = 0.1;
        /// <summary>A line / polyline / arc shorter than this (mm), or a circle of a smaller radius, is "dài 0".</summary>
        public const double ZeroLengthMm = 0.01;
        /// <summary>An object is "lạc xa" when it lies farther from the main part than this many times the main part's size.</summary>
        public const double FarFactor = 20;
        /// <summary>The main part is never taken smaller than this (mm), so a small drawing does not flag its own title.</summary>
        public const double FarMinSizeMm = 10000;
        /// <summary>Fewer objects than this: the far-away check is skipped (no main part to speak of).</summary>
        public const int FarMinObjects = 10;
        /// <summary>A text of the standard whose height is off every standard height by more than this share is reported.</summary>
        public const double TextHeightTolerance = 0.10;
        /// <summary>Example handles per group in the result.</summary>
        public const int ExamplesPerGroup = 5;
        /// <summary>Objects read at most by one check (model + layouts); the result says "scanned N of M".</summary>
        public const int MaxScan = 20000;
        /// <summary>Objects changed at most by one fix_drawing call (call again for the rest).</summary>
        public const int MaxFix = 2000;
        /// <summary>A viewport scale counts as standard when it is within this share of a 1:n of F2Numbers.StandardScales.</summary>
        public const double ViewportScaleTolerance = 0.005;

        /// <summary>The levels the user sees.</summary>
        public const string LevelFix = "nên sửa", LevelConsider = "cân nhắc", LevelHint = "gợi ý";
    }

    /// <summary>Nâng cấp đợt G2: the groups of check_drawing (Core, no AutoCAD; the scan is in Drawing/DrawingCheckTools.cs).</summary>
    public static class DrawingCheckLogic
    {
        public sealed class GroupDef
        {
            public string Id, Name, Level, Topic, Hint;
            /// <summary>The fix_drawing fix of the group (null = report only).</summary>
            public bool Fixable;
            /// <summary>Only checked when the drawing uses the standard of setup_standards.</summary>
            public bool NeedsStandard;
        }

        private static GroupDef G(string id, string topic, string name, string level, bool fixable, bool std, string hint) =>
            new GroupDef { Id = id, Topic = topic, Name = name, Level = level, Fixable = fixable, NeedsStandard = std, Hint = hint };

        /// <summary>Every group, in the order of the report (topic: layer, props, text, geometry, block, sheet).</summary>
        public static readonly IReadOnlyList<GroupDef> Groups = new List<GroupDef>
        {
            G("layer_zero", "layer", "Hình nằm trên layer 0", G2Numbers.LevelFix, true, false, "fix_drawing layer_zero with layer = the layer the user chooses"),
            G("defpoints", "layer", "Hình nằm trên layer Defpoints (không in ra)", G2Numbers.LevelFix, true, false, "fix_drawing defpoints with layer = the layer the user chooses"),
            G("hidden_layers", "layer", "Layer tắt / đóng băng / không in còn chứa hình (sẽ in thiếu)", G2Numbers.LevelConsider, false, false, "set_layer_props to turn on / thaw / plot, only if the user wants them printed"),
            G("nonstandard_layers", "layer", "Layer không thuộc bảng layer chuẩn", G2Numbers.LevelConsider, false, true, "move the objects with set_entity_props if the user wants; never rename the user's layers on your own"),
            G("not_bylayer", "props", "Màu / kiểu nét / độ dày nét không theo layer", G2Numbers.LevelConsider, true, false, "fix_drawing not_bylayer (back to ByLayer)"),
            G("nonstandard_styles", "text", "Chữ / kích thước / ghi chú mũi tên dùng kiểu ngoài bộ chuẩn", G2Numbers.LevelConsider, true, true, "fix_drawing nonstandard_styles (the style of the standard)"),
            G("text_height", "text", "Chữ lệch cỡ chuẩn theo tỉ lệ", G2Numbers.LevelConsider, false, true, "edit_text height = one of the standard heights the result lists"),
            G("empty_text", "text", "Chữ rỗng", G2Numbers.LevelFix, true, false, "fix_drawing empty_text (erase)"),
            G("dim_override", "text", "Kích thước bị gõ đè số", G2Numbers.LevelFix, true, false, "fix_drawing dim_override (show the measured value again)"),
            G("no_dimensions", "text", "Có tường mà chưa có kích thước nào", G2Numbers.LevelHint, false, false, "dim_floor_plan"),
            G("duplicates", "geometry", "Hình trùng nhau", G2Numbers.LevelFix, true, false, "fix_drawing duplicates (erase the extra copies, keep one)"),
            G("zero_length", "geometry", "Đường dài 0", G2Numbers.LevelFix, true, false, "fix_drawing zero_length (erase)"),
            G("far_away", "geometry", "Hình lạc rất xa phần còn lại", G2Numbers.LevelFix, false, false, "show the user (zoom_to); erase or move only if the user says so"),
            G("hatch_broken", "geometry", "Hatch hỏng (không có biên)", G2Numbers.LevelFix, false, false, "redraw the hatch with draw_hatch"),
            G("hatch_unassociated", "geometry", "Hatch mất liên kết với biên", G2Numbers.LevelConsider, false, false, "only a note: the hatch no longer follows its boundary"),
            G("unnamed_blocks", "block", "Block chưa đặt tên (dán dạng block)", G2Numbers.LevelConsider, false, false, "a name with define_block if the user wants"),
            G("empty_attributes", "block", "Thuộc tính block để trống", G2Numbers.LevelConsider, false, false, "set_block_attributes"),
            G("missing_refs", "block", "Xref / ảnh mất file", G2Numbers.LevelFix, false, false, "reload_xref or ask the user for the file"),
            G("no_title_block", "sheet", "Tờ in không có khung tên", G2Numbers.LevelConsider, false, false, "create_sheet"),
            G("viewport_unlocked", "sheet", "Khung nhìn chưa khóa", G2Numbers.LevelFix, true, false, "fix_drawing viewport_unlocked (lock)"),
            G("viewport_scale", "sheet", "Khung nhìn có tỉ lệ không chuẩn", G2Numbers.LevelConsider, false, false, "set_viewport scale = a standard 1:n"),
            G("title_empty", "sheet", "Ô khung tên còn trống", G2Numbers.LevelConsider, false, false, "set_block_attributes on the title block"),
        };

        public static GroupDef Find(string id) => Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

        public static IEnumerable<string> FixableIds => Groups.Where(g => g.Fixable).Select(g => g.Id);

        public static string LevelKey(string level) =>
            level == G2Numbers.LevelFix ? "nen_sua" : level == G2Numbers.LevelConsider ? "can_nhac" : "goi_y";

        // ------------------------------------------------------------ the standard

        /// <summary>Layers of the ve-kien-truc table and of the tools (A-*, the sheet frame, plants).</summary>
        public static readonly IReadOnlyCollection<string> KnownLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "0", "Defpoints", "A-AXIS", "A-AXIS-TAG", "A-COL", "A-COLS", "A-WALL", "A-WALL-PAT", "A-DOOR", "A-WIND", "A-STAIR",
            "A-FURN", "A-DIM", "A-TEXT", "A-HIDDEN", "A-SLAB", "A-FLOR", "A-FIXT", "A-ELEV", "A-SECT", "A-SECT-BACK", "A-SECT-PAT",
            F2Numbers.FrameLayer, F2Numbers.ViewportLayer, F2Numbers.PlantLayer,
            G5Numbers.Layer, // Test & sửa G5: the background picture of trace_image
        };

        /// <summary>
        /// A layer name of the standard: the table, a layer the 3D / section tools make from one of them (A-WALL-3D, A-ROOF…: any
        /// "A-" name), an xref's layer (it belongs to the other drawing).
        /// </summary>
        public static bool IsStandardLayer(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.Contains("|")) return true;
            if (KnownLayers.Contains(name)) return true;
            return name.StartsWith("A-", StringComparison.OrdinalIgnoreCase) || name.StartsWith(F2Numbers.FrameLayer + "-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The prefix of the standard in use ("KT" first, then "VN"), from the text styles of the drawing; null = not used.</summary>
        public static string StandardPrefix(IEnumerable<string> textStyles)
        {
            var set = new HashSet<string>(textStyles ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var pre in new[] { "KT", "VN" })
                if (set.Contains(StandardsLogic.TextStyleName(pre))) return pre;
            return null;
        }

        /// <summary>"KT-100", "KT-100-M" → 100 for the prefix; null when it is not a dimension style of the standard.</summary>
        public static int? DimStyleScale(string prefix, string dimStyle)
        {
            if (prefix == null || dimStyle == null) return null;
            var m = Regex.Match(dimStyle, "^" + Regex.Escape(prefix) + @"-([0-9]+)(-(M|CM|IN|DM|FT))?$", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : (int?)null;
        }

        /// <summary>The text styles of the standard (its text style and the title block's).</summary>
        public static bool IsStandardTextStyle(string prefix, string style) =>
            prefix != null && (string.Equals(style, StandardsLogic.TextStyleName(prefix), StringComparison.OrdinalIgnoreCase)
                || string.Equals(style, F2Numbers.TitleTextStyle, StringComparison.OrdinalIgnoreCase));

        public static bool IsStandardDimStyle(string prefix, string style) => DimStyleScale(prefix, style).HasValue;

        /// <summary>The standard text heights (drawing units) at this scale: title, room name, note, small.</summary>
        public static double[] StandardHeights(int scale, double mmPerUnit) =>
            StandardsLogic.TextHeights(scale, mmPerUnit).Values.Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();

        /// <summary>A text height off every standard height by more than the tolerance; returns the nearest standard height.</summary>
        public static bool HeightOff(double h, double[] standard, out double nearest)
        {
            nearest = 0;
            if (standard == null || standard.Length == 0 || !(h > 0)) return false;
            nearest = standard.OrderBy(s => Math.Abs(s - h)).First();
            return standard.All(s => Math.Abs(h - s) > G2Numbers.TextHeightTolerance * s);
        }

        // ------------------------------------------------------------ geometry

        /// <summary>The key of an object for "trùng": its kind, layer and the numbers of its shape rounded to the tolerance.</summary>
        public static string ShapeKey(string kind, string layer, IEnumerable<double> numbers, double tol, string extra = null)
        {
            var sb = new StringBuilder(kind).Append('|').Append((layer ?? "").ToUpperInvariant());
            foreach (var n in numbers)
            {
                double q = tol > 0 ? Math.Round(n / tol) : n;
                if (q == 0) q = 0; // -0
                sb.Append('|').Append(q.ToString("R", CultureInfo.InvariantCulture));
            }
            if (extra != null) sb.Append('|').Append(extra);
            return sb.ToString();
        }

        /// <summary>A line's two ends in one order (a line drawn backwards is the same line).</summary>
        public static double[] LineNumbers(double[] a, double[] b)
        {
            bool swap = a[0] > b[0] || (a[0] == b[0] && (a[1] > b[1] || (a[1] == b[1] && a[2] > b[2])));
            return swap ? new[] { b[0], b[1], b[2], a[0], a[1], a[2] } : new[] { a[0], a[1], a[2], b[0], b[1], b[2] };
        }

        /// <summary>A polyline's vertices (x, y, bulge…) in the order that sorts first (drawn backwards = the same; bulges negated).</summary>
        public static double[] PolylineNumbers(IList<double[]> verts, IList<double> bulges, bool closed)
        {
            int n = verts.Count;
            var fwd = new List<double>();
            for (int i = 0; i < n; i++) { fwd.Add(verts[i][0]); fwd.Add(verts[i][1]); fwd.Add(bulges != null && i < bulges.Count ? bulges[i] : 0); }
            if (closed) return fwd.ToArray(); // a closed loop started elsewhere is not looked for
            var back = new List<double>();
            for (int i = n - 1; i >= 0; i--)
            {
                back.Add(verts[i][0]); back.Add(verts[i][1]);
                back.Add(i > 0 && bulges != null && i - 1 < bulges.Count ? -bulges[i - 1] : 0);
            }
            for (int i = 0; i < fwd.Count; i++)
            {
                if (fwd[i] < back[i]) return fwd.ToArray();
                if (fwd[i] > back[i]) return back.ToArray();
            }
            return fwd.ToArray();
        }

        /// <summary>Indices of the extra copies: every object after the first of each key (the oldest is kept).</summary>
        /// <summary>Sửa KFG-15: one object for the copy check: its kind / layer / exact parts, and its lengths (drawing units).</summary>
        public sealed class DupItem
        {
            public string Group;
            public double[] Nums;
        }

        /// <summary>
        /// Sửa KFG-15: indices of the extra copies, each number within tol of an earlier object of the same group (not a rounding
        /// to a grid of tol, which split 1000.049 / 1000.051). Candidates are found by the cell of the first number and its two
        /// neighbours, then compared number by number; the oldest is kept.
        /// </summary>
        public static List<int> DuplicateIndices(IList<DupItem> items, double tol)
        {
            var cells = new Dictionary<(string, long), List<int>>();
            var dup = new List<int>();
            double t = tol > 0 ? tol : 1e-9;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it?.Group == null || it.Nums == null) continue;
                long c = it.Nums.Length > 0 ? (long)Math.Floor(it.Nums[0] / t) : 0;
                bool isDup = false;
                for (long k = c - 1; k <= c + 1 && !isDup; k++)
                {
                    if (!cells.TryGetValue((it.Group, k), out var list)) continue;
                    foreach (int j in list)
                    {
                        var o = items[j].Nums;
                        if (o.Length != it.Nums.Length) continue;
                        bool same = true;
                        for (int n = 0; n < o.Length && same; n++) same = Math.Abs(o[n] - it.Nums[n]) <= t * (1 + 1e-9);
                        if (same) { isDup = true; break; }
                    }
                }
                if (isDup) { dup.Add(i); continue; }
                if (!cells.TryGetValue((it.Group, c), out var own)) cells[(it.Group, c)] = own = new List<int>();
                own.Add(i);
            }
            return dup;
        }

        /// <summary>Sửa KFG-15: exact parts of a shape (angles, scales, bulges, texts) written rounded, never by the length tolerance.</summary>
        public static string Exact(params double[] v) => string.Join("/", v.Select(x => Math.Round(x, 6).ToString("R", CultureInfo.InvariantCulture)));

        public static List<int> DuplicateIndices(IList<string> keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dup = new List<int>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null) continue;
                if (!seen.Add(keys[i])) dup.Add(i);
            }
            return dup;
        }

        /// <summary>Value at share q (0..1) of the sorted values.</summary>
        public static double Percentile(List<double> sorted, double q)
        {
            if (sorted.Count == 0) return 0;
            double pos = q * (sorted.Count - 1);
            int i = (int)Math.Floor(pos);
            if (i >= sorted.Count - 1) return sorted[sorted.Count - 1];
            return sorted[i] + (sorted[i + 1] - sorted[i]) * (pos - i);
        }

        /// <summary>The value at share q of the sorted values, the rank rounded towards the middle (low: up, high: down).</summary>
        public static double RankInward(List<double> sorted, double q, bool low)
        {
            if (sorted.Count == 0) return 0;
            double pos = q * (sorted.Count - 1);
            int i = low ? (int)Math.Ceiling(pos - 1e-9) : (int)Math.Floor(pos + 1e-9);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, i))];
        }

        /// <summary>
        /// Indices of the objects far from the main part: the main part is the box of the 5 %…95 % of the centres (at least
        /// FarMinSizeMm wide); an object whose box is farther from it than FarFactor × its size is far away.
        /// </summary>
        public static List<int> FarAway(IList<(double[] min, double[] max)> boxes, double mmPerUnit)
        {
            var res = new List<int>();
            if (boxes.Count < G2Numbers.FarMinObjects) return res;
            var cx = boxes.Select(b => (b.min[0] + b.max[0]) / 2).OrderBy(v => v).ToList();
            var cy = boxes.Select(b => (b.min[1] + b.max[1]) / 2).OrderBy(v => v).ToList();
            // Test & sửa đợt G2 (G2T-1): the ranks round inwards (no interpolation towards an outlier): with 10–19 objects an
            // interpolated 95 % lay half way to the one far object, so it was never reported
            double x0 = RankInward(cx, 0.05, true), x1 = RankInward(cx, 0.95, false), y0 = RankInward(cy, 0.05, true), y1 = RankInward(cy, 0.95, false);
            double size = Math.Max(Math.Max(x1 - x0, y1 - y0), G2Numbers.FarMinSizeMm / mmPerUnit);
            double limit = G2Numbers.FarFactor * size;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                double dx = Math.Max(0, Math.Max(x0 - b.max[0], b.min[0] - x1));
                double dy = Math.Max(0, Math.Max(y0 - b.max[1], b.min[1] - y1));
                if (Math.Sqrt(dx * dx + dy * dy) > limit) res.Add(i);
            }
            return res;
        }

        /// <summary>1:n of a viewport (custom scale = paper / model, model in drawing units); 0 when it cannot be read.</summary>
        public static double ViewportDenominator(double customScale, double mmPerUnit) =>
            customScale > 0 ? mmPerUnit / customScale : 0;

        public static bool StandardViewportScale(double denominator) =>
            denominator > 0 && F2Numbers.StandardScales.Any(s => Math.Abs(denominator - s) <= G2Numbers.ViewportScaleTolerance * s);

        /// <summary>Test tổng thể G: a viewport looking straight down (plan); a 3D / perspective viewport has no print scale.</summary>
        public static bool IsPlanView(double x, double y, double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            return l < 1e-12 || Math.Abs(z) / l > 1 - 1e-6;
        }

        /// <summary>The name AutoCAD gives a new layout ("Layout1", "Layout 2", "Bố cục1").</summary>
        public static bool IsDefaultLayoutName(string name) =>
            !string.IsNullOrEmpty(name) && Regex.IsMatch(name.Trim(), @"^(Layout|Bố cục|Bo cuc)\s*[0-9]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Sửa KFG-08 (lần 2): a layout not checked as a sheet: a default name (Layout1…), nothing but viewports on it (the one
        /// AutoCAD makes when the tab is opened counts as nothing), and the user did not name it in 'sheets'.
        /// </summary>
        public static bool IsUnusedTab(string layout, int objectsOtherThanViewports, bool askedBySheets) =>
            !askedBySheets && objectsOtherThanViewports == 0 && IsDefaultLayoutName(layout);

        /// <summary>The note naming the unused tabs, so Claude can tell the user and check them when they are real sheets.</summary>
        public static string UnusedTabsNote(IEnumerable<string> tabs) =>
            $"{string.Join(", ", tabs)}: only a viewport, no title block or other object, so taken as unused tab(s) and not checked as sheets. " +
            "If one of them is a sheet to print, call check_drawing again with sheets = its name(s) to check it in full.";

        /// <summary>An "A$C…" block: what AutoCAD names a block made by Paste as Block.</summary>
        public static bool IsUnnamedBlock(string name) =>
            !string.IsNullOrEmpty(name) && Regex.IsMatch(name, @"^A\$C[0-9A-Fa-f]{8}$");

        /// <summary>A dimension text override that hides the measured value ("&lt;&gt;" keeps it; empty = no override).</summary>
        public static bool HidesMeasurement(string dimensionText) =>
            // Sửa KFG (nghi vấn G2): " " hides the number too (AutoCAD prints no text); only "" means no override
            !string.IsNullOrEmpty(dimensionText) && !dimensionText.Contains("<>");

        /// <summary>
        /// Test & sửa đợt G2 (G2T-3): an xref is "mất file" only when AutoCAD did not find its file (FileNotFound / Unresolved);
        /// one the user unloaded on purpose (Unloaded), or that is not used, is not an error.
        /// </summary>
        public static bool XrefMissing(string status) =>
            string.Equals(status, "FileNotFound", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Unresolved", StringComparison.OrdinalIgnoreCase);

        /// <summary>A layer of walls: A-WALL (and its kin) or a name with WALL / TUONG / TƯỜNG.</summary>
        public static bool IsWallLayer(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var u = name.ToUpperInvariant();
            if (u.EndsWith("-PAT", StringComparison.Ordinal) || u.EndsWith("-3D", StringComparison.Ordinal)) return false;
            return u.Contains("WALL") || u.Contains("TUONG") || u.Contains("TƯỜNG");
        }

        // ------------------------------------------------------------ fix_drawing

        /// <summary>
        /// The card token of a fix: one 16-hex token for "this fix on exactly these objects (to this layer)", so the Đồng ý
        /// of one fix never allows another one, and a drawing that changed since the card asks again.
        /// </summary>
        public static string FixToken(string fix, string layer, IEnumerable<string> handles)
        {
            var hs = handles.Select(h => h.Trim().ToUpperInvariant()).Distinct().OrderBy(h => h, StringComparer.Ordinal);
            return Fnv("fix:" + (fix ?? "").ToLowerInvariant() + ":" + (layer ?? "").Trim().ToUpperInvariant() + ":" + string.Join(",", hs));
        }

        private static string Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>The target layer of layer_zero / defpoints: given, and neither 0 nor Defpoints.</summary>
        /// <summary>Sửa KFG-18: a target layer that does not print (off, frozen, not plotted — e.g. X-ANH-GOC) would only move the problem.</summary>
        public static string TargetLayerStateError(string fix, string layer, bool off, bool frozen, bool plottable)
        {
            if (!(off || frozen || !plottable)) return null;
            var why = string.Join(", ", new[] { off ? "off" : null, frozen ? "frozen" : null, !plottable ? "not plotted" : null }.Where(x => x != null));
            return $"fix_drawing {fix}: layer '{layer}' is {why}, so the objects would still not print. Choose a layer that is on and plotted (ask the user).";
        }

        public static string TargetLayerError(string fix, string layer)
        {
            bool needs = fix == "layer_zero" || fix == "defpoints";
            if (!needs) return null;
            if (string.IsNullOrWhiteSpace(layer)) return $"fix_drawing {fix}: 'layer' is required (the layer the objects go to; ask the user which one).";
            if (layer.Trim() == "0" || string.Equals(layer.Trim(), "Defpoints", StringComparison.OrdinalIgnoreCase))
                return $"fix_drawing {fix}: 'layer' must be another layer than 0 / Defpoints.";
            return null;
        }

        /// <summary>The Excel rows of a check (header + one row per group).</summary>
        public static List<List<object>> ExcelRows(IEnumerable<Dictionary<string, object>> groups)
        {
            var rows = new List<List<object>> { new List<object> { "STT", "Nhóm", "Mức", "Số lượng", "Ví dụ (handle)", "Sửa tự động" } };
            int i = 1;
            foreach (var g in groups)
            {
                var ex = g.TryGetValue("examples", out var e) && e is IEnumerable<object> list ? string.Join(", ", list.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture))) : "";
                rows.Add(new List<object> { i++, g["name"], g["level"], g["count"], ex, g.TryGetValue("fix", out var f) && f != null ? "có" : "không" });
            }
            return rows;
        }
    }
}
