using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using ServerEvent = Project333.PvpServer.Messages.BattleEventDto;
using ServerEventType = Project333.PvpServer.Messages.BattleEventType;
using ServerCommand = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using ServerCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;
using ServerPlayer = Project333.PvpServer.Messages.PlayerIdDto;
using ServerCoord = Project333.PvpServer.Messages.TileCoordDto;

// Uses the actual shared services and session wrapper without any socket, DB or live match.
internal static class ActionDebuffOnlineChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo CaptureOccupant = typeof(BattleSession).GetMethod("CreateOccupantSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo RestoreOccupant = typeof(BattleSession).GetMethod("RestoreOccupant", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo CaptureBattle = typeof(BattleSession).GetMethod("CaptureBattleSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo AppendHistory = typeof(BattleSession).GetMethod("AppendCombatLogEntries", Hidden)!;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            CheckOccupantJson(provider, owner);
            CheckWireProjection(provider, owner);
            CheckSessionRecoveryAndGlobalEnds(provider, owner);
            CheckChangeEventsAndHistory(provider, owner);
            CheckRejectedCommands(provider, owner);
            CheckTimeout(provider, owner);
        }
        CheckForcedAiTurnEnd(provider);
        Console.WriteLine("PASS Stun/Freeze online: both seats; Unit/Building/Master JSON; opposite-viewer projector; legacy/negative/pending-revival fields; global ends; max refresh; apply/decrease/clear wire events and history; rejected-command atomicity; timeout and forced AI.");
    }

    private static void CheckOccupantJson(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner);
        foreach (var source in Occupants(fixture.State))
        {
            source.ApplyStun(3); source.ApplyFreeze(2);
            var saved = Snapshot(source);
            var received = JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(JsonSerializer.Serialize(saved))!;
            var restored = Restore(received, provider);
            Require(restored.Kind == source.Kind && restored.RuntimeId == source.RuntimeId && restored.OwnerId == source.OwnerId &&
                restored.BaseAttack == source.BaseAttack && restored.CurrentHp == source.CurrentHp,
                "Action-debuff snapshot changed identity, printed combat data or health.");
            CheckCounters(restored, 3, 2, "occupant JSON");
            var legacy = JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(WithoutActionFields(JsonSerializer.Serialize(saved)))!;
            CheckCounters(Restore(legacy, provider), 0, 0, "legacy occupant JSON");
            CheckCounters(Restore(saved with { StunTurnEndsRemaining = -3, FreezeTurnEndsRemaining = -2 }, provider), 0, 0, "invalid negative counters");
            if (source.Kind != OccupantKind.Master)
            {
                source.EnterSealbound(2);
                CheckCounters(Restore(Snapshot(source), provider), 3, 2, "ordinary Sealbound overlap");
            }
        }
        var pending = Snapshot(fixture.Unit) with { CardId = DemonKingRules.CardId, CurrentHp = 0,
            IsDemonKingRevivalPending = true, DemonKingRevivalTurnStartsRemaining = 3,
            DemonKingRevivalCountdownPlayerId = owner.ToString(), StunTurnEndsRemaining = 5, FreezeTurnEndsRemaining = 4 };
        var pendingRestored = Restore(pending, provider);
        Require(pendingRestored.IsDemonKingRevivalPending, "Pending revival was not restored.");
        CheckCounters(pendingRestored, 0, 0, "pending revival must not revive stale action debuffs");
    }

    private static void CheckWireProjection(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner);
        foreach (var source in Occupants(fixture.State)) { source.ApplyStun(4); source.ApplyFreeze(2); }
        fixture.State.GetPlayer(owner).Hand.Add("Goblin");
        foreach (var viewer in new[] { PlayerId.Player, PlayerId.AI })
        {
            var view = new BattleStateViewFactory().CreateForPlayer(fixture.State, "control-wire", viewer);
            var json = JsonSerializer.Serialize(view);
            var received = JsonSerializer.Deserialize<BattleStateViewDto>(json)!;
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(received);
            foreach (var source in Occupants(fixture.State))
            {
                var local = Occupants(projected).Single(occupant => occupant.RuntimeId == source.RuntimeId);
                Require(local.OwnerId == (source.OwnerId == viewer ? PlayerId.Player : PlayerId.AI) && local.Kind == source.Kind,
                    "Opposite-viewer projection changed owner or occupant kind.");
                CheckCounters(local, 4, 2, "wire projector");
                Require(local.CannotAttack && local.CannotCounterattack && local.CannotActivate && local.CannotMoveDueToState &&
                    local.DoesNotBlockFrontRow && !local.EffectsSuppressed,
                    "Projected action debuffs lost restrictions or suppressed unrelated effects.");
            }
            Require(received.Opponent.VisibleHandCardIds.Count == 0, "Action state exposed the opponent's hand.");
            var legacy = JsonSerializer.Deserialize<BattleStateViewDto>(WithoutActionFields(json))!;
            foreach (var local in Occupants(BattleStateViewProjector.CreateLocalPerspectiveState(legacy)))
                CheckCounters(local, 0, 0, "legacy wire defaults");
            foreach (var row in received.Occupants) { row.StunTurnEndsRemaining = -4; row.FreezeTurnEndsRemaining = -2; }
            foreach (var local in Occupants(BattleStateViewProjector.CreateLocalPerspectiveState(received)))
                CheckCounters(local, 0, 0, "negative wire defaults");
        }
    }

    private static void CheckSessionRecoveryAndGlobalEnds(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner);
        foreach (var source in Occupants(fixture.State)) { source.ApplyStun(2); source.ApplyFreeze(1); }
        var saved = fixture.Session.CreatePersistenceSnapshot("control-recovery");
        var restoredSession = Recover(saved); var restoredState = State(restoredSession);
        foreach (var source in Occupants(restoredState)) CheckCounters(source, 2, 1, "full session JSON restore");
        var originalKey = AiBattleStateCopy.PositionKey(restoredState);
        var copy = AiBattleStateCopy.Create(restoredState);
        foreach (var source in Occupants(copy)) CheckCounters(source, 2, 1, "AI clone after recovery");
        new EndTurnService().EndTurn(copy);
        Require(AiBattleStateCopy.PositionKey(restoredState) == originalKey, "AI simulation changed recovered live timers.");
        var first = EndTurn(restoredSession, owner, restoredState.TurnNumber);
        foreach (var source in Occupants(restoredState))
        {
            CheckCounters(source, 1, 0, "first global end");
            CheckEvent(first, source, "stun", 1); CheckEvent(first, source, "freeze", 0);
        }
        var nextOwner = restoredState.ActivePlayerId;
        var second = EndTurn(restoredSession, nextOwner, restoredState.TurnNumber);
        foreach (var source in Occupants(restoredState))
        {
            CheckCounters(source, 0, 0, "opponent global end");
            CheckEvent(second, source, "stun", 0);
            Require(!second.Any(e => e.EffectId == "freeze" && e.TargetRuntimeId == source.RuntimeId), "Expired freeze emitted a repeated clear.");
        }
        var logs = restoredSession.CreatePersistenceSnapshot("control-history").CombatLogEntries!;
        Require(logs.Count(log => log.EntryType == BattleCombatLogEntryType.FreezeCleared) == Occupants(restoredState).Count() &&
            logs.Count(log => log.EntryType == BattleCombatLogEntryType.StunCleared) == Occupants(restoredState).Count(),
            "Global expiration lost or duplicated combat-log clears.");
        var legacySaved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(WithoutActionFields(JsonSerializer.Serialize(saved)))!;
        foreach (var source in Occupants(State(Recover(legacySaved)))) CheckCounters(source, 0, 0, "legacy full session restore");
    }

    private static void CheckChangeEventsAndHistory(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner);
        IReadOnlyList<ServerEvent> Record(Action change)
        {
            var before = CaptureBattle.Invoke(null, new object[] { fixture.State })!;
            change();
            var after = CaptureBattle.Invoke(null, new object[] { fixture.State })!;
            var events = new List<ServerEvent>();
            AppendHistory.Invoke(fixture.Session, new[] { events, before, after });
            return events;
        }
        var applied = Record(() => { fixture.Unit.ApplyStun(3); fixture.Unit.ApplyFreeze(2); });
        CheckEvent(applied, fixture.Unit, "stun", 3); CheckEvent(applied, fixture.Unit, "freeze", 2);
        Require(Record(() => { fixture.Unit.ApplyStun(1); fixture.Unit.ApplyFreeze(1); }).Count == 0,
            "A shorter max reapplication emitted a false state change.");
        var refreshed = Record(() => fixture.Unit.ApplyStun(5));
        CheckEvent(refreshed, fixture.Unit, "stun", 5);
        Require(refreshed.Count == 1, "Max duration refresh emitted unrelated state changes.");
        var cleared = Record(() => fixture.Unit.ApplyErasure());
        CheckEvent(cleared, fixture.Unit, "stun", 0); CheckEvent(cleared, fixture.Unit, "freeze", 0);
        var logs = fixture.Session.CreatePersistenceSnapshot("control-apply-clear").CombatLogEntries!;
        Require(logs.Count(log => log.EntryType == BattleCombatLogEntryType.StunApplied) == 1 &&
            logs.Count(log => log.EntryType == BattleCombatLogEntryType.FreezeApplied) == 1 &&
            logs.Count(log => log.EntryType == BattleCombatLogEntryType.StunCleared) == 1 &&
            logs.Count(log => log.EntryType == BattleCombatLogEntryType.FreezeCleared) == 1,
            "Application/clear history duplicated max refresh or omitted a transition.");
        var recovered = Recover(fixture.Session.CreatePersistenceSnapshot("control-history-recovery"));
        Require(recovered.CreatePersistenceSnapshot("resaved").CombatLogEntries!.Count == logs.Count,
            "Reconnect replayed or discarded action-debuff combat history.");
    }

    private static void CheckRejectedCommands(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner);
        fixture.Unit.ApplyStun(2); fixture.Plant.ApplyFreeze(2);
        var target = new UnitState("control-target", "Goblin", fixture.State.GetOpponent(owner).Id,
            new TileCoord(0, 0), AttackType.Melee, 5, 30, true, false, 0);
        fixture.State.GetOpponentBoard(owner).Place(target.Position, target);
        fixture.State.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3));
        var key = AiBattleStateCopy.PositionKey(fixture.State);
        Reject(() => fixture.Session.ApplyCommand(new ServerCommand { ActorId = ToDto(owner), CommandType = ServerCommandType.Attack,
            ExpectedTurnNumber = fixture.State.TurnNumber, SourceCoord = Coord(fixture.Unit.Position), TargetCoord = Coord(target.Position),
            TargetOwnerId = ToDto(target.OwnerId), OccupantRuntimeId = fixture.Unit.RuntimeId }));
        Reject(() => fixture.Session.ApplyCommand(new ServerCommand { ActorId = ToDto(owner), CommandType = ServerCommandType.ActivateEffect,
            ExpectedTurnNumber = fixture.State.TurnNumber, SourceCoord = Coord(fixture.Plant.Position), OccupantRuntimeId = fixture.Plant.RuntimeId }));
        Require(AiBattleStateCopy.PositionKey(fixture.State) == key, "Rejected debuffed command consumed resources, attacks or timers.");
        Reject(() => EndTurn(fixture.Session, fixture.State.GetOpponent(owner).Id, fixture.State.TurnNumber));
        Reject(() => EndTurn(fixture.Session, owner, fixture.State.TurnNumber + 1));
        Require(AiBattleStateCopy.PositionKey(fixture.State) == key, "Wrong-owner/stale end command consumed a global duration.");
    }

    private static void CheckTimeout(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner); fixture.Unit.ApplyStun(2); fixture.Unit.ApplyFreeze(1);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(-1);
        typeof(BattleSession).GetField("_turnTimerVersion", Hidden)!.SetValue(fixture.Session, 7L);
        typeof(BattleSession).GetField("_turnTimerDeadlineUtc", Hidden)!.SetValue(fixture.Session, deadline);
        Require(fixture.Session.TryCreateTurnTimeoutEnvelope(ToDto(owner), 7, deadline, out var envelope) && envelope != null,
            "Actual session timeout did not end the observed main turn.");
        CheckCounters(fixture.Unit, 1, 0, "timeout global end");
        CheckEvent(envelope!.BattleEvents, fixture.Unit, "stun", 1); CheckEvent(envelope.BattleEvents, fixture.Unit, "freeze", 0);
        Require(!fixture.Session.TryCreateTurnTimeoutEnvelope(ToDto(owner), 7, deadline, out _), "Old timeout ended the new turn again.");
        CheckCounters(fixture.Unit, 1, 0, "duplicate timeout");
    }

    private static void CheckForcedAiTurnEnd(ICardDefinitionProvider provider)
    {
        var fixture = Fixture(provider, PlayerId.AI, useAi: true); fixture.Unit.ApplyStun(2); fixture.Unit.ApplyFreeze(1);
        var events = fixture.Session.RunServerAiActionIfNeeded(forceEndTurn: true);
        CheckCounters(fixture.Unit, 1, 0, "forced AI global end");
        CheckEvent(events, fixture.Unit, "stun", 1); CheckEvent(events, fixture.Unit, "freeze", 0);
        Require(events.Any(e => e.EventType == ServerEventType.TurnEnded), "Forced AI did not use the common accepted end path.");
    }

    private static (BattleSession Session, BattleState State, UnitState Unit, BuildingState Plant) Fixture(
        ICardDefinitionProvider provider, PlayerId owner, bool useAi = false)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main);
        var unit = new UnitState("control-unit-" + owner, "Goblin", owner, new TileCoord(0, 0), AttackType.Melee, 17, 30, true, false, 0);
        var plant = new BuildingState("control-plant-" + owner, PowerPlantRules.CardId, owner, new TileCoord(1, 0), false, 0, 30);
        state.GetBoard(owner).Place(unit.Position, unit); state.GetBoard(owner).Place(plant.Position, plant);
        unit.HasSummoningSickness = false; unit.RemainingAttacksThisTurn = 1;
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider); flow.RestoreBattleState(state);
        var session = new BattleSession("control-offline-" + owner, useAi);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, new InMemoryCardUpgradeLevelProvider());
        if (useAi) typeof(BattleSession).GetField("_aiDecisionService", Hidden)!.SetValue(session, new AiDecisionService(provider));
        return (session, state, unit, plant);
    }

    private static IEnumerable<OccupantState> Occupants(BattleState state) =>
        state.PlayerBoard.EnumerateOccupants().Concat(state.AIBoard.EnumerateOccupants());
    private static BattleState State(BattleSession session) =>
        ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(session)!).CurrentBattleState;
    private static BattleSessionOccupantSnapshot Snapshot(OccupantState source) =>
        (BattleSessionOccupantSnapshot)CaptureOccupant.Invoke(null, new object[] { source })!;
    private static OccupantState Restore(BattleSessionOccupantSnapshot snapshot, ICardDefinitionProvider provider) =>
        (OccupantState)RestoreOccupant.Invoke(null, new object[] { snapshot, PlayerId.Player, provider })!;
    private static BattleSession Recover(BattleSessionPersistenceSnapshot snapshot)
    {
        var session = new BattleSession(snapshot.MatchId, snapshot.UseServerAiOpponent);
        Require(session.RestoreFromPersistenceSnapshot(JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!),
            "Full session JSON recovery failed.");
        return session;
    }
    private static IReadOnlyList<ServerEvent> EndTurn(BattleSession session, PlayerId owner, int turn) => session.ApplyCommand(
        new ServerCommand { ActorId = ToDto(owner), CommandType = ServerCommandType.EndTurn, ExpectedTurnNumber = turn });
    private static void CheckCounters(OccupantState source, int stun, int freeze, string context) => Require(
        source.StunTurnEndsRemaining == stun && source.FreezeTurnEndsRemaining == freeze &&
        source.IsStunned == (stun > 0) && source.IsFrozen == (freeze > 0), context + " lost action duration/derived flags.");
    private static void CheckEvent(IReadOnlyList<ServerEvent> events, OccupantState target, string effectId, int remaining)
    {
        var evt = events.Single(e => e.EventType == ServerEventType.StateChanged && e.EffectId == effectId && e.TargetRuntimeId == target.RuntimeId);
        Require(evt.Amount == remaining && evt.RuntimeId == target.RuntimeId && evt.CardId == target.CardId &&
            evt.TargetCardId == target.CardId && evt.TargetOwnerId == ToDto(target.OwnerId) && evt.TargetCoord?.Column == target.Position.Column &&
            evt.TargetCoord?.Row == target.Position.Row, "Action-debuff wire change lost count or target identity.");
        var received = JsonSerializer.Deserialize<ServerEvent>(JsonSerializer.Serialize(evt))!;
        Require(received.EffectId == effectId && received.Amount == remaining && received.TargetRuntimeId == target.RuntimeId,
            "Action-debuff event did not survive wire JSON.");
    }
    private static string WithoutActionFields(string json)
    {
        var node = JsonNode.Parse(json)!; Remove(node); return node.ToJsonString();
        static void Remove(JsonNode? current)
        {
            if (current is JsonObject obj)
            {
                obj.Remove("StunTurnEndsRemaining"); obj.Remove("FreezeTurnEndsRemaining");
                foreach (var property in obj) Remove(property.Value);
            }
            else if (current is JsonArray array) foreach (var child in array) Remove(child);
        }
    }
    private static ServerPlayer ToDto(PlayerId owner) => owner == PlayerId.Player ? ServerPlayer.Player : ServerPlayer.AI;
    private static ServerCoord Coord(TileCoord coord) => new() { Column = coord.Column, Row = coord.Row };
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("An illegal action-debuff command was accepted.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
