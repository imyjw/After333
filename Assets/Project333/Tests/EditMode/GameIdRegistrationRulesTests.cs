using NUnit.Framework;
using Project333.Runtime.Application.Accounts;

namespace Project333.Tests.EditMode
{
    public sealed class GameIdRegistrationRulesTests
    {
        [TestCase("after333", "after333")]
        [TestCase("abc123", "abc123")]
        [TestCase("After.333", "after.333")]
        [TestCase("abcdefghijklmnopqrstuvwxyz1234", "abcdefghijklmnopqrstuvwxyz1234")]
        [TestCase("  Player.01  ", "player.01")]
        public void TryNormalizeGameId_ValidInput_NormalizesToLowercase(
            string input,
            string expected)
        {
            var valid = GameIdRegistrationRules.TryNormalizeGameId(
                input,
                out var normalized,
                out var error);

            Assert.That(valid, Is.True, error);
            Assert.That(normalized, Is.EqualTo(expected));
            Assert.That(error, Is.Empty);
        }

        [TestCase("abc12")]
        [TestCase("abcdefghijklmnopqrstuvwxyz12345")]
        [TestCase("after_333")]
        [TestCase("after-333")]
        [TestCase(".after333")]
        [TestCase("after333.")]
        [TestCase("after..333")]
        public void TryNormalizeGameId_InvalidInput_IsRejected(string input)
        {
            var valid = GameIdRegistrationRules.TryNormalizeGameId(
                input,
                out _,
                out var error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase("After333!")]
        [TestCase("cards 333")]
        [TestCase("abc 123!")]
        public void TryValidatePassword_PrintableAscii_IsAccepted(string password)
        {
            var valid = GameIdRegistrationRules.TryValidatePassword(password, out var error);

            Assert.That(valid, Is.True, error);
            Assert.That(error, Is.Empty);
        }

        [TestCase("short7")]
        [TestCase(" password1")]
        [TestCase("password1 ")]
        [TestCase("비밀번호123!")]
        [TestCase("password123")]
        [TestCase("12345678")]
        public void TryValidatePassword_InvalidOrWeakInput_IsRejected(string password)
        {
            var valid = GameIdRegistrationRules.TryValidatePassword(password, out var error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
