using System.Windows;

namespace PdfOverlayTool
{
    public partial class ThirdPartyNoticesWindow : Window
    {
        public ThirdPartyNoticesWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
            NoticesTextBlock.Text = ThirdPartyNotices.Body;
        }
    }
}
