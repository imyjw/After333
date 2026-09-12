using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    // Local metadata is only for resuming verification; it never authorizes a reward.
    internal static class PendingRewardedAd
    {
        [Serializable] internal sealed class Record
        {
            public string AttemptId;
            public bool Rewarded;
            public bool CanReplay;
        }

        internal static bool HasPending(string url, string owner) =>
            !string.IsNullOrWhiteSpace(owner) && PlayerPrefs.HasKey(Key(url, owner));

        internal static Record Load(string url, string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) return null;
            var key = Key(url, owner);
            if (!PlayerPrefs.HasKey(key)) return null;
            var record = JsonUtility.FromJson<Record>(PlayerPrefs.GetString(key));
            if (record == null || !Guid.TryParse(record.AttemptId, out var id) || id == Guid.Empty)
                throw new InvalidOperationException("Invalid saved ad attempt.");
            return record;
        }

        internal static bool IsBlocking(string url, string owner)
        {
            try { var record = Load(url, owner); return record != null && (record.Rewarded || !record.CanReplay); }
            catch { return true; }
        }

        internal static void Remember(string url, string owner, string attemptId)
        {
            if (string.IsNullOrWhiteSpace(owner) || !Guid.TryParse(attemptId, out var id) || id == Guid.Empty)
                throw new InvalidOperationException("Invalid ad attempt identity.");
            var existing = Load(url, owner);
            if (existing != null && (existing.AttemptId != id.ToString("D") || existing.Rewarded || !existing.CanReplay))
                throw new InvalidOperationException("Previous ad verification is unresolved.");
            Save(url, owner, new Record { AttemptId = id.ToString("D") });
        }

        internal static void MarkRewarded(string url, string owner, string id)
        {
            var record = Load(url, owner);
            if (record?.AttemptId != id) return;
            record.Rewarded = true;
            record.CanReplay = false;
            Save(url, owner, record);
        }

        internal static void MarkClosed(string url, string owner, string id)
        {
            var record = Load(url, owner);
            if (record?.AttemptId != id) return;
            // A close without a completion signal may be retried only after a server check.
            record.CanReplay = !record.Rewarded;
            Save(url, owner, record);
        }

        internal static void Clear(string url, string owner, string id)
        {
            if (Load(url, owner)?.AttemptId != id) return;
            PlayerPrefs.DeleteKey(Key(url, owner));
            PlayerPrefs.Save();
        }

        private static void Save(string url, string owner, Record record)
        {
            PlayerPrefs.SetString(Key(url, owner), JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        private static string Key(string url, string owner)
        {
            using var hash = SHA256.Create();
            var scope = url.TrimEnd('/') + "\n" + owner.ToLowerInvariant();
            return "Project333.PendingRewardedAd." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope))).Replace("-", "");
        }
    }
}