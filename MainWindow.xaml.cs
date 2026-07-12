using Microsoft.Win32;
using PDFtoImage;
using SkiaSharp;
using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
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

        private bool _isDragging;
        private bool _isAutoMode = true;
        private int _processingOperationCount;
        private Point _lastMousePosition;
        private string? _baseFilePath;
        private string? _overlayFilePath;
        private BitmapSource? _baseOriginalImage;
        private BitmapSource? _overlayOriginalImage;
        private double _zoom = 1.0;
        private const double ZOOM_STEP = 1.05;
        private const double ZOOM_MIN = 0.1;
        private const double ZOOM_MAX = 10.0;
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;
        private int? _basePageCount;
        private int? _overlayPageCount;
        private Dictionary<(string path, int page), BitmapSource> _pageCache = new();
        private Dictionary<string, string> _pdfCache = new();
        private Dictionary<(string path, int page, string role), BitmapSource> _tintCache = new();
        public static double ZOOM_STEP1 => ZOOM_STEP;
        private readonly object _tintCacheLock = new();
        private readonly object _pageCacheLock = new();
        private readonly object _pdfCacheLock = new();

        private int selectedDpi = 150;
        private int imageThreshold = 200;

        private double setOffset = 5; // Number of adjacent pages to preload
        private DispatcherTimer? _autoMemoryAdjustmentTimer;
        private int _autoMemoryExceededSeconds;
        private int _autoMemorySevereExceededSeconds;
        private bool _autoPerformanceReductionActive;

        public MainWindow()
        {
            InitializeComponent();
            UseFilteredOverlayPickerCheckbox.IsEnabled = false;
            InitializeAutoMemoryAdjustmentTimer();

            Loaded += (s, e) =>
            {
                UpdatePageInputState();
                UpdatePageNavigationButtons();
                UpdateOverlayNavigationButtons();
                ApplyOverlaySettings();
                UpdateMemoryUsageDisplay();
                SetAutoManualMode(_isAutoMode);
                SetStatus("Load a base PDF and/or overlay PDF to begin.");
            };
        }


        private void LoadBaseFile_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = SelectPdfOrImageFile();

            if (filePath == null)
            {
                return;
            }

            ClearCachesForFile(_baseFilePath);
            _baseFilePath = filePath;
            _basePageCount = GetPdfPageCount(filePath);
            BasePageTextBox.Text = "1";
            LoadBasePage();
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();
            UpdatePageInputState();

            SetStatus($"Loaded base file: {filePath}");
            BaseFileName.Text = Path.GetFileNameWithoutExtension(filePath);
            BasePageCount.Text = GetPdfPageCount(filePath).ToString() ?? "0";
        }

        private void LoadOverlayFile_Click(object sender, RoutedEventArgs e)
        {
            string? filePath;
            if (UseFilteredOverlayPickerCheckbox?.IsChecked == true)
            {
                filePath = SelectOverlayFileFiltered();
            }
            else
            {
                filePath = SelectPdfOrImageFile();
            }

            if (filePath == null)
            {
                return;
            }

            ClearCachesForFile(_overlayFilePath);
            _overlayFilePath = filePath;
            _overlayPageCount = GetPdfPageCount(filePath);
            OverlayPageTextBox.Text = "1";
            LoadOverlayPage();
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();
            UpdatePageInputState();

            SetStatus($"Loaded overlay file: {filePath}");
            OverlayFileName.Text = Path.GetFileNameWithoutExtension(filePath);
            OverlayPageCount.Text = GetPdfPageCount(filePath).ToString() ?? "0";
        }

        private void ReloadPages_Click(object sender, RoutedEventArgs e)
        {
            LoadBasePage();
            LoadOverlayPage();
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            ApplyImageTinting();
            SetStatus("Reloaded selected pages.");
        }

        private void LoadBasePage()
        {
            if (string.IsNullOrWhiteSpace(_baseFilePath))
            {
                return;
            }

            int pageIndex = GetPageIndexFromTextBox(BasePageTextBox?.Text);
            SetProcessingState(true);

            try
            {
                _baseOriginalImage = GetCachedPage(_baseFilePath, pageIndex);
                ApplyBaseImageTinting(pageIndex);
                FitToWindowDeferred();
                bool useTint = TintImagesCheckBox?.IsChecked == true;
                PrunePageCache(_baseFilePath, pageIndex);
                PreloadAdjacentPages(
                    _baseFilePath,
                    pageIndex,
                    _basePageCount,
                    "base",
                    Colors.LimeGreen,
                    useTint);

                UpdatePageTextColor(BasePageTextBox!, pageIndex + 1, _basePageCount);
                UseFilteredOverlayPickerCheckbox.IsEnabled = !string.IsNullOrWhiteSpace(_baseFilePath);
                UpdateMemoryUsageDisplay();

            }
            catch (Exception ex)
            {
                SetStatus($"Could not load base page: {ex.Message}");
            }
            finally
            {
                SetProcessingState(false);
            }
        }

        private void LoadOverlayPage()
        {
            if (string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                return;
            }

            int pageIndex = GetPageIndexFromTextBox(OverlayPageTextBox?.Text);
            SetProcessingState(true);

            try
            {
                _overlayOriginalImage = GetCachedPage(_overlayFilePath, pageIndex);
                ApplyOverlayImageTinting(pageIndex);
                bool useTint = TintImagesCheckBox?.IsChecked == true;
                PrunePageCache(_overlayFilePath, pageIndex);
                PreloadAdjacentPages(
                    _overlayFilePath,
                    pageIndex,
                    _overlayPageCount,
                    "overlay",
                    Colors.Red,
                    useTint);

                UpdatePageTextColor(OverlayPageTextBox!, pageIndex + 1, _overlayPageCount);
                UpdateMemoryUsageDisplay();

            }
            catch (Exception ex)
            {
                SetStatus($"Could not load overlay page: {ex.Message}");
            }
            finally
            {
                SetProcessingState(false);
            }
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
                ProcessingSpinner.Visibility = showSpinner ? Visibility.Visible : Visibility.Collapsed;
            }));
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

        private string? SelectPdfOrImageFile()
        {
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
            string pdfBytes = GetPdfBase64(pdfFilePath);

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

            int threshold = imageThreshold; // adjust this

            for (int i = 0; i < pixels.Length; i += 4)
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
            ApplyImageTinting();
        }

        private void ApplyImageTinting()
        {
            int basePageIndex = GetPageIndexFromTextBox(BasePageTextBox?.Text);
            int overlayPageIndex = GetPageIndexFromTextBox(OverlayPageTextBox?.Text);

            ApplyBaseImageTinting(basePageIndex);
            ApplyOverlayImageTinting(overlayPageIndex);
        }
        private void ApplyBaseImageTinting(int pageIndex)
        {
            if (_baseOriginalImage == null || string.IsNullOrWhiteSpace(_baseFilePath))
                return;

            bool useTint = TintImagesCheckBox?.IsChecked == true;

            BaseImage.Source = useTint
                ? GetCachedTintedPage(
                    _baseFilePath,
                    pageIndex,
                    _baseOriginalImage,
                    "base",
                    Colors.LimeGreen)
                : _baseOriginalImage;
        }

        private void ApplyOverlayImageTinting(int pageIndex)
        {
            if (_overlayOriginalImage == null || string.IsNullOrWhiteSpace(_overlayFilePath))
                return;

            bool useTint = TintImagesCheckBox?.IsChecked == true;

            OverlayImage.Source = useTint
                ? GetCachedTintedPage(
                    _overlayFilePath,
                    pageIndex,
                    _overlayOriginalImage,
                    "overlay",
                    Colors.Red)
                : _overlayOriginalImage;
        }

        private BitmapSource CreateTintedImage(BitmapSource source, Color tintColor)
        {
            BitmapSource convertedSource = new FormatConvertedBitmap(
                source,
                PixelFormats.Bgra32,
                null,
                0);

            int width = convertedSource.PixelWidth;
            int height = convertedSource.PixelHeight;
            int bytesPerPixel = 4;
            int stride = width * bytesPerPixel;

            byte[] sourcePixels = new byte[height * stride];
            byte[] outputPixels = new byte[height * stride];

            convertedSource.CopyPixels(sourcePixels, stride, 0);

            for (int i = 0; i < sourcePixels.Length; i += bytesPerPixel)
            {
                byte blue = sourcePixels[i];
                byte green = sourcePixels[i + 1];
                byte red = sourcePixels[i + 2];
                byte alpha = sourcePixels[i + 3];

                double brightness = (red + green + blue) / 3.0;
                double darkness = 255.0 - brightness;

                byte outputAlpha = (byte)(darkness * (alpha / 255.0));

                outputPixels[i] = tintColor.B;
                outputPixels[i + 1] = tintColor.G;
                outputPixels[i + 2] = tintColor.R;
                outputPixels[i + 3] = outputAlpha;
            }

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
            ApplyOverlaySettings();
        }

        private void AutoModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isAutoMode = !_isAutoMode;
            SetAutoManualMode(_isAutoMode);
        }

        private void SetAutoManualMode(bool isAutoMode)
        {
            if (AutoModeToggleButton != null)
            {
                AutoModeToggleButton.Content = isAutoMode ? "AUTO" : "MANUAL";
            }

            bool showAdvancedControls = !isAutoMode;

            if (DpiLabel != null) DpiLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (DpiControls != null) DpiControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (PageCacheLabel != null) PageCacheLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (PageCacheControls != null) PageCacheControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (SensitivityLabel != null) SensitivityLabel.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (SensitivityControls != null) SensitivityControls.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
            if (TintImagesCheckBox != null) TintImagesCheckBox.Visibility = showAdvancedControls ? Visibility.Visible : Visibility.Collapsed;

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
                _autoMemoryExceededSeconds = 0;
                _autoMemorySevereExceededSeconds = 0;
                return;
            }

            if (!TryGetMemoryThresholdState(out bool cacheOverThreshold, out bool systemOverThreshold))
            {
                return;
            }

            if (cacheOverThreshold || systemOverThreshold)
            {
                if (GetCacheRadius() > 0)
                {
                    _autoMemorySevereExceededSeconds = 0;
                    _autoMemoryExceededSeconds++;

                    if (_autoMemoryExceededSeconds >= 10)
                    {
                        ReduceCachePageSize();
                    }
                }
                else
                {
                    _autoMemoryExceededSeconds = 0;
                    _autoMemorySevereExceededSeconds++;

                    if (_autoMemorySevereExceededSeconds >= 30)
                    {
                        ReduceDpi();
                    }
                }
            }
            else
            {
                _autoMemoryExceededSeconds = 0;
                _autoMemorySevereExceededSeconds = 0;
            }
        }

        private bool TryGetMemoryThresholdState(out bool cacheOverThreshold, out bool systemOverThreshold)
        {
            cacheOverThreshold = false;
            systemOverThreshold = false;

            long estimatedBytes = 0;

            lock (_pageCacheLock)
            {
                foreach (BitmapSource? image in _pageCache.Values)
                {
                    estimatedBytes += EstimateBitmapBytes(image);
                }
            }

            lock (_tintCacheLock)
            {
                foreach (BitmapSource? image in _tintCache.Values)
                {
                    estimatedBytes += EstimateBitmapBytes(image);
                }
            }

            if (TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes) && totalSystemMemoryBytes > 0)
            {
                double cachePercentOfSystemMemory = estimatedBytes / (double)totalSystemMemoryBytes * 100.0;
                cacheOverThreshold = cachePercentOfSystemMemory > 25.0;
            }

            systemOverThreshold = TryGetSystemMemoryLoadPercent(out double systemMemoryLoadPercent) && systemMemoryLoadPercent > 90.0;
            return true;
        }

        private void ReduceCachePageSize()
        {
            if (CacheSizeSlider == null)
            {
                return;
            }

            double newCacheRadius = Math.Max(0, CacheSizeSlider.Value - 1);
            if (newCacheRadius == CacheSizeSlider.Value)
            {
                return;
            }

            CacheSizeSlider.Value = newCacheRadius;
            _autoPerformanceReductionActive = true;
            UpdateAutoModeButtonAppearance();
            SetStatus("Low memory - AUTO performance reduction!");
            ApplyMemorySettings();
            _autoMemoryExceededSeconds = 0;
        }

        private void ReduceDpi()
        {
            if (DpiSlider == null)
            {
                return;
            }

            double newDpi = Math.Max(150, DpiSlider.Value - 25);
            if (newDpi == DpiSlider.Value)
            {
                return;
            }

            DpiSlider.Value = newDpi;
            _autoPerformanceReductionActive = true;
            UpdateAutoModeButtonAppearance();
            SetStatus("Low memory - AUTO performance reduction!");
            ApplyMemorySettings();
            _autoMemorySevereExceededSeconds = 0;
        }

        private void ApplyMemorySettings()
        {
            if (DpiSlider == null || ImageThresholdSlider == null || CacheSizeSlider == null)
            {
                return;
            }

            selectedDpi = (int)DpiSlider.Value;
            imageThreshold = (int)ImageThresholdSlider.Value;
            setOffset = CacheSizeSlider.Value;
            UpdateMemoryUsageDisplay();

            if (!string.IsNullOrWhiteSpace(_baseFilePath))
            {
                int basePageIndex = GetPageIndexFromTextBox(BasePageTextBox?.Text);
                PrunePageCache(_baseFilePath, basePageIndex);
                LoadBasePage();
            }

            if (!string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                int overlayPageIndex = GetPageIndexFromTextBox(OverlayPageTextBox?.Text);
                PrunePageCache(_overlayFilePath, overlayPageIndex);
                LoadOverlayPage();
            }
        }

        private void MemoryControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyMemorySettings();
        }

        private void UpdateAutoModeButtonAppearance()
        {
            if (AutoModeToggleButton == null)
            {
                return;
            }

            if (!_isAutoMode || !_autoPerformanceReductionActive)
            {
                AutoModeToggleButton.Background = new SolidColorBrush(Color.FromRgb(244, 244, 244));
                return;
            }

            AutoModeToggleButton.Background = Brushes.PaleGoldenrod;
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

            long estimatedBytes = 0;
            int pageCacheCount = 0;
            int tintCacheCount = 0;

            lock (_pageCacheLock)
            {
                pageCacheCount = _pageCache.Count;
                foreach (BitmapSource? image in _pageCache.Values)
                {
                    estimatedBytes += EstimateBitmapBytes(image);
                }
            }

            lock (_tintCacheLock)
            {
                tintCacheCount = _tintCache.Count;
                foreach (BitmapSource? image in _tintCache.Values)
                {
                    estimatedBytes += EstimateBitmapBytes(image);
                }
            }

            if (TryGetTotalSystemMemoryBytes(out long totalSystemMemoryBytes) && totalSystemMemoryBytes > 0)
            {
                double cachePercentOfSystemMemory = estimatedBytes / (double)totalSystemMemoryBytes * 100.0;
                double systemMemoryLoadPercent = 0.0;
                bool cacheOverThreshold = cachePercentOfSystemMemory > 25.0;
                bool systemOverThreshold = TryGetSystemMemoryLoadPercent(out systemMemoryLoadPercent) && systemMemoryLoadPercent > 90.0;

                MemoryUsageTextBlock.Inlines.Clear();
                MemoryUsageTextBlock.Inlines.Add(new Run("Mem: "));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{cachePercentOfSystemMemory:0.0}%")
                {
                    Foreground = cacheOverThreshold ? Brushes.Red : MemoryUsageTextBlock.Foreground
                });
                MemoryUsageTextBlock.Inlines.Add(new Run("/"));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{systemMemoryLoadPercent:0.0}%")
                {
                    Foreground = systemOverThreshold ? Brushes.Red : MemoryUsageTextBlock.Foreground
                });
            }
            else
            {
                double megabytes = estimatedBytes / (1024.0 * 1024.0);
                MemoryUsageTextBlock.Inlines.Clear();
                MemoryUsageTextBlock.Inlines.Add(new Run("Mem: "));
                MemoryUsageTextBlock.Inlines.Add(new Run($"{megabytes:0.0} MB"));
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
        }

        private void ResetOverlay_Click(object sender, RoutedEventArgs e)
        {
            OpacitySlider.Value = 50;
            ScaleSlider.Value = 100;
            XOffsetSlider.Value = 0;
            YOffsetSlider.Value = 0;

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

        private void SetStatus(string message)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
                StatusText.Foreground = message.Equals("Low memory - AUTO performance reduction!", StringComparison.OrdinalIgnoreCase)
                    ? Brushes.Red
                    : Brushes.Black;
            }
        }
        private void ViewerScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
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
                _zoom *= ZOOM_STEP1;
            }
            else
            {
                _zoom /= ZOOM_STEP1;
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

            // Get content size (unscaled)
            if (BaseImage.Source == null)
                return;

            double contentWidth = BaseImage.Source.Width;
            double contentHeight = BaseImage.Source.Height;

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
            SetStatus("Pan start triggered");
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
        private void ChangePages(int delta)
        {
            bool anyChanged = false;
            bool anyLimitHit = false;

            // BASE PDF
            if (!string.IsNullOrWhiteSpace(_baseFilePath))
            {
                int basePage = GetPageNumber(BasePageTextBox.Text);
                int newBasePage = basePage + delta;

                if (newBasePage >= 1 &&
                    (!_basePageCount.HasValue || newBasePage <= _basePageCount.Value))
                {
                    BasePageTextBox.Text = newBasePage.ToString();
                    LoadBasePage();
                    anyChanged = true;
                }
                else
                {
                    anyLimitHit = true;
                    ShowError(delta < 0
                        ? "Base PDF: first page reached."
                        : "Base PDF: last page reached.");
                }
            }

            // OVERLAY PDF
            if (!string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                int overlayPage = GetPageNumber(OverlayPageTextBox.Text);
                int newOverlayPage = overlayPage + delta;

                if (newOverlayPage >= 1 &&
                    (!_overlayPageCount.HasValue || newOverlayPage <= _overlayPageCount.Value))
                {
                    OverlayPageTextBox.Text = newOverlayPage.ToString();
                    LoadOverlayPage();
                    anyChanged = true;
                }
                else
                {
                    anyLimitHit = true;
                    ShowError(delta < 0
                        ? "Overlay PDF: first page reached."
                        : "Overlay PDF: last page reached.");
                }
            }

            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            if (anyChanged && !anyLimitHit)
            {
                SetStatus($"Pages loaded. Base: {BasePageTextBox.Text}, Overlay: {OverlayPageTextBox.Text}");
            }
        }

        private void ChangeOverlayPage(int delta)
        {
            if (string.IsNullOrWhiteSpace(_overlayFilePath))
                return;

            int overlayPage = GetPageNumber(OverlayPageTextBox.Text);
            int newOverlayPage = overlayPage + delta;

            if (newOverlayPage >= 1 &&
                (!_overlayPageCount.HasValue || newOverlayPage <= _overlayPageCount.Value))
            {
                OverlayPageTextBox.Text = newOverlayPage.ToString();
                LoadOverlayPage();

                SetStatus($"Overlay page: {OverlayPageTextBox.Text}");
            }
            else
            {
                ShowError(delta < 0
                    ? "Overlay PDF: first page reached."
                    : "Overlay PDF: last page reached.");
            }

            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();
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

            LoadBasePage();
            LoadOverlayPage();
            UpdatePageNavigationButtons();
            UpdateOverlayNavigationButtons();

            SetStatus($"Loaded pages: Base={BasePageTextBox.Text}, Overlay={OverlayPageTextBox.Text}");
        }
        private int? GetPdfPageCount(string filePath)
        {
            try
            {
                string pdfBytes = GetPdfBase64(filePath);
                return Conversion.GetPageCount(pdfBytes);
            }
            catch
            {
                return null;
            }
        }
        private void ShowError(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = Brushes.Red;
        }
        private void UpdatePageNavigationButtons()
        {
            bool canGoPrevious = false;
            bool canGoNext = false;

            // BASE
            if (!string.IsNullOrWhiteSpace(_baseFilePath))
            {
                int basePage = GetPageNumber(BasePageTextBox.Text);

                if (basePage > 1)
                    canGoPrevious = true;

                if (!_basePageCount.HasValue || basePage < _basePageCount.Value)
                    canGoNext = true;
            }

            // OVERLAY
            if (!string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                int overlayPage = GetPageNumber(OverlayPageTextBox.Text);

                if (overlayPage > 1)
                    canGoPrevious = true;

                if (!_overlayPageCount.HasValue || overlayPage < _overlayPageCount.Value)
                    canGoNext = true;
            }

            PreviousPageButton.IsEnabled = canGoPrevious;
            NextPageButton.IsEnabled = canGoNext;



        }

        private void UpdateOverlayNavigationButtons()
        {
            // No overlay loaded
            if (string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                PreviousPageOffsetButton.IsEnabled = false;
                NextPageOffsetButton.IsEnabled = false;
                return;
            }

            int currentPage = GetPageNumber(OverlayPageTextBox.Text);

            bool canGoPrev = currentPage > 1;
            bool canGoNext = !_overlayPageCount.HasValue || currentPage < _overlayPageCount.Value;

            PreviousPageOffsetButton.IsEnabled = canGoPrev;
            NextPageOffsetButton.IsEnabled = canGoNext;

        }
        private void UpdatePageInputState()
        {
            if (string.IsNullOrWhiteSpace(_baseFilePath))
            {
                BasePageTextBox.Text = "";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(BasePageTextBox.Text))
                    BasePageTextBox.Text = "1";
            }

            if (string.IsNullOrWhiteSpace(_overlayFilePath))
            {
                OverlayPageTextBox.Text = "";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(OverlayPageTextBox.Text))
                    OverlayPageTextBox.Text = "1";
            }
        }
        private BitmapSource GetCachedPage(string filePath, int pageIndex)
        {
            var key = (filePath, pageIndex);

            lock (_pageCacheLock)
            {
                if (_pageCache.TryGetValue(key, out BitmapSource? cached) && cached != null)
                {
                    return cached;
                }
            }

            BitmapSource image = RenderPdfPageToBitmapSource(filePath, pageIndex);

            lock (_pageCacheLock)
            {
                if (!_pageCache.ContainsKey(key))
                {
                    _pageCache[key] = image;
                    UpdateMemoryUsageDisplay();
                }

                return _pageCache[key];
            }
        }

        private BitmapSource GetCachedTintedPage(
                                                    string filePath,
                                                    int pageIndex,
                                                    BitmapSource source,
                                                    string role,
                                                    Color tintColor)
        {
            var key = (filePath, pageIndex, role);

            lock (_tintCacheLock)
            {
                if (_tintCache.TryGetValue(key, out BitmapSource? cached) && cached != null)
                {
                    return cached;
                }
            }

            BitmapSource tinted = CreateTintedImage(source, tintColor);

            lock (_tintCacheLock)
            {
                if (!_tintCache.ContainsKey(key))
                {
                    _tintCache[key] = tinted;
                    UpdateMemoryUsageDisplay();
                }

                return _tintCache[key];
            }
        }
        private int GetCacheRadius()
        {
            return Math.Max(0, (int)Math.Round(setOffset));
        }

        private string GetPdfBase64(string filePath)
        {
            lock (_pdfCacheLock)
            {
                if (_pdfCache.TryGetValue(filePath, out string? cachedPdfBytes) &&
                    cachedPdfBytes != null)
                {
                    return cachedPdfBytes;
                }
            }

            string pdfBytes = Convert.ToBase64String(File.ReadAllBytes(filePath));

            lock (_pdfCacheLock)
            {
                if (!_pdfCache.ContainsKey(filePath))
                {
                    _pdfCache[filePath] = pdfBytes;
                }

                return _pdfCache[filePath];
            }
        }
        private void PreloadAdjacentPages(
    string filePath,
    int currentPageIndex,
    int? pageCount,
    string role,
    Color tintColor,
    bool useTint)
        {

            SetProcessingState(true);

            Task.Run(() =>
            {
                try
                {
                    int cacheRadius = GetCacheRadius();
                    for (int offset = 1; offset <= cacheRadius; offset++)
                    {
                        int prev = currentPageIndex - offset;
                        int next = currentPageIndex + offset;

                        // PREVIOUS PAGE
                        if (prev >= 0)
                        {
                            BitmapSource prevImage = GetCachedPage(filePath, prev);

                            if (useTint)
                            {
                                GetCachedTintedPage(filePath, prev, prevImage, role, tintColor);
                            }
                        }

                        // NEXT PAGE
                        if (!pageCount.HasValue || next < pageCount.Value)
                        {
                            BitmapSource nextImage = GetCachedPage(filePath, next);

                            if (useTint)
                            {
                                GetCachedTintedPage(filePath, next, nextImage, role, tintColor);
                            }
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

        private bool IsPageCached(string filePath, int pageIndex)
        {
            var key = (filePath, pageIndex);

            lock (_pageCacheLock)
            {
                return _pageCache.ContainsKey(key);
            }
        }
        private void PrunePageCache(string filePath, int currentPageIndex)
        {
            int cacheRadius = GetCacheRadius();

            lock (_pageCacheLock)
            {
                var keysToRemove = _pageCache.Keys
                    .Where(k =>
                        k.path == filePath &&
                        Math.Abs(k.page - currentPageIndex) > cacheRadius)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _pageCache.Remove(key);
                }
            }

            UpdateMemoryUsageDisplay();

            // Also prune tint cache
            lock (_tintCacheLock)
            {
                var tintKeysToRemove = _tintCache.Keys
                    .Where(k =>
                        k.path == filePath &&
                        Math.Abs(k.page - currentPageIndex) > cacheRadius)
                    .ToList();

                foreach (var key in tintKeysToRemove)
                {
                    _tintCache.Remove(key);
                }
            }

            UpdateMemoryUsageDisplay();
        }
        private void UpdatePageTextColor(TextBox textBox, int currentPage, int? pageCount)
        {
            bool isAtLowerLimit = currentPage <= 1;
            bool isAtUpperLimit = pageCount.HasValue && currentPage >= pageCount.Value;

            if (isAtLowerLimit || isAtUpperLimit)
            {
                textBox.Foreground = Brushes.Red;
            }
            else
            {
                textBox.Foreground = Brushes.Black;
            }
        }
        private string? GetBasePrefix()
        {
            if (string.IsNullOrWhiteSpace(_baseFilePath))
                return null;

            string name = Path.GetFileNameWithoutExtension(_baseFilePath);
            int idx = name.IndexOf('_');

            return idx > 0 ? name.Substring(0, idx) : name;
        }
        private string? SelectOverlayFileFiltered()
        {
            if (string.IsNullOrWhiteSpace(_baseFilePath))
                return SelectPdfOrImageFile(); // fallback

            string baseDirectory = Path.GetDirectoryName(_baseFilePath)!;
            string? basePrefix = GetBasePrefix();

            if (string.IsNullOrWhiteSpace(basePrefix))
                return SelectPdfOrImageFile();

            string baseFileName = Path.GetFileNameWithoutExtension(_baseFilePath);

            // ✅ Get matching files
            var matchingFiles = Directory.GetFiles(baseDirectory, "*.pdf")
    .Where(f =>
    {
        string name = Path.GetFileNameWithoutExtension(f);

        return name.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)
            && !name.Equals(baseFileName, StringComparison.OrdinalIgnoreCase); // ✅ EXCLUDE BASE FILE
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
            var listBox = new ListBox
            {
                ItemsSource = matchingFiles,
                Margin = new Thickness(10)
            };
            // ✅ Let user pick from filtered list
            var dialog = new Window
            {
                Title = $"Double-click to select revision",
                Width = 500,
                Height = 400,
                Content = new ListBox
                {
                    ItemsSource = matchingFiles,
                    Margin = new Thickness(10)
                }
            };

            ListBox list = (ListBox)dialog.Content;

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

            lock (_pageCacheLock)
            {
                var pageKeysToRemove = _pageCache.Keys
                    .Where(k => k.path.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var key in pageKeysToRemove)
                {
                    _pageCache.Remove(key);
                }
            }

            lock (_tintCacheLock)
            {
                var tintKeysToRemove = _tintCache.Keys
                    .Where(k => k.path.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var key in tintKeysToRemove)
                {
                    _tintCache.Remove(key);
                }
            }

            lock (_pdfCacheLock)
            {
                _pdfCache.Remove(filePath);
            }
        }
    }
}