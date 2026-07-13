using Microsoft.Win32;
using PDFtoImage;
using SkiaSharp;
using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace PdfOverlayTool
{
    public partial class MainWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // Zoom
        private const double ZOOM_STEP = 1.05;
        private const double ZOOM_MIN = 0.1;
        private const double ZOOM_MAX = 10.0;
        private const double SCALE_WHEEL_STEP = 1.0;

        // Auto memory-management thresholds / timings
        private const double CACHE_MEMORY_PERCENT_THRESHOLD = 25.0;
        private const double SYSTEM_MEMORY_LOAD_THRESHOLD = 90.0;
        // Hysteresis band: recovery only starts well below the reduction thresholds.
        private const double CACHE_RECOVERY_PERCENT_THRESHOLD = 15.0;
        private const double SYSTEM_RECOVERY_LOAD_THRESHOLD = 80.0;
        private const int CACHE_ADJUST_DELAY_SECONDS = 2;
        private const int AUTO_ADJUST_COOLDOWN_SECONDS = 2;

        // Linked AUTO quality levels: cache and DPI stepped together (DPI fixed at 250 through level 4).
        // Final descent splits cache then DPI: (1,200) → (0,200) → (0,150).
        private static readonly (double Cache, double Dpi)[] AutoQualityLevels =
        {
            (0, 150), // level 0 — floor
            (0, 200), // level 1 — cache stepdown from 1
            (1, 200), // level 2
            (2, 225), // level 3
            (3, 250), // level 4
            (4, 250), // level 5
            (5, 250), // level 6 — default
        };

        private const int MaxAutoQualityLevel = 6;

        private const string LOW_MEMORY_MESSAGE = "Low memory - AUTO performance reduction!";
        private const string RECOVERY_MESSAGE = "AUTO restoring quality - memory headroom available.";

        // Indeterminate processing bar geometry (must match MainWindow.xaml).
        private const double PROCESSING_BAR_WIDTH = 70;
        private const double PROCESSING_BAR_TRACK_WIDTH = 200;

        // Max trailing characters of a file path shown in the status message.
        private const int PATH_STATUS_MAX_LENGTH = 50;

        private bool _isDragging;
        private bool _isMemorySliderDragging;
        private bool _isAutoMode = true;
        private int _processingOperationCount;
        private Point _lastMousePosition;
        private double _zoom = 1.0;
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        private Dictionary<(string path, int page, string role), BitmapSource> _displayCache = new();
        private Dictionary<string, byte[]> _pdfCache = new();
        private readonly object _displayCacheLock = new();
        private readonly object _pdfCacheLock = new();
        private readonly Dictionary<(string path, int page), object> _renderGates = new();
        private readonly object _renderGatesLock = new();

        // Running total of display-cache bytes, maintained on every insert/remove so
        // memory checks don't have to iterate the cache (they run on every page fetch).
        private long _estimatedCacheBytes;
        private long _estimatedPdfCacheBytes;

        private int selectedDpi = 250;
        private int _renderDpiForCurrentDisplay = 250;
        private int imageThreshold = 200;

        private double setOffset = 5; // Number of adjacent pages to preload
        private DispatcherTimer? _autoMemoryAdjustmentTimer;
        private int _autoQualityLevel = MaxAutoQualityLevel;
        private int _autoPressureSeconds;
        private int _autoRecoverySeconds;
        private bool _autoPerformanceReductionActive;
        private bool _autoPerformanceRecoveryActive;
        private bool _memoryPressureActive;
        private int _autoAdjustCooldownSeconds;

        // Whole 90-degree turns of the overlay (0-3); fine skew comes from RotateFineSlider.
        private int _overlayQuarterTurns;

        private bool _isRestoringSettings;
        private bool _settingsReady;
        private int _lastWeeklyReminderWeekKey;
        private bool _registrationComplete;
        private string _userName = "";
        private string _userEmail = "";
        private string _installId = "";
        private bool _installIdNeedsSave;
        private bool _termsAccepted;
        private string _termsVersion = "";
        private string _termsAcceptedUtc = "";
        private DateTime _sessionStartUtc;

        private readonly SessionTelemetry _sessionTelemetry = new();

        private ColorPaletteSelection _colorPaletteSelection = ColorPaletteSelection.Default;

        private DocumentPane _basePane = null!;
        private DocumentPane _overlayPane = null!;

        public MainWindow()
        {
            InitializeComponent();

            _basePane = new DocumentPane("base", "Base", ColorPalette.GetBaseTintColor(ColorPaletteSelection.Default), BaseImage, BasePageTextBox, BasePageCount, BaseFileName);
            _overlayPane = new DocumentPane("overlay", "Overlay", ColorPalette.GetOverlayTintColor(ColorPaletteSelection.Default), OverlayImage, OverlayPageTextBox, OverlayPageCount, OverlayFileName);

            RestoreUserSettings();
            _settingsReady = true;

            if (_installIdNeedsSave)
            {
                SaveUserSettings();
            }

            UseFilteredOverlayPickerCheckbox.IsEnabled = false;
            InitializeAutoMemoryAdjustmentTimer();

            Closing += (_, _) =>
            {
                SaveUserSettings();
                SendSessionTelemetryIfNeeded();
            };

            Loaded += (s, e) =>
            {
                ShowRegistrationIfNeeded();
                ShowBetaSplashIfNeeded();
                ApplyBetaFileLoadingMode();
                UpdatePageInputState();
                UpdatePageNavigationButtons();
                UpdateOverlayNavigationButtons();
                ApplyOverlaySettings();
                UpdateMemoryUsageDisplay();
                SetAutoManualMode(_isAutoMode);

                if (BetaConfig.IsFileLoadingDisabled)
                {
                    LoadDemoFiles();
                }
                else
                {
                    SetStatus("Load a base PDF and/or overlay PDF to begin.");
                }

                RecordSessionStart();
            };
        }

        private void ShowRegistrationIfNeeded()
        {
            if (_registrationComplete && _termsAccepted && _termsVersion == BetaTerms.Version)
            {
                return;
            }

            var registration = new RegistrationWindow
            {
                Owner = this
            };

            if (registration.ShowDialog() != true)
            {
                return;
            }

            _registrationComplete = true;
            _userName = registration.UserName;
            _userEmail = registration.UserEmail;
            _termsAccepted = registration.TermsAccepted;
            _termsVersion = registration.TermsVersion;
            _termsAcceptedUtc = registration.TermsAcceptedUtc.ToString("o");
            SaveUserSettings();

            GoogleSheetTelemetry.SendRegistration(
                _installId,
                _userName,
                _userEmail,
                _termsAccepted,
                _termsVersion,
                registration.TermsAcceptedUtc);
        }

        private void RecordSessionStart()
        {
            if (!_registrationComplete)
            {
                return;
            }

            _sessionStartUtc = DateTime.UtcNow;
            _sessionTelemetry.Reset();
            TelemetryContext.BeginSession(_sessionStartUtc);
            TelemetryContext.RegisterSessionReporting(
                () => _sessionTelemetry.CreateCloseSnapshot(BuildSessionSettingsSnapshot()),
                () => _isAutoMode);
        }

        private void SendSessionTelemetryIfNeeded()
        {
            if (!_registrationComplete || _sessionStartUtc == default || TelemetryContext.CrashReported)
            {
                return;
            }

            int sessionSeconds = Math.Max(0, (int)(DateTime.UtcNow - _sessionStartUtc).TotalSeconds);
            SessionCloseSnapshot snapshot = _sessionTelemetry.CreateCloseSnapshot(BuildSessionSettingsSnapshot());
            GoogleSheetTelemetry.SendSession(
                _installId,
                _userName,
                _userEmail,
                _isAutoMode,
                sessionSeconds,
                _termsAccepted,
                _termsVersion,
                _termsAcceptedUtc,
                snapshot);
        }

        private SessionSettingsSnapshot BuildSessionSettingsSnapshot()
        {
            return new SessionSettingsSnapshot
            {
                Opacity = OpacitySlider?.Value ?? 50,
                Dpi = DpiSlider?.Value ?? 250,
                PageCache = CacheSizeSlider?.Value ?? 5,
                Sensitivity = ImageThresholdSlider?.Value ?? 200,
                IsAutoMode = _isAutoMode,
                OverlayOnlyRevisions = UseFilteredOverlayPickerCheckbox?.IsChecked == true,
                TintEnabled = TintImagesCheckBox?.IsChecked == true,
                ColorBlindFriendly = _colorPaletteSelection.ColorBlindFriendly,
                ColorPaletteName = ColorPalette.GetThemeName(_colorPaletteSelection.Theme)
            };
        }

        private void ShowBetaSplashIfNeeded()
        {
            bool fileLoadingDisabled = BetaConfig.IsFileLoadingDisabled;
            bool showSplash;

            if (fileLoadingDisabled)
            {
                showSplash = true;
            }
            else
            {
                int currentWeekKey = BetaConfig.GetCurrentWeekKey();
                showSplash = _lastWeeklyReminderWeekKey != currentWeekKey;

                if (showSplash)
                {
                    _lastWeeklyReminderWeekKey = currentWeekKey;
                    SaveUserSettings();
                }
            }

            if (!showSplash)
            {
                return;
            }

            var splash = new SplashWindow(fileLoadingDisabled)
            {
                Owner = this
            };
            splash.ShowDialog();
        }

        private void ApplyBetaFileLoadingMode()
        {
            bool fileLoadingDisabled = BetaConfig.IsFileLoadingDisabled;

            if (LoadBaseButton != null)
            {
                LoadBaseButton.IsEnabled = !fileLoadingDisabled;
            }

            if (LoadOverlayButton != null)
            {
                LoadOverlayButton.IsEnabled = !fileLoadingDisabled;
            }

            if (ViewerBorder != null)
            {
                ViewerBorder.AllowDrop = !fileLoadingDisabled;
            }

            UpdateViewerBorderToolTip();
        }

        private bool HasAnyDocumentLoaded()
        {
            return !string.IsNullOrWhiteSpace(_basePane?.FilePath)
                || !string.IsNullOrWhiteSpace(_overlayPane?.FilePath);
        }

        private void UpdateViewerBorderToolTip()
        {
            if (ViewerBorder == null)
            {
                return;
            }

            if (HasAnyDocumentLoaded())
            {
                ToolTipService.SetIsEnabled(ViewerBorder, false);
                return;
            }

            ToolTipService.SetIsEnabled(ViewerBorder, true);
            ViewerBorder.ToolTip = BetaConfig.IsFileLoadingDisabled
                ? "Demonstration mode: only the included demo files are loaded. Ctrl + mouse wheel zooms; Ctrl + drag pans."
                : "Drop a PDF or image here: left half loads base, right half loads overlay. Ctrl + mouse wheel zooms; Ctrl + drag pans.";
        }

        private static bool IsDemoFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(filePath);
            return fullPath.Equals(Path.GetFullPath(GetDemoFilePath("Demo_Rev A.pdf")), StringComparison.OrdinalIgnoreCase)
                || fullPath.Equals(Path.GetFullPath(GetDemoFilePath("Demo_Rev B.pdf")), StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanLoadUserSelectedFile(string? filePath) =>
            !BetaConfig.IsFileLoadingDisabled || IsDemoFilePath(filePath);

        private static string GetDemoFilePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "Resources", fileName);

        private void LoadDemoFiles()
        {
            if (!BetaConfig.IsFileLoadingDisabled)
            {
                return;
            }

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

            if (baseLoaded || overlayLoaded)
            {
                _sessionTelemetry.RecordDemoAutoLoaded();
            }

            if (!baseLoaded && !overlayLoaded)
            {
                SetStatus("Demonstration files could not be found.");
            }

            UpdateViewerBorderToolTip();
        }

        private void LoadBaseFile_Click(object sender, RoutedEventArgs e)
        {
            LoadFileIntoPane(_basePane, SelectPdfOrImageFile());
        }

        private void LoadOverlayFile_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = UseFilteredOverlayPickerCheckbox?.IsChecked == true
                ? SelectOverlayFileFiltered()
                : SelectPdfOrImageFile();

            LoadFileIntoPane(_overlayPane, filePath);
        }

        private void LoadFileIntoPane(DocumentPane pane, string? filePath)
        {
            if (filePath == null || !CanLoadUserSelectedFile(filePath))
            {
                return;
            }

            ClearCachesForFile(pane.FilePath);
            pane.FilePath = filePath;
            pane.PageCount = GetPdfPageCount(filePath);
            pane.PageTextBox.Text = "1";

            DocumentPane otherPane = pane == _basePane ? _overlayPane : _basePane;
            if (!string.IsNullOrWhiteSpace(otherPane.FilePath)
                && GetPageNumber(otherPane.PageTextBox.Text) != 1)
            {
                otherPane.PageTextBox.Text = "1";
                LoadPage(otherPane);
            }

            RecordFileOpenTelemetry(pane, filePath);
            bool fitToWindowOnLoad = pane == _basePane
                || string.IsNullOrWhiteSpace(_basePane?.FilePath);
            LoadPage(pane, fitToWindowOnLoad: fitToWindowOnLoad);
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();
            UpdatePageInputState();

            SetStatus($"Loaded {pane.Role} file: {TrimPathForDisplay(filePath)}");
            pane.FileNameTextBlock.Text = Path.GetFileNameWithoutExtension(filePath);
            pane.PageCountRun.Text = pane.PageCount?.ToString() ?? "0";
            UpdateViewerBorderToolTip();
        }

        private void RecordFileOpenTelemetry(DocumentPane pane, string filePath)
        {
            try
            {
                long fileBytes = new FileInfo(filePath).Length;
                double sizeMegabytes = fileBytes / (1024.0 * 1024.0);
                int pageCount = pane.PageCount ?? 1;
                _sessionTelemetry.RecordFileOpen(sizeMegabytes, pageCount);
            }
            catch
            {
                // Non-fatal: telemetry must never affect file loading.
            }
        }

        private void ReloadPages_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(_basePane);
            LoadPage(_overlayPane);
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            SetStatus("Reloaded selected pages.");
        }

        private void LoadPage(DocumentPane pane, bool fitToWindowOnLoad = false)
        {
            if (string.IsNullOrWhiteSpace(pane.FilePath))
            {
                return;
            }

            string filePath = pane.FilePath;
            int pageIndex = GetPageIndexFromTextBox(pane.PageTextBox?.Text);
            bool useTint = TintImagesCheckBox?.IsChecked == true;

            // Newer LoadPage calls for the same pane invalidate any render still in
            // flight, so a slow page can never overwrite a page requested after it.
            // Only ever touched on the UI thread.
            int loadVersion = ++pane.LoadVersion;

            // Fast path: display bitmap already cached for this pane.
            BitmapSource? cached = TryGetCachedDisplayBitmap(filePath, pageIndex, pane.Role);
            if (cached != null)
            {
                ApplyLoadedPage(pane, filePath, pageIndex, cached, fitToWindowOnLoad);
                return;
            }

            // Slow path: render off the UI thread so page turns never freeze the window.
            SetProcessingState(true);
            Color tintColor = pane.TintColor;
            Task.Run(() =>
            {
                try
                {
                    BitmapSource display = GetDisplayBitmap(
                        filePath,
                        pageIndex,
                        pane.Role,
                        tintColor,
                        useTint);

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (loadVersion == pane.LoadVersion)
                        {
                            ApplyLoadedPage(pane, filePath, pageIndex, display, fitToWindowOnLoad);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        SetStatus($"Could not load {pane.Role} page: {ex.Message}", isError: true)));
                }
                finally
                {
                    SetProcessingState(false);
                }
            });
        }

        private void ApplyLoadedPage(
            DocumentPane pane,
            string filePath,
            int pageIndex,
            BitmapSource displayBitmap,
            bool fitToWindowOnLoad = false)
        {
            pane.ImageControl.Source = displayBitmap;

            if (pane == _basePane)
            {
                UseFilteredOverlayPickerCheckbox.IsEnabled = true;
            }
            else
            {
                // The rotation pivot is the image center, so it must follow size changes
                // when a different page or file lands in the overlay.
                ApplyOverlayRotation();
            }

            if (fitToWindowOnLoad)
            {
                _renderDpiForCurrentDisplay = selectedDpi;
                FitToWindowDeferred();
            }
            else if (pane == _basePane)
            {
                _renderDpiForCurrentDisplay = selectedDpi;
                ApplyCurrentZoomDeferred();
            }

            PrunePageCache(filePath, pageIndex);
            PreloadAdjacentPages(pane, filePath, pageIndex);

            // A successful load/navigation always shows black; red is only used to
            // flag a *failed* page-change attempt (see TryAdvancePane).
            pane.PageTextBox.Foreground = Brushes.Black;

            UpdateMemoryUsageDisplay();
        }

        private void SetProcessingState(bool isProcessing)
        {
            if (ProcessingSpinner == null)
            {
                return;
            }

            if (isProcessing)
            {
                Interlocked.Increment(ref _processingOperationCount);
            }
            else if (Interlocked.CompareExchange(ref _processingOperationCount, 0, 0) > 0)
            {
                Interlocked.Decrement(ref _processingOperationCount);
            }

            bool showSpinner = Interlocked.CompareExchange(ref _processingOperationCount, 0, 0) > 0;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (showSpinner)
                {
                    ProcessingSpinner.Visibility = Visibility.Visible;
                    StartProcessingAnimation();
                }
                else
                {
                    StopProcessingAnimation();
                    ProcessingSpinner.Visibility = Visibility.Collapsed;
                }
            }));
        }

        private void StartProcessingAnimation()
        {
            if (ProcessingSpinnerTransform == null)
            {
                return;
            }

            // Slide the accent segment across the (clipped) track and repeat: a classic
            // indeterminate "still working" bar. Linear so the loop boundary is seamless.
            var slide = new DoubleAnimation
            {
                From = -PROCESSING_BAR_WIDTH,
                To = PROCESSING_BAR_TRACK_WIDTH,
                Duration = TimeSpan.FromSeconds(1.1),
                RepeatBehavior = RepeatBehavior.Forever
            };
            ProcessingSpinnerTransform.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private void StopProcessingAnimation()
        {
            ProcessingSpinnerTransform?.BeginAnimation(TranslateTransform.XProperty, null);
        }

        // Keep the status message short so it never crowds the centered "still working"
        // bar: show at most the trailing PATH_STATUS_MAX_LENGTH characters of the path,
        // prefixed with an ellipsis when trimmed.
        private static string TrimPathForDisplay(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || filePath.Length <= PATH_STATUS_MAX_LENGTH)
            {
                return filePath;
            }

            return "..." + filePath.Substring(filePath.Length - PATH_STATUS_MAX_LENGTH);
        }

        private int GetPageIndexFromTextBox(string? text)
        {
            if (!int.TryParse(text, out int pageNumber))
            {
                return 0;
            }

            if (pageNumber < 1)
            {
                return 0;
            }

            return pageNumber - 1;
        }

        private static readonly string[] SupportedFileExtensions =
            { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

        private static string? GetDroppedFilePath(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return null;
            }

            string filePath = files[0];
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedFileExtensions.Contains(extension) ? filePath : null;
        }

        private void Viewer_DragOver(object sender, DragEventArgs e)
        {
            if (BetaConfig.IsFileLoadingDisabled)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = GetDroppedFilePath(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Viewer_Drop(object sender, DragEventArgs e)
        {
            string? filePath = GetDroppedFilePath(e);
            if (filePath == null)
            {
                return;
            }

            // Left half of the viewer loads the base document, right half the overlay.
            bool isLeftHalf = e.GetPosition(ViewerBorder).X < ViewerBorder.ActualWidth / 2;
            LoadFileIntoPane(isLeftHalf ? _basePane : _overlayPane, filePath);
            e.Handled = true;
        }

        private string? SelectPdfOrImageFile()
        {
            if (BetaConfig.IsFileLoadingDisabled)
            {
                return null;
            }

            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select PDF or image file",
                Filter =
                    "PDF and image files (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|" +
                    "PDF files (*.pdf)|*.pdf|" +
                    "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|" +
                    "All files (*.*)|*.*"
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        private BitmapSource LoadFileAsBitmapSource(string filePath, int pageIndex)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".pdf")
            {
                return RenderPdfPageToBitmapSource(filePath, pageIndex);
            }

            return LoadImageFileAsBitmapSource(filePath);
        }

        private BitmapSource LoadImageFileAsBitmapSource(string filePath)
        {
            BitmapImage bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private BitmapSource RenderPdfPageToBitmapSource(string pdfFilePath, int pageIndex)
        {
            byte[] pdfBytes = GetPdfBytes(pdfFilePath);

            var options = new PDFtoImage.RenderOptions
            {
                Dpi = selectedDpi,
            };

            using SKBitmap sourceBitmap = Conversion.ToImage(pdfBytes, pageIndex, null, options);

            var info = new SKImageInfo(
                sourceBitmap.Width,
                sourceBitmap.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            using SKBitmap targetBitmap = new SKBitmap(info);

            using (SKCanvas canvas = new SKCanvas(targetBitmap))
            {
                canvas.Clear(SKColors.Transparent);

                using SKImage sourceImage = SKImage.FromBitmap(sourceBitmap);

                var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

                var destinationRect = new SKRect(
                    0,
                    0,
                    targetBitmap.Width,
                    targetBitmap.Height);

                canvas.DrawImage(
                    sourceImage,
                    destinationRect,
                    sampling);

                canvas.Flush();
            }

            int stride = targetBitmap.RowBytes;
            int byteCount = targetBitmap.ByteCount;

            byte[] pixels = new byte[byteCount];

            Marshal.Copy(
                targetBitmap.GetPixels(),
                pixels,
                0,
                byteCount);

            int threshold = imageThreshold;
            int width = targetBitmap.Width;
            int height = targetBitmap.Height;

            // Rows are independent, so binarize them in parallel; this pass touches every
            // pixel of a page-sized bitmap and dominates render time after rasterization.
            Parallel.For(0, height, y =>
            {
                int rowStart = y * stride;
                int rowEnd = rowStart + width * 4;

                for (int i = rowStart; i < rowEnd; i += 4)
                {
                    byte blue = pixels[i];
                    byte green = pixels[i + 1];
                    byte red = pixels[i + 2];

                    // simple brightness
                    int brightness = (red * 299 + green * 587 + blue * 114) / 1000;

                    byte value = (brightness < threshold) ? (byte)0 : (byte)255;

                    pixels[i] = value; // B
                    pixels[i + 1] = value; // G
                    pixels[i + 2] = value; // R
                    pixels[i + 3] = 255;   // full alpha
                }
            });

            BitmapSource bitmap = BitmapSource.Create(
                targetBitmap.Width,
                targetBitmap.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            bitmap.Freeze();

            return bitmap;
        }

        private void TintImagesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_basePane == null || _overlayPane == null)
            {
                return;
            }

            // Cached bitmaps are either tinted or untinted — not both — so mode changes require a fresh cache.
            ClearRenderCaches();

            if (!string.IsNullOrWhiteSpace(_basePane.FilePath))
            {
                LoadPage(_basePane);
            }

            if (!string.IsNullOrWhiteSpace(_overlayPane.FilePath))
            {
                LoadPage(_overlayPane);
            }
        }

        private BitmapSource CreateTintedImage(BitmapSource source, Color tintColor)
        {
            // Rendered PDF pages are already Bgra32, so converting again would just be
            // an extra full-page copy; only convert other formats (loaded image files).
            BitmapSource convertedSource = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = convertedSource.PixelWidth;
            int height = convertedSource.PixelHeight;
            int bytesPerPixel = 4;
            int stride = width * bytesPerPixel;

            byte[] sourcePixels = new byte[height * stride];
            byte[] outputPixels = new byte[height * stride];

            convertedSource.CopyPixels(sourcePixels, stride, 0);

            byte tintBlue = tintColor.B;
            byte tintGreen = tintColor.G;
            byte tintRed = tintColor.R;

            // Full-page per-pixel pass; rows are independent, so run them in parallel.
            Parallel.For(0, height, y =>
            {
                int rowStart = y * stride;
                int rowEnd = rowStart + width * bytesPerPixel;

                for (int i = rowStart; i < rowEnd; i += bytesPerPixel)
                {
                    byte blue = sourcePixels[i];
                    byte green = sourcePixels[i + 1];
                    byte red = sourcePixels[i + 2];
                    byte alpha = sourcePixels[i + 3];

                    // Darker source pixels become more opaque tint (integer math:
                    // darkness = 255 - average brightness, scaled by source alpha).
                    int darkness = 255 - (red + green + blue) / 3;
                    byte outputAlpha = (byte)(darkness * alpha / 255);

                    outputPixels[i] = tintBlue;
                    outputPixels[i + 1] = tintGreen;
                    outputPixels[i + 2] = tintRed;
                    outputPixels[i + 3] = outputAlpha;
                }
            });

            BitmapSource tintedImage = BitmapSource.Create(
                width,
                height,
                convertedSource.DpiX,
                convertedSource.DpiY,
                PixelFormats.Bgra32,
                null,
                outputPixels,
                stride);

            tintedImage.Freeze();

            return tintedImage;
        }

        private void OverlayControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isRestoringSettings)
            {
                return;
            }

            ApplyOverlaySettings();
            SaveUserSettings();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            _sessionTelemetry.RecordHelpOpened();
            var helpWindow = new HelpWindow { Owner = this };
            helpWindow.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_colorPaletteSelection)
            {
                Owner = this
            };

            settingsWindow.PaletteChanged += SetColorPaletteSelection;
            settingsWindow.ResetDefaultsRequested += ResetPreferencesToDefaults;
            settingsWindow.ShowDialog();
        }

        private void SetColorPaletteSelection(ColorPaletteSelection selection)
        {
            if (_colorPaletteSelection == selection)
            {
                return;
            }

            _colorPaletteSelection = selection;
            ApplyColorPalette();
            RefreshLoadedDocumentsAfterDisplayChange();
            SaveUserSettings();
        }

        private void ResetPreferencesToDefaults()
        {
            UserSettings defaults = UserSettings.CreatePreferenceDefaults();

            _isRestoringSettings = true;
            try
            {
                if (OpacitySlider != null)
                {
                    OpacitySlider.Value = defaults.Opacity;
                }

                if (DpiSlider != null)
                {
                    DpiSlider.Value = defaults.Dpi;
                }

                if (CacheSizeSlider != null)
                {
                    CacheSizeSlider.Value = defaults.PageCache;
                }

                if (ImageThresholdSlider != null)
                {
                    ImageThresholdSlider.Value = defaults.Sensitivity;
                }

                if (UseFilteredOverlayPickerCheckbox != null)
                {
                    UseFilteredOverlayPickerCheckbox.IsChecked = defaults.OverlayOnlyRevisions;
                }

                if (TintImagesCheckBox != null)
                {
                    TintImagesCheckBox.IsChecked = true;
                }

                _isAutoMode = defaults.IsAutoMode;
                _colorPaletteSelection = ColorPaletteSelection.Default;
                _autoQualityLevel = MaxAutoQualityLevel;
            }
            finally
            {
                _isRestoringSettings = false;
            }

            SetAutoManualMode(_isAutoMode);
            ApplyColorPalette();
            ApplyOverlaySettings();
            ApplyMemorySettings(invalidateRenders: true);
            SaveUserSettings();
            SetStatus("All settings restored to defaults.");
        }

        private void ApplyColorPalette()
        {
            ColorPalette.ApplyTheme(_colorPaletteSelection, Resources);

            _basePane.SetTintColor(ColorPalette.GetBaseTintColor(_colorPaletteSelection));
            _overlayPane.SetTintColor(ColorPalette.GetOverlayTintColor(_colorPaletteSelection));

            if (BaseFileName != null)
            {
                BaseFileName.Foreground = (System.Windows.Media.Brush)FindResource("BaseFileBrush");
            }

            if (OverlayFileName != null)
            {
                OverlayFileName.Foreground = (System.Windows.Media.Brush)FindResource("OverlayFileBrush");
            }

            UpdateAutoModeButtonAppearance();
            RefreshThemeControlChrome();
        }

        private void RefreshThemeControlChrome()
        {
            var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            var textBrush = (System.Windows.Media.Brush)FindResource("TextBrush");
            var controlBackground = (System.Windows.Media.Brush)FindResource("ControlBackgroundBrush");
            var controlBorder = (System.Windows.Media.Brush)FindResource("ControlBorderBrush");
            var prominentSliderStyle = (Style)FindResource("ProminentSliderStyle");

            foreach (Slider? slider in new Slider?[]
            {
                OpacitySlider,
                ScaleSlider,
                RotateFineSlider,
                DpiSlider,
                CacheSizeSlider,
                ImageThresholdSlider,
                XOffsetSlider,
                YOffsetSlider
            })
            {
                if (slider == null)
                {
                    continue;
                }

                slider.Foreground = accentBrush;
                slider.Style = null;
                slider.Style = prominentSliderStyle;
            }

            foreach (Button? button in new Button?[]
            {
                LoadBaseButton,
                LoadOverlayButton,
                PreviousPageButton,
                NextPageButton,
                PreviousPageOffsetButton,
                NextPageOffsetButton,
                HelpButton,
                SettingsButton,
                AutoModeToggleButton
            })
            {
                if (button == null)
                {
                    continue;
                }

                button.Background = controlBackground;
                button.BorderBrush = controlBorder;
                button.Foreground = button == HelpButton || button == SettingsButton
                    ? accentBrush
                    : textBrush;
            }
        }

        private void RefreshLoadedDocumentsAfterDisplayChange()
        {
            ClearRenderCaches();

            if (!string.IsNullOrWhiteSpace(_basePane.FilePath))
            {
                LoadPage(_basePane);
            }

            if (!string.IsNullOrWhiteSpace(_overlayPane.FilePath))
            {
                LoadPage(_overlayPane);
            }
        }

        private void RestoreUserSettings()
        {
            UserSettings settings = UserSettings.Load();

            _isRestoringSettings = true;
            try
            {
                if (OpacitySlider != null)
                {
                    OpacitySlider.Value = Clamp(settings.Opacity, OpacitySlider.Minimum, OpacitySlider.Maximum);
                }

                if (DpiSlider != null)
                {
                    DpiSlider.Value = Clamp(settings.Dpi, DpiSlider.Minimum, DpiSlider.Maximum);
                }

                if (CacheSizeSlider != null)
                {
                    CacheSizeSlider.Value = Clamp(settings.PageCache, CacheSizeSlider.Minimum, CacheSizeSlider.Maximum);
                }

                if (ImageThresholdSlider != null)
                {
                    ImageThresholdSlider.Value = Clamp(
                        settings.Sensitivity,
                        ImageThresholdSlider.Minimum,
                        ImageThresholdSlider.Maximum);
                }

                if (UseFilteredOverlayPickerCheckbox != null)
                {
                    UseFilteredOverlayPickerCheckbox.IsChecked = settings.OverlayOnlyRevisions;
                }

                _isAutoMode = settings.IsAutoMode;
                _colorPaletteSelection = ColorPalette.ParseSettings(settings.ColorPaletteMode, settings.ColorBlindFriendly);
                _lastWeeklyReminderWeekKey = settings.LastWeeklyReminderWeekKey;
                _registrationComplete = settings.RegistrationComplete;
                _userName = settings.UserName ?? "";
                _userEmail = settings.UserEmail ?? "";

                if (string.IsNullOrWhiteSpace(settings.InstallId))
                {
                    _installId = Guid.NewGuid().ToString("N");
                    _installIdNeedsSave = true;
                }
                else
                {
                    _installId = settings.InstallId;
                }

                _termsAccepted = settings.TermsAccepted;
                _termsVersion = settings.TermsVersion ?? "";
                _termsAcceptedUtc = settings.TermsAcceptedUtc ?? "";
            }
            finally
            {
                _isRestoringSettings = false;
            }

            // Sync the backing fields (selectedDpi, setOffset, etc.) without a full re-render;
            // no documents are loaded yet at startup.
            ApplyMemorySettings(invalidateRenders: false);
            ApplyColorPalette();

            if (_isAutoMode)
            {
                SyncAutoQualityLevelFromSliders();
            }
        }

        private void SaveUserSettings()
        {
            if (!_settingsReady || _isRestoringSettings)
            {
                return;
            }

            var settings = new UserSettings
            {
                Opacity = OpacitySlider?.Value ?? 50,
                Dpi = DpiSlider?.Value ?? 250,
                PageCache = CacheSizeSlider?.Value ?? 5,
                Sensitivity = ImageThresholdSlider?.Value ?? 200,
                OverlayOnlyRevisions = UseFilteredOverlayPickerCheckbox?.IsChecked == true,
                IsAutoMode = _isAutoMode,
                ColorPaletteMode = ColorPalette.GetThemeName(_colorPaletteSelection.Theme),
                ColorBlindFriendly = _colorPaletteSelection.ColorBlindFriendly,
                LastWeeklyReminderWeekKey = _lastWeeklyReminderWeekKey,
                RegistrationComplete = _registrationComplete,
                UserName = _userName,
                UserEmail = _userEmail,
                InstallId = _installId,
                TermsAccepted = _termsAccepted,
                TermsVersion = _termsVersion,
                TermsAcceptedUtc = _termsAcceptedUtc
            };

            settings.Save();
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void UseFilteredOverlayPickerCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRestoringSettings)
            {
                return;
            }

            SaveUserSettings();
        }

        private void AutoModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isAutoMode = !_isAutoMode;
            SetAutoManualMode(_isAutoMode);
            SaveUserSettings();
        }

        private void SetAutoManualMode(bool isAutoMode)
        {
            if (AutoModeToggleButton != null)
            {
                AutoModeToggleButton.Content = isAutoMode ? "AUTO" : "MANUAL";
            }

            bool showAdvancedControls = !isAutoMode;

            if (AutoPerformanceReadout != null)
            {
                AutoPerformanceReadout.Visibility = isAutoMode ? Visibility.Visible : Visibility.Collapsed;
            }

            if (DpiLabel != null) DpiLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (DpiControls != null) DpiControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (PageCacheLabel != null) PageCacheLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (PageCacheControls != null) PageCacheControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (SensitivityLabel != null) SensitivityLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (SensitivityControls != null) SensitivityControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (TintImagesCheckBox != null) TintImagesCheckBox.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;

            if (isAutoMode)
            {
                SyncAutoQualityLevelFromSliders();
            }

            UpdateAutoModeButtonAppearance();
        }

        private void InitializeAutoMemoryAdjustmentTimer()
        {
            _autoMemoryAdjustmentTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoMemoryAdjustmentTimer.Tick += AutoMemoryAdjustmentTimer_Tick;
            _autoMemoryAdjustmentTimer.Start();
        }

        private void AutoMemoryAdjustmentTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isAutoMode)
            {
                _memoryPressureActive = false;
                _autoPressureSeconds = 0;
                _autoRecoverySeconds = 0;
                _autoAdjustCooldownSeconds = 0;
                _autoPerformanceReductionActive = false;
                _autoPerformanceRecoveryActive = false;
                return;
            }

            if (!TryGetMemoryThresholdState(out bool cacheOverThreshold, out bool systemOverThreshold))
            {
                return;
            }

            // Live signal (read by PreloadAdjacentPages) that suppresses speculative
            // preloading while memory is tight - that preloading is the main CPU hog.
            _memoryPressureActive = cacheOverThreshold || systemOverThreshold;

            if (_autoAdjustCooldownSeconds > 0)
            {
                _autoAdjustCooldownSeconds--;
                return;
            }

            if (_memoryPressureActive)
            {
                _autoPerformanceRecoveryActive = false;
                _autoRecoverySeconds = 0;

                if (_autoQualityLevel > 0)
                {
                    _autoPressureSeconds++;

                    if (_autoPressureSeconds >= CACHE_ADJUST_DELAY_SECONDS)
                    {
                        ApplyAutoQualityLevel(_autoQualityLevel - 1, isReduction: true);
                        _autoPressureSeconds = 0;
                    }
                }

                UpdateAutoModeButtonAppearance();
            }
            else
            {
                _autoPressureSeconds = 0;
                _autoPerformanceReductionActive = false;

                if (TryGetMemoryRecoveryState(out bool canRecover)
                    && canRecover
                    && _autoQualityLevel < MaxAutoQualityLevel)
                {
                    _autoPerformanceRecoveryActive = true;
                    _autoRecoverySeconds++;

                    if (_autoRecoverySeconds >= CACHE_ADJUST_DELAY_SECONDS)
                    {
                        ApplyAutoQualityLevel(_autoQualityLevel + 1, isReduction: false);
                        _autoRecoverySeconds = 0;
                    }
                }
                else
                {
                    _autoPerformanceRecoveryActive = false;
                    _autoRecoverySeconds = 0;
                }

                UpdateAutoModeButtonAppearance();
            }
        }

        private void SyncAutoQualityLevelFromSliders()
        {
            _autoQualityLevel = DeriveAutoQualityLevelFromSliders();
        }

        private int DeriveAutoQualityLevelFromSliders()
        {
            double cache = CacheSizeSlider?.Value ?? AutoQualityLevels[MaxAutoQualityLevel].Cache;
            double dpi = DpiSlider?.Value ?? AutoQualityLevels[MaxAutoQualityLevel].Dpi;

            for (int level = 0; level <= MaxAutoQualityLevel; level++)
            {
                if (Math.Abs(cache - AutoQualityLevels[level].Cache) < 0.5
                    && Math.Abs(dpi - AutoQualityLevels[level].Dpi) < 0.5)
                {
                    return level;
                }
            }

            // Non-table values (e.g. after MANUAL): highest level whose targets are still met.
            for (int level = MaxAutoQualityLevel; level >= 0; level--)
            {
                if (cache >= AutoQualityLevels[level].Cache - 0.5
                    && dpi >= AutoQualityLevels[level].Dpi - 0.5)
                {
                    return level;
                }
            }

            return 0;
        }

        private void ApplyAutoQualityLevel(int level, bool isReduction)
        {
            level = Math.Clamp(level, 0, MaxAutoQualityLevel);

            if (level == _autoQualityLevel
                && CacheSizeSlider != null
                && DpiSlider != null
                && Math.Abs(CacheSizeSlider.Value - AutoQualityLevels[level].Cache) < 0.5
                && Math.Abs(DpiSlider.Value - AutoQualityLevels[level].Dpi) < 0.5)
            {
                return;
            }

            _autoQualityLevel = level;
            (double cache, double dpi) = AutoQualityLevels[level];

            _isRestoringSettings = true;
            try
            {
                if (CacheSizeSlider != null)
                {
                    CacheSizeSlider.Value = cache;
                }

                if (DpiSlider != null)
                {
                    DpiSlider.Value = dpi;
                }
            }
            finally
            {
                _isRestoringSettings = false;
            }

            ApplyMemorySettings(invalidateRenders: true);
            SaveUserSettings();

            if (isReduction)
            {
                _autoPerformanceReductionActive = true;
                _sessionTelemetry.RecordAutoMemoryReductionEngaged();
                SetStatus(LOW_MEMORY_MESSAGE, isError: true);
            }
            else
            {
                _autoPerformanceRecoveryActive = level < MaxAutoQualityLevel;
                if (level < MaxAutoQualityLevel)
                {
                    _sessionTelemetry.RecordAutoMemoryRecoveryEngaged();
                    SetStatus(RECOVERY_MESSAGE);
                }
            }

            _autoAdjustCooldownSeconds = AUTO_ADJUST_COOLDOWN_SECONDS;
            UpdateAutoModeButtonAppearance();
        }

        private bool TryGetMemoryRecoveryState(out bool canRecover)
        {
            canRecover = false;

            long estimatedBytes = GetTotalAppCacheBytes();

            if (!TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes) || totalSystemMemoryBytes <= 0)
            {
                return false;
            }

            double cachePercentOfSystemMemory = estimatedBytes / (double)totalSystemMemoryBytes * 100.0;
            bool cacheComfortablyLow = cachePercentOfSystemMemory <= CACHE_RECOVERY_PERCENT_THRESHOLD;

            if (!TryGetSystemMemoryLoadPercent(out double systemMemoryLoadPercent))
            {
                return false;
            }

            bool systemComfortablyLow = systemMemoryLoadPercent <= SYSTEM_RECOVERY_LOAD_THRESHOLD;
            canRecover = cacheComfortablyLow && systemComfortablyLow;
            return true;
        }

        private bool TryGetMemoryThresholdState(out bool cacheOverThreshold, out bool systemOverThreshold)
        {
            cacheOverThreshold = false;
            systemOverThreshold = false;

            long estimatedBytes = GetTotalAppCacheBytes();

            if (TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes) && totalSystemMemoryBytes > 0)
            {
                double cachePercentOfSystemMemory = estimatedBytes / (double)totalSystemMemoryBytes * 100.0;
                cacheOverThreshold = cachePercentOfSystemMemory > CACHE_MEMORY_PERCENT_THRESHOLD;
            }

            systemOverThreshold = TryGetSystemMemoryLoadPercent(out double systemMemoryLoadPercent)
                && systemMemoryLoadPercent > SYSTEM_MEMORY_LOAD_THRESHOLD;
            return true;
        }

        private void MemoryControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isRestoringSettings)
            {
                return;
            }

            // While the thumb is being dragged, defer the (expensive) refresh until release.
            // Discrete changes (track click, keyboard) are not drags, so they apply immediately.
            if (_isMemorySliderDragging)
            {
                return;
            }

            ApplyMemorySettings(RequiresRenderInvalidation(sender));
            SaveUserSettings();
        }

        private void MemorySlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isMemorySliderDragging = true;
        }

        private void MemorySlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isMemorySliderDragging = false;
            ApplyMemorySettings(RequiresRenderInvalidation(sender));
            SaveUserSettings();
        }

        // DPI and Sensitivity change the rendered pixels, so their caches must be rebuilt.
        // The Page Cache size only affects how many pages are retained/preloaded.
        private bool RequiresRenderInvalidation(object sender)
        {
            return sender == DpiSlider || sender == ImageThresholdSlider;
        }

        private void ApplyMemorySettings(bool invalidateRenders)
        {
            // Slider ValueChanged can fire during InitializeComponent, before the panes exist.
            if (_basePane == null || _overlayPane == null)
            {
                return;
            }

            if (DpiSlider == null || ImageThresholdSlider == null || CacheSizeSlider == null)
            {
                return;
            }

            int newDpi = (int)DpiSlider.Value;

            // Rendered bitmap dimensions scale with DPI; adjust zoom inversely so the page
            // stays the same apparent size on screen instead of jumping when DPI changes.
            if (invalidateRenders
                && _renderDpiForCurrentDisplay > 0
                && newDpi != _renderDpiForCurrentDisplay)
            {
                CompensateZoomForDpiChange(_renderDpiForCurrentDisplay, newDpi);
            }

            selectedDpi = newDpi;
            imageThreshold = (int)ImageThresholdSlider.Value;
            setOffset = CacheSizeSlider.Value;

            // A DPI/Sensitivity change makes every cached render stale, so drop them and let
            // the reload below re-render the current page and preload the neighbours afresh.
            if (invalidateRenders)
            {
                ClearRenderCaches();
            }

            UpdateMemoryUsageDisplay();

            if (!string.IsNullOrWhiteSpace(_basePane.FilePath))
            {
                int basePageIndex = GetPageIndexFromTextBox(_basePane.PageTextBox?.Text);
                PrunePageCache(_basePane.FilePath, basePageIndex);
                LoadPage(_basePane);
            }

            if (!string.IsNullOrWhiteSpace(_overlayPane.FilePath))
            {
                int overlayPageIndex = GetPageIndexFromTextBox(_overlayPane.PageTextBox?.Text);
                PrunePageCache(_overlayPane.FilePath, overlayPageIndex);
                LoadPage(_overlayPane);
            }
        }

        private void ClearRenderCaches()
        {
            lock (_displayCacheLock)
            {
                _displayCache.Clear();
            }

            Interlocked.Exchange(ref _estimatedCacheBytes, 0);
        }

        private void UpdateAutoModeButtonAppearance()
        {
            if (AutoModeToggleButton == null)
            {
                return;
            }

            if (!_isAutoMode)
            {
                AutoModeToggleButton.Background = (SolidColorBrush)FindResource("AutoNormalBrush");
                return;
            }

            if (_autoPerformanceReductionActive)
            {
                AutoModeToggleButton.Background = (SolidColorBrush)FindResource("AutoReducedBrush");
                return;
            }

            if (_autoPerformanceRecoveryActive)
            {
                AutoModeToggleButton.Background = (SolidColorBrush)FindResource("AutoRecoveryBrush");
                return;
            }

            AutoModeToggleButton.Background = (SolidColorBrush)FindResource("AutoNormalBrush");
        }

        private long GetEstimatedDisplayCacheBytes()
        {
            return Interlocked.Read(ref _estimatedCacheBytes);
        }

        private long GetEstimatedPdfCacheBytes()
        {
            return Interlocked.Read(ref _estimatedPdfCacheBytes);
        }

        private long GetTotalAppCacheBytes()
        {
            return GetEstimatedDisplayCacheBytes() + GetEstimatedPdfCacheBytes();
        }

        private void UpdateMemoryUsageDisplay()
        {
            if (MemoryUsageTextBlock == null)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateMemoryUsageDisplay));
                return;
            }

            long estimatedBytes = GetTotalAppCacheBytes();
            _sessionTelemetry.RecordCacheBytes(estimatedBytes);

            long displayBytes = GetEstimatedDisplayCacheBytes();
            long pdfBytes = GetEstimatedPdfCacheBytes();
            double displayMegabytes = displayBytes / (1024.0 * 1024.0);
            double pdfMegabytes = pdfBytes / (1024.0 * 1024.0);
            double totalMegabytes = estimatedBytes / (1024.0 * 1024.0);

            string memoryToolTip =
                $"App cache: {totalMegabytes:0.0} MB total "
                + $"(bitmap {displayMegabytes:0.0} MB + PDF {pdfMegabytes:0.0} MB). "
                + "First value is app cache vs system RAM; second is overall system memory load. "
                + "Red values indicate high usage.";
            MemoryUsageTextBlock.ToolTip = memoryToolTip;

            if (TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes) && totalSystemMemoryBytes > 0)
            {
                double cachePercentOfSystemMemory = estimatedBytes / (double)totalSystemMemoryBytes * 100.0;
                double systemMemoryLoadPercent = 0.0;
                bool cacheOverThreshold = cachePercentOfSystemMemory > CACHE_MEMORY_PERCENT_THRESHOLD;
                bool systemOverThreshold = TryGetSystemMemoryLoadPercent(out systemMemoryLoadPercent)
                    && systemMemoryLoadPercent > SYSTEM_MEMORY_LOAD_THRESHOLD;

                MemoryUsageTextBlock.Inlines.Clear();
                MemoryUsageTextBlock.Inlines.Add(new Run("Mem: "));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{cachePercentOfSystemMemory:0}%")
                {
                    Foreground = cacheOverThreshold ? Brushes.Red : MemoryUsageTextBlock.Foreground
                });
                MemoryUsageTextBlock.Inlines.Add(new Run($" ({totalMegabytes:0.0} MB)"));
                MemoryUsageTextBlock.Inlines.Add(new Run("/"));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{systemMemoryLoadPercent:0}%")
                {
                    Foreground = systemOverThreshold ? Brushes.Red : MemoryUsageTextBlock.Foreground
                });
            }
            else
            {
                MemoryUsageTextBlock.Inlines.Clear();
                MemoryUsageTextBlock.Inlines.Add(new Run("Mem: "));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{totalMegabytes:0.0} MB"));
                MemoryUsageTextBlock.Inlines.Add(new Run($" ({displayMegabytes:0.0}+{pdfMegabytes:0.0})"));
                MemoryUsageTextBlock.Inlines.Add(new Run("/"));
                MemoryUsageTextBlock.Inlines.Add(new Run("--%"));
            }
        }

        private bool TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes)
        {
            totalSystemMemoryBytes = 0;

            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };

            if (!GlobalMemoryStatusEx(ref memoryStatus))
            {
                return false;
            }

            totalSystemMemoryBytes = (long)memoryStatus.ullTotalPhys;
            return totalSystemMemoryBytes > 0;
        }

        private bool TryGetSystemMemoryLoadPercent(out double systemMemoryLoadPercent)
        {
            systemMemoryLoadPercent = 0.0;

            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };

            if (!GlobalMemoryStatusEx(ref memoryStatus))
            {
                return false;
            }

            systemMemoryLoadPercent = memoryStatus.dwMemoryLoad;
            return true;
        }

        private long EstimateBitmapBytes(BitmapSource? bitmap)
        {
            if (bitmap == null)
            {
                return 0;
            }

            int bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
            return (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
        }

        private void ApplyOverlaySettings()
        {
            if (OverlayImage == null ||
                OverlayScaleTransform == null ||
                OverlayTranslateTransform == null ||
                OpacitySlider == null ||
                ScaleSlider == null ||
                XOffsetSlider == null ||
                YOffsetSlider == null)
            {
                return;
            }

            double opacityBalance = OpacitySlider.Value;

            double baseOpacity = 1.0;
            double overlayOpacity = 1.0;

            if (opacityBalance < 0)
            {
                // Slider left: fade the base image
                baseOpacity = 1.0 + (opacityBalance / 100.0);
            }
            else if (opacityBalance > 0)
            {
                // Slider right: fade the overlay image
                overlayOpacity = 1.0 - (opacityBalance / 100.0);
            }

            BaseImage.Opacity = Math.Clamp(baseOpacity, 0.0, 1.0);
            OverlayImage.Opacity = Math.Clamp(overlayOpacity, 0.0, 1.0);

            double scale = ScaleSlider.Value / 100.0;
            OverlayScaleTransform.ScaleX = scale;
            OverlayScaleTransform.ScaleY = scale;

            OverlayTranslateTransform.X = XOffsetSlider.Value;
            OverlayTranslateTransform.Y = YOffsetSlider.Value;

            ApplyOverlayRotation();
        }

        private void ApplyOverlayRotation()
        {
            if (OverlayRotateTransform == null || OverlayImage == null)
            {
                return;
            }

            double fineAngle = RotateFineSlider?.Value ?? 0.0;
            double totalAngle = _overlayQuarterTurns * 90.0 + fineAngle;

            // Pivot around the image center in natural (unscaled) coordinates; the
            // rotate transform runs before scale/translate in the transform group.
            OverlayRotateTransform.CenterX = (OverlayImage.Source?.Width ?? OverlayImage.ActualWidth) / 2.0;
            OverlayRotateTransform.CenterY = (OverlayImage.Source?.Height ?? OverlayImage.ActualHeight) / 2.0;
            OverlayRotateTransform.Angle = totalAngle;

            if (RotateAngleText != null)
            {
                // Show in the -180..180 range so 270 reads as -90.
                double displayAngle = ((totalAngle + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
                RotateAngleText.Text = $"{displayAngle:0.0}\u00B0";
            }
        }

        private void RotateCw_Click(object sender, RoutedEventArgs e)
        {
            _overlayQuarterTurns = (_overlayQuarterTurns + 1) % 4;
            ApplyOverlayRotation();
            SetStatus($"Overlay rotated to {RotateAngleText?.Text}.");
        }

        private void ResetOverlay_Click(object sender, RoutedEventArgs e)
        {
            OpacitySlider.Value = 50;
            ScaleSlider.Value = 100;
            XOffsetSlider.Value = 0;
            YOffsetSlider.Value = 0;
            _overlayQuarterTurns = 0;
            RotateFineSlider.Value = 0;

            ApplyOverlaySettings();

            SetStatus("Overlay reset.");
        }

        private void OverlayImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If Ctrl is held, let ScrollViewer handle panning instead
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                return;
            }
            if (OverlayImage.Source == null)
            {
                return;
            }

            _isDragging = true;
            _lastMousePosition = e.GetPosition(OverlayHost);
            OverlayImage.CaptureMouse();

            SetStatus("Dragging overlay.");
        }

        private void OverlayImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            OverlayImage.ReleaseMouseCapture();

            SetStatus("Overlay drag complete.");
        }

        private void OverlayImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            Point currentMousePosition = e.GetPosition(OverlayHost);

            double deltaX = currentMousePosition.X - _lastMousePosition.X;
            double deltaY = currentMousePosition.Y - _lastMousePosition.Y;

            XOffsetSlider.Value += deltaX;
            YOffsetSlider.Value += deltaY;

            _lastMousePosition = currentMousePosition;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
                StatusText.Foreground = isError ? Brushes.Red : Brushes.Black;
            }
        }

        private void ViewerScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool altDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (ctrlDown && altDown)
            {
                if (ScaleSlider == null || string.IsNullOrWhiteSpace(_overlayPane?.FilePath))
                {
                    return;
                }

                e.Handled = true;

                double direction = e.Delta > 0 ? 1.0 : -1.0;
                double newScale = ScaleSlider.Value + (direction * SCALE_WHEEL_STEP);
                ScaleSlider.Value = Math.Clamp(newScale, ScaleSlider.Minimum, ScaleSlider.Maximum);
                return;
            }

            if (!ctrlDown)
            {
                return;
            }

            if (ViewerScrollViewer == null || OverlayHost == null)
            {
                return;
            }

            e.Handled = true;

            double oldZoom = _zoom;

            if (e.Delta > 0)
            {
                _zoom *= ZOOM_STEP;
            }
            else
            {
                _zoom /= ZOOM_STEP;
            }

            _zoom = Math.Clamp(_zoom, ZOOM_MIN, ZOOM_MAX);

            if (Math.Abs(_zoom - oldZoom) < 0.0001)
            {
                return;
            }

            Point mousePositionInScrollViewer = e.GetPosition(ViewerScrollViewer);

            double contentXBeforeZoom =
                (ViewerScrollViewer.HorizontalOffset + mousePositionInScrollViewer.X) / oldZoom;

            double contentYBeforeZoom =
                (ViewerScrollViewer.VerticalOffset + mousePositionInScrollViewer.Y) / oldZoom;

            if (OverlayHost.LayoutTransform is ScaleTransform transform)
            {
                transform.ScaleX = _zoom;
                transform.ScaleY = _zoom;
            }

            ViewerScrollViewer.UpdateLayout();

            double newHorizontalOffset =
                (contentXBeforeZoom * _zoom) - mousePositionInScrollViewer.X;

            double newVerticalOffset =
                (contentYBeforeZoom * _zoom) - mousePositionInScrollViewer.Y;

            ViewerScrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
            ViewerScrollViewer.ScrollToVerticalOffset(newVerticalOffset);
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            if (ViewerScrollViewer == null || OverlayHost == null)
                return;

            // Must have something loaded
            if (BaseImage.Source == null && OverlayImage.Source == null)
                return;

            // Get viewport size (visible area)
            double viewportWidth = ViewerScrollViewer.ViewportWidth;
            double viewportHeight = ViewerScrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0)
                return;

            // Size to whichever document is loaded (base when present, otherwise overlay-only).
            double contentWidth;
            double contentHeight;
            if (BaseImage.Source is BitmapSource baseSource)
            {
                contentWidth = baseSource.Width;
                contentHeight = baseSource.Height;
            }
            else if (OverlayImage.Source is BitmapSource overlaySource)
            {
                contentWidth = overlaySource.Width;
                contentHeight = overlaySource.Height;
            }
            else
            {
                return;
            }

            if (contentWidth <= 0 || contentHeight <= 0)
                return;

            // Calculate scale to fit both dimensions
            double scaleX = viewportWidth / contentWidth;
            double scaleY = viewportHeight / contentHeight;

            _zoom = Math.Min(scaleX, scaleY);

            _zoom = Math.Clamp(_zoom, ZOOM_MIN, ZOOM_MAX);

            // Apply zoom
            if (OverlayHost.LayoutTransform is ScaleTransform transform)
            {
                transform.ScaleX = _zoom;
                transform.ScaleY = _zoom;
            }

            // Reset scroll to top-left
            ViewerScrollViewer.ScrollToHorizontalOffset(0);
            ViewerScrollViewer.ScrollToVerticalOffset(0);
        }

        private void ViewerScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            if (sender is not ScrollViewer scrollViewer)
                return;

            e.Handled = true;

            _isPanning = true;

            _panStartPoint = e.GetPosition(this);
            _panStartHorizontalOffset = scrollViewer.HorizontalOffset;
            _panStartVerticalOffset = scrollViewer.VerticalOffset;

            scrollViewer.CaptureMouse();

            Cursor = Cursors.SizeAll;
        }

        private void ViewerScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning)
                return;

            e.Handled = true;

            if (sender is not ScrollViewer scrollViewer)
                return;

            Point currentPoint = e.GetPosition(this);

            double deltaX = currentPoint.X - _panStartPoint.X;
            double deltaY = currentPoint.Y - _panStartPoint.Y;

            scrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
            scrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
        }

        private void ViewerScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning)
                return;

            e.Handled = true;

            if (sender is not ScrollViewer scrollViewer)
                return;

            _isPanning = false;
            scrollViewer.ReleaseMouseCapture();

            Cursor = Cursors.Arrow;
        }

        private void FitToWindowDeferred()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitToWindow_Click(this, new RoutedEventArgs());
            }), DispatcherPriority.Loaded);
        }

        private void CompensateZoomForDpiChange(int oldDpi, int newDpi)
        {
            if (oldDpi <= 0 || newDpi <= 0 || oldDpi == newDpi)
            {
                return;
            }

            _zoom *= oldDpi / (double)newDpi;
            _zoom = Math.Clamp(_zoom, ZOOM_MIN, ZOOM_MAX);
        }

        private void ApplyCurrentZoomDeferred()
        {
            Dispatcher.BeginInvoke(ApplyCurrentZoom, DispatcherPriority.Loaded);
        }

        private void ApplyCurrentZoom()
        {
            if (OverlayHost?.LayoutTransform is ScaleTransform transform)
            {
                transform.ScaleX = _zoom;
                transform.ScaleY = _zoom;
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            ChangePages(1);
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            ChangePages(-1);
        }

        private void NextPageOffset_Click(object sender, RoutedEventArgs e)
        {
            ChangeOverlayPage(1);
        }

        private void PreviousPageOffset_Click(object sender, RoutedEventArgs e)
        {
            ChangeOverlayPage(-1);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isArrowKey = e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down;
            if (!isArrowKey)
            {
                return;
            }

            // Leave arrow keys alone while editing a page number or adjusting a slider,
            // so caret movement and value stepping keep working.
            if (Keyboard.FocusedElement is TextBox ||
                Keyboard.FocusedElement is System.Windows.Controls.Primitives.RangeBase)
            {
                return;
            }

            // Ctrl+arrows: nudge the overlay one pixel for precise registration.
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (string.IsNullOrWhiteSpace(_overlayPane?.FilePath))
                {
                    return;
                }

                switch (e.Key)
                {
                    case Key.Left: XOffsetSlider.Value -= 1; break;
                    case Key.Right: XOffsetSlider.Value += 1; break;
                    case Key.Up: YOffsetSlider.Value -= 1; break;
                    case Key.Down: YOffsetSlider.Value += 1; break;
                }

                SetStatus($"Overlay offset: {XOffsetSlider.Value:0}, {YOffsetSlider.Value:0}");
                e.Handled = true;
                return;
            }

            // Plain left/right: page through the documents.
            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                ChangePages(e.Key == Key.Right ? 1 : -1);
                e.Handled = true;
            }
        }

        private void ChangePages(int delta)
        {
            bool anyChanged = false;
            bool anyLimitHit = false;

            foreach (DocumentPane pane in new[] { _basePane, _overlayPane })
            {
                if (string.IsNullOrWhiteSpace(pane.FilePath))
                {
                    continue;
                }

                if (TryAdvancePane(pane, delta))
                {
                    anyChanged = true;
                }
                else
                {
                    anyLimitHit = true;
                }
            }

            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            if (anyChanged && !anyLimitHit)
            {
                SetStatus($"Pages loaded. Base: {_basePane.PageTextBox.Text}, Overlay: {_overlayPane.PageTextBox.Text}");
            }
        }

        private void ChangeOverlayPage(int delta)
        {
            if (string.IsNullOrWhiteSpace(_overlayPane.FilePath))
                return;

            if (TryAdvancePane(_overlayPane, delta))
            {
                SetStatus($"Overlay page: {_overlayPane.PageTextBox.Text}");
            }

            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();
        }

        private bool TryAdvancePane(DocumentPane pane, int delta)
        {
            int currentPage = GetPageNumber(pane.PageTextBox.Text);
            int newPage = currentPage + delta;

            if (newPage >= 1 &&
                (!pane.PageCount.HasValue || newPage <= pane.PageCount.Value))
            {
                pane.PageTextBox.Text = newPage.ToString();
                LoadPage(pane);
                return true;
            }

            // Attempted to move to a non-existent page: keep the page number where it is
            // and flag it red to show the change was rejected.
            pane.PageTextBox.Foreground = Brushes.Red;
            ShowError(delta < 0
                ? $"{pane.DisplayName} PDF: first page reached."
                : $"{pane.DisplayName} PDF: last page reached.");
            return false;
        }

        private int GetPageNumber(string text)
        {
            if (!int.TryParse(text, out int page) || page < 1)
                return 1;

            return page;
        }

        private void PageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            // Prevent beep / extra processing
            e.Handled = true;

            LoadPage(_basePane);
            LoadPage(_overlayPane);
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            SetStatus($"Loaded pages: Base={_basePane.PageTextBox.Text}, Overlay={_overlayPane.PageTextBox.Text}");
        }

        private int? GetPdfPageCount(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".pdf")
            {
                return 1;
            }

            try
            {
                byte[] pdfBytes = GetPdfBytes(filePath);
                return Conversion.GetPageCount(pdfBytes);
            }
            catch
            {
                return null;
            }
        }

        private void ShowError(string message)
        {
            SetStatus(message, isError: true);
        }

        private void UpdatePageNavigationButtons()
        {
            bool canGoPrevious = false;
            bool canGoNext = false;

            foreach (DocumentPane pane in new[] { _basePane, _overlayPane })
            {
                if (string.IsNullOrWhiteSpace(pane.FilePath))
                {
                    continue;
                }

                int page = GetPageNumber(pane.PageTextBox.Text);

                if (page > 1)
                    canGoPrevious = true;

                if (!pane.PageCount.HasValue || page < pane.PageCount.Value)
                    canGoNext = true;
            }

            PreviousPageButton.IsEnabled = canGoPrevious;
            NextPageButton.IsEnabled = canGoNext;
        }

        private void UpdateOverlayNavigationButtons()
        {
            // No overlay loaded
            if (string.IsNullOrWhiteSpace(_overlayPane.FilePath))
            {
                PreviousPageOffsetButton.IsEnabled = false;
                NextPageOffsetButton.IsEnabled = false;
                return;
            }

            int currentPage = GetPageNumber(_overlayPane.PageTextBox.Text);

            bool canGoPrev = currentPage > 1;
            bool canGoNext = !_overlayPane.PageCount.HasValue || currentPage < _overlayPane.PageCount.Value;

            PreviousPageOffsetButton.IsEnabled = canGoPrev;
            NextPageOffsetButton.IsEnabled = canGoNext;
        }

        private void UpdatePageInputState()
        {
            SyncPageTextBox(_basePane);
            SyncPageTextBox(_overlayPane);
        }

        private void SyncPageTextBox(DocumentPane pane)
        {
            if (string.IsNullOrWhiteSpace(pane.FilePath))
            {
                pane.PageTextBox.Text = "";
            }
            else if (string.IsNullOrWhiteSpace(pane.PageTextBox.Text))
            {
                pane.PageTextBox.Text = "1";
            }
        }

        // Cache probe only - never renders. Lets LoadPage take a synchronous fast path
        // for pages that are already rendered for display.
        private BitmapSource? TryGetCachedDisplayBitmap(string filePath, int pageIndex, string role)
        {
            lock (_displayCacheLock)
            {
                return _displayCache.TryGetValue((filePath, pageIndex, role), out BitmapSource? cached)
                    ? cached
                    : null;
            }
        }

        /// <summary>
        /// Returns the bitmap shown in the viewer: tinted when tint is on, plain otherwise.
        /// Only that form is cached — untinted intermediates are not retained.
        /// </summary>
        private BitmapSource GetDisplayBitmap(
            string filePath,
            int pageIndex,
            string role,
            Color tintColor,
            bool useTint)
        {
            var cacheKey = (filePath, pageIndex, role);

            lock (_displayCacheLock)
            {
                if (_displayCache.TryGetValue(cacheKey, out BitmapSource? cached) && cached != null)
                {
                    return cached;
                }
            }

            var renderKey = (filePath, pageIndex);

            object renderGate;
            lock (_renderGatesLock)
            {
                if (!_renderGates.TryGetValue(renderKey, out object? existingGate))
                {
                    existingGate = new object();
                    _renderGates[renderKey] = existingGate;
                }

                renderGate = existingGate;
            }

            try
            {
                lock (renderGate)
                {
                    lock (_displayCacheLock)
                    {
                        if (_displayCache.TryGetValue(cacheKey, out BitmapSource? cached) && cached != null)
                        {
                            return cached;
                        }
                    }

                    BitmapSource rendered = LoadFileAsBitmapSource(filePath, pageIndex);
                    BitmapSource display = useTint
                        ? CreateTintedImage(rendered, tintColor)
                        : rendered;

                    lock (_displayCacheLock)
                    {
                        _displayCache[cacheKey] = display;
                    }

                    Interlocked.Add(ref _estimatedCacheBytes, EstimateBitmapBytes(display));
                    UpdateMemoryUsageDisplay();
                    return display;
                }
            }
            finally
            {
                lock (_renderGatesLock)
                {
                    _renderGates.Remove(renderKey);
                }
            }
        }

        private int GetCacheRadius()
        {
            return Math.Max(0, (int)Math.Round(setOffset));
        }

        private byte[] GetPdfBytes(string filePath)
        {
            lock (_pdfCacheLock)
            {
                if (_pdfCache.TryGetValue(filePath, out byte[]? cachedPdfBytes) &&
                    cachedPdfBytes != null)
                {
                    return cachedPdfBytes;
                }
            }

            byte[] pdfBytes = File.ReadAllBytes(filePath);

            lock (_pdfCacheLock)
            {
                if (!_pdfCache.ContainsKey(filePath))
                {
                    _pdfCache[filePath] = pdfBytes;
                    Interlocked.Add(ref _estimatedPdfCacheBytes, pdfBytes.Length);
                    UpdateMemoryUsageDisplay();
                }

                return _pdfCache[filePath];
            }
        }

        private void PreloadAdjacentPages(DocumentPane pane, string filePath, int currentPageIndex)
        {
            bool useTint = TintImagesCheckBox?.IsChecked == true;
            // Under AUTO memory pressure, skip speculative preloading entirely. It is the
            // main source of re-render churn that fights the UI for CPU; pages still render
            // on demand when the user actually navigates to them.
            if (_isAutoMode && _memoryPressureActive)
            {
                return;
            }

            // Rapid paging fires a preload per page turn; cancel the superseded one so
            // stale tasks don't keep rendering pages the user has already moved past.
            // Only touched on the UI thread (ApplyLoadedPage always runs there).
            pane.PreloadCts?.Cancel();
            var cts = new CancellationTokenSource();
            pane.PreloadCts = cts;
            CancellationToken token = cts.Token;

            int? pageCount = pane.PageCount;
            string role = pane.Role;
            Color tintColor = pane.TintColor;

            SetProcessingState(true);

            Task.Run(() =>
            {
                try
                {
                    int cacheRadius = GetCacheRadius();
                    for (int offset = 1; offset <= cacheRadius && !token.IsCancellationRequested; offset++)
                    {
                        int prev = currentPageIndex - offset;
                        int next = currentPageIndex + offset;

                        // PREVIOUS PAGE
                        if (prev >= 0)
                        {
                            GetDisplayBitmap(filePath, prev, role, tintColor, useTint);
                        }

                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        // NEXT PAGE
                        if (!pageCount.HasValue || next < pageCount.Value)
                        {
                            GetDisplayBitmap(filePath, next, role, tintColor, useTint);
                        }
                    }
                }
                catch
                {
                    // Do not interrupt UI
                }
                finally
                {
                    SetProcessingState(false);
                }
            });
        }

        private void PrunePageCache(string filePath, int currentPageIndex)
        {
            int cacheRadius = GetCacheRadius();

            long removedBytes = 0;

            lock (_displayCacheLock)
            {
                var keysToRemove = _displayCache.Keys
                    .Where(k =>
                        k.path == filePath &&
                        Math.Abs(k.page - currentPageIndex) > cacheRadius)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    removedBytes += EstimateBitmapBytes(_displayCache[key]);
                    _displayCache.Remove(key);
                }
            }

            Interlocked.Add(ref _estimatedCacheBytes, -removedBytes);
            UpdateMemoryUsageDisplay();
        }

        private string? GetBasePrefix()
        {
            if (string.IsNullOrWhiteSpace(_basePane.FilePath))
                return null;

            string name = Path.GetFileNameWithoutExtension(_basePane.FilePath);
            int idx = name.IndexOf('_');

            return idx > 0 ? name.Substring(0, idx) : name;
        }

        private string? SelectOverlayFileFiltered()
        {
            if (string.IsNullOrWhiteSpace(_basePane.FilePath))
                return SelectPdfOrImageFile(); // fallback

            string baseDirectory = Path.GetDirectoryName(_basePane.FilePath)!;
            string? basePrefix = GetBasePrefix();

            if (string.IsNullOrWhiteSpace(basePrefix))
                return SelectPdfOrImageFile();

            string baseFileName = Path.GetFileNameWithoutExtension(_basePane.FilePath);

            var matchingFiles = Directory.GetFiles(baseDirectory, "*.pdf")
                .Where(f =>
                {
                    string name = Path.GetFileNameWithoutExtension(f);

                    return name.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)
                        && !name.Equals(baseFileName, StringComparison.OrdinalIgnoreCase); // exclude the base file
                })
                .Select(f => new FileItem
                {
                    FullPath = f,
                    DisplayName = Path.GetFileName(f)
                })
                .OrderBy(f => f.DisplayName)
                .ToList();

            if (matchingFiles.Count == 0)
            {
                MessageBox.Show($"No matching overlay files found for prefix '{basePrefix}_'.");
                return null;
            }

            // Let the user pick from the filtered list
            var list = new ListBox
            {
                ItemsSource = matchingFiles,
                Margin = new Thickness(10)
            };

            var dialog = new Window
            {
                Title = "Double-click to select revision",
                Width = 500,
                Height = 400,
                Content = list
            };

            list.MouseDoubleClick += (s, e) => dialog.DialogResult = true;

            if (dialog.ShowDialog() == true)
            {
                if (list.SelectedItem is FileItem selected)
                {
                    return selected.FullPath;
                }
            }

            return null;
        }

        private class FileItem
        {
            public string FullPath { get; set; } = "";
            public string DisplayName { get; set; } = "";

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private void ClearCachesForFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            long removedBytes = 0;

            lock (_displayCacheLock)
            {
                var keysToRemove = _displayCache.Keys
                    .Where(k => k.path.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    removedBytes += EstimateBitmapBytes(_displayCache[key]);
                    _displayCache.Remove(key);
                }
            }

            Interlocked.Add(ref _estimatedCacheBytes, -removedBytes);

            lock (_pdfCacheLock)
            {
                if (_pdfCache.TryGetValue(filePath, out byte[]? cachedPdfBytes) && cachedPdfBytes != null)
                {
                    Interlocked.Add(ref _estimatedPdfCacheBytes, -cachedPdfBytes.Length);
                    _pdfCache.Remove(filePath);
                }
            }

            UpdateMemoryUsageDisplay();
        }

        /// <summary>
        /// Holds the state and UI references for one document (base or overlay),
        /// so the load / tint / navigation logic can be shared between both.
        /// </summary>
        private sealed class DocumentPane
        {
            public DocumentPane(
                string role,
                string displayName,
                Color tintColor,
                Image imageControl,
                TextBox pageTextBox,
                Run pageCountRun,
                TextBlock fileNameTextBlock)
            {
                Role = role;
                DisplayName = displayName;
                TintColor = tintColor;
                ImageControl = imageControl;
                PageTextBox = pageTextBox;
                PageCountRun = pageCountRun;
                FileNameTextBlock = fileNameTextBlock;
            }

            public void SetTintColor(Color tintColor)
            {
                TintColor = tintColor;
            }

            public string Role { get; }
            public string DisplayName { get; }
            public Color TintColor { get; private set; }
            public Image ImageControl { get; }
            public TextBox PageTextBox { get; }
            public Run PageCountRun { get; }
            public TextBlock FileNameTextBlock { get; }

            public string? FilePath { get; set; }
            public int? PageCount { get; set; }

            /// <summary>
            /// Incremented by each LoadPage call (UI thread only); background renders
            /// compare against it so a stale result is discarded instead of applied.
            /// </summary>
            public int LoadVersion { get; set; }

            /// <summary>
            /// Cancellation for the pane's in-flight preload task (UI thread only);
            /// each new preload cancels the previous one.
            /// </summary>
            public CancellationTokenSource? PreloadCts { get; set; }
        }
    }
}
