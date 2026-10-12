using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Runs;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

internal static class MeteorChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var definition = (ScriptedSpellCardDefinition)provider.GetRequired(MeteorRules.CardId);
        Require(definition.Damage == 40 && definition.DamageType == DamageType.Magic &&
            definition.Cost.Mana == 7 && CardUpgradeRules.IsCardUpgradeable(definition), "Meteor canonical definition differs.");
        CheckReleasePools();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            for (var level = 0; level <= 13; level++) CheckCast(provider, owner, level);
            CheckProtectionAndChain(provider, owner);
            CheckRestore(provider, owner);
            CheckPendingRevivalEvents(provider, owner);
            CheckSpellPowerSuppression(provider, owner);
        }
        CheckActualServerAi(provider);
        Require(definition.Damage == 40, "An upgraded cast mutated the shared base definition.");
        Console.WriteLine("PASS Meteor: actual draft/reward filters, both seats Lv.0-13 Magic damage/current SpellPower/display/exact hand/mana, protections/area guards, actual server AI, pending revival feedback, destruction chains/outcome, participant upgrades/full JSON recovery.");
    }
    private static void CheckReleasePools()
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        var record = database.Cards.Single(c => c.Id == MeteorRules.CardId);
        Require(record.Rarity == CardRarity.Unique, "Meteor must be offered and rewarded in the Unique rarity pool.");
        var generator = new ServerDraftOfferGenerator(database); var offered = false;
        for (var seed = 0; seed < 512 && !offered; seed++)
        {
            var first = generator.CreateOffer(seed, Array.Empty<string>())[0];
            offered = generator.CreateOffer(seed, new[] { first }).Contains(MeteorRules.CardId);
        }
        Require(offered, "Actual post-Legendary draft offers never offered Meteor.");
        var rewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.Static | BindingFlags.NonPublic)!;
        Require((bool)rewardable.Invoke(null, new object[] { record })!, "Actual server reward filter rejected Meteor.");
        var generate = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.Static | BindingFlags.NonPublic)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var pool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)generate.Invoke(null, new object[] { run, pool })!;
        Require(rewards.Count == 1 && rewards[MeteorRules.CardId] == 3, "Actual one-win rewards did not grant three Meteor copies.");
    }
    private static void CheckCast(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var fixture = Fixture(provider, owner, level); var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var unit = Unit(enemy, 0, 500, defense: 4, hiding: true, flying: true);
        var building = Building(enemy, 1, 500, defense: 4);
        var ally = Unit(owner, 0, 500, spellPower: 70);
        var enemySource = Unit(enemy, 3, 500, spellPower: 100);
        foreach (var occupant in new OccupantState[] { unit, building, ally, enemySource }) Place(state, occupant);
        Amplify(fixture, owner);
        var unused = state.GetPlayer(owner).Hand.Add(MeteorRules.CardId);
        var chosen = state.GetPlayer(owner).Hand.AddTemporaryReplicate(MeteorRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0));
        var mana = state.GetPlayer(owner).Resources.Mana; var gold = state.GetPlayer(owner).Resources.Gold;
        var events = fixture.Session.ApplyCommand(Command(owner, chosen.RuntimeId)); var damage = 40 + level + 80;
        Require(unit.CurrentHp == 500 - (damage - 4) && building.CurrentHp == 500 - (damage - 4),
            $"{owner} Lv.{level}: Magic mitigation or current owner SpellPower differs; buildings must not take triple Meteor damage.");
        Require(state.GetPlayer(enemy).Master.CurrentHp == 333 - damage && ally.CurrentHp == 500 && state.GetPlayer(owner).Master.CurrentHp == 333,
            "Enemy Master damage or allied immunity differs.");
        Require(events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == damage,
            "Outbound display differs from upgraded Magic packet including live SpellPower.");
        var packets = events.Where(e => e.EventType == BattleEventType.DamageApplied).ToArray();
        Require(packets.Length == 4 && packets.All(e => e.DamageType == DamageType.Magic && e.ValueCause == BattleValueChangeCause.Spell &&
            e.SourceOwnerId == Dto(owner) && e.TargetOwnerId == Dto(enemy) && e.SourceCardId == MeteorRules.CardId),
            "Meteor damage events lost provenance/type/owner or duplicated packets.");
        Require(packets.Single(e => e.TargetRuntimeId == building.RuntimeId).Amount == damage - 4,
            "Building event differs from actual Magic HP loss.");
        Require(!state.GetPlayer(owner).Hand.Contains(MeteorRules.CardId, chosen.RuntimeId) && state.GetPlayer(owner).Hand.Contains(MeteorRules.CardId, unused.RuntimeId) &&
            state.GetPlayer(owner).Resources.Mana == mana - 7 && state.GetPlayer(owner).Resources.Gold == gold && state.GetPlayer(owner).Discard.CardIds.Contains(MeteorRules.CardId),
            "Server did not consume exact temporary card and exactly seven Mana.");
        Require(state.PersistentEffects.Count == 1 && state.PlayerBoard.FalloutStacks + state.AIBoard.FalloutStacks == 0,
            "Meteor created a persistent effect beyond the existing amplification.");
    }
    private static void CheckProtectionAndChain(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner, 0); var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var sealedUnit = Unit(enemy, 0, 8); sealedUnit.EnterSealbound(3);
        var invincible = Unit(enemy, 1, 8); invincible.AddInvincibleEffect(InvincibleDurationType.Always);
        var endure = Unit(enemy, 2, 8, endure: true);
        var drained = Building(enemy, 3, 500, defense: 99); drained.IsDrained = true;
        foreach (var occupant in new OccupantState[] { sealedUnit, invincible, endure, drained }) Place(state, occupant);
        var events = CastNew(fixture, owner);
        Require(sealedUnit.CurrentHp == 8 && !events.Any(e => e.TargetRuntimeId == sealedUnit.RuntimeId), "Sealbound received an area packet.");
        Require(invincible.CurrentHp == 8 && events.Count(e => e.EventType == BattleEventType.InvinciblePrevented && e.TargetRuntimeId == invincible.RuntimeId) == 1,
            "Invincible Magic protection feedback differs.");
        Require(endure.CurrentHp == 1 && endure.EndureUsed && drained.CurrentHp == 380, "Endure or Drained Magic damage differs.");
        fixture = Fixture(provider, owner, 0); state = fixture.State;
        var rear = Unit(enemy, 0, 100, hiding: true, flying: true, row: 1);
        var guard = Unit(enemy, 0, 100, shielder: true); Place(state, rear); Place(state, guard);
        events = CastNew(fixture, owner);
        Require(rear.CurrentHp == 60 && guard.CurrentHp == 60 && events.Count(e => e.TargetRuntimeId == rear.RuntimeId && e.EventType == BattleEventType.DamageApplied) == 1 &&
            events.Count(e => e.TargetRuntimeId == guard.RuntimeId && e.EventType == BattleEventType.DamageApplied) == 1,
            "Area Magic was redirected by Shielder or reduced by Flying/Hiding.");
        foreach (var enemyHp in new[] { 60, 70, 50 })
        {
            fixture = Fixture(provider, owner, 0); state = fixture.State;
            state.GetPlayer(owner).Master.CurrentHp = 20; state.GetPlayer(enemy).Master.CurrentHp = enemyHp;
            Place(state, Building(enemy, 0, 25, id: "NuclearPowerPlant")); Place(state, Building(enemy, 1, 25, id: "NuclearPowerPlant"));
            events = CastNew(fixture, owner);
            Require(state.Result.IsDraw == (enemyHp == 60) && state.GetPlayer(owner).Master.CurrentHp == -40 &&
                state.GetPlayer(enemy).Master.CurrentHp == enemyHp - 100 && (enemyHp == 60 || state.Result.Winner == (enemyHp > 60 ? enemy : owner)),
                "All area packets and reactor chains must resolve before both-Master final HP comparison.");
            var array = events.ToList();
            Require(array.FindLastIndex(e => e.EventType == BattleEventType.DamageApplied && e.SourceCardId == MeteorRules.CardId) <
                array.FindIndex(e => e.ValueCause == BattleValueChangeCause.NuclearPowerPlant), "Destruction chain preceded remaining Meteor packets.");
        }
        fixture = Fixture(provider, owner, 0); state = fixture.State; state.GetPlayer(enemy).Master.CurrentHp = 40;
        CastNew(fixture, owner); Require(state.Result.Winner == owner, "An empty field with lethal Master damage did not finish.");
    }
    private static void CheckPendingRevivalEvents(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner, 0, serverAi: owner == PlayerId.AI);
        var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var king = new UnitState("reviving-king", "DemonKing", enemy, new TileCoord(0, 0), AttackType.Melee, 1, 8, true, false, 0);
        var erased = new UnitState("erased-king", "DemonKing", enemy, new TileCoord(1, 0), AttackType.Melee, 1, 8, true, false, 0);
        erased.ApplyErasure(); Place(state, king); Place(state, erased); state.GetPlayer(enemy).Master.CurrentHp = 40;
        var card = state.GetPlayer(owner).Hand.Add(MeteorRules.CardId); state.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0));
        IReadOnlyList<BattleEventDto> events;
        if (owner == PlayerId.AI)
        {
            ClearOtherAiHand(state, card.RuntimeId); state.AI.Master.RemainingAttacksThisTurn = 0;
            events = fixture.Session.RunServerAiActionIfNeeded();
        }
        else events = fixture.Session.ApplyCommand(Command(owner, card.RuntimeId));
        Require(events.Any(e => e.EventType == BattleEventType.SpellCast && e.CardId == MeteorRules.CardId), "Expected actual Meteor server cast.");
        Require(king.IsDemonKingRevivalPending && state.GetBoard(enemy).GetOccupant(king.Position) == king &&
            events.Any(e => e.EventType == BattleEventType.DamageApplied && e.TargetRuntimeId == king.RuntimeId && e.Amount == 8) &&
            !events.Any(e => e.EventType == BattleEventType.OccupantRemoved && e.TargetRuntimeId == king.RuntimeId),
            "Meteor must record HP loss without falsely removing a pending-revival Demon King.");
        Require(!erased.IsDemonKingRevivalPending && state.GetBoard(enemy).GetOccupant(erased.Position) == null &&
            events.Count(e => e.EventType == BattleEventType.OccupantRemoved && e.TargetRuntimeId == erased.RuntimeId) == 1,
            "A suppressed Demon King must retain genuine removal/death feedback.");
    }
    private static void CheckSpellPowerSuppression(ICardDefinitionProvider provider, PlayerId owner)
    {
        foreach (var isBuilding in new[] { false, true })
        foreach (var status in new[] { "normal", "drained", "erasure", "sealed" })
        {
            var fixture = Fixture(provider, owner, 0); var state = fixture.State;
            OccupantState source = isBuilding ? Building(owner, 0, 500, spellPower: 20) : Unit(owner, 0, 500, spellPower: 20);
            if (status == "drained") source.IsDrained = true;
            if (status == "erasure") source.ApplyErasure();
            if (status == "sealed") source.EnterSealbound(3);
            Place(state, source); Amplify(fixture, owner);
            var events = CastNew(fixture, owner); var expected = status == "normal" ? 70 : 50;
            Require(state.GetOpponent(owner).Master.CurrentHp == 333 - expected &&
                events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == expected,
                "SpellPower suppression or independent player amplification differs in actual server damage/display.");
        }
    }
    private static void CheckActualServerAi(ICardDefinitionProvider provider)
    {
        var fixture = Fixture(provider, PlayerId.AI, 13, serverAi: true); var state = fixture.State;
        var source = Unit(PlayerId.AI, 0, 500, spellPower: 20); source.RemainingAttacksThisTurn = 0; Place(state, source);
        state.Player.Master.CurrentHp = 73; state.AI.Master.RemainingAttacksThisTurn = 0;
        ClearOtherAiHand(state, "none");
        state.AI.Hand.Add(MeteorRules.CardId); state.AI.Hand.AddTemporaryReplicate(MeteorRules.CardId);
        state.AI.Resources.Add(new ResourceSet(7, 0, 0, 0));
        var events = fixture.Session.RunServerAiActionIfNeeded();
        Require(state.Result.Winner == PlayerId.AI && events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == 73 &&
            events.Single(e => e.EventType == BattleEventType.SpellCast).SourceOwnerId == PlayerIdDto.AI,
            "Actual server AI did not choose the untargeted Meteor win using live SpellPower and account level.");
        Require(state.AI.Resources.Mana == 0 && state.AI.Hand.Count == 1 && state.AI.Hand.CardIds.Single() == MeteorRules.CardId,
            "Actual AI cast did not consume exactly one hand copy and seven Mana.");
    }
    private static void CheckRestore(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner, 13); var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        Place(state, Unit(owner, 0, 500, spellPower: 20)); Amplify(fixture, owner);
        var card = state.GetPlayer(owner).Hand.Add(MeteorRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0)); Place(state, Building(enemy, 0, 500));
        var snapshot = fixture.Session.CreatePersistenceSnapshot("meteor-recovery") with
        {
            Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? 13 : 0), Participant(PlayerId.AI, owner == PlayerId.AI ? 13 : 0) }
        };
        var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var session = new BattleSession(snapshot.MatchId, false);
        Require(session.RestoreFromPersistenceSnapshot(saved), "JSON persistence recovery failed.");
        var restored = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(session)!).CurrentBattleState;
        var events = session.ApplyCommand(Command(owner, card.RuntimeId));
        Require(restored.GetPlayer(enemy).Master.CurrentHp == 250 && restored.GetBoard(enemy).GetOccupant(new TileCoord(0, 0))!.CurrentHp == 417 &&
            events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == 83,
            "Persisted participant level, occupant SpellPower or player amplification differed after JSON restore.");
        Require(session.CreatePersistenceSnapshot("meteor-resaved").Participants!.Single(p => p.RuntimeSeatId == Dto(owner).ToString())
            .CardUpgradeLevels[MeteorRules.CardId] == 13, "Participant level was lost on next save.");
    }
    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "meteor-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [MeteorRules.CardId] = level });
    private static (BattleSession Session, BattleState State) Fixture(ICardDefinitionProvider provider, PlayerId owner, int level, bool serverAi = false)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, MeteorRules.CardId, level);
        levels.SetUpgradeLevel(state.GetOpponent(owner).Id, MeteorRules.CardId, level == 13 ? 0 : 13);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession("meteor-check-" + owner + "-" + level, serverAi);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        if (serverAi) typeof(BattleSession).GetField("_aiDecisionService", Hidden)!.SetValue(session,
            new AiDecisionService(provider, new TargetingService(), levels, new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 }));
        return (session, state);
    }
    private static IReadOnlyList<BattleEventDto> CastNew((BattleSession Session, BattleState State) fixture, PlayerId owner)
    {
        var card = fixture.State.GetPlayer(owner).Hand.Add(MeteorRules.CardId);
        fixture.State.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0)); return fixture.Session.ApplyCommand(Command(owner, card.RuntimeId));
    }
    private static void Amplify((BattleSession Session, BattleState State) fixture, PlayerId owner)
    {
        var card = fixture.State.GetPlayer(owner).Hand.Add(MagicAmplificationCircleRules.CardId);
        fixture.State.GetPlayer(owner).Resources.Add(new ResourceSet(1, 0, 0, 0));
        fixture.Session.ApplyCommand(Command(owner, card.RuntimeId, card.CardId));
    }
    private static void ClearOtherAiHand(BattleState state, string keepRuntimeId)
    {
        foreach (var other in state.AI.Hand.Cards.Where(c => c.RuntimeId != keepRuntimeId).ToArray()) state.AI.Hand.Remove(other.CardId, other.RuntimeId);
    }
    private static ClientBattleCommandMessage Command(PlayerId owner, string runtimeId, string cardId = MeteorRules.CardId) => new()
    { ActorId = Dto(owner), CommandType = OnlineBattleCommandType.CastScriptedSpell, CardId = cardId, HandCardRuntimeId = runtimeId };
    private static UnitState Unit(PlayerId owner, int col, int hp, int defense = 0, bool endure = false, bool hiding = false, bool flying = false,
        int spellPower = 0, bool shielder = false, int row = 0) =>
        new(Guid.NewGuid().ToString(), "Goblin", owner, new TileCoord(col, row), AttackType.Melee, 1, hp, true, false, 0,
            hasEndure: endure, physicalDefense: 999, magicDefense: defense, hasHiding: hiding, hasFlying: flying, spellPower: spellPower, hasShielder: shielder);
    private static BuildingState Building(PlayerId owner, int col, int hp, int defense = 0, string id = "ManaPond", int spellPower = 0) =>
        new(Guid.NewGuid().ToString(), id, owner, new TileCoord(col, 0), false, 0, hp, physicalDefense: 0, magicDefense: defense, spellPower: spellPower);
    private static void Place(BattleState state, OccupantState occupant) => state.GetBoard(occupant.OwnerId).Place(occupant.Position, occupant);
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
