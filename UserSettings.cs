using System.IO;
using System.Text.Json;

namespace PdfOverlayTool
{
    /// <summary>
    /// User preferences persisted under %LocalAppData%\PdfOverlayTool\settings.json.
    /// </summary>
    public sealed class UserSettings
    {
        public double Opacity { get; set; } = 50;
        public double Dpi { get; set; } = 250;
        public double PageCache { get; set; } = 5;
        public double Sensitivity { get; set; } = 200;
        public bool OverlayOnlyRevisions { get; set; }

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PdfOverlayTool");

        private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new UserSettings();
                }

                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Non-fatal: the app still runs with in-memory values.
            }
        }
    }
}
