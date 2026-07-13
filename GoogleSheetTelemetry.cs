using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PdfOverlayTool
{
    public static class GoogleSheetTelemetry
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ExitRequestTimeout = TimeSpan.FromSeconds(5);

        public static void SendRegistration(
            string installId,
            string name,
            string email,
            bool termsAccepted,
            string termsVersion,
            DateTime termsAcceptedUtc)
        {
            var payload = BuildPayload("registration", installId, name, email);
            payload.Details = BuildTermsDetails(termsAccepted, termsVersion, termsAcceptedUtc);
            SendFireAndForget(payload);
        }

        public static void SendCrashOnExit(
            string installId,
            string name,
            string email,
            bool isAutoMode,
            int sessionSeconds,
            bool termsAccepted,
            string termsVersion,
            string termsAcceptedUtc,
            SessionCloseSnapshot? sessionSnapshot,
            ColorPaletteSelection paletteSelection,
            Exception exception,
            string origin,
            bool isTerminating)
        {
            if (!TelemetryConfig.IsEnabled)
            {
                return;
            }

            var payload = BuildPayload("crash", installId, name, email);
            payload.IsAutoMode = isAutoMode;
            payload.SessionSeconds = sessionSeconds;

            var details = sessionSnapshot == null
                ? BuildRuntimeDetails(termsAccepted, termsVersion, termsAcceptedUtc, paletteSelection)
                : BuildSessionDetails(termsAccepted, termsVersion, termsAcceptedUtc, sessionSnapshot);

            foreach (var entry in TelemetryCrashHandler.BuildCrashDetails(exception, origin, isTerminating))
            {
                details[entry.Key] = entry.Value;
            }

            payload.Details = details;

            if (isTerminating)
            {
                try
                {
                    PostToAppsScriptAsync(payload, ExitRequestTimeout).GetAwaiter().GetResult();
                }
                catch
                {
                    // Non-fatal: telemetry must never block or crash the app.
                }

                return;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    PostToAppsScriptAsync(payload, ExitRequestTimeout).GetAwaiter().GetResult();
                }
                catch
                {
                    // Non-fatal: telemetry must never block or crash the app.
                }
            })
            {
                IsBackground = false,
                Name = "TelemetryCrashSend"
            };

            thread.Start();
        }

        public static void SendSession(
            string installId,
            string name,
            string email,
            bool isAutoMode,
            int sessionSeconds,
            bool termsAccepted,
            string termsVersion,
            string termsAcceptedUtc,
            SessionCloseSnapshot sessionSnapshot)
        {
            SendSessionOnExit(
                installId,
                name,
                email,
                isAutoMode,
                sessionSeconds,
                termsAccepted,
                termsVersion,
                termsAcceptedUtc,
                sessionSnapshot);
        }

        /// <summary>
        /// Sends session telemetry without blocking window close. The process may linger
        /// briefly in the background until the POST finishes or times out.
        /// </summary>
        public static void SendSessionOnExit(
            string installId,
            string name,
            string email,
            bool isAutoMode,
            int sessionSeconds,
            bool termsAccepted,
            string termsVersion,
            string termsAcceptedUtc,
            SessionCloseSnapshot sessionSnapshot)
        {
            if (!TelemetryConfig.IsEnabled)
            {
                return;
            }

            var payload = BuildPayload("session", installId, name, email);
            payload.IsAutoMode = isAutoMode;
            payload.SessionSeconds = sessionSeconds;
            payload.Details = BuildSessionDetails(
                termsAccepted,
                termsVersion,
                termsAcceptedUtc,
                sessionSnapshot);

            var thread = new Thread(() =>
            {
                try
                {
                    PostToAppsScriptAsync(payload, ExitRequestTimeout).GetAwaiter().GetResult();
                }
                catch
                {
                    // Non-fatal: telemetry must never block or crash the app.
                }
            })
            {
                IsBackground = false,
                Name = "TelemetrySessionSend"
            };

            thread.Start();
        }

        private static Dictionary<string, object?> BuildTermsDetails(
            bool termsAccepted,
            string termsVersion,
            DateTime termsAcceptedUtc)
        {
            return new Dictionary<string, object?>
            {
                ["termsAccepted"] = termsAccepted,
                ["termsVersion"] = termsVersion,
                ["termsAcceptedUtc"] = termsAcceptedUtc.ToString("o"),
                ["demoOnly"] = BetaConfig.IsFileLoadingDisabled
            };
        }

        private static Dictionary<string, object?> BuildRuntimeDetails(
            bool termsAccepted,
            string termsVersion,
            string termsAcceptedUtc,
            ColorPaletteSelection? paletteSelection = null)
        {
            var details = new Dictionary<string, object?>
            {
                ["termsAccepted"] = termsAccepted,
                ["termsVersion"] = termsVersion,
                ["termsAcceptedUtc"] = termsAcceptedUtc,
                ["demoOnly"] = BetaConfig.IsFileLoadingDisabled
            };

            if (paletteSelection.HasValue)
            {
                AddColorPaletteDetails(details, paletteSelection.Value);
            }

            return details;
        }

        private static void AddColorPaletteDetails(Dictionary<string, object?> details, ColorPaletteSelection selection)
        {
            details["colorPalette"] = ColorPalette.GetThemeName(selection.Theme);
            details["colorBlindFriendly"] = selection.ColorBlindFriendly;
        }

        private static Dictionary<string, object?> BuildSessionDetails(
            bool termsAccepted,
            string termsVersion,
            string termsAcceptedUtc,
            SessionCloseSnapshot snapshot)
        {
            var paletteSelection = ColorPalette.ParseSettings(
                snapshot.Settings.ColorPaletteName,
                snapshot.Settings.ColorBlindFriendly);
            var details = BuildRuntimeDetails(
                termsAccepted,
                termsVersion,
                termsAcceptedUtc,
                paletteSelection);

            details["settingsAtClose"] = new Dictionary<string, object?>
            {
                ["opacity"] = snapshot.Settings.Opacity,
                ["dpi"] = snapshot.Settings.Dpi,
                ["pageCache"] = snapshot.Settings.PageCache,
                ["sensitivity"] = snapshot.Settings.Sensitivity,
                ["isAutoMode"] = snapshot.Settings.IsAutoMode,
                ["overlayOnlyRevisions"] = snapshot.Settings.OverlayOnlyRevisions,
                ["tintEnabled"] = snapshot.Settings.TintEnabled,
                ["colorBlindFriendly"] = snapshot.Settings.ColorBlindFriendly,
                ["colorPalette"] = snapshot.Settings.ColorPaletteName
            };

            details["filesOpenedCount"] = snapshot.FilesOpenedCount;
            details["maxFileSizeMb"] = snapshot.MaxFileSizeMegabytes;
            details["maxFilePageCount"] = snapshot.MaxFilePageCount;
            details["avgFileSizeMb"] = snapshot.AvgFileSizeMegabytes;
            details["avgFilePageCount"] = snapshot.AvgFilePageCount;
            details["demoStatus"] = snapshot.DemoStatus;
            details["helpClickCount"] = snapshot.HelpClickCount;
            details["autoMemoryReductionEngaged"] = snapshot.AutoMemoryReductionEngaged;
            details["autoMemoryRecoveryEngaged"] = snapshot.AutoMemoryRecoveryEngaged;
            details["autoMemoryManagementEngaged"] = snapshot.AutoMemoryManagementEngaged;
            details["maxCacheMb"] = snapshot.MaxCacheMegabytes;

            return details;
        }

        private static TelemetryPayload BuildPayload(string type, string installId, string name, string email)
        {
            return new TelemetryPayload
            {
                Type = type,
                InstallId = installId,
                Name = name,
                Email = email,
                Version = BetaConfig.VersionNumber,
                Os = Environment.OSVersion.VersionString
            };
        }

        private static void SendFireAndForget(TelemetryPayload payload)
        {
            if (!TelemetryConfig.IsEnabled)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await PostToAppsScriptAsync(payload);
                }
                catch
                {
                    // Non-fatal: telemetry must never block or crash the app.
                }
            });
        }

        /// <summary>
        /// Apps Script web apps respond to POST with a 302 redirect; the JSON body
        /// is returned on a follow-up GET to the Location URL.
        /// </summary>
        private static async Task PostToAppsScriptAsync(TelemetryPayload payload, TimeSpan? timeout = null)
        {
            string webAppUrl = TelemetryConfig.ResolveWebAppUrl();
            string json = JsonSerializer.Serialize(payload, JsonOptions);

            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = timeout ?? RequestTimeout };
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage postResponse =
                await client.PostAsync(webAppUrl, content).ConfigureAwait(false);

            if (!IsRedirectStatus(postResponse.StatusCode))
            {
                return;
            }

            Uri redirectUri = ResolveRedirectUri(webAppUrl, postResponse.Headers.Location);
            using HttpResponseMessage _ = await client.GetAsync(redirectUri).ConfigureAwait(false);
        }

        private static Uri ResolveRedirectUri(string webAppUrl, Uri? location)
        {
            if (location == null)
            {
                throw new InvalidOperationException("Apps Script redirect response did not include a Location header.");
            }

            return location.IsAbsoluteUri ? location : new Uri(new Uri(webAppUrl), location);
        }

        private static bool IsRedirectStatus(System.Net.HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code is 301 or 302 or 303 or 307 or 308;
        }

        private sealed class TelemetryPayload
        {
            public string Type { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
            public string InstallId { get; set; } = "";
            public string Version { get; set; } = "";
            public string Os { get; set; } = "";
            public bool? IsAutoMode { get; set; }
            public int? SessionSeconds { get; set; }
            public Dictionary<string, object?>? Details { get; set; }
        }
    }
}
