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

internal static class OrcChieftainChecks
{
    private const string CardId = "OrcChieftain";
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Canonical database validation failed.");
        var record = database.Cards.Single(card => card.Id == CardId);
        Require(record.Rarity == CardRarity.Rare && record.Affiliation == CardAffiliation.Fantasy &&
            record.IncludeInDraft && record.IncludeInRewards && record.Races.SequenceEqual(new[] { UnitRace.Orc }), "Metadata/release pools differ.");
        var generator = new ServerDraftOfferGenerator(database);
        Require(Enumerable.Range(0, 512).Any(seed => generator.CreateOffer(seed,
            new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId)), "Actual draft never offered the chief.");
        var isRewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)isRewardable.Invoke(null, new object[] { record })!, "Actual reward filter rejects the chief.");
        var draftPool = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator).GetField("_draftCards", Hidden)!.GetValue(generator)!;
        var upgradeCount = database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()));
        var rewardCount = database.Cards.Count(card => (bool)isRewardable.Invoke(null, new object[] { card })!);
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {draftPool.Count}, upgradeable {upgradeCount}, actual rewards {rewardCount}.");
        var rewardsMethod = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewards = (IReadOnlyDictionary<string, int>)rewardsMethod.Invoke(null, new object[]
            { run, new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } } })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random reward did not grant the chief.");
        var provider = database.CreateProvider();
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            for (var level = 0; level <= 13; level++) CheckSession(provider, owner, level);
        Console.WriteLine("PASS OrcChieftain: actual draft/rewards; both seats Lv0-13 exact hand/mana/self and stacked aura; later Orc summons; full JSON source, HP, erased-source and account-upgrade restoration; individual source destruction and HP-loss death chain.");
    }

    private static void CheckSession(ICardDefinitionProvider provider, PlayerId owner, int level)
    {
        var milestones = new[] { 3, 6, 9, 13 }.Count(value => level >= value);
        var attack = 50 + milestones;
        var hp = 70 + level - milestones;
        var deck = Enumerable.Repeat("Goblin", 33).ToArray();
        var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main);
        var levels = new InMemoryCardUpgradeLevelProvider();
        levels.SetUpgradeLevel(owner, CardId, level);
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels);
        flow.RestoreBattleState(state);
        var session = new BattleSession("chief-" + owner + "-" + level, false);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels);
        var unused = state.GetPlayer(owner).Hand.Add(CardId);
        var first = Summon(session, state, owner, CardId, 0, out var events);
        Require(first.OriginalAttack == attack && first.OriginalMaxHp == hp &&
            first.BaseAttack == attack + 10 && first.Attack == attack + 10 &&
            first.MaxHp == hp + 10 && first.CurrentHp == hp + 10 && first.Races.SequenceEqual(new[] { UnitRace.Orc }),
            $"{owner} Lv{level}: printed/self aura stats differ.");
        Require(state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId), "Another identical hand card was consumed.");
        var played = events.Single(e => e.EventType == BattleEventType.CardPlayed && e.CardId == CardId);
        Require(played.CardAttack == attack + 10 && played.CardMaxHp == hp + 10, "Wire summon values differ from authoritative self aura stats.");
        var orc = Summon(session, state, owner, "OrcBerserker", 3, out _);
        Require(orc.BaseAttack == 30 && orc.MaxHp == 60 && orc.CurrentHp == 60, "Later Orc missed the existing source aura.");
        var second = Summon(session, state, owner, CardId, 1, out _);
        Require(first.BaseAttack == attack + 20 && first.MaxHp == hp + 20 &&
            second.BaseAttack == attack + 20 && orc.BaseAttack == 40 && orc.MaxHp == 70, "Independent source auras do not stack.");
        orc.ApplyErasure();
        Require(orc.BaseAttack == 20 && orc.MaxHp == 50 && orc.CurrentHp == 50 &&
            orc.ErasedOrcChieftainSourceRuntimeIds.Count == 2, "Erasure did not preserve baseline/mark the two old sources.");
        var third = Summon(session, state, owner, CardId, 4, out _);
        Require(orc.BaseAttack == 30 && orc.MaxHp == 60 && orc.CurrentHp == 60 &&
            orc.CombatStatBuffs.Single().SourceRuntimeId == third.RuntimeId, "A new source after Erasure must apply normally.");
        var sourceIds = new[] { first.RuntimeId, second.RuntimeId, third.RuntimeId };
        var snapshot = session.CreatePersistenceSnapshot("chief-recovery") with
        {
            Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? level : 0),
                Participant(PlayerId.AI, owner == PlayerId.AI ? level : 0) }
        };
        var received = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!;
        // A restore cannot depend on the source tiles preceding their recipients in JSON.
        var domain = received.DomainState!;
        received = received with
        {
            DomainState = domain with
            {
                PlayerBoard = domain.PlayerBoard with { Occupants = domain.PlayerBoard.Occupants.Reverse().ToArray() },
                AIBoard = domain.AIBoard with { Occupants = domain.AIBoard.Occupants.Reverse().ToArray() }
            }
        };
        var restoredSession = new BattleSession(snapshot.MatchId, false);
        Require(restoredSession.RestoreFromPersistenceSnapshot(received), "Full JSON restoration failed.");
        var restored = ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(restoredSession)!).CurrentBattleState;
        var board = restored.GetBoard(owner);
        var restoredFirst = board.EnumerateOccupants().Single(unit => unit.RuntimeId == first.RuntimeId);
        var restoredThird = board.EnumerateOccupants().Single(unit => unit.RuntimeId == third.RuntimeId);
        var restoredOrc = board.EnumerateOccupants().Single(unit => unit.RuntimeId == orc.RuntimeId);
        Require(restoredFirst.OriginalAttack == attack && restoredFirst.OriginalMaxHp == hp &&
            restoredFirst.BaseAttack == attack + 30 && restoredFirst.MaxHp == hp + 30 && restoredFirst.CurrentHp == hp + 30 &&
            restoredFirst.CombatStatBuffs.Select(buff => buff.SourceRuntimeId).Order().SequenceEqual(sourceIds.Order()), "Stacked restored source IDs/stats doubled or disappeared.");
        Require(restoredOrc.IsErasure && restoredOrc.BaseAttack == 30 && restoredOrc.MaxHp == 60 && restoredOrc.CurrentHp == 60 &&
            restoredOrc.ErasedOrcChieftainSourceRuntimeIds.Order().SequenceEqual(new[] { first.RuntimeId, second.RuntimeId }.Order()), "Restored Erasure/source/HP differs.");
        var view = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restored, snapshot.MatchId, owner);
        var viewReceived = JsonSerializer.Deserialize<Project333.Runtime.Application.Online.BattleStateViewDto>(JsonSerializer.Serialize(view))!;
        var viewed = viewReceived.Occupants.Single(unit => unit.RuntimeId == restoredOrc.RuntimeId);
        Require(viewed.Attack == 30 && viewed.CurrentHp == 60 && viewed.CombatStatBuffs.Single().SourceRuntimeId == third.RuntimeId &&
            viewed.ErasedOrcChieftainSourceRuntimeIds.Count == 2, "Authoritative view lost per-source or erased-source data.");
        Require(restoredSession.CreatePersistenceSnapshot("chief-resaved").Participants!.Single(p => p.RuntimeSeatId == Dto(owner).ToString())
            .CardUpgradeLevels[CardId] == level, "Restored account upgrade differs.");
        OccupantDestructionService.DestroyOccupant(restored, owner, restoredFirst);
        Require(restoredOrc.BaseAttack == 30 && restoredOrc.MaxHp == 60 && restoredOrc.CurrentHp == 60, "Already erased source was removed twice.");
        var restoredSecond = board.EnumerateOccupants().Single(unit => unit.RuntimeId == second.RuntimeId);
        Require(restoredSecond.BaseAttack == attack + 20 && restoredSecond.MaxHp == hp + 20, "Destroying one source must retain exactly two contributions.");
        restoredOrc.CurrentHp = 5;
        OccupantDestructionService.DestroyOccupant(restored, owner, restoredThird);
        Require(board.GetOccupant(new TileCoord(3, 0)) == null, "Losing 10 current HP must resolve the resulting Orc death.");
        Require(restoredSecond.BaseAttack == attack + 10 && restoredSecond.MaxHp == hp + 10, "Only the surviving source contribution must remain.");
    }

    private static UnitState Summon(BattleSession session, BattleState state, PlayerId owner, string id, int column, out IReadOnlyList<BattleEventDto> events)
    {
        var selected = state.GetPlayer(owner).Hand.Add(id);
        var cost = id == CardId ? 7 : 5;
        state.GetPlayer(owner).Resources.Add(new ResourceSet(cost, 0, 0, 0));
        var mana = state.GetPlayer(owner).Resources.Mana;
        events = session.ApplyCommand(new ClientBattleCommandMessage
        {
            ActorId = Dto(owner), CommandType = OnlineBattleCommandType.PlayUnitCard,
            CardId = id, HandCardRuntimeId = selected.RuntimeId, HasTarget = true,
            TargetOwnerId = Dto(owner), TargetCoord = new TileCoordDto { Column = column, Row = 0 }
        });
        Require(state.GetPlayer(owner).Resources.Mana == mana - cost &&
            !state.GetPlayer(owner).Hand.Contains(id, selected.RuntimeId), "Exact hand/cost differs.");
        return (UnitState)state.GetBoard(owner).GetOccupant(new TileCoord(column, 0))!;
    }
    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(
        Dto(owner).ToString(), owner == PlayerId.Player ? "PlayerA" : "PlayerB", "chief-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
