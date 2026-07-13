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
        private readonly List<FileOpenRecord> _fileOpens = new();

        private int _helpClickCount;
        private bool _autoMemoryReductionEngaged;
        private bool _autoMemoryRecoveryEngaged;
        private double _maxCacheMegabytes;
        private DemoStatus _demoStatus = DemoStatus.NotApplicable;

        public void Reset()
        {
            lock (_lock)
            {
                _fileOpens.Clear();
                _helpClickCount = 0;
                _autoMemoryReductionEngaged = false;
                _autoMemoryRecoveryEngaged = false;
                _maxCacheMegabytes = 0;
                _demoStatus = DemoStatus.NotApplicable;
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

        public void RecordFileOpen(string role, double sizeMegabytes, int pageCount, bool isDemo)
        {
            lock (_lock)
            {
                _fileOpens.Add(new FileOpenRecord
                {
                    Role = role,
                    SizeMegabytes = Math.Round(sizeMegabytes, 2),
                    PageCount = pageCount,
                    IsDemo = isDemo
                });
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

        /// <summary>Future: user dismissed the interactive demo tour.</summary>
        public void RecordDemoSkipped()
        {
            lock (_lock)
            {
                _demoStatus = DemoStatus.Skipped;
            }
        }

        public SessionCloseSnapshot CreateCloseSnapshot(SessionSettingsSnapshot settings)
        {
            lock (_lock)
            {
                return new SessionCloseSnapshot
                {
                    Settings = settings,
                    FilesOpenedCount = _fileOpens.Count,
                    FilesOpened = _fileOpens.Select(f => f.ToAnonymous()).ToList(),
                    DemoStatus = _demoStatus.ToString(),
                    HelpClickCount = _helpClickCount,
                    AutoMemoryReductionEngaged = _autoMemoryReductionEngaged,
                    AutoMemoryRecoveryEngaged = _autoMemoryRecoveryEngaged,
                    AutoMemoryManagementEngaged = _autoMemoryReductionEngaged || _autoMemoryRecoveryEngaged,
                    MaxCacheMegabytes = Math.Round(_maxCacheMegabytes, 2)
                };
            }
        }

        private sealed class FileOpenRecord
        {
            public string Role { get; set; } = "";
            public double SizeMegabytes { get; set; }
            public int PageCount { get; set; }
            public bool IsDemo { get; set; }

            public Dictionary<string, object?> ToAnonymous() => new()
            {
                ["role"] = Role,
                ["sizeMb"] = SizeMegabytes,
                ["pageCount"] = PageCount,
                ["isDemo"] = IsDemo
            };
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
    }

    public sealed class SessionCloseSnapshot
    {
        public SessionSettingsSnapshot Settings { get; set; } = new();
        public int FilesOpenedCount { get; set; }
        public List<Dictionary<string, object?>> FilesOpened { get; set; } = new();
        public string DemoStatus { get; set; } = "";
        public int HelpClickCount { get; set; }
        public bool AutoMemoryReductionEngaged { get; set; }
        public bool AutoMemoryRecoveryEngaged { get; set; }
        public bool AutoMemoryManagementEngaged { get; set; }
        public double MaxCacheMegabytes { get; set; }
    }
}
