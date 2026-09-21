using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Application.Online;
using ClientBattleCommandMessage = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using OnlineBattleCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;
using TileCoordDto = Project333.PvpServer.Messages.TileCoordDto;

static class PveReconnectChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    public static async Task Run(Action<string> pass, CancellationToken ct)
    {
        foreach (var grace in new[] { TimeSpan.FromSeconds(60), TimeSpan.Zero })
        {
            var manager = new BattleSessionManager();
            var session = manager.GetOrCreate("pve-grace-" + Guid.NewGuid().ToString("N"), true);
            var original = Connection();
            Require(session.TryAddConnection(original, out var error) && session.TryStartBattleIfReady(out error), error);
            var before = JsonSerializer.Serialize(session.CreatePersistenceSnapshot("before").DomainState);
            Require(session.TryGetCurrentTurnTimer(out var actor, out var version, out var turnDeadline), "Started battle must have a timer.");
            var started = DateTimeOffset.UtcNow;
            var detached = session.DetachConnection(original, grace)!;
            Require(detached.Reserved && detached.DeadlineUtc >= started.AddSeconds(60) &&
                    detached.DeadlineUtc <= DateTimeOffset.UtcNow.AddSeconds(60), "Explicit/default reconnect grace must last 60 seconds.");
            Require(manager.FindReconnectStatus(original.AccountId) == null, "PvP-only discovery must exclude PvE.");
            var status = manager.FindReconnectStatus(original.AccountId, useServerAiOpponent: true);
            Require(status?.MatchId == session.MatchId && status.RemainingSeconds is >= 59 and <= 60, "PvE discovery must recover the account's pending battle.");
            Require(manager.FindReconnectStatus(Guid.NewGuid().ToString(), useServerAiOpponent: true) == null, "Another account must not discover the seat.");
            Require(!session.TryCreateExpiredDisconnectForfeitEnvelope(detached.SeatId, detached.IdentityKey, detached.DeadlineUtc, out _), "An early timeout callback cannot consume the 60-second grace.");
            Require(session.DetachConnection(original, TimeSpan.FromMinutes(3)) == null, "Duplicate cleanup must not extend grace.");
            Require(!session.TryAddConnection(Connection(), out _), "Another account cannot claim a disconnected PvE seat.");
            manager.RemoveIfEmpty(session);
            Require(manager.TryGet(session.MatchId, out var retained) && ReferenceEquals(retained, session), "Empty PvE room must survive until grace expires.");
            var replacement = Connection(original.AccountId);
            replacement.PlayerDeckCardIds.Clear(); replacement.PlayerDeckCardIds.Add("forged-reconnect-card");
            replacement.CardUpgradeLevels["Goblin"] = 999;
            Require(session.TryAddConnection(replacement, out error), error);
            Require(replacement.ReconnectedToPendingSeat && replacement.AssignedSeatId == original.AssignedSeatId &&
                replacement.RunId == original.RunId && replacement.DeckId == original.DeckId &&
                replacement.PlayerDeckCardIds.SequenceEqual(original.PlayerDeckCardIds) &&
                replacement.CardUpgradeLevels["Goblin"] == original.CardUpgradeLevels["Goblin"], "New connection must restore original seat/run/deck/upgrades, ignoring supplied replacement metadata.");
            Require(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("after").DomainState) == before, "Reconnect must preserve hand/board/resources/mulligan state.");
            Require(session.TryGetCurrentTurnTimer(out var nextActor, out var nextVersion, out var nextDeadline) &&
                actor == nextActor && version == nextVersion && turnDeadline == nextDeadline, "Reconnect must preserve the existing PvP-compatible running timer policy.");
            Require(!session.HasPendingReconnectReservations &&
                !session.TryCreateExpiredDisconnectForfeitEnvelope(detached.SeatId, detached.IdentityKey, detached.DeadlineUtc, out _), "A canceled reservation's callback cannot forfeit the restored battle.");
        }
        pass("PvE explicit/default 60-second grace supports account discovery, protects the original participant and rejects premature/stale forfeits");

        var expired = new BattleSession("pve-expired-" + Guid.NewGuid().ToString("N"), true);
        var owner = Connection();
        Require(expired.TryAddConnection(owner, out _) && expired.TryStartBattleIfReady(out _), "Expiry fixture must start.");
        var reservation = expired.DetachConnection(owner, TimeSpan.FromMilliseconds(25))!;
        await WaitUntil(reservation.DeadlineUtc, ct);
        Require(!expired.TryGetReconnectStatus(owner.AccountId, out _) && !expired.TryAddConnection(Connection(owner.AccountId), out _), "At/after deadline an expired seat cannot reconnect, even before its callback runs.");
        Require(expired.TryCreateExpiredDisconnectForfeitEnvelope(reservation.SeatId, reservation.IdentityKey, reservation.DeadlineUtc, out _), "An expired reservation must produce server forfeiture.");
        var result = expired.GetFinalResult()!;
        Require(result.UseServerAiOpponent && result.EndedReason == "reconnect_timeout" && !result.IsDraw &&
            result.Runs.Count == 1 && !result.Runs[0].Won && result.Runs[0].AccountId == owner.AccountId &&
            result.Runs[0].RunId == owner.RunId && result.Runs[0].DeckId == owner.DeckId, "Timeout must produce one loss for the original PvE participant.");
        for (var i = 0; i < 8; i++)
            Require(!expired.TryCreateExpiredDisconnectForfeitEnvelope(reservation.SeatId, reservation.IdentityKey, reservation.DeadlineUtc, out _) &&
                expired.GetFinalResult()!.ToJson() == result.ToJson(), "Repeated callbacks must preserve one result ID/outcome.");
        pass("PvE deadline boundary rejects late rejoin and resolves exactly one server-owned loss for the original run");

        var canceled = new BattleSession("pve-cancel-" + Guid.NewGuid().ToString("N"), true);
        owner = Connection();
        Require(canceled.TryAddConnection(owner, out _) && canceled.TryStartBattleIfReady(out _), "Cancellation fixture must start.");
        reservation = canceled.DetachConnection(owner, TimeSpan.FromMilliseconds(1))!;
        await WaitUntil(reservation.DeadlineUtc, ct);
        canceled.CancelForServerShutdown();
        Require(!canceled.TryCreateExpiredDisconnectForfeitEnvelope(reservation.SeatId, reservation.IdentityKey, reservation.DeadlineUtc, out _) &&
            canceled.GetFinalResult() == null && !canceled.IsBattleEnded, "Server shutdown must cancel even a due timeout without inventing a loss or draw.");
        Require(!canceled.TryGetReconnectStatus(owner.AccountId, out _) && !canceled.TryAddConnection(Connection(owner.AccountId), out _), "Canceled battle cannot be rediscovered or resumed.");
        pass("PvE server-shutdown cancellation suppresses an already-due disconnect loss and offers no old battle for reconnect");
    }

    public static async Task CheckNetwork(HttpClient http, NpgsqlConnection db, IConfiguration configuration,
        TestAccount owner, TestAccount outsider, Action<string> pass, CancellationToken ct)
    {
        var admission = await StartChallenge(http, owner, ct);
        var match = admission.MatchId;
        var challengeAttemptId = admission.AttemptId;
        var sockets = new List<ClientWebSocket>();
        try
        {
            var contenders = await Task.WhenAll(Open(ct), Open(ct));
            sockets.AddRange(contenders);
            var competingMatches = new[] { match, match };
            await Task.WhenAll(Enumerable.Range(0, contenders.Length).Select(i =>
                Send(contenders[i], Join(owner, competingMatches[i], false, challengeAttemptId), ct)));
            var joinResults = await Task.WhenAll(contenders.Select(socket =>
                ReadUntil(socket, m => m.HasAssignedSeat || m.Error != null, ct, allowError: true)));
            Require(joinResults.Count(m => m.HasAssignedSeat) == 1 &&
                joinResults.Count(m => m.Error?.Code == "already_in_pve_match") == 1,
                "Two concurrent challenge sockets for one admission must admit exactly one connection.");
            var acceptedIndex = Array.FindIndex(joinResults, m => m.HasAssignedSeat);
            var active = contenders[acceptedIndex];
            var assigned = joinResults[acceptedIndex];
            match = competingMatches[acceptedIndex];
            Require(assigned.MatchId == match, "The surviving socket must retain its requested match identity.");
            contenders[1 - acceptedIndex].Abort();
            await ReadUntil(active, m => m.StateView != null, ct);
            pass("two concurrent AI challenge WebSocket joins for one admission create one playable battle");
            await SendCommand(active, owner, assigned, OnlineBattleCommandType.PassMulligan, 1, ct);
            var main = await ReadUntil(active, m => m.StateView?.Phase.ToString() == "Main" && m.StateView.ActivePlayerId.ToString() == "Player", ct);
            var master = main.StateView!.Occupants.Single(o => o.OwnerId.ToString() == "Player" && o.Kind.ToString() == "Master");
            await Send(active, new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.ClientCommand, ClientCommand = new ClientBattleCommandMessage {
                MatchId = match, AccountId = owner.AccountId.ToString(), PlayerToken = owner.AccountId.ToString(), ActorId = assigned.AssignedSeatId,
                Sequence = 2, CommandType = OnlineBattleCommandType.MoveOccupant,
                SourceCoord = new TileCoordDto { Column = master.Coord.Column, Row = master.Coord.Row },
                DestinationCoord = new TileCoordDto { Column = 1, Row = 1 }
            } }, ct);
            var moved = await ReadUntil(active, m => m.StateView?.Occupants.Any(o => o.RuntimeId == master.RuntimeId && o.Coord.Column == 1 && o.Coord.Row == 1) == true, ct);
            var expectedState = BattleState(moved.StateView!);
            var before = await Scores(db, owner, ct);
            var outsiderBefore = await Scores(db, outsider, ct);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                active.Abort();
                var discovery = await WaitForDiscovery(http, owner, match, ct);
                Require(Field(discovery, "BattleMode").GetString() == "pve" &&
                    Field(discovery, "GameMode").GetString() == "ai_challenge" &&
                    Field(discovery, "AiChallengeAttemptId").GetString() == challengeAttemptId &&
                    Field(discovery, "RemainingSeconds").GetInt32() is >= 50 and <= 60, "Fresh app must discover a PvE battle with its remaining 60-second grace.");
                Require(!Field(await Status(http, owner, "/pvp/reconnect-status", ct), "HasReconnectableBattle").GetBoolean(), "Legacy PvP discovery must not offer a PvE battle.");
                Require(!Field(await Status(http, outsider, "/battle/reconnect-status", ct), "HasReconnectableBattle").GetBoolean(), "Authenticated discovery must not reveal another account's PvE battle.");
                if (attempt == 0)
                {
                    using var intruder = await Open(ct);
                    await Send(intruder, Join(outsider, match, true, challengeAttemptId), ct);
                    await ReadUntil(intruder, m => m.Error != null, ct, allowError: true);
                }
                active = await Open(ct); sockets.Add(active);
                var join = Join(owner, Field(discovery, "MatchId").GetString()!, true, challengeAttemptId);
                // An app relaunch has no old transport/connection object. The server must bind by account.
                Require(join.PreviousConnectionId == "", "Fresh app fixture must omit its old connection ID.");
                await Send(active, join, ct);
                var resumed = await ReadUntil(active, m => m.HasAssignedSeat, ct);
                var restored = await ReadUntil(active, m => m.StateView != null, ct);
                Require(resumed.MatchId == match && resumed.AccountId == owner.AccountId.ToString() &&
                    resumed.AssignedSeatId == assigned.AssignedSeatId && resumed.AssignedOnlineSeatId == assigned.AssignedOnlineSeatId,
                    "Fresh socket must attach the same account to the same PvE seat.");
                Require(BattleState(restored.StateView!) == expectedState, "Two reconnects must preserve moved master, hand, resources, turn deadline and hidden AI hand.");
                Require(restored.StateView!.Opponent.VisibleHandCardIds.Count == 0 && restored.StateView.Opponent.VisibleHandCards.Count == 0, "Reconnect cannot reveal AI hand identities.");
                Require(await Scores(db, owner, ct) == before, "Reconnect alone must not change run results/economy.");
                assigned = resumed;
            }
            pass("real PvE WebSocket loss and two fresh-app reconnects discover the same account battle and retain seat, moved board, hand, resources and timer");
            await SendCommand(active, owner, assigned, OnlineBattleCommandType.Surrender, 3, ct);
            await ReadUntil(active, m => m.StateView?.IsEnded == true, ct);
            var after = await Scores(db, owner, ct);
            Require(after.Wins == before.Wins && after.Losses == before.Losses && after.Wallet == before.Wallet &&
                await Scores(db, outsider, ct) == outsiderBefore, "A lost AI challenge must preserve both run scores and both accounts economies.");
            await using var receipt = new NpgsqlCommand("select payload::text from battle_result_receipts where match_id=@match;", db);
            receipt.Parameters.AddWithValue("match", match);
            var payload = (string)(await receipt.ExecuteScalarAsync(ct))!;
            var recorded = JsonSerializer.Deserialize<BattleResult>(payload, Json)!;
            Require(recorded.UseServerAiOpponent && recorded.AiChallengeAttemptId?.ToString() == challengeAttemptId && recorded.Runs.Count == 1 && recorded.Runs[0].RunId == owner.RunId.ToString() &&
                recorded.Runs[0].DeckId == owner.DeckId.ToString(), "Restored PvE result must bind the original run and deck.");
            var store = new BattleResultStore(new DbConnectionFactory(configuration));
            Require(!await store.ApplyAsync(recorded, ct) && !await store.ApplyAsync(recorded, ct) && await Scores(db, owner, ct) == after,
                "Duplicate restored-battle result delivery must not add losses twice.");
            Require(!Field(await Status(http, owner, "/battle/reconnect-status", ct), "HasReconnectableBattle").GetBoolean(), "Ended PvE battle must disappear from reconnect discovery.");
            using var late = await Open(ct);
            await Send(late, Join(owner, match, true, challengeAttemptId), ct);
            var rejection = await ReadUntil(late, m => m.Error != null, ct, allowError: true);
            Require(rejection.Error!.Code == "ai_challenge_not_available" && await Scores(db, owner, ct) == after,
                "A late reconnect cannot silently restart an ended PvE or apply another loss.");
            pass("restored AI challenge loss leaves normal run/economy unchanged, preserves admission/deck binding, deduplicates delivery and rejects ended reconnect");
        }
        finally { foreach (var socket in sockets) { socket.Abort(); socket.Dispose(); } }
    }

    internal static async Task<(string MatchId, string AttemptId)> StartChallenge(HttpClient http, TestAccount account, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ai-challenge/start")
        {
            Content = JsonContent.Create(new { RequestId = Guid.NewGuid(), RunId = account.RunId.ToString(), DeckId = account.DeckId.ToString() })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var attempt = payload.RootElement;
        return (Field(attempt, "MatchId").GetString()!, Field(attempt, "AttemptId").GetString()!);
    }

    static BattleClientConnection Connection(string? account = null)
    {
        var c = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket()) {
            AccountId = account ?? Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N"),
            RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString() };
        c.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33)); c.CardUpgradeLevels["Goblin"] = 2;
        return c;
    }
    static async Task WaitUntil(DateTimeOffset deadline, CancellationToken ct)
    {
        while (DateTimeOffset.UtcNow < deadline)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, (deadline - DateTimeOffset.UtcNow).TotalMilliseconds)), ct);
    }
    static string BattleState(BattleStateViewDto s) => JsonSerializer.Serialize(new {
        s.MatchId, s.ViewerId, s.ViewerOnlineSeatId, s.TurnNumber, s.ActivePlayerId, s.Phase,
        s.TurnTimerVersion, s.TurnTimerDeadlineUnixTimeMilliseconds, s.IsEnded, s.Player, s.Opponent,
        s.Occupants, s.PersistentEffects, s.HasPendingRobotFusion, s.PendingRobotFusionOwnerId, s.PendingRobotFusionCardId
    }, Json);
    static async Task<(int Wins, int Losses, string Wallet)> Scores(NpgsqlConnection db, TestAccount a, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("select wins,losses,(select to_jsonb(w)::text from user_wallets w where account_id=@account) from draft_runs where id=@run;", db);
        query.Parameters.AddWithValue("account", a.AccountId); query.Parameters.AddWithValue("run", a.RunId);
        await using var reader = await query.ExecuteReaderAsync(ct); Require(await reader.ReadAsync(ct), "Fixture run required.");
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2));
    }
    static async Task<JsonElement> WaitForDiscovery(HttpClient http, TestAccount a, string match, CancellationToken ct)
    {
        for (var i = 0; i < 40; i++) {
            var response = await Status(http, a, "/battle/reconnect-status", ct);
            if (Field(response, "HasReconnectableBattle").GetBoolean()) {
                Require(Field(response, "MatchId").GetString() == match, "Discovery must return the interrupted match."); return response;
            }
            await Task.Delay(100, ct);
        }
        throw new InvalidOperationException("PvE battle was not discoverable after transport termination.");
    }
    static async Task<JsonElement> Status(HttpClient http, TestAccount a, string route, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a.Token);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return document.RootElement.Clone();
    }
    static JsonElement Field(JsonElement value, string name) => value.EnumerateObject().First(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    static OnlineBattleEnvelope Join(TestAccount a, string match, bool reconnect, string challengeAttemptId = "") => new() {
        MessageType = OnlineBattleMessageType.JoinMatch, MatchId = match, SessionToken = a.Token,
        PlayerToken = a.AccountId.ToString(), AccountId = a.AccountId.ToString(), RunId = a.RunId.ToString(), DeckId = a.DeckId.ToString(),
        UseServerAiOpponent = true, UseMatchmakingQueue = false, IsReconnectAttempt = reconnect,
        GameMode = "ai_challenge", AiChallengeAttemptId = challengeAttemptId };
    static Task SendCommand(ClientWebSocket socket, TestAccount a, OnlineBattleEnvelope assigned, OnlineBattleCommandType type, long sequence, CancellationToken ct) =>
        Send(socket, new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.ClientCommand, ClientCommand = new ClientBattleCommandMessage {
            MatchId = assigned.MatchId, AccountId = a.AccountId.ToString(), PlayerToken = a.AccountId.ToString(),
            ActorId = assigned.AssignedSeatId, CommandType = type, Sequence = sequence } }, ct);
    static async Task<ClientWebSocket> Open(CancellationToken ct)
    {
        var socket = new ClientWebSocket(); socket.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct); return socket;
    }
    static Task Send(ClientWebSocket socket, OnlineBattleEnvelope message, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Json)), WebSocketMessageType.Text, true, ct);
    static async Task<OnlineBattleEnvelope> ReadUntil(ClientWebSocket socket, Func<OnlineBattleEnvelope, bool> predicate, CancellationToken ct, bool allowError = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var buffer = new byte[32768];
        while (true) {
            using var data = new MemoryStream(); WebSocketReceiveResult part;
            do { part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
                Require(part.MessageType != WebSocketMessageType.Close, "Unexpected battle close."); data.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
            var m = JsonSerializer.Deserialize<OnlineBattleEnvelope>(data.ToArray(), Json)!;
            if (m.Error != null && !allowError) throw new InvalidOperationException("PvE request rejected: " + m.Error.Code + " " + m.Error.Message);
            if (predicate(m)) return m;
        }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
