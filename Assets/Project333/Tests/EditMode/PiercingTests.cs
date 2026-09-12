using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class PiercingTests
    {
        [Test]
        public void Attack_PiercingDamagesRearOccupantWithItsOwnDefenseAndNoCounterattack()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 10,
                maxHp: 20,
                hasPiercing: true);
            var front = CreateUnit(
                "front",
                PlayerId.AI,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 4,
                maxHp: 20,
                physicalDefense: 2);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 99,
                maxHp: 20,
                physicalDefense: 3);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(12));
            Assert.That(rear.CurrentHp, Is.EqualTo(13));
            Assert.That(attacker.CurrentHp, Is.EqualTo(20), "The rear occupant must not counterattack.");
            Assert.That(
                battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing).Amount,
                Is.EqualTo(7));
        }

        [Test]
        public void Attack_PiercingDirectRearTargetDamagesFrontOccupant()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 10,
                maxHp: 20,
                hasPiercing: true);
            var front = CreateUnit(
                "front",
                PlayerId.AI,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 20);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: 20);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, rear.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(10));
            Assert.That(rear.CurrentHp, Is.EqualTo(10));
            Assert.That(
                battleState.ValuePopupEvents.Count(value => value.Cause == BattleValueChangeCause.Piercing),
                Is.EqualTo(1));
            Assert.That(
                battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing).Amount,
                Is.EqualTo(10));
        }

        [Test]
        public void Attack_MultihitPiercingAppliesPerHitAndLifeStealUsesActualFrontAndRearHpLoss()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "multihit-piercing",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 50,
                hitsPerAttack: 3,
                hasLifeSteal: true,
                hasPiercing: true);
            var front = CreateUnit(
                "front",
                PlayerId.AI,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 100);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: 100,
                physicalDefense: 2);
            PrepareAttacker(attacker);
            attacker.CurrentHp = 1;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(85));
            Assert.That(rear.CurrentHp, Is.EqualTo(91));
            Assert.That(attacker.CurrentHp, Is.EqualTo(25));
            Assert.That(
                battleState.ValuePopupEvents.Count(value => value.Cause == BattleValueChangeCause.Piercing),
                Is.EqualTo(3));
            Assert.That(
                battleState.ValuePopupEvents
                    .Where(value => value.Cause == BattleValueChangeCause.LifeSteal)
                    .Sum(value => value.Amount),
                Is.EqualTo(24));
        }

        [Test]
        public void Attack_FinalPiercingHitResolvesBeforeCounterattackAndDeadAttackerDoesNotLifeSteal()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "melee-piercing",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 10,
                maxHp: 5,
                hasLifeSteal: true,
                hasPiercing: true);
            var front = CreateUnit(
                "front-counter",
                PlayerId.AI,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 5,
                maxHp: 20);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 50,
                maxHp: 20);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(10));
            Assert.That(rear.CurrentHp, Is.EqualTo(10));
            Assert.That(battleState.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(
                battleState.ValuePopupEvents.Count(value => value.Cause == BattleValueChangeCause.LifeSteal),
                Is.Zero);
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 3)]
        [TestCase(1, 3)]
        public void Attack_PiercingShielderAbsorbsBothPacketsFromEitherRow(int targetRow, int defense)
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 10,
                maxHp: 20,
                hasPiercing: true);
            var frontShielder = CreateUnit(
                "front-shielder",
                PlayerId.AI,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 30,
                physicalDefense: defense,
                hasShielder: true);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: 30);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(frontShielder.Position, frontShielder);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, new TileCoord(1, targetRow));

            Assert.That(frontShielder.CurrentHp, Is.EqualTo(30 - 2 * (10 - defense)));
            Assert.That(rear.CurrentHp, Is.EqualTo(30));
            var normalHit = battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.NormalAttack);
            var piercingHit = battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing);
            Assert.That(normalHit.RuntimeId, Is.EqualTo(frontShielder.RuntimeId));
            Assert.That(piercingHit.RuntimeId, Is.EqualTo(frontShielder.RuntimeId));
            Assert.That(normalHit.Amount, Is.EqualTo(10 - defense));
            Assert.That(piercingHit.Amount, Is.EqualTo(10 - defense));
        }

        [Test]
        public void Attack_RedirectedPiercingOverflowsAfterBothDefensesAndLifeStealsOnlyActualHpLoss()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear,
                shielderHp: 12, shielderDefense: 2, rearDefense: 3, hasLifeSteal: true);
            attacker.CurrentHp = 1;

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(-1),
                "Direct hit: 10-2=8. Redirected Piercing: (10-3)-2=5, including 1 overflow.");
            Assert.That(battleState.AIBoard.GetOccupant(front.Position), Is.Null);
            Assert.That(rear.CurrentHp, Is.EqualTo(29));
            Assert.That(attacker.CurrentHp, Is.EqualTo(14));
            var piercingHits = battleState.ValuePopupEvents.Where(value => value.Cause == BattleValueChangeCause.Piercing).ToList();
            Assert.That(piercingHits.Count, Is.EqualTo(2));
            Assert.That(piercingHits.Single(value => value.RuntimeId == front.RuntimeId).Amount, Is.EqualTo(4));
            Assert.That(piercingHits.Single(value => value.RuntimeId == rear.RuntimeId).Amount, Is.EqualTo(1));
        }

        [Test]
        public void Attack_ShielderKilledByDirectHitCannotRedirectPiercing()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear, shielderHp: 10);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(battleState.AIBoard.GetOccupant(front.Position), Is.Null);
            Assert.That(rear.CurrentHp, Is.EqualTo(20));
            var piercingHit = battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing);
            Assert.That(piercingHit.RuntimeId, Is.EqualTo(rear.RuntimeId));
            Assert.That(piercingHit.Amount, Is.EqualTo(10));
        }

        [Test]
        public void Attack_MultihitRedirectedPiercingAppliesDefenseAndLifeStealPerPacket()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear,
                shielderHp: 100, shielderDefense: 2, hitsPerAttack: 3, hasLifeSteal: true);
            attacker.CurrentHp = 1;

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(52));
            Assert.That(rear.CurrentHp, Is.EqualTo(30));
            Assert.That(attacker.CurrentHp, Is.EqualTo(49));
            var piercingHits = battleState.ValuePopupEvents.Where(value => value.Cause == BattleValueChangeCause.Piercing).ToList();
            Assert.That(piercingHits.Count, Is.EqualTo(3));
            Assert.That(piercingHits.All(value => value.RuntimeId == front.RuntimeId && value.Amount == 8), Is.True);
        }

        [Test]
        public void Attack_MeleeRedirectedPiercingDoesNotAddAnotherCounterattack()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear, attackType: AttackType.Melee);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(10));
            Assert.That(rear.CurrentHp, Is.EqualTo(30));
            Assert.That(attacker.CurrentHp, Is.EqualTo(97));
            Assert.That(battleState.ValuePopupEvents.Count(value => value.Cause == BattleValueChangeCause.Counterattack), Is.EqualTo(1));
        }

        [Test]
        public void Attack_InvincibleShielderPreventsBothPackets()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear);
            front.AddInvincibleEffect(InvincibleDurationType.Always);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(30));
            Assert.That(rear.CurrentHp, Is.EqualTo(30));
            var piercingHit = battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing);
            Assert.That(piercingHit.RuntimeId, Is.EqualTo(front.RuntimeId));
            Assert.That(piercingHit.Amount, Is.Zero);
            Assert.That(piercingHit.IsInvinciblePrevented, Is.True);
        }

        [Test]
        public void Attack_SealboundRearDoesNotRedirectPiercingToShielder()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear);
            rear.EnterSealbound(2);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(20));
            Assert.That(rear.CurrentHp, Is.EqualTo(30));
            Assert.That(battleState.ValuePopupEvents.Any(value => value.Cause == BattleValueChangeCause.Piercing), Is.False);
        }

        [Test]
        public void Attack_ErasedShielderCannotRedirectPiercing()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear);
            front.ApplyErasure();

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(20));
            Assert.That(rear.CurrentHp, Is.EqualTo(20));
            Assert.That(battleState.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing).RuntimeId,
                Is.EqualTo(rear.RuntimeId));
        }

        [Test]
        public void Attack_ShielderEndureIsConsumedOnlyOnceAcrossDirectAndPiercingPackets()
        {
            var battleState = CreateShielderBattle(out var attacker, out var front, out var rear,
                shielderHp: 5, shielderHasEndure: true);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.EndureUsed, Is.True);
            Assert.That(battleState.AIBoard.GetOccupant(front.Position), Is.Null);
            Assert.That(rear.CurrentHp, Is.EqualTo(21));
            var piercingHits = battleState.ValuePopupEvents.Where(value => value.Cause == BattleValueChangeCause.Piercing).ToList();
            Assert.That(piercingHits.Single(value => value.RuntimeId == front.RuntimeId).Amount, Is.EqualTo(1));
            Assert.That(piercingHits.Single(value => value.RuntimeId == rear.RuntimeId).Amount, Is.EqualTo(9));
        }

        [Test]
        public void Attack_PiercingRearDamageUsesDrainedEndureInvincibleAndSealboundRules()
        {
            var drained = ResolveRearHit(rear => rear.IsDrained = true, rearPhysicalDefense: 99);
            Assert.That(drained.CurrentHp, Is.EqualTo(5));

            var enduring = ResolveRearHit(rear => { }, rearMaxHp: 3, rearHasEndure: true);
            Assert.That(enduring.CurrentHp, Is.EqualTo(1));
            Assert.That(enduring.EndureUsed, Is.True);

            var invincible = ResolveRearHit(rear => rear.AddInvincibleEffect(InvincibleDurationType.Always));
            Assert.That(invincible.CurrentHp, Is.EqualTo(20));

            var sealbound = ResolveRearHit(rear => rear.EnterSealbound(2));
            Assert.That(sealbound.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void Attack_ErasureSuppressesPiercing()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "erased-piercing",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 20,
                hasPiercing: true);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(1, 0), AttackType.Melee, 0, 20);
            var rear = CreateUnit("rear", PlayerId.AI, new TileCoord(1, 1), AttackType.Melee, 0, 20);
            PrepareAttacker(attacker);
            attacker.ApplyErasure();
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(15));
            Assert.That(rear.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void Attack_PiercingRearDamageIsNotStoppedByHidingOrFlying()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 20,
                hasPiercing: true);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(1, 0), AttackType.Melee, 0, 20);
            var rear = CreateUnit(
                "hidden-flying-rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: 20,
                hasHiding: true,
                hasFlying: true);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(rear.CurrentHp, Is.EqualTo(15));
            Assert.That(rear.IsHiding, Is.True, "Piercing damage is not a normal-attack declaration against the rear occupant.");
        }

        [Test]
        public void Attack_HuanShuPiercingUsesResolvedRandomTargetsColumn()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "huan-shu-piercing",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 4,
                maxHp: 20,
                hasPiercing: true);
            var friendlyFront = CreateUnit(
                "friendly-front",
                PlayerId.Player,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 20);
            var friendlyRear = CreateUnit(
                "friendly-rear",
                PlayerId.Player,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: 20);
            var declaredEnemy = CreateUnit(
                "declared-enemy",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 20);
            PrepareAttacker(attacker);
            attacker.ApplyHuanShu();
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.PlayerBoard.Place(friendlyFront.Position, friendlyFront);
            battleState.PlayerBoard.Place(friendlyRear.Position, friendlyRear);
            battleState.AIBoard.Place(declaredEnemy.Position, declaredEnemy);

            new AttackService(new TargetingService(), _ => 0).Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                declaredEnemy.Position);

            Assert.That(friendlyFront.CurrentHp, Is.EqualTo(16));
            Assert.That(friendlyRear.CurrentHp, Is.EqualTo(16));
            Assert.That(declaredEnemy.CurrentHp, Is.EqualTo(20));
            Assert.That(battleState.LastAttackResolution.ResolvedTargetCoord, Is.EqualTo(friendlyFront.Position));
        }

        [Test]
        public void Attack_AttackCapableBuildingCanUsePiercing()
        {
            var battleState = CreateBattleState();
            var attacker = new BuildingState(
                "piercing-building",
                "piercing-building",
                PlayerId.Player,
                new TileCoord(0, 0),
                canAttack: true,
                attack: 6,
                maxHp: 30,
                hasPiercing: true);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(1, 0), AttackType.Melee, 0, 20);
            var rear = CreateUnit("rear", PlayerId.AI, new TileCoord(1, 1), AttackType.Melee, 0, 20);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(14));
            Assert.That(rear.CurrentHp, Is.EqualTo(14));
        }

        [Test]
        public void Attack_PiercingCanDamageFrontBuildingAndRearMaster()
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();
            var playerMaster = new MasterState(
                "player-master",
                PlayerId.Player,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333);
            var aiMaster = new MasterState(
                "ai-master",
                PlayerId.AI,
                new TileCoord(1, 1),
                attack: 3,
                maxHp: 333);
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 6,
                maxHp: 20,
                hasPiercing: true);
            var frontBuilding = new BuildingState(
                "front-building",
                "front-building",
                PlayerId.AI,
                new TileCoord(1, 0),
                canAttack: false,
                attack: 0,
                maxHp: 20,
                damageType: DamageType.None);
            playerBoard.Place(playerMaster.Position, playerMaster);
            playerBoard.Place(attacker.Position, attacker);
            aiBoard.Place(aiMaster.Position, aiMaster);
            aiBoard.Place(frontBuilding.Position, frontBuilding);
            var battleState = new BattleState(
                CreatePlayer(PlayerId.Player, playerMaster),
                CreatePlayer(PlayerId.AI, aiMaster),
                playerBoard,
                aiBoard);
            battleState.RestoreRuntimeState(1, PlayerId.Player, PhaseType.Main);
            PrepareAttacker(attacker);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, frontBuilding.Position);

            Assert.That(frontBuilding.CurrentHp, Is.EqualTo(14));
            Assert.That(aiMaster.CurrentHp, Is.EqualTo(327));
        }

        [Test]
        public void StateViewRoundTrip_PreservesPiercingForMasterBuildingAndUnit()
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();
            var playerMaster = new MasterState(
                "player-master",
                PlayerId.Player,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333,
                hasPiercing: true);
            var aiMaster = new MasterState(
                "ai-master",
                PlayerId.AI,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333);
            var building = new BuildingState(
                "building",
                "building",
                PlayerId.Player,
                new TileCoord(0, 0),
                canAttack: true,
                attack: 2,
                maxHp: 20,
                hasPiercing: true);
            var unit = CreateUnit(
                "unit",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 2,
                maxHp: 20,
                hasPiercing: true);
            playerBoard.Place(playerMaster.Position, playerMaster);
            playerBoard.Place(building.Position, building);
            aiBoard.Place(aiMaster.Position, aiMaster);
            aiBoard.Place(unit.Position, unit);
            var battleState = new BattleState(
                CreatePlayer(PlayerId.Player, playerMaster),
                CreatePlayer(PlayerId.AI, aiMaster),
                playerBoard,
                aiBoard);
            battleState.RestoreRuntimeState(1, PlayerId.Player, PhaseType.Main);

            var view = new BattleStateViewFactory().CreateForPlayer(battleState, "piercing-match", PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projected.Player.Master.HasPiercing, Is.True);
            Assert.That(projected.PlayerBoard.GetOccupant(building.Position).HasPiercing, Is.True);
            Assert.That(projected.AIBoard.GetOccupant(unit.Position).HasPiercing, Is.True);
        }

        private static BattleState CreateShielderBattle(
            out UnitState attacker,
            out UnitState front,
            out UnitState rear,
            AttackType attackType = AttackType.Ranged,
            int shielderHp = 30,
            int shielderDefense = 0,
            int rearDefense = 0,
            int hitsPerAttack = 1,
            bool hasLifeSteal = false,
            bool shielderHasEndure = false)
        {
            var battleState = CreateBattleState();
            attacker = CreateUnit("piercing-attacker", PlayerId.Player, new TileCoord(0, 0), attackType, 10, 100,
                hitsPerAttack: hitsPerAttack, hasLifeSteal: hasLifeSteal, hasPiercing: true);
            front = CreateUnit("front-shielder", PlayerId.AI, new TileCoord(1, 0), AttackType.Melee, 3, shielderHp,
                physicalDefense: shielderDefense, hasShielder: true, hasEndure: shielderHasEndure);
            rear = CreateUnit("rear", PlayerId.AI, new TileCoord(1, 1), AttackType.Melee, 50, 30,
                physicalDefense: rearDefense);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);
            return battleState;
        }

        private static UnitState ResolveRearHit(
            System.Action<UnitState> configureRear,
            int rearMaxHp = 20,
            int rearPhysicalDefense = 0,
            bool rearHasEndure = false)
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "piercing-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 20,
                hasPiercing: true);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(1, 0), AttackType.Melee, 0, 20);
            var rear = CreateUnit(
                "rear",
                PlayerId.AI,
                new TileCoord(1, 1),
                AttackType.Melee,
                attack: 0,
                maxHp: rearMaxHp,
                physicalDefense: rearPhysicalDefense,
                hasEndure: rearHasEndure);
            configureRear(rear);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(rear.Position, rear);

            new AttackService().Attack(battleState, PlayerId.Player, attacker.Position, front.Position);
            return rear;
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp,
            int physicalDefense = 0,
            int hitsPerAttack = 1,
            bool hasShielder = false,
            bool hasLifeSteal = false,
            bool hasEndure = false,
            bool hasPiercing = false,
            bool hasHiding = false,
            bool hasFlying = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: hitsPerAttack,
                hasEndure: hasEndure,
                hasShielder: hasShielder,
                hasLifeSteal: hasLifeSteal,
                damageType: DamageType.Physical,
                physicalDefense: physicalDefense,
                hasHiding: hasHiding,
                hasFlying: hasFlying,
                hasPiercing: hasPiercing);
        }

        private static void PrepareAttacker(OccupantState attacker)
        {
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
        }

        private static BattleState CreateBattleState()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static PlayerState CreatePlayer(PlayerId playerId, MasterState master)
        {
            return new PlayerState(
                playerId,
                new ResourceSet(),
                new DeckState(CreateDeck(playerId.ToString())),
                new HandState(),
                new DiscardState(),
                master);
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var index = 0; index < 10; index++)
            {
                cards.Add($"{prefix}-{index:00}");
            }

            return cards;
        }
    }
}
