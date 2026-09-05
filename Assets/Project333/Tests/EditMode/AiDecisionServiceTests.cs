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
    public sealed class AiDecisionServiceTests
    {
        [Test]
        public void GetNextCommand_WhenMasterAttackCanKillEnemyMaster_PrefersMasterAttack()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.Player.Master.CurrentHp = 3;

            var decisionService = new AiDecisionService(new InMemoryCardDefinitionProvider(new CardDefinition[0]));

            var command = decisionService.GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<AttackCommand>());
            var attackCommand = (AttackCommand)command;
            Assert.That(attackCommand.AttackerCoord, Is.EqualTo(new TileCoord(2, 1)));
            Assert.That(attackCommand.TargetCoord, Is.EqualTo(new TileCoord(2, 1)));
        }

        [Test]
        public void GetNextCommand_WhenNoAttackAvailable_PlaysResourceCard()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.RemainingAttacksThisTurn = 0;
            ClearHand(battleState.AI);
            battleState.AI.Hand.Add("sample_gold_miner");

            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new UnitCardDefinition(
                    "sample_gold_miner",
                    "Sample Gold Miner",
                    new ResourceSet(0, 0, 0, 2),
                    AttackType.Melee,
                    1,
                    4,
                    true,
                    false,
                    0,
                    new ResourceSet(0, 0, 0, 1))
            });

            var decisionService = new AiDecisionService(provider);

            var command = decisionService.GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            var playUnitCardCommand = (PlayUnitCardCommand)command;
            Assert.That(playUnitCardCommand.CardId, Is.EqualTo("sample_gold_miner"));
            Assert.That(playUnitCardCommand.TargetCoord, Is.EqualTo(new TileCoord(2, 0)));
        }

        [Test]
        public void GetNextCommand_WhenMultiHitAttackCanKill_PrefersAttack()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.RemainingAttacksThisTurn = 0;

            var attacker = new UnitState(
                runtimeId: "ai-double-hit",
                cardId: "ai-double-hit",
                ownerId: PlayerId.AI,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 10,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: 2);
            attacker.HasSummoningSickness = false;
            battleState.AIBoard.Place(new TileCoord(0, 0), attacker);

            var defender = new UnitState(
                runtimeId: "player-miner",
                cardId: "player-miner",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 6,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(0, 0, 0, 1));
            defender.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), defender);

            var decisionService = new AiDecisionService(new InMemoryCardDefinitionProvider(new CardDefinition[0]));

            var command = decisionService.GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<AttackCommand>());
            var attackCommand = (AttackCommand)command;
            Assert.That(attackCommand.AttackerCoord, Is.EqualTo(new TileCoord(0, 0)));
            Assert.That(attackCommand.TargetCoord, Is.EqualTo(new TileCoord(0, 0)));
        }

        [Test]
        public void GetNextCommand_WhenTargetHasUnusedEndure_DoesNotTreatAttackAsKill()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.RemainingAttacksThisTurn = 0;

            var attacker = new UnitState(
                runtimeId: "ai-attacker",
                cardId: "ai-attacker",
                ownerId: PlayerId.AI,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 10,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
            attacker.HasSummoningSickness = false;
            battleState.AIBoard.Place(new TileCoord(0, 0), attacker);

            var defender = new UnitState(
                runtimeId: "player-endure",
                cardId: "player-endure",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(0, 0, 0, 1),
                hasEndure: true);
            defender.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), defender);

            var decisionService = new AiDecisionService(new InMemoryCardDefinitionProvider(new CardDefinition[0]));

            var command = decisionService.GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<AttackCommand>());
            var attackCommand = (AttackCommand)command;
            Assert.That(attackCommand.TargetCoord, Is.EqualTo(new TileCoord(2, 1)));
        }

        [Test]
        public void GetNextCommand_WhenDamageSpellWouldTriggerEndure_DoesNotTreatSpellAsKill()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.RemainingAttacksThisTurn = 0;
            ClearHand(battleState.AI);
            battleState.AI.Hand.Add("lethal-spell");

            var defender = new UnitState(
                runtimeId: "player-endure",
                cardId: "player-endure",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasEndure: true);
            battleState.PlayerBoard.Place(defender.Position, defender);

            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    "lethal-spell",
                    "Lethal Spell",
                    new ResourceSet(),
                    damage: 3,
                    damageType: DamageType.Magic),
            });

            var command = new AiDecisionService(provider).GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void GetNextCommand_WhenBackRowTargetIsProtectedByShielder_SkipsProtectedTarget()
        {
            var battleState = CreateBattleState();
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.RemainingAttacksThisTurn = 0;

            var attacker = new UnitState(
                runtimeId: "ai-archer",
                cardId: "ai-archer",
                ownerId: PlayerId.AI,
                position: new TileCoord(0, 0),
                attackType: AttackType.Ranged,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
            attacker.HasSummoningSickness = false;
            battleState.AIBoard.Place(new TileCoord(0, 0), attacker);

            var shielder = new UnitState(
                runtimeId: "player-shielder",
                cardId: "player-shielder",
                ownerId: PlayerId.Player,
                position: new TileCoord(2, 0),
                attackType: AttackType.Melee,
                attack: 0,
                maxHp: 2,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasShielder: true);
            shielder.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(new TileCoord(2, 0), shielder);
            battleState.Player.Master.CurrentHp = 1;

            var decisionService = new AiDecisionService(new InMemoryCardDefinitionProvider(new CardDefinition[0]));

            var command = decisionService.GetNextCommand(battleState);

            Assert.That(command, Is.TypeOf<AttackCommand>());
            var attackCommand = (AttackCommand)command;
            Assert.That(attackCommand.AttackerCoord, Is.EqualTo(new TileCoord(0, 0)));
            Assert.That(attackCommand.TargetCoord, Is.EqualTo(new TileCoord(2, 0)));
        }

        private static void ClearHand(PlayerState playerState)
        {
            foreach (var cardId in new List<string>(playerState.Hand.CardIds))
            {
                playerState.Hand.Remove(cardId);
            }
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            return setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
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
