using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class HuanShuTests
    {
        [Test]
        public void CastHuanShu_AppliesPermanentStatusAndConsumesCard()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(0, 0);
            var target = CreateUnit("target", PlayerId.AI, targetCoord);
            battleState.AIBoard.Place(targetCoord, target);
            GiveHuanShuToPlayer(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                HuanShuRules.CardId,
                PlayerId.AI,
                targetCoord);

            Assert.That(target.IsUnderHuanShu, Is.True);
            Assert.That(target.HuanShuOwnerTurnsRemaining, Is.EqualTo(HuanShuRules.ActiveStateMarker));
            Assert.That(target.HuanShuEligibleAfterTurnNumber, Is.Zero);
            Assert.That(battleState.Player.Hand.Contains(HuanShuRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(HuanShuRules.CardId));
            Assert.That(battleState.Player.Resources.Qi, Is.Zero);
        }

        [Test]
        public void CastHuanShu_RecastKeepsPermanentStatusActive()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(0, 0);
            var target = CreateUnit("target", PlayerId.AI, targetCoord);
            target.RestoreHuanShuState(1, 0);
            battleState.AIBoard.Place(targetCoord, target);
            GiveHuanShuToPlayer(battleState);

            CreateSpellService().CastScriptedSpell(
                battleState,
                PlayerId.Player,
                HuanShuRules.CardId,
                PlayerId.AI,
                targetCoord);

            Assert.That(target.HuanShuOwnerTurnsRemaining, Is.EqualTo(HuanShuRules.ActiveStateMarker));
        }

        [Test]
        public void HuanShuRules_RejectsAlliedMasterBuildingSealboundAndEnemyHidingTargets()
        {
            var battleState = CreateBattleState();
            var buildingCoord = new TileCoord(0, 0);
            var hidingCoord = new TileCoord(0, 1);
            var sealboundCoord = new TileCoord(1, 0);
            var alliedCoord = new TileCoord(0, 0);
            battleState.AIBoard.Place(buildingCoord, new BuildingState(
                "building",
                "Building",
                PlayerId.AI,
                buildingCoord,
                canAttack: false,
                attack: 0,
                maxHp: 20,
                damageType: DamageType.None));
            battleState.AIBoard.Place(
                hidingCoord,
                CreateUnit("hiding", PlayerId.AI, hidingCoord, hasHiding: true));
            var sealbound = CreateUnit("sealbound", PlayerId.AI, sealboundCoord);
            sealbound.EnterSealbound(2);
            battleState.AIBoard.Place(sealboundCoord, sealbound);
            battleState.PlayerBoard.Place(
                alliedCoord,
                CreateUnit("allied", PlayerId.Player, alliedCoord));

            Assert.That(HuanShuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.AI,
                battleState.AI.Master.Position), Is.False);
            Assert.That(HuanShuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.AI,
                buildingCoord), Is.False);
            Assert.That(HuanShuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.AI,
                hidingCoord), Is.False);
            Assert.That(HuanShuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.AI,
                sealboundCoord), Is.False);
            Assert.That(HuanShuRules.IsLegalTarget(
                battleState,
                PlayerId.Player,
                PlayerId.Player,
                alliedCoord), Is.False);
            Assert.That(HuanShuRules.CanCast(battleState, PlayerId.Player), Is.False);
        }

        [Test]
        public void Attack_HuanShuCanRedirectToFriendlyOccupantAndUsesNormalCombat()
        {
            var battleState = CreateBattleState();
            var attackerCoord = new TileCoord(0, 0);
            var friendlyCoord = new TileCoord(1, 0);
            var enemyCoord = new TileCoord(0, 0);
            var attacker = CreateUnit("attacker", PlayerId.Player, attackerCoord, attack: 4, maxHp: 10);
            var friendly = CreateUnit("friendly", PlayerId.Player, friendlyCoord, attack: 2, maxHp: 10);
            var enemy = CreateUnit("enemy", PlayerId.AI, enemyCoord, attack: 3, maxHp: 10);
            ReadyForAttack(attacker);
            attacker.ApplyHuanShu();
            battleState.PlayerBoard.Place(attackerCoord, attacker);
            battleState.PlayerBoard.Place(friendlyCoord, friendly);
            battleState.AIBoard.Place(enemyCoord, enemy);

            new AttackService(new TargetingService(), _ => 0).Attack(
                battleState,
                PlayerId.Player,
                attackerCoord,
                enemyCoord);

            Assert.That(friendly.CurrentHp, Is.EqualTo(6));
            Assert.That(attacker.CurrentHp, Is.EqualTo(8));
            Assert.That(enemy.CurrentHp, Is.EqualTo(10));
            Assert.That(battleState.LastAttackResolution.WasHuanShuRedirected, Is.True);
            Assert.That(battleState.LastAttackResolution.ResolvedTargetOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.LastAttackResolution.ResolvedTargetCoord, Is.EqualTo(friendlyCoord));
        }

        [Test]
        public void Attack_HuanShuSelectsOnceForAllMultihitStrikes()
        {
            var battleState = CreateBattleState();
            var attackerCoord = new TileCoord(0, 0);
            var friendlyCoord = new TileCoord(1, 0);
            var enemyCoord = new TileCoord(0, 0);
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                attackerCoord,
                attack: 4,
                maxHp: 20,
                hitsPerAttack: 3);
            var friendly = CreateUnit("friendly", PlayerId.Player, friendlyCoord, attack: 2, maxHp: 30);
            var enemy = CreateUnit("enemy", PlayerId.AI, enemyCoord, attack: 3, maxHp: 30);
            ReadyForAttack(attacker);
            attacker.ApplyHuanShu();
            battleState.PlayerBoard.Place(attackerCoord, attacker);
            battleState.PlayerBoard.Place(friendlyCoord, friendly);
            battleState.AIBoard.Place(enemyCoord, enemy);
            var selectionCount = 0;

            new AttackService(new TargetingService(), _ =>
            {
                selectionCount++;
                return 0;
            }).Attack(battleState, PlayerId.Player, attackerCoord, enemyCoord);

            Assert.That(selectionCount, Is.EqualTo(1));
            Assert.That(friendly.CurrentHp, Is.EqualTo(18));
            Assert.That(enemy.CurrentHp, Is.EqualTo(30));
        }

        [Test]
        public void Attack_HuanShuCandidatePoolKeepsFriendlyHidingButExcludesProtectedEnemies()
        {
            var battleState = CreateBattleState();
            var attackerCoord = new TileCoord(0, 0);
            var friendlyHidingCoord = new TileCoord(1, 0);
            var declaredTargetCoord = new TileCoord(0, 0);
            var enemyHidingCoord = new TileCoord(1, 0);
            var enemyFlyingCoord = new TileCoord(2, 0);
            var attacker = CreateUnit("attacker", PlayerId.Player, attackerCoord, attack: 4, maxHp: 20);
            ReadyForAttack(attacker);
            attacker.ApplyHuanShu();
            battleState.PlayerBoard.Place(attackerCoord, attacker);
            battleState.PlayerBoard.Place(
                friendlyHidingCoord,
                CreateUnit("friendly-hiding", PlayerId.Player, friendlyHidingCoord, hasHiding: true));
            battleState.AIBoard.Place(
                declaredTargetCoord,
                CreateUnit("declared", PlayerId.AI, declaredTargetCoord, maxHp: 30));
            battleState.AIBoard.Place(
                enemyHidingCoord,
                CreateUnit("enemy-hiding", PlayerId.AI, enemyHidingCoord, hasHiding: true));
            battleState.AIBoard.Place(
                enemyFlyingCoord,
                CreateUnit("enemy-flying", PlayerId.AI, enemyFlyingCoord, hasFlying: true));
            var candidateCount = 0;

            new AttackService(new TargetingService(), count =>
            {
                candidateCount = count;
                return 2;
            }).Attack(battleState, PlayerId.Player, attackerCoord, declaredTargetCoord);

            Assert.That(candidateCount, Is.EqualTo(4));
        }

        [Test]
        public void HuanShu_DoesNotCountTurnEnds_PersistsThroughSealbound_AndErasureClearsIt()
        {
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0));
            target.ApplyHuanShu();

            Assert.That(target.ResolveHuanShuOwnerTurnEnd(PlayerId.Player, 2), Is.False);
            Assert.That(target.IsUnderHuanShu, Is.True);
            Assert.That(target.ResolveHuanShuOwnerTurnEnd(PlayerId.AI, 2), Is.False);
            Assert.That(target.IsUnderHuanShu, Is.True);

            target.EnterSealbound(2);
            Assert.That(target.ResolveHuanShuOwnerTurnEnd(PlayerId.AI, 3), Is.False);
            Assert.That(target.IsUnderHuanShu, Is.True);
            target.ApplyErasure();
            Assert.That(target.IsUnderHuanShu, Is.True,
                "Sealbound blocks Erasure and preserves HuanShu.");

            target.ResolveSealboundOwnerTurnStart();
            target.ResolveSealboundOwnerTurnStart();
            target.ApplyErasure();
            Assert.That(target.IsErasure, Is.True);
            Assert.That(target.IsUnderHuanShu, Is.False);
        }

        [Test]
        public void HuanShu_RemainsAfterAnyNumberOfOwnerTurnEnds()
        {
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0));
            target.ApplyHuanShu();

            for (var turn = 2; turn <= 20; turn++)
            {
                Assert.That(target.ResolveHuanShuOwnerTurnEnd(PlayerId.AI, turn), Is.False);
            }

            Assert.That(target.IsUnderHuanShu, Is.True);
            Assert.That(target.HuanShuOwnerTurnsRemaining, Is.EqualTo(HuanShuRules.ActiveStateMarker));
        }

        [Test]
        public void HuanShu_OwnershipTransferPreservesPermanentStatus()
        {
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0));
            target.RestoreHuanShuState(2, eligibleAfterTurnNumber: 1);
            target.SetBattleTurnContext(PlayerId.Player, turnNumber: 4);

            target.TransferOwnership(PlayerId.Player);

            Assert.That(target.IsUnderHuanShu, Is.True);
            Assert.That(target.HuanShuOwnerTurnsRemaining, Is.EqualTo(HuanShuRules.ActiveStateMarker));
            Assert.That(target.HuanShuEligibleAfterTurnNumber, Is.Zero);
        }

        [Test]
        public void StateViewAndTooltip_PreservePermanentHuanShu()
        {
            var battleState = CreateBattleState();
            var targetCoord = new TileCoord(0, 0);
            var target = CreateUnit("target", PlayerId.AI, targetCoord);
            target.RestoreHuanShuState(2, eligibleAfterTurnNumber: 7);
            battleState.AIBoard.Place(targetCoord, target);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "huan-shu-match",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.AIBoard.GetOccupant(targetCoord);
            var tooltip = OccupantSpecialEffectTooltipCatalog.Build(null, restored)
                .Single(entry => entry.Title == "환술");

            Assert.That(restored.IsUnderHuanShu, Is.True);
            Assert.That(restored.HuanShuOwnerTurnsRemaining, Is.EqualTo(HuanShuRules.ActiveStateMarker));
            Assert.That(restored.HuanShuEligibleAfterTurnNumber, Is.Zero);
            Assert.That(tooltip.Description, Is.EqualTo("일반 공격 시 공격 대상이 무작위로 변경됩니다."));
        }

        private static SpellService CreateSpellService()
        {
            return new SpellService(new InMemoryCardDefinitionProvider(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    HuanShuRules.CardId,
                    "환술",
                    new ResourceSet(mana: 0, qi: HuanShuRules.QiCost, power: 0, gold: 0),
                    HuanShuRules.EffectId,
                    damage: 0,
                    damageType: DamageType.None),
            }));
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            int attack = 3,
            int maxHp = 20,
            int hitsPerAttack = 1,
            bool hasHiding = false,
            bool hasFlying = false)
        {
            return new UnitState(
                id,
                id,
                ownerId,
                coord,
                AttackType.Melee,
                attack,
                maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: hitsPerAttack,
                hasHiding: hasHiding,
                hasFlying: hasFlying);
        }

        private static void ReadyForAttack(OccupantState occupant)
        {
            occupant.HasSummoningSickness = false;
            occupant.RemainingAttacksThisTurn = 1;
        }

        private static void GiveHuanShuToPlayer(BattleState battleState)
        {
            battleState.Player.Hand.Add(HuanShuRules.CardId);
            battleState.Player.Resources.Add(
                new ResourceSet(mana: 0, qi: HuanShuRules.QiCost, power: 0, gold: 0));
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
