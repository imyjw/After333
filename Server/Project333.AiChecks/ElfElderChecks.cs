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

internal static class ElfElderChecks
{
    private const string CardId = "ElfElder";
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Card database rejected ElfElder.");
        var record = database.Cards.Single(card => card.Id == CardId);
        var definition = (UnitCardDefinition)provider.GetRequired(CardId);
        Require(record.DisplayName == "엘프 장로" && record.Rarity == CardRarity.Rare &&
            record.Affiliation == CardAffiliation.Fantasy && record.IncludeInDraft && record.IncludeInRewards &&
            record.Races.SequenceEqual(new[] { UnitRace.Elf }) && string.IsNullOrEmpty(record.EffectText) &&
            record.SpecialEffectText == "관통\n속공", "ElfElder metadata/effect/race/pool flags differ.");
        Require(definition.Cost.Mana == 5 && definition.Cost.Gold + definition.Cost.Qi + definition.Cost.Power == 0 &&
            definition.Attack == 30 && definition.Health == 25 && definition.AttackType == AttackType.Ranged &&
            definition.DamageType == DamageType.Physical && definition.PhysicalDefense == 0 && definition.MagicDefense == 0 &&
            definition.CanMove && definition.HitsPerAttack == 1 && definition.MaxAttacksPerTurn == 1 &&
            definition.HasRush && !definition.HasReplicate && !definition.HasBerserker && !definition.HasEndure &&
            !definition.HasShielder && !definition.HasLifeSteal && !definition.HasHiding && !definition.HasFlying &&
            definition.HasPiercing && CardUpgradeRules.IsCardUpgradeable(definition), "ElfElder printed rules differ.");
        CheckActualPools(database, record);
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            for (var level = 0; level <= 13; level++)
                foreach (var targetRow in new[] { 0, 1 }) CheckActualSession(provider, owner, level, targetRow);
        Console.WriteLine("PASS ElfElder: actual Rare draft/reward pools, both seats Lv.0-13 exact hand/mana/summon/display, immediate Rush attack on either row, Physical Piercing/no counter, Elf/Erasure, full JSON participant upgrades/identity/combat buffs/view restoration.");
    }

    private static void CheckActualPools(JsonCardDefinitionDatabase database, JsonCardDefinitionRecord record)
    {
        var generator = new ServerDraftOfferGenerator(database);
        var actualDraftPool = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator)
            .GetField("_draftCards", Hidden)!.GetValue(generator)!;
        Require(actualDraftPool.Single(card => card.Id == CardId).Rarity == CardRarity.Rare,
            "Actual draft pool omitted the Rare ElfElder.");
        Require(Enumerable.Range(0, 512).Any(seed => generator.CreateOffer(seed,
            new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId)),
            "Actual draft offers never selected ElfElder.");
        var isRewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)isRewardable.Invoke(null, new object[] { record })!, "Server reward filter rejected ElfElder.");
        var rewardMethod = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)rewardMethod.Invoke(null, new object[] { run, rewardPool })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random card reward creation omitted ElfElder.");
        var rewardCount = database.Cards.Count(card => (bool)isRewardable.Invoke(null, new object[] { card })!);
        var upgrades = database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()));
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {actualDraftPool.Count}, upgradeable {upgrades}, actual rewards {rewardCount}.");
    }

    private static void CheckActualSession(ICardDefinitionProvider provider, PlayerId owner, int level, int targetRow)
    {
        var attackGain = new[] { 3, 6, 9, 13 }.Count(milestone => level >= milestone);
        var attack = 30 + attackGain;
        var hp = 25 + level - attackGain;
        var enemy = owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        var enemyLevel = 13 - level;
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider();
        levels.SetUpgradeLevel(owner, CardId, level); levels.SetUpgradeLevel(enemy, CardId, enemyLevel);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession("elf-elder-" + owner + "-" + level + "-" + targetRow, false);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        var unused = state.GetPlayer(owner).Hand.Add(CardId); var chosen = state.GetPlayer(owner).Hand.Add(CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(5, 0, 0, 0));
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
            unit.Races.SequenceEqual(new[] { UnitRace.Elf }) && !unit.HasSummoningSickness && unit.WasSummonedThisTurn && unit.HasRush && unit.HasActivePiercing && unit.CanMove,
            $"{owner} Lv.{level}: actual summoned combat/upgrade/race differs.");
        Require(state.GetPlayer(owner).Resources.Mana == mana - 5 &&
            !state.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId) &&
            state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId), "Mana or exact selected hand copy differs.");
        var played = events.Single(evt => evt.EventType == BattleEventType.CardPlayed && evt.CardId == CardId);
        Require(played.CardAttack == attack && played.CardMaxHp == hp, "Authoritative played-card display differs.");
        DamageResolutionRules.ApplyEffectDamage(unit, 1, DamageType.Fixed);
        var front = new UnitState("elder-front", "fixture", enemy, new TileCoord(1, 0), AttackType.Melee,
            200, 100, true, false, 0, physicalDefense: 2);
        var rear = new UnitState("elder-rear", "fixture", enemy, new TileCoord(1, 1), AttackType.Melee,
            200, 100, true, false, 0, physicalDefense: 5);
        state.GetBoard(enemy).Place(front.Position, front); state.GetBoard(enemy).Place(rear.Position, rear);
        var direct = targetRow == 0 ? front : rear; var pierced = targetRow == 0 ? rear : front;
        Require(unit.WasSummonedThisTurn && !unit.HasSummoningSickness, "Rush must permit this immediate summon-turn attack without fixture overrides.");
        events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.Attack, ExpectedTurnNumber = state.TurnNumber,
            OccupantRuntimeId = unit.RuntimeId, SourceCoord = new TileCoordDto { Column = 0, Row = 0 }, HasTarget = true,
            TargetOwnerId = Dto(enemy), TargetCoord = new TileCoordDto { Column = 1, Row = targetRow }
        });
        var damagePackets = events.Where(evt => evt.EventType == BattleEventType.DamageApplied).ToArray();
        Require(events.Any(evt => evt.EventType == BattleEventType.AttackStarted) &&
            front.CurrentHp == 100 - (attack - 2) && rear.CurrentHp == 100 - (attack - 5) &&
            unit.CurrentHp == hp - 1 && unit.RemainingAttacksThisTurn == 0 && damagePackets.Length == 2,
            "Actual immediate ranged Physical attack or absence of a counter differs.");
        foreach (var victim in new[] { direct, pierced })
        {
            var packet = damagePackets.Single(evt => evt.TargetRuntimeId == victim.RuntimeId);
            Require(packet.ValueCause == (victim == direct ? BattleValueChangeCause.NormalAttack : BattleValueChangeCause.Piercing) &&
                packet.SourceRuntimeId == unit.RuntimeId && packet.Amount == attack - victim.PhysicalDefense &&
                packet.HpBefore == 100 && packet.HpAfter == 100 - (attack - victim.PhysicalDefense),
                "Direct/Piercing wire packet lost its own defense, cause, source identity or HP.");
        }
        unit.ApplyCombatStatBuff("fixture-buff", attackBonus: 2, maxHpBonus: 4);
        DamageResolutionRules.ApplyEffectDamage(unit, 4, DamageType.Magic);
        var snapshot = session.CreatePersistenceSnapshot("elf-elder-recovery") with
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
            restored.Races.SequenceEqual(new[] { UnitRace.Elf }) && restored.AttackType == AttackType.Ranged &&
            restored.DamageType == DamageType.Physical && restored.HasRush && restored.HasActivePiercing && !restored.HasSummoningSickness &&
            restored.WasSummonedThisTurn && restored.RemainingAttacksThisTurn == 0 && restored.CombatStatBuffs.Count == 1,
            "Restored identity, combat buffs, account baseline, race or spent attack differs.");
        Require(restoredState.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId) &&
            !restoredState.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId), "Full JSON restore changed hand-copy identity.");
        var resaved = restoredSession.CreatePersistenceSnapshot("elf-elder-resaved");
        foreach (var seat in new[] { PlayerId.Player, PlayerId.AI })
            Require(resaved.Participants!.Single(participant => participant.RuntimeSeatId == Dto(seat).ToString()).CardUpgradeLevels[CardId] ==
                (seat == owner ? level : enemyLevel), "Participant account upgrade did not round-trip.");
        var view = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restoredState, snapshot.MatchId, enemy);
        var viewReceived = JsonSerializer.Deserialize<Project333.Runtime.Application.Online.BattleStateViewDto>(JsonSerializer.Serialize(view))!;
        var viewed = viewReceived.Occupants.Single(occupant => occupant.RuntimeId == restored.RuntimeId);
        Require(viewReceived.ViewerId == enemy && viewed.OwnerId == owner && viewed.Attack == attack + 2 &&
            viewed.CurrentHp == hp - 1 && viewed.MaxHp == hp + 4 && viewed.OriginalAttack == attack &&
            viewed.OriginalMaxHp == hp && viewed.Races.SequenceEqual(new[] { UnitRace.Elf }) &&
            viewed.AttackType == AttackType.Ranged && viewed.DamageType == DamageType.Physical && viewed.HasRush && viewed.HasPiercing &&
            !viewed.HasSummoningSickness && viewed.RemainingAttacksThisTurn == 0,
            "Opposite-viewer JSON view lost identity/stats/race.");
        Require(viewReceived.Opponent.VisibleHandCardIds.Count == 0, "Opponent hand was exposed.");
        new ErasureService().Apply(restoredState, restored);
        Require(restored.IsErasure && restored.Attack == attack && restored.MaxHp == hp && restored.CurrentHp == hp - 1 &&
            restored.HasRace(UnitRace.Elf) && restored.AttackType == AttackType.Ranged && restored.CombatStatBuffs.Count == 0 &&
            restored.HasRush && restored.HasPiercing && !restored.HasActivePiercing && restored.HasSummoningSickness,
            "Erasure changed upgraded baseline/race/damaged HP or failed to suppress Rush/Piercing.");
    }

    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "elder-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
