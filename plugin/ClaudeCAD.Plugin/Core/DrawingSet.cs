using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt G1: the numbers and words of a drawing set the user meets, one place each
    /// (docs/ban-giao/nang-cap-g1.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class G1Numbers
    {
        /// <summary>Sheet numbers (= layout names): KT-01, KT-02… in the order plans, elevations, sections, perspective.</summary>
        public const string SheetPrefix = "KT";
        public const int SheetDigits = 2;
        /// <summary>Smallest scale denominator of the plans, elevations and sections (a larger one when the view does not fit).</summary>
        public const int DefaultScale = 100;
        /// <summary>Height of the horizontal cut of a plan above its floor (mm), as usual for architectural plans.</summary>
        public const double PlanCutHeightMm = 1200;
        /// <summary>Gap on paper (mm) between two views drawn side by side in model space (each is a sheet's viewport).</summary>
        public const double ViewGapPaperMm = 30;
        /// <summary>Part of the viewport the perspective fills (the rest is margin).</summary>
        public const double PerspectiveFill = 0.85;
        /// <summary>Most sheets of one set; most storeys read from the 3D model.</summary>
        public const int MaxSheets = 30;
        public const int MaxLevels = 10;
        /// <summary>The PDF of the whole set next to the drawing: &lt;drawing&gt;-bo-ho-so.pdf.</summary>
        public const string PdfSuffix = "-bo-ho-so";
        /// <summary>The title block scale field of the perspective (no scale).</summary>
        public const string NoScaleText = "—";
        /// <summary>Visual style of the perspective viewport (printed as displayed); missing in the drawing: as AutoCAD has it.</summary>
        public const string PerspectiveVisualStyle = "Realistic";

        public const string TitlePlan = "MẶT BẰNG", TitlePlanStorey = "MẶT BẰNG TẦNG {0}";
        public const string TitleSection = "MẶT CẮT {0}-{0}";
        public const string TitlePerspective = "PHỐI CẢNH";

        public static readonly IReadOnlyDictionary<string, string> ElevationTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["front"] = "MẶT ĐỨNG TRƯỚC", ["back"] = "MẶT ĐỨNG SAU", ["left"] = "MẶT ĐỨNG BÊN TRÁI", ["right"] = "MẶT ĐỨNG BÊN PHẢI",
        };
    }

    public enum SheetKind { Plan, Elevation, Section, Perspective }

    /// <summary>One sheet of the set, as planned (AutoCAD-free).</summary>
    public sealed class SetSheet
    {
        public SheetKind Kind;
        public string Number = "";
        public string Title = "";
        /// <summary>Plan: storey index (0-based) of a 3D cut, or -1 for a 2D area. Elevation: front / back / left / right.</summary>
        public int Level = -1;
        public double LevelZ;
        public string Direction;
        /// <summary>Section line in the UCS plan (start, end); 2D plan area (min, max) in the UCS.</summary>
        public double[] A, B;
        /// <summary>Estimated size of the view (drawing units) and its scale denominator (0 = perspective: fitted).</summary>
        public double W, H;
        public int Scale;
    }

    /// <summary>What a set can have on this machine and drawing, and why the rest is missing.</summary>
    public sealed class SetAbilities
    {
        public bool Sheets, Views3d, Pdf;
        public bool Solids, Content2d;
    }

    /// <summary>
    /// Nâng cấp đợt G1: "ra bộ hồ sơ" — plans, elevations, sections and a perspective of the model, one view per sheet with
    /// the F2 title block, in one PDF if asked. The plugin (Drawing/DrawingSetTools.cs) only runs this plan with the tools
    /// that already exist (create_sheet, section_view, projection_view, plot_pdf), each under its own plan group.
    /// </summary>
    public static class DrawingSetLogic
    {
        public const string Tool = "create_drawing_set";
        public static readonly string[] Kinds = { "plan", "elevation", "section", "perspective" };
        public static readonly string[] Elevations = { "front", "back", "left", "right" };

        /// <summary>The tools whose plan group each part needs: the set never does more than those tools may.</summary>
        public const string SheetTool = "create_sheet", ViewTool = "projection_view", CutTool = "section_view", PdfTool = "plot_pdf";

        /// <summary>Named object dictionary entry that remembers the last set (its layouts and the views it drew).</summary>
        public const string RecordKey = "CLAUDECAD_DRAWING_SET";

        public static SetAbilities Abilities(string plan, bool solids, bool content2d) => new SetAbilities
        {
            Sheets = PlanLogic.Allowed(plan, SheetTool),
            Views3d = PlanLogic.Allowed(plan, ViewTool) && PlanLogic.Allowed(plan, CutTool),
            Pdf = PlanLogic.Allowed(plan, PdfTool),
            Solids = solids,
            Content2d = content2d,
        };

        /// <summary>Why a kind cannot be made here (null = it can). workMode: the F1 mode limiting the chat, or null.</summary>
        public static string Missing(string kind, SetAbilities a, string workMode, string plan)
        {
            string need3d = workMode != null ? "needs the 3D part: switch the mode to \"Cả hai\" (or 3D)" : $"needs the 3D plan (this machine has the {PlanLogic.Label(plan)} plan)";
            switch (kind)
            {
                case "plan":
                    if (a.Solids && a.Views3d) return null;
                    if (a.Content2d && a.Sheets) return null;
                    if (!a.Content2d && a.Solids) return need3d;
                    return a.Content2d ? null : "the drawing has neither a 2D plan nor 3D solids";
                default:
                    if (!a.Solids) return "the drawing has no 3D solids (build them with build_3d_from_plan first)";
                    if (!a.Views3d) return need3d;
                    if (kind == "perspective" && !a.Sheets) return SheetsMissing(workMode, plan);
                    return null;
            }
        }

        public static string SheetsMissing(string workMode, string plan) => workMode != null
            ? "putting the views on sheets with a title block needs the 2D part: switch the mode to \"Cả hai\" (or 2D)"
            : $"putting the views on sheets with a title block and the PDF need the 2D plan (this machine has the {PlanLogic.Label(plan)} plan)";

        /// <summary>Storey levels: the lowest z of each group of wall bottoms closer than tol (sorted, at most MaxLevels).</summary>
        public static List<double> Levels(IEnumerable<double> bottoms, double tol)
        {
            var z = bottoms.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
            var outl = new List<double>();
            foreach (var v in z)
                if (outl.Count == 0 || v - outl[outl.Count - 1] > tol) outl.Add(v);
            return outl.Take(G1Numbers.MaxLevels).ToList();
        }

        /// <summary>Internal: wall bottoms closer than this (mm) are one storey; a wall piece lower than LevelMinWallMm (a lintel
        /// kept apart when AutoCAD refused the union) starts no storey.</summary>
        public const double LevelTolMm = 50, LevelMinWallMm = 1500;

        /// <summary>Storey levels from the wall solids (bottom z, height, in drawing units).</summary>
        public static List<double> StoreyLevels(IEnumerable<(double bottom, double height)> walls, double mmPerUnit)
        {
            var f = mmPerUnit > 0 ? mmPerUnit : 1;
            return Levels(walls.Where(w => w.height * f >= LevelMinWallMm).Select(w => w.bottom), LevelTolMm / f);
        }

        /// <summary>Test & sửa G1: how many storeys the walls have before the MaxLevels cut (to say when some are left out).</summary>
        public static int StoreyCount(IEnumerable<(double bottom, double height)> walls, double mmPerUnit)
        {
            var f = mmPerUnit > 0 ? mmPerUnit : 1;
            var z = walls.Where(w => w.height * f >= LevelMinWallMm).Select(w => w.bottom).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
            int n = 0;
            double lastZ = double.NegativeInfinity;
            foreach (var v in z) if (n == 0 || v - lastZ > LevelTolMm / f) { n++; lastZ = v; }
            return n;
        }

        /// <summary>A wall solid by its layer (A-WALL-3D of build_3d_from_plan, or a name with WALL / TUONG).</summary>
        public static bool IsWallLayer(string layer)
        {
            var s = (layer ?? "").ToUpperInvariant();
            return s.Contains("WALL") || s.Contains("TUONG") || s.Contains("TƯỜNG");
        }

        public static string SheetNumber(string prefix, int i) =>
            prefix + "-" + i.ToString(new string('0', G1Numbers.SheetDigits), CultureInfo.InvariantCulture);

        /// <summary>A layout-name prefix: letters, digits, '_' (1–20 characters).</summary>
        public static string CheckPrefix(string prefix)
        {
            var s = (prefix ?? G1Numbers.SheetPrefix).Trim();
            if (s.Length == 0 || s.Length > 20 || !s.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                throw ToolException.Invalid($"{Tool}: prefix must be 1–20 letters / digits (e.g. \"KT\").");
            return s;
        }

        public static List<string> CheckKinds(IList<string> kinds)
        {
            if (kinds == null) return Kinds.ToList();
            var outl = new List<string>();
            foreach (var k in kinds)
            {
                var s = (k ?? "").Trim().ToLowerInvariant();
                if (!Kinds.Contains(s)) throw ToolException.Invalid($"{Tool}: unknown kind '{k}'. Kinds: {string.Join(", ", Kinds)}.");
                if (!outl.Contains(s)) outl.Add(s);
            }
            if (outl.Count == 0) throw ToolException.Invalid($"{Tool}: name at least one kind ({string.Join(", ", Kinds)}).");
            return outl;
        }

        public static List<string> CheckElevations(IList<string> dirs)
        {
            if (dirs == null) return Elevations.ToList();
            var outl = new List<string>();
            foreach (var d in dirs)
            {
                var s = (d ?? "").Trim().ToLowerInvariant();
                if (!Elevations.Contains(s)) throw ToolException.Invalid($"{Tool}: unknown elevation '{d}'. Use {string.Join(", ", Elevations)}.");
                if (!outl.Contains(s)) outl.Add(s);
            }
            return outl;
        }

        /// <summary>Direction from the model to the viewer of an elevation, in the UCS (front: the viewer at −Y, looking +Y).</summary>
        public static double[] ElevationDirection(string dir)
        {
            switch (dir)
            {
                case "back": return new double[] { 0, 1, 0 };
                case "left": return new double[] { -1, 0, 0 };
                case "right": return new double[] { 1, 0, 0 };
                default: return new double[] { 0, -1, 0 };
            }
        }

        /// <summary>The perspective (south-west iso, as AutoCAD's SW Isometric) in the UCS.</summary>
        public static readonly double[] PerspectiveDirection = { -1, -1, 1 };

        /// <summary>
        /// Default sections through the middle of the model box (UCS): A-A along the long side, B-B across it; each line
        /// runs a little past the box so it crosses every wall.
        /// </summary>
        public static List<(double[] a, double[] b)> DefaultSections(double[] min, double[] max)
        {
            double cx = (min[0] + max[0]) / 2, cy = (min[1] + max[1]) / 2, w = max[0] - min[0], h = max[1] - min[1];
            double m = 0.05 * Math.Max(w, h) + 1e-6;
            var alongX = (new[] { min[0] - m, cy }, new[] { max[0] + m, cy });
            var alongY = (new[] { cx, min[1] - m }, new[] { cx, max[1] + m });
            return w >= h ? new List<(double[], double[])> { alongX, alongY } : new List<(double[], double[])> { alongY, alongX };
        }

        /// <summary>Letters of the sections: A, B, … Z, then A1, B1…</summary>
        public static string SectionLetter(int i) => ((char)('A' + i % 26)).ToString() + (i >= 26 ? (i / 26).ToString(CultureInfo.InvariantCulture) : "");

        public sealed class Request
        {
            public List<string> Kinds;
            public List<string> Elevations;
            /// <summary>Section lines in the UCS (null = the two default ones).</summary>
            public List<(double[] a, double[] b, string name)> Sections;
            /// <summary>2D plan areas in the UCS (null = the whole 2D drawing); used when the plans come from 2D.</summary>
            public List<(double[] min, double[] max, string title)> PlanAreas;
            /// <summary>"auto", "3d" or "2d".</summary>
            public string PlanSource = "auto";
            public string Prefix = G1Numbers.SheetPrefix;
            /// <summary>Given scale denominator (0 = the default rule).</summary>
            public int Scale;
            /// <summary>The model box in the UCS (solids), the 2D drawing box in the UCS, storey levels (UCS z).</summary>
            public double[] SolidMin, SolidMax, Min2d, Max2d;
            public List<double> Levels = new List<double>();
        }

        public sealed class Plan
        {
            public List<SetSheet> Sheets = new List<SetSheet>();
            /// <summary>Kind → why it is not in the set.</summary>
            public Dictionary<string, string> Missing = new Dictionary<string, string>();
            public List<string> Notes = new List<string>();
            /// <summary>"3d" (cuts of the model) or "2d" (the 2D drawing) for the plans.</summary>
            public string PlanSource;
        }

        /// <summary>The sheets of a set (without scales: see ChooseScales).</summary>
        public static Plan Build(Request r, SetAbilities a, string workMode, string plan)
        {
            var p = new Plan();
            var kinds = CheckKinds(r.Kinds);
            var src = (r.PlanSource ?? "auto").Trim().ToLowerInvariant();
            if (src != "auto" && src != "2d" && src != "3d") throw ToolException.Invalid($"{Tool}: plan_source must be auto, 3d or 2d.");
            foreach (var k in kinds)
            {
                var why = Missing(k, a, workMode, plan);
                if (why != null) { p.Missing[k] = why; continue; }
                switch (k)
                {
                    case "plan": AddPlans(p, r, a, src, workMode, plan); break;
                    case "elevation":
                        foreach (var d in CheckElevations(r.Elevations))
                            p.Sheets.Add(new SetSheet
                            {
                                Kind = SheetKind.Elevation, Direction = d, Title = G1Numbers.ElevationTitles[d],
                                W = d == "front" || d == "back" ? r.SolidMax[0] - r.SolidMin[0] : r.SolidMax[1] - r.SolidMin[1],
                                H = r.SolidMax[2] - r.SolidMin[2],
                            });
                        break;
                    case "section":
                        var lines = r.Sections ?? DefaultSections(r.SolidMin, r.SolidMax).Select(t => (t.a, t.b, (string)null)).ToList();
                        for (int i = 0; i < lines.Count; i++)
                        {
                            var (sa, sb, name) = lines[i];
                            double dx = sb[0] - sa[0], dy = sb[1] - sa[1];
                            if (Math.Sqrt(dx * dx + dy * dy) < 1e-9) throw ToolException.Invalid($"{Tool}: section {i + 1}: the two points of its line are the same.");
                            p.Sheets.Add(new SetSheet
                            {
                                Kind = SheetKind.Section, A = sa, B = sb,
                                Title = string.IsNullOrWhiteSpace(name) ? string.Format(CultureInfo.InvariantCulture, G1Numbers.TitleSection, SectionLetter(i)) : name.Trim(),
                                W = Math.Abs(dx) >= Math.Abs(dy) ? r.SolidMax[0] - r.SolidMin[0] : r.SolidMax[1] - r.SolidMin[1],
                                H = r.SolidMax[2] - r.SolidMin[2],
                            });
                        }
                        break;
                    case "perspective":
                        p.Sheets.Add(new SetSheet { Kind = SheetKind.Perspective, Title = G1Numbers.TitlePerspective });
                        break;
                }
            }
            if (p.Sheets.Count > G1Numbers.MaxSheets)
                throw ToolException.Invalid($"{Tool}: the set would have {p.Sheets.Count} sheets (at most {G1Numbers.MaxSheets}). Nothing was made. Ask for fewer kinds, elevations or sections.");
            var prefix = CheckPrefix(r.Prefix);
            for (int i = 0; i < p.Sheets.Count; i++) p.Sheets[i].Number = SheetNumber(prefix, i + 1);
            return p;
        }

        private static void AddPlans(Plan p, Request r, SetAbilities a, string src, string workMode, string plan)
        {
            bool from3d = src == "3d" || (src == "auto" && a.Solids && a.Views3d);
            if (src == "3d" && !(a.Solids && a.Views3d)) { p.Missing["plan"] = a.Solids ? Missing("elevation", a, workMode, plan) : "plan_source 3d: the drawing has no 3D solids"; return; }
            if (src == "2d" && !(a.Content2d && a.Sheets)) { p.Missing["plan"] = a.Content2d ? SheetsMissing(workMode, plan) : "plan_source 2d: the drawing has no 2D plan"; return; }
            if (!from3d && !a.Sheets) { p.Missing["plan"] = SheetsMissing(workMode, plan); return; }
            p.PlanSource = from3d ? "3d" : "2d";
            if (from3d)
            {
                var levels = r.Levels.Count > 0 ? r.Levels : new List<double> { r.SolidMin[2] };
                for (int i = 0; i < levels.Count; i++)
                    p.Sheets.Add(new SetSheet
                    {
                        Kind = SheetKind.Plan, Level = i, LevelZ = levels[i],
                        Title = levels.Count == 1 ? G1Numbers.TitlePlan : string.Format(CultureInfo.InvariantCulture, G1Numbers.TitlePlanStorey, i + 1),
                        W = r.SolidMax[0] - r.SolidMin[0], H = r.SolidMax[1] - r.SolidMin[1],
                    });
                return;
            }
            var areas = r.PlanAreas ?? new List<(double[], double[], string)> { (r.Min2d, r.Max2d, null) };
            for (int i = 0; i < areas.Count; i++)
            {
                var (mn, mx, title) = areas[i];
                var lo = new[] { Math.Min(mn[0], mx[0]), Math.Min(mn[1], mx[1]) };
                var hi = new[] { Math.Max(mn[0], mx[0]), Math.Max(mn[1], mx[1]) };
                if (hi[0] - lo[0] < 1e-9 || hi[1] - lo[1] < 1e-9) throw ToolException.Invalid($"{Tool}: plan area {i + 1} has no size.");
                p.Sheets.Add(new SetSheet
                {
                    Kind = SheetKind.Plan, A = lo, B = hi,
                    Title = !string.IsNullOrWhiteSpace(title) ? title.Trim() : areas.Count == 1 ? G1Numbers.TitlePlan : string.Format(CultureInfo.InvariantCulture, G1Numbers.TitlePlanStorey, i + 1),
                    W = hi[0] - lo[0], H = hi[1] - lo[1],
                });
            }
        }

        /// <summary>
        /// Scales: the one given, else per kind the smallest standard scale that fits every view of that kind on the paper,
        /// never below 1:DefaultScale (so all plans share a scale, all elevations and sections another). Perspective: 0.
        /// </summary>
        public static void ChooseScales(Plan p, SheetLogic.Sheet sheet, double mmPerUnit, int given)
        {
            foreach (var g in p.Sheets.Where(s => s.Kind != SheetKind.Perspective).GroupBy(s => s.Kind == SheetKind.Plan))
            {
                int n = given;
                if (n <= 0)
                {
                    n = G1Numbers.DefaultScale;
                    foreach (var s in g) n = Math.Max(n, SheetLogic.FitScale(sheet, mmPerUnit, s.W, s.H));
                }
                foreach (var s in g) s.Scale = n;
            }
        }

        /// <summary>
        /// Test & sửa G1: the views that do not fit their viewport at the chosen scale (a scale given too small, or a model
        /// larger than the largest standard scale): the sheet then shows only part of the view, so the result says so.
        /// </summary>
        public static List<string> NotFitting(Plan p, SheetLogic.Sheet sheet, double mmPerUnit) =>
            p.Sheets.Where(s => s.Kind != SheetKind.Perspective && s.Scale > 0 && !SheetLogic.Fits(sheet, s.Scale, mmPerUnit, s.W, s.H))
                .Select(s => s.Title).ToList();

        /// <summary>
        /// Where each 2D view is drawn in model space: a row to the right of everything (startX), each view in a slot as wide
        /// as its viewport shows (plus a gap), so no neighbour shows on a sheet. Returns the lower-left corner of each view.
        /// </summary>
        public static List<double[]> Slots(IList<SetSheet> views, SheetLogic.Sheet sheet, double mmPerUnit, double startX, double y)
        {
            var outl = new List<double[]>();
            double x = startX;
            foreach (var s in views)
            {
                var area = SheetLogic.ModelArea(sheet, Math.Max(1, s.Scale), mmPerUnit);
                double gap = G1Numbers.ViewGapPaperMm * Math.Max(1, s.Scale) / mmPerUnit;
                double slot = Math.Max(area[0], s.W);
                double slotH = Math.Max(area[1], s.H);
                outl.Add(new[] { x + (slot - s.W) / 2, y + (slotH - s.H) / 2 });
                x += slot + gap;
            }
            return outl;
        }

        /// <summary>
        /// Viewport CustomScale (paper per drawing unit) that fits the box corners seen along dir in the viewport, and the
        /// size they take on the view plane (u right, v up as SectionLogic.Frame).
        /// </summary>
        public static double FitCustomScale(IEnumerable<double[]> corners, double[] dir, double vpW, double vpH)
        {
            var (u, v) = SectionLogic.Frame(dir);
            var pts = corners.ToList();
            double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
            double w = pts.Max(q => Dot(q, u)) - pts.Min(q => Dot(q, u));
            double h = pts.Max(q => Dot(q, v)) - pts.Min(q => Dot(q, v));
            if (w < 1e-9 && h < 1e-9) return 1;
            double s = Math.Min(w > 1e-9 ? vpW / w : double.MaxValue, h > 1e-9 ? vpH / h : double.MaxValue);
            return s * G1Numbers.PerspectiveFill;
        }

        /// <summary>The 8 corners of a box.</summary>
        public static List<double[]> Corners(double[] min, double[] max)
        {
            var outl = new List<double[]>();
            for (int i = 0; i < 8; i++) outl.Add(new[] { (i & 1) == 0 ? min[0] : max[0], (i & 2) == 0 ? min[1] : max[1], (i & 4) == 0 ? min[2] : max[2] });
            return outl;
        }

        // ------------------------------------------------------------------ the record of the last set

        public sealed class Record
        {
            public List<string> Layouts = new List<string>();
            public List<string> Handles = new List<string>();
        }

        public static List<string> Encode(Record r) =>
            r.Layouts.Select(l => "L:" + l).Concat(r.Handles.Select(h => "H:" + h)).ToList();

        public static Record Decode(IEnumerable<string> lines)
        {
            var r = new Record();
            foreach (var s in lines ?? Enumerable.Empty<string>())
            {
                if (s == null || s.Length < 3) continue;
                if (s.StartsWith("L:", StringComparison.Ordinal)) r.Layouts.Add(s.Substring(2));
                else if (s.StartsWith("H:", StringComparison.Ordinal)) r.Handles.Add(s.Substring(2));
            }
            return r;
        }

        /// <summary>The card token of replacing the last set (its layouts, in any order, case-free).</summary>
        public static string ReplaceToken(IEnumerable<string> layouts)
        {
            var key = "drawing-set:" + string.Join("|", layouts.Select(l => l.Trim().ToUpperInvariant()).OrderBy(l => l, StringComparer.Ordinal));
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(key)) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Test & sửa G1: the token of replacing the last set, with the views it drew: a set without sheets (a 3D machine) has
        /// no layout, so its token would be the same for every drawing and every run.
        /// </summary>
        public static string ReplaceToken(IEnumerable<string> layouts, IEnumerable<string> handles) =>
            ReplaceToken(layouts.Concat((handles ?? Enumerable.Empty<string>()).Select(h => "#" + h.Trim().ToUpperInvariant())));

        /// <summary>Test & sửa G1: the title fields the user gave that create_sheet would refuse (checked before anything changes).</summary>
        public static List<string> UnknownFields(IEnumerable<string> keys)
        {
            var known = new HashSet<string>(SheetLogic.Fields.Select(x => x.field), StringComparer.OrdinalIgnoreCase);
            return (keys ?? Enumerable.Empty<string>()).Where(k => !known.Contains(k)).ToList();
        }

        /// <summary>Layout names of the new set that exist and are not the last set's: the user's own layouts, never touched.</summary>
        public static List<string> Clashes(IEnumerable<string> wanted, IEnumerable<string> existing, Record last)
        {
            var ours = new HashSet<string>(last?.Layouts ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            return wanted.Where(w => have.Contains(w) && !ours.Contains(w)).ToList();
        }

        /// <summary>The PDF file name of the set (next to the drawing).</summary>
        public static string PdfName(string drawingName)
        {
            var n = (drawingName ?? "").Trim();
            n = n.Substring(Math.Max(n.LastIndexOf('\\'), n.LastIndexOf('/')) + 1); // a Windows path on any OS
            int dot = n.LastIndexOf('.');
            var b = dot > 0 ? n.Substring(0, dot) : n;
            if (string.IsNullOrWhiteSpace(b)) b = "ban-ve";
            return b + G1Numbers.PdfSuffix + ".pdf";
        }
    }
}
