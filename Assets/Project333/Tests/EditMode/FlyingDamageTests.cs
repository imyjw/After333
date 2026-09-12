using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class FlyingDamageTests
    {
        [Test]
        public void Attack_FourFlyingMatchups_ApplyToEveryDamageType(
            [Values(AttackType.Melee, AttackType.Ranged)] AttackType attackType,
            [Values(false, true)] bool attackerFlying,
            [Values(DamageType.Physical, DamageType.Magic, DamageType.Fixed)] DamageType damageType)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10,
                attackType: attackType, flying: attackerFlying, damageType: damageType);
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true);
            Attack(state, attacker, target);

            var expected = attackType == AttackType.Melee && !attackerFlying ? 5 : 10;
            Assert.That(target.CurrentHp, Is.EqualTo(50 - expected));
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.NormalAttack).Amount,
                Is.EqualTo(expected));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
        }

        [TestCase(10, 3, DamageType.Physical, 3)]
        [TestCase(10, 3, DamageType.Magic, 3)]
        [TestCase(10, 99, DamageType.Fixed, 5)]
        [TestCase(11, 0, DamageType.Physical, 5)]
        [TestCase(1, 0, DamageType.Physical, 0)]
        [TestCase(5, 10, DamageType.Physical, 0)]
        public void Attack_AppliesDefenseBeforeHalvingAndFloorsEachHit(int attack, int defense,
            DamageType damageType, int expected)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: attack, damageType: damageType);
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true, defense: defense);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(50 - expected));
        }

        [Test]
        public void Attack_MultihitAndLifeStealUseActualReducedHpLoss()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 9, hits: 3, lifeSteal: true);
            attacker.CurrentHp = 10;
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true, defense: 2);
            target.CurrentHp = 8;
            Attack(state, attacker, target);

            Assert.That(target.CurrentHp, Is.EqualTo(-1));
            Assert.That(attacker.CurrentHp, Is.EqualTo(18));
            Assert.That(state.ValuePopupEvents.Where(e => e.Cause == BattleValueChangeCause.NormalAttack)
                .Select(e => e.Amount), Is.EqualTo(new[] { 3, 3, 2 }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Attack_PiercingChecksFlyingSeparatelyForEachRow(bool frontFlying, bool backFlying)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, piercing: true);
            var front = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: frontFlying);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: backFlying, defense: 2);
            Attack(state, attacker, front);

            Assert.That(front.CurrentHp, Is.EqualTo(frontFlying ? 45 : 40));
            Assert.That(back.CurrentHp, Is.EqualTo(backFlying ? 46 : 42));
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.Piercing).Amount,
                Is.EqualTo(backFlying ? 4 : 8));
        }

        [Test]
        public void Attack_DirectRearHitAlsoHalvesPiercingAgainstFlyingFront()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, piercing: true);
            var front = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1));
            Attack(state, attacker, back);

            Assert.That(back.CurrentHp, Is.EqualTo(40));
            Assert.That(front.CurrentHp, Is.EqualTo(45));
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.Piercing).Amount,
                Is.EqualTo(5));
        }

        [Test]
        public void Attack_HiddenFlyingPiercingRecipientStillTakesHalf()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, piercing: true);
            var front = Unit(state, PlayerId.AI, new TileCoord(0, 0));
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true, hiding: true);
            Attack(state, attacker, front);
            Assert.That(back.CurrentHp, Is.EqualTo(45));
            Assert.That(back.IsHiding, Is.True);
        }

        [TestCase(false, 0)]
        [TestCase(false, 4)]
        [TestCase(true, 0)]
        [TestCase(true, 4)]
        public void Attack_FlyingShielderUsesProtectedTargetsMitigationBeforeOwnDefense(bool backFlying, int counter)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 1, attack: counter);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: backFlying, defense: 2);
            Attack(state, attacker, back);

            Assert.That(shield.CurrentHp, Is.EqualTo(backFlying ? 47 : 43));
            Assert.That(back.CurrentHp, Is.EqualTo(50));
            Assert.That(attacker.CurrentHp, Is.EqualTo(50 - counter));
        }

        [Test]
        public void Attack_UserExample_TenHalvedThenShielderDefenseTwoEqualsThree()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 2);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true);
            Attack(state, attacker, back);
            Assert.That(shield.CurrentHp, Is.EqualTo(47));
            Assert.That(back.CurrentHp, Is.EqualTo(50));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Attack_PiercingShielderUsesProtectedFlightNotShieldFlight(bool shieldFlying, bool backFlying)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, piercing: true);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: shieldFlying, shielder: true, defense: 1);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: backFlying, defense: 2);
            Attack(state, attacker, shield);

            var directDamage = shieldFlying ? 4 : 9;
            var redirectedDamage = backFlying ? 3 : 7;
            Assert.That(shield.CurrentHp, Is.EqualTo(50 - directDamage - redirectedDamage));
            Assert.That(back.CurrentHp, Is.EqualTo(50));
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.Piercing).Amount,
                Is.EqualTo(redirectedDamage));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Attack_ShielderOverflowDoesNotApplyTargetDefenseOrFlyingTwice(int counter)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 1, attack: counter, hp: 2);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true, defense: 2);
            Attack(state, attacker, back);

            Assert.That(shield.CurrentHp, Is.EqualTo(-1));
            Assert.That(back.CurrentHp, Is.EqualTo(49), "(10-2)/2-1-2 = 1 overflow.");
            Assert.That(attacker.CurrentHp, Is.EqualTo(50 - counter));
        }

        [Test]
        public void Attack_FlyingTargetEndureAndInvincibleUseReducedDamage()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true, endure: true, hp: 5);
            target.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.AI);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(5));
            Assert.That(target.EndureUsed, Is.False);

            var other = Unit(state, PlayerId.AI, new TileCoord(1, 0), flying: true, endure: true, hp: 5);
            attacker.RemainingAttacksThisTurn = 1;
            Attack(state, attacker, other);
            Assert.That(other.CurrentHp, Is.EqualTo(1));
            Assert.That(other.EndureUsed, Is.True);
        }

        [Test]
        public void Attack_InvincibleProtectedTargetTransfersZeroDamage()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true, shielder: true);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true);
            back.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.AI);
            Attack(state, attacker, back);
            Assert.That(shield.CurrentHp, Is.EqualTo(50));
            Assert.That(back.CurrentHp, Is.EqualTo(50));
        }

        [Test]
        public void Attack_ShielderAndOverflowRecipientEndureStillPreventLethalHpLoss()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 1, hp: 2, endure: true);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1),
                flying: true, defense: 2, hp: 1, endure: true);
            Attack(state, attacker, back);
            Assert.That(shield.CurrentHp, Is.EqualTo(1));
            Assert.That(shield.EndureUsed, Is.True);
            Assert.That(back.CurrentHp, Is.EqualTo(1));
            Assert.That(back.EndureUsed, Is.True);
        }

        [Test]
        public void Attack_DrainedProtectedTargetsOverflowDoesNotTripleTwice()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 1, hp: 2);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true, defense: 99);
            back.IsDrained = true;
            Attack(state, attacker, back);
            Assert.That(shield.CurrentHp, Is.EqualTo(-27));
            Assert.That(back.CurrentHp, Is.EqualTo(23), "10*3-1-2 = 27, with no second Drained multiplier.");
        }

        [Test]
        public void Spell_FlyingAndShielderKeepExistingSpellDefenseAndOverflowOrder()
        {
            var state = State();
            var shield = Unit(state, PlayerId.AI, new TileCoord(0, 0),
                flying: true, shielder: true, defense: 1, hp: 2);
            var back = Unit(state, PlayerId.AI, new TileCoord(0, 1), flying: true, defense: 2);
            state.Player.Hand.Add("test-spell");
            new SpellService().CastDamageSpell(state, PlayerId.Player, "test-spell",
                PlayerId.AI, back.Position, damage: 10, damageType: DamageType.Magic);
            Assert.That(shield.CurrentHp, Is.EqualTo(-7));
            Assert.That(back.CurrentHp, Is.EqualTo(45), "Spell overflow: 10-1-2-2 = 5; Flying never halves spells.");
        }

        [Test]
        public void Attack_SuppressedFlyingLosesDamageReductionAndErasedAttackerCountsAsGround()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, flying: true);
            attacker.ApplyErasure();
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), flying: true, defense: 2);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(46));
            attacker.RemainingAttacksThisTurn = 1;
            target.ApplyErasure();
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(38));

            var drained = Unit(state, PlayerId.AI, new TileCoord(1, 0), flying: true, defense: 99);
            drained.IsDrained = true;
            attacker.RemainingAttacksThisTurn = 1;
            Attack(state, attacker, drained);
            Assert.That(drained.CurrentHp, Is.EqualTo(20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Attack_CounterattackAgainstFlyingIsNotHalved(bool defenderFlying)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10, flying: true, defense: 2);
            var target = Unit(state, PlayerId.AI, new TileCoord(0, 0), attack: 10, flying: defenderFlying);
            Attack(state, attacker, target);
            Assert.That(attacker.CurrentHp, Is.EqualTo(42));
            Assert.That(target.CurrentHp, Is.EqualTo(40));
        }

        [Test]
        public void Attack_HuanShuCanRedirectGroundMeleeToFlyingAndHalvesDamage()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            attacker.ApplyHuanShu();
            var target = Unit(state, PlayerId.AI, new TileCoord(4, 0), flying: true);
            new AttackService(new TargetingService(), count => count - 1).Attack(
                state, PlayerId.Player, attacker.Position, state.AI.Master.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(45));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Attack_FlyingBuildingAndMasterReceiveSameReduction(bool useMaster)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, new TileCoord(0, 0), attack: 10);
            OccupantState target = useMaster
                ? (OccupantState)new MasterState("flying-master", PlayerId.AI, new TileCoord(0, 0),
                    attack: 0, maxHp: 50, hasFlying: true)
                : new BuildingState("flying-building", "flying-building", PlayerId.AI, new TileCoord(0, 0),
                    canAttack: false, attack: 0, maxHp: 50, hasFlying: true);
            state.AIBoard.Place(target.Position, target);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(45));
        }

        [Test]
        public void AiSimulation_UsesSameFlyingReductionWithoutMutatingAuthoritativeState()
        {
            var state = State(PlayerId.AI);
            var attacker = Unit(state, PlayerId.AI, new TileCoord(0, 0), attack: 10);
            var target = Unit(state, PlayerId.Player, new TileCoord(0, 0), flying: true, hp: 5);
            var provider = new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            var planner = new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 10000 });
            var command = planner.GetNextCommand(state);
            Assert.That(command, Is.TypeOf<AttackCommand>());
            var attack = (AttackCommand)command;
            Assert.That(attack.TargetCoord, Is.EqualTo(target.Position));
            Assert.That(target.CurrentHp, Is.EqualTo(5));
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, attack.TargetCoord);
            Assert.That(target.CurrentHp, Is.Zero);
        }

        private static void Attack(BattleState state, OccupantState attacker, OccupantState target)
        {
            new AttackService().Attack(state, attacker.OwnerId, attacker.Position, target.Position);
        }

        private static UnitState Unit(BattleState state, PlayerId owner, TileCoord coord,
            int attack = 0, int hp = 50, AttackType attackType = AttackType.Melee, bool flying = false,
            int defense = 0, DamageType damageType = DamageType.Physical, int hits = 1,
            bool lifeSteal = false, bool piercing = false, bool shielder = false, bool hiding = false,
            bool endure = false)
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), "test-unit", owner, coord,
                attackType, attack, hp, true, false, 0, hasFlying: flying,
                physicalDefense: defense, magicDefense: defense, damageType: damageType,
                hitsPerAttack: hits, hasLifeSteal: lifeSteal, hasPiercing: piercing,
                hasShielder: shielder, hasHiding: hiding, hasEndure: endure);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(coord, unit);
            return unit;
        }

        private static BattleState State(PlayerId first = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("unused", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, first));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var card in player.Hand.CardIds.ToArray()) player.Hand.Remove(card);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }
    }
}
