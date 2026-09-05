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
    public sealed class DamageTypeResolutionTests
    {
        [Test]
        public void ResolveIncomingDamage_AppliesMatchingDefenseAndFixedIgnoresDefense()
        {
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Physical,
                    isDrained: false,
                    physicalDefense: 3,
                    magicDefense: 2),
                Is.EqualTo(2));
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Magic,
                    isDrained: false,
                    physicalDefense: 3,
                    magicDefense: 2),
                Is.EqualTo(3));
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Fixed,
                    isDrained: false,
                    physicalDefense: 3,
                    magicDefense: 2),
                Is.EqualTo(5));
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 2,
                    damageType: DamageType.Physical,
                    isDrained: false,
                    physicalDefense: 3,
                    magicDefense: 0),
                Is.Zero);
        }

        [Test]
        public void ResolveIncomingDamage_DrainedIgnoresDefenseAndTriplesAllDamageTypes()
        {
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Physical,
                    isDrained: true,
                    physicalDefense: 99,
                    magicDefense: 99),
                Is.EqualTo(15));
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Magic,
                    isDrained: true,
                    physicalDefense: 99,
                    magicDefense: 99),
                Is.EqualTo(15));
            Assert.That(
                DamageResolutionRules.ResolveIncomingDamage(
                    damage: 5,
                    damageType: DamageType.Fixed,
                    isDrained: true,
                    physicalDefense: 99,
                    magicDefense: 99),
                Is.EqualTo(15));
        }

        [TestCase(DamageType.Physical)]
        [TestCase(DamageType.Magic)]
        [TestCase(DamageType.Fixed)]
        public void ApplyEffectDamage_FirstLethalDamageOfAnyTypeTriggersEndure(DamageType damageType)
        {
            var target = CreateUnit(
                "endure-target",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 5,
                damageType: DamageType.Physical,
                hasEndure: true);

            var firstDamage = DamageResolutionRules.ApplyEffectDamage(target, 10, damageType);

            Assert.That(firstDamage, Is.EqualTo(4));
            Assert.That(target.CurrentHp, Is.EqualTo(1));
            Assert.That(target.EndureUsed, Is.True);

            var secondDamage = DamageResolutionRules.ApplyEffectDamage(target, 10, damageType);

            Assert.That(secondDamage, Is.EqualTo(1));
            Assert.That(target.CurrentHp, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void Attack_MultiHitAppliesDefenseToEveryHit()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 20,
                damageType: DamageType.Physical,
                hitsPerAttack: 3);
            var defender = CreateUnit(
                "defender",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 20,
                damageType: DamageType.Physical,
                physicalDefense: 3);
            attacker.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(defender.Position, defender);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                defender.Position);

            Assert.That(defender.CurrentHp, Is.EqualTo(14));
        }

        [Test]
        public void Attack_CounterattackUsesCounterattackerDamageType()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 20,
                damageType: DamageType.Physical,
                physicalDefense: 99,
                magicDefense: 2);
            var defender = CreateUnit(
                "defender",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 5,
                maxHp: 20,
                damageType: DamageType.Magic);
            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(defender.Position, defender);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                defender.Position);

            Assert.That(attacker.CurrentHp, Is.EqualTo(17));
            Assert.That(defender.CurrentHp, Is.EqualTo(19));
        }

        [Test]
        public void Attack_ShielderAndProtectedTargetApplyDefenseInSequence()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 10,
                maxHp: 20,
                damageType: DamageType.Physical);
            var shielder = CreateUnit(
                "shielder",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 0,
                maxHp: 5,
                damageType: DamageType.Physical,
                physicalDefense: 3,
                hasShielder: true);
            var protectedTarget = CreateUnit(
                "protected",
                PlayerId.AI,
                new TileCoord(0, 1),
                AttackType.Melee,
                attack: 1,
                maxHp: 10,
                damageType: DamageType.Physical,
                physicalDefense: 1);
            attacker.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(shielder.Position, shielder);
            battleState.AIBoard.Place(protectedTarget.Position, protectedTarget);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                protectedTarget.Position);

            Assert.That(battleState.AIBoard.GetOccupant(shielder.Position), Is.Null);
            Assert.That(protectedTarget.CurrentHp, Is.EqualTo(9));
        }

        [Test]
        public void Attack_LifeStealHealsOnlyActualHpLostAfterDefense()
        {
            var battleState = CreateBattleState();
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 5,
                maxHp: 20,
                damageType: DamageType.Physical,
                hasLifeSteal: true);
            var defender = CreateUnit(
                "defender",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 10,
                damageType: DamageType.Physical,
                physicalDefense: 3);
            attacker.HasSummoningSickness = false;
            attacker.CurrentHp = 5;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(defender.Position, defender);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                defender.Position);

            Assert.That(defender.CurrentHp, Is.EqualTo(8));
            Assert.That(attacker.CurrentHp, Is.EqualTo(7));
        }

        [Test]
        public void CastDamageSpell_UsesDefinitionMagicDamageType()
        {
            var battleState = CreateBattleState();
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    "firebolt",
                    "Firebolt",
                    new ResourceSet(),
                    damage: 10,
                    damageType: DamageType.Magic),
            });
            var target = CreateUnit(
                "target",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 20,
                damageType: DamageType.Physical,
                physicalDefense: 99,
                magicDefense: 3);
            battleState.Player.Hand.Add("firebolt");
            battleState.AIBoard.Place(target.Position, target);

            new SpellService(provider).CastDamageSpell(
                battleState,
                PlayerId.Player,
                "firebolt",
                PlayerId.AI,
                target.Position);

            Assert.That(target.CurrentHp, Is.EqualTo(13));
        }

        [Test]
        public void EndTurn_RedDragonEffectUsesMagicDamageType()
        {
            var battleState = CreateBattleState();
            var redDragon = CreateUnit(
                "RedDragon",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 50,
                maxHp: 50,
                damageType: DamageType.Physical);
            var target = CreateUnit(
                "target",
                PlayerId.AI,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 100,
                damageType: DamageType.Physical,
                physicalDefense: 60,
                magicDefense: 6);
            redDragon.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(redDragon.Position, redDragon);
            battleState.AIBoard.Place(target.Position, target);

            new EndTurnService().EndTurn(battleState);

            Assert.That(target.CurrentHp, Is.EqualTo(73));
        }

        [Test]
        public void ResolveTurnStart_DeckExhaustionDamageIgnoresMasterDefenses()
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();
            var playerMaster = new MasterState(
                "player-master",
                PlayerId.Player,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333,
                physicalDefense: 99,
                magicDefense: 99);
            var aiMaster = new MasterState(
                "ai-master",
                PlayerId.AI,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333);
            var player = new PlayerState(
                PlayerId.Player,
                new ResourceSet(),
                new DeckState(new string[0]),
                new HandState(),
                new DiscardState(),
                playerMaster);
            var ai = new PlayerState(
                PlayerId.AI,
                new ResourceSet(),
                new DeckState(new[] { "ai-card" }),
                new HandState(),
                new DiscardState(),
                aiMaster);
            playerBoard.Place(playerMaster.Position, playerMaster);
            aiBoard.Place(aiMaster.Position, aiMaster);
            var battleState = new BattleState(player, ai, playerBoard, aiBoard);
            battleState.StartNextTurn(PlayerId.Player);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(playerMaster.CurrentHp, Is.EqualTo(330));
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp,
            DamageType damageType,
            int physicalDefense = 0,
            int magicDefense = 0,
            int hitsPerAttack = 1,
            bool hasShielder = false,
            bool hasLifeSteal = false,
            bool hasEndure = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: hitsPerAttack,
                hasEndure: hasEndure,
                hasShielder: hasShielder,
                hasLifeSteal: hasLifeSteal,
                damageType: damageType,
                physicalDefense: physicalDefense,
                magicDefense: magicDefense);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new[]
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
