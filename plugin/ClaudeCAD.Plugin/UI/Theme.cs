using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClaudeCAD.Plugin.Core;

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

        // ============================================================== control styles

        /// <summary>
        /// Flat controls painted with the palette keys, so buttons, combo boxes and check boxes follow
        /// the AutoCAD theme instead of showing Windows' light default chrome on a dark palette.
        /// Parsed at runtime (the project has no compiled XAML); every brush is a DynamicResource, so a
        /// theme switch repaints without rebuilding anything.
        /// </summary>
        private const string StylesXaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <Style TargetType='{x:Type Button}'>
    <Setter Property='Background' Value='{DynamicResource cc.chip}'/>
    <Setter Property='Foreground' Value='{DynamicResource cc.fg}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource cc.border}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,2'/>
    <Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Button}'>
          <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' SnapsToDevicePixels='True'>
            <ContentPresenter Margin='{TemplateBinding Padding}'
                              HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource cc.hover}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource cc.press}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ToggleButton}'>
    <Setter Property='Background' Value='{DynamicResource cc.chip}'/>
    <Setter Property='Foreground' Value='{DynamicResource cc.fg}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource cc.border}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,2'/>
    <Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ToggleButton}'>
          <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' SnapsToDevicePixels='True'>
            <ContentPresenter Margin='{TemplateBinding Padding}'
                              HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource cc.hover}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type CheckBox}'>
    <Setter Property='Foreground' Value='{DynamicResource cc.fg}'/>
    <Setter Property='Background' Value='{DynamicResource cc.input}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource cc.muted}'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type CheckBox}'>
          <StackPanel Orientation='Horizontal' Background='Transparent'>
            <Border x:Name='box' Width='14' Height='14' VerticalAlignment='Center'
                    Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'>
              <Path x:Name='tick' Visibility='Collapsed' Data='M 2.5 7 L 5.5 10 L 11 3.5'
                    Stroke='{DynamicResource cc.fg}' StrokeThickness='1.6'/>
            </Border>
            <ContentPresenter Margin='6,0,0,0' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='tick' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource cc.accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ComboBoxItem}'>
    <Setter Property='Foreground' Value='{DynamicResource cc.fg}'/>
    <Setter Property='Padding' Value='6,2'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBoxItem}'>
          <Border x:Name='bd' Background='Transparent' Padding='{TemplateBinding Padding}'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource cc.hover}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ComboBox}'>
    <Setter Property='Background' Value='{DynamicResource cc.input}'/>
    <Setter Property='Foreground' Value='{DynamicResource cc.fg}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource cc.border}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='MinHeight' Value='20'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBox}'>
          <Grid>
            <ToggleButton x:Name='toggle' Focusable='False' ClickMode='Press'
                          Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                          BorderThickness='{TemplateBinding BorderThickness}'
                          IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='{x:Type ToggleButton}'>
                  <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                          BorderThickness='{TemplateBinding BorderThickness}' SnapsToDevicePixels='True'>
                    <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,6,0'
                          Data='M 0 0 L 4 4 L 8 0' Stroke='{DynamicResource cc.fg}' StrokeThickness='1.3'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                      <Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource cc.accent}'/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible='False' Margin='6,2,20,2' VerticalAlignment='Center' HorizontalAlignment='Left'
                              Content='{TemplateBinding SelectionBoxItem}'
                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/>
            <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='False' Focusable='False'
                   PopupAnimation='None' IsOpen='{TemplateBinding IsDropDownOpen}'>
              <Border MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
                      MaxHeight='{TemplateBinding MaxDropDownHeight}'
                      Background='{DynamicResource cc.input}' BorderBrush='{DynamicResource cc.border}' BorderThickness='1'>
                <ScrollViewer>
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        // One parsed copy per UI thread: WPF styles belong to the thread that first uses them
        // (AutoCAD has one; the UI tests start a fresh STA thread per test).
        [ThreadStatic] private static ResourceDictionary _styles;
        [ThreadStatic] private static bool _stylesTried;

        /// <summary>False when the styles could not be parsed: the palette then keeps Windows' default controls.</summary>
        public static bool StylesLoaded => _styles != null;

        /// <summary>Merges the flat control styles into <paramref name="res"/> (once per dictionary).</summary>
        private static void EnsureStyles(ResourceDictionary res)
        {
            if (!_stylesTried)
            {
                _stylesTried = true;
                try
                {
                    _styles = (ResourceDictionary)XamlReader.Parse(StylesXaml);
                }
                catch (Exception ex)
                {
                    // Cosmetic only: never let a style problem keep the palette from opening.
                    _styles = null;
                    try { Logger.Warn("palette styles not loaded: " + ex.Message); } catch { }
                }
            }
            if (_styles != null && !res.MergedDictionaries.Contains(_styles)) res.MergedDictionaries.Add(_styles);
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
            EnsureStyles(res);
            if (dark)
            {
                res[PanelBg] = B(0x25, 0x29, 0x2E);
                res[InputBg] = B(0x1E, 0x21, 0x25);
                res[Fg] = B(0xEC, 0xEF, 0xF2);
                res[MutedFg] = B(0xA0, 0xA8, 0xB0);
                res[ErrorFg] = B(0xFF, 0x8A, 0x80);
                res[WarnFg] = B(0xE8, 0xC1, 0x5A);
                res[UserBg] = B(0x2A, 0x2E, 0x34);
                res[AssistantBg] = B(0x33, 0x38, 0x40);
                res[CardBg] = B(0x2A, 0x2E, 0x34);
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
                res[UserBg] = B(0xEA, 0xEC, 0xEF);
                res[AssistantBg] = B(0xFF, 0xFF, 0xFF);
                res[CardBg] = B(0xFF, 0xFF, 0xFF);
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
