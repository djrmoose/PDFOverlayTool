using System.Windows;

namespace PdfOverlayTool
{
    public partial class SettingsWindow : Window
    {
        private bool _suppressColorBlindEvents;

        public event Action<bool>? ColorBlindFriendlyChanged;
        public event Action? ResetDefaultsRequested;

        public SettingsWindow(bool colorBlindFriendly)
        {
            InitializeComponent();

            _suppressColorBlindEvents = true;
            ColorBlindFriendlyCheckBox.IsChecked = colorBlindFriendly;
            _suppressColorBlindEvents = false;
        }

        private void ColorBlindFriendlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressColorBlindEvents)
            {
                return;
            }

            ColorBlindFriendlyChanged?.Invoke(ColorBlindFriendlyCheckBox.IsChecked == true);
        }

        private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "Reset all saved preferences to their defaults?\n\n"
                + "This restores opacity, performance sliders, AUTO mode, Revs Only, tint, and color palette. "
                + "Registration and loaded files are not affected.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            ResetDefaultsRequested?.Invoke();

            _suppressColorBlindEvents = true;
            ColorBlindFriendlyCheckBox.IsChecked = false;
            _suppressColorBlindEvents = false;
        }
    }
}
