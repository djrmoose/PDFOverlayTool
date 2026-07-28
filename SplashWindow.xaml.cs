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
                Title = "Beta period ended";
                TitleTextBlock.Text = "Beta period ended";

                BodyTextBlock.Inlines.Clear();
                BodyTextBlock.Inlines.Add(new Run(
                    "The Overlay Compare Tool " + BetaConfig.VersionNumber + " beta period has ended. " +
                    "Only demonstration mode is available (included sample files only).\n\n" +
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
                Title = "Beta " + BetaConfig.VersionNumber;
                TitleTextBlock.Text = "Beta";

                BodyTextBlock.Inlines.Clear();
                BodyTextBlock.Inlines.Add(new Run(
                    "This is " + BetaConfig.VersionNumber + " of the Overlay Compare Tool beta.\n\n" +
                    $"This beta is valid until {BetaConfig.FileLoadingDisabledDateDisplay}. " +
                    "After that date, only demonstration mode will be available.\n\n" +
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
