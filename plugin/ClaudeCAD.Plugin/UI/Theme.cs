using System;
using System.Windows;
using System.Windows.Media;

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
        public const string UserBg = "cc.user";
        public const string AssistantBg = "cc.assistant";
        public const string CardBg = "cc.card";
        public const string Accent = "cc.accent";
        public const string Border = "cc.border";
        public const string CodeBg = "cc.code";
        public const string LinkFg = "cc.link";
        public const string ChipBg = "cc.chip";

        private static SolidColorBrush B(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
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
                res[UserBg] = B(0x2B, 0x57, 0x97);
                res[AssistantBg] = B(0x33, 0x38, 0x40);
                res[CardBg] = B(0x3B, 0x34, 0x22);
                res[Accent] = B(0x4C, 0x9A, 0xFF);
                res[Border] = B(0x45, 0x4B, 0x54);
                res[CodeBg] = B(0x1B, 0x1E, 0x22);
                res[LinkFg] = B(0x7C, 0xB7, 0xFF);
                res[ChipBg] = B(0x3A, 0x41, 0x4A);
            }
            else
            {
                res[PanelBg] = B(0xF4, 0xF5, 0xF7);
                res[InputBg] = B(0xFF, 0xFF, 0xFF);
                res[Fg] = B(0x1F, 0x23, 0x28);
                res[MutedFg] = B(0x5F, 0x67, 0x70);
                res[ErrorFg] = B(0xC6, 0x28, 0x28);
                res[UserBg] = B(0xD7, 0xE6, 0xFB);
                res[AssistantBg] = B(0xFF, 0xFF, 0xFF);
                res[CardBg] = B(0xFF, 0xF4, 0xD6);
                res[Accent] = B(0x1A, 0x6F, 0xD8);
                res[Border] = B(0xD0, 0xD5, 0xDB);
                res[CodeBg] = B(0xEE, 0xF0, 0xF3);
                res[LinkFg] = B(0x1A, 0x6F, 0xD8);
                res[ChipBg] = B(0xE4, 0xE8, 0xEE);
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
