using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Runs;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using LogEntry = Project333.Runtime.Application.Online.BattleCombatLogEntryDto;

internal static class NuclearChecks
{
    private const int BaseDamage = 100;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var definition = (ScriptedSpellCardDefinition)provider.GetRequired(NuclearRules.CardId);
        Require(definition.Damage == BaseDamage && definition.DamageType == DamageType.Fixed,
            "Nuclear shared definition must contain 100 Fixed damage.");
        Require(CardUpgradeRules.IsCardUpgradeable(definition), "Nuclear must be upgradeable.");
        CheckDraftAndRewardAdmission();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            for (var level = 0; level <= CardUpgradeRules.MaxLevel; level++)
                CheckUpgradedServerCast(provider, owner, level);
            CheckProtectionRules(provider, owner);
            CheckSimultaneousDamageAndDestruction(provider, owner);
            CheckFalloutMasterDefeat(provider, owner);
            CheckParticipantUpgradeRestoration(provider, owner);
            CheckFalloutRestoration(provider, owner);
        }
        Require(definition.Damage == BaseDamage, "Upgraded casts mutated the shared card definition.");
        Console.WriteLine("PASS Nuclear: both seats, Lv.0-13 immediate damage/display, exact hand copy, Fixed/protection rules, plant chain, Master/unit Fallout and building exclusion, periodic defeat, participant upgrades and full JSON/view restoration.");
    }

    private static void CheckDraftAndRewardAdmission()
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        var record = database.Cards.Single(card => card.Id == NuclearRules.CardId);
        Require(record.IncludeInDraft && record.IncludeInRewards, "Nuclear must be enabled in both release pools.");
        var generator = new ServerDraftOfferGenerator(database);
        var offered = false;
        for (var seed = 0; seed < 64 && !offered; seed++)
        {
            var opening = generator.CreateOffer(seed, Array.Empty<string>());
            if (!opening.Contains(NuclearRules.CardId)) continue;
            var next = generator.CreateOffer(seed, new[] { NuclearRules.CardId });
            Require(next.All(id => database.Cards.Single(card => card.Id == id).Rarity != CardRarity.Legendary),
                "Nuclear opening pick must preserve the later non-Legendary offer rule.");
            offered = true;
        }
        Require(offered, "The server draft never offered released Nuclear as a Legendary opening candidate.");
        var rewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.Static | BindingFlags.NonPublic)!;
        Require((bool)rewardable.Invoke(null, new object[] { record })!, "The actual server reward filter rejected Nuclear.");
        var singleCardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var generate = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rewards = (IReadOnlyDictionary<string, int>)generate.Invoke(null, new object[] { run, singleCardPool })!;
        Require(rewards.Count == 1 && rewards.TryGetValue(NuclearRules.CardId, out var count) && count == 3,
            "The actual normalized Legendary reward pool must grant Nuclear's three card copies for one win.");
    }

    private static void CheckUpgradedServerCast(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var fixture = CreateFixture(provider, owner, level);
        var state = fixture.State;
        var enemy = state.GetOpponent(owner).Id;
        var enemyUnit = Unit("enemy", enemy, 0, 0, 500, physicalDefense: 999, magicDefense: 999,
            hasHiding: true, hasFlying: true);
        var enemyBuilding = new BuildingState("building", "ManaPond", enemy, new TileCoord(1, 0), false, 0, 500,
            physicalDefense: 999, magicDefense: 999);
        var ally = Unit("ally", owner, 0, 0, 500, spellPower: 50);
        Place(state, enemyUnit); Place(state, enemyBuilding); Place(state, ally);
        var unusedCopy = state.GetPlayer(owner).Hand.Add(NuclearRules.CardId);
        var selectedCopy = state.GetPlayer(owner).Hand.Add(NuclearRules.CardId);
        var powerBefore = state.GetPlayer(owner).Resources.Power;
        var discardedBefore = state.GetPlayer(owner).Discard.Count;
        var events = Cast(fixture.Session, state, owner, selectedCopy.RuntimeId);
        var expected = BaseDamage + level;
        Require(enemyUnit.CurrentHp == 500 - expected && enemyBuilding.CurrentHp == 500 - expected,
            $"{owner} Lv.{level}: server damage did not ignore defense/flying or hit hidden units/buildings.");
        Require(state.GetPlayer(enemy).Master.CurrentHp == 333 - expected,
            $"{owner} Lv.{level}: enemy master did not receive immediate damage.");
        Require(ally.CurrentHp == 500 && state.GetPlayer(owner).Master.CurrentHp == 333,
            "Nuclear directly damaged its caster's board.");
        Require(events.Single(x => x.EventType == BattleEventType.SpellCast).CardSpellDamage == expected,
            $"{owner} Lv.{level}: outbound spell damage disagrees with server resolution.");
        var damageEvents = events.Where(x => x.EventType == BattleEventType.DamageApplied).ToArray();
        Require(damageEvents.Length == 3 && damageEvents.All(x => x.Amount == expected &&
            x.DamageType == DamageType.Fixed && x.ValueCause == BattleValueChangeCause.Spell &&
            x.SourceOwnerId == ToDto(owner) && x.TargetOwnerId == ToDto(enemy) &&
            x.SourceCardId == NuclearRules.CardId), "Nuclear immediate damage events lost provenance/type/count.");
        Require(!state.GetPlayer(owner).Hand.Contains(NuclearRules.CardId, selectedCopy.RuntimeId) &&
            state.GetPlayer(owner).Hand.Contains(NuclearRules.CardId, unusedCopy.RuntimeId),
            "The server consumed a different Nuclear hand instance.");
        Require(state.GetPlayer(owner).Resources.Power == powerBefore &&
            state.GetPlayer(owner).Discard.Count == discardedBefore + 1,
            $"Nuclear did not spend exactly its added {NuclearRules.PowerCost} Power and discard one card.");
        Require(state.GetBoard(enemy).FalloutStacks == 1 && state.GetBoard(owner).FalloutStacks == 0 &&
            enemyUnit.FalloutStacks == 1 && enemyBuilding.FalloutStacks == 0 &&
            state.GetPlayer(enemy).Master.FalloutStacks == 1, "Fallout must expose enemy Units/Master and exclude Buildings.");
        Require(((ScriptedSpellCardDefinition)provider.GetRequired(NuclearRules.CardId)).Damage == BaseDamage,
            "A hand cast leaked its upgrade level into the shared definition.");
        var turnEvents = EndTurn(fixture.Session, owner);
        Require(enemyUnit.CurrentHp == 490 - expected && state.GetPlayer(enemy).Master.CurrentHp == 323 - expected &&
            enemyBuilding.CurrentHp == 500 - expected && ally.CurrentHp == 500,
            $"Lv.{level} changed fixed-10 Fallout damage or incorrectly ticked a Building/ally.");
        var ticks = turnEvents.Where(x => x.ValueCause == BattleValueChangeCause.Fallout).ToArray();
        Require(ticks.Length == 2 && ticks.All(x => x.EventType == BattleEventType.DamageApplied &&
            x.Amount == 10 && x.DamageType == DamageType.Fixed && x.TargetOwnerId == ToDto(enemy)),
            "Server must emit exactly one fixed-10 Fallout packet for each exposed Unit/Master.");
    }

    private static void CheckProtectionRules(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner, 13);
        var state = fixture.State;
        var enemy = state.GetOpponent(owner).Id;
        var sealedUnit = Unit("sealed", enemy, 0, 0, 50); sealedUnit.EnterSealbound(3);
        var invincible = Unit("invincible", enemy, 1, 0, 50, hasEndure: true);
        invincible.AddInvincibleEffect(InvincibleDurationType.Always);
        var endure = Unit("endure", enemy, 2, 0, 50, hasEndure: true);
        var drained = Unit("drained", enemy, 3, 0, 500, hasEndure: true); drained.IsDrained = true;
        var lethal = Unit("lethal", enemy, 4, 0, 20);
        foreach (var unit in new[] { sealedUnit, invincible, endure, drained, lethal }) Place(state, unit);
        var events = CastNewCard(fixture, owner);
        Require(sealedUnit.CurrentHp == 50 && sealedUnit.FalloutStacks == 0 &&
            !events.Any(x => x.TargetRuntimeId == sealedUnit.RuntimeId), "Sealbound received Nuclear damage/feedback/exposure.");
        Require(invincible.CurrentHp == 50 && !invincible.EndureUsed &&
            events.Count(x => x.EventType == BattleEventType.InvinciblePrevented && x.TargetRuntimeId == invincible.RuntimeId) == 1,
            "Invincible did not prevent Fixed damage or consumed Endure.");
        Require(endure.CurrentHp == 1 && endure.EndureUsed &&
            events.Any(x => x.EventType == BattleEventType.DamageApplied && x.TargetRuntimeId == endure.RuntimeId && x.Amount == 49),
            "Endure did not survive Nuclear at 1 HP with actual-damage feedback.");
        Require(drained.CurrentHp == 161 && !drained.EndureUsed &&
            events.Any(x => x.TargetRuntimeId == drained.RuntimeId && x.Amount == 339),
            "Drained did not triple Lv.13 Fixed damage.");
        Require(state.GetBoard(enemy).GetOccupant(lethal.Position) == null &&
            events.Any(x => x.EventType == BattleEventType.OccupantRemoved && x.RuntimeId == lethal.RuntimeId),
            "Lethal immediate Nuclear damage did not remove the unit.");
        var master = state.GetPlayer(enemy).Master;
        master.AddInvincibleEffect(InvincibleDurationType.Always);
        var turnEvents = EndTurn(fixture.Session, owner);
        Require(sealedUnit.CurrentHp == 50 && sealedUnit.FalloutStacks == 0 && invincible.CurrentHp == 50 &&
            !invincible.EndureUsed && state.GetBoard(enemy).GetOccupant(endure.Position) == null && drained.CurrentHp == 131,
            "Periodic Fallout did not preserve Sealbound/Invincible/used-Endure/Drained rules.");
        Require(master.CurrentHp == 220 && master.FalloutStacks == 1 &&
            turnEvents.Any(x => x.EventType == BattleEventType.InvinciblePrevented &&
                x.ValueCause == BattleValueChangeCause.Fallout && x.TargetRuntimeId == master.RuntimeId),
            "Master Invincible did not prevent Fallout with server feedback.");
        master.RestoreInvincibleEffects(Array.Empty<InvincibleEffectState>());
        master.IsDrained = true;
        var secondTurnEvents = EndTurn(fixture.Session, enemy);
        Require(master.CurrentHp == 190 && secondTurnEvents.Any(x => x.EventType == BattleEventType.DamageApplied &&
            x.ValueCause == BattleValueChangeCause.Fallout && x.TargetRuntimeId == master.RuntimeId && x.Amount == 30),
            "Master Drained did not triple Fallout on the other player's turn start.");
    }

    private static void CheckFalloutMasterDefeat(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner, 13);
        var state = fixture.State;
        var enemyMaster = state.GetOpponent(owner).Master;
        enemyMaster.CurrentHp = 120;
        CastNewCard(fixture, owner);
        Require(!state.IsEnded && enemyMaster.CurrentHp == 7 && enemyMaster.FalloutStacks == 1,
            "The immediate packet should leave this Master alive with one Fallout stack.");
        var events = EndTurn(fixture.Session, owner);
        Require(enemyMaster.CurrentHp == -3 && state.GetPlayer(owner).Master.CurrentHp == 333 &&
            state.IsEnded && state.Result.Winner == owner &&
            events.Count(x => x.EventType == BattleEventType.BattleEnded) == 1,
            "Lethal Master Fallout did not finish the server battle with the correct winner.");
        Require(events.Any(x => x.EventType == BattleEventType.DamageApplied && x.ValueCause == BattleValueChangeCause.Fallout &&
            x.TargetRuntimeId == enemyMaster.RuntimeId && x.Amount == 7 && x.HpAfter == 0),
            "Lethal Master Fallout feedback must report actual lost HP.");
    }

    private static void CheckSimultaneousDamageAndDestruction(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner, 0);
        var state = fixture.State;
        var enemy = state.GetOpponent(owner).Id;
        state.GetPlayer(owner).Master.CurrentHp = 20;
        state.GetPlayer(enemy).Master.CurrentHp = 20;
        var plant = new BuildingState("plant", NuclearPowerPlantRules.CardId, enemy, new TileCoord(0, 0), false, 0, 20);
        var secondTarget = Unit("after-plant", enemy, 1, 0, 120);
        var ally = Unit("chain-ally", owner, 0, 0, 200);
        Place(state, plant); Place(state, secondTarget); Place(state, ally);
        var events = CastNewCard(fixture, owner);
        var targetDamage = events.Where(x => x.EventType == BattleEventType.DamageApplied &&
            x.TargetRuntimeId == secondTarget.RuntimeId).ToArray();
        Require(targetDamage.Length == 2 && targetDamage[0].ValueCause == BattleValueChangeCause.Spell &&
            targetDamage[0].Amount == 100 && targetDamage[1].ValueCause == BattleValueChangeCause.NuclearPowerPlant &&
            targetDamage[1].Amount == 20, "Nuclear resolved a destruction chain before applying its whole damage packet.");
        Require(ally.CurrentHp == 170 && state.GetPlayer(owner).Master.CurrentHp == -10 &&
            state.GetPlayer(enemy).Master.CurrentHp == -110,
            "Destroyed NuclearPowerPlant did not finish both-board damage after the master received lethal damage.");
        Require(state.IsEnded && !state.Result.IsDraw && state.Result.Winner == owner &&
            events.Count(x => x.EventType == BattleEventType.BattleEnded) == 1,
            "Server resolved the battle before the chain finished or lost simultaneous master outcome.");
    }

    private static void CheckParticipantUpgradeRestoration(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner, 13);
        var enemy = fixture.State.GetOpponent(owner).Id;
        var handCard = fixture.State.GetPlayer(owner).Hand.Add(NuclearRules.CardId);
        fixture.State.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, NuclearRules.PowerCost, 0));
        Place(fixture.State, Unit("restored-target", enemy, 0, 0, 500));
        var snapshot = fixture.Session.CreatePersistenceSnapshot("nuclear-upgrade-check") with
        {
            Participants = new[]
            {
                Participant(PlayerId.Player, owner == PlayerId.Player ? 13 : 0),
                Participant(PlayerId.AI, owner == PlayerId.AI ? 13 : 0)
            }
        };
        var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(saved), "Server persistence recovery failed.");
        var restoredState = GetFlow(restoredSession).CurrentBattleState;
        Require(restoredState.GetPlayer(owner).Hand.Contains(NuclearRules.CardId, handCard.RuntimeId),
            "Recovery changed Nuclear's hand RuntimeId.");
        var events = restoredSession.ApplyCommand(CastCommand(owner, handCard.RuntimeId));
        Require(restoredState.GetPlayer(enemy).Master.CurrentHp == 220 &&
            restoredState.GetBoard(enemy).GetOccupant(new TileCoord(0, 0))!.CurrentHp == 387 &&
            events.Single(x => x.EventType == BattleEventType.SpellCast).CardSpellDamage == 113,
            "Persisted account upgrade levels did not reach actual server resolution/display after recovery.");
        var resaved = restoredSession.CreatePersistenceSnapshot("nuclear-upgrade-recheck");
        Require(resaved.Participants!.Single(x => x.RuntimeSeatId == ToDto(owner).ToString()).CardUpgradeLevels[NuclearRules.CardId] == 13,
            "Recovered participant upgrade level was lost on the next persistence write.");
        Require(restoredState.GetPlayer(enemy).Master.FalloutStacks == 1,
            "A recovered upgraded cast did not expose the enemy Master.");
    }

    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        ToDto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB",
        $"nuclear-account-{owner}", $"run-{owner}", $"deck-{owner}", Enumerable.Repeat("Goblin", 33).ToArray(),
        new Dictionary<string, int> { [NuclearRules.CardId] = level });

    private static void CheckFalloutRestoration(ICardDefinitionProvider provider, PlayerId owner)
    {
        var fixture = CreateFixture(provider, owner, 13);
        var state = fixture.State;
        var enemy = state.GetOpponent(owner).Id;
        var building = new BuildingState("old-building", "PowerPlant", enemy, new TileCoord(1, 0), false, 0, 500);
        Place(state, building);
        var target = Unit("fallout-target", enemy, 0, 0, 326);
        Place(state, target);
        CastNewCard(fixture, owner); CastNewCard(fixture, owner);
        Require(state.GetPlayer(enemy).Master.CurrentHp == 107, "Stacked upgraded casts did not deal 113 damage each.");
        EndTurn(fixture.Session, owner);
        Require(target.CurrentHp == 80 && target.FalloutStacks == 2 && state.GetPlayer(enemy).Master.CurrentHp == 87 &&
            state.GetPlayer(enemy).Master.FalloutStacks == 2 && building.CurrentHp == 274 && building.FalloutStacks == 0,
            "Two stacks must tick Unit/Master for 20 and never tick the immediately damaged Building.");
        var logs = (List<LogEntry>)typeof(BattleSession).GetField("_combatLogRecords", PrivateInstance)!.GetValue(fixture.Session)!;
        Require(logs.Any(x => x.ValueCause == BattleValueChangeCause.Fallout && x.Amount == 20 && x.TargetOwnerId == enemy),
            "Server Fallout combat log missing.");
        state.GetBoard(enemy).Remove(target.Position); target.TransferOwnership(owner);
        Place(state, target); target.ApplyErasure();
        var restore = typeof(BattleSession).GetMethod("RestoreBattleState", BindingFlags.NonPublic | BindingFlags.Static)!;
        var snapshot = fixture.Session.CreatePersistenceSnapshot("nuclear-fallout-check");
        var json = JsonSerializer.Serialize(snapshot.DomainState);
        BattleState Restore(string value) => (BattleState)restore.Invoke(null, new object[] {
            JsonSerializer.Deserialize<BattleSessionDomainSnapshot>(value)!, provider })!;
        // Previous versions wrote Building exposure. Recover through the real full-session path.
        var saved = JsonNode.Parse(JsonSerializer.Serialize(snapshot))!;
        foreach (var boardName in new[] { "PlayerBoard", "AIBoard" })
            foreach (var occupant in saved["DomainState"]![boardName]!["Occupants"]!.AsArray())
                if (occupant!["Kind"]!.GetValue<string>() == "Building")
                {
                    occupant["FalloutPlayerStacks"] = 3;
                    occupant["FalloutAiStacks"] = 4;
                }
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(
            JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(saved.ToJsonString())!),
            "Full session Fallout JSON recovery failed.");
        var restored = GetFlow(restoredSession).CurrentBattleState;
        var restoredTarget = restored.GetBoard(owner).GetOccupant(new TileCoord(0, 0))!;
        var restoredBuilding = restored.GetBoard(enemy).GetOccupant(building.Position)!;
        Require(restored.GetBoard(enemy).FalloutStacks == 2 && restoredTarget.FalloutStacks == 2 && restoredTarget.CurrentHp == 80 &&
            restored.GetPlayer(enemy).Master.FalloutStacks == 2 && restored.GetPlayer(enemy).Master.CurrentHp == 87 &&
            restoredBuilding.FalloutPlayerStacks == 0 && restoredBuilding.FalloutAiStacks == 0,
            "Full JSON recovery lost Unit/Master exposure or retained obsolete Building stacks.");
        var restoredSnapshot = restoredSession.CreatePersistenceSnapshot("nuclear-fallout-view-check");
        foreach (var view in new[] { restoredSnapshot.PlayerView!, restoredSnapshot.AiView! })
        {
            var masterView = view.Occupants.Single(x => x.RuntimeId == restored.GetPlayer(enemy).Master.RuntimeId);
            var buildingView = view.Occupants.Single(x => x.RuntimeId == building.RuntimeId);
            Require(masterView.Kind == OccupantKind.Master && masterView.OwnerId == enemy && masterView.CurrentHp == 87 &&
                masterView.FalloutPlayerStacks + masterView.FalloutAiStacks == 2 &&
                buildingView.CurrentHp == 274 && buildingView.FalloutPlayerStacks == 0 && buildingView.FalloutAiStacks == 0,
                "Server views must expose Master Fallout and normalized Building zeros to both seats.");
            Require(view.PlayerFalloutStacks == (view.ViewerId == enemy ? 2 : 0) &&
                view.OpponentFalloutStacks == (view.ViewerId == enemy ? 0 : 2),
                "Server field exposure was not mapped to the correct viewer perspective.");
        }
        var future = Unit("future", enemy, 0, 0, 100); Place(restored, future);
        var futureBuilding = new BuildingState("future-building", "PowerPlant", enemy, new TileCoord(3, 0), false, 0, 100);
        Place(restored, futureBuilding);
        Require(future.FalloutStacks == 0 && futureBuilding.FalloutStacks == 0,
            "Restored Nuclear history must not grant Fallout to future Units or Buildings.");
        restored.RestoreRuntimeState(restored.TurnNumber, enemy, PhaseType.Main);
        restored.GetPlayer(enemy).Hand.Add("Goblin");
        restored.GetPlayer(enemy).Resources.Add(new ResourceSet(10, 0, 0, 0));
        var played = new PlayCardService(provider).PlayUnitCard(restored, enemy, "Goblin", new TileCoord(4, 0));
        Require(played.FalloutStacks == 0, "Actual post-recovery summon inherited Fallout.");
        EndTurn(restoredSession, enemy);
        Require(restoredTarget.CurrentHp == 60 && future.CurrentHp == 100 && restored.GetPlayer(enemy).Master.CurrentHp == 67 &&
            restoredBuilding.CurrentHp == 274 && futureBuilding.CurrentHp == 100,
            "Recovered Fallout did not tick both fields while preserving all Buildings.");
        var old = JsonNode.Parse(json)!;
        foreach (var boardName in new[] { "PlayerBoard", "AIBoard" })
        {
            var board = old[boardName]!.AsObject(); board.Remove("FalloutStacks");
            foreach (var occupant in board["Occupants"]!.AsArray())
            {
                occupant!.AsObject().Remove("FalloutPlayerStacks");
                occupant.AsObject().Remove("FalloutAiStacks");
            }
        }
        var legacy = Restore(old.ToJsonString());
        Require(legacy.PlayerBoard.FalloutStacks == 0 && legacy.AIBoard.FalloutStacks == 0 &&
            legacy.GetBoard(owner).GetOccupant(new TileCoord(0, 0))!.FalloutStacks == 0 &&
            legacy.GetPlayer(enemy).Master.FalloutStacks == 0,
            "Legacy snapshot Fallout defaults failed.");
    }

    private static (BattleSession Session, BattleState State) CreateFixture(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var upgrades = new InMemoryCardUpgradeLevelProvider();
        upgrades.SetUpgradeLevel(owner, NuclearRules.CardId, level);
        upgrades.SetUpgradeLevel(state.GetOpponent(owner).Id, NuclearRules.CardId, level == 13 ? 0 : 13);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, upgrades); flow.RestoreBattleState(state);
        var session = new BattleSession($"nuclear-check-{owner}-{level}", false);
        typeof(BattleSession).GetField("_battleFlowController", PrivateInstance)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", PrivateInstance)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", PrivateInstance)!.SetValue(session, upgrades);
        return (session, state);
    }

    private static IReadOnlyList<BattleEventDto> CastNewCard((BattleSession Session, BattleState State) fixture, PlayerId owner)
    {
        var card = fixture.State.GetPlayer(owner).Hand.Add(NuclearRules.CardId);
        return Cast(fixture.Session, fixture.State, owner, card.RuntimeId);
    }

    private static IReadOnlyList<BattleEventDto> Cast(BattleSession session, BattleState state, PlayerId owner, string handRuntimeId)
    {
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, NuclearRules.PowerCost, 0));
        return session.ApplyCommand(CastCommand(owner, handRuntimeId));
    }

    private static ClientBattleCommandMessage CastCommand(PlayerId owner, string handRuntimeId) => new()
    {
        ActorId = ToDto(owner), CommandType = OnlineBattleCommandType.CastScriptedSpell,
        CardId = NuclearRules.CardId, HandCardRuntimeId = handRuntimeId
    };

    private static IReadOnlyList<BattleEventDto> EndTurn(BattleSession session, PlayerId owner) => session.ApplyCommand(
        new ClientBattleCommandMessage { ActorId = ToDto(owner), CommandType = OnlineBattleCommandType.EndTurn });

    private static UnitState Unit(string id, PlayerId owner, int column, int row, int hp,
        bool hasEndure = false, int physicalDefense = 0, int magicDefense = 0,
        bool hasHiding = false, bool hasFlying = false, int spellPower = 0) => new(
        id, "Goblin", owner, new TileCoord(column, row), AttackType.Melee, 1, hp, true, false, 0,
        hasEndure: hasEndure, physicalDefense: physicalDefense, magicDefense: magicDefense,
        hasHiding: hasHiding, hasFlying: hasFlying, spellPower: spellPower);

    private static void Place(BattleState state, OccupantState occupant) => state.GetBoard(occupant.OwnerId).Place(occupant.Position, occupant);
    private static PlayerIdDto ToDto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static BattleFlowController GetFlow(BattleSession session) =>
        (BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", PrivateInstance)!.GetValue(session)!;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
