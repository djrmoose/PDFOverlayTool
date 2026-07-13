using System.Windows;
using System.Windows.Controls;

namespace PdfOverlayTool
{
    public partial class SettingsWindow : Window
    {
        private bool _suppressPaletteEvents;

        public event Action<ColorPaletteSelection>? PaletteChanged;
        public event Action? ResetDefaultsRequested;

        public SettingsWindow(ColorPaletteSelection selection)
        {
            _suppressPaletteEvents = true;
            InitializeComponent();
            _suppressPaletteEvents = false;

            ApplyPreview(selection);
            SelectPalette(selection, notify: false);
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
            ColorPaletteSelection defaults = ColorPaletteSelection.Default;
            SelectPalette(defaults, notify: false);
            ApplyPreview(defaults);
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

        private void ApplyPreview(ColorPaletteSelection selection)
        {
            ColorPalette.ApplyDialogTheme(this, selection);
        }
    }
}
