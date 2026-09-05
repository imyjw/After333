using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class MerchantCaravanTests
    {
        [Test]
        public void ResolveTurnStart_EachActiveMerchantCaravanGrantsThreeGold()
        {
            var battleState = CreateTurnStartBattle();
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateMerchantCaravan("merchant-1", new TileCoord(0, 0)));
            battleState.PlayerBoard.Place(
                new TileCoord(4, 1),
                CreateMerchantCaravan("merchant-2", new TileCoord(4, 1)));
            var goldBefore = battleState.Player.Resources.Gold;

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(
                battleState.Player.Resources.Gold,
                Is.EqualTo(goldBefore + (MerchantCaravanRules.TurnStartGoldGain * 2)));
            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(2));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo(MerchantCaravanRules.CardId));
            Assert.That(
                battleState.ResourceChangeEvents[0].Gained.Gold,
                Is.EqualTo(MerchantCaravanRules.TurnStartGoldGain));
        }

        [Test]
        public void ResolveTurnStart_SuppressedMerchantCaravansDoNotGrantGold()
        {
            var battleState = CreateTurnStartBattle();
            var active = CreateMerchantCaravan("active", new TileCoord(0, 0));
            var drained = CreateMerchantCaravan("drained", new TileCoord(0, 1));
            var erased = CreateMerchantCaravan("erased", new TileCoord(1, 0));
            var sealbound = CreateMerchantCaravan("sealbound", new TileCoord(1, 1));
            drained.IsDrained = true;
            erased.ApplyErasure();
            sealbound.EnterSealbound(1);

            battleState.PlayerBoard.Place(active.Position, active);
            battleState.PlayerBoard.Place(drained.Position, drained);
            battleState.PlayerBoard.Place(erased.Position, erased);
            battleState.PlayerBoard.Place(sealbound.Position, sealbound);
            var goldBefore = battleState.Player.Resources.Gold;

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(
                battleState.Player.Resources.Gold,
                Is.EqualTo(goldBefore + MerchantCaravanRules.TurnStartGoldGain));
        }

        private static BuildingState CreateMerchantCaravan(string runtimeId, TileCoord position)
        {
            return new BuildingState(
                runtimeId,
                MerchantCaravanRules.CardId,
                PlayerId.Player,
                position,
                canAttack: false,
                attack: 0,
                maxHp: MerchantCaravanRules.BaseHealth,
                turnStartResourceGain: new ResourceSet(
                    mana: 0,
                    qi: 0,
                    power: 0,
                    gold: MerchantCaravanRules.TurnStartGoldGain),
                damageType: DamageType.None);
        }

        private static BattleState CreateTurnStartBattle()
        {
            var battleState = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
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
