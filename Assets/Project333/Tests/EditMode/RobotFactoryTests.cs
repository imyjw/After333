using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class RobotFactoryTests
    {
        [Test]
        public void ResolveTurnStart_GeneratesRobotBeforeBaseDraw_AndBurnsBaseDrawAtFullHand()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            for (var i = 0; i < PlayerState.BaseMaxHandSize - 1; i++)
            {
                battleState.Player.Hand.Add($"existing-{i}");
            }

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);
            var deckBefore = battleState.Player.Deck.Count;

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(factory.IsDrained, Is.False);
            Assert.That(factory.IsErasure, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(PlayerState.BaseMaxHandSize));
            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.True);
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckBefore - 1));
            Assert.That(battleState.Player.FailedDrawCount, Is.EqualTo(0));
            Assert.That(battleState.CardGenerationEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.CardGenerationEvents[0].AddedToHand, Is.True);
        }

        [Test]
        public void ResolveTurnStart_WhenFactoryUpkeepCannotBePaid_DrainsFactoryAndSkipsGeneration()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Spend(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 3));
            battleState.RestoreRuntimeState(1, PlayerId.Player, PhaseType.TurnStart);
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(factory.IsDrained, Is.True);
            Assert.That(factory.IsErasure, Is.False);
            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.False);
            Assert.That(battleState.CardGenerationEvents, Is.Empty);
        }

        [Test]
        public void ResolveTurnStart_WhenDrainedFactoryUpkeepIsPaid_ClearsDrainedAndGeneratesCard()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            factory.IsDrained = true;
            battleState.PlayerBoard.Place(factory.Position, factory);

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(factory.IsDrained, Is.False);
            Assert.That(factory.IsErasure, Is.False);
            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.True);
            Assert.That(battleState.CardGenerationEvents, Has.Count.EqualTo(1));
        }

        [Test]
        public void ResolveTurnStart_ErasureFactoryPaysNoUpkeepAndGeneratesNoCard()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            factory.ApplyErasure();
            battleState.PlayerBoard.Place(factory.Position, factory);

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(factory.IsErasure, Is.True);
            Assert.That(factory.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(1));
            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.False);
            Assert.That(battleState.CardGenerationEvents, Is.Empty);
        }

        [Test]
        public void ResolveTurnStart_ActiveFactoriesGenerateOneCardEachInTileOrder()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 2, gold: 0));
            var laterFactory = CreateFactory("later", new TileCoord(4, 1));
            var earlierFactory = CreateFactory("earlier", new TileCoord(0, 1));
            battleState.PlayerBoard.Place(laterFactory.Position, laterFactory);
            battleState.PlayerBoard.Place(earlierFactory.Position, earlierFactory);

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.CardGenerationEvents, Has.Count.EqualTo(2));
            Assert.That(battleState.CardGenerationEvents[0].SourceRuntimeId, Is.EqualTo("earlier"));
            Assert.That(battleState.CardGenerationEvents[1].SourceRuntimeId, Is.EqualTo("later"));
            Assert.That(battleState.Player.Hand.CardIds, Has.Exactly(2).EqualTo("Robot-A"));
        }

        [Test]
        public void ResolveTurnStart_FiltersOutRobotUnitsExcludedFromDraft()
        {
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                CreateRobotDefinition("Robot-Included", includeInDraft: true),
                CreateRobotDefinition("Robot-Hidden", includeInDraft: false),
            });
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);

            new TurnStartService(
                    new ScienceUpkeepService(),
                    new RobotFactoryService(provider, new Random(333)))
                .ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Hand.Contains("Robot-Included"), Is.True);
            Assert.That(battleState.Player.Hand.Contains("Robot-Hidden"), Is.False);
        }

        [Test]
        public void ResolveTurnStart_WhenGeneratedCardCannotFit_BurnsItWithoutDeckFailureDamage()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            for (var i = 0; i < battleState.Player.MaxHandSize; i++)
            {
                battleState.Player.Hand.Add($"full-{i}");
            }

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);
            var masterHpBefore = battleState.Player.Master.CurrentHp;

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(battleState.Player.MaxHandSize));
            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.False);
            Assert.That(battleState.CardGenerationEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.CardGenerationEvents[0].AddedToHand, Is.False);
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(masterHpBefore));
        }

        [Test]
        public void GeneratedRobot_WhenPlayed_UsesOwnersCurrentUpgradeLevel()
        {
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                CreateRobotDefinition("Robot-A", includeInDraft: true),
            });
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);
            new TurnStartService(
                    new ScienceUpkeepService(),
                    new RobotFactoryService(provider, new Random(333)))
                .ResolveTurnStart(battleState);

            var unit = new PlayCardService(
                    provider,
                    new SummonService(),
                    new FixedUpgradeLevelProvider("Robot-A", 3))
                .PlayUnitCard(battleState, PlayerId.Player, "Robot-A", new TileCoord(1, 0));

            Assert.That(unit.BaseAttack, Is.EqualTo(2));
            Assert.That(unit.MaxHp, Is.EqualTo(3));
        }

        [Test]
        public void ResolveTurnStart_RobotFactoryRunsBeforeFirewall_AndBaseDrawRunsAfterFirewall()
        {
            var battleState = CreateTurnStartBattle();
            ClearHand(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            var factory = CreateFactory("factory", new TileCoord(0, 0));
            battleState.PlayerBoard.Place(factory.Position, factory);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: "Firewall",
                ownerId: PlayerId.AI,
                effectId: "firewall",
                appliedTurn: 2,
                endConditionText: "1 trigger",
                turnStartResourceGain: new ResourceSet(),
                targetRow: 1,
                remainingTriggers: 1,
                effectDamage: 333,
                effectDamageType: DamageType.Magic,
                targetsOwnerBoard: false));
            var deckBefore = battleState.Player.Deck.Count;

            CreateTurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Hand.Contains("Robot-A"), Is.True);
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckBefore));
            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        private static TurnStartService CreateTurnStartService()
        {
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                CreateRobotDefinition("Robot-A", includeInDraft: true),
            });
            return new TurnStartService(
                new ScienceUpkeepService(),
                new RobotFactoryService(provider, new Random(333)));
        }

        private static UnitCardDefinition CreateRobotDefinition(string cardId, bool includeInDraft)
        {
            return new UnitCardDefinition(
                cardId,
                cardId,
                new ResourceSet(),
                AttackType.Melee,
                attack: 1,
                health: 1,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 0,
                hasRobot: true,
                includeInDraft: includeInDraft);
        }

        private static BuildingState CreateFactory(string runtimeId, TileCoord coord)
        {
            return new BuildingState(
                runtimeId,
                RobotFactoryService.CardId,
                PlayerId.Player,
                coord,
                canAttack: false,
                attack: 0,
                maxHp: 50,
                damageType: DamageType.None,
                sciencePowerUpkeep: 1);
        }

        private static BattleState CreateTurnStartBattle()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.RestoreRuntimeState(3, PlayerId.Player, PhaseType.TurnStart);
            return battleState;
        }

        private static void ClearHand(PlayerState player)
        {
            foreach (var cardId in new List<string>(player.Hand.CardIds))
            {
                player.Hand.Remove(cardId);
            }
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var i = 0; i < 20; i++)
            {
                cards.Add($"{prefix}-{i}");
            }

            return cards;
        }

        private sealed class FixedUpgradeLevelProvider : ICardUpgradeLevelProvider
        {
            private readonly string _cardId;
            private readonly int _level;

            public FixedUpgradeLevelProvider(string cardId, int level)
            {
                _cardId = cardId;
                _level = level;
            }

            public int GetUpgradeLevel(PlayerId ownerId, string cardId)
            {
                return string.Equals(cardId, _cardId, StringComparison.Ordinal) ? _level : 0;
            }
        }
    }
}
