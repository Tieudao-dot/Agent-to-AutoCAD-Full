using System.Windows;
using System.Windows.Media;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// AcadTheme reads AutoCAD's palette theme by reflection, so these tests feed it stand-ins with the
    /// same property names as Autodesk.Windows.Palettes.PaletteTheme — no AutoCAD needed.
    /// </summary>
    public class AcadThemeTests
    {
        /// <summary>A palette theme as AutoCAD 2022-2025 exposes it (colors, brushes and a font).</summary>
        private sealed class FakeTheme
        {
            public bool IsDark { get; set; } = true;
            public Color PaletteContainerBackgroundColor { get; set; } = Color.FromRgb(0x2E, 0x32, 0x38);
            public Color CategoryBackgroundColor { get; set; } = Color.FromRgb(0x34, 0x39, 0x41);
            public Color CategoryBorderColor { get; set; } = Color.FromRgb(0x1D, 0x20, 0x24);
            public Color TextInputBackgroundColor { get; set; } = Color.FromRgb(0x22, 0x25, 0x2A);
            public Color ControlTextColor { get; set; } = Color.FromRgb(0xDF, 0xE2, 0xE5);
            public Color TextInputInactiveTextColor { get; set; } = Color.FromRgb(0x9B, 0xA1, 0xA8);
            public Color PaletteHyperlinkTextColor { get; set; } = Color.FromRgb(0x6B, 0xA3, 0xD6);
            public Color SquareButtonBackgroundColor { get; set; } = Color.FromRgb(0x3A, 0x3F, 0x46);
            public Color TextInputActiveBorderColor { get; set; } = Color.FromRgb(0x4C, 0x9A, 0xFF);
            public Brush SquareButtonHoverBackgroundBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x3F, 0x45, 0x4E));
            public Brush SquareButtonClickBackgroundBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x2F, 0x66, 0xA8));
            public object NormalFont { get; set; } = new FakeFont();
        }

        private sealed class FakeFont
        {
            public FontFamily FontFamily { get; set; } = new FontFamily("Tahoma");
            public double FontSize { get; set; } = 12;
            public FontWeight FontWeight { get; set; } = FontWeights.Normal;
            public FontStyle FontStyle { get; set; } = FontStyles.Normal;
        }

        private static Color Of(Brush b) => ((SolidColorBrush)b).Color;

        [Fact]
        public void ReadsColorsFontAndDarkFlag()
        {
            var info = AcadTheme.ReadFrom(new FakeTheme());

            Assert.NotNull(info);
            Assert.True(info.IsDark);
            Assert.Equal(Color.FromRgb(0x2E, 0x32, 0x38), Of(info.Brushes[Theme.PanelBg]));
            Assert.Equal(Color.FromRgb(0x34, 0x39, 0x41), Of(info.Brushes[Theme.HeaderBg]));
            Assert.Equal(Color.FromRgb(0x22, 0x25, 0x2A), Of(info.Brushes[Theme.InputBg]));
            Assert.Equal(Color.FromRgb(0xDF, 0xE2, 0xE5), Of(info.Brushes[Theme.Fg]));
            Assert.Equal(Color.FromRgb(0x9B, 0xA1, 0xA8), Of(info.Brushes[Theme.MutedFg]));
            Assert.Equal(Color.FromRgb(0x1D, 0x20, 0x24), Of(info.Brushes[Theme.Border]));
            Assert.Equal(Color.FromRgb(0x6B, 0xA3, 0xD6), Of(info.Brushes[Theme.LinkFg]));
            Assert.Equal(Color.FromRgb(0x3F, 0x45, 0x4E), Of(info.Brushes[Theme.HoverBg]));
            Assert.Equal(Color.FromRgb(0x2F, 0x66, 0xA8), Of(info.Brushes[Theme.PressBg]));
            Assert.Equal("Tahoma", info.Font.Source);
            Assert.Equal(12, info.FontSize);
            // Brushes must be frozen: the palette paints them from the UI thread after we read them here.
            Assert.All(info.Brushes.Values, b => Assert.True(b.IsFrozen));
        }

        [Fact]
        public void FallsBackPerPropertyAndIgnoresUnknownThemes()
        {
            // A theme that only knows the panel background: every other key stays absent so Theme.Apply
            // keeps its built-in value.
            var info = AcadTheme.ReadFrom(new { PaletteContainerBackgroundColor = Color.FromRgb(1, 2, 3) });

            Assert.NotNull(info);
            Assert.Equal(Color.FromRgb(1, 2, 3), Of(info.Brushes[Theme.PanelBg]));
            Assert.False(info.Brushes.ContainsKey(Theme.Fg));
            Assert.Null(info.IsDark);

            // Nothing recognizable at all, and null: no Info rather than an empty one.
            Assert.Null(AcadTheme.ReadFrom(new { Unrelated = 42 }));
            Assert.Null(AcadTheme.ReadFrom(null));
        }

        [Fact]
        public void SkipsTransparentColorsAndOutOfRangeFontSizes()
        {
            var info = AcadTheme.ReadFrom(new
            {
                // "not set" in several AutoCAD themes — must not paint invisible text.
                ControlTextColor = Color.FromArgb(0, 0xFF, 0xFF, 0xFF),
                PaletteContainerBackgroundColor = Color.FromRgb(0x2E, 0x32, 0x38),
                NormalFont = new { FontFamily = "Arial", FontSize = 96.0 },
            });

            Assert.NotNull(info);
            Assert.False(info.Brushes.ContainsKey(Theme.Fg));
            Assert.Equal("Arial", info.Font.Source);
            Assert.Equal(0, info.FontSize);
        }

        [Fact]
        public void AcceptsSystemDrawingColorsAndFontFamilyNames()
        {
            var info = AcadTheme.ReadFrom(new
            {
                PaletteContainerBackgroundColor = System.Drawing.Color.FromArgb(255, 0x10, 0x20, 0x30),
                NormalFont = new { FontFamily = "Segoe UI", FontSize = 13.0 },
            });

            Assert.NotNull(info);
            Assert.Equal(Color.FromRgb(0x10, 0x20, 0x30), Of(info.Brushes[Theme.PanelBg]));
            Assert.Equal("Segoe UI", info.Font.Source);
            Assert.Equal(13, info.FontSize);
        }

        [Fact]
        public void FontOnlyDropsTheColors()
        {
            var info = AcadTheme.ReadFrom(new FakeTheme()).FontOnly();

            Assert.Empty(info.Brushes);
            Assert.Equal("Tahoma", info.Font.Source);
            Assert.Equal(12, info.FontSize);
            Assert.True(info.IsDark);
        }

        [Fact]
        public void ApplyPutsAutoCadColorsOverTheBuiltInSet()
        {
            var res = new ResourceDictionary();
            var info = AcadTheme.ReadFrom(new { PaletteContainerBackgroundColor = Color.FromRgb(1, 2, 3) });

            Theme.Apply(res, dark: true, acad: info);

            Assert.Equal(Color.FromRgb(1, 2, 3), Of((Brush)res[Theme.PanelBg]));
            // A key AutoCAD did not give us keeps the built-in dark value.
            Assert.Equal(Color.FromRgb(0xEC, 0xEF, 0xF2), Of((Brush)res[Theme.Fg]));

            // No theme at all: the built-in set, including the new shell keys.
            var plain = new ResourceDictionary();
            Theme.Apply(plain, dark: true, acad: null);
            Assert.Equal(Color.FromRgb(0x25, 0x29, 0x2E), Of((Brush)plain[Theme.PanelBg]));
            Assert.NotNull(plain[Theme.HeaderBg]);
            Assert.NotNull(plain[Theme.HoverBg]);
            Assert.NotNull(plain[Theme.PressBg]);
            Assert.NotNull(plain[Theme.AccentFg]);
        }

        [Fact]
        public void ReadAndSubscribeStayQuietWithoutAutoCad()
        {
            // No AdUiPalettes in the test process: both must simply report "not available".
            Assert.Null(AcadTheme.Read());
            Assert.False(AcadTheme.Subscribe(() => { }));
            Assert.False(AcadTheme.Subscribe(null));
            AcadTheme.Unsubscribe(); // must not throw when nothing is subscribed
        }
    }
}
