namespace PdfOverlayTool
{
    /// <summary>
    /// Google Apps Script web app endpoint for beta registration and usage telemetry.
    /// Configure via %LocalAppData%\Overlay Compare Tool\telemetry.json (recommended) or WebAppUrl below.
    /// </summary>
    public static class TelemetryConfig
    {
        /// <summary>Optional compile-time fallback if telemetry.json is missing.</summary>
        public const string WebAppUrl = "https://script.google.com/macros/s/AKfycbzBSGLEkA0eGDwH9C6-2NsL1oYrMyqn1l2x7qyGYDdSYKEJJby_wPiZOjgZp0406WQS/exec";

        public static bool IsEnabled => !string.IsNullOrWhiteSpace(ResolveWebAppUrl());

        public static string ResolveWebAppUrl()
        {
            string fromFile = TelemetrySettings.Load().WebAppUrl.Trim();
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                return fromFile;
            }

            return WebAppUrl.Trim();
        }
    }
}
