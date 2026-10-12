using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiConservationTests
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

        private static UnitState Unit(BattleState state, PlayerId owner, int col, int attack, int hp,
            bool producer = false, bool endure = false, AttackType type = AttackType.Melee)
        {
            var coord = new TileCoord(col, 0);
            var unit = new UnitState(Guid.NewGuid().ToString(), "fixture", owner, coord, type,
                attack, hp, false, false, 0, producer ? new ResourceSet(1, 0, 0, 0) : null, hasEndure: endure);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(coord, unit);
            return unit;
        }

        private static IBattleCommand Decide(BattleState state)
        {
            var json = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "Project333/Resources/Project333/Data/cards.json"));
            var planner = new AiDecisionService(JsonCardDefinitionDatabase.FromJson(json).CreateProvider(),
                new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 3, MaxMilliseconds = 10000, MaxSimulations = 16000 });
            return planner.GetNextCommand(state);
        }

        [TestCase(2, 100, false)] // Harmless chip damage.
        [TestCase(2, 5, false)]   // Low-value kill.
        [TestCase(10, 100, false)] // High attack, but not killable.
        [TestCase(0, 100, true)]  // Producer, but not killable.
        public void Producer_SavesAttackWithoutValuableKill(int attack, int hp, bool targetProducer)
        {
            var state = State();
            var producer = Unit(state, PlayerId.AI, 0, 10, 60, true);
            Unit(state, PlayerId.Player, 0, attack, hp, targetProducer);
            Assert.That(Decide(state), Is.TypeOf<EndTurnCommand>());
            Assert.That(producer.RemainingAttacksThisTurn, Is.EqualTo(1));
            Assert.That(producer.CurrentHp, Is.EqualTo(60));
        }

        [TestCase(0, true)]
        [TestCase(20, false)]
        public void Producer_TakesValuableKill(int attack, bool targetProducer)
        {
            var state = State();
            Unit(state, PlayerId.AI, 0, 10, 60, true);
            var victim = Unit(state, PlayerId.Player, 0, attack, 5, targetProducer);
            var command = Decide(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(victim.Position));
        }

        [Test]
        public void Producer_DoesNotTradeItsLifeForGlassCannon()
        {
            var state = State();
            Unit(state, PlayerId.AI, 0, 10, 5, true);
            Unit(state, PlayerId.Player, 0, 20, 5);
            Assert.That(Decide(state), Is.TypeOf<EndTurnCommand>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Producer_RecognizesInvincibleOrEnduringTargetIsNotKilled(bool invincible)
        {
            var state = State();
            Unit(state, PlayerId.AI, 0, 10, 60, true);
            var victim = Unit(state, PlayerId.Player, 0, 20, 5, endure: !invincible);
            if (invincible) victim.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            Assert.That(Decide(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void Producer_StillFinishesEnemyMaster()
        {
            var state = State();
            Unit(state, PlayerId.AI, 0, 10, 60, true);
            state.Player.Master.CurrentHp = 5;
            var command = Decide(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(state.Player.Master.Position));
        }

        [Test]
        public void OrdinaryFighter_StillUsesNonlethalAttack()
        {
            var state = State();
            Unit(state, PlayerId.AI, 0, 10, 60);
            Assert.That(Decide(state), Is.TypeOf<AttackCommand>());
        }

        private static void GiveHuanShu(BattleState state)
        {
            state.AI.Hand.Add(HuanShuRules.CardId);
            state.AI.Resources.Add(new ResourceSet(0, 3, 0, 0));
        }

        [TestCase(20, false)]
        [TestCase(333, false)]
        [TestCase(333, true)]
        public void HuanShu_SavesCardAgainstLowAttackEvenWithHighHpOrProduction(int hp, bool producer)
        {
            var state = State();
            GiveHuanShu(state);
            Unit(state, PlayerId.Player, 0, 3, hp, producer);
            Assert.That(Decide(state), Is.TypeOf<EndTurnCommand>());
            Assert.That(state.AI.Hand.CardIds, Does.Contain(HuanShuRules.CardId));
        }

        [Test]
        public void HuanShu_PrefersHighAttackOverHighHpProducer()
        {
            var state = State();
            GiveHuanShu(state);
            Unit(state, PlayerId.Player, 2, 3, 333, true);
            var dangerous = Unit(state, PlayerId.Player, 0, 40, 50);
            var command = Decide(state) as CastScriptedSpellCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.CardId, Is.EqualTo(HuanShuRules.CardId));
            Assert.That(command.TargetCoord, Is.EqualTo(dangerous.Position));
            Assert.That(dangerous.IsUnderHuanShu, Is.False);
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public void HuanShu_LowAttackUsedOnlyWhenLowMasterIsExposed(bool guard, bool invincible, bool cast)
        {
            var state = State();
            GiveHuanShu(state);
            state.AI.Master.CurrentHp = 2;
            if (guard) Unit(state, PlayerId.AI, 2, 0, 100);
            if (invincible) state.AI.Master.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.AI);
            Unit(state, PlayerId.Player, 0, 3, 40);
            var command = Decide(state);
            Assert.That(command, cast ? Is.TypeOf<CastScriptedSpellCommand>() : Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void HuanShu_DoesNotRecastAlreadyAfflictedTarget()
        {
            var state = State();
            GiveHuanShu(state);
            Unit(state, PlayerId.Player, 0, 40, 50).ApplyHuanShu();
            Assert.That(Decide(state), Is.TypeOf<EndTurnCommand>());
        }
    }
}
