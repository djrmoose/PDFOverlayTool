namespace PdfOverlayTool
{
    /// <summary>
    /// Anonymous per-session counters and aggregates sent on application close.
    /// </summary>
    public sealed class SessionTelemetry
    {
        /// <summary>Demo tour / auto-load state for this session.</summary>
        public enum DemoStatus
        {
            /// <summary>Demo-only auto-load was not applicable this session.</summary>
            NotApplicable,

            /// <summary>Demo-only mode: included demos were loaded automatically at startup.</summary>
            AutoLoaded,

            /// <summary>User viewed the interactive demo (future feature).</summary>
            Viewed,

            /// <summary>User skipped the interactive demo (future feature).</summary>
            Skipped
        }

        private readonly object _lock = new();

        private int _filesOpenedCount;
        private double _totalFileSizeMegabytes;
        private long _totalFilePageCount;
        private double _maxFileSizeMegabytes;
        private int _maxFilePageCount;

        private int _helpClickCount;
        private bool _autoMemoryReductionEngaged;
        private bool _autoMemoryRecoveryEngaged;
        private double _maxCacheMegabytes;
        private DemoStatus _demoStatus = DemoStatus.NotApplicable;
        private bool _closeFeedbackPromptShown;
        private bool _closeFeedbackSkipped;
        private string? _closeFeedbackRating;
        private string? _closeFeedbackText;

        public void Reset()
        {
            lock (_lock)
            {
                _filesOpenedCount = 0;
                _totalFileSizeMegabytes = 0;
                _totalFilePageCount = 0;
                _maxFileSizeMegabytes = 0;
                _maxFilePageCount = 0;
                _helpClickCount = 0;
                _autoMemoryReductionEngaged = false;
                _autoMemoryRecoveryEngaged = false;
                _maxCacheMegabytes = 0;
                _demoStatus = DemoStatus.NotApplicable;
                _closeFeedbackPromptShown = false;
                _closeFeedbackSkipped = false;
                _closeFeedbackRating = null;
                _closeFeedbackText = null;
            }
        }

        public void RecordHelpOpened()
        {
            lock (_lock)
            {
                _helpClickCount++;
            }
        }

        public void RecordAutoMemoryReductionEngaged()
        {
            lock (_lock)
            {
                _autoMemoryReductionEngaged = true;
            }
        }

        public void RecordAutoMemoryRecoveryEngaged()
        {
            lock (_lock)
            {
                _autoMemoryRecoveryEngaged = true;
            }
        }

        public void RecordCacheBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            double megabytes = bytes / (1024.0 * 1024.0);
            lock (_lock)
            {
                if (megabytes > _maxCacheMegabytes)
                {
                    _maxCacheMegabytes = megabytes;
                }
            }
        }

        public void RecordFileOpen(double sizeMegabytes, int pageCount)
        {
            lock (_lock)
            {
                _filesOpenedCount++;
                _totalFileSizeMegabytes += sizeMegabytes;
                _totalFilePageCount += pageCount;

                if (sizeMegabytes > _maxFileSizeMegabytes)
                {
                    _maxFileSizeMegabytes = sizeMegabytes;
                }

                if (pageCount > _maxFilePageCount)
                {
                    _maxFilePageCount = pageCount;
                }
            }
        }

        /// <summary>Demo-only mode: demos were loaded automatically at startup.</summary>
        public void RecordDemoAutoLoaded()
        {
            lock (_lock)
            {
                _demoStatus = DemoStatus.AutoLoaded;
            }
        }

        /// <summary>Future: user completed the interactive demo tour.</summary>
        public void RecordDemoViewed()
        {
            lock (_lock)
            {
                _demoStatus = DemoStatus.Viewed;
            }
        }

        /// <summary>User dismissed the interactive demo tour.</summary>
        public void RecordDemoSkipped()
        {
            lock (_lock)
            {
                _demoStatus = DemoStatus.Skipped;
            }
        }

        public void RecordCloseFeedback(bool skipped, string? rating, string? feedbackText)
        {
            lock (_lock)
            {
                _closeFeedbackPromptShown = true;
                _closeFeedbackSkipped = skipped;
                _closeFeedbackRating = rating;
                _closeFeedbackText = feedbackText;
            }
        }

        public SessionCloseSnapshot CreateCloseSnapshot(SessionSettingsSnapshot settings)
        {
            lock (_lock)
            {
                double? avgFileSizeMegabytes = _filesOpenedCount > 0
                    ? Math.Round(_totalFileSizeMegabytes / _filesOpenedCount, 2)
                    : null;
                double? avgFilePageCount = _filesOpenedCount > 0
                    ? Math.Round(_totalFilePageCount / (double)_filesOpenedCount, 2)
                    : null;

                return new SessionCloseSnapshot
                {
                    Settings = settings,
                    FilesOpenedCount = _filesOpenedCount,
                    MaxFileSizeMegabytes = _filesOpenedCount > 0
                        ? Math.Round(_maxFileSizeMegabytes, 2)
                        : null,
                    MaxFilePageCount = _filesOpenedCount > 0 ? _maxFilePageCount : null,
                    AvgFileSizeMegabytes = avgFileSizeMegabytes,
                    AvgFilePageCount = avgFilePageCount,
                    DemoStatus = _demoStatus.ToString(),
                    HelpClickCount = _helpClickCount,
                    AutoMemoryReductionEngaged = _autoMemoryReductionEngaged,
                    AutoMemoryRecoveryEngaged = _autoMemoryRecoveryEngaged,
                    AutoMemoryManagementEngaged = _autoMemoryReductionEngaged || _autoMemoryRecoveryEngaged,
                    MaxCacheMegabytes = Math.Round(_maxCacheMegabytes, 2),
                    CloseFeedbackPromptShown = _closeFeedbackPromptShown,
                    CloseFeedbackSkipped = _closeFeedbackSkipped,
                    CloseFeedbackRating = _closeFeedbackRating,
                    CloseFeedbackText = _closeFeedbackText
                };
            }
        }
    }

    public sealed class SessionSettingsSnapshot
    {
        public double Opacity { get; set; }
        public double Dpi { get; set; }
        public double PageCache { get; set; }
        public double Sensitivity { get; set; }
        public bool IsAutoMode { get; set; }
        public bool OverlayOnlyRevisions { get; set; }
        public bool TintEnabled { get; set; }
        public bool ColorBlindFriendly { get; set; }
        public string ColorPaletteName { get; set; } = ColorPalette.StandardPaletteName;
    }

    public sealed class SessionCloseSnapshot
    {
        public SessionSettingsSnapshot Settings { get; set; } = new();
        public int FilesOpenedCount { get; set; }
        public double? MaxFileSizeMegabytes { get; set; }
        public int? MaxFilePageCount { get; set; }
        public double? AvgFileSizeMegabytes { get; set; }
        public double? AvgFilePageCount { get; set; }
        public string DemoStatus { get; set; } = "";
        public int HelpClickCount { get; set; }
        public bool AutoMemoryReductionEngaged { get; set; }
        public bool AutoMemoryRecoveryEngaged { get; set; }
        public bool AutoMemoryManagementEngaged { get; set; }
        public double MaxCacheMegabytes { get; set; }
        public bool CloseFeedbackPromptShown { get; set; }
        public bool CloseFeedbackSkipped { get; set; }
        public string? CloseFeedbackRating { get; set; }
        public string? CloseFeedbackText { get; set; }
    }
}
