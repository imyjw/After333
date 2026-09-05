using UnityEngine;
using Project333.Runtime.Application.Accounts;

namespace Project333.Runtime.Presentation
{
    public static class Project333ServerEndpointSettings
    {
        public const string LocalHttpUrl = "http://127.0.0.1:7333";
        public const string PublicHttpUrl = "https://api.after333.com";
        public const string LocalBattleWebSocketUrl = "ws://127.0.0.1:7333/battle";
        public const string PublicBattleWebSocketUrl = "wss://api.after333.com/battle";

        // Keep the original constants for compatibility with existing scene builders and tools.
        public const string DefaultHttpUrl = LocalHttpUrl;
        public const string DefaultBattleWebSocketUrl = LocalBattleWebSocketUrl;
        public const string HttpUrlPlayerPrefsKey = "Project333.Server.HttpUrl";
        public const string BattleWebSocketUrlPlayerPrefsKey = "Project333.Server.BattleWebSocketUrl";

        public static string ResolveHttpUrl(string inspectorUrl)
        {
            var commandLineUrl = Project333ClientProfile.GetServerUrlOverride();
            if (!string.IsNullOrWhiteSpace(commandLineUrl))
            {
                return NormalizeHttpUrl(commandLineUrl);
            }

            var overrideUrl = PlayerPrefs.GetString(ScopedKey(HttpUrlPlayerPrefsKey), string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return NormalizeHttpUrl(overrideUrl);
            }

            return ResolveEnvironmentHttpUrl(inspectorUrl, UsesEditorDefaults);
        }

        public static string ResolveBattleWebSocketUrl(string inspectorUrl)
        {
            var commandLineUrl = Project333ClientProfile.GetBattleWebSocketUrlOverride();
            if (!string.IsNullOrWhiteSpace(commandLineUrl))
            {
                return NormalizeBattleWebSocketUrl(commandLineUrl);
            }

            var commandLineHttpUrl = Project333ClientProfile.GetServerUrlOverride();
            if (!string.IsNullOrWhiteSpace(commandLineHttpUrl))
            {
                return DeriveBattleWebSocketUrl(commandLineHttpUrl);
            }

            var overrideUrl = PlayerPrefs.GetString(ScopedKey(BattleWebSocketUrlPlayerPrefsKey), string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return NormalizeBattleWebSocketUrl(overrideUrl);
            }

            var httpOverrideUrl = PlayerPrefs.GetString(ScopedKey(HttpUrlPlayerPrefsKey), string.Empty);
            if (!string.IsNullOrWhiteSpace(httpOverrideUrl))
            {
                return DeriveBattleWebSocketUrl(httpOverrideUrl);
            }

            return ResolveEnvironmentBattleWebSocketUrl(inspectorUrl, UsesEditorDefaults);
        }

        public static string ResolveEnvironmentHttpUrl(string inspectorUrl, bool isEditor)
        {
            var environmentDefault = isEditor ? LocalHttpUrl : PublicHttpUrl;
            if (string.IsNullOrWhiteSpace(inspectorUrl))
            {
                return environmentDefault;
            }

            var normalizedUrl = NormalizeHttpUrl(inspectorUrl);
            return !isEditor && IsLegacyLocalHttpDefault(normalizedUrl)
                ? PublicHttpUrl
                : normalizedUrl;
        }

        public static string ResolveEnvironmentBattleWebSocketUrl(string inspectorUrl, bool isEditor)
        {
            var environmentDefault = isEditor ? LocalBattleWebSocketUrl : PublicBattleWebSocketUrl;
            if (string.IsNullOrWhiteSpace(inspectorUrl))
            {
                return environmentDefault;
            }

            var normalizedUrl = NormalizeBattleWebSocketUrl(inspectorUrl);
            return !isEditor && IsLegacyLocalBattleWebSocketDefault(normalizedUrl)
                ? PublicBattleWebSocketUrl
                : normalizedUrl;
        }

        public static void SaveHttpUrlOverride(string httpUrl)
        {
            if (string.IsNullOrWhiteSpace(httpUrl))
            {
                PlayerPrefs.DeleteKey(ScopedKey(HttpUrlPlayerPrefsKey));
            }
            else
            {
                PlayerPrefs.SetString(ScopedKey(HttpUrlPlayerPrefsKey), NormalizeHttpUrl(httpUrl));
            }

            PlayerPrefs.Save();
        }

        public static void SaveBattleWebSocketUrlOverride(string webSocketUrl)
        {
            if (string.IsNullOrWhiteSpace(webSocketUrl))
            {
                PlayerPrefs.DeleteKey(ScopedKey(BattleWebSocketUrlPlayerPrefsKey));
            }
            else
            {
                PlayerPrefs.SetString(ScopedKey(BattleWebSocketUrlPlayerPrefsKey), NormalizeBattleWebSocketUrl(webSocketUrl));
            }

            PlayerPrefs.Save();
        }

        public static void ClearOverrides()
        {
            PlayerPrefs.DeleteKey(ScopedKey(HttpUrlPlayerPrefsKey));
            PlayerPrefs.DeleteKey(ScopedKey(BattleWebSocketUrlPlayerPrefsKey));
            PlayerPrefs.Save();
        }

        public static string DeriveBattleWebSocketUrl(string httpUrl)
        {
            var normalizedHttpUrl = NormalizeHttpUrl(httpUrl);
            if (normalizedHttpUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                return EnsureBattlePath("wss://" + normalizedHttpUrl.Substring("https://".Length));
            }

            if (normalizedHttpUrl.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))
            {
                return EnsureBattlePath("ws://" + normalizedHttpUrl.Substring("http://".Length));
            }

            return DefaultBattleWebSocketUrl;
        }

        private static string NormalizeHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return DefaultHttpUrl;
            }

            return url.Trim().TrimEnd('/');
        }

        private static string NormalizeBattleWebSocketUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return DefaultBattleWebSocketUrl;
            }

            var normalizedUrl = url.Trim().TrimEnd('/');
            if (normalizedUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) ||
                normalizedUrl.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))
            {
                return DeriveBattleWebSocketUrl(normalizedUrl);
            }

            return EnsureBattlePath(normalizedUrl);
        }

        private static string EnsureBattlePath(string webSocketUrl)
        {
            if (string.IsNullOrWhiteSpace(webSocketUrl))
            {
                return DefaultBattleWebSocketUrl;
            }

            var normalizedUrl = webSocketUrl.Trim().TrimEnd('/');
            return normalizedUrl.EndsWith("/battle", System.StringComparison.OrdinalIgnoreCase)
                ? normalizedUrl
                : normalizedUrl + "/battle";
        }

        private static string ScopedKey(string baseKey)
        {
            return Project333ClientProfile.ToScopedPlayerPrefsKey(baseKey);
        }

        private static bool IsLegacyLocalHttpDefault(string url)
        {
            return string.Equals(url, LocalHttpUrl, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(url, "http://localhost:7333", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacyLocalBattleWebSocketDefault(string url)
        {
            return string.Equals(url, LocalBattleWebSocketUrl, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(url, "ws://localhost:7333/battle", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesEditorDefaults
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}
