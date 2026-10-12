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
            Require(State(session).GetPlayer(first).Hand.Count == 4, "Opening draw must remain one card.");
            session = Restore(session);
            CheckViews(session, player, opponent, first, 3, 4);
            var second = State(session).GetOpponent(first).Id;
            session.ApplyCommand(Command(first, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 3, 4);
            Require(State(session).TurnNumber == 2 && State(session).GetPlayer(second).Hand.Count == 4,
                "Second opening draw changed.");
            session.ApplyCommand(Command(second, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 6, 4);
            session.ApplyCommand(Command(first, OnlineBattleCommandType.EndTurn));
            CheckViews(session, player, opponent, first, 6, 7);
            session = Restore(session);
            CheckViews(session, player, opponent, first, 6, 7);
        }
        Console.WriteLine("PASS Opening Gold: actual PvP setup, either first seat, both client views, mulligan, first four turns, one-card opening draws, repeat start and JSON recovery.");
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
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            Require(projected.Player.Resources.Gold == view.Player.Resources.Gold &&
                projected.AI.Resources.Gold == view.Opponent.Resources.Gold, "Client projection changed Gold.");
        }
    }

    private static BattleSession Restore(BattleSession session)
    {
        var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
            JsonSerializer.Serialize(session.CreatePersistenceSnapshot("opening-gold-check")))!;
        var restored = new BattleSession(session.MatchId, false);
        Require(restored.RestoreFromPersistenceSnapshot(saved), "JSON recovery failed.");
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
