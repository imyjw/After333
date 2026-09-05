using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class HidingTests
    {
        [Test]
        public void PlayUnitCard_WithHidingDefinition_EntersFieldHidden()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("hiding-unit");
            var definition = new UnitCardDefinition(
                cardId: "hiding-unit",
                displayName: "Hiding Unit",
                cost: new ResourceSet(),
                attackType: AttackType.Melee,
                attack: 7,
                health: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasHiding: true);
            var service = new PlayCardService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));
            var coord = new TileCoord(0, 0);

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, definition.CardId, coord);

            Assert.That(unit.HasHiding, Is.True);
            Assert.That(unit.HidingRevealed, Is.False);
            Assert.That(unit.IsHiding, Is.True);
            Assert.That(unit.DoesNotBlockFrontRow, Is.True);
        }

        [Test]
        public void Targeting_HidingFrontCannotBeTargetedAndDoesNotBlockBackRow()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0));
            var hiddenFront = CreateUnit("hidden-front", PlayerId.AI, new TileCoord(0, 0), hasHiding: true);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(hiddenFront.Position, hiddenFront);
            battleState.AIBoard.Place(back.Position, back);

            var targets = new TargetingService().GetLegalTargets(
                battleState,
                PlayerId.Player,
                attacker.Position);

            Assert.That(targets, Has.No.Member(hiddenFront.Position));
            Assert.That(targets, Does.Contain(back.Position));
        }

        [Test]
        public void Attack_LegalDeclarationRevealsBeforeCombatAndIllegalAttemptDoesNotReveal()
        {
            var battleState = CreateBattleStateInMainPhase();
            var hiddenAttacker = CreateUnit("hidden-attacker", PlayerId.Player, new TileCoord(0, 0), hasHiding: true);
            var defender = CreateUnit("defender", PlayerId.AI, new TileCoord(0, 0));
            battleState.PlayerBoard.Place(hiddenAttacker.Position, hiddenAttacker);
            battleState.AIBoard.Place(defender.Position, defender);

            Assert.Throws<InvalidOperationException>(() =>
                new AttackService().Attack(
                    battleState,
                    PlayerId.Player,
                    hiddenAttacker.Position,
                    defender.Position));
            Assert.That(hiddenAttacker.IsHiding, Is.True,
                "A rejected attack must not reveal Hiding.");

            hiddenAttacker.HasSummoningSickness = false;
            hiddenAttacker.RemainingAttacksThisTurn = 1;
            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                hiddenAttacker.Position,
                defender.Position);

            Assert.That(hiddenAttacker.IsHiding, Is.False);
            Assert.That(hiddenAttacker.HidingRevealed, Is.True);
            Assert.That(hiddenAttacker.CurrentHp, Is.EqualTo(15),
                "The revealed melee attacker must receive the normal counterattack.");
            Assert.That(defender.CurrentHp, Is.EqualTo(15));
        }

        [Test]
        public void SingleTargetSpell_EnemyCannotTargetHidingButOwnerCanAndAreaDamageStillApplies()
        {
            var battleState = CreateBattleStateInMainPhase();
            var enemyHidden = CreateUnit("enemy-hidden", PlayerId.AI, new TileCoord(0, 0), hasHiding: true);
            var alliedHidden = CreateUnit("allied-hidden", PlayerId.Player, new TileCoord(1, 0), hasHiding: true);
            battleState.AIBoard.Place(enemyHidden.Position, enemyHidden);
            battleState.PlayerBoard.Place(alliedHidden.Position, alliedHidden);
            battleState.Player.Hand.Add("test-spell");
            var spellService = new SpellService();

            Assert.Throws<InvalidOperationException>(() =>
                spellService.CastDamageSpell(
                    battleState,
                    PlayerId.Player,
                    "test-spell",
                    PlayerId.AI,
                    enemyHidden.Position,
                    damage: 5,
                    damageType: DamageType.Magic));
            Assert.That(battleState.Player.Hand.Contains("test-spell"), Is.True,
                "A rejected spell must not consume the card.");

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "test-spell",
                PlayerId.Player,
                alliedHidden.Position,
                damage: 5,
                damageType: DamageType.Magic);

            Assert.That(alliedHidden.CurrentHp, Is.EqualTo(15));
            Assert.That(alliedHidden.IsHiding, Is.True,
                "An allied single-target spell must not reveal Hiding.");

            var areaDamage = DamageResolutionRules.ApplyEffectDamage(
                enemyHidden,
                3,
                DamageType.Magic);
            Assert.That(areaDamage, Is.EqualTo(3));
            Assert.That(enemyHidden.CurrentHp, Is.EqualTo(17));
            Assert.That(enemyHidden.IsHiding, Is.True);
        }

        [Test]
        public void DrainedAndErasure_PermanentlyRevealHiding()
        {
            var drained = CreateUnit("drained-hidden", PlayerId.Player, new TileCoord(0, 0), hasHiding: true);
            drained.IsDrained = true;

            Assert.That(drained.IsHiding, Is.False);
            Assert.That(drained.HidingRevealed, Is.True);

            drained.IsDrained = false;
            Assert.That(drained.IsHiding, Is.False,
                "Clearing Drained must not restore Hiding.");

            var erased = CreateUnit("erased-hidden", PlayerId.Player, new TileCoord(1, 0), hasHiding: true);
            erased.ApplyErasure();

            Assert.That(erased.IsErasure, Is.True);
            Assert.That(erased.IsHiding, Is.False);
            Assert.That(erased.HidingRevealed, Is.True);
        }

        [Test]
        public void Sealbound_TakesPriorityOverHidingAndErasureImmunityPreservesItForRelease()
        {
            var battleState = CreateBattleStateInMainPhase();
            var unit = CreateUnit("sealed-hidden", PlayerId.Player, new TileCoord(0, 0), hasHiding: true);
            unit.EnterSealbound(1);
            battleState.PlayerBoard.Place(unit.Position, unit);

            new ErasureService().Apply(battleState, unit);

            Assert.That(unit.IsErasure, Is.False);
            Assert.That(unit.HidingRevealed, Is.False);
            Assert.That(unit.IsHiding, Is.False,
                "Sealbound is the active state until release.");

            unit.ResolveSealboundOwnerTurnStart();

            Assert.That(unit.IsSealbound, Is.False);
            Assert.That(unit.IsHiding, Is.True);
        }

        [Test]
        public void Shielder_HidingShielderDoesNotRedirectUntilRevealed()
        {
            var board = new BoardState();
            var hiddenShielder = CreateUnit("hidden-shielder", PlayerId.AI, new TileCoord(0, 0), hasHiding: true, hasShielder: true);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            board.Place(hiddenShielder.Position, hiddenShielder);
            board.Place(back.Position, back);

            Assert.That(ShielderService.Resolve(board, back.Position).IsProtected, Is.False);

            hiddenShielder.RevealHiding();

            Assert.That(ShielderService.Resolve(board, back.Position).IsProtected, Is.True);
        }

        [Test]
        public void StateViewRoundTrip_PreservesHidingTraitAndRevealState()
        {
            var battleState = CreateBattleStateInMainPhase();
            var hidden = CreateUnit("hidden-view", PlayerId.Player, new TileCoord(0, 0), hasHiding: true);
            var revealed = CreateUnit("revealed-view", PlayerId.Player, new TileCoord(1, 0), hasHiding: true);
            revealed.RevealHiding();
            battleState.PlayerBoard.Place(hidden.Position, hidden);
            battleState.PlayerBoard.Place(revealed.Position, revealed);

            var view = new BattleStateViewFactory().CreateForPlayer(battleState, "hiding-match", PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restoredHidden = projected.PlayerBoard.GetOccupant(hidden.Position);
            var restoredRevealed = projected.PlayerBoard.GetOccupant(revealed.Position);

            Assert.That(restoredHidden.HasHiding, Is.True);
            Assert.That(restoredHidden.IsHiding, Is.True);
            Assert.That(restoredHidden.HidingRevealed, Is.False);
            Assert.That(restoredRevealed.HasHiding, Is.True);
            Assert.That(restoredRevealed.IsHiding, Is.False);
            Assert.That(restoredRevealed.HidingRevealed, Is.True);
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            bool hasHiding = false,
            bool hasShielder = false)
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
                hasShielder: hasShielder,
                hasHiding: hasHiding);
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            new TurnStartService().ResolveTurnStart(battleState);
            return battleState;
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
