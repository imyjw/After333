using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;

static class TurnInputChecks
{
    public static void Run(Action<string> pass)
    {
        foreach (var pve in new[] { false, true })
        {
            var session = new BattleSession("turn-input-" + Guid.NewGuid().ToString("N"), pve);
            var first = Connection(); var second = Connection();
            Require(session.TryAddConnection(first, out var error), error);
            if (!pve) Require(session.TryAddConnection(second, out error), error);
            Require(session.TryStartBattleIfReady(out error), error);
            session.ApplyCommand(new ClientBattleCommandMessage { ActorId = first.AssignedSeatId,
                CommandType = OnlineBattleCommandType.PassMulligan });
            if (!pve) session.ApplyCommand(new ClientBattleCommandMessage { ActorId = second.AssignedSeatId,
                CommandType = OnlineBattleCommandType.PassMulligan });
            var view = session.CreateStateViewEnvelope(first).StateView!;
            var oldCommand = new ClientBattleCommandMessage { ActorId = (PlayerIdDto)(int)view.ActivePlayerId,
                CommandType = OnlineBattleCommandType.EndTurn, ExpectedTurnNumber = view.TurnNumber };
            session.ApplyCommand(oldCommand);
            view = session.CreateStateViewEnvelope(first).StateView!;
            session.ApplyCommand(new ClientBattleCommandMessage { ActorId = (PlayerIdDto)(int)view.ActivePlayerId,
                CommandType = OnlineBattleCommandType.EndTurn, ExpectedTurnNumber = view.TurnNumber });
            view = session.CreateStateViewEnvelope(first).StateView!;
            Require((int)view.ActivePlayerId == (int)oldCommand.ActorId, "Must return to the original actor's next turn.");
            var before = JsonSerializer.Serialize(session.CreatePersistenceSnapshot("before").DomainState);
            Reject(() => session.ApplyCommand(oldCommand));
            Reject(() => session.ApplyCommand(new ClientBattleCommandMessage { ActorId = oldCommand.ActorId,
                CommandType = OnlineBattleCommandType.Attack, ExpectedTurnNumber = oldCommand.ExpectedTurnNumber,
                SourceCoord = new TileCoordDto { Column = 2, Row = 1 }, TargetCoord = new TileCoordDto { Column = 2, Row = 1 } }));
            Require(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("after").DomainState) == before,
                "Stale EndTurn and attack must not mutate the current turn or resources.");
            oldCommand.ExpectedTurnNumber = view.TurnNumber;
            session.ApplyCommand(oldCommand);
            Require(session.CreateStateViewEnvelope(first).StateView!.TurnNumber == view.TurnNumber + 1,
                "Fresh EndTurn must still work.");
            pass($"{(pve ? "PvE" : "PvP")}: delayed previous-turn EndTurn/attack rejected without mutation; fresh command accepted");
        }
    }
    private static BattleClientConnection Connection()
    {
        var connection = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
        { AccountId = Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N"),
            RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString() };
        connection.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        return connection;
    }
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("이전 턴")) { return; }
        throw new Exception("Expected stale turn rejection.");
    }
}
