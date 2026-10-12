using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    // Only the identity of an unread result. Cards are already granted by the server.
    public static class PendingCardPackPresentation
    {
        public static void Remember(string serverUrl, string accountId, string requestId)
        {
            if (!HasScope(serverUrl, accountId) || !TryNormalize(requestId, out var normalized))
                throw new ArgumentException("A server, account and purchase request identity are required.");
            PlayerPrefs.SetString(Key(serverUrl, accountId), normalized);
            PlayerPrefs.Save();
        }

        public static string GetRequestId(string serverUrl, string accountId)
        {
            if (!HasScope(serverUrl, accountId)) return string.Empty;
            return TryNormalize(PlayerPrefs.GetString(Key(serverUrl, accountId), string.Empty), out var id) ? id : string.Empty;
        }

        public static void Clear(string serverUrl, string accountId, string requestId)
        {
            if (!HasScope(serverUrl, accountId) || !TryNormalize(requestId, out var id) ||
                GetRequestId(serverUrl, accountId) != id) return;
            PlayerPrefs.DeleteKey(Key(serverUrl, accountId));
            PlayerPrefs.Save();
        }

        private static bool HasScope(string serverUrl, string accountId) =>
            !string.IsNullOrWhiteSpace(serverUrl) && !string.IsNullOrWhiteSpace(accountId);

        private static bool TryNormalize(string requestId, out string normalized)
        {
            normalized = string.Empty;
            if (!Guid.TryParse(requestId, out var id) || id == Guid.Empty) return false;
            normalized = id.ToString("D");
            return true;
        }

        private static string Key(string serverUrl, string accountId)
        {
            using var hash = SHA256.Create();
            var scope = serverUrl.Trim().TrimEnd('/') + "\n" + accountId.Trim().ToLowerInvariant();
            return "Project333.PendingCardPackPresentation.v1." +
                BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope))).Replace("-", string.Empty);
        }
    }
}
