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
    public sealed class GuSpellTests
    {
        [Test]
        public void CastGu_TransfersSameUnitAndPreservesCombatState()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService();
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord);
            target.IncreaseBaseAttack(5);
            target.IncreaseMaxHpAndCurrentHp(7);
            target.IncreasePhysicalDefense(2);
            target.IncreaseMagicDefense(3);
            target.CurrentHp = 21;
            target.EndureUsed = true;
            target.AddInvincibleEffect(
                InvincibleDurationType.OwnerTurns,
                ownerTurns: 2,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: PlayerId.AI);
            var attackBeforeControl = target.Attack;
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            var destination = new TileCoord(0, 0);
            var transferred = battleState.PlayerBoard.GetOccupant(destination);
            Assert.That(transferred, Is.SameAs(target));
            Assert.That(battleState.AIBoard.GetOccupant(targetCoord), Is.Null);
            Assert.That(transferred.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(transferred.RuntimeId, Is.EqualTo("controlled-unit"));
            Assert.That(transferred.Position, Is.EqualTo(destination));
            Assert.That(transferred.BaseAttack, Is.EqualTo(17));
            Assert.That(transferred.MaxHp, Is.EqualTo(47));
            Assert.That(transferred.CurrentHp, Is.EqualTo(21));
            Assert.That(transferred.Attack, Is.EqualTo(attackBeforeControl));
            Assert.That(transferred.PhysicalDefense, Is.EqualTo(6));
            Assert.That(transferred.MagicDefense, Is.EqualTo(8));
            Assert.That(transferred.HasShielder, Is.True);
            Assert.That(transferred.HasLifeSteal, Is.True);
            Assert.That(transferred.HasFlying, Is.True);
            Assert.That(transferred.EndureUsed, Is.True);
            Assert.That(transferred.InvincibleEffects.Count, Is.EqualTo(1));
            Assert.That(transferred.HasSummoningSickness, Is.True);
            Assert.That(transferred.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(battleState.Player.Hand.Contains(GuRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(GuRules.CardId));
            Assert.That(battleState.Player.Resources.Qi, Is.Zero);
        }

        [Test]
        public void CastGu_UsesFirstEmptyTileInLockedOrder()
        {
            var battleState = CreateBattleState();
            PlaceFriendlyFiller(battleState, new TileCoord(0, 0), "friendly-00");
            PlaceFriendlyFiller(battleState, new TileCoord(0, 1), "friendly-01");
            PlaceFriendlyFiller(battleState, new TileCoord(1, 0), "friendly-10");
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord);
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 1)), Is.SameAs(target));
        }

        [Test]
        public void CastGu_ActiveRushTargetCanAttackImmediately()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord, hasRush: true);
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            Assert.That(target.HasSummoningSickness, Is.False);
            Assert.That(target.RemainingAttacksThisTurn, Is.EqualTo(target.EffectiveMaxAttacksPerTurn));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void CastGu_SuppressionStateIsPreservedAndSuppressesRush(
            bool isDrained,
            bool isErasure)
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord, hasRush: true);
            if (isDrained)
            {
                target.IsDrained = true;
            }
            else if (isErasure)
            {
                target.ApplyErasure();
            }

            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            Assert.That(target.IsDrained, Is.EqualTo(isDrained));
            Assert.That(target.IsErasure, Is.EqualTo(isErasure));
            Assert.That(target.HasSummoningSickness, Is.True);
            Assert.That(target.RemainingAttacksThisTurn, Is.Zero);
        }

        [Test]
        public void CastGu_NormalTargetCanAttackFromNextOwnerTurn()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord);
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);
            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            battleState.Player.Resources.Add(
                new ResourceSet(mana: 0, qi: 0, power: 2, gold: 0));
            battleState.StartNextTurn(PlayerId.Player);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(target.HasSummoningSickness, Is.False);
            Assert.That(target.RemainingAttacksThisTurn, Is.EqualTo(target.EffectiveMaxAttacksPerTurn));
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(1));
            Assert.That(battleState.AI.Resources.Mana, Is.Zero);
        }

        [Test]
        public void CastGu_WhenCasterBoardIsFull_DoesNotConsumeCardOrResources()
        {
            var battleState = CreateBattleState();
            FillPlayerBoard(battleState);
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord);
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);

            Assert.Throws<InvalidOperationException>(() =>
                CreateSpellService().CastScriptedSpell(
                    battleState,
                    PlayerId.Player,
                    GuRules.CardId,
                    PlayerId.AI,
                    targetCoord));

            Assert.That(battleState.Player.Hand.Contains(GuRules.CardId), Is.True);
            Assert.That(battleState.Player.Discard.CardIds, Does.Not.Contain(GuRules.CardId));
            Assert.That(battleState.Player.Resources.Qi, Is.EqualTo(GuRules.QiCost));
            Assert.That(battleState.AIBoard.GetOccupant(targetCoord), Is.SameAs(target));
        }

        [Test]
        public void GuRules_RejectMasterBuildingSealboundAndHidingTargets()
        {
            var battleState = CreateBattleState();
            var buildingCoord = new TileCoord(0, 0);
            var hidingCoord = new TileCoord(0, 1);
            var sealboundCoord = new TileCoord(1, 0);
            battleState.AIBoard.Place(buildingCoord, new BuildingState(
                "enemy-building",
                "EnemyBuilding",
                PlayerId.AI,
                buildingCoord,
                canAttack: false,
                attack: 0,
                maxHp: 20,
                damageType: DamageType.None));
            battleState.AIBoard.Place(hidingCoord, CreateTarget(hidingCoord, hasHiding: true, runtimeId: "hiding"));
            var sealbound = CreateTarget(sealboundCoord, runtimeId: "sealbound");
            sealbound.EnterSealbound(2);
            battleState.AIBoard.Place(sealboundCoord, sealbound);

            Assert.That(GuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.AI,
                battleState.AI.Master.Position), Is.False);
            Assert.That(GuRules.IsLegalTarget(battleState, PlayerId.Player, PlayerId.AI, buildingCoord), Is.False);
            Assert.That(GuRules.IsLegalTarget(battleState, PlayerId.Player, PlayerId.AI, hidingCoord), Is.False);
            Assert.That(GuRules.IsLegalTarget(battleState, PlayerId.Player, PlayerId.AI, sealboundCoord), Is.False);
            Assert.That(GuRules.CanCast(battleState, PlayerId.Player), Is.False);
        }

        [Test]
        public void CastGu_TransferredStateSurvivesOnlineStateViewProjection()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(4, 0);
            var target = CreateTarget(targetCoord);
            target.IsDrained = true;
            battleState.AIBoard.Place(targetCoord, target);
            GiveGuToPlayer(battleState);
            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                GuRules.CardId,
                PlayerId.AI,
                targetCoord);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "gu-reconnect",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.PlayerBoard.GetOccupant(new TileCoord(0, 0));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.RuntimeId, Is.EqualTo(target.RuntimeId));
            Assert.That(restored.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(restored.IsDrained, Is.True);
            Assert.That(restored.HasSummoningSickness, Is.True);
            Assert.That(restored.RemainingAttacksThisTurn, Is.Zero);
        }

        private static SpellService CreateSpellService()
        {
            return new SpellService(new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    GuRules.CardId,
                    "고독",
                    new ResourceSet(mana: 0, qi: GuRules.QiCost, power: 0, gold: 0),
                    GuRules.EffectId,
                    damage: 0,
                    damageType: DamageType.None),
            }));
        }

        private static UnitState CreateTarget(
            TileCoord coord,
            bool hasRush = false,
            bool hasHiding = false,
            string runtimeId = "controlled-unit")
        {
            return new UnitState(
                runtimeId,
                "EnemyUnit",
                PlayerId.AI,
                coord,
                AttackType.Melee,
                attack: 12,
                maxHp: 40,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 2,
                turnStartResourceGain: new ResourceSet(mana: 1, qi: 0, power: 0, gold: 1),
                maxAttacksPerTurn: 2,
                hitsPerAttack: 3,
                hasBerserker: true,
                hasEndure: true,
                hasShielder: true,
                hasLifeSteal: true,
                damageType: DamageType.Magic,
                physicalDefense: 4,
                magicDefense: 5,
                hasRobot: true,
                hasRush: hasRush,
                hasHiding: hasHiding,
                hasFlying: true,
                spellPower: 2);
        }

        private static void GiveGuToPlayer(BattleState battleState)
        {
            battleState.Player.Hand.Add(GuRules.CardId);
            battleState.Player.Resources.Add(
                new ResourceSet(mana: 0, qi: GuRules.QiCost, power: 0, gold: 0));
        }

        private static void FillPlayerBoard(BattleState battleState)
        {
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var coord = new TileCoord(column, row);
                    if (battleState.PlayerBoard.GetOccupant(coord) == null)
                    {
                        PlaceFriendlyFiller(battleState, coord, $"friendly-{column}-{row}");
                    }
                }
            }
        }

        private static void PlaceFriendlyFiller(BattleState battleState, TileCoord coord, string runtimeId)
        {
            battleState.PlayerBoard.Place(coord, new UnitState(
                runtimeId,
                runtimeId,
                PlayerId.Player,
                coord,
                AttackType.Melee,
                attack: 1,
                maxHp: 1,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0));
        }

        private static BattleState CreateBattleState()
        {
            var battleState = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
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
