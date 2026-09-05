using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class DemonKingTests
    {
        [Test]
        public void ResolveDefeatedOccupants_DemonKingStaysOnItsTileAndEntersRevivalSeal()
        {
            var battleState = CreateBattleState(turnNumber: 4, activePlayerId: PlayerId.AI);
            var demonKing = CreateDemonKing();
            battleState.PlayerBoard.Place(demonKing.Position, demonKing);
            demonKing.CurrentHp = 0;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(demonKing.Position), Is.SameAs(demonKing));
            Assert.That(demonKing.CurrentHp, Is.Zero);
            Assert.That(demonKing.IsSealbound, Is.True);
            Assert.That(demonKing.IsDemonKingRevivalPending, Is.True);
            Assert.That(
                demonKing.DemonKingRevivalTurnStartsRemaining,
                Is.EqualTo(DemonKingRules.RevivalTurnStarts));
            Assert.That(demonKing.DemonKingRevivalCountdownPlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(demonKing.DemonKingRevivalEligibleAfterTurnNumber, Is.EqualTo(4));
        }

        [Test]
        public void DestroyOccupant_DirectDestructionAlsoEntersRevivalSeal()
        {
            var battleState = CreateBattleState(turnNumber: 3, activePlayerId: PlayerId.Player);
            var demonKing = CreateDemonKing();
            battleState.PlayerBoard.Place(demonKing.Position, demonKing);

            OccupantDestructionService.DestroyOccupant(
                battleState,
                PlayerId.Player,
                demonKing);

            Assert.That(battleState.PlayerBoard.GetOccupant(demonKing.Position), Is.SameAs(demonKing));
            Assert.That(demonKing.CurrentHp, Is.Zero);
            Assert.That(demonKing.IsDemonKingRevivalPending, Is.True);
            Assert.That(demonKing.DemonKingRevivalCountdownPlayerId, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void ResolveTurnStart_CountsDeathTurnPlayerAndRevivesOnItsThirdFutureTurnStart()
        {
            var battleState = CreateBattleState(turnNumber: 4, activePlayerId: PlayerId.AI);
            var demonKing = CreateDemonKing();
            battleState.PlayerBoard.Place(demonKing.Position, demonKing);
            demonKing.CurrentHp = 0;
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
            var service = new TurnStartService();

            ResolveTurnStart(battleState, service, PlayerId.Player);
            Assert.That(demonKing.DemonKingRevivalTurnStartsRemaining, Is.EqualTo(3));

            ResolveTurnStart(battleState, service, PlayerId.AI);
            Assert.That(demonKing.DemonKingRevivalTurnStartsRemaining, Is.EqualTo(2));
            ResolveTurnStart(battleState, service, PlayerId.Player);
            ResolveTurnStart(battleState, service, PlayerId.AI);
            Assert.That(demonKing.DemonKingRevivalTurnStartsRemaining, Is.EqualTo(1));
            ResolveTurnStart(battleState, service, PlayerId.Player);
            ResolveTurnStart(battleState, service, PlayerId.AI);

            Assert.That(demonKing.IsDemonKingRevivalPending, Is.False);
            Assert.That(demonKing.IsSealbound, Is.False);
            Assert.That(demonKing.DemonKingRevivalCount, Is.EqualTo(1));
            Assert.That(demonKing.BaseAttack, Is.EqualTo(66));
            Assert.That(demonKing.MaxHp, Is.EqualTo(66));
            Assert.That(demonKing.CurrentHp, Is.EqualTo(66));
            Assert.That(demonKing.HasSummoningSickness, Is.True);
            Assert.That(demonKing.RemainingAttacksThisTurn, Is.Zero);
        }

        [Test]
        public void RepeatedRevival_AddsThirtyThreeToOriginalStatsForEachRevival()
        {
            var demonKing = CreateDemonKing();

            Assert.That(demonKing.TryEnterDemonKingRevivalSeal(PlayerId.AI, 4), Is.True);
            ResolveThreeCountdownStarts(demonKing, PlayerId.AI, 6);
            Assert.That(demonKing.BaseAttack, Is.EqualTo(66));
            Assert.That(demonKing.MaxHp, Is.EqualTo(66));

            Assert.That(demonKing.TryEnterDemonKingRevivalSeal(PlayerId.Player, 11), Is.True);
            ResolveThreeCountdownStarts(demonKing, PlayerId.Player, 13);

            Assert.That(demonKing.DemonKingRevivalCount, Is.EqualTo(2));
            Assert.That(demonKing.BaseAttack, Is.EqualTo(99));
            Assert.That(demonKing.MaxHp, Is.EqualTo(99));
            Assert.That(demonKing.CurrentHp, Is.EqualTo(99));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DefeatedWhileEffectsSuppressed_IsRemovedPermanently(
            bool drained,
            bool erased)
        {
            var battleState = CreateBattleState(turnNumber: 3, activePlayerId: PlayerId.Player);
            var demonKing = CreateDemonKing();
            demonKing.IsDrained = drained;
            if (erased)
            {
                demonKing.ApplyErasure();
            }

            battleState.PlayerBoard.Place(demonKing.Position, demonKing);
            demonKing.CurrentHp = 0;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(demonKing.Position), Is.Null);
            Assert.That(demonKing.IsDemonKingRevivalPending, Is.False);
        }

        [Test]
        public void StateViewRoundTrip_PreservesDemonKingRevivalState()
        {
            var battleState = CreateBattleState(turnNumber: 4, activePlayerId: PlayerId.AI);
            var demonKing = CreateDemonKing();
            battleState.PlayerBoard.Place(demonKing.Position, demonKing);
            Assert.That(demonKing.TryEnterDemonKingRevivalSeal(PlayerId.AI, 4), Is.True);
            Assert.That(demonKing.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 6), Is.False);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "demon-king-match",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.PlayerBoard.GetOccupant(demonKing.Position);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.IsDemonKingRevivalPending, Is.True);
            Assert.That(restored.DemonKingRevivalTurnStartsRemaining, Is.EqualTo(2));
            Assert.That(restored.DemonKingRevivalCountdownPlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(restored.DemonKingRevivalEligibleAfterTurnNumber, Is.EqualTo(4));
            Assert.That(restored.DemonKingRevivalCount, Is.Zero);
            Assert.That(restored.CurrentHp, Is.Zero);
        }

        private static UnitState CreateDemonKing()
        {
            return new UnitState(
                runtimeId: "demon-king-runtime",
                cardId: DemonKingRules.CardId,
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: DemonKingRules.BaseAttack,
                maxHp: DemonKingRules.BaseHealth,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                damageType: DamageType.Magic,
                physicalDefense: DemonKingRules.PhysicalDefense,
                magicDefense: DemonKingRules.MagicDefense);
        }

        private static void ResolveThreeCountdownStarts(
            OccupantState demonKing,
            PlayerId countdownPlayerId,
            int firstTurnNumber)
        {
            Assert.That(
                demonKing.ResolveDemonKingRevivalTurnStart(countdownPlayerId, firstTurnNumber),
                Is.False);
            Assert.That(
                demonKing.ResolveDemonKingRevivalTurnStart(countdownPlayerId, firstTurnNumber + 2),
                Is.False);
            Assert.That(
                demonKing.ResolveDemonKingRevivalTurnStart(countdownPlayerId, firstTurnNumber + 4),
                Is.True);
        }

        private static void ResolveTurnStart(
            BattleState battleState,
            TurnStartService service,
            PlayerId activePlayerId)
        {
            battleState.StartNextTurn(activePlayerId);
            service.ResolveTurnStart(battleState);
        }

        private static BattleState CreateBattleState(int turnNumber, PlayerId activePlayerId)
        {
            var request = new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player);
            var battleState = new BattleSetupService().CreateInitialState(request);
            battleState.RestoreRuntimeState(turnNumber, activePlayerId, PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var index = 0; index < 20; index++)
            {
                cards.Add($"{prefix}-{index:00}");
            }

            return cards;
        }
    }
}
