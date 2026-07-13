using System.Windows;
using System.Windows.Controls;

namespace PdfOverlayTool
{
    public partial class SettingsWindow : Window
    {
        private bool _suppressPaletteEvents;
        private bool _suppressRevisionSeparatorEvents;

        public event Action<ColorPaletteSelection>? PaletteChanged;
        public event Action<string>? RevisionSeparatorChanged;
        public event Action? ResetDefaultsRequested;

        public SettingsWindow(ColorPaletteSelection selection, string revisionSeparator)
        {
            _suppressPaletteEvents = true;
            _suppressRevisionSeparatorEvents = true;
            InitializeComponent();
            _suppressPaletteEvents = false;
            _suppressRevisionSeparatorEvents = false;

            ApplyPreview(selection);
            SelectPalette(selection, notify: false);
            SetRevisionSeparatorText(revisionSeparator, notify: false);
        }

        private void PaletteOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPaletteEvents)
            {
                return;
            }

            if (sender is RadioButton radio && radio.IsChecked != true)
            {
                return;
            }

            ColorPaletteSelection selection = GetSelectedPalette();
            ApplyPreview(selection);
            PaletteChanged?.Invoke(selection);
        }

        private void RevisionSeparatorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressRevisionSeparatorEvents)
            {
                return;
            }

            RevisionSeparatorChanged?.Invoke(NormalizeRevisionSeparator(RevisionSeparatorTextBox.Text));
        }

        private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetDefaultsRequested?.Invoke();
            ColorPaletteSelection defaults = ColorPaletteSelection.Default;
            SelectPalette(defaults, notify: false);
            ApplyPreview(defaults);
            SetRevisionSeparatorText(UserSettings.CreatePreferenceDefaults().RevisionSeparator, notify: false);
        }

        private ColorPaletteSelection GetSelectedPalette()
        {
            AppTheme theme = BlueGreyThemeRadio?.IsChecked == true
                ? AppTheme.BlueGrey
                : AppTheme.Standard;

            return new ColorPaletteSelection(theme, ColorBlindFriendlyCheckBox?.IsChecked == true);
        }

        private void SelectPalette(ColorPaletteSelection selection, bool notify)
        {
            _suppressPaletteEvents = true;
            if (StandardThemeRadio != null)
            {
                StandardThemeRadio.IsChecked = selection.Theme == AppTheme.Standard;
            }

            if (BlueGreyThemeRadio != null)
            {
                BlueGreyThemeRadio.IsChecked = selection.Theme == AppTheme.BlueGrey;
            }

            if (ColorBlindFriendlyCheckBox != null)
            {
                ColorBlindFriendlyCheckBox.IsChecked = selection.ColorBlindFriendly;
            }

            _suppressPaletteEvents = false;

            if (notify)
            {
                PaletteChanged?.Invoke(selection);
            }
        }

        private void SetRevisionSeparatorText(string separator, bool notify)
        {
            _suppressRevisionSeparatorEvents = true;
            if (RevisionSeparatorTextBox != null)
            {
                RevisionSeparatorTextBox.Text = separator;
            }

            _suppressRevisionSeparatorEvents = false;

            if (notify)
            {
                RevisionSeparatorChanged?.Invoke(NormalizeRevisionSeparator(separator));
            }
        }

        private static string NormalizeRevisionSeparator(string? separator) =>
            separator?.Trim() ?? "";

        private void ApplyPreview(ColorPaletteSelection selection)
        {
            ColorPalette.ApplyDialogTheme(this, selection);
        }
    }
}
