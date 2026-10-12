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
    public sealed class TrapInstallationTests
    {
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private static readonly TileCoord AttackCoord = new TileCoord(0, 0);

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void InstallMovesExactHandInstanceForFreeWithoutDiscardOrPublicIdentity(PlayerId ownerId)
        {
            var state = State(ownerId);
            var definition = TrapReturnCardRules.CreateDefinition();
            state.GetPlayer(ownerId).Hand.Add(definition.CardId, "selected-trap", false);
            var processor = Processor(definition);
            processor.Execute(state, ownerId, new InstallTrapCommand(definition.CardId, "selected-trap"));
            var trap = state.ArmedTraps.Single();
            Assert.That(trap.OwnerId, Is.EqualTo(ownerId));
            Assert.That(trap.RuntimeId, Is.EqualTo("selected-trap"));
            Assert.That(trap.CardId, Is.EqualTo(definition.CardId));
            Assert.That(trap.ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(trap.InstalledTurn, Is.EqualTo(5));
            Assert.That(state.GetPlayer(ownerId).Hand.Count, Is.Zero);
            Assert.That(state.GetPlayer(ownerId).Discard.CardIds, Is.Empty);
            Assert.That(state.GetPlayer(ownerId).Resources.Gold, Is.Zero);
            Assert.That(state.GetArmedTrapCount(ownerId), Is.EqualTo(1));
            AssertInstalledIsAnonymous(state.TrapEvents.Single(), ownerId, 1);
        }

        [Test]
        public void CapturedActivationCostCannotBeMutatedThroughDefinitionStateOrCallerReference()
        {
            var sourceCost = new ResourceSet(1, 2, 3, 4);
            var definition = Trap("custom", sourceCost);
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(definition.CardId, "trap", false);
            var trap = new TrapService(Provider(definition)).Install(state, PlayerId.Player, definition.CardId, "trap");
            sourceCost.Add(new ResourceSet(9, 9, 9, 9));
            definition.ActivationCost.Add(new ResourceSet(9, 9, 9, 9));
            trap.ActivationCost.Spend(new ResourceSet(1, 2, 3, 4));
            Assert.That(new[] { trap.ActivationCost.Mana, trap.ActivationCost.Qi,
                trap.ActivationCost.Power, trap.ActivationCost.Gold }, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void DuplicateSameOwnerSameCardRejectsAtomicallyButBothOwnersMayInstallIt()
        {
            var definition = TrapReturnCardRules.CreateDefinition();
            var state = State(PlayerId.Player);
            var service = new TrapService(Provider(definition));
            state.Player.Hand.Add(definition.CardId, "first", false);
            state.Player.Hand.Add(definition.CardId, "second", false);
            service.Install(state, PlayerId.Player, definition.CardId, "first");
            Assert.Throws<InvalidOperationException>(() =>
                service.Install(state, PlayerId.Player, definition.CardId, "second"));
            Assert.That(state.Player.Hand.Cards.Single().RuntimeId, Is.EqualTo("second"));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.Player.Resources.Gold, Is.Zero);
            Assert.That(state.Player.Discard.CardIds, Is.Empty);
            state.SetActivePlayer(PlayerId.AI);
            state.AI.Hand.Add(definition.CardId, "other-owner", false);
            service.Install(state, PlayerId.AI, definition.CardId, "other-owner");
            Assert.That(state.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(state.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(1));
        }

        [TestCase("missing-instance")]
        [TestCase("other-card-instance")]
        public void WrongHandIdentityDoesNotConsumeAnyCardOrAddATrap(string requestedId)
        {
            var definition = TrapReturnCardRules.CreateDefinition();
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(definition.CardId, "right-instance", false);
            state.Player.Hand.Add("Golem", "other-card-instance", false);
            Assert.Throws<InvalidOperationException>(() => new TrapService(Provider(definition))
                .Install(state, PlayerId.Player, definition.CardId, requestedId));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(2));
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.Player.Discard.CardIds, Is.Empty);
        }

        [TestCase(PhaseType.Mulligan)]
        [TestCase(PhaseType.TurnStart)]
        [TestCase(PhaseType.TurnEnd)]
        public void OutsideMainPhaseInstallationLeavesHandAndResourcesUntouched(PhaseType phase)
        {
            var definition = TrapReturnCardRules.CreateDefinition();
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(definition.CardId, "trap", false);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 10));
            state.SetPhase(phase);
            Assert.Throws<InvalidOperationException>(() => new TrapService(Provider(definition))
                .Install(state, PlayerId.Player, definition.CardId, "trap"));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(1));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(10));
            Assert.That(state.ArmedTraps, Is.Empty);
        }

        [Test]
        public void WrongTurnDeadMasterEndedBattleAndNonTrapCardAreRejectedBeforeHandRemoval()
        {
            var definition = TrapReturnCardRules.CreateDefinition();
            var state = State(PlayerId.AI);
            state.Player.Hand.Add(definition.CardId, "trap", false);
            var service = new TrapService(Provider(definition));
            Assert.Throws<InvalidOperationException>(() => service.Install(state, PlayerId.Player, definition.CardId, "trap"));
            state.SetActivePlayer(PlayerId.Player);
            state.Player.Master.CurrentHp = 0;
            Assert.Throws<InvalidOperationException>(() => service.Install(state, PlayerId.Player, definition.CardId, "trap"));
            state.Player.Master.CurrentHp = 333;
            state.EndBattle(PlayerId.Player);
            Assert.Throws<InvalidOperationException>(() => service.Install(state, PlayerId.Player, definition.CardId, "trap"));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(1));
            Assert.That(state.ArmedTraps, Is.Empty);
            state = State(PlayerId.Player);
            var normal = new DamageSpellCardDefinition("firebolt", "firebolt", new ResourceSet(), 10);
            state.Player.Hand.Add(normal.CardId, "ordinary", false);
            Assert.Throws<InvalidOperationException>(() => new TrapService(Provider(normal))
                .Install(state, PlayerId.Player, normal.CardId, "ordinary"));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void InstalledEventsCannotAccidentallyCarryEvenSuppliedSecretFields()
        {
            var trapEvent = new BattleTrapEvent(BattleTrapEventKind.Installed, PlayerId.AI, 2,
                "secret-card", "secret-runtime", 13, PlayerId.AI, "target", "target-card", AttackCoord, true);
            AssertInstalledIsAnonymous(trapEvent, PlayerId.AI, 2);
        }

        [Test]
        public void VisibleTrapsAndProjectedHiddenCountsCombineWithoutDuplicatingOwnedState()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.AI.RestoreHiddenTrapCount(3);
            Assert.That(state.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(state.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(3));
            state.AI.RestoreHiddenTrapCount(-3);
            Assert.That(state.GetArmedTrapCount(PlayerId.AI), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetArmedTrapCount((PlayerId)99));
        }

        [Test]
        public void ProcessorClearsPriorTransientTrapEventsWithoutRemovingArmedTraps()
        {
            var first = Trap("first", new ResourceSet(0, 0, 0, 4));
            var second = Trap("second", new ResourceSet(0, 0, 0, 4));
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(first.CardId, "first", false);
            state.Player.Hand.Add(second.CardId, "second", false);
            var processor = Processor(first, second);
            processor.Execute(state, PlayerId.Player, new InstallTrapCommand(first.CardId, "first"));
            processor.Execute(state, PlayerId.Player, new InstallTrapCommand(second.CardId, "second"));
            Assert.That(state.ArmedTraps.Select(trap => trap.RuntimeId), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            AssertInstalledIsAnonymous(state.TrapEvents[0], PlayerId.Player, 2);
        }

        [Test]
        public void LegacyHandSelectionAndReplicateRemainCompatibleWithInstallation()
        {
            var definition = Trap("replicating-trap", new ResourceSet(0, 0, 0, 4), true);
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(definition.CardId, "permanent", false);
            state.Player.Hand.Add(definition.CardId, "temporary", true);
            Processor(definition).Execute(state, PlayerId.Player, new InstallTrapCommand(definition.CardId));
            Assert.That(state.ArmedTraps.Single().RuntimeId, Is.EqualTo("temporary"));
            Assert.That(state.ArmedTraps.Single().IsTemporaryReplicate, Is.True);
            Assert.That(state.Player.Hand.Cards.Any(card => card.RuntimeId == "permanent"), Is.True);
            Assert.That(state.Player.Hand.Cards.Count(card => card.IsTemporaryReplicate), Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ReturnConsumesGoldAndTrapOnceBeforeAnyDamageHitCounterOrDeath(PlayerId trapOwner)
        {
            var state = State(trapOwner);
            Arm(state, trapOwner);
            state.GetPlayer(trapOwner).Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, Opponent(trapOwner), attackType: AttackType.Melee, hits: 3);
            var hp = attacker.CurrentHp;
            PrepareAttack(state, attacker.OwnerId);
            new AttackService().Attack(state, attacker.OwnerId, attacker.Position, MasterCoord);
            Assert.That(state.GetBoard(attacker.OwnerId).GetOccupant(AttackCoord), Is.Null);
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Cards.Single().CardId, Is.EqualTo(attacker.CardId));
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Cards.Single().IsTemporaryReplicate, Is.False);
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Cards.Single().RuntimeId, Is.Not.EqualTo(attacker.RuntimeId));
            Assert.That(state.GetPlayer(trapOwner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(attacker.CurrentHp, Is.EqualTo(hp));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.ValuePopupEvents, Is.Empty);
            Assert.That(state.GetPlayer(trapOwner).Resources.Gold, Is.Zero);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.GetPlayer(trapOwner).Discard.CardIds, Is.EqualTo(new[] { TrapReturnCardRules.CardId }));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            var trigger = state.TrapEvents.Single();
            Assert.That(trigger.Kind, Is.EqualTo(BattleTrapEventKind.Triggered));
            Assert.That(trigger.CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(trigger.RuntimeId, Is.EqualTo("trap-" + trapOwner));
            Assert.That(trigger.TargetRuntimeId, Is.EqualTo(attacker.RuntimeId));
            Assert.That(trigger.TargetOwnerId, Is.EqualTo(attacker.OwnerId));
            Assert.That(trigger.TargetCoord, Is.EqualTo(AttackCoord));
            Assert.That(trigger.AddedToHand, Is.True);
            Assert.That(trigger.ArmedTrapCount, Is.Zero);
        }

        [Test]
        public void InsufficientActivationFundsLeaveTrapSecretAndArmedForALaterAttack()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 3));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(3));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.False);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            attacker.RemainingAttacksThisTurn = 1;
            state.ClearValuePopupEvents();
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents.Single().Kind, Is.EqualTo(BattleTrapEventKind.Triggered));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void InstallationOrderSelectsFirstAffordableMatchingTrapAndConsumesOnlyThatOne()
        {
            var state = State(PlayerId.Player);
            var first = Trap("first", new ResourceSet(0, 0, 0, 4));
            var second = Trap("second", new ResourceSet(0, 0, 0, 1));
            Arm(state, PlayerId.Player, first);
            Arm(state, PlayerId.Player, second);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 5));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo("first"));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo("second"));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(state.Player.Discard.CardIds, Is.EqualTo(new[] { "first" }));
        }

        [Test]
        public void UnaffordableEarlierTrapRemainsSecretWhileLaterAffordableTrapTriggers()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player, Trap("unaffordable", new ResourceSet(0, 0, 0, 9)));
            Arm(state, PlayerId.Player, Trap("affordable", new ResourceSet(0, 0, 0, 4)));
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo("affordable"));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo("unaffordable"));
        }

        [Test]
        public void FullHandBurnsTheReturnedCardWithoutDiscardDeathDrawFailureOrOverflow()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI, "DemonKing");
            for (var index = 0; index < state.AI.MaxHandSize; index++)
                state.AI.Hand.Add("spare", "spare-" + index, false);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.AIBoard.GetOccupant(AttackCoord), Is.Null);
            Assert.That(state.AI.Hand.Count, Is.EqualTo(state.AI.MaxHandSize));
            Assert.That(state.AI.Discard.CardIds, Is.Empty);
            Assert.That(state.AI.FailedDrawCount, Is.Zero);
            Assert.That(attacker.IsDemonKingRevivalPending, Is.False);
            Assert.That(attacker.CurrentHp, Is.EqualTo(50));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.TrapEvents.Single().AddedToHand, Is.False);
        }

        [Test]
        public void ReturnedCardResummonsFromCurrentOwnersDefinitionRatherThanBattleBuffsOrDamage()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI, "Golem");
            attacker.ApplyCombatStatBuff("old-buff", attackBonus: 15, maxHpBonus: 10, physicalDefenseBonus: 12, magicDefenseBonus: 13, sourceRuntimeId: "old-source");
            attacker.CurrentHp = 7;
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            var card = state.AI.Hand.Cards.Single();
            var definition = new UnitCardDefinition("Golem", "Golem", new ResourceSet(),
                AttackType.Melee, 10, 20, true, false, 0);
            var level = new InMemoryCardUpgradeLevelProvider();
            level.SetUpgradeLevel(PlayerId.AI, "Golem", 3);
            var summoned = new PlayCardService(Provider(definition), new SummonService(), level)
                .PlayUnitCard(state, PlayerId.AI, card.CardId, AttackCoord, card.RuntimeId);
            Assert.That(summoned.BaseAttack, Is.EqualTo(11));
            Assert.That(summoned.MaxHp, Is.EqualTo(22));
            Assert.That(summoned.CurrentHp, Is.EqualTo(22));
            Assert.That(summoned.PhysicalDefense, Is.Zero);
            Assert.That(summoned.MagicDefense, Is.Zero);
            Assert.That(summoned.CombatStatBuffs, Is.Empty);
        }

        [Test]
        public void ReturnCanRemoveChieftainAuraAndResolveOnlyRemainingOrcDefeats()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var chief = PlaceAttacker(state, PlayerId.AI, "OrcChieftain", races: new[] { UnitRace.Orc });
            var orc = new UnitState("orc", "OrcWarrior", PlayerId.AI, new TileCoord(1, 0),
                AttackType.Melee, 10, 20, true, false, 0, races: new[] { UnitRace.Orc });
            state.AIBoard.Place(orc.Position, orc);
            orc.CurrentHp = 5;
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, chief.Position, MasterCoord);
            Assert.That(state.AIBoard.GetOccupant(AttackCoord), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(orc.Position), Is.Null);
            Assert.That(state.AI.Hand.CardIds, Does.Contain("OrcChieftain"));
            Assert.That(state.AI.Hand.CardIds, Does.Not.Contain("OrcWarrior"));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
        }

        [Test]
        public void ReturnTriggersAgainstInvincibleMasterBeforeShielderTransfer()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            state.Player.Master.AddInvincibleEffect(InvincibleDurationType.Always);
            var shield = new UnitState("shield", "Shieldbearer", PlayerId.Player, new TileCoord(2, 0),
                AttackType.Melee, 20, 100, true, false, 0, hasShielder: true);
            state.PlayerBoard.Place(shield.Position, shield);
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(shield.CurrentHp, Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MasterAndBuildingAttacksAreNotReturned(bool masterAttack)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            OccupantState attacker = state.AI.Master;
            if (!masterAttack)
            {
                attacker = new BuildingState("building", "Tower", PlayerId.AI, AttackCoord, true, 20, 100);
                state.AIBoard.Place(AttackCoord, attacker);
            }
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.False);
            Assert.That(state.Player.Master.CurrentHp, Is.LessThan(333));
        }

        [Test]
        public void InvalidAttackAndUnaffordableManaCannonDoNotSpendActivateRevealOrUseRng()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI, "ManaCannon");
            attacker.ApplyHuanShu();
            PrepareAttack(state, PlayerId.AI);
            var rngCalls = 0;
            var service = new AttackService(new TargetingService(), count => { rngCalls++; return 0; });
            Assert.Throws<InvalidOperationException>(() => service.Attack(state, PlayerId.AI, AttackCoord, MasterCoord));
            Assert.That(rngCalls, Is.Zero);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(1));
            Assert.That(state.LastAttackResolution, Is.Null);
            state.AI.Resources.Add(new ResourceSet(1, 0, 0, 0));
            Assert.Throws<InvalidOperationException>(() =>
                service.Attack(state, PlayerId.AI, AttackCoord, new TileCoord(4, 1)));
            Assert.That(state.AI.Resources.Mana, Is.EqualTo(1));
            Assert.That(rngCalls, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AcceptedManaCannonAttackPaysItsCostOnceBeforeBeingReturned(bool substituteGold)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            state.AI.Resources.Add(substituteGold ? new ResourceSet(0, 0, 0, 1) : new ResourceSet(1, 0, 0, 0));
            var attacker = PlaceAttacker(state, PlayerId.AI, "ManaCannon", hits: 3);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, AttackCoord, MasterCoord);
            Assert.That(state.AI.Resources.Mana, Is.Zero);
            Assert.That(state.AI.Resources.Gold, Is.Zero);
            Assert.That(state.Player.Resources.Gold, Is.Zero);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void TransferredUnitReturnsToItsCurrentOwnerRatherThanItsOriginalOwner()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.Player);
            state.PlayerBoard.Remove(AttackCoord);
            attacker.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(AttackCoord, attacker);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, AttackCoord, MasterCoord);
            Assert.That(state.Player.Hand.Count, Is.Zero);
            Assert.That(state.AI.Hand.Cards.Single().CardId, Is.EqualTo(attacker.CardId));
            Assert.That(state.TrapEvents.Single().TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
        }

        [TestCase("drained")]
        [TestCase("stun")]
        [TestCase("freeze")]
        public void ActionLockedAttackerCannotActivateOrRevealATrap(string status)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            if (status == "drained") attacker.IsDrained = true;
            else if (status == "stun") attacker.ApplyStun(1);
            else attacker.ApplyFreeze(1);
            PrepareAttack(state, PlayerId.AI);
            Assert.Throws<InvalidOperationException>(() =>
                new AttackService().Attack(state, PlayerId.AI, AttackCoord, MasterCoord));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.LastAttackResolution, Is.Null);
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void NonUpgradeableTrapIgnoresAnAccountUpgradeLevel()
        {
            var definition = TrapReturnCardRules.CreateDefinition();
            var state = State(PlayerId.Player);
            state.Player.Hand.Add(definition.CardId, "trap", false);
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(PlayerId.Player, definition.CardId, 13);
            var trap = new TrapService(Provider(definition), levels)
                .Install(state, PlayerId.Player, definition.CardId, "trap");
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
            Assert.That(trap.UpgradeLevel, Is.Zero);
            Assert.That(trap.ActivationCost.Gold, Is.EqualTo(4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GenericTrapActivationUsesExistingGoldSubstitution(bool goldOnly)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player, Trap("mixed-cost", new ResourceSet(2, 1, 1, 1)));
            state.Player.Resources.Add(goldOnly ? new ResourceSet(0, 0, 0, 5) : new ResourceSet(1, 1, 0, 3));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(new[] { state.Player.Resources.Mana, state.Player.Resources.Qi,
                state.Player.Resources.Power, state.Player.Resources.Gold }, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo("mixed-cost"));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void HuanShuChecksTheResolvedMasterInsteadOfTheDeclaredTarget(int selectedTarget)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var bystander = new UnitState("bystander", "Golem", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Ranged, 1, 100, true, false, 0);
            state.PlayerBoard.Place(bystander.Position, bystander);
            var attacker = PlaceAttacker(state, PlayerId.AI);
            attacker.ApplyHuanShu();
            PrepareAttack(state, PlayerId.AI);
            var randomCalls = 0;
            var service = new AttackService(new TargetingService(), count =>
            {
                Assert.That(count, Is.EqualTo(3));
                randomCalls++;
                return selectedTarget;
            });
            // Board enumeration yields opponent unit, opponent master, then attacker's own master.
            var declared = selectedTarget == 1 ? bystander.Position : MasterCoord;
            service.Attack(state, PlayerId.AI, attacker.Position, declared);
            Assert.That(randomCalls, Is.EqualTo(1));
            Assert.That(state.LastAttackResolution.WasHuanShuRedirected, Is.True);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.EqualTo(selectedTarget == 1));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            if (selectedTarget == 1)
            {
                Assert.That(state.ArmedTraps, Is.Empty);
                Assert.That(state.TrapEvents.Single().TargetRuntimeId, Is.EqualTo(attacker.RuntimeId));
                Assert.That(state.AIBoard.GetOccupant(AttackCoord), Is.Null);
                Assert.That(state.Player.Resources.Gold, Is.Zero);
                Assert.That(state.ValuePopupEvents, Is.Empty);
            }
            else
            {
                Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
                Assert.That(state.TrapEvents, Is.Empty);
                Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
                Assert.That(state.AIBoard.GetOccupant(AttackCoord), Is.SameAs(attacker));
                Assert.That(selectedTarget == 0 ? bystander.CurrentHp : state.AI.Master.CurrentHp,
                    Is.EqualTo(selectedTarget == 0 ? 80 : 313));
            }
        }

        [Test]
        public void AConsumedTrapCanBeInstalledAgainFromAnotherExactCopy()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Hand.Add(TrapReturnCardRules.CardId, "next-copy", false);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = PlaceAttacker(state, PlayerId.AI);
            PrepareAttack(state, PlayerId.AI);
            new AttackService().Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            state.SetActivePlayer(PlayerId.Player);
            Processor(TrapReturnCardRules.CreateDefinition()).Execute(state, PlayerId.Player,
                new InstallTrapCommand(TrapReturnCardRules.CardId, "next-copy"));
            Assert.That(state.ArmedTraps.Single().RuntimeId, Is.EqualTo("next-copy"));
            Assert.That(state.Player.Hand.Count, Is.Zero);
            AssertInstalledIsAnonymous(state.TrapEvents.Single(), PlayerId.Player, 1);
        }

        private static void AssertInstalledIsAnonymous(BattleTrapEvent e, PlayerId ownerId, int count)
        {
            Assert.That(e.Kind, Is.EqualTo(BattleTrapEventKind.Installed));
            Assert.That(e.OwnerId, Is.EqualTo(ownerId));
            Assert.That(e.ArmedTrapCount, Is.EqualTo(count));
            Assert.That(new[] { e.CardId, e.RuntimeId, e.TargetRuntimeId, e.TargetCardId },
                Is.All.EqualTo(string.Empty));
            Assert.That(e.UpgradeLevel, Is.Zero);
            Assert.That(e.TargetCoord, Is.Null);
            Assert.That(e.AddedToHand, Is.False);
        }

        private static BattleState State(PlayerId activeOwner)
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
            state.RestoreRuntimeState(5, activeOwner, PhaseType.Main);
            return state;
        }

        private static void Arm(BattleState state, PlayerId owner, TrapSpellCardDefinition definition = null)
        {
            definition = definition ?? TrapReturnCardRules.CreateDefinition();
            state.SetActivePlayer(owner);
            var runtimeId = definition.CardId == TrapReturnCardRules.CardId ? "trap-" + owner : definition.CardId;
            state.GetPlayer(owner).Hand.Add(definition.CardId, runtimeId, false);
            new TrapService(Provider(definition)).Install(state, owner, definition.CardId, runtimeId);
        }

        private static UnitState PlaceAttacker(BattleState state, PlayerId owner, string cardId = "Golem",
            AttackType attackType = AttackType.Ranged, int hits = 1, IReadOnlyList<UnitRace> races = null)
        {
            var unit = new UnitState("attacker", cardId, owner, AttackCoord, attackType,
                20, 50, true, false, 0, hitsPerAttack: hits, races: races);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(AttackCoord, unit);
            return unit;
        }

        private static void PrepareAttack(BattleState state, PlayerId owner)
        {
            state.SetActivePlayer(owner);
            state.ClearTrapEvents();
            state.ClearValuePopupEvents();
        }

        private static PlayerId Opponent(PlayerId owner) => owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        private static TrapSpellCardDefinition Trap(string id, ResourceSet cost, bool replicate = false) =>
            new TrapSpellCardDefinition(id, id, cost, TrapTriggerType.EnemyUnitAttacksMaster,
                TrapEffectType.ReturnAttackerToHand, replicate);
        private static ICardDefinitionProvider Provider(params CardDefinition[] definitions) =>
            new InMemoryCardDefinitionProvider(definitions);
        private static BattleCommandProcessor Processor(params CardDefinition[] definitions)
        {
            var provider = Provider(definitions);
            return new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                new MoveService(), new AttackService(), new EndTurnService());
        }
    }
}
