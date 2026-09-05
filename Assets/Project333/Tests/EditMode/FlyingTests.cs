using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class FlyingTests
    {
        [Test]
        public void PlayUnitCard_WithFlyingDefinition_EntersFieldWithActiveFlying()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("flying-unit");
            var definition = new UnitCardDefinition(
                cardId: "flying-unit",
                displayName: "Flying Unit",
                cost: new ResourceSet(),
                attackType: AttackType.Melee,
                attack: 7,
                health: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasFlying: true);
            var service = new PlayCardService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));

            var unit = service.PlayUnitCard(
                battleState,
                PlayerId.Player,
                definition.CardId,
                new TileCoord(0, 0));

            Assert.That(unit.HasFlying, Is.True);
            Assert.That(unit.HasActiveFlying, Is.True);
            Assert.That(unit.DoesNotBlockFrontRow, Is.True);
        }

        [Test]
        public void Targeting_NonFlyingMeleeCannotTargetFlyingButRangedAndFlyingMeleeCan()
        {
            var battleState = CreateBattleStateInMainPhase();
            var flyingFront = CreateUnit(
                "flying-front",
                PlayerId.AI,
                new TileCoord(0, 0),
                hasFlying: true);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            battleState.AIBoard.Place(flyingFront.Position, flyingFront);
            battleState.AIBoard.Place(back.Position, back);

            var melee = CreateUnit("melee", PlayerId.Player, new TileCoord(0, 0));
            battleState.PlayerBoard.Place(melee.Position, melee);
            var meleeTargets = GetTargets(battleState, melee);

            Assert.That(meleeTargets, Has.No.Member(flyingFront.Position));
            Assert.That(meleeTargets, Does.Contain(back.Position),
                "Active Flying does not block its back row.");

            battleState.PlayerBoard.Remove(melee.Position);
            var ranged = CreateUnit(
                "ranged",
                PlayerId.Player,
                new TileCoord(0, 0),
                attackType: AttackType.Ranged);
            battleState.PlayerBoard.Place(ranged.Position, ranged);
            Assert.That(GetTargets(battleState, ranged), Does.Contain(flyingFront.Position));

            battleState.PlayerBoard.Remove(ranged.Position);
            var flyingMelee = CreateUnit(
                "flying-melee",
                PlayerId.Player,
                new TileCoord(0, 0),
                hasFlying: true);
            battleState.PlayerBoard.Place(flyingMelee.Position, flyingMelee);
            Assert.That(GetTargets(battleState, flyingMelee), Does.Contain(flyingFront.Position));
        }

        [Test]
        public void Attack_FlyingMeleeBypassesFrontRowAndReceivesMeleeCounterattack()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit(
                "flying-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                attack: 10,
                maxHp: 30,
                hasFlying: true);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(0, 0));
            var back = CreateUnit(
                "back",
                PlayerId.AI,
                new TileCoord(0, 1),
                attack: 4,
                maxHp: 30);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(front.Position, front);
            battleState.AIBoard.Place(back.Position, back);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                back.Position);

            Assert.That(front.CurrentHp, Is.EqualTo(20));
            Assert.That(back.CurrentHp, Is.EqualTo(20));
            Assert.That(attacker.CurrentHp, Is.EqualTo(26));
        }

        [Test]
        public void Attack_FlyingMeleeAgainstShielderProtectedBack_RedirectsAndShielderCounterattacks()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit(
                "flying-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                attack: 10,
                maxHp: 30,
                hasFlying: true);
            var shielder = CreateUnit(
                "shielder",
                PlayerId.AI,
                new TileCoord(0, 0),
                attack: 4,
                maxHp: 6,
                hasShielder: true);
            var back = CreateUnit(
                "back",
                PlayerId.AI,
                new TileCoord(0, 1),
                attackType: AttackType.Ranged,
                maxHp: 30);
            PrepareAttacker(attacker);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(shielder.Position, shielder);
            battleState.AIBoard.Place(back.Position, back);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                back.Position);

            Assert.That(battleState.AIBoard.GetOccupant(shielder.Position), Is.Null);
            Assert.That(back.CurrentHp, Is.EqualTo(26),
                "Only damage beyond the Shielder's pre-hit HP reaches the protected target.");
            Assert.That(attacker.CurrentHp, Is.EqualTo(26),
                "The melee Shielder is the actual counterattacker.");
        }

        [Test]
        public void Shielder_FlyingShielderRejectsOnlyNonFlyingMeleeNormalAttack()
        {
            var board = new BoardState();
            var flyingShielder = CreateUnit(
                "flying-shielder",
                PlayerId.AI,
                new TileCoord(0, 0),
                hasShielder: true,
                hasFlying: true);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            board.Place(flyingShielder.Position, flyingShielder);
            board.Place(back.Position, back);

            Assert.That(
                ShielderService.ResolveForNormalAttack(
                    board,
                    back.Position,
                    AttackType.Melee,
                    attackerHasActiveFlying: false).IsProtected,
                Is.False);
            Assert.That(
                ShielderService.ResolveForNormalAttack(
                    board,
                    back.Position,
                    AttackType.Ranged,
                    attackerHasActiveFlying: false).IsProtected,
                Is.True);
            Assert.That(
                ShielderService.ResolveForNormalAttack(
                    board,
                    back.Position,
                    AttackType.Melee,
                    attackerHasActiveFlying: true).IsProtected,
                Is.True);
            Assert.That(ShielderService.Resolve(board, back.Position).IsProtected, Is.True,
                "Shielder-eligible spell and effect resolution is not restricted by Flying.");

            var secondBoard = new BoardState();
            var groundShielder = CreateUnit(
                "ground-shielder",
                PlayerId.AI,
                new TileCoord(1, 0),
                hasShielder: true);
            var flyingBack = CreateUnit(
                "flying-back",
                PlayerId.AI,
                new TileCoord(1, 1),
                hasFlying: true);
            secondBoard.Place(groundShielder.Position, groundShielder);
            secondBoard.Place(flyingBack.Position, flyingBack);
            Assert.That(
                ShielderService.ResolveForNormalAttack(
                    secondBoard,
                    flyingBack.Position,
                    AttackType.Ranged,
                    attackerHasActiveFlying: false).IsProtected,
                Is.True);
        }

        [Test]
        public void DrainedErasureSealboundAndHiding_ApplyLockedFlyingPriority()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit("attacker", PlayerId.Player, new TileCoord(0, 0));
            var flyingFront = CreateUnit(
                "flying-front",
                PlayerId.AI,
                new TileCoord(0, 0),
                hasFlying: true);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(0, 1));
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(flyingFront.Position, flyingFront);
            battleState.AIBoard.Place(back.Position, back);

            Assert.That(GetTargets(battleState, attacker), Has.No.Member(flyingFront.Position));

            flyingFront.IsDrained = true;
            Assert.That(flyingFront.HasActiveFlying, Is.False);
            Assert.That(GetTargets(battleState, attacker), Does.Contain(flyingFront.Position));
            Assert.That(GetTargets(battleState, attacker), Does.Contain(back.Position));

            flyingFront.IsDrained = false;
            Assert.That(flyingFront.HasActiveFlying, Is.True);
            Assert.That(GetTargets(battleState, attacker), Has.No.Member(flyingFront.Position));

            flyingFront.ApplyErasure();
            Assert.That(flyingFront.HasActiveFlying, Is.False);
            Assert.That(GetTargets(battleState, attacker), Does.Contain(flyingFront.Position));
            Assert.That(GetTargets(battleState, attacker), Has.No.Member(back.Position),
                "Erasure suppresses Flying, so the ordinary front-row blocker rule returns.");

            var sealedFlying = CreateUnit(
                "sealed-flying",
                PlayerId.AI,
                new TileCoord(1, 0),
                hasFlying: true);
            sealedFlying.EnterSealbound(1);
            battleState.AIBoard.Place(sealedFlying.Position, sealedFlying);
            Assert.That(GetTargets(battleState, attacker), Has.No.Member(sealedFlying.Position));
            sealedFlying.ResolveSealboundOwnerTurnStart();
            Assert.That(sealedFlying.HasActiveFlying, Is.True);
            Assert.That(GetTargets(battleState, attacker), Has.No.Member(sealedFlying.Position));

            var hiddenFlying = CreateUnit(
                "hidden-flying",
                PlayerId.AI,
                new TileCoord(2, 0),
                attackType: AttackType.Ranged,
                hasHiding: true,
                hasFlying: true);
            battleState.AIBoard.Place(hiddenFlying.Position, hiddenFlying);
            battleState.PlayerBoard.Remove(attacker.Position);
            var rangedAttacker = CreateUnit(
                "ranged-attacker",
                PlayerId.Player,
                attacker.Position,
                attackType: AttackType.Ranged);
            battleState.PlayerBoard.Place(rangedAttacker.Position, rangedAttacker);
            Assert.That(GetTargets(battleState, rangedAttacker), Has.No.Member(hiddenFlying.Position));
            hiddenFlying.RevealHiding();
            Assert.That(GetTargets(battleState, rangedAttacker), Does.Contain(hiddenFlying.Position));
        }

        [Test]
        public void SpellDamage_CanTargetActiveFlyingOccupant()
        {
            var battleState = CreateBattleStateInMainPhase();
            var flying = CreateUnit(
                "flying-target",
                PlayerId.AI,
                new TileCoord(0, 0),
                maxHp: 20,
                hasFlying: true);
            battleState.AIBoard.Place(flying.Position, flying);
            battleState.Player.Hand.Add("test-spell");

            new SpellService().CastDamageSpell(
                battleState,
                PlayerId.Player,
                "test-spell",
                PlayerId.AI,
                flying.Position,
                damage: 5,
                damageType: DamageType.Magic);

            Assert.That(flying.CurrentHp, Is.EqualTo(15));
        }

        [Test]
        public void StateViewRoundTrip_PreservesFlyingForUnitBuildingAndMaster()
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();
            var playerMaster = new MasterState(
                "player-master",
                PlayerId.Player,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333,
                hasFlying: true);
            var aiMaster = new MasterState(
                "ai-master",
                PlayerId.AI,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333);
            var flyingBuilding = new BuildingState(
                "flying-building",
                "flying-building",
                PlayerId.Player,
                new TileCoord(0, 0),
                canAttack: false,
                attack: 0,
                maxHp: 30,
                hasFlying: true);
            var flyingUnit = CreateUnit(
                "flying-unit",
                PlayerId.AI,
                new TileCoord(0, 0),
                hasFlying: true);
            playerBoard.Place(playerMaster.Position, playerMaster);
            aiBoard.Place(aiMaster.Position, aiMaster);
            playerBoard.Place(flyingBuilding.Position, flyingBuilding);
            aiBoard.Place(flyingUnit.Position, flyingUnit);
            var battleState = new BattleState(
                CreatePlayer(PlayerId.Player, playerMaster),
                CreatePlayer(PlayerId.AI, aiMaster),
                playerBoard,
                aiBoard);
            battleState.RestoreRuntimeState(1, PlayerId.Player, PhaseType.Main);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "flying-match",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projected.Player.Master.HasFlying, Is.True);
            Assert.That(projected.PlayerBoard.GetOccupant(flyingBuilding.Position).HasFlying, Is.True);
            Assert.That(projected.AIBoard.GetOccupant(flyingUnit.Position).HasFlying, Is.True);
        }

        private static IReadOnlyCollection<TileCoord> GetTargets(BattleState battleState, OccupantState attacker)
        {
            return new TargetingService().GetLegalTargets(
                battleState,
                attacker.OwnerId,
                attacker.Position);
        }

        private static void PrepareAttacker(OccupantState attacker)
        {
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType = AttackType.Melee,
            int attack = 5,
            int maxHp = 20,
            bool hasShielder = false,
            bool hasHiding = false,
            bool hasFlying = false)
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
                hasShielder: hasShielder,
                hasHiding: hasHiding,
                hasFlying: hasFlying);
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            new TurnStartService().ResolveTurnStart(battleState);
            return battleState;
        }

        private static PlayerState CreatePlayer(PlayerId playerId, MasterState master)
        {
            return new PlayerState(
                playerId,
                new ResourceSet(),
                new DeckState(CreateDeck(playerId.ToString())),
                new HandState(),
                new DiscardState(),
                master);
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
