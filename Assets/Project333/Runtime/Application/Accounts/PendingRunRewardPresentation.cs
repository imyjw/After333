using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    // This marker resumes a result display; only the server can grant or confirm rewards.
    public static class PendingRunRewardPresentation
    {
        public static void Remember(string serverUrl, string accountId, string runId)
        {
            if (!HasScope(serverUrl, accountId) || !TryNormalizeRunId(runId, out var normalizedRunId))
            {
                throw new ArgumentException("A server, account and valid run identity are required.");
            }

            // A newly opened completed run replaces a stale display marker from an older run.
            PlayerPrefs.SetString(Key(serverUrl, accountId), normalizedRunId);
            PlayerPrefs.Save();
        }

        public static string GetPendingRunId(string serverUrl, string accountId)
        {
            if (!HasScope(serverUrl, accountId))
            {
                return string.Empty;
            }

            var savedRunId = PlayerPrefs.GetString(Key(serverUrl, accountId), string.Empty);
            return TryNormalizeRunId(savedRunId, out var normalizedRunId)
                ? normalizedRunId
                : string.Empty;
        }

        public static void Clear(string serverUrl, string accountId, string runId)
        {
            if (!HasScope(serverUrl, accountId) || !TryNormalizeRunId(runId, out var normalizedRunId) ||
                !string.Equals(GetPendingRunId(serverUrl, accountId), normalizedRunId, StringComparison.Ordinal))
            {
                return;
            }

            PlayerPrefs.DeleteKey(Key(serverUrl, accountId));
            PlayerPrefs.Save();
        }

        private static bool HasScope(string serverUrl, string accountId)
        {
            return !string.IsNullOrWhiteSpace(serverUrl) && !string.IsNullOrWhiteSpace(accountId);
        }

        private static bool TryNormalizeRunId(string runId, out string normalizedRunId)
        {
            normalizedRunId = string.Empty;
            if (!Guid.TryParse(runId, out var parsed) || parsed == Guid.Empty)
            {
                return false;
            }

            normalizedRunId = parsed.ToString("D");
            return true;
        }

        private static string Key(string serverUrl, string accountId)
        {
            using var hash = SHA256.Create();
            var scope = serverUrl.Trim().TrimEnd('/') + "\n" + accountId.Trim().ToLowerInvariant();
            return "Project333.PendingRunRewardPresentation.v1." +
                BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope))).Replace("-", string.Empty);
        }
    }
}
