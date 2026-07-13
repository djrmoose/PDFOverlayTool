using System.Windows.Media;

namespace PdfOverlayTool
{
    /// <summary>
    /// Document and UI accent colors for standard vs color-blind-friendly display.
    /// CVD palette uses Okabe–Ito blue/orange for the comparison tints.
    /// </summary>
    public static class ColorPalette
    {
        public static Color GetBaseTintColor(bool colorBlindFriendly)
        {
            return colorBlindFriendly ? FromHex("#0072B2") : Colors.LimeGreen;
        }

        public static Color GetOverlayTintColor(bool colorBlindFriendly)
        {
            return colorBlindFriendly ? FromHex("#E69F00") : Colors.Red;
        }

        public static Color GetAutoReducedColor(bool colorBlindFriendly)
        {
            return colorBlindFriendly ? FromHex("#CC6677") : FromHex("#E8C98A");
        }

        public static Color GetAutoRecoveryColor(bool colorBlindFriendly)
        {
            return colorBlindFriendly ? FromHex("#0072B2") : FromHex("#A8D5A2");
        }

        public static void UpdateBrushResources(System.Windows.ResourceDictionary resources, bool colorBlindFriendly)
        {
            resources["BaseFileBrush"] = CreateBrush(GetBaseTintColor(colorBlindFriendly));
            resources["OverlayFileBrush"] = CreateBrush(GetOverlayTintColor(colorBlindFriendly));
            resources["AutoReducedBrush"] = CreateBrush(GetAutoReducedColor(colorBlindFriendly));
            resources["AutoRecoveryBrush"] = CreateBrush(GetAutoRecoveryColor(colorBlindFriendly));
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color FromHex(string hex)
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        }
    }
}
