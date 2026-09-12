using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiPlanningTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }
        private static UnitState Unit(BattleState state, PlayerId owner, TileCoord coord, int attack, int hp,
            AttackType type = AttackType.Melee, bool shielder = false, bool piercing = false,
            bool robot = false, int upkeep = 0, string id = "unit")
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), id, owner, coord, type, attack, hp, true,
                robot, upkeep, hasShielder: shielder, hasRobot: robot, hasPiercing: piercing);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(coord, unit);
            return unit;
        }
        private static AiDecisionService Planner(params CardDefinition[] cards) => Planner(new InMemoryCardDefinitionProvider(cards));
        private static AiDecisionService Planner(ICardDefinitionProvider provider) => new AiDecisionService(provider,
            new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
            new AiSearchOptions { MaxMilliseconds = 10000, MaxSimulations = 16000 });
        private static void Execute(BattleState state, ICardDefinitionProvider provider, IBattleCommand command)
        {
            new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(),
                new AttackService(new TargetingService(), new Random(41).Next), new EndTurnService(new Random(41)))
                .Execute(state, PlayerId.AI, command);
        }

        [Test]
        public void Observation_HidesBothDeckOrdersAndEnemyHand_ButRetainsOwnHandIdentity()
        {
            var state = State();
            state.Player.Hand.Add("secret");
            state.AI.Hand.Add("known", "known-runtime", true);
            var copy = AiBattleStateCopy.Create(state, true);
            Assert.That(copy.Player.Hand.CardIds, Is.All.EqualTo(AiBattleStateCopy.UnknownCardId));
            Assert.That(copy.Player.Deck.CardIds, Is.All.EqualTo(AiBattleStateCopy.UnknownCardId));
            Assert.That(copy.AI.Deck.CardIds, Is.All.EqualTo(AiBattleStateCopy.UnknownCardId));
            Assert.That(copy.AI.Hand.Cards[0].RuntimeId, Is.EqualTo("known-runtime"));
            Assert.That(copy.AI.Hand.Cards[0].IsTemporaryReplicate, Is.True);
            Assert.That(copy.AI.Master, Is.SameAs(copy.AIBoard.GetOccupant(copy.AI.Master.Position)));
        }

        [Test]
        public void DetachedCopy_PreservesStatusesAndErasureBaseline_WithoutSharingMutableState()
        {
            var state = State();
            var unit = Unit(state, PlayerId.AI, new TileCoord(0,0), 20, 30, id: "Werewolf");
            unit.IncreaseBaseAttack(10);
            unit.IncreaseMaxHpAndCurrentHp(8);
            unit.ApplyHuanShu();
            unit.AddInvincibleEffect(InvincibleDurationType.OwnerTurns, 2, state.TurnNumber, PlayerId.AI);
            state.PersistentEffects.Add(new PersistentEffectState("test", PlayerId.AI, "test", 1, "3 turns", new ResourceSet(1,0,0,0), 3));
            var copy = AiBattleStateCopy.Create(state);
            var copiedUnit = copy.AIBoard.GetOccupant(unit.Position);
            Assert.That(copiedUnit.IsUnderHuanShu, Is.True);
            Assert.That(copiedUnit.BaseAttack, Is.EqualTo(30));
            copiedUnit.ResolveInvincibleTurnEnd(PlayerId.AI);
            Assert.That(unit.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
            copiedUnit.ApplyErasure();
            Assert.That(copiedUnit.BaseAttack, Is.EqualTo(20));
            Assert.That(unit.IsErasure, Is.False);
            copy.PersistentEffects[0].ResolveOwnerTurnStart();
            Assert.That(state.PersistentEffects[0].OwnerTurnStartsRemaining, Is.EqualTo(3));
            copy.AIBoard.Remove(unit.Position);
            Assert.That(state.AIBoard.GetOccupant(unit.Position), Is.SameAs(unit));
        }

        [Test]
        public void DetachedCopy_WerewolfAuraUsesCopiedBoard()
        {
            var state = State();
            var first = Unit(state, PlayerId.AI, new TileCoord(0,0), 20, 30, id: "Werewolf");
            var second = Unit(state, PlayerId.AI, new TileCoord(1,0), 20, 30, id: "Werewolf");
            var copy = AiBattleStateCopy.Create(state);
            copy.AIBoard.Remove(second.Position);
            Assert.That(copy.AIBoard.GetOccupant(first.Position).Attack, Is.EqualTo(20));
            Assert.That(first.Attack, Is.EqualTo(30));
        }

        [Test]
        public void Planning_DoesNotChangeAuthoritativeBoardHandResourcesOrEffectCounters()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(0,0), 20, 30, AttackType.Ranged);
            Unit(state, PlayerId.Player, new TileCoord(0,0), 5, 10);
            var before = AiBattleStateCopy.PositionKey(state);
            Planner().GetNextCommand(state);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
        }

        [Test]
        public void Attack_AvoidsSuicidalMasterCounterattack()
        {
            var state = State();
            state.AI.Master.CurrentHp = 2;
            state.AI.Master.RemainingAttacksThisTurn = 1;
            var command = Planner().GetNextCommand(state);
            Assert.That(command, Is.Not.TypeOf<AttackCommand>());
        }

        [Test]
        public void Attack_ResolvesPiercingAndShielderRatherThanSkippingProtectedTargets()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(0,0), 10, 20, AttackType.Ranged, piercing: true);
            Unit(state, PlayerId.Player, new TileCoord(2,0), 0, 15, shielder: true);
            state.Player.Master.CurrentHp = 5;
            var provider = new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            var command = Planner(provider).GetNextCommand(state);
            Assert.That(command, Is.TypeOf<AttackCommand>());
            Execute(state, provider, command);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
            Assert.That(state.IsEnded, Is.True);
        }

        [Test]
        public void Planning_UsesResourceSpellThenAffordableLethalSpell()
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(0,0,0,2));
            state.Player.Master.CurrentHp = 20;
            state.AI.Hand.Add("ManaStone");
            state.AI.Hand.Add("finisher");
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] {
                new ScriptedSpellCardDefinition("ManaStone", "Mana", new ResourceSet(0,0,0,2), ManaStoneRules.EffectId),
                new DamageSpellCardDefinition("finisher", "Finish", new ResourceSet(3,0,0,0), 33)
            });
            var planner = Planner(provider);
            var first = planner.GetNextCommand(state);
            Assert.That(first, Is.TypeOf<CastScriptedSpellCommand>());
            Execute(state, provider, first);
            var second = planner.GetNextCommand(state);
            Assert.That(second, Is.TypeOf<CastDamageSpellCommand>());
            Execute(state, provider, second);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void Planning_CanUseGuInsteadOfIgnoringScriptedSpells()
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(0,10,0,0));
            state.AI.Hand.Add("Gu");
            Unit(state, PlayerId.Player, new TileCoord(0,0), 100, 100);
            var card = new ScriptedSpellCardDefinition("Gu", "Gu", new ResourceSet(0,10,0,0), GuRules.EffectId);
            var command = Planner(card).GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastScriptedSpellCommand>());
            Assert.That(((CastScriptedSpellCommand)command).HasTarget, Is.True);
        }

        [Test]
        public void Planning_RobotFusionCompletesPaidSelectionThenAttacks()
        {
            var state = State();
            var survivor = Unit(state, PlayerId.AI, new TileCoord(0,0), 3, 15, AttackType.Ranged, robot:true, upkeep:2);
            var material = Unit(state, PlayerId.AI, new TileCoord(0,1), 30, 15, robot:true, upkeep:1);
            material.HasSummoningSickness = true;
            material.RemainingAttacksThisTurn = 0;
            state.Player.Master.CurrentHp = 33;
            state.AI.Resources.Add(new ResourceSet(0,0,3,0));
            state.AI.Hand.Add("RobotFusion");
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] {
                new ScriptedSpellCardDefinition("RobotFusion", "Fusion", new ResourceSet(0,0,3,0), RobotFusionRules.EffectId)
            });
            var planner = Planner(provider);
            var begin = planner.GetNextCommand(state);
            Assert.That(begin, Is.TypeOf<CastScriptedSpellCommand>());
            Execute(state, provider, begin);
            Assert.That(state.PendingRobotFusion, Is.Not.Null);
            var finish = planner.GetNextCommand(state);
            Assert.That(finish, Is.TypeOf<CastScriptedSpellCommand>());
            Assert.That(((CastScriptedSpellCommand)finish).HasMultipleTargets, Is.True);
            Execute(state, provider, finish);
            Assert.That(state.PendingRobotFusion, Is.Null);
            Assert.That(survivor.Attack, Is.EqualTo(33));
            Execute(state, provider, planner.GetNextCommand(state));
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void Move_RescuesMasterByMovingBehindExistingBlocker()
        {
            var state = State();
            state.AI.Master.CurrentHp = 3;
            var blocker = Unit(state, PlayerId.AI, new TileCoord(0,0), 0, 100);
            blocker.RemainingAttacksThisTurn = 0;
            Unit(state, PlayerId.Player, new TileCoord(4,0), 30, 30);
            var command = Planner().GetNextCommand(state);
            Assert.That(command, Is.TypeOf<MoveOccupantCommand>());
            Execute(state, new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()), command);
            Assert.That(new TargetingService().CanTarget(state, PlayerId.Player, new TileCoord(4,0), state.AI.Master.Position), Is.False);
        }

        [Test]
        public void Budget_IsBoundedAndReturnedCommandIsLegal()
        {
            var state = State();
            for (var col = 0; col < 5; col++) Unit(state, PlayerId.AI, new TileCoord(col,0), 10, 50);
            var provider = new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            var planner = new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxSimulations = 12, MaxMilliseconds = 10000 });
            var command = planner.GetNextCommand(state);
            Assert.That(planner.LastSimulationCount, Is.LessThanOrEqualTo(12));
            Assert.DoesNotThrow(() => Execute(state, provider, command));
        }

        [Test]
        public void RepeatedDecisions_AlwaysEndTurnByActionCap()
        {
            var state = State();
            var planner = Planner();
            IBattleCommand last = null;
            for (var i = 0; i < 64; i++) last = planner.GetNextCommand(state);
            Assert.That(last, Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void HiddenHandAndDeckChanges_DoNotChangeDeterministicDecision()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(0,0), 10, 50, AttackType.Ranged);
            Unit(state, PlayerId.Player, new TileCoord(0,0), 3, 8);
            state.Player.Hand.Add("secret-a");
            var other = AiBattleStateCopy.Create(state);
            other.Player.Hand.Remove("secret-a");
            other.Player.Hand.Add("secret-b");
            foreach (var deck in new[] { other.Player.Deck, other.AI.Deck })
            {
                var count = deck.Count;
                while (deck.TryDraw(out _)) { }
                for (var i = 0; i < count; i++) deck.AddToTop("different-hidden-" + i);
            }
            var first = Planner().GetNextCommand(state);
            var second = Planner().GetNextCommand(other);
            Assert.That(first, Is.TypeOf<AttackCommand>());
            Assert.That(second, Is.TypeOf<AttackCommand>());
            Assert.That(((AttackCommand)second).AttackerCoord, Is.EqualTo(((AttackCommand)first).AttackerCoord));
            Assert.That(((AttackCommand)second).TargetCoord, Is.EqualTo(((AttackCommand)first).TargetCoord));
        }

        [Test]
        public void Clone_PreservesSealboundAndDemonKingCountdown()
        {
            var state = State();
            var demon = Unit(state, PlayerId.AI, new TileCoord(0,0), 33, 33, id: "DemonKing");
            demon.RestoreDemonKingRevivalState(true, 2, PlayerId.Player, 3, 1);
            var sealedUnit = Unit(state, PlayerId.AI, new TileCoord(1,0), 10, 10);
            sealedUnit.EnterSealbound(3);
            var copy = AiBattleStateCopy.Create(state);
            var clonedDemon = copy.AIBoard.GetOccupant(demon.Position);
            Assert.That(clonedDemon.IsDemonKingRevivalPending, Is.True);
            Assert.That(clonedDemon.DemonKingRevivalCountdownPlayerId, Is.EqualTo(PlayerId.Player));
            Assert.That(clonedDemon.DemonKingRevivalEligibleAfterTurnNumber, Is.EqualTo(3));
            Assert.That(clonedDemon.DemonKingRevivalCount, Is.EqualTo(1));
            copy.AIBoard.GetOccupant(sealedUnit.Position).ResolveSealboundOwnerTurnStart();
            Assert.That(sealedUnit.SealboundOwnerTurnStartsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void EndTurn_RecognizesRedDragonLethalWithoutWastingActions()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(0,0), 0, 50, id: "RedDragon");
            state.Player.Master.CurrentHp = 20;
            Assert.That(Planner().GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void InvincibleTarget_DoesNotAttractUselessAttack()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.AI, new TileCoord(0,0), 100, 100, AttackType.Ranged);
            var target = Unit(state, PlayerId.Player, new TileCoord(0,0), 0, 1);
            target.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            var command = Planner().GetNextCommand(state);
            Assert.That(command, Is.TypeOf<AttackCommand>());
            Assert.That(((AttackCommand)command).TargetCoord, Is.EqualTo(state.Player.Master.Position));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [TestCase("Firewall")]
        [TestCase("BiochemicalBomb")]
        [TestCase("TimedBomb")]
        [TestCase("HuanShu")]
        public void CurrentScriptedDamageAndDebuffCards_ArePlayableCandidates(string cardId)
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Project333/Resources/Project333/Data/cards.json"));
            var provider = JsonCardDefinitionDatabase.FromJson(json).CreateProvider();
            var state = State();
            state.AI.Hand.Add(cardId);
            state.AI.Resources.Add(new ResourceSet(5,5,5,5));
            for (var col = 0; col < 5; col++) Unit(state, PlayerId.Player, new TileCoord(col,0), 50, 30);
            var command = Planner(provider).GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastScriptedSpellCommand>());
            Assert.That(((CastScriptedSpellCommand)command).CardId, Is.EqualTo(cardId));
            Assert.DoesNotThrow(() => Execute(state, provider, command));
        }

        [Test]
        public void Replicate_ConsecutiveCopiesCanBePlannedDespiteDifferentScenarioGuids()
        {
            var state = State();
            state.Player.Master.CurrentHp = 20;
            state.AI.Hand.Add("repeat");
            var card = new DamageSpellCardDefinition("repeat", "Repeat", new ResourceSet(), 10, hasReplicate: true);
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { card });
            var planner = Planner(provider);
            var command = planner.GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastDamageSpellCommand>());
            Execute(state, provider, command);
            var copy = state.AI.Hand.Cards.Single();
            Assert.That(copy.IsTemporaryReplicate, Is.True);
            var next = planner.GetNextCommand(state);
            Assert.That(((IHandCardCommand)next).HandCardRuntimeId, Is.EqualTo(copy.RuntimeId));
            Execute(state, provider, next);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }
    }
}
