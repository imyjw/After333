using NUnit.Framework;
using Project333.Runtime.Presentation.Online;

namespace Project333.Tests.EditMode
{
    public sealed class OnlineBattleConnectionTesterTests
    {
        [TestCase(1, 0.5f)]
        [TestCase(2, 1f)]
        [TestCase(3, 2f)]
        [TestCase(4, 4f)]
        [TestCase(5, 5f)]
        [TestCase(10, 5f)]
        public void CalculateAutomaticReconnectDelaySeconds_UsesCappedExponentialBackoff(
            int completedAttemptCount,
            float expectedSeconds)
        {
            var result = OnlineBattleConnectionTester.CalculateAutomaticReconnectDelaySeconds(
                completedAttemptCount,
                initialDelaySeconds: 0.5f,
                maximumDelaySeconds: 5f);

            Assert.That(result, Is.EqualTo(expectedSeconds));
        }
    }
}
