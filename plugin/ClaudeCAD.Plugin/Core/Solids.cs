using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>A named 3D view: view direction (target → camera, WCS) and the up vector on screen.</summary>
    public sealed class ViewPreset
    {
        public string Name;
        public double[] Direction;
        public double[] Up;
    }

    /// <summary>
    /// 3D (phase 1) math that needs no AutoCAD, so it is unit-tested on CI: view presets, visual style names,
    /// parameter checks of the solid primitives, revolve axis, expected volumes (self-test) and the projected
    /// size of a 3D box in a view (zoom / capture). Points and vectors are double[] {x, y, z} in mm.
    /// </summary>
    public static class SolidLogic
    {
        public const double Eps = 1e-9;

        // ================================================================= explode / erase of older 3D solids (decided 12/11/2026)

        /// <summary>
        /// Set on explode_entities / erase_entities by the assistant (never by Claude: not in the tools' schemas, replaced in
        /// batch calls): the handles the user approved with the Đồng ý button of a confirmation card in this turn. Decided
        /// 12/11/2026: C1, erasing 3D solids from before the turn always goes through the card like exploding them; C2, only
        /// the card's button unlocks it and only for the handles on the card. A macro never records it (KT-10).
        /// </summary>
        public const string UserConfirmedParam = "user_confirmed";

        /// <summary>The tools the plugin itself checks for 3D solids from before the turn (by object type).</summary>
        public static readonly IReadOnlyList<string> CardConfirmTools = new[] { "explode_entities", "erase_entities" };

        /// <summary>
        /// AutoCAD .NET types (Autodesk.AutoCAD.DatabaseServices, base types included) that explode_entities / erase_entities
        /// break up or erase only through the card when they existed before the turn: 3D solids, ACIS bodies, surfaces
        /// (PlaneSurface, NurbSurface, ExtrudedSurface… all derive from Surface) and meshes. 2D objects (blocks, polylines,
        /// regions…) keep the usual rule (a question only above 20 objects).
        /// </summary>
        public static readonly IReadOnlyList<string> ExplodeConfirmTypes = new[] { "Solid3d", "Body", "Surface", "SubDMesh", "PolyFaceMesh", "PolygonMesh" };

        /// <summary>typeNames = the entity's type and its base types (DatabaseServices only).</summary>
        public static bool IsExplodeConfirmType(IEnumerable<string> typeNames) =>
            typeNames != null && typeNames.Any(n => ExplodeConfirmTypes.Contains(n, StringComparer.Ordinal));

        /// <summary>The approved handles user_confirmed carries (upper case); empty for anything but a list of handles.</summary>
        public static HashSet<string> ConfirmedHandles(object value)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (value is System.Collections.IEnumerable list && !(value is string))
                foreach (var x in list)
                    if (x is string h && h.Trim().Length > 0) set.Add(h.Trim().ToUpperInvariant());
            return set;
        }

        /// <summary>
        /// The handles that must not be exploded / erased yet: 3D solids / surfaces / meshes not created in this turn and not
        /// on the card the user approved. Empty = go ahead.
        /// </summary>
        public static List<string> SolidsNeedingCard(IEnumerable<(string handle, bool solidLike, bool createdThisTurn)> items, ICollection<string> confirmed)
        {
            if (items == null) return new List<string>();
            return items.Where(i => i.solidLike && !i.createdThisTurn && !(confirmed != null && confirmed.Any(c => string.Equals(c, i.handle, StringComparison.OrdinalIgnoreCase))))
                .Select(i => (i.handle ?? "").ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>The refusal Claude gets: starts with "Confirmation required" like the assistant's own gate.</summary>
        public static string CardConfirmMessage(string tool, IReadOnlyList<string> handles)
        {
            bool erase = tool == "erase_entities";
            var list = string.Join(", ", handles.Take(20)) + (handles.Count > 20 ? ", …" : "");
            return $"Confirmation required: {tool} would {(erase ? "erase" : "break up")} {handles.Count} 3D solid(s) / surface(s) / mesh(es) that were " +
                   $"already in the drawing before this turn ({list}). Nothing was {(erase ? "erased" : "exploded")}. Only the Đồng ý button of a confirmation " +
                   "card that lists these handles allows it; a typed \"ok\" / \"đồng ý\" or the plan card's \"Làm\" does not. Do not retry now and do not " +
                   "work around it (no copy-then-explode, no erase and redraw). Call request_confirmation now with exactly these handles and one " +
                   $"Vietnamese sentence saying what will change (e.g. \"{(erase ? "Xóa khối 2F4A?" : "Phá khối 2F4A thành các mặt?")}\"), and end your turn.";
        }

        // ================================================================= views

        private static readonly double[] Z = { 0, 0, 1 };
        private static readonly double[] Y = { 0, 1, 0 };

        /// <summary>The presets of set_view_3d / capture_view, like AutoCAD's ViewCube (Z up; top/bottom: +Y up).</summary>
        public static readonly IReadOnlyList<ViewPreset> Presets = new List<ViewPreset>
        {
            new ViewPreset { Name = "top", Direction = new double[] { 0, 0, 1 }, Up = Y },
            new ViewPreset { Name = "bottom", Direction = new double[] { 0, 0, -1 }, Up = Y },
            new ViewPreset { Name = "front", Direction = new double[] { 0, -1, 0 }, Up = Z },
            new ViewPreset { Name = "back", Direction = new double[] { 0, 1, 0 }, Up = Z },
            new ViewPreset { Name = "left", Direction = new double[] { -1, 0, 0 }, Up = Z },
            new ViewPreset { Name = "right", Direction = new double[] { 1, 0, 0 }, Up = Z },
            new ViewPreset { Name = "sw_iso", Direction = new double[] { -1, -1, 1 }, Up = Z },
            new ViewPreset { Name = "se_iso", Direction = new double[] { 1, -1, 1 }, Up = Z },
            new ViewPreset { Name = "ne_iso", Direction = new double[] { 1, 1, 1 }, Up = Z },
            new ViewPreset { Name = "nw_iso", Direction = new double[] { -1, 1, 1 }, Up = Z },
        };

        public static IEnumerable<string> PresetNames => Presets.Select(p => p.Name);

        private static readonly Dictionary<string, string> PresetAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = "top", ["iso"] = "sw_iso", ["side"] = "right",
            ["tren"] = "top", ["trên"] = "top", ["duoi"] = "bottom", ["dưới"] = "bottom",
            ["truoc"] = "front", ["trước"] = "front", ["sau"] = "back",
            ["trai"] = "left", ["trái"] = "left", ["phai"] = "right", ["phải"] = "right", ["ben"] = "right", ["bên"] = "right",
            ["swiso"] = "sw_iso", ["seiso"] = "se_iso", ["neiso"] = "ne_iso", ["nwiso"] = "nw_iso",
            ["southwest"] = "sw_iso", ["southeast"] = "se_iso", ["northeast"] = "ne_iso", ["northwest"] = "nw_iso",
        };

        /// <summary>A preset by name ("top", "front", "sw_iso"... or an alias such as "iso", "trước"); throws INVALID_PARAMS otherwise.</summary>
        public static ViewPreset Preset(string name, string tool = "set_view_3d")
        {
            var key = (name ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            if (PresetAliases.TryGetValue(key, out var alias)) key = alias;
            var p = Presets.FirstOrDefault(x => x.Name == key);
            if (p == null)
                throw ToolException.Invalid($"{tool}: unknown view '{name}'. Use one of: {string.Join(", ", PresetNames)}.");
            return p;
        }

        /// <summary>
        /// A custom view direction (target → camera). The up vector is +Z on screen, or +Y when looking straight
        /// down / up (the direction is parallel to Z).
        /// </summary>
        public static ViewPreset Custom(double[] direction, string tool = "set_view_3d")
        {
            var d = Unit(direction, $"{tool}: 'direction' must be a non-zero vector [x, y, z]");
            bool vertical = Math.Abs(d[0]) < 1e-6 && Math.Abs(d[1]) < 1e-6;
            return new ViewPreset { Name = "custom", Direction = d, Up = vertical ? Y : Z };
        }

        /// <summary>
        /// Screen axes of a view: right and up unit vectors perpendicular to the view direction (up = the
        /// preset's up vector made perpendicular).
        /// </summary>
        public static (double[] right, double[] up) ScreenAxes(ViewPreset v)
        {
            var d = Unit(v.Direction, "view direction is zero");
            var up = Sub(v.Up, Scale(d, Dot(v.Up, d)));
            if (Len(up) < 1e-9) up = Sub(Y, Scale(d, Dot(Y, d)));
            up = Unit(up, "view up vector is degenerate");
            var right = Cross(up, d);
            return (Unit(right, "view right vector is degenerate"), up);
        }

        /// <summary>Width and height (mm) on screen of the box min–max seen along the view, and its centre (WCS).</summary>
        public static (double width, double height, double[] center) ProjectedSize(double[] min, double[] max, ViewPreset v)
        {
            var (right, up) = ScreenAxes(v);
            double rMin = double.MaxValue, rMax = double.MinValue, uMin = double.MaxValue, uMax = double.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new[] { (i & 1) == 0 ? min[0] : max[0], (i & 2) == 0 ? min[1] : max[1], (i & 4) == 0 ? Z3(min) : Z3(max) };
                double r = Dot(c, right), u = Dot(c, up);
                rMin = Math.Min(rMin, r); rMax = Math.Max(rMax, r);
                uMin = Math.Min(uMin, u); uMax = Math.Max(uMax, u);
            }
            var center = new[] { (min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (Z3(min) + Z3(max)) / 2 };
            return (rMax - rMin, uMax - uMin, center);
        }

        // ================================================================= visual styles

        /// <summary>Parameter value → name of the visual style in the drawing's dictionary.</summary>
        private static readonly (string key, string acad)[] Styles =
        {
            ("2d_wireframe", "2dWireframe"),
            ("wireframe", "Wireframe"),
            ("hidden", "Hidden"),
            ("conceptual", "Conceptual"),
            ("shaded", "Shaded"),
            ("shaded_with_edges", "Shaded with edges"),
            ("realistic", "Realistic"),
            ("xray", "X-Ray"),
        };

        public static IEnumerable<string> VisualStyleKeys => Styles.Select(s => s.key);

        /// <summary>
        /// GraphicsInterface.VisualStyleType member of each style (compared as text): the fallback when the drawing's
        /// dictionary names are not the English ones (localized AutoCAD / templates), e.g. "Conceptual" → Conceptual.
        /// </summary>
        public static string VisualStyleTypeOf(string acadName)
        {
            switch (acadName)
            {
                case "2dWireframe": return "Wireframe2D";
                case "Wireframe": return "Wireframe3D";
                case "Hidden": return "Hidden";
                case "Conceptual": return "Conceptual";
                case "Shaded": return "Shaded";
                case "Shaded with edges": return "ShadedWithEdges";
                case "Realistic": return "Realistic";
                case "X-Ray": return "XRay";
                default: return null;
            }
        }

        /// <summary>Older dictionary names of the same styles (drawings from AutoCAD 2010 and before).</summary>
        public static IEnumerable<string> VisualStyleAliases(string acadName)
        {
            yield return acadName;
            if (acadName == "Wireframe") yield return "3dWireframe";
            if (acadName == "Hidden") yield return "3dHidden";
        }

        /// <summary>
        /// Picks the drawing's visual style for acadName among (name, type, internal) entries: first by name (or an
        /// older name), then by VisualStyleType, skipping internal-use styles. Null when the drawing has none.
        /// </summary>
        public static string PickVisualStyle(IEnumerable<(string name, string type, bool internalUse)> entries, string acadName)
        {
            var list = entries.ToList();
            foreach (var alias in VisualStyleAliases(acadName))
            {
                var hit = list.FirstOrDefault(e => !e.internalUse && string.Equals(e.name, alias, StringComparison.OrdinalIgnoreCase));
                if (hit.name != null) return hit.name;
            }
            var type = VisualStyleTypeOf(acadName);
            if (type == null) return null;
            var byType = list.FirstOrDefault(e => !e.internalUse && e.type == type);
            return byType.name;
        }

        /// <summary>"conceptual" → "Conceptual", "2d wireframe" → "2dWireframe"...; throws INVALID_PARAMS for an unknown style.</summary>
        public static string VisualStyleName(string style, string tool = "set_visual_style")
        {
            var key = (style ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            if (key == "2dwireframe" || key == "2d") key = "2d_wireframe";
            if (key == "x_ray") key = "xray";
            if (key == "shaded_edges") key = "shaded_with_edges";
            foreach (var (k, acad) in Styles)
                if (k == key || acad.Equals(style?.Trim(), StringComparison.OrdinalIgnoreCase)) return acad;
            throw ToolException.Invalid($"{tool}: unknown visual style '{style}'. Use one of: {string.Join(", ", VisualStyleKeys)}.");
        }

        // ================================================================= draw_solid (nâng cấp đợt A)

        /// <summary>The shapes of draw_solid (one tool instead of draw_box … draw_pyramid, which stay as macro aliases).</summary>
        public static readonly string[] Shapes = { "box", "cylinder", "sphere", "wedge", "torus", "pyramid" };

        /// <summary>The old tool of each shape (its code builds the solid).</summary>
        public static string AliasOf(string shape) => "draw_" + NormalizeShape(shape);

        /// <summary>The parameters of each shape (besides shape and layer / color...).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> ShapeParams = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["box"] = new[] { "length", "width", "height", "corner", "center", "rotation" },
            ["cylinder"] = new[] { "base", "radius", "diameter", "top_radius", "height", "top_center" },
            ["sphere"] = new[] { "center", "radius", "diameter" },
            ["wedge"] = new[] { "length", "width", "height", "corner", "rotation" },
            ["torus"] = new[] { "center", "radius", "tube_radius" },
            ["pyramid"] = new[] { "base", "sides", "radius", "top_radius", "height", "top_center", "rotation" },
        };

        /// <summary>"box" … "pyramid" (a few everyday synonyms too); anything else is a clear error.</summary>
        public static string NormalizeShape(string shape, string tool = "draw_solid")
        {
            var s = (shape ?? "").Trim().ToLowerInvariant();
            switch (s)
            {
                case "cube": case "hop": case "hộp": s = "box"; break;
                case "cyl": case "tru": case "trụ": s = "cylinder"; break;
                case "ball": case "cau": case "cầu": s = "sphere"; break;
                case "ramp": case "nem": case "nêm": s = "wedge"; break;
                case "ring": case "xuyen": case "xuyến": s = "torus"; break;
                case "chop": case "chóp": s = "pyramid"; break;
            }
            if (Array.IndexOf(Shapes, s) < 0)
                throw ToolException.Invalid($"{tool}: 'shape' must be one of {string.Join(", ", Shapes)} (got '{shape}').");
            return s;
        }

        /// <summary>A parameter of another shape (tube_radius on a box...) is a mistake: say so instead of ignoring it.</summary>
        public static void CheckShapeParams(string shape, IEnumerable<string> keys, string tool = "draw_solid")
        {
            var own = new HashSet<string>(ShapeParams[shape], StringComparer.Ordinal);
            var other = new HashSet<string>(ShapeParams.Values.SelectMany(v => v), StringComparer.Ordinal);
            var wrong = keys.Where(k => other.Contains(k) && !own.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (wrong.Count > 0)
                throw ToolException.Invalid($"{tool}: {string.Join(", ", wrong)} {(wrong.Count == 1 ? "is not a parameter" : "are not parameters")} of shape '{shape}'. " +
                    $"A {shape} takes: {string.Join(", ", ShapeParams[shape])}.");
        }

        // ================================================================= primitives: checks

        public static void CheckBox(double length, double width, double height, string tool = "draw_box")
        {
            if (!(length > 0) || !(width > 0) || !(height > 0))
                throw ToolException.Invalid($"{tool}: length, width and height must all be > 0 mm (got {length} × {width} × {height}).");
        }

        /// <summary>Cylinder / cone / frustum: base radius > 0, top radius ≥ 0 (0 = cone), height ≠ 0.</summary>
        public static void CheckFrustum(double radius, double topRadius, double height, string tool = "draw_cylinder")
        {
            if (!(radius > 0)) throw ToolException.Invalid($"{tool}: 'radius' (base) must be > 0 mm.");
            if (topRadius < 0) throw ToolException.Invalid($"{tool}: 'top_radius' must be >= 0 (0 = cone).");
            if (!(Math.Abs(height) > Eps)) throw ToolException.Invalid($"{tool}: the height must not be 0.");
        }

        public static void CheckTorus(double majorRadius, double minorRadius, string tool = "draw_torus")
        {
            if (!(majorRadius > 0) || !(minorRadius > 0)) throw ToolException.Invalid($"{tool}: 'radius' and 'tube_radius' must be > 0 mm.");
            if (minorRadius >= majorRadius)
                throw ToolException.Invalid($"{tool}: 'tube_radius' ({minorRadius}) must be smaller than 'radius' ({majorRadius}), the distance from the centre to the tube's centre line.");
        }

        public const int MinPyramidSides = 3;
        public const int MaxPyramidSides = 32;

        public static void CheckPyramid(int sides, double radius, double topRadius, double height, string tool = "draw_pyramid")
        {
            if (sides < MinPyramidSides || sides > MaxPyramidSides)
                throw ToolException.Invalid($"{tool}: 'sides' must be between {MinPyramidSides} and {MaxPyramidSides} (got {sides}).");
            CheckFrustum(radius, topRadius, height, tool);
        }

        /// <summary>Largest |taper_angle| (degrees), the same bound as the assistant's schema.</summary>
        public const double MaxTaper = 89.9;

        /// <summary>Extrusion: height ≠ 0, taper strictly between -90° and 90° (positive narrows the solid, like EXTRUDE).</summary>
        public static void CheckExtrude(double height, double taperDeg, string tool = "extrude")
        {
            if (!(Math.Abs(height) > Eps)) throw ToolException.Invalid($"{tool}: 'height' must not be 0 (negative extrudes downwards).");
            if (!(Math.Abs(taperDeg) <= MaxTaper)) throw ToolException.Invalid($"{tool}: 'taper_angle' must be between -89.9 and 89.9 degrees (got {taperDeg}).");
        }

        /// <summary>
        /// The height to give Solid3d.Extrude so a positive height always builds upwards (+Z) from a flat profile in
        /// the XY plane, whatever way its normal points (lines / arcs drawn clockwise, mirrored curves give -Z).
        /// Profiles that are not flat on XY extrude along their own normal.
        /// </summary>
        public static double ExtrudeHeightAlongNormal(double height, double normalZ) => normalZ < -0.5 ? -height : height;

        /// <summary>
        /// draw_wedge: after centring the bounding box, the centroid of the wanted wedge (tall face at -X, slope down
        /// towards +X, right angle at the bottom) is (-l/6, 0, -h/6). False for any other orientation.
        /// </summary>
        public static bool WedgeCentroidOk(double[] centroid, double l, double w, double h)
        {
            double tol = 1e-3 * Math.Max(l, Math.Max(w, h));
            return Math.Abs(centroid[0] + l / 6) <= tol && Math.Abs(centroid[1]) <= tol && Math.Abs(Z3(centroid) + h / 6) <= tol;
        }

        /// <summary>Revolve axis start → end: a unit direction, and the angle (degrees, 0 &lt; a ≤ 360) in radians.</summary>
        public static (double[] dir, double angleRad) RevolveAxis(double[] start, double[] end, double angleDeg, string tool = "revolve")
        {
            var d = Unit(Sub(end, start), $"{tool}: 'axis_start' and 'axis_end' must be different points");
            if (!(angleDeg > 0) || angleDeg > 360) throw ToolException.Invalid($"{tool}: 'angle' must be in (0, 360] degrees (got {angleDeg}).");
            return (d, angleDeg * Math.PI / 180.0);
        }

        /// <summary>
        /// A revolve profile must stay on one side of its axis (crossing it gives a self-intersecting solid that
        /// AutoCAD refuses) and the axis must lie in the profile's plane. The side of each point is measured in the
        /// plane with the given normal (default +Z, the XY plane), so profiles on vertical planes are checked too.
        /// The caller passes points sampled along the curves (bulges, arcs, circles), not only the vertices.
        /// null when fine, else an error text. Points on the axis are allowed.
        /// </summary>
        public static string ProfileCrossesAxis(IList<double[]> profile, double[] axisStart, double[] axisEnd, double[] normal = null, string tool = "revolve")
        {
            if (profile == null || profile.Count == 0) return null;
            var n = Unit(normal ?? Z, $"{tool}: the profile normal is zero");
            var axis = Sub(axisEnd, axisStart);
            double l = Len(axis);
            if (l < Eps) return null; // RevolveAxis already refuses this
            var d = Scale(axis, 1 / l);
            if (Math.Abs(Dot(d, n)) > 1e-6)
                return $"{tool}: the axis does not lie in the profile's plane (it points out of it). Put axis_start and axis_end in the plane of the profile, e.g. both with the profile's z for a profile drawn on XY.";
            int pos = 0, neg = 0;
            foreach (var p in profile)
            {
                double side = Dot(Cross(d, Sub(p, axisStart)), n);
                if (side > 1e-6) pos++;
                else if (side < -1e-6) neg++;
            }
            return pos > 0 && neg > 0
                ? $"{tool}: the profile crosses the axis (points on both sides). Move the axis to one edge of the profile or split the profile."
                : null;
        }

        /// <summary>solid_intersect: the solids up to 'handles' (in order) have no common volume (KT-28).</summary>
        public static string EmptyIntersectionMessage(string tool, IList<string> handles) =>
            $"{tool}: the solids do not overlap: {string.Join(", ", handles)} have no common volume, so the intersection is empty. Nothing was changed. " +
            "Check their positions with get_solid_info (min / max).";

        /// <summary>Box placement: the corner (min X, min Y, min Z before rotation) or the centre of the box.</summary>
        public static double[] BoxCenter(double[] corner, double length, double width, double height, double rotationDeg)
        {
            double a = rotationDeg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            double hx = length / 2, hy = width / 2;
            return new[] { corner[0] + hx * c - hy * s, corner[1] + hx * s + hy * c, Z3(corner) + height / 2 };
        }

        // ================================================================= expected volumes (mm³)

        public static double BoxVolume(double l, double w, double h) => Math.Abs(l * w * h);
        public static double WedgeVolume(double l, double w, double h) => Math.Abs(l * w * h) / 2;
        public static double SphereVolume(double r) => 4.0 / 3.0 * Math.PI * r * r * r;
        public static double TorusVolume(double major, double minor) => 2 * Math.PI * Math.PI * major * minor * minor;

        /// <summary>Cylinder (r1 = r2), cone (r2 = 0) or frustum of a cone.</summary>
        public static double FrustumVolume(double r1, double r2, double h) => Math.PI * Math.Abs(h) / 3 * (r1 * r1 + r1 * r2 + r2 * r2);

        /// <summary>Regular polygon with inscribed-circle radius a (apothem): area n · a² · tan(π / n).</summary>
        public static double PolygonAreaByApothem(int n, double a) => n * a * a * Math.Tan(Math.PI / n);

        /// <summary>Pyramid / frustum of a pyramid, radii of the inscribed circles (what Solid3d.CreatePyramid takes).</summary>
        public static double PyramidVolume(int sides, double r1, double r2, double h)
        {
            double a1 = PolygonAreaByApothem(sides, r1), a2 = PolygonAreaByApothem(sides, r2);
            return Math.Abs(h) / 3 * (a1 + a2 + Math.Sqrt(a1 * a2));
        }

        /// <summary>True when actual is within relTol (e.g. 0.001 = 0.1 %) of expected.</summary>
        public static bool VolumeMatches(double actual, double expected, double relTol = 1e-3) =>
            Math.Abs(actual - expected) <= relTol * Math.Max(Math.Abs(expected), 1e-6);

        /// <summary>mm³ → m³.</summary>
        public static double ToCubicMetres(double mm3) => mm3 / 1e9;

        // ================================================================= vectors

        public static double Z3(double[] p) => p.Length > 2 ? p[2] : 0;
        public static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], Z3(a) - Z3(b) };
        public static double[] Scale(double[] a, double k) => new[] { a[0] * k, a[1] * k, Z3(a) * k };
        public static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + Z3(a) * Z3(b);
        public static double Len(double[] a) => Math.Sqrt(Dot(a, a));
        public static double[] Cross(double[] a, double[] b) =>
            new[] { a[1] * Z3(b) - Z3(a) * b[1], Z3(a) * b[0] - a[0] * Z3(b), a[0] * b[1] - a[1] * b[0] };

        public static double[] Unit(double[] v, string error)
        {
            if (v == null) throw ToolException.Invalid(error + ".");
            double l = Len(v);
            if (!(l > Eps) || double.IsNaN(l) || double.IsInfinity(l)) throw ToolException.Invalid(error + ".");
            return new[] { v[0] / l, v[1] / l, Z3(v) / l };
        }
    }
}
