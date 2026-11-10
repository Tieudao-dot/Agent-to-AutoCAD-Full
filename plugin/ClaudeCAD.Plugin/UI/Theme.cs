using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Palette colors as dynamic resources (feature 19). Elements reference the keys
    /// with SetResourceReference, so switching dark/light only replaces the brushes.
    /// </summary>
    public static class Theme
    {
        public const string PanelBg = "cc.panel";
        public const string InputBg = "cc.input";
        public const string Fg = "cc.fg";
        public const string MutedFg = "cc.muted";
        public const string ErrorFg = "cc.error";
        /// <summary>Warning glyph (confirmation, "AutoCAD đang bận"): the color sits on the icon, not the card.</summary>
        public const string WarnFg = "cc.warn";
        public const string UserBg = "cc.user";
        public const string AssistantBg = "cc.assistant";
        public const string CardBg = "cc.card";
        public const string Accent = "cc.accent";
        public const string Border = "cc.border";
        public const string CodeBg = "cc.code";
        public const string LinkFg = "cc.link";
        public const string ChipBg = "cc.chip";
        /// <summary>Toolbar / section-header strip, like a Properties palette category row.</summary>
        public const string HeaderBg = "cc.header";
        /// <summary>Icon button under the mouse.</summary>
        public const string HoverBg = "cc.hover";
        /// <summary>Icon button being pressed, and the selected row.</summary>
        public const string PressBg = "cc.press";
        /// <summary>Text on top of <see cref="Accent"/>.</summary>
        public const string AccentFg = "cc.accentfg";

        /// <summary>
        /// Corner radius for every box in the palette. AutoCAD's own palettes are square, so this is 0 —
        /// it stays a single constant so the whole shell can be rounded again from one place.
        /// </summary>
        public static readonly CornerRadius Corner = new CornerRadius(0);

        /// <summary>
        /// Thin shadow that lifts the toolbar, the input row and the cards off the model-space background.
        /// </summary>
        public static Effect Shadow(double depth = 2, double blur = 5, double opacity = 0.35)
        {
            var e = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = depth >= 0 ? 270 : 90,
                ShadowDepth = Math.Abs(depth),
                BlurRadius = blur,
                Opacity = opacity,
                RenderingBias = RenderingBias.Performance,
            };
            e.Freeze();
            return e;
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        /// <summary>
        /// Fills the palette brushes: the built-in set for <paramref name="dark"/> first, then whatever
        /// AutoCAD's own palette theme gave us on top, so a key AutoCAD does not expose keeps a sane value.
        /// </summary>
        public static void Apply(ResourceDictionary res, bool dark, AcadTheme.Info acad)
        {
            Apply(res, dark);
            if (acad == null) return;
            foreach (var pair in acad.Brushes)
            {
                if (pair.Value != null) res[pair.Key] = pair.Value;
            }
        }

        public static void Apply(ResourceDictionary res, bool dark)
        {
            if (dark)
            {
                res[PanelBg] = B(0x25, 0x29, 0x2E);
                res[InputBg] = B(0x1E, 0x21, 0x25);
                res[Fg] = B(0xEC, 0xEF, 0xF2);
                res[MutedFg] = B(0xA0, 0xA8, 0xB0);
                res[ErrorFg] = B(0xFF, 0x8A, 0x80);
                res[WarnFg] = B(0xE8, 0xC1, 0x5A);
                res[UserBg] = B(0x2B, 0x57, 0x97);
                res[AssistantBg] = B(0x33, 0x38, 0x40);
                res[CardBg] = B(0x3B, 0x34, 0x22);
                res[Accent] = B(0x4C, 0x9A, 0xFF);
                res[Border] = B(0x45, 0x4B, 0x54);
                res[CodeBg] = B(0x1B, 0x1E, 0x22);
                res[LinkFg] = B(0x7C, 0xB7, 0xFF);
                res[ChipBg] = B(0x3A, 0x41, 0x4A);
                res[HeaderBg] = B(0x34, 0x39, 0x41);
                res[HoverBg] = B(0x3F, 0x45, 0x4E);
                res[PressBg] = B(0x2F, 0x66, 0xA8);
                res[AccentFg] = B(0xFF, 0xFF, 0xFF);
            }
            else
            {
                res[PanelBg] = B(0xF4, 0xF5, 0xF7);
                res[InputBg] = B(0xFF, 0xFF, 0xFF);
                res[Fg] = B(0x1F, 0x23, 0x28);
                res[MutedFg] = B(0x5F, 0x67, 0x70);
                res[ErrorFg] = B(0xC6, 0x28, 0x28);
                res[WarnFg] = B(0xA6, 0x6A, 0x00);
                res[UserBg] = B(0xD7, 0xE6, 0xFB);
                res[AssistantBg] = B(0xFF, 0xFF, 0xFF);
                res[CardBg] = B(0xFF, 0xF4, 0xD6);
                res[Accent] = B(0x1A, 0x6F, 0xD8);
                res[Border] = B(0xD0, 0xD5, 0xDB);
                res[CodeBg] = B(0xEE, 0xF0, 0xF3);
                res[LinkFg] = B(0x1A, 0x6F, 0xD8);
                res[ChipBg] = B(0xE4, 0xE8, 0xEE);
                res[HeaderBg] = B(0xE7, 0xEA, 0xEE);
                res[HoverBg] = B(0xDD, 0xE2, 0xE8);
                res[PressBg] = B(0xC3, 0xD8, 0xF2);
                res[AccentFg] = B(0xFF, 0xFF, 0xFF);
            }
        }

        /// <summary>AutoCAD COLORTHEME: 0 = dark, 1 = light.</summary>
        public static bool AutoCadIsDark()
        {
            try
            {
                return Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("COLORTHEME")) == 0;
            }
            catch
            {
                return true;
            }
        }

        public static void Ref(FrameworkElement e, DependencyProperty p, string key) => e.SetResourceReference(p, key);
        public static void Ref(FrameworkContentElement e, DependencyProperty p, string key) => e.SetResourceReference(p, key);
    }
}
