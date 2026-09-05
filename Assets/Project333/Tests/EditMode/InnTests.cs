using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class InnTests
    {
        [Test]
        public void ResolveTurnStart_EachActiveInnGrantsOneGold()
        {
            var battleState = CreateTurnStartBattle();
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateInn("inn-1", new TileCoord(0, 0)));
            battleState.PlayerBoard.Place(
                new TileCoord(4, 1),
                CreateInn("inn-2", new TileCoord(4, 1)));
            var goldBefore = battleState.Player.Resources.Gold;

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(
                battleState.Player.Resources.Gold,
                Is.EqualTo(goldBefore + (InnRules.TurnStartGoldGain * 2)));
        }

        [Test]
        public void ResolveTurnStart_SuppressedInnsDoNotGrantGold()
        {
            var battleState = CreateTurnStartBattle();
            var active = CreateInn("active", new TileCoord(0, 0));
            var drained = CreateInn("drained", new TileCoord(0, 1));
            var erased = CreateInn("erased", new TileCoord(1, 0));
            var sealbound = CreateInn("sealbound", new TileCoord(1, 1));
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
                Is.EqualTo(goldBefore + InnRules.TurnStartGoldGain));
        }

        private static BuildingState CreateInn(string runtimeId, TileCoord position)
        {
            return new BuildingState(
                runtimeId,
                InnRules.CardId,
                PlayerId.Player,
                position,
                canAttack: false,
                attack: 0,
                maxHp: InnRules.BaseHealth,
                turnStartResourceGain: new ResourceSet(
                    mana: 0,
                    qi: 0,
                    power: 0,
                    gold: InnRules.TurnStartGoldGain),
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
