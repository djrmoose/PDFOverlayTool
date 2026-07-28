using System.Globalization;

namespace PdfOverlayTool
{
    /// <summary>
    /// Beta limits. Change <see cref="FileLoadingDisabledDate"/> to adjust when
    /// custom file loading is turned off and demonstration mode begins.
    /// </summary>
    public static class BetaConfig
    {
        public static readonly DateOnly FileLoadingDisabledDate = new(2026, 12, 1);

        public const string VersionNumber = "v1.0.1-beta.2";

        public const string DeveloperEmail = "djrmoose@gmail.com";

        public static bool IsFileLoadingDisabled =>
            DateOnly.FromDateTime(DateTime.Today) >= FileLoadingDisabledDate;

        public static string FileLoadingDisabledDateDisplay =>
            FileLoadingDisabledDate.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);

        public static int GetCurrentWeekKey(DateTime? date = null)
        {
            DateTime value = date ?? DateTime.Today;
            return ISOWeek.GetYear(value) * 100 + ISOWeek.GetWeekOfYear(value);
        }
    }
}
