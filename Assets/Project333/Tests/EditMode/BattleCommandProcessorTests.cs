using System.Collections.Generic;
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
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(7));
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
    }
}
