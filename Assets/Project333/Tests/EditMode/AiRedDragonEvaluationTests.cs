using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class AiRedDragonEvaluationTests
    {
        [Test]
        public void FutureDoomedProducer_HasNoMaterialIncomeOrAttackCreditInEitherScoreTerm()
        {
            var immediate = State();
            var coord = new TileCoord(0, 1);
            Place(immediate, PlayerId.AI, coord, "doomed-producer", 80, 33,
                ranged: true, producer: true);
            var near = Near(immediate);
            near.AIBoard.GetOccupant(coord).CurrentHp = 11;
            var future = AiBattleStateCopy.Create(near);
            future.AIBoard.Remove(coord);
            var emptyImmediate = AiBattleStateCopy.Create(immediate);
            emptyImmediate.AIBoard.Remove(coord);
            var emptyNear = AiBattleStateCopy.Create(near);
            emptyNear.AIBoard.Remove(coord);

            var forecast = Forecast(immediate, near, future.AIBoard) * 0.25 +
                Forecast(near, near, future.AIBoard) * 0.75;
            var reference = Before(emptyImmediate, emptyNear) * 0.25 + Evaluate(emptyNear) * 0.75;

            Assert.That(forecast, Is.EqualTo(reference).Within(0.000001));
            Assert.That(Before(immediate, near) * 0.25 + Evaluate(near) * 0.75,
                Is.GreaterThan(reference + 100));
        }

        [Test]
        public void GuardDoomedAtEnemyTurnEnd_StillProtectsMasterDuringEnemyMainPhase()
        {
            var immediate = State();
            immediate.AI.Master.CurrentHp = 10;
            var guardCoord = new TileCoord(2, 0);
            Place(immediate, PlayerId.AI, guardCoord, "temporary-guard", 0, 33);
            Place(immediate, PlayerId.Player, new TileCoord(0, 0), "enemy-melee", 100, 100);
            var near = Near(immediate);
            var future = AiBattleStateCopy.Create(near);
            future.AIBoard.Remove(guardCoord);
            var exposedImmediate = AiBattleStateCopy.Create(immediate);
            exposedImmediate.AIBoard.Remove(guardCoord);
            var exposedNear = AiBattleStateCopy.Create(near);
            exposedNear.AIBoard.Remove(guardCoord);

            var guardedScore = Forecast(immediate, near, future.AIBoard);
            var exposedScore = Forecast(exposedImmediate, exposedNear, future.AIBoard);

            Assert.That(guardedScore, Is.GreaterThan(exposedScore + 50),
                "The still-living near-horizon guard blocks imminent attacks even though it has no future material value.");
        }

        [Test]
        public void HypotheticalPassTerminalAndMasterHp_AreNotImportedIntoEvaluation()
        {
            var immediate = State();
            var near = Near(immediate);
            var hypotheticalPass = AiBattleStateCopy.Create(near);
            hypotheticalPass.Player.Master.CurrentHp = 0;
            hypotheticalPass.AI.Master.CurrentHp = 0;
            hypotheticalPass.Player.Resources.Add(new ResourceSet(100, 100, 100, 100));
            hypotheticalPass.AI.Resources.Add(new ResourceSet(100, 100, 100, 100));
            hypotheticalPass.EndBattleAsDraw();

            Assert.That(Forecast(immediate, near, hypotheticalPass.AIBoard),
                Is.EqualTo(Before(immediate, near)).Within(0.000001));
            Assert.That(Forecast(near, near, hypotheticalPass.AIBoard),
                Is.EqualTo(Evaluate(near)).Within(0.000001));
        }

        [Test]
        public void FuturePendingDemonKing_RetainsMaterialButDoesNotProjectAnAttack()
        {
            var immediate = State();
            var coord = new TileCoord(0, 0);
            Place(immediate, PlayerId.AI, coord, DemonKingRules.CardId, 100, 33);
            var near = Near(immediate);
            var aliveFuture = AiBattleStateCopy.Create(near);
            var pendingFuture = AiBattleStateCopy.Create(near);
            var pending = pendingFuture.AIBoard.GetOccupant(coord);
            Assert.That(pending.TryEnterDemonKingRevivalSeal(PlayerId.Player, near.TurnNumber), Is.True);
            var removedFuture = AiBattleStateCopy.Create(near);
            removedFuture.AIBoard.Remove(coord);

            var aliveScore = Forecast(near, near, aliveFuture.AIBoard);
            var pendingScore = Forecast(near, near, pendingFuture.AIBoard);
            var removedScore = Forecast(near, near, removedFuture.AIBoard);

            Assert.That(pendingScore, Is.LessThan(aliveScore));
            Assert.That(pendingScore, Is.GreaterThan(removedScore + 100));
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var card in player.Hand.CardIds.ToArray()) player.Hand.Remove(card);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }

        private static BattleState Near(BattleState state)
        {
            var near = AiBattleStateCopy.Create(state);
            near.StartNextTurn(PlayerId.Player);
            near.SetPhase(PhaseType.Main);
            return near;
        }

        private static UnitState Place(BattleState state, PlayerId owner, TileCoord coord, string cardId,
            int attack, int hp, bool ranged = false, bool producer = false)
        {
            var unit = new UnitState(owner + "-" + cardId, cardId, owner, coord,
                ranged ? AttackType.Ranged : AttackType.Melee, attack, hp, false, false, 0,
                producer ? new ResourceSet(1, 0, 0, 0) : null);
            state.GetBoard(owner).Place(coord, unit);
            return unit;
        }

        private static double Evaluate(BattleState state) => Invoke("Evaluate", state);
        private static double Before(BattleState state, BattleState near) => Invoke("EvaluateBeforeTurnStart", state, near);
        private static double Forecast(BattleState state, BattleState near, BoardState futureAiBoard) =>
            Invoke("EvaluateWithSurvivalForecast", state, near, futureAiBoard);

        private static double Invoke(string method, params object[] args)
        {
            var type = typeof(AiDecisionService).Assembly.GetType(
                "Project333.Runtime.Application.Services.AiPositionEvaluator", throwOnError: true);
            return (double)type.GetMethod(method).Invoke(Activator.CreateInstance(type, nonPublic: true), args);
        }
    }
}