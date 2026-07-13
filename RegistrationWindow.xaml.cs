using System.ComponentModel;
using System.Windows;

namespace PdfOverlayTool
{
    public partial class RegistrationWindow : Window
    {
        public string UserName { get; private set; } = "";
        public string UserEmail { get; private set; } = "";
        public bool TermsAccepted { get; private set; }
        public string TermsVersion { get; private set; } = "";
        public DateTime TermsAcceptedUtc { get; private set; }

        public RegistrationWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
            TitleTextBlock.Text = $"Welcome to Overlay Compare Tool {BetaConfig.VersionNumber}";
            TermsTextBlock.Text = BetaTerms.Body;
            Closing += RegistrationWindow_Closing;
        }

        private void RegistrationWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (DialogResult != true)
            {
                Application.Current.Shutdown();
            }
        }

        private void AcceptTermsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ContinueButton.IsEnabled = AcceptTermsCheckBox.IsChecked == true;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (AcceptTermsCheckBox.IsChecked != true)
            {
                ShowValidation("You must accept the Beta Terms of Use to continue.");
                return;
            }

            string name = NameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidation("Please enter your name.");
                NameTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowValidation("Please enter a valid email address.");
                EmailTextBox.Focus();
                return;
            }

            UserName = name;
            UserEmail = email;
            TermsAccepted = true;
            TermsVersion = BetaTerms.Version;
            TermsAcceptedUtc = DateTime.UtcNow;
            DialogResult = true;
        }

        private void ShowValidation(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private static bool IsValidEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@'))
            {
                return false;
            }

            int dot = email.LastIndexOf('.');
            return dot > at + 1 && dot < email.Length - 1;
        }
    }
}
