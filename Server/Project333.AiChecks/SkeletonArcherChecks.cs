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

internal static class SkeletonArcherChecks
{
    private const string CardId = "SkeletonArcher";
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Card database rejected Skeleton Archer.");
        var record = database.Cards.Single(card => card.Id == CardId);
        var definition = (UnitCardDefinition)provider.GetRequired(CardId);
        Require(record.DisplayName == "스켈레톤아처" && record.Rarity == CardRarity.Common &&
            record.Affiliation == CardAffiliation.Fantasy && record.IncludeInDraft && record.IncludeInRewards &&
            record.Races.SequenceEqual(new[] { UnitRace.Undead }) && string.IsNullOrEmpty(record.EffectText) &&
            string.IsNullOrEmpty(record.SpecialEffectText), "Skeleton Archer metadata/effect/race/pool flags differ.");
        Require(definition.Cost.Mana == 1 && definition.Cost.Gold + definition.Cost.Qi + definition.Cost.Power == 0 &&
            definition.Attack == 10 && definition.Health == 3 && definition.AttackType == AttackType.Ranged &&
            definition.DamageType == DamageType.Physical && definition.PhysicalDefense == 0 && definition.MagicDefense == 0 &&
            definition.CanMove && definition.HitsPerAttack == 1 && definition.MaxAttacksPerTurn == 1 &&
            !definition.HasRush && !definition.HasReplicate && !definition.HasBerserker && !definition.HasEndure &&
            !definition.HasShielder && !definition.HasLifeSteal && !definition.HasHiding && !definition.HasFlying &&
            !definition.HasPiercing && CardUpgradeRules.IsCardUpgradeable(definition), "Skeleton Archer printed rules differ.");
        CheckActualPools(database, record);
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            for (var level = 0; level <= 13; level++) CheckActualSession(provider, owner, level);
        Console.WriteLine("PASS SkeletonArcher: actual Common draft/reward pools, both seats Lv.0-13 exact hand/mana/summon/display, ranged Physical attack without counter, Undead/Erasure, full JSON participant upgrades/identity/combat buffs/view restoration.");
    }

    private static void CheckActualPools(JsonCardDefinitionDatabase database, JsonCardDefinitionRecord record)
    {
        var generator = new ServerDraftOfferGenerator(database);
        var actualDraftPool = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator)
            .GetField("_draftCards", Hidden)!.GetValue(generator)!;
        Require(actualDraftPool.Single(card => card.Id == CardId).Rarity == CardRarity.Common,
            "Actual draft pool omitted the Common Skeleton Archer.");
        Require(Enumerable.Range(0, 512).Any(seed => generator.CreateOffer(seed,
            new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId)),
            "Actual draft offers never selected Skeleton Archer.");
        var isRewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)isRewardable.Invoke(null, new object[] { record })!, "Server reward filter rejected Skeleton Archer.");
        var rewardMethod = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)rewardMethod.Invoke(null, new object[] { run, rewardPool })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random card reward creation omitted Skeleton Archer.");
        var rewardCount = database.Cards.Count(card => (bool)isRewardable.Invoke(null, new object[] { card })!);
        var upgrades = database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()));
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {actualDraftPool.Count}, upgradeable {upgrades}, actual rewards {rewardCount}.");
    }

    private static void CheckActualSession(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var attackGain = new[] { 3, 6, 9, 13 }.Count(milestone => level >= milestone);
        var attack = 10 + attackGain;
        var hp = 3 + level - attackGain;
        var enemy = owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        var enemyLevel = 13 - level;
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider();
        levels.SetUpgradeLevel(owner, CardId, level); levels.SetUpgradeLevel(enemy, CardId, enemyLevel);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession("skeleton-archer-" + owner + "-" + level, false);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        var unused = state.GetPlayer(owner).Hand.Add(CardId); var chosen = state.GetPlayer(owner).Hand.Add(CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(1, 0, 0, 0));
        var mana = state.GetPlayer(owner).Resources.Mana;
        var events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.PlayUnitCard, ExpectedTurnNumber = state.TurnNumber,
            CardId = CardId, HandCardRuntimeId = chosen.RuntimeId, HasTarget = true, TargetOwnerId = Dto(owner),
            TargetCoord = new TileCoordDto { Column = 0, Row = 0 }
        });
        var unit = (UnitState)state.GetBoard(owner).GetOccupant(new TileCoord(0, 0))!;
        Require(unit.CardId == CardId && unit.Attack == attack && unit.OriginalAttack == attack &&
            unit.MaxHp == hp && unit.OriginalMaxHp == hp && unit.CurrentHp == hp &&
            unit.AttackType == AttackType.Ranged && unit.DamageType == DamageType.Physical &&
            unit.Races.SequenceEqual(new[] { UnitRace.Undead }) && unit.HasSummoningSickness && unit.CanMove,
            $"{owner} Lv.{level}: actual summoned combat/upgrade/race differs.");
        Require(state.GetPlayer(owner).Resources.Mana == mana - 1 &&
            !state.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId) &&
            state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId), "Mana or exact selected hand copy differs.");
        var played = events.Single(evt => evt.EventType == BattleEventType.CardPlayed && evt.CardId == CardId);
        Require(played.CardAttack == attack && played.CardMaxHp == hp, "Authoritative played-card display differs.");
        DamageResolutionRules.ApplyEffectDamage(unit, 1, DamageType.Fixed);
        var meleeTarget = new UnitState("melee-target", "fixture", enemy, new TileCoord(0, 0), AttackType.Melee,
            200, 100, true, false, 0, physicalDefense: 2);
        meleeTarget.HasSummoningSickness = false; state.GetBoard(enemy).Place(meleeTarget.Position, meleeTarget);
        unit.HasSummoningSickness = false;
        events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.Attack, ExpectedTurnNumber = state.TurnNumber,
            OccupantRuntimeId = unit.RuntimeId, SourceCoord = new TileCoordDto { Column = 0, Row = 0 }, HasTarget = true,
            TargetOwnerId = Dto(enemy), TargetCoord = new TileCoordDto { Column = 0, Row = 0 }
        });
        var damagePackets = events.Where(evt => evt.EventType == BattleEventType.DamageApplied).ToArray();
        Require(events.Any(evt => evt.EventType == BattleEventType.AttackStarted) &&
            meleeTarget.CurrentHp == 100 - (attack - 2) && unit.CurrentHp == hp - 1 && unit.RemainingAttacksThisTurn == 0 &&
            damagePackets.Length == 1 && damagePackets[0].ValueCause == BattleValueChangeCause.NormalAttack &&
            damagePackets[0].SourceRuntimeId == unit.RuntimeId && damagePackets[0].TargetRuntimeId == meleeTarget.RuntimeId &&
            damagePackets[0].Amount == attack - 2 && damagePackets[0].HpBefore == 100 &&
            damagePackets[0].HpAfter == 100 - (attack - 2),
            "Actual ranged Physical wire packet or absence of melee counter differs.");
        unit.ApplyCombatStatBuff("fixture-buff", attackBonus: 2, maxHpBonus: 4);
        DamageResolutionRules.ApplyEffectDamage(unit, 4, DamageType.Magic);
        var snapshot = session.CreatePersistenceSnapshot("skeleton-archer-recovery") with
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
            restored.MaxHp == hp + 4 && restored.CurrentHp == hp - 1 && restored.OriginalAttack == attack && restored.OriginalMaxHp == hp &&
            restored.Races.SequenceEqual(new[] { UnitRace.Undead }) && restored.AttackType == AttackType.Ranged &&
            restored.DamageType == DamageType.Physical && restored.RemainingAttacksThisTurn == 0 && restored.CombatStatBuffs.Count == 1,
            "Restored identity, combat buffs, account baseline, race or spent attack differs.");
        Require(restoredState.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId) &&
            !restoredState.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId), "Full JSON restore changed hand-copy identity.");
        var resaved = restoredSession.CreatePersistenceSnapshot("skeleton-archer-resaved");
        foreach (var seat in new[] { PlayerId.Player, PlayerId.AI })
            Require(resaved.Participants!.Single(participant => participant.RuntimeSeatId == Dto(seat).ToString()).CardUpgradeLevels[CardId] ==
                (seat == owner ? level : enemyLevel), "Participant account upgrade did not round-trip.");
        var view = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restoredState, snapshot.MatchId, enemy);
        var viewReceived = JsonSerializer.Deserialize<Project333.Runtime.Application.Online.BattleStateViewDto>(JsonSerializer.Serialize(view))!;
        var viewed = viewReceived.Occupants.Single(occupant => occupant.RuntimeId == restored.RuntimeId);
        Require(viewReceived.ViewerId == enemy && viewed.OwnerId == owner && viewed.Attack == attack + 2 &&
            viewed.CurrentHp == hp - 1 && viewed.MaxHp == hp + 4 && viewed.OriginalAttack == attack &&
            viewed.OriginalMaxHp == hp && viewed.Races.SequenceEqual(new[] { UnitRace.Undead }) &&
            viewed.AttackType == AttackType.Ranged && viewed.DamageType == DamageType.Physical,
            "Opposite-viewer JSON view lost identity/stats/race.");
        Require(viewReceived.Opponent.VisibleHandCardIds.Count == 0, "Opponent hand was exposed.");
        new ErasureService().Apply(restoredState, restored);
        Require(restored.IsErasure && restored.Attack == attack && restored.MaxHp == hp && restored.CurrentHp == hp - 1 &&
            restored.HasRace(UnitRace.Undead) && restored.AttackType == AttackType.Ranged && restored.CombatStatBuffs.Count == 0,
            "Erasure changed upgraded baseline, race, ranged rules or damaged HP.");
    }

    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "archer-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
