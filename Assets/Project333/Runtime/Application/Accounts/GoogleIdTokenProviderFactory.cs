using System;

namespace Project333.Runtime.Application.Accounts
{
    public enum GoogleIdTokenProviderKind
    {
        Unsupported = 0,
        WindowsDesktop = 1,
        AndroidCredentialManager = 2
    }

    public static class GoogleIdTokenProviderFactory
    {
        public static GoogleIdTokenProviderKind CurrentPlatformKind => SelectProviderKind(
            GoogleDesktopOAuthIdTokenProvider.IsCurrentPlatformSupported,
            GoogleAndroidCredentialIdTokenProvider.IsCurrentPlatformSupported);

        public static GoogleIdTokenProviderKind SelectProviderKind(
            bool supportsWindowsDesktop,
            bool supportsAndroidCredentialManager)
        {
            if (supportsAndroidCredentialManager)
            {
                return GoogleIdTokenProviderKind.AndroidCredentialManager;
            }

            return supportsWindowsDesktop
                ? GoogleIdTokenProviderKind.WindowsDesktop
                : GoogleIdTokenProviderKind.Unsupported;
        }

        public static string ResolveClientId(
            GoogleIdTokenProviderKind providerKind,
            string desktopClientId,
            string androidServerClientId)
        {
            var clientId = providerKind == GoogleIdTokenProviderKind.AndroidCredentialManager
                ? androidServerClientId
                : providerKind == GoogleIdTokenProviderKind.WindowsDesktop
                    ? desktopClientId
                    : string.Empty;
            return clientId?.Trim() ?? string.Empty;
        }

        public static string GetConfigurationFieldName(GoogleIdTokenProviderKind providerKind)
        {
            return providerKind == GoogleIdTokenProviderKind.AndroidCredentialManager
                ? "Google Android Server Client Id"
                : "Google Desktop Client Id";
        }

        public static string GetPlatformDisplayName(GoogleIdTokenProviderKind providerKind)
        {
            switch (providerKind)
            {
                case GoogleIdTokenProviderKind.WindowsDesktop:
                    return "Windows";
                case GoogleIdTokenProviderKind.AndroidCredentialManager:
                    return "Android";
                default:
                    return "지원되지 않는 플랫폼";
            }
        }

        public static IGoogleIdTokenProvider CreateForCurrentPlatform(
            string desktopClientId,
            string androidServerClientId,
            string accountServerUrl)
        {
            switch (CurrentPlatformKind)
            {
                case GoogleIdTokenProviderKind.WindowsDesktop:
                    return new GoogleDesktopOAuthIdTokenProvider(desktopClientId, accountServerUrl);
                case GoogleIdTokenProviderKind.AndroidCredentialManager:
                    return new GoogleAndroidCredentialIdTokenProvider(androidServerClientId);
                default:
                    throw new PlatformNotSupportedException(
                        "Google login is not available on the current platform.");
            }
        }

        public static bool IsServerConfiguredForProvider(
            GoogleIdTokenProviderKind providerKind,
            bool googleTokenValidationConfigured,
            bool desktopCodeExchangeConfigured)
        {
            if (!googleTokenValidationConfigured)
            {
                return false;
            }

            return providerKind != GoogleIdTokenProviderKind.WindowsDesktop ||
                   desktopCodeExchangeConfigured;
        }

        public static bool ShouldAttemptAutomaticAndroidSignIn(
            GoogleIdTokenProviderKind providerKind,
            bool featureEnabled,
            bool clientConfigured,
            bool serverConfigured,
            bool explicitlySuppressed,
            bool hadSavedLogin,
            bool explicitlyEnabled)
        {
            return providerKind == GoogleIdTokenProviderKind.AndroidCredentialManager &&
                   featureEnabled &&
                   clientConfigured &&
                   serverConfigured &&
                   !explicitlySuppressed &&
                   (!hadSavedLogin || explicitlyEnabled);
        }
    }
}
