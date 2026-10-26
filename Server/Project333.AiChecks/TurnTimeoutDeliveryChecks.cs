using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Observability;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Envelope = Project333.PvpServer.Messages.OnlineBattleEnvelope;
using Command = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using CommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;
using Seat = Project333.PvpServer.Messages.PlayerIdDto;

// Calls the actual production timeout handler; controlled sockets/audit lock replace external I/O.
// No HTTP listener, real network, database, accounts or AI search is started.
internal static class TurnTimeoutDeliveryChecks
{
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Handler = typeof(BattleSession).Assembly.GetType("Program")!
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method => method.Name.Contains("g__ResolveTurnTimeoutAsync|", StringComparison.Ordinal));

    public static async Task RunAsync()
    {
        var failures = new List<string>();
        var passed = 0;
        for (var first = 0; first < 2; first++)
            foreach (var mode in new[] { "audit", "blocked-events", "blocked-state", "broken-peer", "normal", "mulligan", "ai-audit", "terminal" })
            {
                var label = $"{mode}/first-{first}";
                try { await CheckAsync(first, mode); passed++; Console.WriteLine("PASS timeout delivery " + label); }
                catch (Exception error) { failures.Add(label + ": " + error.Message); Console.WriteLine("FAIL timeout delivery " + failures[^1]); }
            }
        Console.WriteLine($"Timeout delivery checks: {passed}/16 passed. Actual handler; no DB/listener/network/account.");
        if (failures.Count > 0) Environment.ExitCode = 1; // Report failure without opening a native crash-report dialog.
    }

    static async Task CheckAsync(int first, string mode)
    {
        using var slow = new SocketProbe { BlockSend = mode == "blocked-events" ? 1 : mode == "blocked-state" ? 2 : 0, FailSend = mode == "broken-peer" };
        using var healthy = new SocketProbe();
        var useAi = mode == "ai-audit";
        var session = new BattleSession("offline-timeout-" + Guid.NewGuid().ToString("N"), useAi,
            new RandomFirstPlayerSelector(new FixedRandom(first)));
        var a = new BattleClientConnection("offline-a", useAi ? healthy : slow) { PlayerToken = "a" };
        var b = new BattleClientConnection("offline-b", healthy) { PlayerToken = "b" };
        a.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        b.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        Require(session.TryAddConnection(a, out _) && (useAi || session.TryAddConnection(b, out _)) && session.TryStartBattleIfReady(out _), "Fixture must start.");
        if (mode != "mulligan")
        {
            session.ApplyCommand(new Command { ActorId = Seat.Player, CommandType = CommandType.PassMulligan });
            if (!useAi) session.ApplyCommand(new Command { ActorId = Seat.AI, CommandType = CommandType.PassMulligan });
        }
        if (mode == "terminal")
        {
            var flow = (BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(session)!;
            var next = flow.CurrentBattleState!.GetOpponent(flow.CurrentBattleState.ActivePlayerId);
            next.Master.CurrentHp = 1;
            while (next.Deck.TryDraw(out _)) { }
        }
        var auditDirectory = Path.Combine(Path.GetTempPath(), "After333-TimeoutCheck-" + Guid.NewGuid().ToString("N"));
        var audit = new AuditLogService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PROJECT333_AUDIT_LOG_ENABLED"] = (mode == "audit" || useAi || mode == "terminal") ? "true" : "false",
            ["PROJECT333_AUDIT_LOG_DIR"] = auditDirectory,
            ["PROJECT333_AUDIT_LOG_RETENTION_DAYS"] = "0"
        }).Build());
        var auditLock = (SemaphoreSlim)typeof(AuditLogService).GetField("_writeLock", Hidden)!.GetValue(audit)!;
        var auditHeld = mode == "audit" || useAi || mode == "terminal";
        if (auditHeld) await auditLock.WaitAsync();
        typeof(BattleSession).GetField("_turnTimerDeadlineUtc", Hidden)!.SetValue(session, DateTimeOffset.UtcNow.AddSeconds(-1));
        Require(session.TryGetCurrentTurnTimer(out var actor, out var version, out var deadline), "Timer missing.");
        Task? running = null;
        try
        {
            running = Invoke(session, audit, actor, version, deadline);
            // Everything before the controlled first async wait runs synchronously. No real ten-second sleep.
            if (mode == "terminal")
            {
                Require(healthy.Messages.Count == 1 && healthy.Messages[0].BattleEvents.Any(e => e.EventType == BattleEventType.BattleEnded),
                    "Terminal event should arrive, but final state must await durable result processing.");
                Require(!running.IsCompleted, "Terminal recording barrier was skipped.");
            }
            else
            {
                Require(healthy.Messages.Count == 2, "Healthy client must receive event AND next-turn state before blocked logging/peer delivery.");
                Require(healthy.Messages[0].MessageType == OnlineBattleMessageType.BattleEvents &&
                        healthy.Messages[1].MessageType == OnlineBattleMessageType.StateView, "Per-client event/state order changed.");
                var view = healthy.Messages[1].StateView!;
                Require(view.Phase == PhaseType.Main && view.TurnNumber == (mode == "mulligan" ? 1 : 2), "Did not publish the resolved turn.");
                if (mode != "mulligan")
                    Require(view.ActivePlayerId == (first == 0 ? PlayerId.AI : PlayerId.Player), "Active seat did not change.");
                Require(view.ViewerOnlineSeatId == (useAi ? OnlineBattleSeatId.PlayerA : OnlineBattleSeatId.PlayerB) && view.Player.VisibleHandCards.Count > 0 && view.Opponent.VisibleHandCards.Count == 0, "Viewer privacy projection changed.");
                Require(view.TurnTimerDeadlineUnixTimeMilliseconds > view.ServerUnixTimeMilliseconds && view.TurnTimerDurationSeconds == 73, "Existing timer duration/deadline changed.");
                if (mode != "mulligan")
                    Require(!session.TryCreateTurnTimeoutEnvelope(actor, version, deadline, out _), "Old callback ended the next turn twice.");
                if (auditHeld || slow.BlockSend > 0) Require(!running.IsCompleted, "Controlled blocked operation must still be awaited.");
            }
        }
        finally
        {
            if (auditHeld) auditLock.Release();
            slow.Release.TrySetResult();
            if (running != null) await running.WaitAsync(TimeSpan.FromSeconds(15));
            session.CancelForServerShutdown();
            if (Directory.Exists(auditDirectory))
            {
                foreach (var file in Directory.GetFiles(auditDirectory, "audit_*.jsonl", SearchOption.TopDirectoryOnly)) File.Delete(file);
                Directory.Delete(auditDirectory); // Only this new empty fixture directory; never recursive.
            }
        }
        if (mode == "terminal")
            Require(healthy.Messages.Count == 2 && healthy.Messages[1].StateView?.IsEnded == true,
                "Final ended state must be sent after recording completes.");
    }

    static Task Invoke(BattleSession session, AuditLogService audit, Seat actor, long version, DateTimeOffset deadline) =>
        (Task)Handler.Invoke(null, new object?[] { session, new BattleSessionManager(), null, null, null, audit, actor, version, deadline, Json })!;
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    sealed class FixedRandom(int first) : Random { public override int Next(int maxValue) => first; }

    sealed class SocketProbe : WebSocket
    {
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<Envelope> Messages = new();
        public int BlockSend;
        public bool FailSend;
        int sends;
        WebSocketState state = WebSocketState.Open;
        public override WebSocketState State => state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() => state = WebSocketState.Aborted;
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? reason, CancellationToken token) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? reason, CancellationToken token) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> bytes, CancellationToken token) => throw new NotSupportedException();
        public override async Task SendAsync(ArraySegment<byte> bytes, WebSocketMessageType type, bool endOfMessage, CancellationToken token)
        {
            if (++sends == BlockSend) await Release.Task.WaitAsync(token);
            if (FailSend) throw new WebSocketException("Controlled offline send failure.");
            Messages.Add(JsonSerializer.Deserialize<Envelope>(bytes.AsSpan(), Json)!);
        }
    }
}
