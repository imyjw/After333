using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project333.Tests.EditMode
{
    public sealed class AccountCredentialStoreTests
    {
        private const string CredentialKey = "Project333.Tests.AccountCredentialStore";

        private FakeSecureCredentialStore _secureStore;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(CredentialKey);
            PlayerPrefs.Save();
            _secureStore = new FakeSecureCredentialStore();
            AccountCredentialStore.SetStoreForTests(_secureStore);
        }

        [TearDown]
        public void TearDown()
        {
            AccountCredentialStore.ResetStoreForTests();
            PlayerPrefs.DeleteKey(CredentialKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void SetString_WithSecureStore_DoesNotKeepCredentialInPlayerPrefs()
        {
            PlayerPrefs.SetString(CredentialKey, "legacy-copy");

            AccountCredentialStore.SetString(CredentialKey, "secure-token");

            Assert.That(_secureStore.TryGetString(CredentialKey, out var savedValue), Is.True);
            Assert.That(savedValue, Is.EqualTo("secure-token"));
            Assert.That(PlayerPrefs.HasKey(CredentialKey), Is.False);
        }

        [Test]
        public void GetString_WithLegacyPlayerPrefsCredential_MigratesItToSecureStore()
        {
            PlayerPrefs.SetString(CredentialKey, "legacy-token");
            PlayerPrefs.Save();

            var loadedValue = AccountCredentialStore.GetString(CredentialKey);

            Assert.That(loadedValue, Is.EqualTo("legacy-token"));
            Assert.That(_secureStore.TryGetString(CredentialKey, out var migratedValue), Is.True);
            Assert.That(migratedValue, Is.EqualTo("legacy-token"));
            Assert.That(PlayerPrefs.HasKey(CredentialKey), Is.False);
        }

        [Test]
        public void GetString_WhenSecureMigrationFails_KeepsLegacyCredential()
        {
            PlayerPrefs.SetString(CredentialKey, "legacy-token");
            PlayerPrefs.Save();
            _secureStore.FailWrites = true;
            LogAssert.Expect(
                LogType.Error,
                "After333 could not migrate an account credential to Fake secure storage.");

            var loadedValue = AccountCredentialStore.GetString(CredentialKey);

            Assert.That(loadedValue, Is.EqualTo("legacy-token"));
            Assert.That(_secureStore.TryGetString(CredentialKey, out _), Is.False);
            Assert.That(PlayerPrefs.GetString(CredentialKey), Is.EqualTo("legacy-token"));
        }

        [Test]
        public void DeleteKey_RemovesSecureAndLegacyCopies()
        {
            _secureStore.TrySetString(CredentialKey, "secure-token");
            PlayerPrefs.SetString(CredentialKey, "legacy-token");

            AccountCredentialStore.DeleteKey(CredentialKey);

            Assert.That(_secureStore.TryGetString(CredentialKey, out _), Is.False);
            Assert.That(PlayerPrefs.HasKey(CredentialKey), Is.False);
        }

        private sealed class FakeSecureCredentialStore : IAccountCredentialStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public string StorageName => "Fake secure storage";
            public bool IsSecure => true;
            public bool FailWrites { get; set; }

            public bool TryGetString(string key, out string value)
            {
                return _values.TryGetValue(key, out value);
            }

            public bool TrySetString(string key, string value)
            {
                if (FailWrites)
                {
                    return false;
                }

                _values[key] = value;
                return true;
            }

            public bool TryDeleteKey(string key)
            {
                _values.Remove(key);
                return true;
            }

            public void Flush()
            {
            }
        }
    }
}
