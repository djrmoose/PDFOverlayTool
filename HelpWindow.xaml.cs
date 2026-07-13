using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace PdfOverlayTool
{
    public partial class HelpWindow : Window
    {
        public bool DemoTourRequested { get; private set; }

        public HelpWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
        }

        private void FeedbackEmailLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void DemoTourButton_Click(object sender, RoutedEventArgs e)
        {
            DemoTourRequested = true;
            Close();
        }
    }
}
