using System;
using System.Text;

namespace Project333.Runtime.Application.Accounts
{
    public static class Project333ClientProfile
    {
        public const string ProfileEnvironmentVariable = "PROJECT333_CLIENT_PROFILE";
        public const string ServerUrlEnvironmentVariable = "PROJECT333_CLIENT_SERVER_URL";
        public const string BattleWebSocketUrlEnvironmentVariable = "PROJECT333_CLIENT_BATTLE_WEBSOCKET_URL";

        private static string _cachedProfileId;
        private static bool _hasResolvedProfileId;

        public static string ProfileId
        {
            get
            {
                if (!_hasResolvedProfileId)
                {
                    _cachedProfileId = SanitizeProfileId(ResolveProfileId());
                    _hasResolvedProfileId = true;
                }

                return _cachedProfileId ?? string.Empty;
            }
        }

        public static bool HasProfile => !string.IsNullOrWhiteSpace(ProfileId);
        public static string DisplayName => HasProfile ? ProfileId : "default";

        public static string ToScopedPlayerPrefsKey(string baseKey)
        {
            if (string.IsNullOrWhiteSpace(baseKey))
            {
                return string.Empty;
            }

            return HasProfile
                ? $"{baseKey}.Profile.{ProfileId}"
                : baseKey;
        }

        public static string GetServerUrlOverride()
        {
            return GetOptionValue(
                ServerUrlEnvironmentVariable,
                "-project333ServerUrl",
                "--project333-server-url");
        }

        public static string GetBattleWebSocketUrlOverride()
        {
            return GetOptionValue(
                BattleWebSocketUrlEnvironmentVariable,
                "-project333BattleWebSocketUrl",
                "--project333-battle-websocket-url");
        }

        private static string ResolveProfileId()
        {
            var profileFromArgs = GetOptionValue(
                ProfileEnvironmentVariable,
                "-project333Profile",
                "--project333-profile");
            return profileFromArgs ?? string.Empty;
        }

        private static string GetOptionValue(string environmentVariableName, params string[] optionNames)
        {
            var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue.Trim();
            }

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                foreach (var optionName in optionNames)
                {
                    if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i + 1 < args.Length ? args[i + 1]?.Trim() : string.Empty;
                    }

                    var prefix = optionName + "=";
                    if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return arg.Substring(prefix.Length).Trim();
                    }
                }
            }

            return string.Empty;
        }

        private static string SanitizeProfileId(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(profileId.Length);
            foreach (var character in profileId.Trim())
            {
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.')
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('_');
                }
            }

            var sanitized = builder.ToString().Trim('.', '-', '_');
            return sanitized.Length <= 48 ? sanitized : sanitized.Substring(0, 48);
        }
    }
}
