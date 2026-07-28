using System.IO;
using System.Text.Json;

namespace PdfOverlayTool
{
    /// <summary>
    /// Local telemetry endpoint config: %LocalAppData%\Overlay Compare Tool\telemetry.json
    /// </summary>
    public sealed class TelemetrySettings
    {
        public string WebAppUrl { get; set; } = "";

        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Overlay Compare Tool",
                "telemetry.json");

        public static TelemetrySettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new TelemetrySettings();
                }

                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<TelemetrySettings>(json) ?? new TelemetrySettings();
            }
            catch
            {
                return new TelemetrySettings();
            }
        }
    }
}
