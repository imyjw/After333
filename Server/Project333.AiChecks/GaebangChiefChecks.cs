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

internal static class GaebangChiefChecks
{
    private const string CardId = GaebangChiefRules.CardId;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Card database rejected Gaebang Chief.");
        var record = database.Cards.Single(card => card.Id == CardId);
        var definition = (UnitCardDefinition)provider.GetRequired(CardId);
        Require(record.DisplayName == "개방 방주" && record.Rarity == CardRarity.Unique &&
            record.Affiliation == CardAffiliation.Murim && record.IncludeInDraft && record.IncludeInRewards &&
            record.Races.SequenceEqual(new[] { UnitRace.Human }) && record.EffectText == GaebangChiefRules.EffectDescription &&
            string.IsNullOrEmpty(record.SpecialEffectText), "Gaebang Chief metadata/effect/race/pool flags differ.");
        Require(definition.Cost.Qi == 7 && definition.Cost.Gold + definition.Cost.Mana + definition.Cost.Power == 0 &&
            definition.Attack == 10 && definition.Health == 60 && definition.AttackType == AttackType.Melee &&
            definition.DamageType == DamageType.Physical && definition.PhysicalDefense == 0 && definition.MagicDefense == 0 &&
            definition.CanMove && definition.HitsPerAttack == 1 && definition.MaxAttacksPerTurn == 1 &&
            !definition.HasRush && !definition.HasReplicate && !definition.HasBerserker && !definition.HasEndure &&
            !definition.HasShielder && !definition.HasLifeSteal && !definition.HasHiding && !definition.HasFlying &&
            !definition.HasPiercing && CardUpgradeRules.IsCardUpgradeable(definition), "Gaebang Chief printed rules differ.");
        CheckActualPools(database, record);
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            for (var level = 0; level <= 13; level++)
                foreach (var gold in new[] { 0, 1, 3 }) CheckActualSession(provider, owner, level, gold);
        Console.WriteLine("PASS GaebangChief: actual Unique draft/reward pools, both seats Lv.0-13 exact hand/qi/non-consuming gold/summon/display, dynamic gold/gain/spend, ordinary melee Physical counter, Human/Erasure/Drained/Seal/Gu, full JSON participant upgrades/identity/combat buffs/view restoration.");
    }

    private static void CheckActualPools(JsonCardDefinitionDatabase database, JsonCardDefinitionRecord record)
    {
        var generator = new ServerDraftOfferGenerator(database);
        var actualDraftPool = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator)
            .GetField("_draftCards", Hidden)!.GetValue(generator)!;
        Require(actualDraftPool.Single(card => card.Id == CardId).Rarity == CardRarity.Unique,
            "Actual draft pool omitted the Unique Gaebang Chief.");
        Require(Enumerable.Range(0, 512).Any(seed => generator.CreateOffer(seed,
            new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId)),
            "Actual draft offers never selected Gaebang Chief.");
        var isRewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)isRewardable.Invoke(null, new object[] { record })!, "Server reward filter rejected Gaebang Chief.");
        var rewardMethod = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)rewardMethod.Invoke(null, new object[] { run, rewardPool })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random card reward creation omitted Gaebang Chief.");
        var rewardCount = database.Cards.Count(card => (bool)isRewardable.Invoke(null, new object[] { card })!);
        var upgrades = database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()));
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {actualDraftPool.Count}, upgradeable {upgrades}, actual rewards {rewardCount}.");
    }

    private static void CheckActualSession(ICardDefinitionProvider provider, PlayerId owner, int level, int gold)
    {
        var attackGain = new[] { 3, 6, 9, 13 }.Count(milestone => level >= milestone);
        var attack = 10 + attackGain;
        var hp = 60 + level - attackGain;
        var enemy = owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        var enemyLevel = 13 - level;
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider();
        levels.SetUpgradeLevel(owner, CardId, level); levels.SetUpgradeLevel(enemy, CardId, enemyLevel);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession("gaebang-chief-" + owner + "-" + level + "-" + gold, false);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        var unused = state.GetPlayer(owner).Hand.Add(CardId); var chosen = state.GetPlayer(owner).Hand.Add(CardId);
        SetGold(state.GetPlayer(owner).Resources, gold);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 7, 0, 0));
        var qi = state.GetPlayer(owner).Resources.Qi;
        var events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.PlayUnitCard, ExpectedTurnNumber = state.TurnNumber,
            CardId = CardId, HandCardRuntimeId = chosen.RuntimeId, HasTarget = true, TargetOwnerId = Dto(owner),
            TargetCoord = new TileCoordDto { Column = 0, Row = 0 }
        });
        var unit = (UnitState)state.GetBoard(owner).GetOccupant(new TileCoord(0, 0))!;
        Require(unit.CardId == CardId && unit.BaseAttack == attack && unit.Attack == attack + gold * 10 && unit.OriginalAttack == attack &&
            unit.MaxHp == hp && unit.OriginalMaxHp == hp && unit.CurrentHp == hp &&
            unit.AttackType == AttackType.Melee && unit.DamageType == DamageType.Physical &&
            unit.Races.SequenceEqual(new[] { UnitRace.Human }) && unit.HasSummoningSickness && unit.CanMove,
            $"{owner} Lv.{level}: actual summoned combat/upgrade/race differs.");
        Require(state.GetPlayer(owner).Resources.Qi == qi - 7 && state.GetPlayer(owner).Resources.Gold == gold &&
            !state.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId) &&
            state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId), "Qi, non-consuming gold or exact selected hand copy differs.");
        var played = events.Single(evt => evt.EventType == BattleEventType.CardPlayed && evt.CardId == CardId);
        Require(played.CardAttack == attack + gold * 10 && played.CardMaxHp == hp && played.AttackBonus == attack - 10, "Authoritative played-card display differs.");
        state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 2));
        Require(unit.Attack == attack + (gold + 2) * 10, "Gold gain did not update attack immediately.");
        state.GetPlayer(owner).Resources.Spend(new ResourceSet(0, 0, 0, 2));
        Require(unit.Attack == attack + gold * 10 && unit.CombatStatBuffs.Count == 0, "Gold spending did not update attack without a permanent gain.");
        DamageResolutionRules.ApplyEffectDamage(unit, 1, DamageType.Fixed);
        var meleeTarget = new UnitState("melee-target", "fixture", enemy, new TileCoord(0, 0), AttackType.Melee,
            5, 100, true, false, 0, physicalDefense: 2);
        meleeTarget.HasSummoningSickness = false; state.GetBoard(enemy).Place(meleeTarget.Position, meleeTarget);
        unit.HasSummoningSickness = false;
        events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.Attack, ExpectedTurnNumber = state.TurnNumber,
            OccupantRuntimeId = unit.RuntimeId, SourceCoord = new TileCoordDto { Column = 0, Row = 0 }, HasTarget = true,
            TargetOwnerId = Dto(enemy), TargetCoord = new TileCoordDto { Column = 0, Row = 0 }
        });
        var damagePackets = events.Where(evt => evt.EventType == BattleEventType.DamageApplied).ToArray();
        var direct = damagePackets.Single(evt => evt.ValueCause == BattleValueChangeCause.NormalAttack);
        var counter = damagePackets.Single(evt => evt.ValueCause == BattleValueChangeCause.Counterattack);
        Require(events.Any(evt => evt.EventType == BattleEventType.AttackStarted) &&
            meleeTarget.CurrentHp == 100 - (attack + gold * 10 - 2) && unit.CurrentHp == hp - 6 && unit.RemainingAttacksThisTurn == 0 &&
            damagePackets.Length == 2 && direct.SourceRuntimeId == unit.RuntimeId && direct.TargetRuntimeId == meleeTarget.RuntimeId &&
            direct.Amount == attack + gold * 10 - 2 && direct.HpBefore == 100 && direct.HpAfter == meleeTarget.CurrentHp &&
            counter.Amount == 5 && counter.TargetRuntimeId == unit.RuntimeId && counter.HpBefore == hp - 1 && counter.HpAfter == hp - 6 &&
            state.GetPlayer(owner).Resources.Gold == gold && unit.BaseAttack == attack && unit.CombatStatBuffs.Count == 0,
            "Actual gold-scaled melee Physical wire packet, ordinary counter or non-consuming gold differs.");
        unit.ApplyCombatStatBuff("fixture-buff", attackBonus: 2, maxHpBonus: 4);
        DamageResolutionRules.ApplyEffectDamage(unit, 4, DamageType.Magic);
        var snapshot = session.CreatePersistenceSnapshot("gaebang-chief-recovery") with
        {
            Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? level : enemyLevel),
                Participant(PlayerId.AI, owner == PlayerId.AI ? level : enemyLevel) }
        };
        var received = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(received), "Full JSON session restoration failed.");
        var restoredState = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(restoredSession)!).CurrentBattleState;
        var restored = (UnitState)restoredState.GetBoard(owner).GetOccupant(unit.Position)!;
        Require(restored.RuntimeId == unit.RuntimeId && restored.OwnerId == owner && restored.BaseAttack == attack + 2 &&
            restored.MaxHp == hp + 4 && restored.CurrentHp == hp - 6 && restored.Attack == attack + 2 + gold * 10 && restored.OriginalAttack == attack && restored.OriginalMaxHp == hp &&
            restored.Races.SequenceEqual(new[] { UnitRace.Human }) && restored.AttackType == AttackType.Melee &&
            restored.DamageType == DamageType.Physical && restored.RemainingAttacksThisTurn == 0 && restored.CombatStatBuffs.Count == 1,
            "Restored identity, combat buffs, account baseline, race or spent attack differs.");
        Require(restoredState.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId) &&
            !restoredState.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId), "Full JSON restore changed hand-copy identity.");
        var resaved = restoredSession.CreatePersistenceSnapshot("gaebang-chief-resaved");
        foreach (var seat in new[] { PlayerId.Player, PlayerId.AI })
            Require(resaved.Participants!.Single(participant => participant.RuntimeSeatId == Dto(seat).ToString()).CardUpgradeLevels[CardId] ==
                (seat == owner ? level : enemyLevel), "Participant account upgrade did not round-trip.");
        var view = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restoredState, snapshot.MatchId, enemy);
        var viewReceived = JsonSerializer.Deserialize<Project333.Runtime.Application.Online.BattleStateViewDto>(JsonSerializer.Serialize(view))!;
        var viewed = viewReceived.Occupants.Single(occupant => occupant.RuntimeId == restored.RuntimeId);
        Require(viewReceived.ViewerId == enemy && viewed.OwnerId == owner && viewed.Attack == attack + 2 + gold * 10 && viewed.BaseAttack == attack + 2 &&
            viewed.CurrentHp == hp - 6 && viewed.MaxHp == hp + 4 && viewed.OriginalAttack == attack &&
            viewed.OriginalMaxHp == hp && viewed.Races.SequenceEqual(new[] { UnitRace.Human }) &&
            viewed.AttackType == AttackType.Melee && viewed.DamageType == DamageType.Physical,
            "Opposite-viewer JSON view lost identity/stats/race.");
        Require(viewReceived.Opponent.VisibleHandCardIds.Count == 0, "Opponent hand was exposed.");
        Require(restoredState.GetPlayer(owner).Resources.Gold == gold, "Restored current owner gold differs.");
        restored.IsDrained = true; Require(restored.Attack == attack + 2, "Drained did not suppress the gold effect.");
        restored.IsDrained = false; Require(restored.Attack == attack + 2 + gold * 10, "Drained release did not restore the current effect.");
        restored.EnterSealbound(1); Require(restored.Attack == attack + 2, "Sealbound did not suppress the gold effect.");
        Require(restored.ResolveSealboundOwnerTurnStart() && restored.Attack == attack + 2 + gold * 10, "Sealbound release lost current gold effect.");
        CheckActualTransferAndErasure(restoredSession, restoredState, restored, enemy, attack, hp, gold);
    }

    private static void CheckActualTransferAndErasure(BattleSession session, BattleState state, UnitState chief,
        PlayerId controller, int printedAttack, int printedHp, int previousGold)
    {
        var previousOwner = chief.OwnerId; var controllerGold = previousGold == 3 ? 1 : 3;
        SetGold(state.GetPlayer(controller).Resources, controllerGold); state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 10, 0, 0));
        state.SetActivePlayer(controller); state.SetPhase(PhaseType.Main);
        var chosen = state.GetPlayer(controller).Hand.Add(GuRules.CardId); var unused = state.GetPlayer(controller).Hand.Add(GuRules.CardId);
        session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(controller), CommandType = OnlineBattleCommandType.CastScriptedSpell, ExpectedTurnNumber = state.TurnNumber,
            CardId = GuRules.CardId, HandCardRuntimeId = chosen.RuntimeId, HasTarget = true, TargetOwnerId = Dto(previousOwner),
            TargetCoord = new TileCoordDto { Column = chief.Position.Column, Row = chief.Position.Row }
        });
        Require(chief.OwnerId == controller && chief.Attack == printedAttack + 2 + controllerGold * 10 &&
            chief.BaseAttack == printedAttack + 2 && chief.OriginalAttack == printedAttack && chief.CurrentHp == printedHp - 6 &&
            chief.CombatStatBuffs.Count == 1 && chief.HasSummoningSickness && state.GetPlayer(controller).Resources.Gold == controllerGold &&
            state.GetPlayer(controller).Resources.Qi == 0 && state.GetPlayer(controller).Hand.Contains(GuRules.CardId, unused.RuntimeId) &&
            !state.GetPlayer(controller).Hand.Contains(GuRules.CardId, chosen.RuntimeId), "Actual Gu owner/gold/account/buff/hand state differs.");
        state.GetPlayer(previousOwner).Resources.Add(new ResourceSet(0, 0, 0, 3));
        Require(chief.Attack == printedAttack + 2 + controllerGold * 10, "Chief still reads its old owner's gold.");
        state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 0, 0, 1));
        var snapshot = session.CreatePersistenceSnapshot("gaebang-chief-transferred");
        var received = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var recovered = new BattleSession(snapshot.MatchId, false);
        Require(recovered.RestoreFromPersistenceSnapshot(received), "Transferred chief full JSON restore failed.");
        var recoveredState = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(recovered)!).CurrentBattleState;
        var restored = (UnitState)recoveredState.GetBoard(controller).GetOccupant(chief.Position)!;
        Require(restored.RuntimeId == chief.RuntimeId && restored.OwnerId == controller && restored.BaseAttack == printedAttack + 2 &&
            restored.OriginalAttack == printedAttack && restored.Attack == printedAttack + 2 + (controllerGold + 1) * 10 &&
            restored.MaxHp == printedHp + 4 && restored.CurrentHp == printedHp - 6 && restored.HasRace(UnitRace.Human),
            "Transferred owner resources or original summoner upgrades were not restored independently.");
        new ErasureService().Apply(recoveredState, restored);
        recoveredState.GetPlayer(controller).Resources.Add(new ResourceSet(0, 0, 0, 1));
        Require(restored.IsErasure && restored.Attack == printedAttack && restored.BaseAttack == printedAttack &&
            restored.MaxHp == printedHp && restored.CurrentHp == printedHp - 6 && restored.CombatStatBuffs.Count == 0 &&
            restored.HasRace(UnitRace.Human), "Erasure failed to suppress dynamic gold without changing upgrade/race/damaged HP.");
        var erasedSnapshot = recovered.CreatePersistenceSnapshot("gaebang-chief-erased");
        var erasedRecovered = new BattleSession(erasedSnapshot.MatchId, false);
        Require(erasedRecovered.RestoreFromPersistenceSnapshot(JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(erasedSnapshot))!),
            "Erased chief full JSON restore failed.");
        var erasedState = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(erasedRecovered)!).CurrentBattleState;
        var erased = erasedState.GetBoard(controller).GetOccupant(restored.Position)!;
        Require(erased.IsErasure && erased.Attack == printedAttack && erased.OriginalAttack == printedAttack,
            "Reconnect reactivated an erased current-gold effect or changed the upgraded baseline.");
    }

    private static void SetGold(ResourceSet resources, int gold)
    {
        resources.Spend(new ResourceSet(0, 0, 0, resources.Gold)); resources.Add(new ResourceSet(0, 0, 0, gold));
    }

    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "chief-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
