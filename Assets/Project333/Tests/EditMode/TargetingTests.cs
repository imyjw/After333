using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class TargetingTests
    {
        [Test]
        public void GetLegalTargets_MeleeHonorsFrontRowBlocking()
        {
            var battleState = CreateBattleState();
            var targetingService = new TargetingService();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), CreateUnit("enemy-front-0", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee));
            battleState.AIBoard.Place(new TileCoord(2, 0), CreateUnit("enemy-front-2", PlayerId.AI, new TileCoord(2, 0), AttackType.Melee));
            battleState.AIBoard.Place(new TileCoord(4, 1), CreateUnit("enemy-back-4", PlayerId.AI, new TileCoord(4, 1), AttackType.Melee));

            var legalTargets = targetingService.GetLegalTargets(battleState, PlayerId.Player, new TileCoord(0, 0));

            Assert.That(legalTargets, Does.Contain(new TileCoord(0, 0)));
            Assert.That(legalTargets, Does.Contain(new TileCoord(2, 0)));
            Assert.That(legalTargets, Does.Contain(new TileCoord(4, 1)));
            Assert.That(legalTargets, Has.No.Member(new TileCoord(2, 1)));
        }

        [Test]
        public void GetLegalTargets_RangedIgnoresFrontRowBlocking()
        {
            var battleState = CreateBattleState();
            var targetingService = new TargetingService();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(2, 0), CreateUnit("enemy-front", PlayerId.AI, new TileCoord(2, 0), AttackType.Melee));

            var legalTargets = targetingService.GetLegalTargets(battleState, PlayerId.Player, new TileCoord(0, 0));

            Assert.That(legalTargets, Does.Contain(new TileCoord(2, 0)));
            Assert.That(legalTargets, Does.Contain(new TileCoord(2, 1)));
        }

        [Test]
        public void GetLegalTargets_DisabledScienceUnitDoesNotBlockBackRow()
        {
            var battleState = CreateBattleState();
            var targetingService = new TargetingService();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee);
            var disabledScienceFront = new UnitState(
                runtimeId: "science-front",
                cardId: "science-front",
                ownerId: PlayerId.AI,
                position: new TileCoord(2, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 1);

            disabledScienceFront.IsDisabled = true;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(2, 0), disabledScienceFront);

            var legalTargets = targetingService.GetLegalTargets(battleState, PlayerId.Player, new TileCoord(0, 0));

            Assert.That(legalTargets, Does.Contain(new TileCoord(2, 1)));
        }

        private static UnitState CreateUnit(string id, PlayerId ownerId, TileCoord coord, AttackType attackType)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
