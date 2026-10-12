using System.Reflection;
using System.Text.Json;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.Networking;

internal static class TutorialRewardedAdIsolationChecks
{
    internal static async Task RunAsync()
    {
        const string url = "http://tutorial-ad.synthetic.invalid";
        var oldValues = PlayerPrefs.Values;
        var oldSaves = PlayerPrefs.Saves;
        var oldAccount = AccountSessionState.AccountId;
        var oldApplied = AccountSessionState.Applied;
        var oldWallet = AccountSessionState.Wallet;
        var oldEndpoint = Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url;
        var oldSend = UnityWebRequest.OnSend;
        var oldSends = UnityWebRequest.Sends;
        var worker = new RewardedAdRecovery();
        Invoke(typeof(TutorialRuntimeIsolation), null, "ResetRuntimeState");
        Invoke(typeof(TutorialRuntimeIsolation), null, "Initialize");
        Invoke(typeof(RewardedAdRecovery), worker, "OnEnable");
        try
        {
            PlayerPrefs.Values = new();
            AccountSessionState.AccountId = Guid.NewGuid().ToString("D");
            var owner = AccountSessionState.AccountId;
            AccountSessionState.Applied = 0;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url = url;
            var id = Guid.NewGuid().ToString("D");
            PendingRewardedAd.Remember(url, owner, id);
            var saved = JsonSerializer.Serialize(PlayerPrefs.Values);
            var saves = PlayerPrefs.Saves;
            var sends = UnityWebRequest.Sends;
            var ad = new ProbeAd();
            RewardedAdRecovery.ObserveShow(ad, url, owner, id);
            TutorialRuntimeIsolation.RequestEntry();
            UnityWebRequest.OnSend = _ => throw new Exception("Tutorial ad worker sent HTTP.");
            await worker.RecoverAsync();
            ad.EmitRewarded();
            ad.EmitClosed();
            Require(JsonSerializer.Serialize(PlayerPrefs.Values) == saved && PlayerPrefs.Saves == saves &&
                    UnityWebRequest.Sends == sends && AccountSessionState.Applied == 0,
                "Tutorial ad recovery or late SDK signal changed saved intent/wallet or sent a request.");
            TutorialRuntimeIsolation.CancelEntry();
            var pending = PendingRewardedAd.Load(url, owner);
            Require(pending.AttemptId == id && pending.Rewarded && !pending.CanReplay && AccountSessionState.Applied == 0,
                "Legitimate deferred SDK completion was lost or made replayable on exit.");
            Console.WriteLine("PASS: tutorial suspends ad recovery and defers legitimate SDK markers until exit without wallet mutation");

            var deferredSaved = JsonSerializer.Serialize(PlayerPrefs.Values);
            UnityWebRequest.OnSend = r =>
            {
                Require(r.Verb == "GET" && r.Url.EndsWith("/" + id), "Tutorial recovery created a new ad attempt.");
                r.result = UnityWebRequest.Result.Success;
                r.responseCode = 200;
                r.downloadHandler.text = JsonSerializer.Serialize(new { attemptId = id, status = "granted", wallet = new { tickets = 7 } });
                TutorialRuntimeIsolation.RequestEntry();
                TutorialRuntimeIsolation.CancelEntry();
            };
            await worker.RecoverAsync();
            Require(AccountSessionState.Applied == 0 && JsonSerializer.Serialize(PlayerPrefs.Values) == deferredSaved,
                "An in-flight grant escaped cancellation during rapid tutorial entry/exit.");
            UnityWebRequest.OnSend = r =>
            {
                Require(r.Verb == "GET" && r.Url.EndsWith("/" + id), "Resuming ad recovery changed the saved attempt.");
                r.result = UnityWebRequest.Result.Success;
                r.responseCode = 200;
                r.downloadHandler.text = JsonSerializer.Serialize(new { attemptId = id, status = "granted", wallet = new { tickets = 7 } });
            };
            await worker.RecoverAsync();
            Require(AccountSessionState.Applied == 1 && !PendingRewardedAd.HasPending(url, owner),
                "The same ad worker did not resume after tutorial exit.");
            Console.WriteLine("PASS: an in-flight ad response stays cancelled across rapid entry/exit; the same worker resumes and accepts only the matching server grant");
        }
        finally
        {
            TutorialRuntimeIsolation.CancelEntry();
            Invoke(typeof(RewardedAdRecovery), worker, "OnDisable");
            Invoke(typeof(RewardedAdRecovery), null, "StopObserving");
            Invoke(typeof(RewardedAdRecovery), worker, "OnDestroy");
            PlayerPrefs.Values = oldValues;
            PlayerPrefs.Saves = oldSaves;
            AccountSessionState.AccountId = oldAccount;
            AccountSessionState.Applied = oldApplied;
            AccountSessionState.Wallet = oldWallet;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url = oldEndpoint;
            UnityWebRequest.OnSend = oldSend;
            UnityWebRequest.Sends = oldSends;
        }
    }

    private static void Invoke(Type type, object instance, string method) =>
        type.GetMethod(method, BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance))!.Invoke(instance, null);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class ProbeAd : IRewardedAdService
    {
        public event Action StateChanged { add { } remove { } }
        public event Action Rewarded, Closed;
        public bool IsReady => true;
        public bool IsShowing => false;
        public string StatusMessage => string.Empty;
        public void Initialize(string a, string b, string c) { }
        public bool TryShow(string a, string b, out string error) { error = string.Empty; return true; }
        public void EmitRewarded() => Rewarded?.Invoke();
        public void EmitClosed() => Closed?.Invoke();
    }
}
