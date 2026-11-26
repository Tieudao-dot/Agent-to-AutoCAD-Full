using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F1: the user's current UCS as a rigid frame in WCS (origin + unit axes). Pure math, no AutoCAD.
    /// Every coordinate a tool takes or returns is in this frame, like the AutoCAD commands; World = identity,
    /// and then nothing is converted at all (the tools behave exactly as before F1).
    /// </summary>
    public sealed class UcsFrame
    {
        public readonly double[] O, X, Y, Z;
        /// <summary>Name of a saved UCS when known (shown in "coordinates"), else null.</summary>
        public readonly string Name;

        public static readonly UcsFrame World = new UcsFrame(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, null);

        public UcsFrame(double[] origin, double[] xAxis, double[] yAxis, string name = null)
        {
            O = V(origin);
            X = Unit(V(xAxis));
            // y made perpendicular to x (like the UCS command), z = x × y
            var y = V(yAxis);
            double d = Dot(y, X);
            Y = Unit(new[] { y[0] - d * X[0], y[1] - d * X[1], y[2] - d * X[2] });
            Z = Unit(Cross(X, Y));
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        public const double Tol = 1e-9;

        /// <summary>World: origin 0 and the WCS axes (within 1e-9); no conversion happens.</summary>
        public bool IsWorld =>
            Near(O, 0, 0, 0, 1e-9) && Near(X, 1, 0, 0, Tol) && Near(Y, 0, 1, 0, Tol);

        /// <summary>The UCS z axis is the WCS z axis (only moved and / or turned in plan): angles and z heights carry over.</summary>
        public bool ZUp => Near(Z, 0, 0, 1, 1e-9);

        /// <summary>Angle of the UCS x axis in the WCS XY plane, degrees (meaningful when ZUp).</summary>
        public double PlanAngleDeg => Math.Atan2(X[1], X[0]) * 180.0 / Math.PI;

        // ------------------------------------------------------------ conversions

        public double[] ToWcs(double[] p)
        {
            var q = V(p);
            return new[]
            {
                O[0] + q[0] * X[0] + q[1] * Y[0] + q[2] * Z[0],
                O[1] + q[0] * X[1] + q[1] * Y[1] + q[2] * Z[1],
                O[2] + q[0] * X[2] + q[1] * Y[2] + q[2] * Z[2],
            };
        }

        public double[] VecToWcs(double[] v)
        {
            var q = V(v);
            return new[]
            {
                q[0] * X[0] + q[1] * Y[0] + q[2] * Z[0],
                q[0] * X[1] + q[1] * Y[1] + q[2] * Z[1],
                q[0] * X[2] + q[1] * Y[2] + q[2] * Z[2],
            };
        }

        public double[] FromWcs(double[] p)
        {
            var q = V(p);
            var d = new[] { q[0] - O[0], q[1] - O[1], q[2] - O[2] };
            return new[] { Dot(d, X), Dot(d, Y), Dot(d, Z) };
        }

        public double[] VecFromWcs(double[] v)
        {
            var q = V(v);
            return new[] { Dot(q, X), Dot(q, Y), Dot(q, Z) };
        }

        /// <summary>Row-major 4x4 UCS → WCS matrix (Matrix3d takes it as is).</summary>
        public double[] ToWcsMatrix() => new[]
        {
            X[0], Y[0], Z[0], O[0],
            X[1], Y[1], Z[1], O[1],
            X[2], Y[2], Z[2], O[2],
            0, 0, 0, 1,
        };

        /// <summary>The 4 corners (WCS) of the window whose opposite corners a, b (WCS) are in this UCS: a rectangle along the UCS axes.</summary>
        public List<double[]> WindowCorners(double[] aWcs, double[] bWcs)
        {
            var a = FromWcs(aWcs);
            var b = FromWcs(bWcs);
            return new List<double[]>
            {
                ToWcs(new[] { a[0], a[1], a[2] }), ToWcs(new[] { b[0], a[1], a[2] }),
                ToWcs(new[] { b[0], b[1], a[2] }), ToWcs(new[] { a[0], b[1], a[2] }),
            };
        }

        /// <summary>Box in WCS → the box in this UCS that holds it (its 8 corners; exact when the axes only turn in 90° steps).</summary>
        public (double[] min, double[] max) BoxFromWcs(double[] wmin, double[] wmax)
        {
            var mn = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var mx = new[] { double.MinValue, double.MinValue, double.MinValue };
            var a = V(wmin);
            var b = V(wmax);
            for (int i = 0; i < 8; i++)
            {
                var c = FromWcs(new[] { (i & 1) == 0 ? a[0] : b[0], (i & 2) == 0 ? a[1] : b[1], (i & 4) == 0 ? a[2] : b[2] });
                for (int k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], c[k]); mx[k] = Math.Max(mx[k], c[k]); }
            }
            return (mn, mx);
        }

        /// <summary>
        /// The WCS box wmin / wmax lies (in x, y) inside the UCS-aligned area umin / umax (UCS numbers, any order): the area
        /// filter of build_3d_from_plan in a UCS (Test & sửa đợt F1).
        /// </summary>
        public bool PlanBoxInside(double[] wmin, double[] wmax, double[] umin, double[] umax)
        {
            var (bmin, bmax) = BoxFromWcs(wmin, wmax);
            return UcsBoxInside(bmin, bmax, umin, umax);
        }

        /// <summary>
        /// KTF-03: the box of a set of WCS points in this UCS — tight, unlike BoxFromWcs of their WCS box, which grows when
        /// the UCS turns by an angle that is not a multiple of 90° (a 100 long line along the UCS x of a 30° UCS has a UCS box
        /// 100 × 0, its WCS box re-boxed is 136.6 × 86.6).
        /// </summary>
        public (double[] min, double[] max) BoxOfPoints(IEnumerable<double[]> wcsPoints)
        {
            var mn = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var mx = new[] { double.MinValue, double.MinValue, double.MinValue };
            bool any = false;
            foreach (var w in wcsPoints)
            {
                var c = FromWcs(w);
                for (int k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], c[k]); mx[k] = Math.Max(mx[k], c[k]); }
                any = true;
            }
            if (!any) throw new ArgumentException("no points");
            return (mn, mx);
        }

        /// <summary>A box already in UCS numbers (bmin / bmax) lies, in x and y, inside the UCS area umin / umax (any order).</summary>
        public static bool UcsBoxInside(double[] bmin, double[] bmax, double[] umin, double[] umax)
        {
            double x0 = Math.Min(umin[0], umax[0]), x1 = Math.Max(umin[0], umax[0]);
            double y0 = Math.Min(umin[1], umax[1]), y1 = Math.Max(umin[1], umax[1]);
            const double eps = 1e-6;
            return bmin[0] >= x0 - eps && bmin[1] >= y0 - eps && bmax[0] <= x1 + eps && bmax[1] <= y1 + eps;
        }

        // ------------------------------------------------------------ JSON (macros)

        /// <summary>{"origin":[..],"x_axis":[..],"y_axis":[..]} (WCS) — stored with macro calls recorded in a UCS.</summary>
        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>
            {
                ["origin"] = O.Select(R).Cast<object>().ToList(),
                ["x_axis"] = X.Select(R9).Cast<object>().ToList(),
                ["y_axis"] = Y.Select(R9).Cast<object>().ToList(),
            };
            if (Name != null) d["name"] = Name;
            return d;
        }

        /// <summary>"world" or the object of ToJson; null when the value is not a frame.</summary>
        public static UcsFrame FromJson(object v)
        {
            if (v is string s) return s.Trim().Equals("world", StringComparison.OrdinalIgnoreCase) ? World : null;
            if (!(v is Dictionary<string, object> d)) return null;
            if (!d.TryGetValue("origin", out var o) || !Params.TryPoint(o, out var origin)) return null;
            if (!d.TryGetValue("x_axis", out var xv) || !Params.TryPoint(xv, out var x)) return null;
            if (!d.TryGetValue("y_axis", out var yv) || !Params.TryPoint(yv, out var y)) return null;
            if (Len(x) < 1e-12 || Len(y) < 1e-12 || Len(Cross(Unit(x), Unit(y))) < 1e-9) return null;
            return new UcsFrame(origin, x, y, d.TryGetValue("name", out var n) ? n as string : null);
        }

        /// <summary>What results say about their coordinates (only when not World).</summary>
        public string Describe() =>
            Name != null ? $"UCS \"{Name}\"" : "the current UCS";

        // ------------------------------------------------------------ math

        private static double R(double d) => Math.Round(d, 6);
        private static double R9(double d) => Math.Round(d, 12);

        private static double[] V(double[] p) =>
            p == null ? new double[3] : new[] { p.Length > 0 ? p[0] : 0, p.Length > 1 ? p[1] : 0, p.Length > 2 ? p[2] : 0 };

        internal static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        internal static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0],
        };

        private static double Len(double[] a) => Math.Sqrt(Dot(V(a), V(a)));

        private static double[] Unit(double[] a)
        {
            double l = Len(a);
            if (l < 1e-12) throw ToolException.Invalid("UCS axis has zero length.");
            return new[] { a[0] / l, a[1] / l, a[2] / l };
        }

        private static bool Near(double[] a, double x, double y, double z, double tol) =>
            Math.Abs(a[0] - x) <= tol && Math.Abs(a[1] - y) <= tol && Math.Abs(a[2] - z) <= tol;
    }

    /// <summary>How a tool meets the UCS (UcsLogic.ModeOf).</summary>
    public enum UcsMode
    {
        /// <summary>Takes no coordinates or only WCS ones on purpose (set_ucs, layouts / paper space, views): untouched.</summary>
        Wcs,
        /// <summary>
        /// Builds new objects from coordinates alone: the handler runs as if the UCS were World and the new model-space
        /// objects are then carried into the UCS by one transform (planes, angles, rotations follow the UCS for free).
        /// </summary>
        Create,
        /// <summary>
        /// Works on objects that already exist (move, query, build_3d_from_plan...): point params are converted UCS → WCS
        /// as they are read (Params.Point), points in the result WCS → UCS (Acad.J), boxes are re-boxed in the UCS.
        /// </summary>
        Ref,
    }

    /// <summary>Ambient UCS of the tool call running on this thread (set by the dispatcher, read by Params / Acad).</summary>
    public static class UcsScope
    {
        [ThreadStatic] private static UcsFrame _input;
        [ThreadStatic] private static UcsFrame _output;
        [ThreadStatic] private static string _tool;
        // KTF-04: mm per drawing unit of the running call (the Height params are mm, the UCS origin is drawing units); 0 = 1
        [ThreadStatic] private static double _mmPerUnit;

        /// <summary>mm per drawing unit of the running call (1 when unknown).</summary>
        public static double MmPerUnit => _mmPerUnit > 0 ? _mmPerUnit : 1;

        /// <summary>Frame point params are converted from (Ref mode and not World), else null.</summary>
        public static UcsFrame Input => _input;
        /// <summary>Frame result points are converted to (Ref mode and not World), else null.</summary>
        public static UcsFrame Output => _output;
        public static string Tool => _tool;

        /// <summary>Sets the ambient frame for one Ref call; World / null and other modes leave everything as is.</summary>
        public static IDisposable Enter(UcsFrame frame, UcsMode mode, string tool, double mmPerUnit = 1)
        {
            var prev = (_input, _output, _tool, _mmPerUnit);
            bool active = frame != null && !frame.IsWorld && mode == UcsMode.Ref;
            _input = active ? frame : null;
            _output = active ? frame : null;
            _tool = tool;
            _mmPerUnit = mmPerUnit > 0 ? mmPerUnit : 1;
            return new Restore(prev);
        }

        /// <summary>Points read in the middle of a call that are already WCS (e.g. picked by the user, stored in a macro).</summary>
        public static IDisposable Suspend()
        {
            var prev = (_input, _output, _tool, _mmPerUnit);
            _input = null;
            _output = null;
            return new Restore(prev);
        }

        private sealed class Restore : IDisposable
        {
            private readonly (UcsFrame, UcsFrame, string, double) _prev;
            private bool _done;
            public Restore((UcsFrame, UcsFrame, string, double) prev) { _prev = prev; }
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                (_input, _output, _tool, _mmPerUnit) = _prev;
            }
        }

        /// <summary>Params.Point hook: converts one named point param of the running call.</summary>
        public static double[] ConvertInput(string name, double[] pt)
        {
            var f = _input;
            if (f == null || pt == null) return pt;
            switch (UcsLogic.KindOf(_tool, name))
            {
                case ParamKind.Vector: return f.VecToWcs(pt);
                case ParamKind.Raw: return pt;
                default: return f.ToWcs(pt);
            }
        }

        /// <summary>Params.Num hook: an absolute plan angle (deg) or an absolute z height of the running call.</summary>
        public static double ConvertNumber(string name, double v)
        {
            var f = _input;
            if (f == null || !f.ZUp) return v;
            switch (UcsLogic.KindOf(_tool, name))
            {
                case ParamKind.PlanAngle: return v + f.PlanAngleDeg;
                // KTF-04: the height is mm, the UCS origin z drawing units (a metre drawing: origin z 3.3 = 3300 mm)
                case ParamKind.Height: return v + f.O[2] * MmPerUnit;
                default: return v;
            }
        }
    }

    public enum ParamKind { Point, Vector, Raw, PlanAngle, Height, Other }

    /// <summary>Which tools build from coordinates (Create), which work on existing objects (Ref) and which stay WCS.</summary>
    public static class UcsLogic
    {
        /// <summary>
        /// Built purely from coordinates: run in the UCS as if it were World, then the new objects are moved into the UCS.
        /// A tool here must not read the geometry of existing objects (that geometry is WCS).
        /// </summary>
        public static readonly HashSet<string> CreateTools = new HashSet<string>(StringComparer.Ordinal)
        {
            "draw_line", "draw_polyline", "draw_circle", "draw_arc", "draw_ellipse", "draw_spline", "draw_rectangle",
            "draw_polygon", "draw_point", "draw_xline", "draw_ray", "draw_wipeout", "draw_revcloud",
            "add_text", "add_mtext", "add_dim_linear", "add_dim_aligned", "add_dim_angular", "add_dim_ordinate",
            "add_mleader", "add_table", "insert_block", "attach_xref",
            "draw_solid", "draw_box", "draw_cylinder", "draw_sphere", "draw_wedge", "draw_torus", "draw_pyramid",
            "build_railing", "build_beam", "build_stair", "build_roof",
            // nâng cấp đợt F2
            "insert_library_block",
        };

        /// <summary>Keep WCS on purpose: the UCS itself, views, layouts / paper space, files, settings.</summary>
        public static readonly HashSet<string> WcsTools = new HashSet<string>(StringComparer.Ordinal)
        {
            // set_view_3d is Ref: its custom direction is a UCS vector (its presets stay WCS views, like VIEW's defaults)
            "set_ucs", "reset_ucs", "set_visual_style", "capture_view", "zoom_extents", "regen",
            "save_drawing", "purge_unused", "undo_last_turn", "batch_execute", "ping",
            "list_layouts", "create_layout", "set_current_layout", "delete_layout", "add_viewport", "set_viewport",
            "plot_pdf", "list_plot_media",
            "create_layer", "list_layers", "set_current_layer", "set_layer_props",
            "create_text_style", "create_dim_style", "set_current_styles", "list_styles",
            "list_xrefs", "reload_xref", "detach_xref", "bind_xref", "export_stl",
            // nâng cấp đợt F2 (create_sheet is Ref: its view_center is a UCS point, the viewport turns with a turned UCS)
            "setup_standards", "layer_state",
            // nâng cấp đợt F3: no coordinates (materials; views are saved / shown as they are)
            "set_material", "named_view",
            // nâng cấp đợt H3: both drawings are compared in their own WCS, the clouds are drawn where the objects are
            "compare_drawings",
        };

        /// <summary>Params that are directions / displacements (turned, not moved) in Ref mode, any tool.</summary>
        public static readonly HashSet<string> VectorParams = new HashSet<string>(StringComparer.Ordinal)
        {
            "displacement", "displacements", "direction", "normal", "x_axis", "y_axis",
        };

        /// <summary>Point-shaped params that are not coordinates (scale factors x, y, z).</summary>
        public static readonly HashSet<string> RawParams = new HashSet<string>(StringComparer.Ordinal) { "scale" };

        /// <summary>Absolute plan angles (deg from the x axis) of Ref tools: + the UCS turn when the UCS z is up.</summary>
        public static readonly Dictionary<string, string[]> PlanAngleParams = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["draw_hatch"] = new[] { "angle" },
            ["edit_text"] = new[] { "rotation" },
        };

        /// <summary>
        /// Absolute z heights in mm of Ref tools: + the UCS origin z (× mm per unit, KTF-04) when the UCS z is up. KTF-05: also
        /// the Ref forms of build_railing (path_handle), build_stair (well_handle), build_beam (path_handles); their Create
        /// forms get the UCS z from the transform of the new solids instead.
        /// </summary>
        public static readonly Dictionary<string, string[]> HeightParams = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["build_3d_from_plan"] = new[] { "base_z" },
            ["build_roof"] = new[] { "wall_top_z" },
            ["build_railing"] = new[] { "base_z" },
            ["build_stair"] = new[] { "base_z" },
            ["build_beam"] = new[] { "top_z" },
        };

        /// <summary>The mode of one call (a few tools depend on their params: from existing objects → Ref).</summary>
        public static UcsMode ModeOf(string tool, IDictionary<string, object> p)
        {
            if (string.IsNullOrEmpty(tool) || WcsTools.Contains(tool)) return UcsMode.Wcs;
            if (!CreateTools.Contains(tool)) return UcsMode.Ref;
            bool Has(string k) => p != null && p.TryGetValue(k, out var v) && v != null && !(v is bool b && !b);
            // built from (or measured against) objects that already exist, whose geometry is WCS: Ref
            switch (tool)
            {
                // the roof finds the wall top from the wall solids unless wall_top_z is given
                case "build_roof": return Has("outline_points") && Has("wall_top_z") && !Has("from_walls") && !Has("outline_handle") ? UcsMode.Create : UcsMode.Ref;
                case "build_stair": return Has("well_handle") ? UcsMode.Ref : UcsMode.Create;
                case "build_beam": return Has("path_handles") ? UcsMode.Ref : UcsMode.Create;
                case "build_railing": return Has("path_handle") ? UcsMode.Ref : UcsMode.Create;
                case "add_dim_angular": return Has("line1_handle") || Has("line2_handle") ? UcsMode.Ref : UcsMode.Create;
            }
            return UcsMode.Create;
        }

        public static ParamKind KindOf(string tool, string name)
        {
            if (string.IsNullOrEmpty(name)) return ParamKind.Other;
            if (RawParams.Contains(name)) return ParamKind.Raw;
            if (VectorParams.Contains(name)) return ParamKind.Vector;
            if (tool != null && PlanAngleParams.TryGetValue(tool, out var a) && a.Contains(name)) return ParamKind.PlanAngle;
            if (tool != null && HeightParams.TryGetValue(tool, out var h) && h.Contains(name)) return ParamKind.Height;
            return ParamKind.Point;
        }

        /// <summary>Key of the UCS stored with a macro call recorded in a UCS ("world" for a replay of an older macro).</summary>
        public const string MacroKey = "_ucs";

        /// <summary>Params of a call to record in a macro: + the UCS when it is not World (World: the very same dict as before F1).</summary>
        public static Dictionary<string, object> ForMacro(Dictionary<string, object> p, UcsFrame frame)
        {
            if (frame == null || frame.IsWorld || p == null) return p;
            var copy = new Dictionary<string, object>(p) { [MacroKey] = frame.ToJson() };
            return copy;
        }

        /// <summary>
        /// Takes the "_ucs" of a macro call out of its params: the frame the call was recorded in. A macro call without one was
        /// recorded in World (every macro before F1): the replay passes "world" for it.
        /// </summary>
        public static UcsFrame TakeMacroFrame(Dictionary<string, object> p)
        {
            if (p == null || !p.TryGetValue(MacroKey, out var v)) return null;
            p.Remove(MacroKey);
            return UcsFrame.FromJson(v) ?? throw ToolException.Invalid($"'{MacroKey}' must be \"world\" or {{origin, x_axis, y_axis}}.");
        }

        /// <summary>Macro replay: every recorded call without a stored UCS was recorded in World (keeps old macros exact).</summary>
        public static Dictionary<string, object> MarkReplay(Dictionary<string, object> p)
        {
            var copy = p == null ? new Dictionary<string, object>() : new Dictionary<string, object>(p);
            if (!copy.ContainsKey(MacroKey)) copy[MacroKey] = "world";
            return copy;
        }

        /// <summary>
        /// Ref results: every {min, max} pair of points (bounding boxes), already turned point by point into the UCS, is
        /// re-boxed so it is the UCS-aligned box of the WCS box again. The UCS of the result is named in "coordinates".
        /// </summary>
        public static object FixResult(object result, UcsFrame frame)
        {
            if (frame == null || frame.IsWorld) return result;
            Walk(result, frame);
            if (result is Dictionary<string, object> d && !d.ContainsKey("coordinates"))
                d["coordinates"] = CoordinatesNote(frame);
            return result;
        }

        public static string CoordinatesNote(UcsFrame frame) =>
            $"All points here are in {frame.Describe()} (not WCS), the same system you give points in.";

        private static void Walk(object v, UcsFrame f)
        {
            if (v is Dictionary<string, object> d)
            {
                foreach (var (a, b) in new[] { ("min", "max"), ("plan_min", "plan_max") })
                {
                    if (d.TryGetValue(a, out var mn) && d.TryGetValue(b, out var mx)
                        && Params.TryPoint(mn, out var pmin) && Params.TryPoint(mx, out var pmax))
                    {
                        // back to the WCS corners, then the box of all 8 corners in the UCS
                        var (bmin, bmax) = f.BoxFromWcs(Min(f.ToWcs(pmin), f.ToWcs(pmax)), Max(f.ToWcs(pmin), f.ToWcs(pmax)));
                        bool three = (mn as List<object>)?.Count == 3 || (mx as List<object>)?.Count == 3 || Math.Abs(bmin[2]) > 1e-9 || Math.Abs(bmax[2]) > 1e-9;
                        d[a] = Json(bmin, three);
                        d[b] = Json(bmax, three);
                    }
                }
                foreach (var k in d.Keys.ToList()) Walk(d[k], f);
            }
            else if (v is List<object> l)
            {
                foreach (var x in l) Walk(x, f);
            }
        }

        private static double[] Min(double[] a, double[] b) => new[] { Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Min(a[2], b[2]) };
        private static double[] Max(double[] a, double[] b) => new[] { Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), Math.Max(a[2], b[2]) };

        private static List<object> Json(double[] p, bool three) =>
            three ? new List<object> { Math.Round(p[0], 6), Math.Round(p[1], 6), Math.Round(p[2], 6) }
                  : new List<object> { Math.Round(p[0], 6), Math.Round(p[1], 6) };

        /// <summary>WCS point → what a result shows (Acad.J hook); unchanged without an output frame.</summary>
        public static double[] OutPoint(double[] wcs) => UcsScope.Output == null ? wcs : UcsScope.Output.FromWcs(wcs);

        public static double[] OutVector(double[] wcs) => UcsScope.Output == null ? wcs : UcsScope.Output.VecFromWcs(wcs);
    }
}
