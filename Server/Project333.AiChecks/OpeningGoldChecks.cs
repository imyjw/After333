using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Online;
using OnlineBattleCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;
using ClientBattleCommandMessage = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using PlayerIdDto = Project333.PvpServer.Messages.PlayerIdDto;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

// Offline command/snapshot checks; no HTTP, database, accounts or live matches.
internal static class OpeningGoldChecks
{
    public static void Run()
    {
        foreach (var first in new[] { PlayerId.Player, PlayerId.AI })
        {
            using var socketA = new DeckCheckSocket();
            using var socketB = new DeckCheckSocket();
            var player = new BattleClientConnection("opening-a", socketA) { PlayerToken = "a" };
            var opponent = new BattleClientConnection("opening-b", socketB) { PlayerToken = "b" };
            player.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
            opponent.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
            var session = new BattleSession("opening-" + first, false,
                new RandomFirstPlayerSelector(new FixedFirstRandom(first)));
            Require(session.TryAddConnection(player, out _) && session.TryAddConnection(opponent, out _), "Seat admission failed.");
            Require(session.TryStartBattleIfReady(out _), "Battle did not start.");
            CheckViews(session, player, opponent, first, 3, 4);
            Require(State(session).Player.Hand.Count == 5 && State(session).AI.Hand.Count == 5 &&
                State(session).Player.Deck.Count == 28 && State(session).AI.Deck.Count == 28,
                "Opening mulligan must contain five cards per player from the 33-card deck.");
            Require(State(session).Phase == PhaseType.Mulligan, "Missing mulligan phase.");
            // Repeated start and pre-turn persistence must not duplicate or reassign opening Gold.
            Require(!session.TryStartBattleIfReady(out _), "Battle initialized twice.");
            session = Restore(session);
            CheckViews(session, player, opponent, first, 3, 4);
            session.ApplyCommand(Command(PlayerId.Player, OnlineBattleCommandType.PassMulligan));
            CheckViews(session, player, opponent, first, 3, 4);
            session.ApplyCommand(Command(PlayerId.AI, OnlineBattleCommandType.PassMulligan));
            Require(State(session).TurnNumber == 1 && State(session).Phase == PhaseType.Main, "First turn did not start.");
            CheckViews(session, player, opponent, first, 3, 4);
            Require(State(session).GetPlayer(first).Hand.Count == 6, "Opening draw must remain one card.");
            session = Restore(session);
            CheckViews(session, player, opponent, first, 3, 4);
            var second = State(session).GetOpponent(first).Id;
            session.ApplyCommand(Command(first, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 3, 4);
            Require(State(session).TurnNumber == 2 && State(session).GetPlayer(second).Hand.Count == 6,
                "Second opening draw changed.");
            session.ApplyCommand(Command(second, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 6, 4);
            session.ApplyCommand(Command(first, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 6, 7);
            session = Restore(session);
            CheckViews(session, player, opponent, first, 6, 7);
        }
        foreach (var first in new[] { PlayerId.Player, PlayerId.AI })
        {
            CheckFullHandMulligan(first);
            CheckMulliganTimeout(first);
        }
        Console.WriteLine("PASS Opening Gold: actual PvP setup, either first seat, both hidden-safe client views, five-card opening hands/deck28, full five-card replacement, invalid/repeated request rejection, mulligan timeout, first four turns, one-card opening draws, repeat start and exact JSON recovery.");
    }

    private static void CheckViews(BattleSession session, BattleClientConnection a, BattleClientConnection b,
        PlayerId first, int firstGold, int secondGold)
    {
        var expectedA = first == PlayerId.Player ? firstGold : secondGold;
        var expectedB = first == PlayerId.AI ? firstGold : secondGold;
        foreach (var connection in new[] { a, b })
        {
            var view = session.CreateStateViewEnvelope(connection).StateView!;
            var isA = connection.AssignedSeatId == PlayerIdDto.Player;
            Require(view.Player.Resources.Gold == (isA ? expectedA : expectedB), "Own wire Gold mismatch.");
            Require(view.Opponent.Resources.Gold == (isA ? expectedB : expectedA), "Opponent wire Gold mismatch.");
            var own = State(session).GetPlayer(isA ? PlayerId.Player : PlayerId.AI);
            var hidden = State(session).GetOpponent(own.Id);
            Require(view.Player.HandCount == own.Hand.Count && view.Player.DeckCount == own.Deck.Count &&
                view.Player.VisibleHandCardIds.SequenceEqual(own.Hand.CardIds) &&
                view.Player.VisibleHandCards.Select(card => card.RuntimeId).SequenceEqual(own.Hand.Cards.Select(card => card.RuntimeId)),
                "Own wire hand identities/counts changed.");
            Require(view.Opponent.HandCount == hidden.Hand.Count && view.Opponent.DeckCount == hidden.Deck.Count &&
                view.Opponent.VisibleHandCardIds.Count == 0 && view.Opponent.VisibleHandCards.Count == 0,
                "Opponent hand identities leaked or hidden counts changed.");
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            Require(projected.Player.Hand.CardIds.SequenceEqual(own.Hand.CardIds) &&
                projected.Player.Hand.Cards.Select(card => card.RuntimeId).SequenceEqual(own.Hand.Cards.Select(card => card.RuntimeId)) &&
                projected.AI.Hand.Count == hidden.Hand.Count &&
                projected.AI.Hand.CardIds.All(cardId => cardId.StartsWith("hidden_", StringComparison.Ordinal)),
                "Client projection changed own identities or revealed hidden hand.");
            Require(projected.Player.Resources.Gold == view.Player.Resources.Gold &&
                projected.AI.Resources.Gold == view.Opponent.Resources.Gold, "Client projection changed Gold.");
        }
    }

    private static void CheckFullHandMulligan(PlayerId first)
    {
        using var socketA = new DeckCheckSocket();
        using var socketB = new DeckCheckSocket();
        var player = new BattleClientConnection("replace-a", socketA) { PlayerToken = "a" };
        var opponent = new BattleClientConnection("replace-b", socketB) { PlayerToken = "b" };
        player.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        opponent.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        var session = new BattleSession("replace-five-" + first, false,
            new RandomFirstPlayerSelector(new FixedFirstRandom(first)));
        Require(session.TryAddConnection(player, out _) && session.TryAddConnection(opponent, out _) &&
            session.TryStartBattleIfReady(out _), "Replacement battle did not start.");
        session = Restore(session);
        var original = State(session).Player.Hand.Cards.Select(card => card.RuntimeId).ToArray();
        var invalid = Command(PlayerId.Player, OnlineBattleCommandType.ApplyMulligan);
        invalid.SelectedCardIds.AddRange(Enumerable.Repeat("Goblin", 6));
        var beforeInvalid = StateFingerprint(session);
        RequireRejected(session, invalid);
        Require(StateFingerprint(session) == beforeInvalid, "An invalid sixth selection changed the five-card hand.");
        var replace = Command(PlayerId.Player, OnlineBattleCommandType.ApplyMulligan);
        replace.SelectedCardIds.AddRange(State(session).Player.Hand.CardIds);
        session.ApplyCommand(replace);
        Require(State(session).Phase == PhaseType.Mulligan && State(session).Player.HasUsedMulligan &&
            !State(session).AI.HasUsedMulligan && State(session).Player.Hand.Count == 5 &&
            State(session).Player.Deck.Count == 28 &&
            State(session).Player.Hand.Cards.All(card => !original.Contains(card.RuntimeId)),
            "Replacing all five cards did not preserve deck/hand counts or replace actual copies.");
        CheckViews(session, player, opponent, first, 3, 4);
        session = Restore(session);
        var beforeRepeat = StateFingerprint(session);
        RequireRejected(session, replace);
        Require(StateFingerprint(session) == beforeRepeat, "A repeated mulligan changed recovered state.");
        var replaceOpponent = Command(PlayerId.AI, OnlineBattleCommandType.ApplyMulligan);
        var originalOpponent = State(session).AI.Hand.Cards.Select(card => card.RuntimeId).ToArray();
        replaceOpponent.SelectedCardIds.AddRange(State(session).AI.Hand.CardIds);
        session.ApplyCommand(replaceOpponent);
        var state = State(session);
        Require(state.Phase == PhaseType.Main && state.TurnNumber == 1 &&
            state.GetPlayer(first).Hand.Count == 6 && state.GetOpponent(first).Hand.Count == 5 &&
            state.GetPlayer(first).Deck.Count == 27 && state.GetOpponent(first).Deck.Count == 28 &&
            state.AI.Hand.Cards.All(card => !originalOpponent.Contains(card.RuntimeId)),
            "Both full-hand mulligans did not produce a five-card result plus one normal first-turn draw.");
        CheckViews(session, player, opponent, first, 3, 4);
        session = Restore(session);
        CheckViews(session, player, opponent, first, 3, 4);
    }

    private static void CheckMulliganTimeout(PlayerId first)
    {
        using var socketA = new DeckCheckSocket();
        using var socketB = new DeckCheckSocket();
        var player = new BattleClientConnection("timeout-a", socketA) { PlayerToken = "a" };
        var opponent = new BattleClientConnection("timeout-b", socketB) { PlayerToken = "b" };
        player.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        opponent.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        var session = new BattleSession("timeout-five-" + first, false,
            new RandomFirstPlayerSelector(new FixedFirstRandom(first)));
        Require(session.TryAddConnection(player, out _) && session.TryAddConnection(opponent, out _) &&
            session.TryStartBattleIfReady(out _), "Timeout battle did not start.");
        session = Restore(session);
        typeof(BattleSession).GetField("_turnTimerDeadlineUtc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DateTimeOffset.UtcNow.AddSeconds(-1));
        Require(session.TryGetCurrentTurnTimer(out var active, out var version, out var deadline), "Missing mulligan timer.");
        Require(session.TryCreateTurnTimeoutEnvelope(active, version, deadline, out var timeout) && timeout != null,
            "Expired mulligan was not auto-confirmed.");
        var state = State(session);
        Require(state.Phase == PhaseType.Main && state.TurnNumber == 1 &&
            state.Player.HasUsedMulligan && state.AI.HasUsedMulligan &&
            state.GetPlayer(first).Hand.Count == 6 && state.GetOpponent(first).Hand.Count == 5,
            "Timeout did not keep five opening cards and one normal first-turn draw.");
        CheckViews(session, player, opponent, first, 3, 4);
        Require(!session.TryCreateTurnTimeoutEnvelope(active, version, deadline, out _), "Stale timeout ran twice.");
    }

    private static string StateFingerprint(BattleSession session)
    {
        var state = State(session);
        return JsonSerializer.Serialize(new
        {
            state.TurnNumber, state.ActivePlayerId, state.Phase,
            Players = new[] { state.Player, state.AI }.Select(player => new
            {
                player.Id, player.Resources.Mana, player.Resources.Qi, player.Resources.Power, player.Resources.Gold,
                player.HasUsedMulligan, player.Deck.CardIds,
                Hand = player.Hand.Cards.Select(card => new { card.CardId, card.RuntimeId, card.IsTemporaryReplicate })
            })
        });
    }

    private static void RequireRejected(BattleSession session, ClientBattleCommandMessage command)
    {
        try { session.ApplyCommand(command); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Invalid or repeated mulligan command was accepted.");
    }

    private static BattleSession Restore(BattleSession session)
    {
        var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
            JsonSerializer.Serialize(session.CreatePersistenceSnapshot("opening-gold-check")))!;
        var beforeState = StateFingerprint(session);
        var restored = new BattleSession(session.MatchId, false);
        Require(restored.RestoreFromPersistenceSnapshot(saved), "JSON recovery failed.");
        Require(StateFingerprint(restored) == beforeState, "JSON recovery changed hand/deck identities, resources or mulligan completion.");
        return restored;
    }

    private static ClientBattleCommandMessage Command(PlayerId actor, OnlineBattleCommandType type) => new()
    {
        ActorId = actor == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI,
        CommandType = type
    };
    private static BattleState State(BattleSession session) =>
        ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!).CurrentBattleState!;
    private static void Require(bool success, string message)
    {
        if (!success) throw new Exception(message);
    }
    private sealed class FixedFirstRandom(PlayerId first) : Random
    {
        public override int Next(int maxValue) => first == PlayerId.Player ? 0 : 1;
    }
}
