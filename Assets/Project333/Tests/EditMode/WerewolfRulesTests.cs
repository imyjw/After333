using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class WerewolfRulesTests
    {
        [Test]
        public void Attack_RecalculatesWhenAnotherWerewolfEntersDiesAndLeavesBoard()
        {
            var board = new BoardState();
            var source = CreateWerewolf("source", PlayerId.Player, new TileCoord(0, 0));
            var ally = CreateWerewolf("ally", PlayerId.Player, new TileCoord(1, 0));

            board.Place(source.Position, source);
            Assert.That(source.Attack, Is.EqualTo(WerewolfRules.BaseAttack));

            board.Place(ally.Position, ally);
            Assert.That(
                source.Attack,
                Is.EqualTo(WerewolfRules.BaseAttack + WerewolfRules.AttackPerOtherWerewolf));

            ally.CurrentHp = 0;
            Assert.That(source.Attack, Is.EqualTo(WerewolfRules.BaseAttack));

            board.Remove(ally.Position);
            Assert.That(source.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(ally.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
        }

        [Test]
        public void Attack_CountsDrainedAndErasedAlliesButExcludesSealboundAlly()
        {
            var board = new BoardState();
            var source = CreateWerewolf("source", PlayerId.Player, new TileCoord(0, 0));
            var drained = CreateWerewolf("drained", PlayerId.Player, new TileCoord(1, 0));
            var erased = CreateWerewolf("erased", PlayerId.Player, new TileCoord(2, 0));
            var sealbound = CreateWerewolf("sealbound", PlayerId.Player, new TileCoord(3, 0));

            drained.IsDrained = true;
            erased.ApplyErasure();
            sealbound.EnterSealbound(2);
            board.Place(source.Position, source);
            board.Place(drained.Position, drained);
            board.Place(erased.Position, erased);
            board.Place(sealbound.Position, sealbound);

            Assert.That(
                source.Attack,
                Is.EqualTo(
                    WerewolfRules.BaseAttack +
                    (WerewolfRules.AttackPerOtherWerewolf * 2)));
        }

        [Test]
        public void Attack_SuppressedSourceDoesNotReceivePackBonus()
        {
            var drainedBoard = new BoardState();
            var drainedSource = CreateWerewolf("drained-source", PlayerId.Player, new TileCoord(0, 0));
            var drainedAlly = CreateWerewolf("drained-ally", PlayerId.Player, new TileCoord(1, 0));
            drainedBoard.Place(drainedSource.Position, drainedSource);
            drainedBoard.Place(drainedAlly.Position, drainedAlly);
            drainedSource.IsDrained = true;

            var erasedBoard = new BoardState();
            var erasedSource = CreateWerewolf("erased-source", PlayerId.Player, new TileCoord(0, 0));
            erasedBoard.Place(erasedSource.Position, erasedSource);
            erasedBoard.Place(
                new TileCoord(1, 0),
                CreateWerewolf("erased-ally", PlayerId.Player, new TileCoord(1, 0)));
            erasedSource.ApplyErasure();

            var sealboundBoard = new BoardState();
            var sealboundSource = CreateWerewolf("sealbound-source", PlayerId.Player, new TileCoord(0, 0));
            sealboundBoard.Place(sealboundSource.Position, sealboundSource);
            sealboundBoard.Place(
                new TileCoord(1, 0),
                CreateWerewolf("sealbound-ally", PlayerId.Player, new TileCoord(1, 0)));
            sealboundSource.EnterSealbound(2);

            Assert.That(drainedSource.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(erasedSource.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(sealboundSource.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
        }

        [Test]
        public void AttackService_UsesLiveWerewolfPackAttack()
        {
            var battleState = CreateBattleState();
            var attacker = CreateWerewolf("attacker", PlayerId.Player, new TileCoord(0, 0));
            var ally = CreateWerewolf("ally", PlayerId.Player, new TileCoord(1, 0));
            var defender = new UnitState(
                "defender",
                "defender",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 0,
                maxHp: 50,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
            attacker.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.PlayerBoard.Place(ally.Position, ally);
            battleState.AIBoard.Place(defender.Position, defender);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                defender.Position);

            Assert.That(defender.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void StateViewProjection_PreservesBaseAttackAndAppliesPackBonusOnce()
        {
            var battleState = CreateBattleState();
            var source = CreateWerewolf("source", PlayerId.Player, new TileCoord(0, 0));
            var ally = CreateWerewolf("ally", PlayerId.Player, new TileCoord(1, 0));
            battleState.PlayerBoard.Place(source.Position, source);
            battleState.PlayerBoard.Place(ally.Position, ally);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "werewolf-view",
                PlayerId.Player);
            var sourceView = view.Occupants.Find(candidate => candidate.RuntimeId == source.RuntimeId);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var projectedSource = projected.PlayerBoard.GetOccupant(source.Position);

            Assert.That(sourceView, Is.Not.Null);
            Assert.That(sourceView.HasBaseAttack, Is.True);
            Assert.That(sourceView.BaseAttack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(
                sourceView.Attack,
                Is.EqualTo(WerewolfRules.BaseAttack + WerewolfRules.AttackPerOtherWerewolf));
            Assert.That(projectedSource.BaseAttack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(
                projectedSource.Attack,
                Is.EqualTo(WerewolfRules.BaseAttack + WerewolfRules.AttackPerOtherWerewolf));
        }

        private static UnitState CreateWerewolf(
            string runtimeId,
            PlayerId ownerId,
            TileCoord position)
        {
            return new UnitState(
                runtimeId,
                WerewolfRules.CardId,
                ownerId,
                position,
                AttackType.Melee,
                WerewolfRules.BaseAttack,
                WerewolfRules.BaseHealth,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                damageType: DamageType.Physical);
        }

        private static BattleState CreateBattleState()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
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
