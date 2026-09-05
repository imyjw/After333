using System;

namespace Project333.Runtime.Presentation
{
    public static class Project333ClientBuildInfo
    {
        public const string CurrentClientVersion = "0.1.0-dev";
        public const string LegacyDevelopmentClientVersion = "unity-dev";

        public static string ResolveClientVersion(string inspectorOverride)
        {
            if (string.IsNullOrWhiteSpace(inspectorOverride) ||
                string.Equals(inspectorOverride.Trim(), LegacyDevelopmentClientVersion, StringComparison.OrdinalIgnoreCase))
            {
                return CurrentClientVersion;
            }

            return inspectorOverride.Trim();
        }
    }
}
