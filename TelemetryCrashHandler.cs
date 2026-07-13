using System.Windows;
using System.Windows.Threading;

namespace PdfOverlayTool
{
    /// <summary>
    /// Registers process-wide exception handlers and sends crash telemetry.
    /// </summary>
    public static class TelemetryCrashHandler
    {
        private const int MaxCrashMessageLength = 2000;
        private const int MaxCrashStackLength = 8000;

        public static void Initialize()
        {
            if (Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            }

            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            TelemetryContext.ReportCrash(e.Exception, "ui_thread", isTerminating: true);

            try
            {
                MessageBox.Show(
                    "Overlay Compare Tool encountered an unexpected error and needs to close.\n\n"
                    + TruncateForDisplay(e.Exception.Message),
                    "Unexpected Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // Non-fatal while shutting down after a crash.
            }

            e.Handled = true;
            Application.Current.Shutdown(-1);
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                TelemetryContext.ReportCrash(ex, "app_domain", e.IsTerminating);
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            TelemetryContext.ReportCrash(e.Exception, "background_task", isTerminating: false);
        }

        internal static Dictionary<string, object?> BuildCrashDetails(
            Exception exception,
            string origin,
            bool isTerminating)
        {
            return new Dictionary<string, object?>
            {
                ["origin"] = origin,
                ["isTerminating"] = isTerminating,
                ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
                ["message"] = Truncate(exception.Message, MaxCrashMessageLength),
                ["stackTrace"] = Truncate(exception.StackTrace ?? "", MaxCrashStackLength)
            };
        }

        private static string TruncateForDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "No error message was available.";
            }

            return Truncate(value, 500);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength] + "…";
        }
    }
}
