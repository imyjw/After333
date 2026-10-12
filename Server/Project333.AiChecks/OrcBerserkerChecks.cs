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

internal static class OrcBerserkerChecks
{
    private const string CardId = "OrcBerserker";
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Card database rejected Orc Berserker.");
        var record = database.Cards.Single(c => c.Id == CardId);
        Require(record.Rarity == CardRarity.Uncommon && record.Affiliation == CardAffiliation.Fantasy &&
            record.IncludeInDraft && record.IncludeInRewards && record.Races.SequenceEqual(new[] { UnitRace.Orc }),
            "Orc Berserker metadata/races/pool flags differ.");
        var definition = (UnitCardDefinition)provider.GetRequired(CardId);
        Require(definition.Attack == 20 && definition.Health == 50 && definition.Cost.Mana == 5 &&
            definition.Cost.Gold == 0 && definition.Cost.Power == 0 && definition.Cost.Qi == 0 &&
            definition.HasBerserker && definition.HitsPerAttack == 1 && definition.MaxAttacksPerTurn == 1 &&
            definition.AttackType == AttackType.Melee && definition.DamageType == DamageType.Physical &&
            definition.PhysicalDefense == 0 && definition.MagicDefense == 0 && definition.CanMove &&
            !definition.HasRush && CardUpgradeRules.IsCardUpgradeable(definition),
            "Orc Berserker base definition differs.");
        var generator = new ServerDraftOfferGenerator(database);
        var offered = Enumerable.Range(0, 512).Any(seed =>
            generator.CreateOffer(seed, new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId));
        Require(offered, "Actual draft generator never offered Orc Berserker.");
        var isRewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)isRewardable.Invoke(null, new object[] { record })!, "Server reward filter rejected Orc Berserker.");
        var actualDraftPool = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator)
            .GetField("_draftCards", Hidden)!.GetValue(generator)!;
        var upgradeableCount = database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()));
        var rewardCount = database.Cards.Count(card => (bool)isRewardable.Invoke(null, new object[] { card })!);
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {actualDraftPool.Count}, upgradeable {upgradeableCount}, actual rewards {rewardCount}.");
        var rewardMethod = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewardPool = new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } };
        var rewards = (IReadOnlyDictionary<string, int>)rewardMethod.Invoke(null, new object[] { run, rewardPool })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random card rewards did not grant Orc Berserker.");
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            for (var level = 0; level <= 13; level++) CheckSummonAndRestore(provider, owner, level);
        Console.WriteLine("PASS OrcBerserker: actual Uncommon draft/reward pools, both seats Lv.0-13 exact hand/mana/summon/display, Berserker/heal/Erasure, Orc race and full JSON upgrade/state/view restoration.");
    }

    private static void CheckSummonAndRestore(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var atkGain = new[] { 3, 6, 9, 13 }.Count(milestone => level >= milestone);
        var expectedAttack = 20 + atkGain;
        var expectedHp = 50 + level - atkGain;
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider();
        levels.SetUpgradeLevel(owner, CardId, level);
        levels.SetUpgradeLevel(state.GetOpponent(owner).Id, CardId, level == 13 ? 0 : 13);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels);
        flow.RestoreBattleState(state);
        var session = new BattleSession("orc-berserker-" + owner + "-" + level, false);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        var unused = state.GetPlayer(owner).Hand.Add(CardId);
        var chosen = state.GetPlayer(owner).Hand.Add(CardId);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(5, 0, 0, 0));
        var manaBefore = state.GetPlayer(owner).Resources.Mana;
        var target = new TileCoord(0, 0);
        var events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.PlayUnitCard,
            CardId = CardId, HandCardRuntimeId = chosen.RuntimeId, HasTarget = true,
            TargetOwnerId = Dto(owner), TargetCoord = new TileCoordDto { Column = 0, Row = 0 }
        });
        var unit = (UnitState)state.GetBoard(owner).GetOccupant(target)!;
        Require(unit.CardId == CardId && unit.BaseAttack == expectedAttack && unit.MaxHp == expectedHp &&
            unit.CurrentHp == expectedHp && unit.HasBerserker && unit.Races.SequenceEqual(new[] { UnitRace.Orc }) &&
            unit.AttackType == AttackType.Melee && unit.DamageType == DamageType.Physical && unit.CanMove,
            $"{owner} Lv.{level}: actual summoned stats/traits differ.");
        Require(state.GetPlayer(owner).Resources.Mana == manaBefore - 5 &&
            !state.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId) &&
            state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId), "Cost or exact hand selection differs.");
        var played = events.Single(e => e.EventType == BattleEventType.CardPlayed && e.CardId == CardId);
        Require(played.CardAttack == expectedAttack && played.CardMaxHp == expectedHp, "Summon wire display differs from upgraded combat values.");
        unit.CurrentHp -= 7;
        Require(unit.Attack == expectedAttack + 7, "Berserker did not track seven lost HP.");
        var snapshot = session.CreatePersistenceSnapshot("orc-berserker-recovery") with
        {
            Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? level : 0),
                Participant(PlayerId.AI, owner == PlayerId.AI ? level : 0) }
        };
        var received = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(received), "Full JSON session restoration failed.");
        var restored = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(restoredSession)!).CurrentBattleState;
        var restoredUnit = (UnitState)restored.GetBoard(owner).GetOccupant(target)!;
        Require(restoredUnit.RuntimeId == unit.RuntimeId && restoredUnit.BaseAttack == expectedAttack &&
            restoredUnit.MaxHp == expectedHp && restoredUnit.CurrentHp == expectedHp - 7 &&
            restoredUnit.Attack == expectedAttack + 7 && restoredUnit.HasBerserker &&
            restoredUnit.Races.SequenceEqual(new[] { UnitRace.Orc }), "Restored race/upgrade/Berserker/HP differs.");
        var view = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restored, snapshot.MatchId, owner);
        var viewed = view.Occupants.Single(o => o.RuntimeId == restoredUnit.RuntimeId);
        Require(viewed.Attack == expectedAttack + 7 && viewed.Races.SequenceEqual(new[] { UnitRace.Orc }), "Authoritative card view lost attack or race.");
        Require(restoredSession.CreatePersistenceSnapshot("orc-resaved").Participants!.Single(p => p.RuntimeSeatId == Dto(owner).ToString())
            .CardUpgradeLevels[CardId] == level, "Participant level was not retained.");
        restoredUnit.Heal(3);
        Require(restoredUnit.Attack == expectedAttack + 4, "Healing did not reduce Berserker lost-HP bonus.");
        restoredUnit.ApplyErasure();
        Require(restoredUnit.Attack == expectedAttack && restoredUnit.HasRace(UnitRace.Orc) &&
            restoredUnit.CurrentHp == expectedHp - 4 && restoredUnit.MaxHp == expectedHp, "Erasure must suppress Berserker without changing race/upgrade/healing HP.");
    }
    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "orc-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
