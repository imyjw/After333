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

internal static class EarthquakeChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var definition = (ScriptedSpellCardDefinition)provider.GetRequired(EarthquakeRules.CardId);
        Require(definition.Damage == 10 && definition.DamageType == DamageType.Physical &&
            definition.Cost.Gold == 3 && CardUpgradeRules.IsCardUpgradeable(definition), "Earthquake canonical card definition differs.");
        CheckReleasePools();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            for (var level = 0; level <= 13; level++) CheckCast(provider, owner, level);
            CheckProtectionAndChain(provider, owner);
            CheckRestore(provider, owner);
            CheckPendingRevivalEvents(provider, owner);
        }
        Require(definition.Damage == 10, "An upgraded cast mutated the shared base definition.");
        Console.WriteLine("PASS Earthquake: actual draft/reward filters, both seats Lv.0-13 Physical damage/display/exact hand/gold, building x3 before defense, protections, empty tiles, destruction chain/outcome, participant upgrades/full JSON recovery.");
    }
    private static void CheckReleasePools()
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        var record = database.Cards.Single(c => c.Id == EarthquakeRules.CardId);
        var generator = new ServerDraftOfferGenerator(database); var offered = false;
        for (var seed = 0; seed < 512 && !offered; seed++)
        {
            var first = generator.CreateOffer(seed, Array.Empty<string>())[0];
            offered = generator.CreateOffer(seed, new[] { first }).Contains(EarthquakeRules.CardId);
        }
        Require(offered, "Actual post-Legendary draft offers never offered Earthquake.");
        var rewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.Static | BindingFlags.NonPublic)!;
        Require((bool)rewardable.Invoke(null, new object[] { record })!, "Actual server reward filter rejected Earthquake.");
        var generate = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.Static | BindingFlags.NonPublic)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var oneCardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)generate.Invoke(null, new object[] { run, oneCardPool })!;
        Require(rewards.Count == 1 && rewards[EarthquakeRules.CardId] == 3, "Actual one-win reward generation did not grant three Earthquake copies.");
    }
    private static void CheckCast(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var fixture = Fixture(provider, owner, level); var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var unit = Unit(enemy, 0, 500, defense: 4, hiding: true, flying: true);
        var building = Building(enemy, 1, 500, defense: 4);
        var ally = Unit(owner, 0, 500, spellPower: 80); Place(state, unit); Place(state, building); Place(state, ally);
        var unused = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
        var chosen = state.GetPlayer(owner).Hand.AddTemporaryReplicate(EarthquakeRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3));
        var gold = state.GetPlayer(owner).Resources.Gold;
        var events = fixture.Session.ApplyCommand(Command(owner, chosen.RuntimeId)); var damage = 10 + level;
        Require(unit.CurrentHp == 500 - (damage - 4) && building.CurrentHp == 500 - (damage * 3 - 4),
            $"{owner} Lv.{level}: Physical/building multiplier order or defense/flying/SpellPower differs.");
        Require(state.GetPlayer(enemy).Master.CurrentHp == 333 - damage && ally.CurrentHp == 500 && state.GetPlayer(owner).Master.CurrentHp == 333,
            "Enemy Master damage or allied immunity differs.");
        Require(events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == damage,
            "Outbound display damage differs from actual upgraded base packet.");
        var packets = events.Where(e => e.EventType == BattleEventType.DamageApplied).ToArray();
        Require(packets.Length == 3 && packets.All(e => e.DamageType == DamageType.Physical && e.ValueCause == BattleValueChangeCause.Spell &&
            e.SourceOwnerId == Dto(owner) && e.TargetOwnerId == Dto(enemy) && e.SourceCardId == EarthquakeRules.CardId),
            "Earthquake damage events lost provenance/type/owner or duplicated packets.");
        Require(packets.Single(e => e.TargetRuntimeId == building.RuntimeId).Amount == damage * 3 - 4,
            "Building event damage does not match HP loss.");
        Require(state.GetPlayer(owner).Hand.Contains(EarthquakeRules.CardId, unused.RuntimeId) &&
            !state.GetPlayer(owner).Hand.Contains(EarthquakeRules.CardId, chosen.RuntimeId) &&
            state.GetPlayer(owner).Resources.Gold == gold - 3 && state.GetPlayer(owner).Discard.CardIds.Contains(EarthquakeRules.CardId),
            "Server did not consume exact temporary card and exactly three Gold.");
        Require(state.PersistentEffects.Count == 0 && state.PlayerBoard.FalloutStacks + state.AIBoard.FalloutStacks == 0,
            "Earthquake invented a persistent effect.");
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
            "Invincible Physical protection feedback differs.");
        Require(endure.CurrentHp == 1 && endure.EndureUsed && drained.CurrentHp == 410,
            "Endure or multiplicative Drained building damage differs.");
        fixture = Fixture(provider, owner, 0); state = fixture.State;
        state.GetPlayer(owner).Master.CurrentHp = 20; state.GetPlayer(enemy).Master.CurrentHp = 30;
        var first = Building(enemy, 0, 25, id: "NuclearPowerPlant"); var second = Building(enemy, 1, 25, id: "NuclearPowerPlant");
        Place(state, first); Place(state, second); events = CastNew(fixture, owner);
        Require(state.Result.IsDraw && state.GetPlayer(owner).Master.CurrentHp == -40 && state.GetPlayer(enemy).Master.CurrentHp == -40,
            "All area packets and reactor chain must resolve before both-Master outcome.");
        var array = events.ToList();
        Require(array.FindLastIndex(e => e.EventType == BattleEventType.DamageApplied && e.SourceCardId == EarthquakeRules.CardId) <
            array.FindIndex(e => e.ValueCause == BattleValueChangeCause.NuclearPowerPlant), "Destruction chain preceded remaining Earthquake packets.");
        fixture = Fixture(provider, owner, 0); state = fixture.State; state.GetPlayer(enemy).Master.CurrentHp = 10;
        CastNew(fixture, owner); Require(state.Result.Winner == owner, "An otherwise empty field with lethal Master damage did not finish.");
    }
    private static void CheckPendingRevivalEvents(ICardDefinitionProvider provider, PlayerId owner)
    {
        // The AI case executes the actual server AI event path; the player case uses the wire command.
        var fixture = Fixture(provider, owner, 0, serverAi: owner == PlayerId.AI);
        var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var king = new UnitState("reviving-king", "DemonKing", enemy, new TileCoord(0, 0), AttackType.Melee, 1, 8, true, false, 0);
        var erased = new UnitState("erased-king", "DemonKing", enemy, new TileCoord(1, 0), AttackType.Melee, 1, 8, true, false, 0);
        erased.ApplyErasure(); Place(state, king); Place(state, erased);
        state.GetPlayer(enemy).Master.CurrentHp = 10;
        var card = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3));
        IReadOnlyList<BattleEventDto> events;
        if (owner == PlayerId.AI)
        {
            foreach (var other in state.AI.Hand.Cards.Where(c => c.RuntimeId != card.RuntimeId).ToArray())
                state.AI.Hand.Remove(other.CardId, other.RuntimeId);
            state.AI.Master.RemainingAttacksThisTurn = 0;
            events = fixture.Session.RunServerAiActionIfNeeded();
        }
        else events = fixture.Session.ApplyCommand(Command(owner, card.RuntimeId));
        Require(events.Any(e => e.EventType == BattleEventType.SpellCast && e.CardId == EarthquakeRules.CardId), "Expected actual Earthquake server cast.");
        Require(king.IsDemonKingRevivalPending && state.GetBoard(enemy).GetOccupant(king.Position) == king &&
            events.Any(e => e.EventType == BattleEventType.DamageApplied && e.TargetRuntimeId == king.RuntimeId && e.Amount == 8) &&
            !events.Any(e => e.EventType == BattleEventType.OccupantRemoved && e.TargetRuntimeId == king.RuntimeId),
            "Earthquake must record HP loss without falsely removing a pending-revival Demon King.");
        Require(!erased.IsDemonKingRevivalPending && state.GetBoard(enemy).GetOccupant(erased.Position) == null &&
            events.Count(e => e.EventType == BattleEventType.OccupantRemoved && e.TargetRuntimeId == erased.RuntimeId) == 1,
            "A suppressed Demon King must retain genuine removal/death feedback.");
    }

    private static void CheckRestore(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = Fixture(provider, owner, 13); var state = fixture.State; var enemy = state.GetOpponent(owner).Id;
        var card = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3)); Place(state, Building(enemy, 0, 500));
        var snapshot = fixture.Session.CreatePersistenceSnapshot("earthquake-recovery") with
        {
            Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? 13 : 0), Participant(PlayerId.AI, owner == PlayerId.AI ? 13 : 0) }
        };
        var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var session = new BattleSession(snapshot.MatchId, false);
        Require(session.RestoreFromPersistenceSnapshot(saved), "JSON persistence recovery failed.");
        var restored = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(session)!).CurrentBattleState;
        var events = session.ApplyCommand(Command(owner, card.RuntimeId));
        Require(restored.GetPlayer(enemy).Master.CurrentHp == 310 && restored.GetBoard(enemy).GetOccupant(new TileCoord(0, 0))!.CurrentHp == 431 &&
            events.Single(e => e.EventType == BattleEventType.SpellCast).CardSpellDamage == 23,
            "Persisted participant level did not reach Earthquake damage/display after restore.");
        Require(session.CreatePersistenceSnapshot("earthquake-resaved").Participants!.Single(p => p.RuntimeSeatId == Dto(owner).ToString())
            .CardUpgradeLevels[EarthquakeRules.CardId] == 13, "Participant level was lost on next save.");
    }
    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "earthquake-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [EarthquakeRules.CardId] = level });
    private static (BattleSession Session, BattleState State) Fixture(ICardDefinitionProvider provider, PlayerId owner, int level, bool serverAi = false)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, EarthquakeRules.CardId, level);
        levels.SetUpgradeLevel(state.GetOpponent(owner).Id, EarthquakeRules.CardId, level == 13 ? 0 : 13);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession("earthquake-check-" + owner + "-" + level, serverAi);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        if (serverAi) typeof(BattleSession).GetField("_aiDecisionService", Hidden)!.SetValue(session,
            new AiDecisionService(provider, new TargetingService(), levels, new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 }));
        return (session, state);
    }
    private static IReadOnlyList<BattleEventDto> CastNew((BattleSession Session, BattleState State) fixture, PlayerId owner)
    {
        var card = fixture.State.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
        fixture.State.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3)); return fixture.Session.ApplyCommand(Command(owner, card.RuntimeId));
    }
    private static ClientBattleCommandMessage Command(PlayerId owner, string runtimeId) => new()
    { ActorId = Dto(owner), CommandType = OnlineBattleCommandType.CastScriptedSpell, CardId = EarthquakeRules.CardId, HandCardRuntimeId = runtimeId };
    private static UnitState Unit(PlayerId owner, int col, int hp, int defense = 0, bool endure = false, bool hiding = false, bool flying = false, int spellPower = 0) =>
        new(Guid.NewGuid().ToString(), "Goblin", owner, new TileCoord(col, 0), AttackType.Melee, 1, hp, true, false, 0,
            hasEndure: endure, physicalDefense: defense, magicDefense: 999, hasHiding: hiding, hasFlying: flying, spellPower: spellPower);
    private static BuildingState Building(PlayerId owner, int col, int hp, int defense = 0, string id = "ManaPond") =>
        new(Guid.NewGuid().ToString(), id, owner, new TileCoord(col, 0), false, 0, hp, physicalDefense: defense, magicDefense: 999);
    private static void Place(BattleState state, OccupantState occupant) => state.GetBoard(occupant.OwnerId).Place(occupant.Position, occupant);
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}