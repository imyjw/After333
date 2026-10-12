using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class IronDomeTests
    {
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private static readonly TileCoord AttackerCoord = new TileCoord(0, 1);
        private static readonly TileCoord OtherCoord = new TileCoord(1, 0);

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void InstallIsFreeSecretNonUpgradeableAndRejectsSameCardAtomically(PlayerId owner)
        {
            var state = State(owner);
            var definition = IronDomeCardRules.CreateDefinition();
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(owner, definition.CardId, 13);
            state.GetPlayer(owner).Hand.Add(definition.CardId, "selected", false);
            state.GetPlayer(owner).Hand.Add(definition.CardId, "reserve", false);
            var service = new TrapService(new InMemoryCardDefinitionProvider(new[] { definition }), levels);
            var trap = service.Install(state, owner, definition.CardId, "selected");
            Assert.That(trap.RuntimeId, Is.EqualTo("selected"));
            Assert.That(trap.TriggerType, Is.EqualTo(TrapTriggerType.EnemyRangedUnitAttacksMaster));
            Assert.That(trap.EffectType, Is.EqualTo(TrapEffectType.DestroyAttacker));
            Assert.That(new[] { trap.ActivationCost.Mana, trap.ActivationCost.Qi,
                trap.ActivationCost.Power, trap.ActivationCost.Gold }, Is.EqualTo(new[] { 0, 0, 4, 0 }));
            Assert.That(trap.UpgradeLevel, Is.Zero);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.Zero);
            Assert.That(state.GetPlayer(owner).Discard.CardIds, Is.Empty);
            Assert.That(state.GetPlayer(owner).Hand.Cards.Single().RuntimeId, Is.EqualTo("reserve"));
            Assert.That(state.TrapEvents.Single().CardId, Is.Empty);
            Assert.That(state.TrapEvents.Single().RuntimeId, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => service.Install(state, owner, definition.CardId, "reserve"));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(owner).Hand.Cards.Single().RuntimeId, Is.EqualTo("reserve"));
        }

        [TestCase(PlayerId.Player, AttackType.Ranged)]
        [TestCase(PlayerId.AI, AttackType.Ranged)]
        public void OrdinaryRangedUnitAttackAtMasterIsCanceledBeforeDamageAndDirectlyDestroyed(PlayerId owner, AttackType type)
        {
            var state = State(owner);
            Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, Opponent(owner), attackType: type, protections: true);
            attacker.RemainingAttacksThisTurn = 2;
            Attack(state, attacker, state.GetPlayer(owner).Master);
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.GetBoard(attacker.OwnerId).GetOccupant(attacker.Position), Is.Null);
            Assert.That(attacker.CurrentHp, Is.EqualTo(100), "Direct destruction creates no HP damage packet.");
            Assert.That(attacker.EndureUsed, Is.False);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(1));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.Zero);
            Assert.That(state.GetPlayer(owner).Discard.CardIds,
                Is.EqualTo(new[] { IronDomeCardRules.CardId }));
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Count, Is.Zero);
            Assert.That(state.ValuePopupEvents, Is.Empty, "No attack, counter, piercing, lifesteal or reflected damage.");
            var triggered = state.TrapEvents.Single();
            Assert.That(triggered.CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(triggered.TargetOwnerId, Is.EqualTo(attacker.OwnerId));
            Assert.That(triggered.TargetRuntimeId, Is.EqualTo(attacker.RuntimeId));
            Assert.That(triggered.TargetCoord, Is.EqualTo(AttackerCoord));
            Assert.That(triggered.AddedToHand, Is.False);
        }

        [TestCase(4, 4, 0, 4)]
        [TestCase(6, 0, 2, 0)]
        [TestCase(2, 4, 0, 2)]
        [TestCase(0, 4, 0, 0)]
        public void ActivationUsesPowerFirstAndOneToOneGoldSubstitution(int power, int gold, int expectedPower, int expectedGold)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(9, 9, power, gold));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Resources.Power, Is.EqualTo(expectedPower));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(expectedGold));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(9));
            Assert.That(state.Player.Resources.Qi, Is.EqualTo(9));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
        }

        [Test]
        public void InsufficientCombinedPowerAndGoldLeavesTheTrapForALaterAffordableAttack()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(20, 20, 2, 1));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.Player.Resources.Power, Is.EqualTo(2));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            state.Player.Resources.Add(new ResourceSet(0, 0, 1, 0));
            attacker.RemainingAttacksThisTurn = 1;
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.Player.Resources.Power, Is.Zero);
            Assert.That(state.Player.Resources.Gold, Is.Zero);
        }

        [TestCase(PlayerId.Player, "master")]
        [TestCase(PlayerId.AI, "master")]
        [TestCase(PlayerId.Player, "building")]
        [TestCase(PlayerId.AI, "building")]
        [TestCase(PlayerId.Player, "unitTarget")]
        [TestCase(PlayerId.AI, "unitTarget")]
        [TestCase(PlayerId.Player, "fakeMaster")]
        [TestCase(PlayerId.AI, "fakeMaster")]
        public void MastersBuildingsAndTargetsOtherThanActualOwnMasterDoNotMatch(PlayerId owner, string excluded)
        {
            var state = State(owner);
            Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 0));
            OccupantState attacker;
            var target = (OccupantState)state.GetPlayer(owner).Master;
            if (excluded == "master") attacker = state.GetPlayer(Opponent(owner)).Master;
            else if (excluded == "building")
            {
                attacker = new BuildingState("building-attacker", "fixture", Opponent(owner), AttackerCoord, true, 20, 100);
                state.GetBoard(Opponent(owner)).Place(attacker.Position, attacker);
            }
            else if (excluded == "fakeMaster")
            {
                attacker = Attacker(state, Opponent(owner));
                target = new MasterState("not-owner-master", owner, OtherCoord, 3, 333);
                state.GetBoard(owner).Place(target.Position, target);
            }
            else
            {
                attacker = Attacker(state, Opponent(owner));
                target = new UnitState("other-target", "fixture", owner, OtherCoord,
                    AttackType.Ranged, 1, 100, true, false, 0);
                state.GetBoard(owner).Place(target.Position, target);
            }
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            var hp = target.CurrentHp;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(hp - (excluded == "master" ? 3 : 20)));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.False);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(4));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        [TestCase("drained")]
        [TestCase("seal")]
        [TestCase("stun")]
        [TestCase("freeze")]
        public void RejectedActionsDoNotSpendRevealConsumeOrDestroy(string status)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, PlayerId.AI);
            if (status == "drained") attacker.IsDrained = true;
            else if (status == "seal") attacker.EnterSealbound(1);
            else if (status == "stun") attacker.ApplyStun(1);
            else attacker.ApplyFreeze(1);
            PrepareAttack(state, PlayerId.AI);
            Assert.Throws<InvalidOperationException>(() =>
                new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(4));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.LastAttackResolution, Is.Null);
        }

        [Test]
        public void UnaffordableManaCannonAttackCannotSpendOrActivateIronDome()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, PlayerId.AI, attackType: AttackType.Ranged, cardId: ManaCannonRules.CardId);
            PrepareAttack(state, PlayerId.AI);
            Assert.Throws<InvalidOperationException>(() =>
                new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(4));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.ValuePopupEvents, Is.Empty);
            Assert.That(state.LastAttackResolution, Is.Null);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InstallationOrderWithReturnConsumesOnlyTheFirstAffordableMatch(bool domeFirst)
        {
            var state = State(PlayerId.Player);
            var dome = IronDomeCardRules.CreateDefinition();
            var returning = TrapReturnCardRules.CreateDefinition();
            Arm(state, PlayerId.Player, domeFirst ? dome : returning);
            Arm(state, PlayerId.Player, domeFirst ? returning : dome);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 4));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(domeFirst ? dome.CardId : returning.CardId));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(domeFirst ? returning.CardId : dome.CardId));
            Assert.That(state.AI.Hand.Count, Is.EqualTo(domeFirst ? 0 : 1));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(domeFirst ? 0 : 4));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(domeFirst ? 4 : 0));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void EarlierUnaffordableDomeRemainsWhileLaterMirrorTriggers()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            Arm(state, PlayerId.Player, TrapMirrorFieldCardRules.CreateDefinition());
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void HuanShuChecksItsFinalTargetAndAlliedOrOrdinaryUnitRedirectsKeepDome(int selected)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var unit = new UnitState("other-target", "fixture", PlayerId.Player, OtherCoord,
                AttackType.Ranged, 1, 100, true, false, 0);
            state.PlayerBoard.Place(unit.Position, unit);
            var attacker = Attacker(state, PlayerId.AI);
            attacker.ApplyHuanShu();
            PrepareAttack(state, PlayerId.AI);
            var calls = 0;
            new AttackService(new TargetingService(), count =>
            {
                Assert.That(count, Is.EqualTo(3));
                calls++;
                return selected;
            }).Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.EqualTo(selected == 1));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(selected == 1 ? 0 : 1));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(selected == 1 ? 0 : 4));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(unit.CurrentHp, Is.EqualTo(selected == 0 ? 80 : 100));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(selected == 2 ? 313 : 333));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RangedManaCannonPaysItsAttackCostBeforeIronDomeCancelsIt(bool goldOnly)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            state.AI.Resources.Add(goldOnly ? new ResourceSet(0, 0, 0, 1) : new ResourceSet(1, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, attackType: AttackType.Ranged, cardId: ManaCannonRules.CardId);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.AI.Resources.Mana, Is.Zero);
            Assert.That(state.AI.Resources.Gold, Is.Zero);
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.Player.Resources.Power, Is.Zero);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DemonKingUsesRevivalUnlessErasedAndNeverGetsNegativeActionCount(bool erased)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, PlayerId.AI, cardId: DemonKingRules.CardId);
            if (erased)
            {
                // No attack has been spent before erasure reduces the two-attack fixture to one ordinary attack.
                attacker.RemainingAttacksThisTurn = attacker.MaxAttacksPerTurn;
                attacker.ApplyErasure();
                Assert.That(attacker.IsErasure, Is.True);
            }
            Assert.That(attacker.RemainingAttacksThisTurn, Is.GreaterThan(0), "The incoming attack must be legal before the trap is checked.");
            Attack(state, attacker, state.Player.Master);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.AI.Hand.Count, Is.Zero);
            Assert.That(state.ValuePopupEvents, Is.Empty);
            if (erased) Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
            else
            {
                Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
                Assert.That(attacker.IsDemonKingRevivalPending, Is.True);
                Assert.That(attacker.IsSealbound, Is.True);
                Assert.That(attacker.DemonKingRevivalCountdownPlayerId, Is.EqualTo(PlayerId.AI));
                Assert.That(attacker.DemonKingRevivalEligibleAfterTurnNumber, Is.EqualTo(5));
            }
        }

        [Test]
        public void DirectDestructionWithdrawsChieftainAuraAndSettlesCollateralDeathWithoutDamage()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var orc = new UnitState("orc", "fixture", PlayerId.AI, OtherCoord,
                AttackType.Ranged, 20, 30, true, false, 0, hasEndure: true, races: new[] { UnitRace.Orc });
            state.AIBoard.Place(orc.Position, orc);
            var chief = new UnitState("chief", OrcChieftainRules.CardId, PlayerId.AI, AttackerCoord,
                AttackType.Ranged, 50, 70, true, false, 0, races: new[] { UnitRace.Orc });
            chief.HasSummoningSickness = false;
            chief.RemainingAttacksThisTurn = 1;
            state.AIBoard.Place(chief.Position, chief);
            Assert.That(orc.MaxHp, Is.EqualTo(40));
            orc.CurrentHp = 8;
            orc.AddInvincibleEffect(InvincibleDurationType.Always);
            Attack(state, chief, state.Player.Master);
            Assert.That(state.AIBoard.GetOccupant(chief.Position), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(orc.Position), Is.Null);
            Assert.That(orc.MaxHp, Is.EqualTo(30));
            Assert.That(orc.CurrentHp, Is.EqualTo(-2));
            Assert.That(orc.EndureUsed, Is.False);
            Assert.That(orc.CombatStatBuffs, Is.Empty);
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void SharedDestructionEffectChainMayDefeatMasterButCannotTurnIntoTrapDamage()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            // A synthetic attacking Unit carries the existing destruction-effect identity
            // to exercise the shared queue independently of the currently nonattacking building card.
            var attacker = Attacker(state, PlayerId.AI, cardId: NuclearPowerPlantRules.CardId);
            state.Player.Master.CurrentHp = 20;
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(2));
            Assert.That(state.ValuePopupEvents.All(cue => cue.Cause == BattleValueChangeCause.NuclearPowerPlant &&
                cue.SourceCardId == NuclearPowerPlantRules.CardId && cue.SourceRuntimeId == attacker.RuntimeId), Is.True);
            Assert.That(state.TrapEvents.Single().TargetRuntimeId, Is.EqualTo(attacker.RuntimeId));
        }

        [Test]
        public void TransferredAttackerUsesItsCurrentOwnerAndNeitherShielderTakesDamage()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, PlayerId.Player);
            state.PlayerBoard.Remove(attacker.Position);
            attacker.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(attacker.Position, attacker);
            var ownShield = new UnitState("own-shield", "fixture", PlayerId.Player, new TileCoord(2, 0),
                AttackType.Ranged, 1, 100, true, false, 0, hasShielder: true, hasFlying: true);
            var enemyShield = new UnitState("enemy-shield", "fixture", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Ranged, 1, 100, true, false, 0, hasShielder: true);
            state.PlayerBoard.Place(ownShield.Position, ownShield);
            state.AIBoard.Place(enemyShield.Position, enemyShield);
            Assert.That(ownShield.HasActiveShielder && ownShield.DoesNotBlockFrontRow, Is.True);
            Assert.That(ShielderService.ResolveForNormalAttack(state.PlayerBoard, state.Player.Master.Position, attacker).Shielder,
                Is.SameAs(ownShield), "The legal melee target still has an active redirecting Shielder.");
            Assert.That(new TargetingService().CanTarget(state, attacker.OwnerId, attacker.Position, state.Player.Master.Position), Is.True);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.TrapEvents.Single().TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(ownShield.CurrentHp, Is.EqualTo(100));
            Assert.That(enemyShield.CurrentHp, Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void CanonicalMetadataValidatesWithoutAnImmediateDamageSpellAndBuildsTheSharedDefinition()
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == IronDomeCardRules.CardId);
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.True, string.Join(Environment.NewLine, validation.Errors));
            var definition = record.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.TriggerType, Is.EqualTo(TrapTriggerType.EnemyRangedUnitAttacksMaster));
            Assert.That(definition.EffectType, Is.EqualTo(TrapEffectType.DestroyAttacker));
            Assert.That(definition.ActivationCost.Power, Is.EqualTo(4));
            Assert.That(definition.Cost.Power, Is.Zero);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
        }

        [TestCase("activation")]
        [TestCase("setup")]
        [TestCase("affiliation")]
        [TestCase("rarity")]
        [TestCase("trigger")]
        [TestCase("oldAnyUnitTrigger")]
        [TestCase("effect")]
        [TestCase("damage")]
        [TestCase("damageType")]
        [TestCase("rewards")]
        public void ValidatorRejectsChangesToTheConfirmedIronDomeContract(string mutation)
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == IronDomeCardRules.CardId);
            switch (mutation)
            {
                case "activation": record.TrapActivationCost.Power = 5; break;
                case "setup": record.Cost.Power = 1; break;
                case "affiliation": record.Affiliation = CardAffiliation.Fantasy; break;
                case "rarity": record.Rarity = CardRarity.Common; break;
                case "trigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksUnit; break;
                case "oldAnyUnitTrigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksMaster; break;
                case "effect": record.TrapEffect = TrapEffectType.ReflectAttackerAttackAsMagic; break;
                case "damage": record.Damage = 100; break;
                case "damageType": record.DamageType = DamageType.Magic; break;
                case "rewards": record.IncludeInRewards = true; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.Contains(IronDomeCardRules.CardId)), Is.True,
                string.Join(Environment.NewLine, validation.Errors));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void MeleeAttackDealsOrdinaryDamageAndLeavesAffordableDomePrivateAndArmed(PlayerId owner)
        {
            var state = State(owner);
            Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 2));
            var attacker = Attacker(state, Opponent(owner), attackType: AttackType.Melee);
            Attack(state, attacker, state.GetPlayer(owner).Master);
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(313));
            Assert.That(attacker.CurrentHp, Is.EqualTo(97), "An ignored melee attack still receives the ordinary Master counterattack.");
            Assert.That(state.GetBoard(attacker.OwnerId).GetOccupant(attacker.Position), Is.SameAs(attacker));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.False);
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(4));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(2));
            Assert.That(state.GetPlayer(owner).Discard.CardIds, Is.Empty);
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.ValuePopupEvents.Any(e => e.RuntimeId == state.GetPlayer(owner).Master.RuntimeId), Is.True);
        }

        [TestCase(PlayerId.Player, false)]
        [TestCase(PlayerId.Player, true)]
        [TestCase(PlayerId.AI, false)]
        [TestCase(PlayerId.AI, true)]
        public void EarlierRangedDomeIsSkippedForMeleeAttackAndLaterAnyUnitTrapCanTrigger(PlayerId owner, bool mirror)
        {
            var state = State(owner);
            Arm(state, owner);
            var later = mirror ? TrapMirrorFieldCardRules.CreateDefinition() : TrapReturnCardRules.CreateDefinition();
            Arm(state, owner, later);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(2, 0, 4, 4));
            var attacker = Attacker(state, Opponent(owner), attackType: AttackType.Melee);
            Attack(state, attacker, state.GetPlayer(owner).Master);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(later.CardId));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(4));
            Assert.That(state.GetPlayer(owner).Resources.Mana, Is.EqualTo(mirror ? 0 : 2));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(mirror ? 4 : 0));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Count, Is.EqualTo(mirror ? 0 : 1));
            if (mirror) Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            else Assert.That(state.GetBoard(attacker.OwnerId).GetOccupant(attacker.Position), Is.Null);
        }

        [Test]
        public void TriggerEnumRetainsPriorCapturedValuesAndAppendsRangedMasterCondition()
        {
            Assert.That((int)TrapTriggerType.None, Is.Zero);
            Assert.That((int)TrapTriggerType.EnemyUnitAttacksMaster, Is.EqualTo(1));
            Assert.That((int)TrapTriggerType.EnemyUnitAttacksUnit, Is.EqualTo(2));
            Assert.That((int)TrapTriggerType.EnemyMeleeUnitAttacksMaster, Is.EqualTo(3));
            Assert.That((int)TrapTriggerType.EnemyRangedUnitAttacksMaster, Is.EqualTo(4));
        }

        [TestCase(PlayerId.Player, DamageType.Physical)]
        [TestCase(PlayerId.AI, DamageType.Physical)]
        [TestCase(PlayerId.Player, DamageType.Magic)]
        [TestCase(PlayerId.AI, DamageType.Magic)]
        public void RangedConditionDoesNotDependOnPhysicalOrMagicDamageOrFlying(PlayerId owner, DamageType damageType)
        {
            var state = State(owner); Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 0));
            var attacker = Attacker(state, Opponent(owner), damageType: damageType);
            Attack(state, attacker, state.GetPlayer(owner).Master);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.GetBoard(attacker.OwnerId).GetOccupant(attacker.Position), Is.Null);
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player, AttackType.Melee)]
        [TestCase(PlayerId.AI, AttackType.Melee)]
        [TestCase(PlayerId.Player, AttackType.Ranged)]
        [TestCase(PlayerId.AI, AttackType.Ranged)]
        public void MeleeAndRangedDestroyTrapsSkipOppositeAttackModeAndConsumeOnlyTheirMatchingCard(PlayerId owner, AttackType mode)
        {
            var state = State(owner);
            Arm(state, owner);
            Arm(state, owner, ExplosiveReactiveArmourCardRules.CreateDefinition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 8, 0));
            var attacker = Attacker(state, Opponent(owner), attackType: mode);
            Attack(state, attacker, state.GetPlayer(owner).Master);
            var matching = mode == AttackType.Ranged ? IronDomeCardRules.CardId : ExplosiveReactiveArmourCardRules.CardId;
            var remaining = mode == AttackType.Ranged ? ExplosiveReactiveArmourCardRules.CardId : IronDomeCardRules.CardId;
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(matching));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(remaining));
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(4));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(
            File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));

        private static BattleState State(PlayerId owner)
        {
            var playerMaster = new MasterState("master-player", PlayerId.Player, MasterCoord, 3, 333);
            var aiMaster = new MasterState("master-ai", PlayerId.AI, MasterCoord, 3, 333);
            var player = new PlayerState(PlayerId.Player, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), playerMaster);
            var ai = new PlayerState(PlayerId.AI, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), aiMaster);
            var playerBoard = new BoardState(); playerBoard.Place(MasterCoord, playerMaster);
            var aiBoard = new BoardState(); aiBoard.Place(MasterCoord, aiMaster);
            var state = new BattleState(player, ai, playerBoard, aiBoard);
            state.RestoreRuntimeState(5, owner, PhaseType.Main);
            return state;
        }

        private static void Arm(BattleState state, PlayerId owner, TrapSpellCardDefinition definition = null)
        {
            definition = definition ?? IronDomeCardRules.CreateDefinition();
            state.SetActivePlayer(owner);
            var runtimeId = definition.CardId + "-armed";
            state.GetPlayer(owner).Hand.Add(definition.CardId, runtimeId, false);
            new TrapService(new InMemoryCardDefinitionProvider(new[] { definition })).Install(state, owner, definition.CardId, runtimeId);
        }

        private static UnitState Attacker(BattleState state, PlayerId owner, AttackType attackType = AttackType.Ranged,
            string cardId = "fixture", bool protections = false, DamageType damageType = DamageType.Physical)
        {
            var attacker = new UnitState("attacker", cardId, owner, AttackerCoord, attackType, 20, 100, true, false, 0,
                maxAttacksPerTurn: 2, hitsPerAttack: protections ? 4 : 1, hasEndure: protections,
                hasLifeSteal: protections, physicalDefense: protections ? 1000 : 0, magicDefense: protections ? 1000 : 0,
                hasHiding: protections, hasFlying: protections, hasPiercing: protections, damageType: damageType);
            if (protections) attacker.AddInvincibleEffect(InvincibleDurationType.Always);
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(attacker.Position, attacker);
            return attacker;
        }

        private static void PrepareAttack(BattleState state, PlayerId owner)
        {
            state.SetActivePlayer(owner);
            state.ClearTrapEvents();
            state.ClearValuePopupEvents();
        }

        private static void Attack(BattleState state, OccupantState attacker, OccupantState target)
        {
            PrepareAttack(state, attacker.OwnerId);
            new AttackService().Attack(state, attacker.OwnerId, attacker.Position, target.Position);
        }

        private static PlayerId Opponent(PlayerId owner) => owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
    }
}
