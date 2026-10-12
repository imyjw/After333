using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public sealed class StunFreezeStatusEffectTests
    {
        [Test]
        public void ReapplicationUsesLongerRemainingDurationAndKeepsTheTwoClocksIndependent()
        {
            var unit = Unit("duration", PlayerId.Player, new TileCoord(0, 0));
            unit.ApplyStun(3);
            unit.ApplyFreeze(1);
            unit.ResolveActionDebuffsTurnEnd();
            AssertDurations(unit, 2, 0);
            unit.ApplyStun(1);
            unit.ApplyStun(0);
            unit.ApplyFreeze(4);
            AssertDurations(unit, 2, 4);
            unit.ApplyStun(5);
            unit.ApplyFreeze(2);
            AssertDurations(unit, 5, 4);
            unit.ResolveActionDebuffsTurnEnd();
            AssertDurations(unit, 4, 3);
            unit.ClearActionDebuffs();
            unit.ResolveActionDebuffsTurnEnd();
            AssertDurations(unit, 0, 0);
            unit.ApplyStun(int.MaxValue);
            unit.ApplyStun(int.MaxValue);
            unit.ApplyFreeze(int.MaxValue);
            unit.ResolveActionDebuffsTurnEnd();
            AssertDurations(unit, int.MaxValue - 1, int.MaxValue - 1);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NegativeApplicationIsRejectedWithoutChangingExistingDurations(bool freeze)
        {
            var unit = Unit("invalid", PlayerId.Player, new TileCoord(0, 0));
            unit.RestoreActionDebuffs(2, 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => Apply(unit, freeze, -1));
            AssertDurations(unit, 2, 3);
        }

        [Test]
        public void RestoreUsesExactRemainingValuesAndNormalizesNegativeValues()
        {
            var unit = Unit("restored", PlayerId.Player, new TileCoord(0, 0));
            unit.RestoreActionDebuffs(4, 2);
            AssertDurations(unit, 4, 2);
            unit.RestoreActionDebuffs(-3, 1);
            AssertDurations(unit, 0, 1);
            unit.RestoreActionDebuffs(0, -1);
            AssertDurations(unit, 0, 0);
        }

        [TestCase(false, OccupantKind.Unit)]
        [TestCase(true, OccupantKind.Unit)]
        [TestCase(false, OccupantKind.Building)]
        [TestCase(true, OccupantKind.Building)]
        [TestCase(false, OccupantKind.Master)]
        [TestCase(true, OccupantKind.Master)]
        public void BlockedNormalAttackConsumesNeitherAttackAllowanceResourcesNorRng(bool freeze, OccupantKind kind)
        {
            var state = Battle();
            var attacker = Actor(state, kind);
            var victim = Unit("victim", PlayerId.AI, new TileCoord(0, 0), attack: 2);
            state.AIBoard.Place(victim.Position, victim);
            Apply(attacker, freeze, 2);
            var resources = state.Player.Resources.Clone();
            var allowance = attacker.RemainingAttacksThisTurn;
            var sequence = state.Counters.ActionSequence;
            var rngCalls = 0;
            var service = new AttackService(new TargetingService(), max => { rngCalls++; return 0; });
            Assert.Throws<InvalidOperationException>(() =>
                service.Attack(state, PlayerId.Player, attacker.Position, victim.Position));
            Assert.That(victim.CurrentHp, Is.EqualTo(100));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.EqualTo(allowance));
            Assert.That(state.Counters.ActionSequence, Is.EqualTo(sequence));
            Assert.That(state.ValuePopupEvents, Is.Empty);
            Assert.That(state.ResourceChangeEvents, Is.Empty);
            AssertResources(state.Player.Resources, resources);
            Assert.That(rngCalls, Is.Zero);
            attacker.ClearActionDebuffs();
            service.Attack(state, PlayerId.Player, attacker.Position, victim.Position);
            Assert.That(victim.CurrentHp, Is.LessThan(100), "Clearing the debuff restores an otherwise legal attack.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HuanShuDoesNotResolveRandomTargetWhileAttackIsBlocked(bool freeze)
        {
            var state = Battle();
            var attacker = Unit("random", PlayerId.Player, new TileCoord(0, 0));
            var victim = Unit("victim", PlayerId.AI, new TileCoord(0, 0));
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.AIBoard.Place(victim.Position, victim);
            attacker.ApplyHuanShu();
            Apply(attacker, freeze, 1);
            var rngCalls = 0;
            var service = new AttackService(new TargetingService(), max => { rngCalls++; return 0; });
            Assert.Throws<InvalidOperationException>(() =>
                service.Attack(state, PlayerId.Player, attacker.Position, victim.Position));
            Assert.That(rngCalls, Is.Zero);
            Assert.That(attacker.IsUnderHuanShu, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MeleeTargetStillTakesNormalDamageButCannotCounterattack(bool freeze)
        {
            var state = Battle();
            var attacker = Unit("attacker", PlayerId.Player, new TileCoord(0, 0), attack: 10);
            var defender = Unit("defender", PlayerId.AI, new TileCoord(0, 0), attack: 20, physicalDefense: 3);
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.AIBoard.Place(defender.Position, defender);
            Apply(defender, freeze, 2);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, defender.Position);
            Assert.That(defender.CurrentHp, Is.EqualTo(93));
            Assert.That(attacker.CurrentHp, Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents.All(e => e.Cause != BattleValueChangeCause.Counterattack), Is.True);
            Assert.That(defender.EffectsSuppressed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnlyFreezeStopsFrontRowBlockingAndBothRemainLegalTargets(bool freeze)
        {
            var state = Battle();
            var attacker = Unit("attacker", PlayerId.Player, new TileCoord(0, 0));
            var front = Unit("front", PlayerId.AI, new TileCoord(0, 0));
            var rear = Unit("rear", PlayerId.AI, new TileCoord(0, 1));
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.AIBoard.Place(front.Position, front);
            state.AIBoard.Place(rear.Position, rear);
            Apply(front, freeze, 2);
            var targeting = new TargetingService();
            Assert.That(targeting.CanTarget(state, PlayerId.Player, attacker.Position, front.Position), Is.True);
            Assert.That(targeting.CanTarget(state, PlayerId.Player, attacker.Position, rear.Position), Is.EqualTo(freeze));
            Assert.That(front.CanBeAffected, Is.True);
        }

        [Test]
        public void StunnedUnitCanMoveAndSwapWhileItsDebuffFollowsTheSameOccupant()
        {
            var state = Battle();
            var stunned = Unit("stunned", PlayerId.Player, new TileCoord(0, 0));
            var ally = Unit("ally", PlayerId.Player, new TileCoord(1, 0));
            state.PlayerBoard.Place(stunned.Position, stunned);
            state.PlayerBoard.Place(ally.Position, ally);
            stunned.ApplyStun(3);
            var service = new MoveService();
            service.Move(state, PlayerId.Player, stunned.Position, ally.Position);
            Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(stunned));
            service.Move(state, PlayerId.Player, stunned.Position, new TileCoord(4, 0));
            Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(4, 0)), Is.SameAs(stunned));
            AssertDurations(stunned, 3, 0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FrozenUnitCannotMoveOrBeSwappedByAnAlly(bool frozenIsSwapTarget)
        {
            var state = Battle();
            var frozen = Unit("frozen", PlayerId.Player, new TileCoord(0, 0));
            var ally = Unit("ally", PlayerId.Player, new TileCoord(1, 0));
            state.PlayerBoard.Place(frozen.Position, frozen);
            state.PlayerBoard.Place(ally.Position, ally);
            frozen.ApplyFreeze(3);
            var from = frozenIsSwapTarget ? ally.Position : frozen.Position;
            var to = frozenIsSwapTarget ? frozen.Position : new TileCoord(4, 0);
            Assert.Throws<InvalidOperationException>(() => new MoveService().Move(state, PlayerId.Player, from, to));
            Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(frozen));
            Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(ally));
            AssertDurations(frozen, 0, 3);
        }

        [TestCase(false, PlayerId.Player)]
        [TestCase(true, PlayerId.Player)]
        [TestCase(false, PlayerId.AI)]
        [TestCase(true, PlayerId.AI)]
        public void ActiveEffectIsUnavailableWithoutSpendingOrConsumingItsOncePerTurnUse(bool freeze, PlayerId owner)
        {
            var state = Battle(owner);
            var plant = new BuildingState("plant", PowerPlantRules.CardId, owner, new TileCoord(0, 0), false, 0, 30);
            state.GetBoard(owner).Place(plant.Position, plant);
            Apply(plant, freeze, 2);
            var resources = state.GetPlayer(owner).Resources.Clone();
            Assert.That(plant.EffectsSuppressed, Is.False);
            Assert.That(plant.CannotActivate, Is.True);
            Assert.That(ActiveEffectService.CanOfferActivation(state, owner, plant.Position), Is.False);
            Assert.That(ActiveEffectService.CanActivate(state, owner, plant.Position), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                ActiveEffectService.Activate(state, owner, plant.Position, plant.RuntimeId));
            AssertResources(state.GetPlayer(owner).Resources, resources);
            Assert.That(plant.LastActiveEffectTurnNumber, Is.EqualTo(-1));
            Assert.That(state.ResourceChangeEvents, Is.Empty);
            plant.ClearActionDebuffs();
            Assert.That(ActiveEffectService.CanActivate(state, owner, plant.Position), Is.True);
            ActiveEffectService.Activate(state, owner, plant.Position, plant.RuntimeId);
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(resources.Gold - 1));
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(resources.Power + 2));
        }

        [Test]
        public void EveryAcceptedGlobalTurnEndTicksBothBoardsAndMastersIncludingTheApplicationTurn()
        {
            var state = Battle();
            var player = Unit("player-status", PlayerId.Player, new TileCoord(0, 0));
            var ai = Unit("ai-status", PlayerId.AI, new TileCoord(0, 0));
            state.PlayerBoard.Place(player.Position, player);
            state.AIBoard.Place(ai.Position, ai);
            player.RestoreActionDebuffs(1, 3);
            ai.RestoreActionDebuffs(2, 1);
            state.Player.Master.ApplyStun(2);
            state.AI.Master.ApplyFreeze(2);
            var service = new EndTurnService();
            service.EndTurn(state);
            AssertDurations(player, 0, 2);
            AssertDurations(ai, 1, 0);
            AssertDurations(state.Player.Master, 1, 0);
            AssertDurations(state.AI.Master, 0, 1);
            Assert.Throws<InvalidOperationException>(() => service.EndTurn(state));
            AssertDurations(player, 0, 2);
            AssertDurations(ai, 1, 0);
            new TurnStartService().ResolveTurnStart(state);
            AssertDurations(player, 0, 2);
            AssertDurations(ai, 1, 0);
            service.EndTurn(state);
            AssertDurations(player, 0, 1);
            AssertDurations(ai, 0, 0);
            AssertDurations(state.Player.Master, 0, 0);
            AssertDurations(state.AI.Master, 0, 0);
        }

        [Test]
        public void SealboundRejectsNewApplicationsButExistingDurationsContinueAtGlobalTurnEnd()
        {
            var state = Battle();
            var sealedUnit = Unit("sealed", PlayerId.Player, new TileCoord(0, 0));
            sealedUnit.RestoreActionDebuffs(2, 3);
            sealedUnit.EnterSealbound(3);
            state.PlayerBoard.Place(sealedUnit.Position, sealedUnit);
            sealedUnit.ApplyStun(10);
            sealedUnit.ApplyFreeze(10);
            AssertDurations(sealedUnit, 2, 3);
            new EndTurnService().EndTurn(state);
            AssertDurations(sealedUnit, 1, 2);
            Assert.That(sealedUnit.IsSealbound, Is.True);
            sealedUnit.RestoreActionDebuffs(4, 5);
            AssertDurations(sealedUnit, 4, 5);
            sealedUnit.ApplyErasure();
            AssertDurations(sealedUnit, 4, 5);
            Assert.That(sealedUnit.IsErasure, Is.False);
        }

        [Test]
        public void ErasureClearsExistingDebuffsButLaterApplicationsWorkNormally()
        {
            var unit = Unit("erased", PlayerId.Player, new TileCoord(0, 0));
            unit.RestoreActionDebuffs(4, 5);
            unit.ApplyErasure();
            AssertDurations(unit, 0, 0);
            unit.ApplyStun(2);
            unit.ApplyFreeze(3);
            AssertDurations(unit, 2, 3);
            unit.ResolveActionDebuffsTurnEnd();
            AssertDurations(unit, 1, 2);
            unit.ApplyErasure();
            AssertDurations(unit, 0, 0);
        }

        [Test]
        public void DemonKingDeathAndRevivalDiscardActionDebuffsWithoutSuppressingItsReturnEffect()
        {
            var demon = Unit(DemonKingRules.CardId, PlayerId.Player, new TileCoord(0, 0));
            demon.RestoreActionDebuffs(4, 5);
            Assert.That(demon.TryEnterDemonKingRevivalSeal(PlayerId.AI, 2), Is.True);
            AssertDurations(demon, 0, 0);
            demon.RestoreActionDebuffs(8, 9);
            AssertDurations(demon, 0, 0);
            Assert.That(demon.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 4), Is.False);
            Assert.That(demon.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 6), Is.False);
            Assert.That(demon.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 8), Is.True);
            AssertDurations(demon, 0, 0);
            Assert.That(demon.CurrentHp, Is.EqualTo(demon.MaxHp));
            Assert.That(demon.IsDemonKingRevivalPending, Is.False);
            demon.ApplyStun(2);
            demon.RestoreDemonKingRevivalState(true, 2, PlayerId.AI, 8, 1);
            AssertDurations(demon, 0, 0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefensesNormalDamageAndInvincibleAreUnaffected(bool freeze)
        {
            var target = Unit("defenses", PlayerId.Player, new TileCoord(0, 0), physicalDefense: 3, magicDefense: 2);
            Apply(target, freeze, 2);
            Assert.That(target.EffectsSuppressed || target.BuffsAndDebuffsSuppressed || target.TakesDrainedDamage, Is.False);
            Assert.That(target.EffectivePhysicalDefense, Is.EqualTo(3));
            Assert.That(target.EffectiveMagicDefense, Is.EqualTo(2));
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Physical), Is.EqualTo(7));
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Magic), Is.EqualTo(8));
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 10, DamageType.Fixed), Is.EqualTo(10));
            Assert.That(target.CurrentHp, Is.EqualTo(75));
            target.AddInvincibleEffect(InvincibleDurationType.Always);
            Assert.That(target.IsInvincible, Is.True);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 100, DamageType.Fixed), Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(75));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TraitsMultiHitSpellPowerHidingAndEndureAreUnaffected(bool freeze)
        {
            var unit = new UnitState("traits", "traits", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Melee, 10, 100, true, false, 0, maxAttacksPerTurn: 3, hitsPerAttack: 2,
                hasBerserker: true, hasEndure: true, hasLifeSteal: true, hasRush: true,
                hasHiding: true, hasFlying: true, spellPower: 20, hasPiercing: true);
            unit.CurrentHp = 90;
            Apply(unit, freeze, 2);
            Assert.That(unit.EffectsSuppressed, Is.False);
            Assert.That(unit.Attack, Is.EqualTo(20));
            Assert.That(unit.EffectiveMaxAttacksPerTurn, Is.EqualTo(3));
            Assert.That(unit.EffectiveHitsPerAttack, Is.EqualTo(2));
            Assert.That(unit.EffectiveSpellPower, Is.EqualTo(20));
            Assert.That(unit.IsHiding && unit.HasActiveFlying && unit.HasActivePiercing && unit.CanTriggerEndure, Is.True);
            Assert.That(unit.HasLifeSteal && unit.HasRush, Is.True);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(unit, 100, DamageType.Fixed), Is.EqualTo(89));
            Assert.That(unit.CurrentHp, Is.EqualTo(1));
            Assert.That(unit.EndureUsed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FrozenOrStunnedShielderStillRedirectsDamageAndKeepsItsArmor(bool freeze)
        {
            var state = Battle();
            var attacker = Unit("ranged", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged);
            var guard = new UnitState("guard", "guard", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Melee, 10, 100, true, false, 0, hasShielder: true, physicalDefense: 3);
            var rear = Unit("rear", PlayerId.AI, new TileCoord(0, 1));
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.AIBoard.Place(guard.Position, guard);
            state.AIBoard.Place(rear.Position, rear);
            Apply(guard, freeze, 2);
            Assert.That(guard.HasActiveShielder, Is.True);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, rear.Position);
            Assert.That(guard.CurrentHp, Is.EqualTo(93));
            Assert.That(rear.CurrentHp, Is.EqualTo(100));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrcAuraContinuesFromAffectedSourceAndToAffectedRecipient(bool freeze)
        {
            var state = Battle();
            var chief = new UnitState("chief", OrcChieftainRules.CardId, PlayerId.Player, new TileCoord(0, 0),
                AttackType.Melee, 50, 70, true, false, 0, races: new[] { UnitRace.Orc });
            var orc = new UnitState("orc", "orc", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Melee, 10, 20, true, false, 0, races: new[] { UnitRace.Orc });
            state.PlayerBoard.Place(chief.Position, chief);
            state.PlayerBoard.Place(orc.Position, orc);
            Apply(chief, freeze, 2);
            Apply(orc, freeze, 2);
            state.PlayerBoard.RefreshOrcChieftainAuras();
            Assert.That(chief.Attack, Is.EqualTo(60));
            Assert.That(chief.MaxHp, Is.EqualTo(80));
            Assert.That(orc.Attack, Is.EqualTo(20));
            Assert.That(orc.MaxHp, Is.EqualTo(30));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnStartResourceProductionAndPowerUpkeepAreUnaffected(bool freeze)
        {
            var state = Battle();
            var unit = new UnitState("producer", "producer", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Melee, 10, 100, true, true, 1,
                turnStartResourceGain: new ResourceSet(3, 4, 2, 1));
            state.PlayerBoard.Place(unit.Position, unit);
            Apply(unit, freeze, 2);
            var before = state.Player.Resources.Clone();
            state.RestoreRuntimeState(3, PlayerId.Player, PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(before.Mana + 3));
            Assert.That(state.Player.Resources.Qi, Is.EqualTo(before.Qi + 4));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(before.Power + 1));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(before.Gold + 4));
            Assert.That(unit.IsDrained, Is.False);
            Assert.That(freeze ? unit.FreezeTurnEndsRemaining : unit.StunTurnEndsRemaining, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PassiveTurnEndCardEffectStillRunsWhileActionsAreBlocked(bool freeze)
        {
            var state = Battle();
            var dragon = Unit("RedDragon", PlayerId.Player, new TileCoord(0, 0));
            state.PlayerBoard.Place(dragon.Position, dragon);
            Apply(dragon, freeze, 2);
            new EndTurnService().EndTurn(state);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(freeze ? dragon.FreezeTurnEndsRemaining : dragon.StunTurnEndsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void DetachedAndAiCopiesKeepIndependentDurationsAndSearchKeysDistinguishThem()
        {
            var state = Battle();
            var unit = Unit("copy", PlayerId.Player, new TileCoord(0, 0));
            state.PlayerBoard.Place(unit.Position, unit);
            unit.RestoreActionDebuffs(4, 3);
            var detached = unit.CloneDetached();
            detached.ResolveActionDebuffsTurnEnd();
            AssertDurations(detached, 3, 2);
            AssertDurations(unit, 4, 3);
            var copy = AiBattleStateCopy.Create(state);
            var copiedUnit = copy.PlayerBoard.GetOccupant(unit.Position);
            AssertDurations(copiedUnit, 4, 3);
            Assert.That(AiBattleStateCopy.PositionKey(copy), Is.EqualTo(AiBattleStateCopy.PositionKey(state)));
            copiedUnit.ApplyStun(8);
            AssertDurations(unit, 4, 3);
            Assert.That(AiBattleStateCopy.PositionKey(copy), Is.Not.EqualTo(AiBattleStateCopy.PositionKey(state)));
            var focusMethod = typeof(AiBattleStateCopy).GetMethod("FocusTargetKey", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(focusMethod.Invoke(null, new object[] { copiedUnit }),
                Is.Not.EqualTo(focusMethod.Invoke(null, new object[] { unit })));
            copiedUnit.RestoreActionDebuffs(4, 4);
            Assert.That(AiBattleStateCopy.PositionKey(copy), Is.Not.EqualTo(AiBattleStateCopy.PositionKey(state)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnershipTransferPreservesGlobalDurations(bool freeze)
        {
            var state = Battle();
            var unit = Unit("transferred", PlayerId.Player, new TileCoord(0, 0));
            state.PlayerBoard.Place(unit.Position, unit);
            Apply(unit, freeze, 3);
            state.PlayerBoard.Remove(unit.Position);
            unit.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(unit.Position, unit);
            new EndTurnService().EndTurn(state);
            Assert.That(unit.OwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(freeze ? unit.FreezeTurnEndsRemaining : unit.StunTurnEndsRemaining, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AiDoesNotGenerateBlockedAttackOrActiveAndCanAttackAfterExpiration(bool freeze)
        {
            var state = Battle(PlayerId.AI);
            var unit = Unit("ai-attacker", PlayerId.AI, new TileCoord(0, 0), AttackType.Ranged);
            var plant = new BuildingState("ai-plant", PowerPlantRules.CardId, PlayerId.AI,
                new TileCoord(1, 0), false, 0, 30);
            state.AIBoard.Place(unit.Position, unit);
            state.AIBoard.Place(plant.Position, plant);
            Apply(unit, freeze, 1);
            Apply(plant, freeze, 1);
            var commands = AiCommands(state);
            Assert.That(commands.OfType<AttackCommand>().Any(c => c.AttackerCoord == unit.Position), Is.False);
            Assert.That(commands.OfType<ActivateEffectCommand>().Any(), Is.False);
            if (freeze)
                Assert.That(commands.OfType<MoveOccupantCommand>().Any(c => c.From == unit.Position), Is.False);
            unit.ResolveActionDebuffsTurnEnd();
            plant.ResolveActionDebuffsTurnEnd();
            commands = AiCommands(state);
            Assert.That(commands.OfType<AttackCommand>().Any(c => c.AttackerCoord == unit.Position), Is.True);
            Assert.That(commands.OfType<ActivateEffectCommand>().Any(), Is.True);
        }

        private static IReadOnlyList<IBattleCommand> AiCommands(BattleState state)
        {
            var type = typeof(AiBattleStateCopy).Assembly.GetType("Project333.Runtime.Application.Services.AiActionGenerator");
            var generator = Activator.CreateInstance(type, new object[] {
                new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()), new TargetingService() });
            var actions = (IEnumerable)type.GetMethod("Generate").Invoke(generator, new object[] { state });
            var result = new List<IBattleCommand>();
            foreach (var action in actions)
                result.AddRange((IBattleCommand[])action.GetType().GetField("Commands").GetValue(action));
            return result;
        }

        private static OccupantState Actor(BattleState state, OccupantKind kind)
        {
            OccupantState actor;
            if (kind == OccupantKind.Master) actor = state.Player.Master;
            else if (kind == OccupantKind.Building)
                actor = new BuildingState("attacker-building", "attacker-building", PlayerId.Player,
                    new TileCoord(0, 0), true, 7, 100);
            else actor = new UnitState("attacker-cannon", ManaCannonRules.CardId, PlayerId.Player,
                new TileCoord(0, 0), AttackType.Ranged, 50, 100, true, false, 0, damageType: DamageType.Magic);
            if (kind != OccupantKind.Master) state.PlayerBoard.Place(actor.Position, actor);
            actor.HasSummoningSickness = false;
            actor.RemainingAttacksThisTurn = 1;
            return actor;
        }

        private static UnitState Unit(string id, PlayerId owner, TileCoord position,
            AttackType attackType = AttackType.Melee, int attack = 10, int physicalDefense = 0, int magicDefense = 0)
        {
            return new UnitState(id, id, owner, position, attackType, attack, 100, true, false, 0,
                physicalDefense: physicalDefense, magicDefense: magicDefense)
            { HasSummoningSickness = false, RemainingAttacksThisTurn = 1 };
        }

        private static BattleState Battle(PlayerId owner = PlayerId.Player)
        {
            var deck = Enumerable.Range(0, 13).Select(i => "unused-" + i).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.RestoreRuntimeState(3, owner, PhaseType.Main);
            return state;
        }

        private static void Apply(OccupantState occupant, bool freeze, int duration)
        {
            if (freeze) occupant.ApplyFreeze(duration);
            else occupant.ApplyStun(duration);
        }

        private static void AssertDurations(OccupantState occupant, int stun, int freeze)
        {
            Assert.That(occupant.StunTurnEndsRemaining, Is.EqualTo(stun));
            Assert.That(occupant.FreezeTurnEndsRemaining, Is.EqualTo(freeze));
            Assert.That(occupant.IsStunned, Is.EqualTo(stun > 0));
            Assert.That(occupant.IsFrozen, Is.EqualTo(freeze > 0));
        }

        private static void AssertResources(ResourceSet actual, ResourceSet expected)
        {
            Assert.That(actual.Mana, Is.EqualTo(expected.Mana));
            Assert.That(actual.Qi, Is.EqualTo(expected.Qi));
            Assert.That(actual.Power, Is.EqualTo(expected.Power));
            Assert.That(actual.Gold, Is.EqualTo(expected.Gold));
        }
    }
}
