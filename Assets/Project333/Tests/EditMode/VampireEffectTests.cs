using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class VampireEffectTests
    {
        [Test]
        public void Attack_VampireHealsFromDamageDealtToMaster()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateCombatUnit("vampire", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);

            vampire.HasSummoningSickness = false;
            vampire.CurrentHp = 20;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(2, 1));

            Assert.That(vampire.CurrentHp, Is.EqualTo(37));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(313));
        }

        [Test]
        public void Attack_VampireLifestealCanSaveFromSimultaneousCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateCombatUnit("vampire", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);
            var defender = CreateCombatUnit("defender", "defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 20);

            vampire.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            vampire.CurrentHp = 5;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(vampire.CurrentHp, Is.EqualTo(15));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(vampire));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_VampireHealsBeforeCounterattackDamageIsApplied()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateCombatUnit("vampire", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 5, 20);
            var defender = CreateCombatUnit("defender", "defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 5);

            vampire.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            vampire.CurrentHp = 18;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(vampire.CurrentHp, Is.EqualTo(10));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(vampire));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        private static UnitState CreateCombatUnit(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp)
        {
            return new UnitState(
                runtimeId: runtimeId,
                cardId: cardId,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
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
