using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace PdfOverlayTool
{
    public partial class SplashWindow : Window
    {
        public SplashWindow(bool fileLoadingDisabled)
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);

            if (fileLoadingDisabled)
            {
                Title = "Demonstration Expired";
                TitleTextBlock.Text = "Demonstration Expired";

                BodyTextBlock.Inlines.Clear();
                BodyTextBlock.Inlines.Add(new Run(
                    "The Overlay Compare Tool " + BetaConfig.VersionNumber + " demonstration period has expired. " +
                    "Only DEMO mode is available.\n\n" +
                    "To request an updated version or provide feedback, contact the developer at "));
                var link = new Hyperlink(new Run(BetaConfig.DeveloperEmail))
                {
                    NavigateUri = new Uri($"mailto:{BetaConfig.DeveloperEmail}?subject=Overlay%20Compare%20Tool%20Feedback")
                };
                link.RequestNavigate += ContactEmailLink_RequestNavigate;
                BodyTextBlock.Inlines.Add(link);
                BodyTextBlock.Inlines.Add(new Run("."));
            }
            else
            {
                Title = "BETA Demonstration " + BetaConfig.VersionNumber;
                TitleTextBlock.Text = "BETA Demonstration";

                BodyTextBlock.Inlines.Clear();
                BodyTextBlock.Inlines.Add(new Run(
                    "This is demonstration " + BetaConfig.VersionNumber + " of the Overlay Compare Tool.\n\n" +
                    $"This demonstration version is valid until {BetaConfig.FileLoadingDisabledDateDisplay}. " +
                    "After that date, only DEMO mode will be available.\n\n" +
                    "Your feedback is extremely valuable! Please send any feedback to "));
                var link = new Hyperlink(new Run(BetaConfig.DeveloperEmail))
                {
                    NavigateUri = new Uri($"mailto:{BetaConfig.DeveloperEmail}?subject=Overlay%20Compare%20Tool%20Feedback")
                };
                link.RequestNavigate += ContactEmailLink_RequestNavigate;
                BodyTextBlock.Inlines.Add(link);
                BodyTextBlock.Inlines.Add(new Run("."));
            }
        }

        private static void ContactEmailLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
