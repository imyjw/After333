using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class RobotFusionSpellTests
    {
        [Test]
        public void CanBegin_CountsTwoLivingRobotsWithTheSameCardId()
        {
            var battleState = CreateBattleState();
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateRobot("a111-first", "A-111", new TileCoord(0, 0), upkeep: 1));
            battleState.PlayerBoard.Place(
                new TileCoord(1, 0),
                CreateRobot("a111-second", "A-111", new TileCoord(1, 0), upkeep: 1));

            Assert.That(RobotFusionRules.CountLivingRobots(battleState.PlayerBoard), Is.EqualTo(2));
            Assert.That(RobotFusionRules.CanBegin(battleState, PlayerId.Player), Is.True);
        }

        [Test]
        public void CanBegin_DoesNotCountDeadRobotWithTheSameCardId()
        {
            var battleState = CreateBattleState();
            var livingRobot = CreateRobot("a111-living", "A-111", new TileCoord(0, 0), upkeep: 1);
            var deadRobot = CreateRobot("a111-dead", "A-111", new TileCoord(1, 0), upkeep: 1);
            deadRobot.CurrentHp = 0;
            battleState.PlayerBoard.Place(livingRobot.Position, livingRobot);
            battleState.PlayerBoard.Place(deadRobot.Position, deadRobot);

            Assert.That(RobotFusionRules.CountLivingRobots(battleState.PlayerBoard), Is.EqualTo(1));
            Assert.That(RobotFusionRules.CanBegin(battleState, PlayerId.Player), Is.False);
        }

        [Test]
        public void CanBegin_DoesNotCountErasureRobot()
        {
            var battleState = CreateBattleState();
            var activeRobot = CreateRobot("active", "A-111", new TileCoord(0, 0), upkeep: 1);
            var erasedRobot = CreateRobot("erased", "A-111", new TileCoord(1, 0), upkeep: 1);
            erasedRobot.ApplyErasure();
            battleState.PlayerBoard.Place(activeRobot.Position, activeRobot);
            battleState.PlayerBoard.Place(erasedRobot.Position, erasedRobot);

            Assert.That(RobotFusionRules.CountLivingRobots(battleState.PlayerBoard), Is.EqualTo(1));
            Assert.That(RobotFusionRules.CanBegin(battleState, PlayerId.Player), Is.False);
        }

        [Test]
        public void BeginRobotFusion_SpendsCostAndEntersPendingSelection()
        {
            var battleState = CreateBattleState();
            AddTwoRobots(battleState);
            AddFusionCardAndPower(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                RobotFusionRules.CardId);

            Assert.That(battleState.Player.Resources.Power, Is.Zero);
            Assert.That(battleState.Player.Hand.Contains(RobotFusionRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(RobotFusionRules.CardId));
            Assert.That(battleState.PendingRobotFusion, Is.Not.Null);
            Assert.That(battleState.PendingRobotFusion.OwnerId, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void ResolveRobotFusion_HighestUpkeepSurvivesAndReceivesCurrentStats()
        {
            var battleState = CreateBattleState();
            var absorbed = CreateRobot(
                "absorbed",
                "A-111",
                new TileCoord(0, 0),
                upkeep: 1,
                attack: 10,
                maxHp: 20);
            absorbed.CurrentHp = 12;
            var survivor = CreateRobot(
                "survivor",
                "A-111",
                new TileCoord(1, 0),
                upkeep: 3,
                attack: 5,
                maxHp: 15,
                damageType: DamageType.Magic);
            survivor.CurrentHp = 10;
            survivor.IsDrained = true;
            battleState.PlayerBoard.Place(absorbed.Position, absorbed);
            battleState.PlayerBoard.Place(survivor.Position, survivor);
            AddFusionCardAndPower(battleState);
            var spellService = CreateSpellService();
            spellService.CastScriptedSpell(battleState, PlayerId.Player, RobotFusionRules.CardId);

            var result = spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                RobotFusionRules.CardId,
                new[] { absorbed.Position, survivor.Position });

            Assert.That(result.SurvivorRuntimeId, Is.EqualTo("survivor"));
            Assert.That(result.AttackBonus, Is.EqualTo(10));
            Assert.That(result.HpBonus, Is.EqualTo(12));
            Assert.That(survivor.BaseAttack, Is.EqualTo(15));
            Assert.That(survivor.MaxHp, Is.EqualTo(27));
            Assert.That(survivor.CurrentHp, Is.EqualTo(22));
            Assert.That(survivor.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(survivor.SciencePowerUpkeep, Is.EqualTo(3));
            Assert.That(survivor.IsDrained, Is.True);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(survivor));
            Assert.That(battleState.PendingRobotFusion, Is.Null);
        }

        [Test]
        public void ResolveRobotFusion_WhenUpkeepTies_EarlierSelectionSurvives()
        {
            var battleState = CreateBattleState();
            var firstBoardRobot = CreateRobot("first-board", "A-111", new TileCoord(0, 0), upkeep: 1);
            var firstSelectedRobot = CreateRobot("first-selected", "A-111", new TileCoord(1, 0), upkeep: 1);
            battleState.PlayerBoard.Place(firstBoardRobot.Position, firstBoardRobot);
            battleState.PlayerBoard.Place(firstSelectedRobot.Position, firstSelectedRobot);
            AddFusionCardAndPower(battleState);
            var spellService = CreateSpellService();
            spellService.CastScriptedSpell(battleState, PlayerId.Player, RobotFusionRules.CardId);

            var result = spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                RobotFusionRules.CardId,
                new[] { firstSelectedRobot.Position, firstBoardRobot.Position });

            Assert.That(result.SurvivorRuntimeId, Is.EqualTo("first-selected"));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(firstSelectedRobot));
        }

        [Test]
        public void BeginRobotFusion_WithOnlyOneLivingRobot_IsRejectedWithoutPaying()
        {
            var battleState = CreateBattleState();
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateRobot("only-robot", "A-111", new TileCoord(0, 0), upkeep: 1));
            AddFusionCardAndPower(battleState);

            Assert.Throws<InvalidOperationException>(() =>
                CreateSpellService().CastScriptedSpell(
                    battleState,
                    PlayerId.Player,
                    RobotFusionRules.CardId));
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(3));
            Assert.That(battleState.Player.Hand.Contains(RobotFusionRules.CardId), Is.True);
            Assert.That(battleState.PendingRobotFusion, Is.Null);
        }

        private static void AddTwoRobots(BattleState battleState)
        {
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateRobot("robot-1", "A-111", new TileCoord(0, 0), upkeep: 1));
            battleState.PlayerBoard.Place(
                new TileCoord(1, 0),
                CreateRobot("robot-2", "A-111", new TileCoord(1, 0), upkeep: 1));
        }

        private static void AddFusionCardAndPower(BattleState battleState)
        {
            battleState.Player.Hand.Add(RobotFusionRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 3, gold: 0));
        }

        private static SpellService CreateSpellService()
        {
            return new SpellService(new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    RobotFusionRules.CardId,
                    "로봇 합체",
                    new ResourceSet(mana: 0, qi: 0, power: 3, gold: 0),
                    RobotFusionRules.EffectId),
            }));
        }

        private static UnitState CreateRobot(
            string runtimeId,
            string cardId,
            TileCoord coord,
            int upkeep,
            int attack = 13,
            int maxHp = 13,
            DamageType damageType = DamageType.Physical)
        {
            return new UnitState(
                runtimeId,
                cardId,
                PlayerId.Player,
                coord,
                AttackType.Melee,
                attack,
                maxHp,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: upkeep,
                damageType: damageType,
                hasRobot: true);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(
                CreateDeck("player"),
                CreateDeck("ai"),
                PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var index = 0; index < 10; index += 1)
            {
                cards.Add($"{prefix}-{index}");
            }

            return cards;
        }
    }
}
