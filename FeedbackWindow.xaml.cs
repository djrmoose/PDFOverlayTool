using System.Windows;
using System.Windows.Controls;

namespace PdfOverlayTool
{
    public partial class FeedbackWindow : Window
    {
        private const int MaxFeedbackLength = 2000;

        public bool WasSkipped { get; private set; }

        public string? Rating { get; private set; }

        public string? FeedbackText { get; private set; }

        public FeedbackWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            WasSkipped = false;
            Rating = (RatingComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            string text = FeedbackTextBox.Text.Trim();
            if (text.Length > MaxFeedbackLength)
            {
                text = text[..MaxFeedbackLength];
            }

            FeedbackText = string.IsNullOrWhiteSpace(text) ? null : text;
            DialogResult = true;
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            WasSkipped = true;
            Rating = null;
            FeedbackText = null;
            DialogResult = true;
        }
    }
}
