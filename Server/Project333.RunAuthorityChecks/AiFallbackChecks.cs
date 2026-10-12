using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Challenges;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.Runtime.Application.Online;
using ServerCommand = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using ServerCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;

static class AiFallbackChecks
{
    public static async Task Run(NpgsqlConnection db, IConfiguration configuration,
        TestAccount a, TestAccount b, TestAccount c, Action<string> pass, CancellationToken ct)
    {
        var factory = new DbConnectionFactory(configuration);
        var service = new PvpMatchmakingService(factory);
        var results = new BattleResultStore(factory);
        var challenges = new AiChallengeService(factory);
        var delay = TimeSpan.FromSeconds(20);

        PvpMatchmakingQueueRequest Request(TestAccount x) => new(x.AccountId.ToString(),
            x.RunId.ToString(), x.DeckId.ToString(), "0.1.0-dev", Guid.NewGuid().ToString("N"));
        BattleClientConnection Client(TestAccount account, PvpMatchmakingQueueRequest request,
            PvpMatchmakingMatchAssignment assignment) => new(request.ConnectionId, new OfflineSocket())
            {
                AccountId = request.AccountId, PlayerToken = request.AccountId, RunId = request.RunId,
                DeckId = request.DeckId, ReservedOnlineSeatId = Enum.Parse<OnlineBattleSeatId>(assignment.Seat),
                MatchmakingJoinPending = true, MatchmakingReservationId = assignment.ReservationId
            };
        async Task Join(TestAccount account, PvpMatchmakingQueueRequest request,
            PvpMatchmakingMatchAssignment assignment, BattleSession session, BattleClientConnection client)
        {
            client.PlayerDeckCardIds.AddRange(account.Cards);
            Require(session.TryAddConnection(client, out var error), error);
            await service.RecordConnectionJoinedAsync(new(assignment.MatchId, request.AccountId,
                request.ConnectionId, assignment.Seat, client.AssignedSeatId.ToString(), request.RunId,
                request.DeckId, request.ClientVersion, "127.0.0.1", "synthetic-ai-fallback",
                assignment.ReservationId), ct);
            session.ConfirmMatchmakingJoin(client);
        }
        async Task Age(PvpMatchmakingQueueRequest request, string match)
        {
            await using var q = new NpgsqlCommand("""
                update pvp_match_connections set connected_at=clock_timestamp()-interval '21 seconds'
                where connection_id=@connection;
                update pvp_match_queue set requested_at=clock_timestamp()-interval '21 seconds'
                where account_id=@account and matched_match_id=(select id from pvp_matches where match_id=@match);
                """, db);
            q.Parameters.AddWithValue("connection", request.ConnectionId);
            q.Parameters.AddWithValue("account", Guid.Parse(request.AccountId));
            q.Parameters.AddWithValue("match", match);
            await q.ExecuteNonQueryAsync(ct);
        }
        async Task<(string Opponent, string Status)> Status(string match)
        {
            await using var q = new NpgsqlCommand("select opponent_kind,match_status from pvp_matches where match_id=@match;", db);
            q.Parameters.AddWithValue("match", match);
            await using var reader = await q.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct), "Fallback match must remain persisted.");
            return (reader.GetString(0), reader.GetString(1));
        }
        async Task RejectActiveAiParticipantChallenge(string state)
        {
            try
            {
                await challenges.StartAsync(b.AccountId,
                    new(Guid.NewGuid(), b.RunId.ToString(), b.DeckId.ToString()), ct);
            }
            catch (RunServiceException ex) when (ex.Code == "battle_already_active") { return; }
            throw new InvalidOperationException("The actual AI participant must not start a challenge during " + state + ".");
        }
        async Task CheckLoss(BattleSession session, BattleClientConnection human, TestAccount account)
        {
            session.ApplyCommand(new ServerCommand { CommandType = ServerCommandType.Surrender, ActorId = human.AssignedSeatId });
            var result = session.GetFinalResult()!;
            Require(result.UseServerAiOpponent && result.AiChallengeAttemptId == null &&
                result.WinnerSeat == OnlineBattleSeatId.ServerAI && result.Runs.Count == 1 &&
                result.Runs[0].AccountId == account.AccountId.ToString() &&
                result.Runs[0].RunId == account.RunId.ToString() && !result.Runs[0].Won,
                "General AI fallback must create exactly the original human's normal run loss.");
            Require(await results.ApplyAsync(result, ct), "First fallback result must persist.");
            Require(!await results.ApplyAsync(result, ct), "Fallback result retry must be idempotent.");
            Require((await Status(session.MatchId)).Status == "completed",
                "An AI winner without a player row must still close the persisted matchmaking room.");
        }

        var ar = Request(a);
        var first = await service.ResolveMatchForQueueAsync(ar, ct);
        var session = new BattleSession(first.MatchId, false);
        var human = Client(a, ar, first);
        await Join(a, ar, first, session, human);
        Require(!(await service.TryStartServerAiFallbackAsync(session, delay, ct)).Started &&
            !session.UseServerAiOpponent && !session.IsBattleStarted,
            "Client join cannot skip the actual server-side 20 second waiting time.");
        await Age(ar, first.MatchId);
        var br = Request(b);
        var second = await service.ResolveMatchForQueueAsync(br, ct);
        Require(second.MatchId == first.MatchId, "A waiting human opponent must get this room first.");
        Require(!(await service.TryStartServerAiFallbackAsync(session, delay, ct)).Started,
            "A second human's pending DB seat reservation must block AI promotion.");
        // Hold the same DB advisory lock to reproduce a human joining after the
        // promotion's in-memory reservation but before its database eligibility check.
        await using (var gate = await db.BeginTransactionAsync(ct))
        {
            await using (var hold = new NpgsqlCommand("select pg_advisory_xact_lock(333,13);", db, gate))
                await hold.ExecuteNonQueryAsync(ct);
            var pendingPromotion = service.TryStartServerAiFallbackAsync(session, delay, ct);
            var reservedHuman = Client(b, br, second);
            Require(session.TryAddConnection(reservedHuman, out var reservedError),
                "A valid reserved human must still enter while AI claim awaits its DB lock: " + reservedError);
            Require(!session.TryStartBattleIfReady(out _), "Uncommitted human join must still wait for its DB confirmation.");
            await gate.CommitAsync(ct);
            Require(!(await pendingPromotion).Started, "A reserved human arriving during promotion must retain priority.");
            session.RemoveConnection(reservedHuman.ConnectionId);
        }
        await service.ReleaseReservationAsync(second.ReservationId!.Value, br.ConnectionId, ct);

        var promotions = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => service.TryStartServerAiFallbackAsync(session, delay, ct)));
        Require(promotions.Count(x => x.Started) == 1 && session.UseServerAiOpponent &&
            session.IsBattleStarted && session.GameMode == "run" && session.MatchId == first.MatchId &&
            human.AssignedOnlineSeatId == OnlineBattleSeatId.PlayerA,
            "Concurrent timer ticks must start one AI battle in the same normal run session.");
        Require(await Status(first.MatchId) == ("ai", "active"),
            "Promotion must persist AI opponent and active status before reporting success.");
        var cr = Request(c);
        var third = await service.ResolveMatchForQueueAsync(cr, ct);
        Require(third.MatchId != first.MatchId, "AI promotion must remove the room from human matching.");
        Require(!session.TryAddConnection(Client(c, cr, third), out _), "An AI room must reject a second human socket.");
        await service.ReleaseReservationAsync(third.ReservationId!.Value, cr.ConnectionId, ct);
        await CheckLoss(session, human, a);
        pass("20 second server wait, pending-human priority, concurrent one-time AI promotion and normal-run AI loss persistence");

        // PlayerA really joins and then leaves while PlayerB has a pending reservation.
        // Keep that disconnected participant's durable audit row, but only PlayerB
        // participates in the subsequent AI battle and is blocked from a new challenge.
        ar = Request(a); first = await service.ResolveMatchForQueueAsync(ar, ct);
        br = Request(b); second = await service.ResolveMatchForQueueAsync(br, ct);
        Require(second.MatchId == first.MatchId && second.Seat == "PlayerB", "Synthetic PlayerB setup failed.");
        session = new BattleSession(first.MatchId, false);
        var formerHuman = Client(a, ar, first);
        await Join(a, ar, first, session, formerHuman);
        Require(session.RemoveConnection(formerHuman.ConnectionId), "Former PlayerA must leave the waiting room.");
        await service.RecordConnectionDisconnectedAsync(new(first.MatchId, ar.AccountId,
            ar.ConnectionId, "PlayerA", null), ct);
        human = Client(b, br, second);
        await Join(b, br, second, session, human);
        await Age(br, first.MatchId);
        Require((await service.TryStartServerAiFallbackAsync(session, delay, ct)).Started,
            "The remaining PlayerB must also receive an AI fallback.");
        Require(human.AssignedSeatId == PlayerIdDto.Player &&
            human.AssignedOnlineSeatId == OnlineBattleSeatId.PlayerB,
            "Runtime normalization must preserve the human's online PlayerB seat.");
        var view = session.CreateStateViewEnvelope(human);
        Require(view.AssignedOnlineSeatId == OnlineBattleSeatId.PlayerB &&
            view.UseServerAiOpponent && view.GameMode == "run" && view.StateView!.ViewerOnlineSeatId == OnlineBattleSeatId.PlayerB,
            "Fallback state must authoritatively update viewer and opponent mode.");
        var formerPlayerChallenge = await challenges.StartAsync(a.AccountId,
            new(Guid.NewGuid(), a.RunId.ToString(), a.DeckId.ToString()), ct);
        Require(formerPlayerChallenge.Status == "reserved" && formerPlayerChallenge.RunId == a.RunId &&
            formerPlayerChallenge.DeckId == a.DeckId,
            "A former waiting PlayerA may start a challenge while PlayerB continues the AI battle.");
        try
        {
            await RejectActiveAiParticipantChallenge("active connection");
        }
        finally
        {
            Require(await challenges.CancelAsync(a.AccountId, formerPlayerChallenge.AttemptId,
                true, "server_cancel", ct), "Synthetic former-player challenge admission must be released.");
        }
        var disconnect = session.DetachConnection(human, TimeSpan.FromSeconds(70))!;
        Require(disconnect.Reserved && session.TryGetReconnectStatus(br.AccountId, out var reconnect) &&
            reconnect!.UseServerAiOpponent && reconnect.GameMode == "run" &&
            reconnect.OnlineSeatId == OnlineBattleSeatId.PlayerB && reconnect.SeatId == PlayerIdDto.Player,
            "Normal AI fallback must expose the same 70 second reconnect contract and stable online seat.");
        await service.RecordConnectionDisconnectedAsync(new(first.MatchId, br.AccountId,
            br.ConnectionId, "PlayerB", disconnect.DeadlineUtc), ct);
        await RejectActiveAiParticipantChallenge("70 second reconnect grace");
        pass("former joined-then-left PlayerA may start an AI challenge; actual PlayerB is blocked while connected and during reconnect grace");
        var recovered = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
            { AccountId = br.AccountId, PlayerToken = br.AccountId, RunId = br.RunId, DeckId = br.DeckId };
        Require(session.TryAddConnection(recovered, out var reconnectError), reconnectError);
        Require(recovered.ReconnectedToPendingSeat && recovered.AssignedOnlineSeatId == OnlineBattleSeatId.PlayerB,
            "Reconnect must restore the same PlayerB seat after runtime normalization.");
        await service.RecordConnectionJoinedAsync(new(first.MatchId, br.AccountId, recovered.ConnectionId,
            "PlayerB", "Player", br.RunId, br.DeckId, br.ClientVersion, "127.0.0.1", "synthetic-ai-reconnect"), ct);
        await CheckLoss(session, recovered, b);
        pass("remaining PlayerB receives AI with stable online seat, original deck/run, 70 second reconnect and idempotent result");

        // A fresh join cannot inherit the canceled room's old timer.
        cr = Request(c); third = await service.ResolveMatchForQueueAsync(cr, ct);
        session = new BattleSession(third.MatchId, false); human = Client(c, cr, third);
        await Join(c, cr, third, session, human); await Age(cr, third.MatchId);
        session.RemoveConnection(human.ConnectionId);
        await service.RecordConnectionDisconnectedAsync(new(third.MatchId, cr.AccountId, cr.ConnectionId, third.Seat, null), ct);
        var retry = Request(c); var retryMatch = await service.ResolveMatchForQueueAsync(retry, ct);
        var retrySession = new BattleSession(retryMatch.MatchId, false); var retryHuman = Client(c, retry, retryMatch);
        await Join(c, retry, retryMatch, retrySession, retryHuman);
        Require(retryMatch.MatchId != third.MatchId &&
            !(await service.TryStartServerAiFallbackAsync(retrySession, delay, ct)).Started,
            "Cancel/rejoin must not inherit elapsed wait from the canceled queue entry.");
        retrySession.RemoveConnection(retryHuman.ConnectionId);
        await service.RecordConnectionDisconnectedAsync(new(retryMatch.MatchId, retry.AccountId,
            retry.ConnectionId, retryMatch.Seat, null), ct);
        pass("cancel/rejoin starts a fresh server waiting interval instead of granting immediate AI");

        var attemptId = Guid.NewGuid();
        var challenge = new BattleSession("ai-challenge-" + attemptId.ToString("N"), true, aiChallengeAttemptId: attemptId);
        Require(challenge.GameMode == "ai_challenge" && challenge.OpponentKind == "ai" &&
            challenge.AiChallengeAttemptId == attemptId, "Challenge purpose must be independent of opponent kind.");
        pass("battle session purpose separates general run AI from an AI challenge attempt");
    }

    public static async Task CheckNetwork(NpgsqlConnection db, TestAccount owner,
        Action<string> pass, CancellationToken ct)
    {
        var json = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        using var socket = new System.Net.WebSockets.ClientWebSocket();
        socket.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
        async Task Send(OnlineBattleEnvelope envelope, CancellationToken token) =>
            await socket.SendAsync(new ArraySegment<byte>(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope, json)),
                System.Net.WebSockets.WebSocketMessageType.Text, true, token);
        async Task<OnlineBattleEnvelope> Read(Func<OnlineBattleEnvelope, bool> predicate)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(35));
            var buffer = new byte[32768];
            while (true)
            {
                using var stream = new MemoryStream();
                System.Net.WebSockets.WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
                    Require(part.MessageType != System.Net.WebSockets.WebSocketMessageType.Close, "AI fallback socket closed unexpectedly.");
                    stream.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                var message = System.Text.Json.JsonSerializer.Deserialize<OnlineBattleEnvelope>(stream.ToArray(), json)!;
                Require(message.Error == null, "AI fallback network request rejected: " + message.Error?.Code);
                if (predicate(message)) return message;
            }
        }
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Send(new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.JoinMatch, UseMatchmakingQueue = true, UseServerAiOpponent = false,
            GameMode = "run", SessionToken = owner.Token, AccountId = owner.AccountId.ToString(),
            PlayerToken = owner.AccountId.ToString(), RunId = owner.RunId.ToString(), DeckId = owner.DeckId.ToString()
        }, ct);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = Task.Run(async () =>
        {
            while (!heartbeatCancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), heartbeatCancellation.Token);
                await Send(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.KeepAlive }, heartbeatCancellation.Token);
            }
        }, heartbeatCancellation.Token);
        OnlineBattleEnvelope assigned;
        OnlineBattleEnvelope started;
        try
        {
            assigned = await Read(m => m.HasAssignedSeat);
            started = await Read(m => m.StateView != null);
            Require(stopwatch.Elapsed >= TimeSpan.FromSeconds(19.5),
                "Real WebSocket fallback must not start before the configured server 20 second wait.");
            Require(started.MatchId == assigned.MatchId && started.UseServerAiOpponent &&
                started.GameMode == "run" && string.IsNullOrEmpty(started.AiChallengeAttemptId) &&
                started.StateView!.Opponent.OnlineSeatId == OnlineBattleSeatId.ServerAI,
                "The waiting socket must receive authoritative AI state in its original normal match.");
        }
        finally
        {
            heartbeatCancellation.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
        await Send(new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.ClientCommand,
            ClientCommand = new ServerCommand
            {
                MatchId = started.MatchId, ActorId = started.AssignedSeatId,
                AccountId = owner.AccountId.ToString(), PlayerToken = owner.AccountId.ToString(),
                CommandType = ServerCommandType.Surrender, Sequence = 1
            }
        }, ct);
        await Read(m => m.StateView?.IsEnded == true);
        await using var result = new NpgsqlCommand("""
            select r.wins=0 and r.losses=1 and m.opponent_kind='ai' and m.match_status='completed'
                and m.winner_account_id is null
                and (select count(*) from battle_result_receipts x where x.match_id=m.match_id)=1
                and not exists(select 1 from ai_challenge_attempts c where c.account_id=@account)
            from draft_runs r join pvp_matches m on m.match_id=@match
            where r.id=@run;
            """, db);
        result.Parameters.AddWithValue("account", owner.AccountId);
        result.Parameters.AddWithValue("run", owner.RunId);
        result.Parameters.AddWithValue("match", started.MatchId);
        Require(await result.ExecuteScalarAsync(ct) is true,
            "Real fallback surrender must record one normal run loss without consuming a daily challenge.");
        pass("real WebSocket waits 20 seconds with heartbeat, starts same-session AI and records one run loss without challenge usage");
        socket.Abort();
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
