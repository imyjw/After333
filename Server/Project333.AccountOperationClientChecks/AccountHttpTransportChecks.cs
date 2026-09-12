using System.Diagnostics;
using Project333.Runtime.Application.Accounts;
using UnityEngine.Networking;

internal static class AccountHttpTransportChecks
{
    internal static async Task RunAsync()
    {
        using (var cancelled = new CancellationTokenSource())
        using (var request = new UnityWebRequest("http://synthetic.invalid", "GET"))
        {
            cancelled.Cancel();
            UnityWebRequest.OnSend = _ => throw new Exception("Pre-cancelled request was sent.");
            await Expect<OperationCanceledException>(() => AccountHttpTransport.SendAsync(request, cancelled.Token));
            Require(request.SendCount == 0, "Pre-cancellation must prevent send.");
        }
        Console.WriteLine("PASS: pre-cancelled HTTP request never sends");

        using (var cancelled = new CancellationTokenSource())
        using (var request = new UnityWebRequest("http://synthetic.invalid", "GET"))
        {
            UnityWebRequest.OnSend = r => { r.CompleteOnSend = false; cancelled.Cancel(); };
            await Expect<OperationCanceledException>(() => AccountHttpTransport.SendAsync(request, cancelled.Token));
            Require(request.SendCount == 1 && request.AbortCount == 1, "In-flight cancellation must abort once without retry.");
        }
        Console.WriteLine("PASS: in-flight cancellation aborts without retry");

        foreach (var result in new[] { UnityWebRequest.Result.Success, UnityWebRequest.Result.ProtocolError })
        {
            using var request = new UnityWebRequest("http://synthetic.invalid", "GET");
            UnityWebRequest.OnSend = r => { r.result = result; r.responseCode = result == UnityWebRequest.Result.Success ? 200 : 409; };
            await AccountHttpTransport.SendAsync(request, default);
            Require(request.timeout == 20 && request.result == result && request.AbortCount == 0 && request.SendCount == 1,
                "Completed HTTP responses must retain their result and caller-specific handling.");
        }
        Console.WriteLine("PASS: success/business-error responses retain their original handling");

        // Native completion never occurs: verify the real monotonic 20-second deadline,
        // and preserve both kinds of pending mutations for existing automatic reconciliation.
        const string url = "http://timeout.synthetic.invalid";
        var requests = new List<UnityWebRequest>();
        UnityWebRequest.OnSend = r => { r.CompleteOnSend = false; requests.Add(r); };
        var watch = Stopwatch.StartNew();
        await Task.WhenAll(CheckPending("ticket_purchase", ""), CheckPending("card_upgrade", "firebolt"));
        Require(watch.Elapsed.TotalSeconds >= 19 && watch.Elapsed.TotalSeconds < 35, "Whole-request timeout did not bound the wait.");
        Require(requests.Count == 2 && requests.All(r => r.SendCount == 1 && r.AbortCount == 1 && r.timeout == 20),
            "Timeout must abort each request exactly once without resubmitting a charge.");
        Console.WriteLine("PASS: 20-second nonresponsive purchase/upgrade requests abort and preserve original pending intent");

        async Task CheckPending(string kind, string card)
        {
            string id;
            using (var operation = PendingAccountOperation.Begin(url, kind, card, 3))
            {
                id = operation.RequestId;
                await Expect<TimeoutException>(() => operation.PostAsync<object, object>(url, "/test", "test", new { }, _ => "failure", default));
                Require(PendingAccountOperation.HasPending(url, kind, card), "Timeout erased an uncertain mutation.");
            }
            Require(!PendingAccountOperation.IsInFlight(url, kind, card), "Timeout left in-flight flag stuck.");
            using var restored = PendingAccountOperation.Begin(url, kind, card, 9);
            Require(restored.RequestId == id && restored.ExpectedUpgradeLevel == 3, "Timeout lost the original request identity or level.");
            restored.Complete(id, AccountSessionState.AccountId);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task Expect<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}