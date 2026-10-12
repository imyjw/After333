using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

// Command/event/persistence checks only; no listener, database or operating accounts.
internal static class TimedBombChecks
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            CheckCountdownAndRestoration(provider, owner);
            CheckInterleavedDetonations(provider, owner);
            CheckMixedAreaEffects(provider, owner, false);
            CheckMixedAreaEffects(provider, owner, true);
            CheckLethalStopsLaterBomb(provider, owner);
        }
        Console.WriteLine("PASS TimedBomb: both casters, 3-2-1-0 global turn starts, stored upgrade damage, exact wire counts/caster/identity, multiple-bomb/mixed-fallout-area damage order, lethal stop, full JSON/both views and no transient replay.");
    }

    private static void CheckCountdownAndRestoration(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner);
        var cast = Cast(fixture.Session, fixture.State, owner);
        Require(cast.Count(e => e.EventType == BattleEventType.SpellCast) == 1 &&
            !cast.Any(e => e.EventType == BattleEventType.TimedBombCountdown), "Cast must display 3 without an immediate countdown tick.");
        var first = EndTurn(fixture.Session, owner);
        CheckTick(first.Single(e => e.EventType == BattleEventType.TimedBombCountdown), owner, 1, 3, 2);
        Require(!first.Any(e => e.EventType == BattleEventType.DamageApplied && e.SourceCardId == TimedBombRules.CardId),
            "First countdown dealt damage.");
        Require(fixture.State.TimedBombCountdownEvents.Count == 0, "Server failed to consume its transient countdown events.");
        var snapshot = fixture.Session.CreatePersistenceSnapshot("timed-bomb-check");
        foreach (var view in new[] { snapshot.PlayerView!, snapshot.AiView! })
            Require(view.PersistentEffects.Count == 2 && view.PersistentEffects[0].IsExpired &&
                view.PersistentEffects[1].RemainingTriggers == 2 && view.PersistentEffects[1].OwnerId == owner,
                "Both viewer snapshots must preserve the expired slot and countdown/caster.");
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
            JsonSerializer.Serialize(snapshot))!), "Full TimedBomb session JSON restoration failed.");
        var restored = GetFlow(restoredSession).CurrentBattleState;
        Require(restored.PersistentEffects.Count == 2 && restored.PersistentEffects[0].IsExpired &&
            restored.PersistentEffects[1].RemainingTriggers == 2 && restored.TimedBombCountdownEvents.Count == 0,
            "Restoration lost stable effect indices or replayed a transient countdown.");
        var second = EndTurn(restoredSession, restored.ActivePlayerId);
        CheckTick(second.Single(e => e.EventType == BattleEventType.TimedBombCountdown), owner, 1, 2, 1);
        var hpBefore = restored.GetOpponent(owner).Master.CurrentHp;
        var third = EndTurn(restoredSession, restored.ActivePlayerId);
        CheckTick(third.Single(e => e.EventType == BattleEventType.TimedBombCountdown), owner, 1, 1, 0);
        var tickIndex = third.ToList().FindIndex(e => e.EventType == BattleEventType.TimedBombCountdown);
        var damageIndex = third.ToList().FindIndex(e => e.EventType == BattleEventType.DamageApplied && e.SourceCardId == TimedBombRules.CardId);
        Require(damageIndex > tickIndex && restored.GetOpponent(owner).Master.CurrentHp == hpBefore - 38 &&
            restored.PersistentEffects[1].IsExpired, "Detonation order or stored level-five physical damage changed after recovery.");
        var repeated = new List<BattleEventDto>();
        typeof(BattleSession).GetMethod("AppendValuePopupEvents", PrivateInstance)!.Invoke(restoredSession,
            new object[] { repeated, ToDto(owner) });
        Require(repeated.Count == 0, "Consumed countdown/damage events were emitted twice.");
    }

    private static void CheckInterleavedDetonations(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner);
        Cast(fixture.Session, fixture.State, owner); Cast(fixture.Session, fixture.State, owner);
        fixture.State.PersistentEffects[1].RestoreRuntimeState(0, false, 1);
        fixture.State.PersistentEffects[2].RestoreRuntimeState(0, false, 1);
        var enemy = fixture.State.GetOpponent(owner).Id;
        var target = new UnitState("target", "Goblin", enemy, new TileCoord(0, 0), AttackType.Melee, 1, 200, true, false, 0);
        fixture.State.GetBoard(enemy).Place(target.Position, target);
        var events = EndTurn(fixture.Session, owner).Where(e => e.EventType == BattleEventType.TimedBombCountdown ||
            (e.EventType == BattleEventType.DamageApplied && e.SourceCardId == TimedBombRules.CardId)).ToArray();
        Require(events.Length == 6, "Two bombs must each emit one countdown and two target damage packets.");
        CheckTick(events[0], owner, 1, 1, 0);
        Require(events[1].EventType == BattleEventType.DamageApplied && events[2].EventType == BattleEventType.DamageApplied,
            "First bomb damage must remain between its own cue and the next bomb's cue.");
        CheckTick(events[3], owner, 2, 1, 0);
        Require(events[4].EventType == BattleEventType.DamageApplied && events[5].EventType == BattleEventType.DamageApplied &&
            target.CurrentHp == 124, "Second bomb packet order or independent damage changed.");
    }

    private static void CheckMixedAreaEffects(ICardDefinitionProvider provider, PlayerId owner, bool detonate)
    {
        var fixture = CreateFixture(provider, owner);
        // Areas are older than the bomb, but the scripted countdown phase still resolves first.
        fixture.State.PersistentEffects.Add(new PersistentEffectState("Firewall", owner, "firewall",
            fixture.State.TurnNumber, "", new ResourceSet(), targetRow: 0, remainingTriggers: 2,
            effectDamage: 10, effectDamageType: DamageType.Magic));
        fixture.State.PersistentEffects.Add(new PersistentEffectState(BiochemicalBombRules.CardId, owner,
            BiochemicalBombRules.EffectId, fixture.State.TurnNumber, "", new ResourceSet(),
            remainingTriggers: 2, effectDamage: 25, effectDamageType: DamageType.Fixed,
            targetStartColumn: BiochemicalBombRules.LeftAreaStartColumn));
        Cast(fixture.Session, fixture.State, owner);
        if (detonate) fixture.State.PersistentEffects[3].RestoreRuntimeState(0, false, 1);
        var enemy = fixture.State.GetOpponent(owner).Id;
        var target = new UnitState("mixed-target", "Goblin", enemy, new TileCoord(0, 0), AttackType.Melee,
            1, 200, true, false, 0);
        fixture.State.GetBoard(enemy).Place(target.Position, target);
        fixture.State.GetBoard(enemy).AddFalloutStack();
        var events = EndTurn(fixture.Session, owner).Where(e => e.EventType == BattleEventType.TimedBombCountdown ||
            e.EventType == BattleEventType.AreaSpellEffectTriggered ||
            (e.EventType == BattleEventType.DamageApplied && new[] { TimedBombRules.CardId, NuclearRules.CardId,
                "Firewall", BiochemicalBombRules.CardId }.Contains(e.SourceCardId))).ToArray();
        CheckTick(events[0], owner, 3, detonate ? 1 : 3, detonate ? 0 : 2);
        var expected = new List<string> { "countdown" };
        if (detonate) expected.AddRange(new[] { "damage:TimedBomb", "damage:TimedBomb" });
        expected.AddRange(new[] { "damage:Nuclear", "damage:Nuclear", "area:firewall", "damage:Firewall",
            "area:biochemical_bomb", "damage:BiochemicalBomb", "damage:BiochemicalBomb" });
        var actual = events.Select(e => e.EventType == BattleEventType.TimedBombCountdown ? "countdown" :
            e.EventType == BattleEventType.AreaSpellEffectTriggered ? "area:" + e.EffectId : "damage:" + e.SourceCardId);
        Require(actual.SequenceEqual(expected), "Mixed effects lost countdown/bomb damage/fallout/area cue-damage order: " +
            string.Join(", ", actual));
        Require(target.CurrentHp == 200 - (detonate ? 38 : 0) - 10 - 10 - 25 &&
            fixture.State.GetOpponent(owner).Master.CurrentHp == 333 - (detonate ? 38 : 0) - 10 - 25,
            "Mixed presentation events changed battle damage.");
        Require(events.Where(e => e.EventType == BattleEventType.AreaSpellEffectTriggered).All(e =>
            e.SourceOwnerId == ToDto(owner) && e.TargetOwnerId == ToDto(enemy)), "Mixed area cue lost caster/target seat.");
        Require(fixture.State.TimedBombCountdownEvents.Count == 0 && fixture.State.AreaSpellEffectEvents.Count == 0 &&
            fixture.State.ValuePopupEvents.Count == 0, "Mixed turn retained transient presentation events.");
    }

    private static void CheckLethalStopsLaterBomb(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner);
        Cast(fixture.Session, fixture.State, owner); Cast(fixture.Session, fixture.State, owner);
        fixture.State.PersistentEffects[1].RestoreRuntimeState(0, false, 1);
        fixture.State.GetOpponent(owner).Master.CurrentHp = 20;
        var events = EndTurn(fixture.Session, owner);
        Require(events.Count(e => e.EventType == BattleEventType.TimedBombCountdown) == 1 &&
            events.Single(e => e.EventType == BattleEventType.TimedBombCountdown).EffectIndex == 1 &&
            fixture.State.PersistentEffects[2].RemainingTriggers == 3 && fixture.State.Result.Winner == owner,
            "A lethal first bomb must not count down or animate a later unresolved bomb.");
    }

    private static void CheckTick(BattleEventDto tick, PlayerId owner, int index, int before, int after)
    {
        Require(tick.EventType == BattleEventType.TimedBombCountdown && tick.EffectIndex == index &&
            tick.RuntimeId == TimedBombRules.EffectId + ":" + index && tick.CardId == TimedBombRules.CardId &&
            tick.SourceCardId == TimedBombRules.CardId && tick.EffectId == TimedBombRules.EffectId &&
            tick.SourceOwnerId == ToDto(owner) && tick.TargetOwnerId == ToDto(owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player) &&
            tick.RemainingTriggersBefore == before && tick.RemainingTriggersAfter == after,
            "TimedBomb countdown lost typed counts, stable identity or actual caster/target seat.");
        var roundTrip = JsonSerializer.Deserialize<BattleEventDto>(JsonSerializer.Serialize(tick))!;
        Require(roundTrip.EffectIndex == index && roundTrip.RemainingTriggersBefore == before &&
            roundTrip.RemainingTriggersAfter == after, "Countdown fields did not survive wire JSON.");
    }

    private static (BattleSession Session, BattleState State) CreateFixture(ICardDefinitionProvider provider, PlayerId owner)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var old = new PersistentEffectState("old", owner, "old", 0, "", new ResourceSet());
        old.Expire(); state.PersistentEffects.Add(old);
        var upgrades = new InMemoryCardUpgradeLevelProvider(); upgrades.SetUpgradeLevel(owner, TimedBombRules.CardId, 5);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, upgrades); flow.RestoreBattleState(state);
        var session = new BattleSession("timed-bomb-check-" + owner, false);
        typeof(BattleSession).GetField("_battleFlowController", PrivateInstance)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", PrivateInstance)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", PrivateInstance)!.SetValue(session, upgrades);
        return (session, state);
    }

    private static IReadOnlyList<BattleEventDto> Cast(BattleSession session, BattleState state, PlayerId owner)
    {
        var card = state.GetPlayer(owner).Hand.Add(TimedBombRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 3, 0));
        return session.ApplyCommand(new ClientBattleCommandMessage { ActorId = ToDto(owner),
            CommandType = OnlineBattleCommandType.CastScriptedSpell, CardId = TimedBombRules.CardId, HandCardRuntimeId = card.RuntimeId });
    }

    private static IReadOnlyList<BattleEventDto> EndTurn(BattleSession session, PlayerId owner) => session.ApplyCommand(
        new ClientBattleCommandMessage { ActorId = ToDto(owner), CommandType = OnlineBattleCommandType.EndTurn });
    private static BattleFlowController GetFlow(BattleSession session) =>
        (BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", PrivateInstance)!.GetValue(session)!;
    private static PlayerIdDto ToDto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
