using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// The palette's icon set: flat single-color line icons on a 16 px grid, the way AutoCAD 2022+ draws its palette
    /// and ribbon icons (thin strokes, no emoji, no color fill, the theme's text color). Drawn for this plugin, not
    /// copied from Autodesk. The path data is plain SVG / WPF path syntax, so the HTML preview uses the same shapes.
    /// </summary>
    public static class Icons
    {
        // name → (path data in a 16 × 16 box, filled shape)
        internal static readonly Dictionary<string, (string data, bool fill)> Shapes = new Dictionary<string, (string, bool)>
        {
            ["settings"] = ("M8,1.5 V3.2 M8,12.8 V14.5 M1.5,8 H3.2 M12.8,8 H14.5 M3.4,3.4 L4.6,4.6 M11.4,11.4 L12.6,12.6 M3.4,12.6 L4.6,11.4 M11.4,4.6 L12.6,3.4 M12.8,8 A4.8,4.8 0 1 1 3.2,8 A4.8,4.8 0 1 1 12.8,8 Z M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8 Z", false),
            ["export"] = ("M8,2 V10 M4.5,6.5 L8,10 L11.5,6.5 M2.5,10.5 V13.5 H13.5 V10.5", false),
            ["new-chat"] = ("M2.5,2.5 H13.5 V11 H6.5 L3.5,13.5 V11 H2.5 Z M8,4.5 V9 M5.75,6.75 H10.25", false),
            ["stop"] = ("M4.5,4.5 H11.5 V11.5 H4.5 Z", true),
            ["chevron-down"] = ("M3.5,6 L8,10.5 L12.5,6", false),
            ["chevron-up"] = ("M3.5,10 L8,5.5 L12.5,10", false),
            ["add"] = ("M8,3 V13 M3,8 H13", false),
            ["skills"] = ("M2,3 H6 A2,2 0 0 1 8,5 V13.5 A1.5,1.5 0 0 0 6.5,12 H2 Z M14,3 H10 A2,2 0 0 0 8,5 V13.5 A1.5,1.5 0 0 1 9.5,12 H14 Z", false),
            ["layers"] = ("M8,2.5 L14,5.5 L8,8.5 L2,5.5 Z M2,8.5 L8,11.5 L14,8.5 M2,11 L8,14 L14,11", false),
            ["macro"] = ("M5,3 L12.5,8 L5,13 Z", false),
            ["notes"] = ("M3,1.5 H10 L13,4.5 V14.5 H3 Z M10,1.5 V4.5 H13 M5.5,7.5 H10.5 M5.5,10 H10.5 M5.5,12.5 H8.5", false),
            ["versions"] = ("M2.7,9.5 A5.5,5.5 0 1 0 4.1,4.1 M1.8,2.4 L4.1,4.1 L2.4,6.4 M8,5 V8.3 L10.4,9.8", false),
            ["select"] = ("M1.5,1.5 H3.5 M5.5,1.5 H7.5 M9.5,1.5 H11.5 M13.5,1.5 V3.5 M13.5,5.5 V7 M1.5,3.5 V5.5 M1.5,7.5 V9.5 M1.5,11.5 V13.5 H3.5 M5.5,13.5 H7 M8.5,7.5 L14.5,10 L12,11 L11,13.5 Z", false),
            ["point"] = ("M8,1.5 V6 M8,10 V14.5 M1.5,8 H6 M10,8 H14.5 M6.5,6.5 H9.5 V9.5 H6.5 Z", false),
            ["image"] = ("M1.5,2.5 H14.5 V13.5 H1.5 Z M1.5,11 L5.5,7 L9,10.5 L11,8.5 L14.5,12 M11.5,5.5 A1,1 0 1 1 9.5,5.5 A1,1 0 1 1 11.5,5.5 Z", false),
            ["capture"] = ("M1.5,5 V1.5 H5 M11,1.5 H14.5 V5 M14.5,11 V14.5 H11 M5,14.5 H1.5 V11 M5.5,5.5 H10.5 V10.5 H5.5 Z", false),
            ["lock"] = ("M3.5,7 H12.5 V14 H3.5 Z M5.5,7 V5 A2.5,2.5 0 0 1 10.5,5 V7 M8,9.5 V11.5", false),
            ["unlock"] = ("M3.5,7 H12.5 V14 H3.5 Z M5.5,7 V5 A2.5,2.5 0 0 1 10.3,4 M8,9.5 V11.5", false),
            ["save"] = ("M2.5,2.5 H11.5 L13.5,4.5 V13.5 H2.5 Z M5,2.5 V5.5 H10.5 V2.5 M5,13.5 V9.5 H11 V13.5", false),
            ["view"] = ("M1.5,8 C3.5,4 12.5,4 14.5,8 C12.5,12 3.5,12 1.5,8 Z M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8 Z", false),
            ["locate"] = ("M12.5,8 A4.5,4.5 0 1 1 3.5,8 A4.5,4.5 0 1 1 12.5,8 Z M8,1.5 V5 M8,11 V14.5 M1.5,8 H5 M11,8 H14.5", false),
            ["pin"] = ("M8,14.5 C8,14.5 3.5,9.8 3.5,6.5 A4.5,4.5 0 0 1 12.5,6.5 C12.5,9.8 8,14.5 8,14.5 Z M9.5,6.5 A1.5,1.5 0 1 1 6.5,6.5 A1.5,1.5 0 1 1 9.5,6.5 Z", false),
            ["wait"] = ("M4,1.5 H12 M4,14.5 H12 M5,1.5 C5,5.5 11,5.5 11,8 C11,10.5 5,10.5 5,14.5 M11,1.5 C11,5.5 5,5.5 5,8 C5,10.5 11,10.5 11,14.5", false),
            ["star"] = ("M8,1.8 L9.9,5.8 L14.2,6.3 L11,9.3 L11.8,13.6 L8,11.5 L4.2,13.6 L5,9.3 L1.8,6.3 L6.1,5.8 Z", false),
            ["warning"] = ("M8,1.8 L14.8,13.8 H1.2 Z M8,6 V9.6 M8,11.4 V12", false),
            ["error"] = ("M14,8 A6,6 0 1 1 2,8 A6,6 0 1 1 14,8 Z M5.6,5.6 L10.4,10.4 M10.4,5.6 L5.6,10.4", false),
            ["ok"] = ("M3,8.5 L6.5,12 L13,4.5", false),
            ["info"] = ("M14,8 A6,6 0 1 1 2,8 A6,6 0 1 1 14,8 Z M8,7 V11.5 M8,4.6 V5.2", false),
            ["close"] = ("M4,4 L12,12 M12,4 L4,12", false),
        };

        /// <summary>True when the set has an icon of this name.</summary>
        public static bool Has(string name) => name != null && Shapes.ContainsKey(name);

        /// <summary>
        /// A 16 px icon. Without <paramref name="brushKey"/> it takes the Foreground of the button (or other control)
        /// it sits in, so MarkIcon's accent color and the theme switch reach it; disabled buttons show it dimmed.
        /// Tag = the icon name (tests and the self-test read it).
        /// </summary>
        public static FrameworkElement Make(string name, string brushKey = null, double size = 16)
        {
            var shape = Shapes.TryGetValue(name, out var s) ? s : Shapes["info"];
            var p = new Path
            {
                Data = Geometry.Parse(shape.data),
                Width = 16,
                Height = 16,
                Stretch = Stretch.None,
                StrokeThickness = 1.2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                SnapsToDevicePixels = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (brushKey != null)
            {
                p.SetResourceReference(Shape.StrokeProperty, brushKey);
                if (shape.fill) p.SetResourceReference(Shape.FillProperty, brushKey);
            }
            else
            {
                var fromControl = new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) };
                p.SetBinding(Shape.StrokeProperty, fromControl);
                if (shape.fill) p.SetBinding(Shape.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
            }
            p.IsEnabledChanged += (o, e) => p.Opacity = p.IsEnabled ? 1 : 0.35;
            FrameworkElement result = p;
            if (size != 16) result = new Viewbox { Width = size, Height = size, Child = p };
            result.Tag = name;
            return result;
        }

        /// <summary>Icon + label for a text button ("Lưu macro" with the save icon).</summary>
        public static StackPanel WithText(string name, string text, string brushKey = null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = Make(name, brushKey, 12);
            icon.Margin = new Thickness(0, 0, 4, 0);
            row.Children.Add(icon);
            row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }
    }
}
