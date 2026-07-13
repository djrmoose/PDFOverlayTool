using System.Windows;

namespace PdfOverlayTool
{
    public partial class DemoReminderWindow : Window
    {
        public DemoReminderWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
        }
    }
}
