using System;
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
    public sealed class AiFocusFireTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var cardId in player.Hand.CardIds.ToArray()) player.Hand.Remove(cardId);
                player.Resources.Add(new ResourceSet(0, 0, 30, 0));
            }
            state.AI.Master.HasSummoningSickness = false;
            state.AI.Master.RemainingAttacksThisTurn = 1;
            state.Player.Master.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            return state;
        }

        private static UnitState A301(BattleState state, int column, int hp = 3)
        {
            // Match the released A-301: master ATK 3 deals 1 after Flying's melee reduction.
            var unit = new UnitState("a301-" + column, "A-301", PlayerId.Player,
                new TileCoord(column, 0), AttackType.Ranged, 33, 3, true, true, 1,
                races: new[] { UnitRace.Robot }, hasFlying: true);
            unit.CurrentHp = hp;
            unit.HasSummoningSickness = false;
            state.PlayerBoard.Place(unit.Position, unit);
            return unit;
        }

        private static AiDecisionService Planner(int depth = 1)
        {
            return new AiDecisionService(new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()),
                new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = depth, MaxMilliseconds = 10000, MaxSimulations = 4000 });
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(1, 3)]
        [TestCase(2, 3)]
        [TestCase(3, 3)]
        public void EqualA301Targets_MasterFocusesLowerHpRegardlessOfColumnOrSearchDepth(int weakestColumn, int depth)
        {
            var state = State();
            for (var column = 1; column <= 3; column++) A301(state, column, column == weakestColumn ? 2 : 3);

            var command = Planner(depth).GetNextCommand(state) as AttackCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.AttackerCoord, Is.EqualTo(state.AI.Master.Position));
            Assert.That(command.TargetCoord, Is.EqualTo(new TileCoord(weakestColumn, 0)));
            new AttackService().Attack(state, PlayerId.AI, command.AttackerCoord, command.TargetCoord);
            Assert.That(state.PlayerBoard.GetOccupant(command.TargetCoord).CurrentHp, Is.EqualTo(1),
                "This is a nonlethal focus preference, not an immediate-kill bonus.");
        }

        [Test]
        public void EqualHpTargets_PreserveExistingStableCoordinateOrder()
        {
            var state = State();
            A301(state, 1);
            A301(state, 2);
            A301(state, 3);

            var command = Planner(3).GetNextCommand(state) as AttackCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(new TileCoord(2, 0)));
        }

        [Test]
        public void FocusPlanning_DoesNotMutateAuthoritativeState()
        {
            var state = State();
            var healthy = A301(state, 2);
            var wounded = A301(state, 3, 2);
            var before = AiBattleStateCopy.PositionKey(state);
            var sequence = state.Counters.ActionSequence;

            var command = Planner(3).GetNextCommand(state) as AttackCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(wounded.Position));
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.Counters.ActionSequence, Is.EqualTo(sequence));
            Assert.That(state.PlayerBoard.GetOccupant(healthy.Position), Is.SameAs(healthy));
            Assert.That(state.PlayerBoard.GetOccupant(wounded.Position), Is.SameAs(wounded));
            Assert.That(wounded.CurrentHp, Is.EqualTo(2));
            Assert.That(state.AI.Master.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void InvincibleTargets_LowerHpDoesNotRewardAnAttackThatDealsNoDamage()
        {
            var state = State();
            foreach (var unit in new[] { A301(state, 2), A301(state, 3, 2) })
                unit.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);

            Assert.That(Planner(3).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void ImmediateMasterLethal_RemainsMoreImportantThanFocusingAWoundedUnit()
        {
            var state = State();
            state.Player.Master.RestoreInvincibleEffects(Array.Empty<InvincibleEffectState>());
            state.Player.Master.CurrentHp = 1;
            A301(state, 2);
            A301(state, 3, 2);

            var command = Planner(3).GetNextCommand(state) as AttackCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(state.Player.Master.Position));
            new AttackService().Attack(state, PlayerId.AI, command.AttackerCoord, command.TargetCoord);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [TestCase("attack")]
        [TestCase("max-hp")]
        [TestCase("future-invincibility")]
        [TestCase("targeted-effect")]
        [TestCase("buff-source")]
        public void EqualDamageButDifferentStatsOrStatuses_PreserveExistingTargetOrder(string difference)
        {
            var state = State();
            var healthy = A301(state, 2);
            var wounded = A301(state, 3, 2);
            switch (difference)
            {
                case "attack":
                    wounded.IncreaseBaseAttack(1);
                    break;
                case "max-hp":
                    wounded.IncreaseMaxHpAndCurrentHp(3);
                    wounded.CurrentHp = 2;
                    break;
                case "future-invincibility":
                    wounded.AddInvincibleEffect(InvincibleDurationType.OwnerTurnOnly, 0,
                        state.TurnNumber, PlayerId.Player);
                    break;
                case "targeted-effect":
                    state.PersistentEffects.Add(new PersistentEffectState("CheonraJimang", PlayerId.AI,
                        "cheonra_jimang", state.TurnNumber, "next owner turn", new ResourceSet(),
                        ownerTurnStartsRemaining: 1, targetRuntimeId: wounded.RuntimeId));
                    break;
                case "buff-source":
                    healthy.ApplyCombatStatBuff("Hero", attackBonus: 1);
                    wounded.ApplyCombatStatBuff("RobotFusion", attackBonus: 1);
                    break;
            }

            var command = Planner().GetNextCommand(state) as AttackCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(healthy.Position),
                "The HP preference only breaks ties between otherwise equivalent occupants.");
        }
    }
}
