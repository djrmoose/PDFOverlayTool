using System.IO;
using System.Text.Json;

namespace PdfOverlayTool
{
    /// <summary>
    /// User preferences persisted under %LocalAppData%\Overlay Compare Tool\settings.json.
    /// </summary>
    public sealed class UserSettings
    {
        public double Opacity { get; set; } = 50;
        public double Dpi { get; set; } = 250;
        public double PageCache { get; set; } = 5;
        public double Sensitivity { get; set; } = 200;
        public bool OverlayOnlyRevisions { get; set; }
        public string RevisionSeparator { get; set; } = "_";
        public bool IsAutoMode { get; set; } = true;
        public string ColorPaletteMode { get; set; } = ColorPalette.BlueGreyPaletteName;
        public bool ColorBlindFriendly { get; set; }
        public int LastWeeklyReminderWeekKey { get; set; }
        public int LastWeeklyFeedbackWeekKey { get; set; }

        /// <summary>Default values for user preferences (excludes registration and install identity).</summary>
        public static UserSettings CreatePreferenceDefaults()
        {
            return new UserSettings
            {
                Opacity = 50,
                Dpi = 250,
                PageCache = 5,
                Sensitivity = 200,
                OverlayOnlyRevisions = false,
                RevisionSeparator = "_",
                IsAutoMode = true,
                ColorPaletteMode = ColorPalette.BlueGreyPaletteName,
                ColorBlindFriendly = false
            };
        }
        public bool RegistrationComplete { get; set; }
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string InstallId { get; set; } = "";
        public bool TermsAccepted { get; set; }
        public string TermsVersion { get; set; } = "";
        public string TermsAcceptedUtc { get; set; } = "";
        public bool DemoIntroCompleted { get; set; }

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Overlay Compare Tool");

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
