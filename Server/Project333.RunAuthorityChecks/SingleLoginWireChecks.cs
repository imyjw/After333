using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Online;
using WireCommand = Project333.PvpServer.Messages.OnlineBattleCommandType;
using WireCommandMessage = Project333.PvpServer.Messages.ClientBattleCommandMessage;

// Only synthetic accounts in RunAuthorityChecks' verified fresh loopback cluster.
static class SingleLoginWireChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() } };
    const string Password = "Synthetic_Single_Login_333!";
    const string ReplacedMessage = "다른 기기에서 로그인하여 연결이 종료되었습니다.";

    public static async Task Run(HttpClient http, NpgsqlConnection db, Action<string> pass, CancellationToken ct)
    {
        await CheckBattle(http, db, false, pass, ct);
        await CheckBattle(http, db, true, pass, ct);
    }

    static async Task CheckBattle(HttpClient http, NpgsqlConnection db, bool pve, Action<string> pass, CancellationToken ct)
    {
        var mode = pve ? "PvE" : "PvP";
        var owner = await Draft(http, db, mode + "-owner", ct);
        var outsider = await Draft(http, db, mode + "-outsider", ct);
        var sockets = new List<ClientWebSocket>();
        try
        {
            var match = "single-login-" + Guid.NewGuid().ToString("N");
            var attempt = "";
            if (pve)
            {
                var admission = await PveReconnectChecks.StartChallenge(http, owner.Account, ct);
                match = admission.MatchId; attempt = admission.AttemptId;
            }
            var old = await Open(ct); sockets.Add(old);
            await Send(old, Join(owner.Account, match, pve, false, attempt), ct);
            var assigned = await ReadUntil(old, m => m.HasAssignedSeat, ct);
            match = assigned.MatchId;
            ClientWebSocket? other = null;
            if (!pve)
            {
                other = await Open(ct); sockets.Add(other);
                await Send(other, Join(outsider.Account, match, false, false), ct);
                await ReadUntil(other, m => m.HasAssignedSeat, ct);
                await ReadUntil(other, m => m.StateView != null, ct);
            }
            var initial = await ReadUntil(old, m => m.StateView != null, ct);
            var expected = StableState(initial.StateView!);
            var scoreBefore = await Scores(db, owner.Account, ct);
            Require(initial.StateView!.Opponent.VisibleHandCardIds.Count == 0 &&
                initial.StateView.Opponent.VisibleHandCards.Count == 0, "Opponent hand must stay hidden.");

            // Rotating an access token must retain the family and its live socket.
            var refreshed = await Post(http, "/auth/refresh", new { refreshToken = owner.RefreshToken,
                clientVersion = "0.1.0-dev" }, null, ct);
            var refreshedToken = Field(refreshed, "sessionToken").GetString()!;
            var refreshedOwner = owner.Account with { Token = refreshedToken };
            await Send(old, Join(refreshedOwner, match, pve, false, attempt), ct);
            var refreshedJoin = await ReadUntil(old, m => m.HasAssignedSeat, ct);
            var afterRefresh = await ReadUntil(old, m => m.StateView != null, ct);
            Require(refreshedJoin.ConnectionId == assigned.ConnectionId && StableState(afterRefresh.StateView!) == expected,
                "Same-family refresh must not evict/restart the live battle connection.");
            pass(mode + " refresh retains the live connection, exact hand/HP/resources and original seat");

            // A genuinely independent login wins. Try a destructive command on the old
            // transport after its HTTP response to prove it cannot surrender the battle.
            var login = await Post(http, "/auth/login", new { gameId = owner.GameId, password = Password,
                clientVersion = "0.1.0-dev" }, null, ct);
            var fresh = owner.Account with { Token = Field(login, "sessionToken").GetString()! };
            try { await SendCommand(old, owner.Account, assigned, WireCommand.Surrender, 9999, ct); }
            catch (WebSocketException) { }
            catch (InvalidOperationException) when (old.State != WebSocketState.Open) { }
            var replaced = await ReadUntil(old, m => m.Error != null, ct, allowError: true);
            Require(replaced.Error!.Code == "session_replaced" && replaced.Error.Message == ReplacedMessage,
                "Old socket must receive the specific session_replaced notice.");
            await ExpectReplacedClose(old, ct);
            Require(await StatusCode(http, refreshedToken, "/auth/session-status", ct) == HttpStatusCode.Unauthorized,
                "Old HTTP session must fail after the winning login.");
            Require(await StatusCode(http, outsider.Account.Token, "/auth/session-status", ct) == HttpStatusCode.OK,
                "Another account must remain logged in.");
            if (!pve)
            {
                // The runner's auth budget is60/minute. Idle polling belongs to
                // its independent account budget, not the login-attempt budget.
                for (var probe = 0; probe < 65; probe++)
                    Require(await StatusCode(http, fresh.Token, "/auth/session-status", ct) == HttpStatusCode.OK,
                        "Lightweight session polling exhausted the separate login-attempt rate limit.");
                pass("65 authenticated session-status probes stay in the account API budget and do not exhaust the60/minute login budget");
            }

            var discovery = await Discover(http, fresh, match, ct);
            Require(Field(discovery, "RemainingSeconds").GetInt32() is >= 55 and <= 70,
                "Login replacement must reserve the existing 70-second reconnect window.");
            Require(Field(discovery, "BattleMode").GetString() == (pve ? "pve" : "pvp"),
                "Discovery must preserve original battle mode.");
            var current = await Open(ct); sockets.Add(current);
            var resume = Join(fresh, match, pve, true, attempt);
            Require(resume.PreviousConnectionId.Length == 0, "New-device resume intentionally has no old connection ID.");
            await Send(current, resume, ct);
            var resumed = await ReadUntil(current, m => m.HasAssignedSeat, ct);
            var restored = await ReadUntil(current, m => m.StateView != null, ct);
            Require(resumed.AssignedSeatId == assigned.AssignedSeatId &&
                resumed.AssignedOnlineSeatId == assigned.AssignedOnlineSeatId && resumed.MatchId == match &&
                resumed.AccountId == owner.Account.AccountId.ToString(), "New login must recover the original match and both seat identities.");
            Require(StableState(restored.StateView!) == expected && !restored.StateView!.IsEnded,
                "Replacement must preserve board/HP/hand/resources/timer and reject the old surrender command.");
            Require(await Scores(db, owner.Account, ct) == scoreBefore,
                "Replacing login or resuming cannot change run/wallet/challenge counts.");
            if (!pve)
            {
                await SendCommand(current, fresh, resumed, WireCommand.Surrender, 1, ct);
                await ReadUntil(current, m => m.StateView?.IsEnded == true, ct);
                await ReadUntil(other!, m => m.StateView?.IsEnded == true, ct);
            }
            else
            {
                await SendCommand(current, fresh, resumed, WireCommand.Surrender, 1, ct);
                await ReadUntil(current, m => m.StateView?.IsEnded == true, ct);
            }
            pass(mode + " new-device login kicks only the superseded socket (1008/session_replaced), rejects its late command and resumes the original 70-second battle/seat/HP/hand");
        }
        finally { foreach (var socket in sockets) { socket.Abort(); socket.Dispose(); } }
    }

    static async Task<(TestAccount Account, string GameId, string RefreshToken)> Draft(HttpClient http, NpgsqlConnection db, string label, CancellationToken ct)
    {
        var gameId = "single." + Guid.NewGuid().ToString("N")[..15];
        var auth = await Post(http, "/auth/register", new { gameId, password = Password,
            displayName = label, clientVersion = "0.1.0-dev" }, null, ct);
        var token = Field(auth, "sessionToken").GetString()!;
        var id = Field(Field(auth, "account"), "id").GetGuid();
        var started = await Post(http, "/runs/start", new { mode = "pve" }, token, ct);
        var runId = Field(Field(started, "activeRun"), "id").GetGuid();
        var offer = Field(started, "currentOfferCardIds");
        var cards = new List<string>(); Guid deck = default;
        for (var index = 0; index < 33; index++)
        {
            var card = offer[0].GetString()!; cards.Add(card);
            var picked = await Post(http, "/runs/select-draft-card", new { runId, cardId = card, pickIndex = index }, token, ct);
            offer = Field(picked, "currentOfferCardIds");
            if (index == 32) deck = Field(Field(picked, "deck"), "id").GetGuid();
        }
        await using var query = new NpgsqlCommand("select count(*) from draft_runs where id=@run and account_id=@account;", db);
        query.Parameters.AddWithValue("run", runId); query.Parameters.AddWithValue("account", id);
        Require((long)(await query.ExecuteScalarAsync(ct))! == 1, "Draft fixture must belong to its synthetic account.");
        return (new TestAccount(id, token, runId, deck, cards, 0, 0), gameId, Field(auth, "refreshToken").GetString()!);
    }
    static string StableState(BattleStateViewDto state) => JsonSerializer.Serialize(new {
        state.MatchId, state.ViewerId, state.ViewerOnlineSeatId, state.TurnNumber, state.ActivePlayerId,
        state.Phase, state.TurnTimerVersion, state.TurnTimerDeadlineUnixTimeMilliseconds, state.IsEnded,
        state.Player, state.Opponent, state.Occupants, state.PersistentEffects,
        state.HasPendingRobotFusion, state.PendingRobotFusionOwnerId, state.PendingRobotFusionCardId }, Json);
    static async Task<string> Scores(NpgsqlConnection db, TestAccount account, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            select jsonb_build_object('run',to_jsonb(r),'wallet',(select to_jsonb(w) from user_wallets w where w.account_id=r.account_id))::text
            from draft_runs r where r.id=@run;
            """, db);
        query.Parameters.AddWithValue("run", account.RunId);
        return (string)(await query.ExecuteScalarAsync(ct))!;
    }
    static async Task<JsonElement> Discover(HttpClient http, TestAccount account, string match, CancellationToken ct)
    {
        for (var index = 0; index < 50; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/battle/reconnect-status");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
            using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (Field(result, "HasReconnectableBattle").GetBoolean())
            { Require(Field(result, "MatchId").GetString() == match, "Discovery returned another battle."); return result; }
            await Task.Delay(100, ct);
        }
        throw new InvalidOperationException("New-device login did not discover its old battle.");
    }
    static async Task<HttpStatusCode> StatusCode(HttpClient http, string token, string route, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct); return response.StatusCode;
    }
    static async Task<JsonElement> Post(HttpClient http, string route, object body, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(route + " failed: " + Field(Field(result, "error"), "code").GetString());
        return result;
    }
    static OnlineBattleEnvelope Join(TestAccount account, string match, bool pve, bool reconnect, string attempt = "") => new() {
        MessageType = OnlineBattleMessageType.JoinMatch, MatchId = match, SessionToken = account.Token,
        AccountId = account.AccountId.ToString(), PlayerToken = account.AccountId.ToString(),
        RunId = account.RunId.ToString(), DeckId = account.DeckId.ToString(),
        UseServerAiOpponent = pve, UseMatchmakingQueue = !pve, IsReconnectAttempt = reconnect,
        GameMode = pve ? "ai_challenge" : "run", AiChallengeAttemptId = attempt };
    static Task SendCommand(ClientWebSocket socket, TestAccount account, OnlineBattleEnvelope assigned, WireCommand type, long sequence, CancellationToken ct) =>
        Send(socket, new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.ClientCommand,
            ClientCommand = new WireCommandMessage { MatchId = assigned.MatchId, ActorId = assigned.AssignedSeatId,
                AccountId = account.AccountId.ToString(), PlayerToken = account.AccountId.ToString(), Sequence = sequence, CommandType = type } }, ct);
    static async Task<ClientWebSocket> Open(CancellationToken ct)
    {
        var socket = new ClientWebSocket(); socket.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct); return socket;
    }
    static Task Send(ClientWebSocket socket, OnlineBattleEnvelope message, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Json)), WebSocketMessageType.Text, true, ct);
    static async Task<OnlineBattleEnvelope> ReadUntil(ClientWebSocket socket, Func<OnlineBattleEnvelope, bool> predicate, CancellationToken ct, bool allowError = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var buffer = new byte[32768];
        while (true)
        {
            using var data = new MemoryStream(); WebSocketReceiveResult part;
            do { part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
                Require(part.MessageType != WebSocketMessageType.Close, "Socket closed before the expected response."); data.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
            var message = JsonSerializer.Deserialize<OnlineBattleEnvelope>(data.ToArray(), Json)!;
            if (message.Error != null && !allowError) throw new InvalidOperationException("Battle rejected request: " + message.Error.Code);
            if (predicate(message)) return message;
        }
    }
    static async Task ExpectReplacedClose(ClientWebSocket socket, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[32768];
        for (var index = 0; index < 30; index++)
        {
            var part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
            if (part.MessageType != WebSocketMessageType.Close) continue;
            Require(part.CloseStatus == WebSocketCloseStatus.PolicyViolation && part.CloseStatusDescription == "session_replaced",
                "Superseded transport must close with 1008/session_replaced.");
            return;
        }
        throw new InvalidOperationException("Superseded transport did not close.");
    }
    static JsonElement Field(JsonElement value, string name) => value.EnumerateObject().First(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
