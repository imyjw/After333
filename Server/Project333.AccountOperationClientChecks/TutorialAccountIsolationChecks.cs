using System.Reflection;
using System.Text.Json;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

internal static class TutorialAccountIsolationChecks
{
    internal static async Task RunAsync()
    {
        const string url = "http://tutorial.synthetic.invalid";
        var oldValues = PlayerPrefs.Values;
        var oldSaves = PlayerPrefs.Saves;
        var oldAccount = AccountSessionState.AccountId;
        var oldApplied = AccountSessionState.Applied;
        var oldMe = AccountSessionState.LastApplied;
        var oldEndpoint = Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url;
        var oldGetMe = GuestAuthClient.OnGetMe;
        var oldGetMeContext = GuestAuthClient.OnGetMeWithContext;
        var oldSend = UnityWebRequest.OnSend;
        var worker = new AccountOperationRecovery();
        Invoke(typeof(TutorialRuntimeIsolation), null, "ResetRuntimeState");
        Invoke(typeof(TutorialRuntimeIsolation), null, "Initialize");
        Invoke(typeof(AccountOperationRecovery), worker, "OnEnable");
        try
        {
            PlayerPrefs.Values = new();
            AccountSessionState.AccountId = Guid.NewGuid().ToString("D");
            var owner = AccountSessionState.AccountId;
            AccountSessionState.Applied = 0;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url = url;
            GuestAuthClient.OnGetMeWithContext = null;
            string id;
            using (var pending = PendingAccountOperation.Begin(url, "ticket_purchase")) id = pending.RequestId;
            var saved = JsonSerializer.Serialize(PlayerPrefs.Values);
            var saves = PlayerPrefs.Saves;
            TutorialRuntimeIsolation.RequestEntry();
            UnityWebRequest.OnSend = _ => throw new Exception("A tutorial worker sent HTTP.");
            await worker.RecoverAsync();
            using (var request = new UnityWebRequest(url, "GET"))
            {
                try { await AccountHttpTransport.SendAsync(request, default); throw new Exception("Tutorial HTTP escaped the guard."); }
                catch (OperationCanceledException) { }
                Require(request.SendCount == 0, "A tutorial HTTP request was sent.");
            }
            Require(JsonSerializer.Serialize(PlayerPrefs.Values) == saved && PlayerPrefs.Saves == saves && AccountSessionState.Applied == 0,
                "Tutorial entry changed saved account work or applied an account refresh.");
            TutorialRuntimeIsolation.CancelEntry();
            Console.WriteLine("PASS: tutorial entry blocks account recovery/HTTP before send without saved metadata or account mutation");

            SceneManager.LoadSceneName(TutorialRuntimeIsolation.SceneName);
            Require(TutorialRuntimeIsolation.IsActive, "Direct tutorial load was not recognized.");
            TutorialRuntimeIsolation.CancelEntry();
            Require(TutorialRuntimeIsolation.IsActive, "Clearing failed-entry intent escaped an active tutorial scene.");
            SceneManager.LoadSceneName("GameStart_VSlice");
            Require(!TutorialRuntimeIsolation.IsActive, "Leaving the tutorial did not resume account access.");
            Console.WriteLine("PASS: direct tutorial scene loading and return automatically preserve the isolation boundary");

            using (var pending = PendingAccountOperation.Begin(url, "ticket_purchase"))
            {
                UnityWebRequest.OnSend = r =>
                {
                    r.result = UnityWebRequest.Result.ProtocolError;
                    r.responseCode = 409;
                    r.downloadHandler.text = "{\"error\":{\"code\":\"operation_cancelled\"}}";
                    TutorialRuntimeIsolation.RequestEntry();
                };
                try { await pending.PostAsync<object, object>(url, "/test", "test", new { }, _ => "rejected", default); }
                catch (OperationCanceledException) { }
                Require(PendingAccountOperation.HasPending(url, "ticket_purchase"), "A response arriving during tutorial cleared pending work.");
            }
            TutorialRuntimeIsolation.CancelEntry();
            Console.WriteLine("PASS: a definitive rejection arriving at tutorial entry still retains the original pending intent");

            var refresh = new TaskCompletionSource<MeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityWebRequest.OnSend = r =>
            {
                Require(r.Url.EndsWith("/account/operations/resolve"), "Recovery retried a charge.");
                r.result = UnityWebRequest.Result.Success;
                r.responseCode = 200;
                r.downloadHandler.text = JsonSerializer.Serialize(new { requestId = id, accountId = owner, status = "completed" });
            };
            GuestAuthClient.OnGetMe = () => refresh.Task;
            var recovering = worker.RecoverAsync();
            Require(!recovering.IsCompleted, "Delayed /me boundary was not reached.");
            TutorialRuntimeIsolation.RequestEntry();
            TutorialRuntimeIsolation.CancelEntry();
            refresh.SetResult(new MeResponse { ResolvedAccount = new AccountDto { ResolvedId = owner } });
            await recovering;
            Require(AccountSessionState.Applied == 0 && PendingAccountOperation.HasPending(url, "ticket_purchase") &&
                    JsonSerializer.Serialize(PlayerPrefs.Values) == saved,
                "A delayed pre-tutorial refresh applied or cleared work after rapid entry/exit.");
            GuestAuthClient.OnGetMe = () => Task.FromResult(new MeResponse { ResolvedAccount = new AccountDto { ResolvedId = owner } });
            await worker.RecoverAsync();
            Require(AccountSessionState.Applied == 1 && !PendingAccountOperation.HasPending(url, "ticket_purchase"),
                "The same worker failed to resume the same request after leaving tutorial.");
            Console.WriteLine("PASS: delayed /me completion stays cancelled across rapid entry/exit; a fresh attempt resumes without lifetime cancellation or another charge");
        }
        finally
        {
            TutorialRuntimeIsolation.CancelEntry();
            Invoke(typeof(AccountOperationRecovery), worker, "OnDisable");
            Invoke(typeof(AccountOperationRecovery), worker, "OnDestroy");
            PlayerPrefs.Values = oldValues;
            PlayerPrefs.Saves = oldSaves;
            AccountSessionState.AccountId = oldAccount;
            AccountSessionState.Applied = oldApplied;
            AccountSessionState.LastApplied = oldMe;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.Url = oldEndpoint;
            GuestAuthClient.OnGetMe = oldGetMe;
            GuestAuthClient.OnGetMeWithContext = oldGetMeContext;
            UnityWebRequest.OnSend = oldSend;
        }
    }

    private static void Invoke(Type type, object instance, string method) =>
        type.GetMethod(method, BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance))!.Invoke(instance, null);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
