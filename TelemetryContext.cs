namespace PdfOverlayTool
{
    /// <summary>
    /// In-memory telemetry state shared with crash handlers. Updated by <see cref="MainWindow"/>.
    /// </summary>
    public static class TelemetryContext
    {
        private static readonly object Lock = new();

        private static Func<SessionCloseSnapshot>? _sessionSnapshotFactory;
        private static Func<bool>? _isAutoModeFactory;
        private static DateTime _sessionStartUtc;
        private static bool _crashReported;

        public static bool CrashReported
        {
            get
            {
                lock (Lock)
                {
                    return _crashReported;
                }
            }
        }

        public static void BeginSession(DateTime sessionStartUtc)
        {
            lock (Lock)
            {
                _sessionStartUtc = sessionStartUtc;
            }
        }

        public static void RegisterSessionReporting(
            Func<SessionCloseSnapshot> sessionSnapshotFactory,
            Func<bool> isAutoModeFactory)
        {
            lock (Lock)
            {
                _sessionSnapshotFactory = sessionSnapshotFactory;
                _isAutoModeFactory = isAutoModeFactory;
            }
        }

        public static void ReportCrash(Exception exception, string origin, bool isTerminating)
        {
            lock (Lock)
            {
                if (_crashReported)
                {
                    return;
                }

                _crashReported = true;
            }

            UserSettings settings = UserSettings.Load();
            if (!settings.RegistrationComplete)
            {
                return;
            }

            SessionCloseSnapshot? snapshot = null;
            try
            {
                snapshot = _sessionSnapshotFactory?.Invoke();
            }
            catch
            {
                // Crash may have corrupted app state; send what we can.
            }

            bool isAutoMode;
            try
            {
                isAutoMode = _isAutoModeFactory?.Invoke() ?? settings.IsAutoMode;
            }
            catch
            {
                isAutoMode = settings.IsAutoMode;
            }

            int sessionSeconds;
            lock (Lock)
            {
                sessionSeconds = _sessionStartUtc == default
                    ? 0
                    : Math.Max(0, (int)(DateTime.UtcNow - _sessionStartUtc).TotalSeconds);
            }

            GoogleSheetTelemetry.SendCrashOnExit(
                settings.InstallId,
                settings.UserName,
                settings.UserEmail,
                isAutoMode,
                sessionSeconds,
                settings.TermsAccepted,
                settings.TermsVersion ?? "",
                settings.TermsAcceptedUtc ?? "",
                snapshot,
                ColorPalette.ParseSettings(settings.ColorPaletteMode, settings.ColorBlindFriendly),
                exception,
                origin,
                isTerminating);
        }
    }
}
