using NUnit.Framework;
using Project333.Runtime.Presentation;

namespace Project333.Tests.EditMode
{
    public sealed class Project333ServerEndpointSettingsTests
    {
        [TestCase(null, true, Project333ServerEndpointSettings.LocalHttpUrl)]
        [TestCase("", false, Project333ServerEndpointSettings.PublicHttpUrl)]
        [TestCase(Project333ServerEndpointSettings.LocalHttpUrl, true, Project333ServerEndpointSettings.LocalHttpUrl)]
        [TestCase(Project333ServerEndpointSettings.LocalHttpUrl, false, Project333ServerEndpointSettings.PublicHttpUrl)]
        [TestCase("http://localhost:7333/", false, Project333ServerEndpointSettings.PublicHttpUrl)]
        [TestCase("https://staging.after333.com/", false, "https://staging.after333.com")]
        public void ResolveEnvironmentHttpUrl_UsesExpectedEnvironmentDefault(
            string inspectorUrl,
            bool isEditor,
            string expected)
        {
            var resolved = Project333ServerEndpointSettings.ResolveEnvironmentHttpUrl(inspectorUrl, isEditor);

            Assert.That(resolved, Is.EqualTo(expected));
        }

        [TestCase(null, true, Project333ServerEndpointSettings.LocalBattleWebSocketUrl)]
        [TestCase("", false, Project333ServerEndpointSettings.PublicBattleWebSocketUrl)]
        [TestCase(Project333ServerEndpointSettings.LocalBattleWebSocketUrl, false, Project333ServerEndpointSettings.PublicBattleWebSocketUrl)]
        [TestCase(Project333ServerEndpointSettings.LocalHttpUrl, false, Project333ServerEndpointSettings.PublicBattleWebSocketUrl)]
        [TestCase("ws://192.168.0.10:7333", false, "ws://192.168.0.10:7333/battle")]
        public void ResolveEnvironmentBattleWebSocketUrl_UsesExpectedEnvironmentDefault(
            string inspectorUrl,
            bool isEditor,
            string expected)
        {
            var resolved = Project333ServerEndpointSettings.ResolveEnvironmentBattleWebSocketUrl(inspectorUrl, isEditor);

            Assert.That(resolved, Is.EqualTo(expected));
        }

        [Test]
        public void DeriveBattleWebSocketUrl_PublicHttpsUrl_UsesSecureWebSocket()
        {
            var resolved = Project333ServerEndpointSettings.DeriveBattleWebSocketUrl(
                Project333ServerEndpointSettings.PublicHttpUrl);

            Assert.That(resolved, Is.EqualTo(Project333ServerEndpointSettings.PublicBattleWebSocketUrl));
        }
    }
}
