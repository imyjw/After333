using NUnit.Framework;
using Project333.Runtime.Application.Accounts;

namespace Project333.Tests.EditMode
{
    public sealed class GoogleIdTokenProviderFactoryTests
    {
        [TestCase(true, false, GoogleIdTokenProviderKind.WindowsDesktop)]
        [TestCase(false, true, GoogleIdTokenProviderKind.AndroidCredentialManager)]
        [TestCase(false, false, GoogleIdTokenProviderKind.Unsupported)]
        [TestCase(true, true, GoogleIdTokenProviderKind.AndroidCredentialManager)]
        public void SelectProviderKind_UsesSupportedPlatformProvider(
            bool supportsWindowsDesktop,
            bool supportsAndroidCredentialManager,
            GoogleIdTokenProviderKind expected)
        {
            var result = GoogleIdTokenProviderFactory.SelectProviderKind(
                supportsWindowsDesktop,
                supportsAndroidCredentialManager);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ResolveClientId_UsesDesktopIdForWindows()
        {
            var result = GoogleIdTokenProviderFactory.ResolveClientId(
                GoogleIdTokenProviderKind.WindowsDesktop,
                " desktop-client ",
                "android-server-client");

            Assert.That(result, Is.EqualTo("desktop-client"));
        }

        [Test]
        public void ResolveClientId_UsesServerIdForAndroid()
        {
            var result = GoogleIdTokenProviderFactory.ResolveClientId(
                GoogleIdTokenProviderKind.AndroidCredentialManager,
                "desktop-client",
                " android-server-client ");

            Assert.That(result, Is.EqualTo("android-server-client"));
        }

        [Test]
        public void AndroidConfigurationField_ExplainsServerClientId()
        {
            Assert.That(
                GoogleIdTokenProviderFactory.GetConfigurationFieldName(
                    GoogleIdTokenProviderKind.AndroidCredentialManager),
                Is.EqualTo("Google Android Server Client Id"));
        }

        [TestCase(GoogleIdTokenProviderKind.WindowsDesktop, true, false, false)]
        [TestCase(GoogleIdTokenProviderKind.WindowsDesktop, true, true, true)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, false, true)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, false, true, false)]
        public void IsServerConfiguredForProvider_RequiresDesktopExchangeOnlyOnWindows(
            GoogleIdTokenProviderKind providerKind,
            bool googleTokenValidationConfigured,
            bool desktopCodeExchangeConfigured,
            bool expected)
        {
            var result = GoogleIdTokenProviderFactory.IsServerConfiguredForProvider(
                providerKind,
                googleTokenValidationConfigured,
                desktopCodeExchangeConfigured);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, true, true, false, false, false, true)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, true, true, false, true, true, true)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, true, true, false, true, false, false)]
        [TestCase(GoogleIdTokenProviderKind.WindowsDesktop, true, true, true, false, false, false, false)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, false, true, true, false, false, false, false)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, false, true, false, false, false, false)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, true, false, false, false, false, false)]
        [TestCase(GoogleIdTokenProviderKind.AndroidCredentialManager, true, true, true, true, false, true, false)]
        public void ShouldAttemptAutomaticAndroidSignIn_RequiresSafeStartupConditions(
            GoogleIdTokenProviderKind providerKind,
            bool featureEnabled,
            bool clientConfigured,
            bool serverConfigured,
            bool explicitlySuppressed,
            bool hadSavedLogin,
            bool explicitlyEnabled,
            bool expected)
        {
            var result = GoogleIdTokenProviderFactory.ShouldAttemptAutomaticAndroidSignIn(
                providerKind,
                featureEnabled,
                clientConfigured,
                serverConfigured,
                explicitlySuppressed,
                hadSavedLogin,
                explicitlyEnabled);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
