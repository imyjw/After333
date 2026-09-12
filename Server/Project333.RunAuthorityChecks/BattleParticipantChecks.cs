using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;

static class BattleParticipantChecks
{
    public static void Run(Action<string> pass)
    {
        var a = Connection();
        var b = Connection();
        var session = Start(a, b);
        var original = Participant(session, a.AccountId);
        var battleBefore = JsonSerializer.Serialize(session.CreatePersistenceSnapshot("before").DomainState);
        a.RunId = b.RunId; a.DeckId = b.DeckId;
        a.PlayerDeckCardIds.Clear(); a.PlayerDeckCardIds.Add("not-a-real-card");
        a.CardUpgradeLevels["Goblin"] = 999;
        session.RefreshConnectionMetadata(a);
        Bound(a, original);
        Require(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("after").DomainState) == battleBefore,
            "Metadata refresh must not reset hands, resources, board or turn state.");
        var spoof = Connection(); spoof.AssignedSeatId = a.AssignedSeatId; spoof.HasAssignedSeat = true;
        Throws(() => session.RefreshConnectionMetadata(spoof));
        a.AccountId = b.AccountId;
        Throws(() => session.RefreshConnectionMetadata(a));
        a.AccountId = original.AccountId;
        EndAndCheck(session, a, original);
        pass("started battle ignores forged run/deck/cards/upgrades; wrong account and unregistered refresh rejected; original owner receives result");

        a = Connection(); b = Connection(); session = Start(a, b);
        original = Participant(session, a.AccountId);
        var attacker = Connection();
        Require(!session.TryReplaceConnectionForReconnect(attacker, a.ConnectionId, out _, out _),
            "Another account cannot replace a live battle participant.");
        Require(!a.IsSuperseded && session.ConnectionCount == 2, "Rejected takeover must preserve the existing socket.");
        var replacement = Connection(a.AccountId);
        Require(session.TryReplaceConnectionForReconnect(replacement, a.ConnectionId, out var replaced, out var error), error);
        Require(ReferenceEquals(replaced, a) && a.IsSuperseded, "Valid takeover must supersede the old connection.");
        Bound(replacement, original);
        EndAndCheck(session, replacement, original);
        pass("live socket takeover restores the original participant binding and rejects another account");

        foreach (var pve in new[] { false, true })
        {
            a = Connection(); b = Connection(); session = Start(a, b, pve);
            original = Participant(session, a.AccountId);
            Disconnect(session, a);
            replacement = Connection(a.AccountId);
            Require(session.TryAddConnection(replacement, out error), error);
            Bound(replacement, original);
            EndAndCheck(session, replacement, original);
            pass($"{(pve ? "PVE" : "PVP")} disconnect/reconnect preserves the initial run, deck and result owner");
        }

        a = Connection(); b = Connection(); session = Start(a, b);
        original = Participant(session, a.AccountId);
        var legacy = session.CreatePersistenceSnapshot("legacy") with { Participants = null };
        Disconnect(session, a); Disconnect(session, b);
        var snapshot = session.CreatePersistenceSnapshot("both_disconnected");
        Require(snapshot.Connections.Count == 0 && snapshot.Participants!.Count == 2,
            "Disconnected participants must survive independently from socket snapshots.");
        // JSON round trip exercises the persistence format, not just shared in-memory references.
        var restored = Restore(JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Require(!restored.TryRestorePendingReconnectReservation("invalid", "PlayerA", a.AccountId, "", DateTimeOffset.UtcNow.AddMinutes(1)), "Malformed seat must fail closed.");
        Require(!restored.TryRestorePendingReconnectReservation("Player", "PlayerA", b.AccountId, "", DateTimeOffset.UtcNow.AddMinutes(1)), "Wrong owner cannot reserve a restored seat.");
        Reserve(restored, a);
        replacement = Connection(a.AccountId);
        Require(restored.TryAddConnection(replacement, out error), error);
        Bound(replacement, original);
        EndAndCheck(restored, replacement, original);
        pass("JSON recovery after both sockets disconnect preserves both participants and allows only the stored account to reconnect");

        restored = Restore(legacy);
        Reserve(restored, a);
        replacement = Connection(a.AccountId);
        Require(restored.TryAddConnection(replacement, out error), error);
        Require(replacement.RunId == original.RunId && replacement.DeckId == original.DeckId,
            "Legacy recovery must retain only the server-saved run/deck.");
        EndAndCheck(restored, replacement, original);
        restored = Restore(snapshot with { Participants = null });
        Require(!restored.TryRestorePendingReconnectReservation("Player", "PlayerA", a.AccountId, "", DateTimeOffset.UtcNow.AddMinutes(1)),
            "Legacy snapshots missing disconnected participants must not trust new client metadata.");
        pass("legacy snapshot recovery uses stored participants; missing historical identity fails closed");

        a = Connection(); b = Connection();
        a.RunId = ""; a.DeckId = "";
        session = Start(a, b);
        a.RunId = b.RunId; a.DeckId = b.DeckId;
        session.RefreshConnectionMetadata(a);
        Require(a.RunId == "" && a.DeckId == "", "Practice battle cannot acquire a run after start.");
        Surrender(session, a);
        Require(session.GetFinalResult()!.Runs.All(r => r.AccountId != a.AccountId),
            "Practice seat must not emit a ranked run result.");
        pass("a battle started without a run cannot attach a reward-bearing run later");

        a = Connection(); b = Connection();
        session = new BattleSession("binding-" + Guid.NewGuid().ToString("N"), false);
        Require(session.TryAddConnection(a, out error), error);
        a.RunId = ""; a.DeckId = ""; session.RefreshConnectionMetadata(a);
        Require(Participant(session, a.AccountId).RunId == "", "Pre-start clearing must remove stale run metadata.");
        session.RemoveConnection(a.ConnectionId);
        Require(session.CreatePersistenceSnapshot("waiting").Participants!.Count == 0, "Departed waiting participant must not leave a result binding.");
        a = Connection(); Require(session.TryAddConnection(a, out error), error);
        a.RunId = Guid.NewGuid().ToString(); session.RefreshConnectionMetadata(a);
        Require(session.TryAddConnection(b, out error), error);
        Require(session.TryStartBattleIfReady(out error), error);
        original = Participant(session, a.AccountId);
        var exported = session.CreatePersistenceSnapshot("copy").Participants!.Single(p => p.AccountId == a.AccountId);
        ((string[])exported.DeckCardIds)[0] = "changed-copy";
        ((Dictionary<string, int>)exported.CardUpgradeLevels)["Goblin"] = 999;
        session.RefreshConnectionMetadata(a);
        Bound(a, original);
        EndAndCheck(session, a, original);
        pass("waiting-room edits remain supported, departed seats are cleared and exported snapshots cannot mutate live bindings");
    }

    static BattleClientConnection Connection(string? account = null)
    {
        var c = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
        {
            AccountId = account ?? Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N"),
            RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString(),
        };
        c.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        c.CardUpgradeLevels["Goblin"] = 2;
        return c;
    }

    static BattleSession Start(BattleClientConnection a, BattleClientConnection b, bool pve = false)
    {
        var s = new BattleSession("binding-" + Guid.NewGuid().ToString("N"), pve);
        Require(s.TryAddConnection(a, out var error), error);
        if (!pve) Require(s.TryAddConnection(b, out error), error);
        Require(s.TryStartBattleIfReady(out error), error);
        return s;
    }
    static BattleSessionParticipantSnapshot Participant(BattleSession s, string account) =>
        s.CreatePersistenceSnapshot("check").Participants!.Single(p => p.AccountId == account);
    static void Bound(BattleClientConnection c, BattleSessionParticipantSnapshot p) =>
        Require(c.AccountId == p.AccountId && c.RunId == p.RunId && c.DeckId == p.DeckId &&
            c.PlayerDeckCardIds.SequenceEqual(p.DeckCardIds) &&
            c.CardUpgradeLevels.Count == p.CardUpgradeLevels.Count &&
            p.CardUpgradeLevels.All(x => c.CardUpgradeLevels.TryGetValue(x.Key, out var level) && level == x.Value),
            "Reconnected socket must receive all initial server participant metadata.");
    static void Disconnect(BattleSession s, BattleClientConnection c)
    {
        Require(s.TryCreateDisconnectReconnectGraceEnvelope(c, TimeSpan.FromMinutes(1), out _, out _, out _, out _), "Disconnect must reserve the existing seat.");
        Require(s.RemoveConnection(c.ConnectionId), "Disconnect removes only the socket.");
    }
    static BattleSession Restore(BattleSessionPersistenceSnapshot snapshot)
    {
        var s = new BattleSession(snapshot.MatchId, snapshot.UseServerAiOpponent);
        Require(s.RestoreFromPersistenceSnapshot(snapshot), "Saved battle must restore.");
        return s;
    }
    static void Reserve(BattleSession s, BattleClientConnection c) =>
        Require(s.TryRestorePendingReconnectReservation(c.AssignedSeatId.ToString(), c.AssignedOnlineSeatId.ToString(),
            c.AccountId, c.PlayerToken, DateTimeOffset.UtcNow.AddMinutes(1)), "Stored owner must regain its saved seat.");
    static void Surrender(BattleSession s, BattleClientConnection c) => s.ApplyCommand(new ClientBattleCommandMessage
    {
        MatchId = s.MatchId, ActorId = c.AssignedSeatId, CommandType = OnlineBattleCommandType.Surrender,
        PlayerToken = c.PlayerToken, Sequence = 1,
    });
    static void EndAndCheck(BattleSession s, BattleClientConnection c, BattleSessionParticipantSnapshot p)
    {
        Surrender(s, c);
        var record = s.GetFinalResult()!.Runs.Single(r => r.AccountId == p.AccountId);
        Require(record.RunId == p.RunId && record.DeckId == p.DeckId && !record.Won &&
            record.SeatId == c.AssignedSeatId && record.OnlineSeatId == c.AssignedOnlineSeatId,
            "Result must reference the original account/run/deck/seat.");
    }
    static void Throws(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected participant identity rejection.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
