using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;

namespace Project333.Tests.EditMode
{
    public sealed class CriticalHitTests
    {
        [TestCase(DamageType.Physical, false, 5)]
        [TestCase(DamageType.Physical, true, 10)]
        [TestCase(DamageType.Magic, false, 5)]
        [TestCase(DamageType.Magic, true, 10)]
        [TestCase(DamageType.Fixed, false, 10)]
        [TestCase(DamageType.Fixed, true, 20)]
        public void Attack_DoublesFinalDamageAfterMatchingDefense(DamageType type, bool hiding, int expected)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: hiding, type: type);
            var target = Unit(state, PlayerId.AI, 0, 0, defense: 5);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(100 - expected));
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.NormalAttack).Amount,
                Is.EqualTo(expected));
            Assert.That(attacker.IsHiding, Is.False);
        }

        [TestCase(AttackType.Melee)]
        [TestCase(AttackType.Ranged)]
        public void Attack_AllHitsCriticalButNextAttackIsNormal(AttackType attackType)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true, hits: 3, attackType: attackType);
            var target = Unit(state, PlayerId.AI, 0, 0, defense: 5, attack: 1, attackType: attackType);
            attacker.RemainingAttacksThisTurn = 2;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(70));
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(55));
            Assert.That(state.ValuePopupEvents.Where(e => e.Cause == BattleValueChangeCause.NormalAttack)
                .Select(e => e.Amount), Is.EqualTo(new[] {10, 10, 10, 5, 5, 5}));
        }

        [Test]
        public void Attack_SpellPowerAndFlyingRoundingPrecedeCritical()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true, type: DamageType.Magic);
            Unit(state, PlayerId.Player, 1, 0, spellPower: 4);
            var target = Unit(state, PlayerId.AI, 0, 0, defense: 3, flying: true);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(90), "(10+4-3)/2 floors to 5, then critical gives 10.");
        }

        [Test]
        public void Attack_DrainedDefenseBypassAndMultiplierPrecedeCritical()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true);
            var target = Unit(state, PlayerId.AI, 0, 0, defense: 99);
            target.IsDrained = true;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(40));
        }

        [TestCase(AttackType.Melee)]
        [TestCase(AttackType.Ranged)]
        public void Attack_ShielderDoublesAfterBothDefensesAndDoesNotRedoubleOverflow(AttackType attackType)
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true, attackType: attackType, flying: true);
            var target = Unit(state, PlayerId.AI, 0, 1, defense: 2);
            var shield = Unit(state, PlayerId.AI, 0, 0, defense: 3, shielder: true, attack: 1);
            shield.CurrentHp = 7;
            Attack(state, attacker, target);
            Assert.That(shield.CurrentHp, Is.EqualTo(-3), "(10-2-3)*2=10.");
            Assert.That(target.CurrentHp, Is.EqualTo(97), "Only the remaining 3 reaches the protected unit.");
        }

        [Test]
        public void Attack_PiercingIsCriticalAndRevealsHiddenRecipient()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true, piercing: true);
            var front = Unit(state, PlayerId.AI, 0, 0, defense: 2);
            var rear = Unit(state, PlayerId.AI, 0, 1, defense: 3, hiding: true);
            Attack(state, attacker, front);
            Assert.That(front.CurrentHp, Is.EqualTo(84));
            Assert.That(rear.CurrentHp, Is.EqualTo(86));
            Assert.That(rear.IsHiding, Is.False);
            Assert.That(state.ValuePopupEvents.Single(e => e.Cause == BattleValueChangeCause.Piercing).Amount, Is.EqualTo(14));
        }

        [Test]
        public void Attack_HuanShuFriendlyHiddenVictimRevealsAndCountersWithoutCritical()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true);
            var friendly = Unit(state, PlayerId.Player, 1, 0, hiding: true, hits: 3, attack: 4);
            var enemy = Unit(state, PlayerId.AI, 0, 0);
            attacker.ApplyHuanShu();
            new AttackService(new TargetingService(), _ => 0).Attack(state, PlayerId.Player,
                attacker.Position, enemy.Position);
            Assert.That(friendly.CurrentHp, Is.EqualTo(80));
            Assert.That(friendly.IsHiding, Is.False);
            Assert.That(attacker.CurrentHp, Is.EqualTo(88));
            Assert.That(state.ValuePopupEvents.Where(e => e.Cause == BattleValueChangeCause.Counterattack)
                .Select(e => e.Amount), Is.EqualTo(new[] {4, 4, 4}));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IncomingAttackPreservesHidingWhenDamageIsZero(bool invincible)
        {
            var target = Unit(State(), PlayerId.AI, 0, 0, hiding: true, defense: 99);
            if (invincible) target.AddInvincibleEffect(InvincibleDurationType.Always);
            DamageResolutionRules.ApplyAttackDamage(target, 10, DamageType.Physical);
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(target.IsHiding, Is.True);
        }

        [Test]
        public void EffectDamageRevealsHidingAndDoesNotAutoCritical()
        {
            var target = Unit(State(), PlayerId.AI, 0, 0, hiding: true);
            DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Magic);
            DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Fixed);
            Assert.That(target.CurrentHp, Is.EqualTo(80));
            Assert.That(target.IsHiding, Is.False);
        }

        [Test]
        public void CriticalDoesNotBypassInvincibleOrEndureAndLifeStealUsesActualHp()
        {
            var state = State();
            var attacker = Unit(state, PlayerId.Player, 0, 0, hiding: true, lifeSteal: true, attackType: AttackType.Ranged);
            attacker.CurrentHp = 50;
            var target = Unit(state, PlayerId.AI, 0, 0, endure: true);
            target.CurrentHp = 12;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(1));
            Assert.That(target.EndureUsed, Is.True);
            Assert.That(attacker.CurrentHp, Is.EqualTo(61));
            var immune = Unit(state, PlayerId.AI, 1, 0);
            immune.AddInvincibleEffect(InvincibleDurationType.Always);
            Assert.That(DamageResolutionRules.ApplyAttackDamage(immune, 10, DamageType.Magic, isCritical: true), Is.Zero);
        }

        [Test]
        public void CriticalDoesNotTurnFullyMitigatedDamageIntoDamage()
        {
            var target = Unit(State(), PlayerId.AI, 0, 0, defense: 10);
            Assert.That(DamageResolutionRules.ApplyAttackDamage(target, 10, DamageType.Magic, isCritical: true), Is.Zero);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void SnapshotProjectionPreservesHiddenCriticalBeforeAttackAndRevealAfterAttack(PlayerId owner)
        {
            var state = State();
            state.SetActivePlayer(owner);
            var attacker = Unit(state, owner, 0, 0, hiding: true);
            var enemy = owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
            var target = Unit(state, enemy, 0, 0, defense: 5);
            var factory = new BattleStateViewFactory();
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(factory.CreateForPlayer(state, "crit", owner));
            var restoredAttacker = restored.PlayerBoard.GetOccupant(attacker.Position);
            var restoredTarget = restored.AIBoard.GetOccupant(target.Position);
            new AttackService().Attack(restored, PlayerId.Player, attacker.Position, target.Position);
            Assert.That(restoredTarget.CurrentHp, Is.EqualTo(90));
            Assert.That(restoredAttacker.IsHiding, Is.False);
            var after = BattleStateViewProjector.CreateLocalPerspectiveState(factory.CreateForPlayer(restored, "crit", PlayerId.Player));
            Assert.That(after.PlayerBoard.GetOccupant(attacker.Position).IsHiding, Is.False);
        }

        [Test]
        public void AiRecognizesHidingCriticalLethalWithoutMutatingSource()
        {
            var state = State();
            state.SetActivePlayer(PlayerId.AI);
            state.AI.Master.RemainingAttacksThisTurn = 0;
            var attacker = Unit(state, PlayerId.AI, 0, 0, hiding: true, attackType: AttackType.Ranged);
            state.Player.Master.CurrentHp = 15;
            var provider = new Project333.Runtime.Infrastructure.Data.InMemoryCardDefinitionProvider(
                Array.Empty<Project333.Runtime.Infrastructure.Data.CardDefinition>());
            var command = new AiDecisionService(provider).GetNextCommand(state);
            Assert.That(command, Is.TypeOf<Project333.Runtime.Application.Commands.AttackCommand>());
            var attack = (Project333.Runtime.Application.Commands.AttackCommand)command;
            Assert.That(attack.AttackerCoord, Is.EqualTo(attacker.Position));
            Assert.That(attack.TargetCoord, Is.EqualTo(state.Player.Master.Position));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(15));
            Assert.That(attacker.IsHiding, Is.True);
            new AttackService().Attack(state, PlayerId.AI, attack.AttackerCoord, attack.TargetCoord);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(-5));
        }

        [TestCase(DamageType.Physical)]
        [TestCase(DamageType.Magic)]
        [TestCase(DamageType.Fixed)]
        public void AnyEffectDamageRevealsOnlyAfterActualHpLoss(DamageType type)
        {
            var target = Unit(State(), PlayerId.AI, 0, 0, hiding: true);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 0, type), Is.Zero);
            Assert.That(target.IsHiding, Is.True);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 1, type), Is.EqualTo(1));
            Assert.That(target.IsHiding, Is.False);
        }

        [Test]
        public void EndureAtOneHpAndInvinciblePreserveHidingWithoutHpLoss()
        {
            var target = Unit(State(), PlayerId.AI, 0, 0, hiding: true, endure: true);
            target.CurrentHp = 1;
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Fixed), Is.Zero);
            Assert.That(target.IsHiding, Is.True);
            target.AddInvincibleEffect(InvincibleDurationType.Always);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Magic), Is.Zero);
            Assert.That(target.IsHiding, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnStart_FirewallAndFalloutRevealHiding(bool fallout)
        {
            var state = State();
            var target = Unit(state, PlayerId.AI, 0, 0, hiding: true);
            if (fallout) target.RestoreFallout(0, 1);
            else
            {
                state.Player.Hand.Add("Firewall");
                var definition = new Project333.Runtime.Infrastructure.Data.ScriptedSpellCardDefinition(
                    "Firewall", "Firewall", new Project333.Runtime.Domain.Resources.ResourceSet(),
                    "firewall", damage: 10, damageType: DamageType.Magic, triggerCount: 2);
                var provider = new Project333.Runtime.Infrastructure.Data.InMemoryCardDefinitionProvider(
                    new Project333.Runtime.Infrastructure.Data.CardDefinition[] { definition });
                new SpellService(provider).CastScriptedSpell(state, PlayerId.Player, "Firewall",
                    PlayerId.AI, new TileCoord(4, 0));
            }
            state.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(target.CurrentHp, Is.EqualTo(90));
            Assert.That(target.IsHiding, Is.False);
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(
                new BattleStateViewFactory().CreateForPlayer(state, "reveal", PlayerId.Player));
            Assert.That(restored.AIBoard.GetOccupant(target.Position).IsHiding, Is.False);
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            new MulliganService().PassMulligan(state, PlayerId.Player);
            state.SetPhase(PhaseType.Main);
            return state;
        }

        private static UnitState Unit(BattleState state, PlayerId owner, int col, int row,
            bool hiding = false, int attack = 10, int defense = 0, int hits = 1,
            AttackType attackType = AttackType.Melee, DamageType type = DamageType.Physical,
            bool flying = false, bool shielder = false, bool piercing = false, int spellPower = 0,
            bool endure = false, bool lifeSteal = false)
        {
            var id = owner + "-" + col + "-" + row;
            var unit = new UnitState(id, "Goblin", owner, new TileCoord(col, row), attackType, attack, 100,
                true, false, 0, hitsPerAttack: hits, hasHiding: hiding, damageType: type,
                physicalDefense: defense, magicDefense: defense, hasFlying: flying, hasShielder: shielder,
                hasPiercing: piercing, spellPower: spellPower, hasEndure: endure, hasLifeSteal: lifeSteal);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }

        private static void Attack(BattleState state, UnitState attacker, UnitState target) =>
            new AttackService().Attack(state, attacker.OwnerId, attacker.Position, target.Position);
    }
}
