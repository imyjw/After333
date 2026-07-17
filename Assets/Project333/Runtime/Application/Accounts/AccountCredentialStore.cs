using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    public interface IAccountCredentialStore
    {
        string StorageName { get; }
        bool IsSecure { get; }
        bool TryGetString(string key, out string value);
        bool TrySetString(string key, string value);
        bool TryDeleteKey(string key);
        void Flush();
    }

    public static class AccountCredentialStore
    {
        private static IAccountCredentialStore _store = CreatePlatformStore();

        public static string ActiveStorageName => _store.StorageName;
        public static bool IsUsingSecureStorage => _store.IsSecure;

        public static string GetString(string key)
        {
            ValidateKey(key);
            if (_store.TryGetString(key, out var value))
            {
                return value ?? string.Empty;
            }

            if (!_store.IsSecure || !PlayerPrefs.HasKey(key))
            {
                return string.Empty;
            }

            var legacyValue = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(legacyValue))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                return string.Empty;
            }

            if (_store.TrySetString(key, legacyValue))
            {
                _store.Flush();
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"After333 migrated a saved account credential to {_store.StorageName}.");
            }
            else
            {
                Debug.LogError($"After333 could not migrate an account credential to {_store.StorageName}.");
            }

            return legacyValue;
        }

        public static void SetString(string key, string value)
        {
            ValidateKey(key);
            if (string.IsNullOrEmpty(value))
            {
                DeleteKey(key);
                return;
            }

            if (!_store.TrySetString(key, value))
            {
                Debug.LogError($"After333 could not save an account credential to {_store.StorageName}.");
                return;
            }

            _store.Flush();
            if (_store.IsSecure && PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        public static void DeleteKey(string key)
        {
            ValidateKey(key);
            if (!_store.TryDeleteKey(key))
            {
                Debug.LogWarning($"After333 could not delete an account credential from {_store.StorageName}.");
            }

            _store.Flush();
            if (_store.IsSecure && PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        public static void Flush()
        {
            _store.Flush();
        }

#if UNITY_INCLUDE_TESTS
        public static void SetStoreForTests(IAccountCredentialStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public static void ResetStoreForTests()
        {
            _store = CreatePlatformStore();
        }
#endif

        private static IAccountCredentialStore CreatePlatformStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidKeystoreCredentialStore();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IosKeychainCredentialStore();
#else
            return new PlayerPrefsAccountCredentialStore();
#endif
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Credential key cannot be empty.", nameof(key));
            }
        }
    }

    internal sealed class PlayerPrefsAccountCredentialStore : IAccountCredentialStore
    {
        public string StorageName => "PlayerPrefs development storage";
        public bool IsSecure => false;

        public bool TryGetString(string key, out string value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = string.Empty;
                return false;
            }

            value = PlayerPrefs.GetString(key, string.Empty);
            return true;
        }

        public bool TrySetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            return true;
        }

        public bool TryDeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            return true;
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidKeystoreCredentialStore : IAccountCredentialStore
    {
        private const string PluginClassName = "com.after333.security.After333SecureCredentialStore";
        private AndroidJavaClass _plugin;

        public string StorageName => "Android Keystore";
        public bool IsSecure => true;

        public bool TryGetString(string key, out string value)
        {
            try
            {
                value = Plugin.CallStatic<string>("getString", key);
                return value != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"After333 Android Keystore read failed: {ex.Message}");
                value = string.Empty;
                return false;
            }
        }

        public bool TrySetString(string key, string value)
        {
            try
            {
                return Plugin.CallStatic<bool>("setString", key, value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"After333 Android Keystore write failed: {ex.Message}");
                return false;
            }
        }

        public bool TryDeleteKey(string key)
        {
            try
            {
                return Plugin.CallStatic<bool>("deleteKey", key);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 Android Keystore delete failed: {ex.Message}");
                return false;
            }
        }

        public void Flush()
        {
        }

        private AndroidJavaClass Plugin =>
            _plugin ?? (_plugin = new AndroidJavaClass(PluginClassName));
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    internal sealed class IosKeychainCredentialStore : IAccountCredentialStore
    {
        public string StorageName => "iOS Keychain";
        public bool IsSecure => true;

        public bool TryGetString(string key, out string value)
        {
            var pointer = After333SecureCredentialGet(key);
            if (pointer == IntPtr.Zero)
            {
                value = string.Empty;
                return false;
            }

            try
            {
                value = Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
                return true;
            }
            finally
            {
                After333SecureCredentialFree(pointer);
            }
        }

        public bool TrySetString(string key, string value)
        {
            return After333SecureCredentialSet(key, value) != 0;
        }

        public bool TryDeleteKey(string key)
        {
            return After333SecureCredentialDelete(key) != 0;
        }

        public void Flush()
        {
        }

        [DllImport("__Internal")]
        private static extern IntPtr After333SecureCredentialGet(string key);

        [DllImport("__Internal")]
        private static extern int After333SecureCredentialSet(string key, string value);

        [DllImport("__Internal")]
        private static extern int After333SecureCredentialDelete(string key);

        [DllImport("__Internal")]
        private static extern void After333SecureCredentialFree(IntPtr pointer);
    }
#endif
}
