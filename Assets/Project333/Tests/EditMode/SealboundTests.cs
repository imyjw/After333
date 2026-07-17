using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class SealboundTests
    {
        [Test]
        public void PlayUnitCard_WithSealboundDefinition_EntersSealboundAndOccupiesTile()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("sealed-unit");
            var definition = new UnitCardDefinition(
                cardId: "sealed-unit",
                displayName: "Sealed Unit",
                cost: new ResourceSet(),
                attackType: AttackType.Melee,
                attack: 7,
                health: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasRush: true,
                sealboundOwnerTurnStarts: 2);
            var service = new PlayCardService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));
            var coord = new TileCoord(0, 0);

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, definition.CardId, coord);

            Assert.That(battleState.PlayerBoard.GetOccupant(coord), Is.SameAs(unit));
            Assert.That(unit.IsSealbound, Is.True);
            Assert.That(unit.SealboundOwnerTurnStartsRemaining, Is.EqualTo(2));
            Assert.That(unit.CannotAttack, Is.True);
            Assert.That(unit.CannotCounterattack, Is.True);
            Assert.That(unit.CannotMoveDueToState, Is.True);
            Assert.That(unit.DoesNotBlockFrontRow, Is.True);
            Assert.That(unit.HasSummoningSickness, Is.True);
            Assert.That(unit.RemainingAttacksThisTurn, Is.Zero);
        }

        [Test]
        public void ResolveTurnStart_SealboundCountsDownLastAndResumesEffectsOnFollowingOwnerTurn()
        {
            var battleState = CreateBattleStateAtTurnStart();
            var unit = new UnitState(
                runtimeId: "sealed-upkeep",
                cardId: "sealed-upkeep",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 5,
                maxHp: 20,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 1,
                turnStartResourceGain: new ResourceSet(mana: 5, qi: 0, power: 0, gold: 0));
            unit.EnterSealbound(2);
            battleState.PlayerBoard.Place(unit.Position, unit);
            var service = new TurnStartService();

            service.ResolveTurnStart(battleState);

            Assert.That(unit.IsSealbound, Is.True);
            Assert.That(unit.SealboundOwnerTurnStartsRemaining, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Mana, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(3));

            battleState.StartNextTurn(PlayerId.Player);
            service.ResolveTurnStart(battleState);

            Assert.That(unit.IsSealbound, Is.False);
            Assert.That(unit.SealboundOwnerTurnStartsRemaining, Is.Zero);
            Assert.That(unit.HasSummoningSickness, Is.True);
            Assert.That(unit.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(battleState.Player.Resources.Mana, Is.Zero,
                "An occupant released last in turn start must not grant resources during that turn start.");
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(4),
                "Sealbound upkeep must not be paid during the release turn start.");

            battleState.StartNextTurn(PlayerId.Player);
            service.ResolveTurnStart(battleState);

            Assert.That(unit.HasSummoningSickness, Is.False);
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(5));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(4),
                "The next applicable upkeep may use gold as a substitute for power.");
            Assert.That(unit.IsDrained, Is.False);
        }

        [Test]
        public void ResolveTurnStart_WhenReleasedUnitHasRush_AllowsAttackOnReleaseTurn()
        {
            var battleState = CreateBattleStateAtTurnStart();
            var unit = CreateUnit("sealed-rush", PlayerId.Player, new TileCoord(0, 0), hasRush: true);
            unit.EnterSealbound(1);
            battleState.PlayerBoard.Place(unit.Position, unit);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(unit.IsSealbound, Is.False);
            Assert.That(unit.HasSummoningSickness, Is.False);
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void Sealbound_BlocksDamageHealingBuffsErasureMovementGuardAndRobotSelection()
        {
            var battleState = CreateBattleStateInMainPhase();
            var unit = new UnitState(
                runtimeId: "sealed-robot",
                cardId: "sealed-robot",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 5,
                maxHp: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasGuard: true,
                physicalDefense: 2,
                magicDefense: 3,
                hasRobot: true);
            unit.CurrentHp = 10;
            unit.EnterSealbound(2);
            battleState.PlayerBoard.Place(unit.Position, unit);
            var attachedEffect = new PersistentEffectState(
                sourceCardId: "attached-effect",
                ownerId: PlayerId.AI,
                effectId: "attached-effect",
                appliedTurn: 1,
                endConditionText: "attached",
                turnStartResourceGain: new ResourceSet(),
                targetRuntimeId: unit.RuntimeId);
            battleState.PersistentEffects.Add(attachedEffect);

            var effectDamage = DamageResolutionRules.ApplyEffectDamage(unit, 100, DamageType.Fixed);
            var attackDamage = DamageResolutionRules.ApplyAttackDamage(unit, 100, DamageType.Fixed);
            unit.Heal(5);
            unit.IncreaseBaseAttack(5);
            unit.IncreaseMaxHpAndCurrentHp(5);
            unit.IncreasePhysicalDefense(5);
            unit.IncreaseMagicDefense(5);
            new ErasureService().Apply(battleState, unit);

            Assert.That(effectDamage, Is.Zero);
            Assert.That(attackDamage, Is.Zero);
            Assert.That(unit.CurrentHp, Is.EqualTo(10));
            Assert.That(unit.BaseAttack, Is.EqualTo(5));
            Assert.That(unit.MaxHp, Is.EqualTo(20));
            Assert.That(unit.PhysicalDefense, Is.EqualTo(2));
            Assert.That(unit.MagicDefense, Is.EqualTo(3));
            Assert.That(unit.IsErasure, Is.False, "Sealbound occupants are immune to Erasure.");
            Assert.That(attachedEffect.IsExpired, Is.False,
                "An Erasure attempt on a Sealbound occupant must not remove attached effects.");
            Assert.That(unit.HasActiveGuard, Is.False);
            Assert.That(unit.HasActiveRobot, Is.False);
            Assert.That(RobotFusionRules.CountLivingRobots(battleState.PlayerBoard), Is.Zero);
            Assert.Throws<InvalidOperationException>(() =>
                new MoveService().Move(battleState, PlayerId.Player, unit.Position, new TileCoord(1, 0)));
            Assert.Throws<InvalidOperationException>(() =>
                GuardService.Resolve(battleState.PlayerBoard, unit.Position));
        }

        [Test]
        public void Targeting_SealboundFrontCannotBeTargetedAndDoesNotBlockBackRow()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0));
            var sealedFront = CreateUnit("sealed-front", PlayerId.AI, new TileCoord(0, 0));
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            sealedFront.EnterSealbound(2);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(sealedFront.Position, sealedFront);
            battleState.AIBoard.Place(back.Position, back);

            var targets = new TargetingService().GetLegalTargets(
                battleState,
                PlayerId.Player,
                attacker.Position);

            Assert.That(targets, Has.No.Member(sealedFront.Position));
            Assert.That(targets, Does.Contain(back.Position));
        }

        [Test]
        public void StateViewRoundTrip_PreservesSealboundCountdownAndRush()
        {
            var battleState = CreateBattleStateInMainPhase();
            var unit = CreateUnit("sealed-view", PlayerId.Player, new TileCoord(0, 0), hasRush: true);
            unit.EnterSealbound(2);
            battleState.PlayerBoard.Place(unit.Position, unit);

            var view = new BattleStateViewFactory().CreateForPlayer(battleState, "sealbound-match", PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.PlayerBoard.GetOccupant(unit.Position);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.IsSealbound, Is.True);
            Assert.That(restored.SealboundOwnerTurnStartsRemaining, Is.EqualTo(2));
            Assert.That(restored.HasRush, Is.True);
            Assert.That(restored.IsErasure, Is.False);
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            bool hasRush = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: AttackType.Melee,
                attack: 5,
                maxHp: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasRush: hasRush);
        }

        private static BattleState CreateBattleStateAtTurnStart()
        {
            var battleState = new BattleSetupService().CreateInitialState(CreateSetupRequest());
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            return battleState;
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var battleState = CreateBattleStateAtTurnStart();
            new TurnStartService().ResolveTurnStart(battleState);
            return battleState;
        }

        private static BattleSetupRequest CreateSetupRequest()
        {
            return new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player);
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
