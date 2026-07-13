namespace PdfOverlayTool
{
    /// <summary>
    /// Google Apps Script web app endpoint for beta registration and usage telemetry.
    /// Configure via %LocalAppData%\PdfOverlayTool\telemetry.json (recommended) or WebAppUrl below.
    /// </summary>
    public static class TelemetryConfig
    {
        /// <summary>Optional compile-time fallback if telemetry.json is missing.</summary>
        public const string WebAppUrl = "https://script.google.com/macros/s/AKfycbzBSGLEkA0eGDwH9C6-2NsL1oYrMyqn1l2x7qyGYDdSYKEJJby_wPiZOjgZp0406WQS/exec";

        public static bool IsEnabled => !string.IsNullOrWhiteSpace(ResolveWebAppUrl());

        public static string ResolveWebAppUrl()
        {
            if (!string.IsNullOrWhiteSpace(WebAppUrl))
            {
                return WebAppUrl.Trim();
            }

            return TelemetrySettings.Load().WebAppUrl.Trim();
        }
    }
}
