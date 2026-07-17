using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class BattleCommandProcessorTests
    {
        [Test]
        public void Execute_PlayUnitCardCommand_PlacesUnitAndSpendsResources()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("unit-card");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 5));

            var processor = CreateProcessor(new CardDefinition[]
            {
                new UnitCardDefinition(
                    cardId: "unit-card",
                    displayName: "Test Unit",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 2),
                    attackType: AttackType.Melee,
                    attack: 3,
                    health: 6,
                    canMove: true,
                    isScience: false,
                    sciencePowerUpkeep: 0),
            });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayUnitCardCommand("unit-card", new TileCoord(0, 0)));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Not.Null);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(6));
            Assert.That(battleState.Player.Hand.Contains("unit-card"), Is.False);
        }

        [Test]
        public void Execute_CastDamageSpellCommand_UsesDefinitionBasedSpell()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("spell-damage");
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0), 2, 6);
            battleState.AIBoard.Place(new TileCoord(0, 0), target);

            var processor = CreateProcessor(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new CastDamageSpellCommand("spell-damage", PlayerId.AI, new TileCoord(0, 0)));

            Assert.That(target.CurrentHp, Is.EqualTo(2));
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain("spell-damage"));
        }

        [Test]
        public void Execute_MoveOccupantCommand_MovesOwnedOccupant()
        {
            var battleState = CreateBattleStateInMainPhase();
            var unit = CreateUnit("mover", PlayerId.Player, new TileCoord(0, 0), 2, 5);
            battleState.PlayerBoard.Place(new TileCoord(0, 0), unit);

            var processor = CreateProcessor(System.Array.Empty<CardDefinition>());

            processor.Execute(
                battleState,
                PlayerId.Player,
                new MoveOccupantCommand(new TileCoord(0, 0), new TileCoord(1, 0)));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(unit));
        }

        [Test]
        public void Execute_AttackCommand_UsesAttackService()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0), 5, 10);
            var defender = CreateUnit("defender", PlayerId.AI, new TileCoord(0, 1), 3, 15);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 1), defender);

            var processor = CreateProcessor(System.Array.Empty<CardDefinition>());

            processor.Execute(
                battleState,
                PlayerId.Player,
                new AttackCommand(new TileCoord(0, 0), new TileCoord(0, 1)));

            Assert.That(attacker.CurrentHp, Is.EqualTo(7));
            Assert.That(defender.CurrentHp, Is.EqualTo(10));
        }

        [Test]
        public void Execute_EndTurnCommand_AdvancesToNextPlayersTurnStart()
        {
            var battleState = CreateBattleStateInMainPhase();
            var processor = CreateProcessor(System.Array.Empty<CardDefinition>());

            processor.Execute(
                battleState,
                PlayerId.Player,
                new EndTurnCommand());

            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(battleState.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void Execute_ReplicateUnit_CreatesTemporaryCopyAfterSuccessfulPlay()
        {
            var battleState = CreateBattleStateInMainPhase();
            var original = battleState.Player.Hand.Add("replicate-unit");
            var processor = CreateProcessor(new CardDefinition[]
            {
                CreateReplicateUnit("replicate-unit", new ResourceSet()),
            });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayUnitCardCommand(
                    "replicate-unit",
                    new TileCoord(0, 0),
                    original.RuntimeId));

            var copy = battleState.Player.Hand.Cards.Single(card => card.CardId == "replicate-unit");
            Assert.That(copy.RuntimeId, Is.Not.EqualTo(original.RuntimeId));
            Assert.That(copy.IsTemporaryReplicate, Is.True);
        }

        [Test]
        public void Execute_ReplicateBuilding_CreatesTemporaryCopyAfterSuccessfulPlay()
        {
            var battleState = CreateBattleStateInMainPhase();
            var original = battleState.Player.Hand.Add("replicate-building");
            var definition = new BuildingCardDefinition(
                cardId: "replicate-building",
                displayName: "Replicate Building",
                cost: new ResourceSet(),
                canAttack: false,
                attack: 0,
                health: 10,
                hasReplicate: true);
            var processor = CreateProcessor(new CardDefinition[] { definition });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayBuildingCardCommand(
                    definition.CardId,
                    new TileCoord(0, 0),
                    original.RuntimeId));

            var copy = battleState.Player.Hand.Cards.Single(card => card.CardId == definition.CardId);
            Assert.That(copy.IsTemporaryReplicate, Is.True);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.TypeOf<BuildingState>());
        }

        [Test]
        public void Execute_RejectedReplicatePlay_DoesNotCreateTemporaryCopy()
        {
            var battleState = CreateBattleStateInMainPhase();
            var original = battleState.Player.Hand.Add("replicate-unit");
            var processor = CreateProcessor(new CardDefinition[]
            {
                CreateReplicateUnit("replicate-unit", new ResourceSet()),
            });

            Assert.Throws<System.InvalidOperationException>(() =>
                processor.Execute(
                    battleState,
                    PlayerId.Player,
                    new PlayUnitCardCommand(
                        "replicate-unit",
                        new TileCoord(2, 1),
                        original.RuntimeId)));

            Assert.That(battleState.Player.Hand.Contains(original.CardId, original.RuntimeId), Is.True);
            Assert.That(
                battleState.Player.Hand.Cards.Any(card => card.IsTemporaryReplicate),
                Is.False);
        }

        [Test]
        public void Execute_ZeroCostReplicateSpell_CanChainAndKeepsOriginalDefinitionCost()
        {
            var battleState = CreateBattleStateInMainPhase();
            var original = battleState.Player.Hand.Add("replicate-spell");
            var firstTarget = CreateUnit("target-1", PlayerId.AI, new TileCoord(0, 0), 0, 5);
            var secondTarget = CreateUnit("target-2", PlayerId.AI, new TileCoord(1, 0), 0, 5);
            battleState.AIBoard.Place(firstTarget.Position, firstTarget);
            battleState.AIBoard.Place(secondTarget.Position, secondTarget);

            var processor = CreateProcessor(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "replicate-spell",
                    displayName: "Replicate Spell",
                    cost: new ResourceSet(),
                    damage: 1,
                    hasReplicate: true),
            });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new CastDamageSpellCommand(
                    "replicate-spell",
                    PlayerId.AI,
                    firstTarget.Position,
                    original.RuntimeId));
            var firstCopy = battleState.Player.Hand.Cards.Single(card => card.CardId == "replicate-spell");

            processor.Execute(
                battleState,
                PlayerId.Player,
                new CastDamageSpellCommand(
                    "replicate-spell",
                    PlayerId.AI,
                    secondTarget.Position,
                    firstCopy.RuntimeId));

            var secondCopy = battleState.Player.Hand.Cards.Single(card => card.CardId == "replicate-spell");
            Assert.That(firstTarget.CurrentHp, Is.EqualTo(4));
            Assert.That(secondTarget.CurrentHp, Is.EqualTo(4));
            Assert.That(secondCopy.RuntimeId, Is.Not.EqualTo(firstCopy.RuntimeId));
            Assert.That(secondCopy.IsTemporaryReplicate, Is.True);
        }

        [Test]
        public void Execute_ReplicateWithRuntimeId_RemovesSelectedOriginalAndPreservesOtherCopy()
        {
            var battleState = CreateBattleStateInMainPhase();
            var selectedOriginal = battleState.Player.Hand.Add("replicate-unit");
            var otherOriginal = battleState.Player.Hand.Add("replicate-unit");
            var existingTemporary = battleState.Player.Hand.AddTemporaryReplicate("replicate-unit");
            var processor = CreateProcessor(new CardDefinition[]
            {
                CreateReplicateUnit("replicate-unit", new ResourceSet()),
            });

            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayUnitCardCommand(
                    "replicate-unit",
                    new TileCoord(0, 0),
                    selectedOriginal.RuntimeId));

            Assert.That(battleState.Player.Hand.Contains("replicate-unit", selectedOriginal.RuntimeId), Is.False);
            Assert.That(battleState.Player.Hand.Contains("replicate-unit", otherOriginal.RuntimeId), Is.True);
            Assert.That(battleState.Player.Hand.Contains("replicate-unit", existingTemporary.RuntimeId), Is.True);
            Assert.That(
                battleState.Player.Hand.Cards.Count(card =>
                    card.CardId == "replicate-unit" && card.IsTemporaryReplicate),
                Is.EqualTo(2));
        }

        [Test]
        public void Execute_ReplicateCopy_UsesOwnersAccountUpgradeLevelAgain()
        {
            var battleState = CreateBattleStateInMainPhase();
            var original = battleState.Player.Hand.Add("leveled-replicate");
            var definition = new UnitCardDefinition(
                cardId: "leveled-replicate",
                displayName: "Leveled Replicate",
                cost: new ResourceSet(),
                attackType: AttackType.Melee,
                attack: 10,
                health: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasReplicate: true);
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { definition });
            var processor = new BattleCommandProcessor(
                new PlayCardService(provider, new SummonService(), new FixedUpgradeLevelProvider(5)),
                new SpellService(provider, new FixedUpgradeLevelProvider(5)),
                new MoveService(),
                new AttackService(),
                new EndTurnService());

            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayUnitCardCommand(
                    definition.CardId,
                    new TileCoord(0, 0),
                    original.RuntimeId));
            var copy = battleState.Player.Hand.Cards.Single(card => card.CardId == definition.CardId);
            processor.Execute(
                battleState,
                PlayerId.Player,
                new PlayUnitCardCommand(
                    definition.CardId,
                    new TileCoord(1, 0),
                    copy.RuntimeId));

            var originalUnit = battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0));
            var copiedUnit = battleState.PlayerBoard.GetOccupant(new TileCoord(1, 0));
            Assert.That(originalUnit.Attack, Is.EqualTo(11));
            Assert.That(originalUnit.MaxHp, Is.EqualTo(24));
            Assert.That(copiedUnit.Attack, Is.EqualTo(originalUnit.Attack));
            Assert.That(copiedUnit.MaxHp, Is.EqualTo(originalUnit.MaxHp));
        }

        [Test]
        public void Execute_EndTurn_RemovesOnlyTemporaryReplicateCards()
        {
            var battleState = CreateBattleStateInMainPhase();
            var permanent = battleState.Player.Hand.Add("replicate-unit");
            var expectedPermanentHandCount = battleState.Player.Hand.Count;
            battleState.Player.Hand.AddTemporaryReplicate("replicate-unit");
            battleState.Player.Hand.AddTemporaryReplicate("another-replicate");
            var processor = CreateProcessor(System.Array.Empty<CardDefinition>());

            processor.Execute(battleState, PlayerId.Player, new EndTurnCommand());

            Assert.That(battleState.Player.Hand.Cards, Has.Count.EqualTo(expectedPermanentHandCount));
            Assert.That(battleState.Player.Hand.Contains("replicate-unit", permanent.RuntimeId), Is.True);
            Assert.That(battleState.Player.Hand.Cards.All(card => !card.IsTemporaryReplicate), Is.True);
        }

        [Test]
        public void ReplicateService_WhenHandIsFull_DoesNotCreateCopy()
        {
            var battleState = CreateBattleStateInMainPhase();
            while (battleState.Player.Hand.Count < battleState.Player.MaxHandSize)
            {
                battleState.Player.Hand.Add($"filler-{battleState.Player.Hand.Count}");
            }

            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                CreateReplicateUnit("replicate-unit", new ResourceSet()),
            });
            var service = new ReplicateService(provider);

            var result = service.TryCreateTemporaryCopy(
                battleState,
                PlayerId.Player,
                "replicate-unit");

            Assert.That(result, Is.Null);
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(battleState.Player.MaxHandSize));
        }

        [Test]
        public void Execute_WhenActorIsNotActivePlayer_Throws()
        {
            var battleState = CreateBattleStateInMainPhase();
            var processor = CreateProcessor(System.Array.Empty<CardDefinition>());

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => processor.Execute(
                    battleState,
                    PlayerId.AI,
                    new EndTurnCommand()));

            Assert.That(exception!.Message, Is.EqualTo("Only the active player can execute commands."));
        }

        private static BattleCommandProcessor CreateProcessor(IEnumerable<CardDefinition> definitions)
        {
            var provider = new InMemoryCardDefinitionProvider(definitions);
            return new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());
        }

        private static UnitCardDefinition CreateReplicateUnit(string cardId, ResourceSet cost)
        {
            return new UnitCardDefinition(
                cardId: cardId,
                displayName: "Replicate Unit",
                cost: cost,
                attackType: AttackType.Melee,
                attack: 1,
                health: 1,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasReplicate: true);
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            int attack,
            int maxHp)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: AttackType.Melee,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var turnStartService = new TurnStartService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

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

        private sealed class FixedUpgradeLevelProvider : ICardUpgradeLevelProvider
        {
            private readonly int _level;

            public FixedUpgradeLevelProvider(int level)
            {
                _level = level;
            }

            public int GetUpgradeLevel(PlayerId ownerId, string cardId)
            {
                return _level;
            }
        }
    }
}
