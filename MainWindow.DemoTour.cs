using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PdfOverlayTool
{
    public partial class MainWindow
    {
        private DemoTourWindow? _demoTourWindow;
        private IReadOnlyList<DemoWalkthroughStep>? _demoTourSteps;
        private int _demoTourStepIndex;
        private double _demoTourSavedZoom = 1.0;

        internal void StartDemoWalkthrough()
        {
            if (_demoTourWindow != null)
            {
                _demoTourWindow.Activate();
                return;
            }

            _demoTourSavedZoom = _zoom;
            _demoTourSteps = BuildDemoWalkthroughSteps();
            _demoTourStepIndex = 0;

            _demoTourWindow = new DemoTourWindow
            {
                Owner = this
            };

            _demoTourWindow.NextRequested += DemoTour_Next;
            _demoTourWindow.BackRequested += DemoTour_Back;
            _demoTourWindow.SkipRequested += DemoTour_Skip;
            _demoTourWindow.Closed += (_, _) => _demoTourWindow = null;

            _demoTourWindow.Show();
            _demoTourWindow.ContentRendered += (_, _) => _demoTourWindow.PositionNearOwner(this);
            ShowDemoTourStep(_demoTourStepIndex);
        }

        private void ShowDemoTourStep(int index)
        {
            if (_demoTourWindow == null || _demoTourSteps == null)
            {
                return;
            }

            DemoWalkthroughStep step = _demoTourSteps[index];
            _demoTourWindow.SetStep(index, _demoTourSteps.Count, step.Title, step.Body);
            step.OnEnter?.Invoke(this);
            SetStatus($"Demo tour — {step.Title}");
        }

        private void DemoTour_Next()
        {
            if (_demoTourSteps == null || _demoTourWindow == null)
            {
                return;
            }

            if (_demoTourStepIndex >= _demoTourSteps.Count - 1)
            {
                DemoTour_Complete();
                return;
            }

            _demoTourSteps[_demoTourStepIndex].OnLeave?.Invoke(this);
            _demoTourStepIndex++;
            ShowDemoTourStep(_demoTourStepIndex);
        }

        private void DemoTour_Back()
        {
            if (_demoTourSteps == null || _demoTourWindow == null || _demoTourStepIndex <= 0)
            {
                return;
            }

            _demoTourSteps[_demoTourStepIndex].OnLeave?.Invoke(this);
            _demoTourStepIndex--;
            ShowDemoTourStep(_demoTourStepIndex);
        }

        private void DemoTour_Skip()
        {
            _sessionTelemetry.RecordDemoSkipped();
            DemoTour_End(restoreZoom: true);
            SetStatus("Demo tour skipped.");
        }

        private void DemoTour_Complete()
        {
            _sessionTelemetry.RecordDemoViewed();
            DemoTour_End(restoreZoom: true);
            SetStatus("Demo tour complete. Load your own files or re-run the tour from Help.");
        }

        private void DemoTour_End(bool restoreZoom)
        {
            if (_demoTourWindow != null)
            {
                _demoTourWindow.Close();
                _demoTourWindow = null;
            }

            _demoTourSteps = null;

            if (restoreZoom)
            {
                _zoom = _demoTourSavedZoom;
                ApplyCurrentZoom();
            }

            ResetOverlay_Click(this, new RoutedEventArgs());
        }

        internal void LoadDemoFilesForTour()
        {
            string baseDemoPath = GetDemoFilePath("Demo_Rev A.pdf");
            string overlayDemoPath = GetDemoFilePath("Demo_Rev B.pdf");

            bool baseLoaded = File.Exists(baseDemoPath);
            bool overlayLoaded = File.Exists(overlayDemoPath);

            if (baseLoaded)
            {
                LoadFileIntoPane(_basePane, baseDemoPath);
            }

            if (overlayLoaded)
            {
                LoadFileIntoPane(_overlayPane, overlayDemoPath);
            }

            if (!baseLoaded && !overlayLoaded)
            {
                SetStatus("Demonstration files could not be found.", isError: true);
            }
        }

        private void DemoTour_FitToWindowDeferred()
        {
            Dispatcher.BeginInvoke(() =>
            {
                FitToWindow_Click(this, new RoutedEventArgs());
            }, DispatcherPriority.Loaded);
        }

        private void DemoTour_AdjustZoom(double factor)
        {
            _zoom = Math.Clamp(_zoom * factor, ZOOM_MIN, ZOOM_MAX);
            ApplyCurrentZoom();
        }

        private static IReadOnlyList<DemoWalkthroughStep> BuildDemoWalkthroughSteps()
        {
            return new DemoWalkthroughStep[]
            {
                new(
                    "Welcome",
                    "This tour loads the included Demo Rev A (base) and Demo Rev B (overlay) PDFs, "
                    + "then walks through the main comparison tools. Click Next to load the samples and fit them to the window.",
                    onEnter: window =>
                    {
                        window.LoadDemoFilesForTour();
                        window.DemoTour_FitToWindowDeferred();
                    }),

                new(
                    "Tinted overlay",
                    "Matching content appears dark. Additions and removals show in the overlay tint "
                    + "(green/red by default; blue/orange when color-blind friendly is enabled in Settings). "
                    + "Toggle Tint in MANUAL mode to compare tinted vs. plain rendering.",
                    onEnter: window =>
                    {
                        if (window.TintImagesCheckBox != null)
                        {
                            window.TintImagesCheckBox.IsChecked = true;
                        }
                    }),

                new(
                    "Page both documents",
                    "The top row pages the base and overlay together — use the < > buttons or the Left / Right arrow keys. "
                    + "Watch both page numbers advance in sync.",
                    onEnter: window => window.ChangePages(1)),

                new(
                    "Overlay-only paging",
                    "The overlay row's < > buttons change only the overlay page. "
                    + "Use this when a revision adds or drops sheets and you need to offset the overlay relative to the base.",
                    onEnter: window =>
                    {
                        window.ChangeOverlayPage(1);
                        window.Dispatcher.BeginInvoke(
                            () => window.ChangeOverlayPage(-1),
                            DispatcherPriority.Background);
                    }),

                new(
                    "Drag to align",
                    "Click and drag the overlay image to line up the two revisions. "
                    + "Try moving it now, then click Next when you're ready to continue.",
                    onEnter: window => window.SetStatus("Demo tour — drag the overlay to align the revisions.")),

                new(
                    "Precise nudge",
                    "Hold Ctrl and use the arrow keys to move the overlay one pixel at a time for fine registration. "
                    + "The tour will nudge the overlay slightly so you can see the offset readout.",
                    onEnter: window =>
                    {
                        if (window.XOffsetSlider != null)
                        {
                            window.XOffsetSlider.Value += 8;
                        }

                        if (window.YOffsetSlider != null)
                        {
                            window.YOffsetSlider.Value += 4;
                        }
                    }),

                new(
                    "Scale",
                    "The Scale slider (or Ctrl + Alt + mouse wheel over the viewer) resizes the overlay relative to the base (75–125%). "
                    + "The tour sets scale to 110% as an example.",
                    onEnter: window =>
                    {
                        if (window.ScaleSlider != null)
                        {
                            window.ScaleSlider.Value = 110;
                        }
                    }),

                new(
                    "Opacity",
                    "The Opacity slider fades the base when moved left, or the overlay when moved right — "
                    + "handy for inspecting one revision at a time. The tour fades the base slightly.",
                    onEnter: window =>
                    {
                        if (window.OpacitySlider != null)
                        {
                            window.OpacitySlider.Value = -35;
                        }
                    }),

                new(
                    "Rotate",
                    "Use ↻ for 90° clockwise steps; the small slider adds ±5° fine correction for skewed scans. "
                    + "The tour applies a sample rotation.",
                    onEnter: window =>
                    {
                        window._overlayQuarterTurns = 1;
                        if (window.RotateFineSlider != null)
                        {
                            window.RotateFineSlider.Value = 2;
                        }

                        window.ApplyOverlayRotation();
                    }),

                new(
                    "Zoom and pan",
                    "Ctrl + mouse wheel zooms the view; Ctrl + drag pans. "
                    + "The tour zooms in slightly — try panning with Ctrl + drag afterward.",
                    onEnter: window => window.DemoTour_AdjustZoom(1.25)),

                new(
                    "Fit to window",
                    "Fit to Window scales the view so the current page fills the viewer. "
                    + "Click Next to run it now.",
                    onEnter: window => window.DemoTour_FitToWindowDeferred()),

                new(
                    "AUTO performance",
                    "AUTO mode (default) adjusts DPI and page cache under memory pressure. "
                    + "The top row shows the current readout; the AUTO button turns amber while reducing quality and green while restoring. "
                    + "Switch to MANUAL to control DPI, Page Cache, and Sensitivity directly.",
                    onEnter: window => window.SetAutoManualMode(window._isAutoMode)),

                new(
                    "Reset overlay",
                    "Reset returns opacity, scale, position, and rotation to neutral. "
                    + "Click Next to reset the overlay now.",
                    onEnter: window => window.ResetOverlay_Click(window, new RoutedEventArgs())),

                new(
                    "You're ready",
                    "Load your own PDFs or images with Load Base / Load Overlay, or drop files onto the viewer "
                    + "(left half = base, right half = overlay). Open Settings for themes, and re-run this tour anytime from Help → Demo Tour."),
            };
        }
    }

    internal sealed class DemoWalkthroughStep
    {
        public DemoWalkthroughStep(string title, string body, Action<MainWindow>? onEnter = null, Action<MainWindow>? onLeave = null)
        {
            Title = title;
            Body = body;
            OnEnter = onEnter;
            OnLeave = onLeave;
        }

        public string Title { get; }

        public string Body { get; }

        public Action<MainWindow>? OnEnter { get; }

        public Action<MainWindow>? OnLeave { get; }
    }
}
