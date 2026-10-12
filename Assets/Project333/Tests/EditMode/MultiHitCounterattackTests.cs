using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class MultiHitCounterattackTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Counterattack_UsesTheDefendersGenericHitCount(int hits)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: hits);
            defender.HasSummoningSickness = true;
            defender.RemainingAttacksThisTurn = 0;

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100 - 4 * hits));
            Assert.That(defender.CurrentHp, Is.EqualTo(99));
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(Enumerable.Repeat(4, hits)));
            Assert.That(Counters(state).All(e => e.SourceRuntimeId == defender.RuntimeId), Is.True);
            Assert.That(defender.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
        }

        [TestCase("Cerberus", 3)]
        [TestCase("Gwangma", 2)]
        [TestCase("A-212", 4)]
        public void Counterattack_UsesTheActualSummonedCardDefinition(string cardId, int expectedHits)
        {
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
                "Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
            var state = State();
            state.SetActivePlayer(PlayerId.AI);
            state.AI.Resources.Add(new ResourceSet(100, 100, 100, 100));
            state.AI.Hand.Add(cardId);
            var defender = new PlayCardService(provider, new SummonService())
                .PlayUnitCard(state, PlayerId.AI, cardId, new TileCoord(0, 0));
            var initialCounterAttack = defender.Attack;
            state.SetActivePlayer(PlayerId.Player);
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 1000);

            Attack(state, attacker, defender);

            Assert.That(defender.EffectiveHitsPerAttack, Is.EqualTo(expectedHits));
            Assert.That(attacker.CurrentHp, Is.EqualTo(1000 - initialCounterAttack * expectedHits));
            Assert.That(Counters(state).Select(e => e.Amount),
                Is.EqualTo(Enumerable.Repeat(initialCounterAttack, expectedHits)));
            Assert.That(Counters(state).All(e => e.SourceCardId == cardId), Is.True);
        }

        [Test]
        public void AttackAndCounterattack_UseTheirOwnHitCountsWithoutMultiplyingThem()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 2, hp: 100, hits: 3);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: 5);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(defender.CurrentHp, Is.EqualTo(94));
            Assert.That(state.ValuePopupEvents.Count(e => e.Cause == BattleValueChangeCause.NormalAttack), Is.EqualTo(3));
            Assert.That(Counters(state), Has.Length.EqualTo(5));
        }

        [TestCase(DamageType.Physical, 2)]
        [TestCase(DamageType.Magic, 1)]
        [TestCase(DamageType.Fixed, 5)]
        public void Counterattack_AppliesMatchingDefenseToEveryHit(DamageType damageType, int damagePerHit)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100, physicalDefense: 3, magicDefense: 4);
            var defender = Unit(state, PlayerId.AI, attack: 5, hp: 100, hits: 3, damageType: damageType);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100 - damagePerHit * 3));
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(Enumerable.Repeat(damagePerHit, 3)));
            Assert.That(Counters(state).All(e => e.DamageType == damageType), Is.True);
        }

        [Test]
        public void Counterattack_EndureSavesOnlyTheFirstLethalHit()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 3, endure: true);
            var defender = Unit(state, PlayerId.AI, attack: 5, hp: 100, hits: 2);

            Attack(state, attacker, defender);

            Assert.That(attacker.EndureUsed, Is.True);
            Assert.That(attacker.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(state.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void Counterattack_ResolvesAllHitsEvenWhenTheCounterattackerDiesInTheExchange()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 5, hp: 10);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 2, hits: 3);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(defender.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(state.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(defender.Position), Is.Null);
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 4, 4, 2 }));
        }

        [Test]
        public void Counterattack_BerserkerKeepsTheAttackValueFromBeforeTheEntireAttackAction()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 3, hp: 100, hits: 3);
            var defender = Unit(state, PlayerId.AI, attack: 2, hp: 20, hits: 4, berserker: true);
            defender.CurrentHp = 17;

            Attack(state, attacker, defender);

            Assert.That(defender.CurrentHp, Is.EqualTo(8));
            Assert.That(defender.Attack, Is.EqualTo(14));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 5, 5, 5, 5 }));
        }

        [Test]
        public void Counterattack_LifeStealHealsTheSumOfActualHpLostAfterDefenseAndOverkill()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 7, physicalDefense: 2);
            var defender = Unit(state, PlayerId.AI, attack: 5, hp: 30, hits: 3, lifeSteal: true);
            defender.CurrentHp = 5;

            Attack(state, attacker, defender);

            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 3, 3, 1 }));
            Assert.That(defender.CurrentHp, Is.EqualTo(11));
            var heal = state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.LifeSteal);
            Assert.That(heal.RuntimeId, Is.EqualTo(defender.RuntimeId));
            Assert.That(heal.Amount, Is.EqualTo(7));
            Assert.That(state.ValuePopupEvents.Last(), Is.SameAs(heal));
        }

        [Test]
        public void Counterattack_LethalLaterHitPreventsTheAttackersDeferredLifeSteal()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 2, hp: 5, lifeSteal: true);
            var defender = Unit(state, PlayerId.AI, attack: 3, hp: 30, hits: 2, lifeSteal: true);
            defender.CurrentHp = 10;

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(state.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(defender.CurrentHp, Is.EqualTo(13));
            Assert.That(state.ValuePopupEvents.Where(e => e.IsHealing).Select(e => e.RuntimeId),
                Is.EqualTo(new[] { defender.RuntimeId }));
        }

        [Test]
        public void Counterattack_DeadCounterattackerDoesNotRecoverFromLifeSteal()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 2, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 3, hp: 30, hits: 3, lifeSteal: true);
            defender.CurrentHp = 1;

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(91));
            Assert.That(state.AIBoard.GetOccupant(defender.Position), Is.Null);
            Assert.That(state.ValuePopupEvents.Any(e => e.IsHealing), Is.False);
        }

        [TestCase(AttackType.Ranged, AttackType.Melee)]
        [TestCase(AttackType.Melee, AttackType.Ranged)]
        public void Counterattack_RequiresMeleeOnBothSides(AttackType attackerType, AttackType defenderType)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100, attackType: attackerType);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: 5, attackType: defenderType);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(Counters(state), Is.Empty);
        }

        [Test]
        public void Counterattack_DrainedMultiHitUnitCannotCounterattack()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: 5);
            defender.IsDrained = true;

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(defender.CurrentHp, Is.EqualTo(97));
            Assert.That(Counters(state), Is.Empty);
        }

        [Test]
        public void Counterattack_SealboundMultiHitUnitCannotBeAttackedOrCounterattack()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: 5);
            defender.EnterSealbound(1);

            Assert.That(() => Attack(state, attacker, defender), Throws.InvalidOperationException);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(defender.CurrentHp, Is.EqualTo(100));
            Assert.That(Counters(state), Is.Empty);
        }

        [Test]
        public void Counterattack_ZeroAttackMultiHitUnitCannotCounterattack()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 0, hp: 100, hits: 5);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(Counters(state), Is.Empty);
        }

        [Test]
        public void Counterattack_ErasureSuppressesMultiHitButKeepsOneBaselineCounterattack()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            var defender = Unit(state, PlayerId.AI, attack: 4, hp: 100, hits: 5);
            defender.ApplyErasure();

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(96));
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void Counterattack_UsesTheInitiallySelectedShielderAndItsHitCount()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 2, hp: 100, hits: 3, flying: true);
            var shielder = Unit(state, PlayerId.AI, attack: 2, hp: 3, hits: 5, shielder: true);
            var target = Unit(state, PlayerId.AI, attack: 20, hp: 100, hits: 2, coord: new TileCoord(0, 1));

            Attack(state, attacker, target);

            Assert.That(attacker.CurrentHp, Is.EqualTo(95));
            Assert.That(state.AIBoard.GetOccupant(shielder.Position), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(97));
            Assert.That(Counters(state), Has.Length.EqualTo(5));
            Assert.That(Counters(state).All(e => e.SourceRuntimeId == shielder.RuntimeId), Is.True);
        }

        [Test]
        public void Counterattack_AppliesFlyingReductionToEveryCounterHit()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100, flying: true, physicalDefense: 1);
            var defender = Unit(state, PlayerId.AI, attack: 5, hp: 100, hits: 3);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(94));
            Assert.That(Counters(state).Select(e => e.Amount), Is.EqualTo(new[] { 2, 2, 2 }));
        }

        [Test]
        public void Counterattack_InvinciblePreventsAndPresentsEveryHit()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, attack: 1, hp: 100);
            attacker.AddInvincibleEffect(InvincibleDurationType.Always);
            var defender = Unit(state, PlayerId.AI, attack: 5, hp: 100, hits: 3, lifeSteal: true);

            Attack(state, attacker, defender);

            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(Counters(state), Has.Length.EqualTo(3));
            Assert.That(Counters(state).All(e => e.Amount == 0 && e.IsInvinciblePrevented), Is.True);
            Assert.That(state.ValuePopupEvents.Any(e => e.IsHealing), Is.False);
        }

        private static BattleValuePopupEvent[] Counters(BattleState state) => state.ValuePopupEvents
            .Where(e => e.Cause == BattleValueChangeCause.Counterattack).ToArray();

        private static void Attack(BattleState state, UnitState attacker, UnitState defender) =>
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, defender.Position);

        private static UnitState Unit(
            BattleState state,
            PlayerId owner,
            int attack,
            int hp,
            int hits = 1,
            AttackType attackType = AttackType.Melee,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool endure = false,
            bool berserker = false,
            bool lifeSteal = false,
            bool shielder = false,
            bool flying = false,
            TileCoord? coord = null)
        {
            var position = coord ?? new TileCoord(0, 0);
            var id = owner + "-" + position.Column + "-" + position.Row;
            var unit = new UnitState(id, id, owner, position, attackType, attack, hp, true, false, 0,
                hitsPerAttack: hits, hasEndure: endure, hasBerserker: berserker, hasLifeSteal: lifeSteal,
                hasShielder: shielder, hasFlying: flying, damageType: damageType,
                physicalDefense: physicalDefense, magicDefense: magicDefense);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(position, unit);
            return unit;
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            return state;
        }
    }
}
