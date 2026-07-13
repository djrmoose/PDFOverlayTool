using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfOverlayTool
{
    /// <summary>
    /// Sets the native Windows 11 title bar colors so focused dialogs match the
    /// inactive gray caption instead of showing accent color or content behind the window.
    /// </summary>
    internal static class NativeTitleBarHelper
    {
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        public static void Apply(Window window, Color captionBackground, Color captionText, Color border)
        {
            if (!IsWindows11OrLater())
            {
                return;
            }

            void ApplyWhenReady()
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                int caption = ToColorRef(captionBackground);
                int text = ToColorRef(captionText);
                int borderColor = ToColorRef(border);

                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
            }

            if (window.IsLoaded)
            {
                ApplyWhenReady();
            }
            else
            {
                window.SourceInitialized += (_, _) => ApplyWhenReady();
            }

            // Windows can revert caption colors when focus changes.
            window.Activated += (_, _) => ApplyWhenReady();
        }

        private static bool IsWindows11OrLater() =>
            Environment.OSVersion.Version.Major >= 10
            && Environment.OSVersion.Version.Build >= 22000;

        private static int ToColorRef(Color color) =>
            color.R | (color.G << 8) | (color.B << 16);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
