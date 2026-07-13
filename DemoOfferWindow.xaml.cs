using System.Windows;

namespace PdfOverlayTool
{
    public partial class DemoOfferWindow : Window
    {
        public bool ShowDemo { get; private set; }

        public DemoOfferWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
        }

        private void ShowDemoButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDemo = true;
            DialogResult = true;
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDemo = false;
            DialogResult = true;
        }
    }
}
