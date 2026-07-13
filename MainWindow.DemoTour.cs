using System.IO;
using System.Windows;

namespace PdfOverlayTool
{
    public partial class MainWindow
    {
        private DemoTourWindow? _demoTourWindow;
        private IReadOnlyList<DemoWalkthroughStep>? _demoTourSteps;
        private int _demoTourStepIndex;

        internal void StartDemoWalkthrough()
        {
            if (_demoTourWindow != null)
            {
                _demoTourWindow.Activate();
                return;
            }

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

            LoadDemoFilesForTour();

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
            _sessionTelemetry.RecordDemoTourStepViewed(index + 1);
            SetStatus($"Demo tour: {step.Title}");
        }

        private void DemoTour_Next()
        {
            if (_demoTourSteps == null || _demoTourWindow == null)
            {
                return;
            }

            if (_demoTourStepIndex >= _demoTourSteps.Count - 1)
            {
                FocusForDemoTour();
                DemoTour_Complete();
                return;
            }

            _demoTourStepIndex++;
            ShowDemoTourStep(_demoTourStepIndex);
            FocusForDemoTour();
        }

        private void DemoTour_Back()
        {
            if (_demoTourSteps == null || _demoTourWindow == null || _demoTourStepIndex <= 0)
            {
                return;
            }

            _demoTourStepIndex--;
            ShowDemoTourStep(_demoTourStepIndex);
        }

        private void DemoTour_Skip()
        {
            _sessionTelemetry.RecordDemoSkipped();
            DemoTour_End();
            SetStatus("Demo tour exited.");
        }

        private void DemoTour_Complete()
        {
            _sessionTelemetry.RecordDemoViewed();
            DemoTour_End();
            SetStatus("Demo tour complete. Load your own files or re-run the tour from Help.");
        }

        private void DemoTour_End()
        {
            if (_demoTourWindow != null)
            {
                _demoTourWindow.Close();
                _demoTourWindow = null;
            }

            _demoTourSteps = null;
        }

        private void FocusForDemoTour()
        {
            Activate();
            Focus();
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

        private static IReadOnlyList<DemoWalkthroughStep> BuildDemoWalkthroughSteps()
        {
            return new DemoWalkthroughStep[]
            {
                new(
                    "Welcome",
                    "Demo Rev A (base) and Demo Rev B (overlay) are loaded for you. "
                    + "This tour walks through the main comparison tools. Try each feature as it comes up."),

                new(
                    "Zoom and pan",
                    "Ctrl + mouse wheel zooms the view; Ctrl + drag pans. "
                    + "Zoom in and pan around the comparison."),

                new(
                    "Drag to align",
                    "Click and drag the overlay image to line up the two revisions. "
                    + "Try to line up the circles."),

                new(
                    "Precise nudge",
                    "Zoom in, then hold Ctrl and use the arrow keys to move the overlay one pixel at a time."),

                new(
                    "Scale",
                    "The Scale slider (or Ctrl + Alt + mouse wheel over the viewer) resizes the overlay relative to the base (75-125%). "
                    + "Use both methods to scale until the triangles are the same size."),

                new(
                    "Reset overlay",
                    "Reset returns opacity, scale, position, and rotation to neutral. "
                    + "Use it whenever you want to clear your overlay adjustments and start over."),

                new(
                    "Rotate",
                    "Use ↻ for 90° clockwise steps; the small slider adds ±5° fine correction for skewed scans. "
                    + "Rotate the hexagons to align."),

                new(
                    "AUTO performance",
                    "AUTO mode (default) adjusts DPI and page cache to control memory use. "
                    + "Current memory use is indicated below the AUTO button: the first value is app cache as a percent of system RAM, "
                    + "the number in parentheses is total cache size in megabytes, and the value after the slash is overall system memory load. "
                    + "The AUTO button turns amber while reducing quality and green while restoring. "
                    + "Click the AUTO button to switch to MANUAL to control DPI and Page Cache directly."),

                new(
                    "Sensitivity",
                    "In MANUAL mode, the Sensitivity slider sets the black/white threshold when rendering PDFs. "
                    + "Adjust it to tune how fine and heavy lines appear against the background. "
                    + "If you reduce sensitivity too far, fine lines may disappear; if you increase it too far, "
                    + "lines become so heavy that details are no longer visible."),

                new(
                    "Page both documents",
                    "The top row pages the base and overlay together. Use the < > buttons or the Left / Right arrow keys. "
                    + "Advance one page and watch both page numbers move in sync."),

                new(
                    "Overlay-only paging",
                    "The overlay row's < > buttons change only the overlay page. "
                    + "Use this when a revision adds or drops sheets and you need to offset the overlay relative to the base. "
                    + "The demo overlay has an extra page inserted at page 3. Adjust the overlay page to page 4 while leaving the base page at page 3, "
                    + "then use the upper arrows to continue paging forward through both documents."),

                new(
                    "Load your own images",
                    "Click Load Base and choose your file to open the base document.\n\n"
                    + "If you have a revision of the base image in the same folder, check Revs Only, then double-click on the revision you want to load. "
                    + "Revs Only filters the overlay list to files that share the base name up to the revision separator "
                    + "(default _, as in Drawing_Rev A.pdf and Drawing_Rev B.pdf). "
                    + "Change the separator in Settings if your files use a different pattern.\n\n"
                    + "You can quickly load files from an open folder with drag and drop: drop a PDF or image on the left half of the viewer for the base, "
                    + "or on the right half for the overlay."),

                new(
                    "Settings",
                    "Open Settings to choose a color palette, enable color-blind friendly overlay tints, and adjust other preferences. "
                    + "Use Reset All Settings to Default to restore the original app preferences."),

                new(
                    "Tour complete",
                    "You can run this tour again anytime from Help, Demo Tour."),
            };
        }
    }

    internal sealed class DemoWalkthroughStep
    {
        public DemoWalkthroughStep(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }

        public string Body { get; }
    }
}
