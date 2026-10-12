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

internal static class ManaCannonChecks
{
    private const string CardId = ManaCannonRules.CardId;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        Require(CardDatabaseValidator.Validate(database).Errors.Count == 0, "Card database rejected Mana Cannon.");
        var record = database.Cards.Single(card => card.Id == CardId);
        var definition = (UnitCardDefinition)provider.GetRequired(CardId);
        Require(record.DisplayName == "마나 대포" && record.Rarity == CardRarity.Uncommon && record.Affiliation == CardAffiliation.Fantasy &&
            record.DefinitionType == JsonCardDefinitionKind.Unit && record.IncludeInDraft && record.IncludeInRewards &&
            record.Races.Length == 0 && record.EffectText == ManaCannonRules.EffectDescription && string.IsNullOrEmpty(record.SpecialEffectText),
            "Mana Cannon metadata, attack cost or explicit no-race differs.");
        Require(definition.Cost.Mana == 2 && definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold == 0 &&
            definition.Attack == 40 && definition.Health == 10 && definition.AttackType == AttackType.Ranged && definition.DamageType == DamageType.Magic &&
            definition.PhysicalDefense == 0 && definition.MagicDefense == 0 && definition.CanMove && definition.Races.Count == 0 &&
            definition.HitsPerAttack == 1 && definition.MaxAttacksPerTurn == 1 && !definition.HasRush && !definition.HasReplicate &&
            !definition.HasBerserker && !definition.HasEndure && !definition.HasShielder && !definition.HasLifeSteal && !definition.HasHiding &&
            !definition.HasFlying && !definition.HasPiercing && CardUpgradeRules.IsCardUpgradeable(definition), "Mana Cannon printed rules differ.");
        CheckPools(database, record);
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            for (var level = 0; level <= 13; level++)
                foreach (var goldPayment in new[] { false, true }) CheckSession(provider, owner, level, goldPayment);
            CheckTransfer(provider, owner);
        }
        CheckActualServerAi(provider);
        Console.WriteLine("PASS ManaCannon: 56 actual both-seat x Lv.0-13 x Mana/Gold summons and ranged Magic attacks, exact chosen hand, no counter, attack affordability/retry, current-owner Gu payment, Erasure/Drained/Seal, full JSON participant upgrades/identity/races/buffs/resource/view recovery and actual server AI.");
    }

    private static void CheckPools(JsonCardDefinitionDatabase database, JsonCardDefinitionRecord record)
    {
        var generator = new ServerDraftOfferGenerator(database);
        var draft = (IReadOnlyList<JsonCardDefinitionRecord>)typeof(ServerDraftOfferGenerator).GetField("_draftCards", Hidden)!.GetValue(generator)!;
        Require(draft.Single(card => card.Id == CardId).Rarity == CardRarity.Uncommon, "Actual Uncommon draft pool omitted Mana Cannon.");
        Require(Enumerable.Range(0, 512).Any(seed => generator.CreateOffer(seed,
            new[] { generator.CreateOffer(seed, Array.Empty<string>())[0] }).Contains(CardId)), "Actual draft offer never selected Mana Cannon.");
        var rewardable = typeof(RunStartService).GetMethod("IsRewardableCard", BindingFlags.NonPublic | BindingFlags.Static)!;
        Require((bool)rewardable.Invoke(null, new object[] { record })!, "Server reward filter rejected Mana Cannon.");
        var create = typeof(RunStartService).GetMethod("CreateRandomRunCardRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var run = new RunSummaryDto(Guid.NewGuid(), "Completed", "Normal", 1, 3, 1, DateTimeOffset.UtcNow, null);
        var rewards = (IReadOnlyDictionary<string, int>)create.Invoke(null, new object[] { run,
            new JsonCardDefinitionDatabase { Cards = new List<JsonCardDefinitionRecord> { record } } })!;
        Require(rewards.Count == 1 && rewards[CardId] == 3, "Actual random reward creation omitted Mana Cannon.");
        var rewardCount = database.Cards.Count(card => (bool)rewardable.Invoke(null, new object[] { card })!);
        Console.WriteLine($"PASS inventory: definitions {database.Cards.Count}, actual draft {draft.Count}, upgradeable {database.Cards.Count(card => CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()))}, actual rewards {rewardCount}.");
    }

    private static void CheckSession(ICardDefinitionProvider provider, PlayerId owner, int level, bool goldPayment)
    {
        var gain = new[] { 3, 6, 9, 13 }.Count(milestone => level >= milestone); var attack = 40 + gain; var hp = 10 + level - gain;
        var state = State(owner); var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, CardId, level);
        var session = Session(provider, state, levels, "mana-cannon-" + owner + "-" + level + "-" + goldPayment);
        SetResources(state.GetPlayer(owner).Resources, 2, 0);
        var unused = state.GetPlayer(owner).Hand.Add(CardId); var chosen = state.GetPlayer(owner).Hand.Add(CardId);
        var events = session.ApplyCommand(new ClientBattleCommandMessage { ActorId = Dto(owner), CommandType = OnlineBattleCommandType.PlayUnitCard,
            ExpectedTurnNumber = state.TurnNumber, CardId = CardId, HandCardRuntimeId = chosen.RuntimeId, HasTarget = true,
            TargetOwnerId = Dto(owner), TargetCoord = Coord(new TileCoord(0, 0)) });
        var cannon = (UnitState)state.GetBoard(owner).GetOccupant(new TileCoord(0, 0))!;
        Require(cannon.Attack == attack && cannon.OriginalAttack == attack && cannon.MaxHp == hp && cannon.OriginalMaxHp == hp &&
            cannon.CurrentHp == hp && cannon.Races.Count == 0 && cannon.CanMove && cannon.HasSummoningSickness &&
            cannon.DamageType == DamageType.Magic && cannon.AttackType == AttackType.Ranged, "Actual summon rules/upgraded baseline differ.");
        Require(state.GetPlayer(owner).Resources.Mana == 0 && state.GetPlayer(owner).Resources.Gold == 0 &&
            state.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId) && !state.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId),
            "Printed Mana2 cost or exact selected hand differs.");
        var played = events.Single(evt => evt.EventType == BattleEventType.CardPlayed && evt.CardId == CardId);
        Require(played.CardAttack == attack && played.CardMaxHp == hp && played.AttackBonus == gain, "Authoritative card stats differ.");
        cannon.HasSummoningSickness = false;
        var target = new UnitState("cannon-target", "fixture", state.GetOpponent(owner).Id, new TileCoord(0, 0), AttackType.Melee,
            9, 100, true, false, 0, physicalDefense: 40, magicDefense: 3); state.GetOpponentBoard(owner).Place(target.Position, target);
        var command = Attack(owner, cannon, target.OwnerId, target.Position, state.TurnNumber);
        var before = AiBattleStateCopy.PositionKey(state);
        ExpectRejected(() => session.ApplyCommand(command), "자원이 부족합니다.");
        Require(AiBattleStateCopy.PositionKey(state) == before && cannon.RemainingAttacksThisTurn == 1 && target.CurrentHp == 100 && state.LastAttackResolution == null,
            "Unaffordable authoritative attack changed resources, usage, HP or attack history.");
        state.GetPlayer(owner).Resources.Add(goldPayment ? new ResourceSet(0, 0, 0, 1) : new ResourceSet(1, 0, 0, 0));
        // A bad target must preserve the newly supplied attack payment for a later valid retry.
        before = AiBattleStateCopy.PositionKey(state);
        ExpectRejected(() => session.ApplyCommand(Attack(owner, cannon, target.OwnerId, new TileCoord(4, 0), state.TurnNumber)), "공격 대상 타일이 비어있습니다.");
        Require(AiBattleStateCopy.PositionKey(state) == before, "Invalid target consumed resources or usage.");
        events = session.ApplyCommand(command);
        var damage = events.Where(evt => evt.EventType == BattleEventType.DamageApplied).ToArray();
        Require(events.Count(evt => evt.EventType == BattleEventType.AttackStarted) == 1 && damage.Length == 1 &&
            damage[0].ValueCause == BattleValueChangeCause.NormalAttack && damage[0].DamageType == DamageType.Magic &&
            damage[0].Amount == attack - 3 && damage[0].SourceRuntimeId == cannon.RuntimeId && damage[0].TargetRuntimeId == target.RuntimeId &&
            damage[0].HpBefore == 100 && damage[0].HpAfter == target.CurrentHp && target.CurrentHp == 100 - attack + 3 &&
            cannon.CurrentHp == hp && cannon.RemainingAttacksThisTurn == 0 && state.GetPlayer(owner).Resources.Mana + state.GetPlayer(owner).Resources.Gold == 0,
            "Authoritative paid ranged magic packet/no-counter/resource state differs.");
        cannon.ApplyCombatStatBuff("fixture", attackBonus: 2, maxHpBonus: 4); DamageResolutionRules.ApplyEffectDamage(cannon, 4, DamageType.Fixed);
        state.GetPlayer(owner).Resources.Add(new ResourceSet(1, 0, 0, 2));
        var snapshot = session.CreatePersistenceSnapshot("mana-cannon-recovery") with { Participants = new[] { Participant(PlayerId.Player, owner == PlayerId.Player ? level : 0),
            Participant(PlayerId.AI, owner == PlayerId.AI ? level : 0) } };
        var restoredSession = Recover(snapshot); var restoredState = GetState(restoredSession); var restored = (UnitState)restoredState.GetBoard(owner).GetOccupant(cannon.Position)!;
        Require(restored.RuntimeId == cannon.RuntimeId && restored.BaseAttack == attack + 2 && restored.OriginalAttack == attack && restored.MaxHp == hp + 4 &&
            restored.OriginalMaxHp == hp && restored.CurrentHp == hp && restored.RemainingAttacksThisTurn == 0 && restored.Races.Count == 0 &&
            restored.DamageType == DamageType.Magic && restored.AttackType == AttackType.Ranged && restored.CombatStatBuffs.Count == 1 &&
            restoredState.GetPlayer(owner).Resources.Mana == 1 && restoredState.GetPlayer(owner).Resources.Gold == 2,
            "Full JSON state lost upgrade baseline, identity, no-race, combat buff, spent attack or resources.");
        var wire = new Project333.Runtime.Application.Online.BattleStateViewFactory().CreateForPlayer(restoredState, snapshot.MatchId, target.OwnerId);
        var received = JsonSerializer.Deserialize<Project333.Runtime.Application.Online.BattleStateViewDto>(JsonSerializer.Serialize(wire))!;
        var occupant = received.Occupants.Single(unit => unit.RuntimeId == cannon.RuntimeId);
        Require(occupant.Attack == attack + 2 && occupant.OriginalAttack == attack && occupant.Races.Length == 0 && occupant.RemainingAttacksThisTurn == 0 &&
            received.Opponent.Resources.Mana == 1 && received.Opponent.Resources.Gold == 2 && received.Opponent.VisibleHandCardIds.Count == 0,
            "Opposite-viewer state lost attack-cost resources/stat/race or exposed the hand.");
        Require(restoredState.GetPlayer(owner).Hand.Contains(CardId, unused.RuntimeId) && !restoredState.GetPlayer(owner).Hand.Contains(CardId, chosen.RuntimeId),
            "Full JSON hand copy identity changed.");
        var resaved = restoredSession.CreatePersistenceSnapshot("mana-cannon-resaved");
        Require(resaved.Participants!.Single(participant => participant.RuntimeSeatId == Dto(owner).ToString()).CardUpgradeLevels[CardId] == level,
            "Account upgrade level did not round-trip.");
        restored.HasSummoningSickness = false; restored.RemainingAttacksThisTurn = 1;
        var restoredTarget = restoredState.GetOpponentBoard(owner).GetOccupant(target.Position)!;
        restoredTarget.CurrentHp = 100;
        restoredSession.ApplyCommand(Attack(owner, restored, restoredTarget.OwnerId, restoredTarget.Position, restoredState.TurnNumber));
        Require(restoredState.GetPlayer(owner).Resources.Mana == 0 && restoredState.GetPlayer(owner).Resources.Gold == 2,
            "Reconnect attack did not spend native mana first or spent extra gold.");
        restored.RemainingAttacksThisTurn = 1; restoredTarget.CurrentHp = 100;
        new ErasureService().Apply(restoredState, restored); restored.HasSummoningSickness = false;
        SetResources(restoredState.GetPlayer(owner).Resources, 0, 0);
        restoredSession.ApplyCommand(Attack(owner, restored, restoredTarget.OwnerId, restoredTarget.Position, restoredState.TurnNumber));
        Require(restored.IsErasure && restored.Attack == attack && restored.Races.Count == 0 && restoredTarget.CurrentHp == 100 - attack + 3 &&
            restoredState.GetPlayer(owner).Resources.Mana + restoredState.GetPlayer(owner).Resources.Gold == 0,
            "Erasure did not suppress the attack-cost card effect while retaining printed stats.");
    }

    private static void CheckTransfer(ICardDefinitionProvider provider, PlayerId previousOwner)
    {
        var state = State(previousOwner); var controller = state.GetOpponent(previousOwner).Id;
        var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(previousOwner, CardId, 13);
        var session = Session(provider, state, levels, "mana-cannon-gu-" + previousOwner);
        SetResources(state.GetPlayer(previousOwner).Resources, 2, 0); var hand = state.GetPlayer(previousOwner).Hand.Add(CardId);
        session.ApplyCommand(new ClientBattleCommandMessage { ActorId = Dto(previousOwner), CommandType = OnlineBattleCommandType.PlayUnitCard,
            ExpectedTurnNumber = state.TurnNumber, CardId = CardId, HandCardRuntimeId = hand.RuntimeId, HasTarget = true,
            TargetOwnerId = Dto(previousOwner), TargetCoord = Coord(new TileCoord(0, 0)) });
        var cannon = (UnitState)state.GetBoard(previousOwner).GetOccupant(new TileCoord(0, 0))!;
        SetResources(state.GetPlayer(previousOwner).Resources, 3, 2); SetResources(state.GetPlayer(controller).Resources, 0, 1);
        state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 10, 0, 0)); var gu = state.GetPlayer(controller).Hand.Add(GuRules.CardId);
        state.SetActivePlayer(controller);
        session.ApplyCommand(new ClientBattleCommandMessage { ActorId = Dto(controller), CommandType = OnlineBattleCommandType.CastScriptedSpell,
            ExpectedTurnNumber = state.TurnNumber, CardId = GuRules.CardId, HandCardRuntimeId = gu.RuntimeId, HasTarget = true,
            TargetOwnerId = Dto(previousOwner), TargetCoord = Coord(cannon.Position) });
        // Gu correctly leaves a non-Rush unit with zero attacks on its transfer turn.
        // This fixture now observes the later eligible attack window, without granting Rush.
        cannon.HasSummoningSickness = false; cannon.RemainingAttacksThisTurn = 1;
        var target = new UnitState("gu-target", "fixture", previousOwner, new TileCoord(0, 0), AttackType.Melee, 9, 100, true, false, 0);
        state.GetBoard(previousOwner).Place(target.Position, target);
        cannon.IsDrained = true;
        ExpectRejected(() => session.ApplyCommand(Attack(controller, cannon, previousOwner, target.Position, state.TurnNumber)), "이 유닛을 공격 불가능한 상태입니다.");
        Require(state.GetPlayer(controller).Resources.Gold == 1 && cannon.RemainingAttacksThisTurn == 1, "Drained rejection consumed payment.");
        cannon.IsDrained = false; cannon.EnterSealbound(1);
        ExpectRejected(() => session.ApplyCommand(Attack(controller, cannon, previousOwner, target.Position, state.TurnNumber)), "이 유닛을 공격 불가능한 상태입니다.");
        Require(state.GetPlayer(controller).Resources.Gold == 1, "Sealbound rejection consumed payment.");
        cannon.RestoreSuppressionStates(false, false); cannon.HasSummoningSickness = false; cannon.RemainingAttacksThisTurn = 1;
        session.ApplyCommand(Attack(controller, cannon, previousOwner, target.Position, state.TurnNumber));
        Require(cannon.OwnerId == controller && cannon.Attack == 44 && cannon.MaxHp == 19 && target.CurrentHp == 56 &&
            state.GetPlayer(controller).Resources.Gold == 0 && state.GetPlayer(previousOwner).Resources.Mana == 3 && state.GetPlayer(previousOwner).Resources.Gold == 2,
            "Gu attack used the prior owner's payment or baseline.");
        var snapshot = session.CreatePersistenceSnapshot("mana-cannon-transferred"); var recovered = Recover(snapshot); var received = GetState(recovered);
        var transferred = (UnitState)received.GetBoard(controller).GetOccupant(cannon.Position)!;
        transferred.HasSummoningSickness = false; transferred.RemainingAttacksThisTurn = 1;
        var before = AiBattleStateCopy.PositionKey(received);
        ExpectRejected(() => recovered.ApplyCommand(Attack(controller, transferred, previousOwner, target.Position, received.TurnNumber)), "자원이 부족합니다.");
        Require(AiBattleStateCopy.PositionKey(received) == before && transferred.OriginalAttack == 44 && transferred.Races.Count == 0,
            "Transferred JSON restore forgot the attack payment or original summoner baseline.");
    }

    private static void CheckActualServerAi(ICardDefinitionProvider provider)
    {
        var state = State(PlayerId.AI); SetResources(state.AI.Resources, 1, 0); state.AI.Master.RemainingAttacksThisTurn = 0;
        while (state.AI.Hand.Count > 0) state.AI.Hand.Remove(state.AI.Hand.CardIds[0]);
        state.Player.Master.CurrentHp = 40;
        var cannon = new UnitState("server-ai-cannon", CardId, PlayerId.AI, new TileCoord(0, 0), AttackType.Ranged, 50, 10, true, false, 0, damageType: DamageType.Magic);
        cannon.HasSummoningSickness = false; state.AIBoard.Place(cannon.Position, cannon);
        var session = Session(provider, state, new InMemoryCardUpgradeLevelProvider(), "mana-cannon-actual-ai", true);
        typeof(BattleSession).GetField("_aiDecisionService", Hidden)!.SetValue(session, new AiDecisionService(provider));
        var events = session.RunServerAiActionIfNeeded();
        Require(state.IsEnded && state.Result.Winner == PlayerId.AI && state.AI.Resources.Mana == 0 && state.AI.Resources.Gold == 0 &&
            events.Any(evt => evt.EventType == BattleEventType.AttackStarted && evt.SourceCardId == CardId) &&
            events.Any(evt => evt.EventType == BattleEventType.DamageApplied && evt.Amount == 40 && evt.DamageType == DamageType.Magic),
            "Actual server AI did not execute the paid ranged magic winning attack.");
    }

    private static BattleSession Session(ICardDefinitionProvider provider, BattleState state, InMemoryCardUpgradeLevelProvider levels, string id, bool ai = false)
    {
        var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
        var session = new BattleSession(id, ai);
        typeof(BattleSession).GetField("_battleFlowController", Hidden)!.SetValue(session, flow);
        typeof(BattleSession).GetField("_cardDefinitionProvider", Hidden)!.SetValue(session, provider);
        typeof(BattleSession).GetField("_cardDisplayUpgradeLevels", Hidden)!.SetValue(session, levels); return session;
    }
    private static BattleState State(PlayerId owner)
    {
        var deck = Enumerable.Repeat("Goblin", 33).ToArray(); var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
        state.SetPhase(PhaseType.Main); return state;
    }
    private static BattleSession Recover(BattleSessionPersistenceSnapshot snapshot)
    {
        var recovered = new BattleSession(snapshot.MatchId, false);
        Require(recovered.RestoreFromPersistenceSnapshot(JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(snapshot))!), "Full JSON restore failed.");
        return recovered;
    }
    private static BattleState GetState(BattleSession session) => ((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController", Hidden)!.GetValue(session)!).CurrentBattleState;
    private static ClientBattleCommandMessage Attack(PlayerId actor, OccupantState attacker, PlayerId targetOwner, TileCoord target, int turn) => new()
    {
        ActorId = Dto(actor), CommandType = OnlineBattleCommandType.Attack, ExpectedTurnNumber = turn, OccupantRuntimeId = attacker.RuntimeId,
        SourceCoord = Coord(attacker.Position), HasTarget = true, TargetOwnerId = Dto(targetOwner), TargetCoord = Coord(target)
    };
    private static void SetResources(ResourceSet resources, int mana, int gold)
    {
        resources.Spend(new ResourceSet(resources.Mana, resources.Qi, resources.Power, resources.Gold)); resources.Add(new ResourceSet(mana, 0, 0, gold));
    }
    private static BattleSessionParticipantSnapshot Participant(PlayerId owner, int level) => new(Dto(owner).ToString(),
        owner == PlayerId.Player ? "PlayerA" : "PlayerB", "cannon-" + owner, "run-" + owner, "deck-" + owner,
        Enumerable.Repeat("Goblin", 33).ToArray(), new Dictionary<string, int> { [CardId] = level });
    private static TileCoordDto Coord(TileCoord coord) => new() { Column = coord.Column, Row = coord.Row };
    private static PlayerIdDto Dto(PlayerId owner) => owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI;
    private static void ExpectRejected(Action action, string expected)
    {
        try { action(); } catch (InvalidOperationException error) { Require(error.Message == expected, "Unexpected rejection: " + error.Message); return; }
        throw new Exception("Expected rejected action was accepted.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
