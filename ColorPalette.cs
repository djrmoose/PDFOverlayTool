using System.Windows;
using System.Windows.Media;

namespace PdfOverlayTool
{
    public enum AppTheme
    {
        Standard,
        BlueGrey
    }

    /// <summary>
    /// UI theme plus optional color-blind comparison overrides.
    /// </summary>
    public readonly record struct ColorPaletteSelection(AppTheme Theme, bool ColorBlindFriendly)
    {
        public static ColorPaletteSelection Default => new(AppTheme.BlueGrey, false);
    }

    /// <summary>
    /// Full application themes: UI chrome, controls, and document comparison tints.
    /// </summary>
    public static class ColorPalette
    {
        public const string StandardPaletteName = "standard";
        public const string BlueGreyPaletteName = "blueGrey";

        /// <summary>Legacy combined palette name; loads as Standard + color-blind overrides.</summary>
        public const string ColorBlindFriendlyPaletteName = "colorBlindFriendly";

        public static ColorPaletteSelection CurrentSelection { get; private set; } = ColorPaletteSelection.Default;

        public static string GetThemeName(AppTheme theme)
        {
            return theme == AppTheme.BlueGrey ? BlueGreyPaletteName : StandardPaletteName;
        }

        public static ColorPaletteSelection ParseSettings(string? paletteName, bool colorBlindFriendly = false)
        {
            if (string.Equals(paletteName, ColorBlindFriendlyPaletteName, StringComparison.OrdinalIgnoreCase))
            {
                return new ColorPaletteSelection(AppTheme.Standard, true);
            }

            AppTheme theme = string.Equals(paletteName, BlueGreyPaletteName, StringComparison.OrdinalIgnoreCase)
                ? AppTheme.BlueGrey
                : AppTheme.Standard;

            return new ColorPaletteSelection(theme, colorBlindFriendly);
        }

        public static Color GetBaseTintColor(ColorPaletteSelection selection) =>
            GetResolvedTheme(selection).BaseTint;

        public static Color GetOverlayTintColor(ColorPaletteSelection selection) =>
            GetResolvedTheme(selection).OverlayTint;

        public static void ApplyToResources(ResourceDictionary resources, ColorPaletteSelection selection)
        {
            ThemeDefinition theme = GetResolvedTheme(selection);

            resources["AccentColor"] = theme.Accent;
            SetBrush(resources, "AccentBrush", theme.Accent);
            SetBrush(resources, "AccentPressedBrush", theme.AccentPressed);
            SetBrush(resources, "ControlBackgroundBrush", theme.ControlBackground);
            SetBrush(resources, "ControlBorderBrush", theme.ControlBorder);
            SetBrush(resources, "ControlHoverBrush", theme.ControlHover);
            SetBrush(resources, "ControlPressedBrush", theme.ControlPressed);
            SetBrush(resources, "TrackBrush", theme.Track);
            SetBrush(resources, "TickBrush", theme.Tick);
            SetBrush(resources, "TextBrush", theme.Text);
            SetBrush(resources, "HelpTextBrush", theme.HelpText);
            SetBrush(resources, "ToolbarBackgroundBrush", theme.ToolbarBackground);
            SetBrush(resources, "ToolbarSecondaryBackgroundBrush", theme.ToolbarSecondaryBackground);
            SetBrush(resources, "PanelBorderBrush", theme.PanelBorder);
            SetBrush(resources, "ViewerBackgroundBrush", theme.ViewerBackground);
            SetBrush(resources, "ProcessingTrackBrush", theme.ProcessingTrack);
            SetBrush(resources, "AutoNormalBrush", theme.AutoNormal);
            SetBrush(resources, "AutoReducedBrush", theme.AutoReduced);
            SetBrush(resources, "AutoRecoveryBrush", theme.AutoRecovery);
            SetBrush(resources, "BaseFileBrush", theme.BaseTint);
            SetBrush(resources, "OverlayFileBrush", theme.OverlayTint);
            SetBrush(resources, "WindowBackgroundBrush", theme.WindowBackground);
            SetBrush(resources, "BodyBrush", theme.HelpText);
        }

        /// <summary>
        /// Updates brush colors in place so controls already using the resource refresh immediately.
        /// </summary>
        private static void SetBrush(ResourceDictionary resources, string key, Color color)
        {
            if (resources[key] is SolidColorBrush existing && !existing.IsFrozen)
            {
                existing.Color = color;
                return;
            }

            resources[key] = new SolidColorBrush(color);
        }

        public static void ApplyTheme(ColorPaletteSelection selection, ResourceDictionary resources)
        {
            CurrentSelection = selection;
            ApplyToResources(resources, selection);
        }

        public static void ApplyDialogTheme(Window window, ColorPaletteSelection selection)
        {
            ThemeDefinition theme = GetResolvedTheme(selection);
            ApplyToResources(window.Resources, selection);
            window.Background = (Brush)window.Resources["ToolbarBackgroundBrush"];

            NativeTitleBarHelper.Apply(
                window,
                theme.ToolbarBackground,
                theme.Text,
                theme.PanelBorder);
        }

        private static ThemeDefinition GetResolvedTheme(ColorPaletteSelection selection)
        {
            ThemeDefinition theme = GetBaseTheme(selection.Theme);
            return selection.ColorBlindFriendly ? ApplyColorBlindOverrides(theme) : theme;
        }

        private static ThemeDefinition GetBaseTheme(AppTheme theme)
        {
            return theme == AppTheme.BlueGrey ? BlueGreyTheme : StandardTheme;
        }

        /// <summary>
        /// Okabe–Ito comparison tints and AUTO indicator colors applied on top of either base theme.
        /// </summary>
        private static ThemeDefinition ApplyColorBlindOverrides(ThemeDefinition theme) =>
            theme with
            {
                AutoReduced = ColorBlindAutoReduced,
                AutoRecovery = ColorBlindAutoRecovery,
                BaseTint = ColorBlindBaseTint,
                OverlayTint = ColorBlindOverlayTint
            };

        // Warm brown controls, green / red comparison tints.
        private static readonly ThemeDefinition StandardTheme = new(
            Accent: FromHex("#7A5230"),
            AccentPressed: FromHex("#5C3D24"),
            ControlBackground: FromHex("#FDF9F4"),
            ControlBorder: FromHex("#C9B08A"),
            ControlHover: FromHex("#F2E8DA"),
            ControlPressed: FromHex("#E3D3BF"),
            Track: FromHex("#D6C6B3"),
            Tick: FromHex("#A89278"),
            Text: FromHex("#3A2E24"),
            HelpText: FromHex("#4A3F35"),
            ToolbarBackground: FromHex("#F0F0F0"),
            ToolbarSecondaryBackground: FromHex("#FAFAFA"),
            PanelBorder: FromHex("#CCCCCC"),
            ViewerBackground: FromHex("#2B2B2B"),
            ProcessingTrack: FromHex("#D8D8D8"),
            AutoNormal: FromHex("#FDF9F4"),
            AutoReduced: FromHex("#E8C98A"),
            AutoRecovery: FromHex("#A8D5A2"),
            WindowBackground: FromHex("#ECEFF1"),
            BaseTint: Colors.LimeGreen,
            OverlayTint: Colors.Red);

        // Cool blue / grey chrome, green / red comparison tints.
        private static readonly ThemeDefinition BlueGreyTheme = new(
            Accent: FromHex("#4A6FA5"),
            AccentPressed: FromHex("#3A587F"),
            ControlBackground: FromHex("#F8FAFC"),
            ControlBorder: FromHex("#B8C4CE"),
            ControlHover: FromHex("#E8EEF4"),
            ControlPressed: FromHex("#D0DCE8"),
            Track: FromHex("#CBD5E0"),
            Tick: FromHex("#7A8794"),
            Text: FromHex("#2C3E50"),
            HelpText: FromHex("#4A5568"),
            ToolbarBackground: FromHex("#ECEFF1"),
            ToolbarSecondaryBackground: FromHex("#F5F7FA"),
            PanelBorder: FromHex("#B0BEC5"),
            ViewerBackground: FromHex("#2B2B2B"),
            ProcessingTrack: FromHex("#CFD8DC"),
            AutoNormal: FromHex("#F8FAFC"),
            AutoReduced: FromHex("#FFAB91"),
            AutoRecovery: FromHex("#64B5F6"),
            WindowBackground: FromHex("#ECEFF1"),
            BaseTint: Colors.LimeGreen,
            OverlayTint: Colors.Red);

        private static readonly Color ColorBlindAutoReduced = FromHex("#CC6677");
        private static readonly Color ColorBlindAutoRecovery = FromHex("#0072B2");
        private static readonly Color ColorBlindBaseTint = FromHex("#0072B2");
        private static readonly Color ColorBlindOverlayTint = FromHex("#E69F00");

        private static Color FromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex)!;
        }

        private readonly record struct ThemeDefinition(
            Color Accent,
            Color AccentPressed,
            Color ControlBackground,
            Color ControlBorder,
            Color ControlHover,
            Color ControlPressed,
            Color Track,
            Color Tick,
            Color Text,
            Color HelpText,
            Color ToolbarBackground,
            Color ToolbarSecondaryBackground,
            Color PanelBorder,
            Color ViewerBackground,
            Color ProcessingTrack,
            Color AutoNormal,
            Color AutoReduced,
            Color AutoRecovery,
            Color WindowBackground,
            Color BaseTint,
            Color OverlayTint);
    }
}
