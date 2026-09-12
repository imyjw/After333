using System;
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
    public sealed class AiDestructionPlanningTests
    {
        private static readonly TileCoord AttackerTile = new TileCoord(2, 0);
        private static readonly TileCoord PlantTile = new TileCoord(4, 0);
        private static readonly TileCoord BlockerTile = new TileCoord(2, 0);

        [Test]
        public void Attack_ExplodesEnemyPlantToDefeatMasterBehindFrontRowBlocker()
        {
            var state = State();
            var attacker = PlaceAttacker(state, AttackType.Melee);
            PlaceBlocker(state);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            state.Player.Master.CurrentHp = 20;
            Assert.That(new TargetingService().CanTarget(state, PlayerId.AI, attacker.Position,
                state.Player.Master.Position), Is.False);
            var provider = Provider();
            var before = AiBattleStateCopy.PositionKey(state);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(command, Is.TypeOf<AttackCommand>());
            Assert.That(((AttackCommand)command).TargetCoord, Is.EqualTo(PlantTile));
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.AIBoard.GetOccupant(AttackerTile).CurrentHp,
                Is.EqualTo(1000 - NuclearPowerPlantRules.DestructionDamage));
            Assert.That(state.PlayerBoard.GetOccupant(BlockerTile), Is.Not.Null,
                "The existing explosion, rather than removal of the front-row blocker, supplies lethal damage.");
        }

        [Test]
        public void DamageSpell_CanDestroyOwnPlantWhenItsExplosionWinsTheBattle()
        {
            var state = State();
            PlaceBlocker(state);
            PlacePlant(state, PlayerId.AI, PlantTile, hp: 5);
            state.Player.Master.CurrentHp = 20;
            var provider = GiveDamageSpell(state, 5);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<CastDamageSpellCommand>());
            var spell = (CastDamageSpellCommand)command;
            Assert.That(spell.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(spell.TargetCoord, Is.EqualTo(PlantTile));
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.AIBoard.GetOccupant(PlantTile), Is.Null);
        }

        [Test]
        public void Attack_AccountsForChainedPlantExplosionsBeforeChoosingLethal()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Melee);
            PlaceBlocker(state);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            var secondPlant = new TileCoord(0, 0);
            PlacePlant(state, PlayerId.Player, secondPlant, hp: NuclearPowerPlantRules.BaseHealth);
            state.Player.Master.CurrentHp = NuclearPowerPlantRules.DestructionDamage + 10;
            var provider = Provider();

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<AttackCommand>());
            Assert.That(((AttackCommand)command).TargetCoord, Is.EqualTo(PlantTile));
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.PlayerBoard.GetOccupant(secondPlant), Is.Null);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333 - 2 * NuclearPowerPlantRules.DestructionDamage));
        }

        [Test]
        public void Planning_PrefersSafeDirectVictoryOverExplosionThatLosesOwnMaster()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Melee);
            PlaceBlocker(state);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            state.Player.Master.CurrentHp = 20;
            state.AI.Master.CurrentHp = 10;
            var provider = GiveDamageSpell(state, 20);

            var command = Planner(provider).GetNextCommand(state);

            AssertDirectMasterSpell(command, state.Player.Master.Position);
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(10));
            Assert.That(state.PlayerBoard.GetOccupant(PlantTile), Is.Not.Null);
        }

        [Test]
        public void Planning_DoesNotAssumeErasedPlantWillExplode()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Melee);
            PlaceBlocker(state);
            var plant = PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            plant.ApplyErasure();
            state.Player.Master.CurrentHp = 20;
            var provider = GiveDamageSpell(state, 20);

            var command = Planner(provider).GetNextCommand(state);

            AssertDirectMasterSpell(command, state.Player.Master.Position);
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.PlayerBoard.GetOccupant(PlantTile), Is.SameAs(plant));
        }

        [Test]
        public void Planning_UsesMasterPhysicalDefenseWhenComparingExplosionAndMagicFinisher()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Melee);
            PlaceBlocker(state);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            state.Player.Master.CurrentHp = 20;
            state.Player.Master.IncreasePhysicalDefense(NuclearPowerPlantRules.DestructionDamage - 10);
            var provider = GiveDamageSpell(state, 20);

            var command = Planner(provider).GetNextCommand(state);

            AssertDirectMasterSpell(command, state.Player.Master.Position);
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.PlayerBoard.GetOccupant(PlantTile), Is.Not.Null);
        }

        [Test]
        public void CrowdedBoard_TightBudgetFindsWinningPlantAttackAtLastCoordinate()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Ranged);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: 5);
            FillEmptyTiles(state);
            state.Player.Master.CurrentHp = 20;
            var provider = Provider();
            var planner = Planner(provider, maxSimulations: 32);
            var before = AiBattleStateCopy.PositionKey(state);

            var command = planner.GetNextCommand(state);

            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(command, Is.TypeOf<AttackCommand>());
            Assert.That(((AttackCommand)command).TargetCoord, Is.EqualTo(PlantTile),
                "The winning explosion must be examined before ordinary chip attacks exhaust the simulation budget.");
            Assert.That(planner.LastSimulationCount, Is.LessThanOrEqualTo(32));
            Execute(state, provider, command);
            AssertAiVictory(state);
        }

        [Test]
        public void CrowdedBoard_TightBudgetFindsWinningOwnPlantSpellBeforeOrdinaryAttacks()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Ranged);
            PlacePlant(state, PlayerId.AI, PlantTile, hp: 5);
            FillEmptyTiles(state);
            state.Player.Master.CurrentHp = 20;
            var provider = GiveDamageSpell(state, 5);
            var planner = Planner(provider, maxSimulations: 32);

            var command = planner.GetNextCommand(state);

            Assert.That(command, Is.TypeOf<CastDamageSpellCommand>());
            var spell = (CastDamageSpellCommand)command;
            Assert.That(spell.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(spell.TargetCoord, Is.EqualTo(PlantTile));
            Assert.That(planner.LastSimulationCount, Is.LessThanOrEqualTo(32));
            Execute(state, provider, command);
            AssertAiVictory(state);
        }

        [Test]
        public void CrowdedBoard_TightBudgetKeepsDirectMasterFinisherAheadOfNonlethalPlantAttacks()
        {
            var state = State();
            PlaceAttacker(state, AttackType.Ranged);
            PlacePlant(state, PlayerId.Player, PlantTile, hp: NuclearPowerPlantRules.BaseHealth);
            FillEmptyTiles(state);
            state.Player.Master.CurrentHp = 20;
            var provider = GiveDamageSpell(state, 20);
            var planner = Planner(provider, maxSimulations: 32);

            var command = planner.GetNextCommand(state);

            AssertDirectMasterSpell(command, state.Player.Master.Position);
            Assert.That(planner.LastSimulationCount, Is.LessThanOrEqualTo(32));
            Execute(state, provider, command);
            AssertAiVictory(state);
            Assert.That(state.PlayerBoard.GetOccupant(PlantTile).CurrentHp,
                Is.EqualTo(NuclearPowerPlantRules.BaseHealth));
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }

        private static UnitState PlaceAttacker(BattleState state, AttackType attackType)
        {
            var attacker = new UnitState("ai-attacker", "attacker", PlayerId.AI, AttackerTile,
                attackType, attack: 5, maxHp: 1000, canMove: false, isScience: false, sciencePowerUpkeep: 0);
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            state.AIBoard.Place(AttackerTile, attacker);
            return attacker;
        }

        private static void PlaceBlocker(BattleState state)
        {
            state.PlayerBoard.Place(BlockerTile, new UnitState("player-blocker", "blocker", PlayerId.Player,
                BlockerTile, AttackType.Melee, attack: 0, maxHp: 200, canMove: false, isScience: false,
                sciencePowerUpkeep: 0));
        }

        private static BuildingState PlacePlant(BattleState state, PlayerId owner, TileCoord coord, int hp)
        {
            var plant = new BuildingState(owner + "-plant-" + coord.Column + "-" + coord.Row,
                NuclearPowerPlantRules.CardId, owner, coord, canAttack: false, attack: 0,
                maxHp: NuclearPowerPlantRules.BaseHealth,
                turnStartResourceGain: new ResourceSet(0, 0, NuclearPowerPlantRules.TurnStartPowerGain, 0),
                damageType: DamageType.None);
            plant.CurrentHp = hp;
            state.GetBoard(owner).Place(coord, plant);
            return plant;
        }

        private static void FillEmptyTiles(BattleState state)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var board = state.GetBoard(owner);
                for (var column = 0; column < BoardState.ColumnCount; column++)
                {
                    for (var row = 0; row < BoardState.RowCount; row++)
                    {
                        var coord = new TileCoord(column, row);
                        if (!board.IsEmpty(coord)) continue;
                        board.Place(coord, new UnitState(owner + "-fixed-" + column + "-" + row, "fixed", owner,
                            coord, AttackType.Melee, attack: 0, maxHp: 1000, canMove: false, isScience: false,
                            sciencePowerUpkeep: 0));
                    }
                }
            }
        }

        private static ICardDefinitionProvider Provider(params CardDefinition[] cards)
        {
            return new InMemoryCardDefinitionProvider(cards);
        }

        private static ICardDefinitionProvider GiveDamageSpell(BattleState state, int damage)
        {
            var card = new DamageSpellCardDefinition("targeted-damage", "Targeted damage", new ResourceSet(0, 0, 0, 1),
                damage, DamageType.Magic);
            state.AI.Hand.Add(card.CardId);
            state.AI.Resources.Add(card.Cost);
            return Provider(card);
        }

        private static AiDecisionService Planner(ICardDefinitionProvider provider, int maxSimulations = 16000)
        {
            return new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 3, MaxMilliseconds = 10000, MaxSimulations = maxSimulations });
        }

        private static void Execute(BattleState state, ICardDefinitionProvider provider, IBattleCommand command)
        {
            new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(),
                new AttackService(new TargetingService(), new Random(41).Next), new EndTurnService(new Random(41)))
                .Execute(state, PlayerId.AI, command);
        }

        private static void AssertDirectMasterSpell(IBattleCommand command, TileCoord masterCoord)
        {
            Assert.That(command, Is.TypeOf<CastDamageSpellCommand>());
            var spell = (CastDamageSpellCommand)command;
            Assert.That(spell.TargetOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(spell.TargetCoord, Is.EqualTo(masterCoord));
        }

        private static void AssertAiVictory(BattleState state)
        {
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.IsDraw, Is.False);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }
    }
}
