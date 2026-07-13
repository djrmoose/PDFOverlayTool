using System.Windows;

namespace PdfOverlayTool
{
    public partial class DemoTourWindow : Window
    {
        public event Action? NextRequested;
        public event Action? BackRequested;
        public event Action? SkipRequested;

        public DemoTourWindow()
        {
            InitializeComponent();
            ColorPalette.ApplyDialogTheme(this, ColorPalette.CurrentSelection);
        }

        public void SetStep(int index, int total, string title, string body)
        {
            StepIndicatorText.Text = $"Step {index + 1} of {total}";
            StepTitleText.Text = title;
            StepBodyText.Text = body;
            BackButton.IsEnabled = index > 0;
            NextButton.Content = index == total - 1 ? "Finish" : "Next";
            SkipButton.Visibility = index == total - 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        public void PositionNearOwner(Window owner)
        {
            const double margin = 16;
            Left = owner.Left + owner.Width - Width - margin;
            Top = owner.Top + owner.Height - ActualHeight - margin;

            if (Left < owner.Left)
            {
                Left = owner.Left + margin;
            }

            if (Top < owner.Top)
            {
                Top = owner.Top + margin;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) => NextRequested?.Invoke();

        private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

        private void SkipButton_Click(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
    }
}
