using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleCombatLogEntryFormatterTests
    {
        [Test]
        public void Format_RobotFactoryGeneration_ShowsIdentityOnlyWhenServerIncludesIt()
        {
            var ownerEntry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.CardGenerated,
                SourceOwnerId = PlayerId.Player,
                SourceCardId = "RobotFactory",
                TargetCardId = "A-111",
                Amount = 1
            };
            var opponentEntry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.CardGenerated,
                SourceOwnerId = PlayerId.AI,
                SourceCardId = "RobotFactory",
                TargetCardId = string.Empty,
                Amount = 1
            };

            Assert.That(
                BattleCombatLogEntryFormatter.Format(ownerEntry, ResolveCardName),
                Is.EqualTo("플레이어가 로봇 공장으로 A-111을 획득"));
            Assert.That(
                BattleCombatLogEntryFormatter.Format(opponentEntry, ResolveCardName),
                Is.EqualTo("상대방이 로봇 공장으로 카드 1장을 획득"));
        }

        [TestCase(13, 0, "플레이어의 용사가 ATK +13을 얻음")]
        [TestCase(0, 13, "상대방의 용사가 HP +13을 얻음")]
        public void Format_HeroGrowth_ShowsGainedStat(
            int attackBonus,
            int hpBonus,
            string expected)
        {
            var result = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HeroGrowth,
                    TargetOwnerId = attackBonus > 0 ? PlayerId.Player : PlayerId.AI,
                    TargetCardId = HeroRules.CardId,
                    AttackBonus = attackBonus,
                    HpBonus = hpBonus
                },
                ResolveCardName);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Format_AttackWithCounterAndMultiHit_IncludesBothHpHistories()
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Attack,
                SourceOwnerId = PlayerId.Player,
                TargetOwnerId = PlayerId.AI,
                SourceCardId = "Cerberus",
                TargetCardId = "Goblin",
                SourceHpHistory = new List<int> { 66, 33 },
                TargetHpHistory = new List<int> { 27, 18, 9, 0 },
                TargetRemoved = true,
                HasCounterattack = true,
                AttackType = AttackType.Melee,
                DamageType = DamageType.Physical
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo(
                    "플레이어의 케르베로스(66→33)가 " +
                    "상대방의 고블린(27→18→9→0, 처치됨)을 물리 공격[반격함]"));
        }

        [TestCase(AttackType.Melee, DamageType.Physical, "물리 공격")]
        [TestCase(AttackType.Melee, DamageType.Magic, "마법 공격")]
        [TestCase(AttackType.Ranged, DamageType.Physical, "원거리 물리 공격")]
        [TestCase(AttackType.Ranged, DamageType.Magic, "원거리 마법 공격")]
        public void Format_Attack_IncludesRangeAndDamageType(
            AttackType attackType,
            DamageType damageType,
            string expectedAttackText)
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Attack,
                SourceOwnerId = PlayerId.AI,
                TargetOwnerId = PlayerId.Player,
                SourceCardId = "Cerberus",
                TargetCardId = "Goblin",
                TargetHpHistory = new List<int> { 300, 297 },
                AttackType = attackType,
                DamageType = damageType
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo(
                    $"상대방의 케르베로스가 플레이어의 고블린(300→297)을 {expectedAttackText}"));
        }

        [Test]
        public void Format_RedDragonDamage_UsesDamageTypeAndHpHistory()
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Damage,
                TargetOwnerId = PlayerId.Player,
                SourceCardId = "RedDragon",
                TargetCardId = "Goblin",
                Amount = 33,
                DamageType = DamageType.Magic,
                ValueCause = BattleValueChangeCause.RedDragon,
                TargetHpHistory = new List<int> { 10, 0 },
                TargetRemoved = true
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo(
                    "플레이어의 고블린이 레드 드래곤 효과로 " +
                    "33의 마법 피해를 입음 (10→0, 처치됨)"));
        }

        [Test]
        public void Format_PiercingDamage_UsesPiercingCauseText()
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Damage,
                SourceOwnerId = PlayerId.Player,
                TargetOwnerId = PlayerId.AI,
                SourceCardId = "A-111",
                TargetCardId = "Goblin",
                Amount = 6,
                DamageType = DamageType.Physical,
                ValueCause = BattleValueChangeCause.Piercing,
                TargetHpHistory = new List<int> { 10, 4 }
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo("상대방의 고블린이 관통 효과로 6의 물리 피해를 입음 (10→4)"));
        }

        [Test]
        public void Format_NuclearPowerPlantDamage_UsesExplosionCauseText()
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Damage,
                SourceOwnerId = PlayerId.Player,
                TargetOwnerId = PlayerId.AI,
                SourceCardId = NuclearPowerPlantRules.CardId,
                TargetCardId = "Goblin",
                Amount = 30,
                DamageType = DamageType.Physical,
                ValueCause = BattleValueChangeCause.NuclearPowerPlant,
                TargetHpHistory = new List<int> { 50, 20 }
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo("상대방의 고블린이 원자력 발전소 폭발로 30의 물리 피해를 입음 (50→20)"));
        }

        [Test]
        public void Format_BiochemicalBombDamage_UsesCardEffectCauseText()
        {
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.Damage,
                SourceOwnerId = PlayerId.Player,
                TargetOwnerId = PlayerId.AI,
                SourceCardId = BiochemicalBombRules.CardId,
                TargetCardId = "Goblin",
                Amount = 44,
                DamageType = DamageType.Physical,
                ValueCause = BattleValueChangeCause.BiochemicalBomb,
                TargetHpHistory = new List<int> { 50, 6 }
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo("상대방의 고블린이 생화학폭탄 효과로 44의 물리 피해를 입음 (50→6)"));
        }

        [Test]
        public void Format_ResourceGainAndSpend_UsesCardAndResourceNames()
        {
            var spent = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.ResourceSpent,
                    SourceOwnerId = PlayerId.Player,
                    SourceCardId = "PowerPlant",
                    Amount = 1,
                    ResourceType = BattleCombatLogResourceType.Gold
                },
                ResolveCardName);
            var gained = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.ResourceGained,
                    SourceOwnerId = PlayerId.Player,
                    SourceCardId = "PowerPlant",
                    Amount = 2,
                    ResourceType = BattleCombatLogResourceType.Power
                },
                ResolveCardName);

            Assert.That(spent, Is.EqualTo("발전소 효과로 플레이어 골드 1 소모"));
            Assert.That(gained, Is.EqualTo("발전소 효과로 플레이어 전력 +2"));
        }

        [Test]
        public void Format_CardDrawn_ShowsSourceAndDrawCount()
        {
            var result = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.CardDrawn,
                    SourceOwnerId = PlayerId.Player,
                    SourceCardId = GaebangBranchRules.CardId,
                    Amount = 2
                },
                ResolveCardName);

            Assert.That(result, Is.EqualTo("개방 분타 효과로 플레이어 카드 2장 드로우"));
        }

        [Test]
        public void Format_ShielderRedirected_UsesShielderEffectName()
        {
            var result = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.ShielderRedirected,
                    SourceOwnerId = PlayerId.Player,
                    TargetOwnerId = PlayerId.AI,
                    SourceCardId = "Shieldbearer",
                    TargetCardId = "Goblin",
                    SourceHpHistory = new List<int> { 30, 20 }
                },
                ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo("쉴더 효과로 플레이어의 방패병(30→20)가 상대방의 고블린 대신 피해를 받음"));
        }

        [Test]
        public void Format_HuanShuLifecycleAndRedirect_UsesKoreanStatusText()
        {
            var applied = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HuanShuApplied,
                    TargetOwnerId = PlayerId.AI,
                    TargetCardId = "Goblin",
                    Amount = 3
                },
                ResolveCardName);
            var redirected = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HuanShuRedirected,
                    SourceOwnerId = PlayerId.AI,
                    TargetOwnerId = PlayerId.Player,
                    SourceCardId = "Goblin",
                    TargetCardId = "Shieldbearer"
                },
                ResolveCardName);
            var cleared = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HuanShuCleared,
                    TargetOwnerId = PlayerId.AI,
                    TargetCardId = "Goblin"
                },
                ResolveCardName);

            Assert.That(applied, Is.EqualTo("상대방의 고블린에게 환술 부여"));
            Assert.That(
                redirected,
                Is.EqualTo("환술로 상대방의 고블린의 공격 대상이 플레이어의 방패병(으)로 변경됨"));
            Assert.That(cleared, Is.EqualTo("상대방의 고블린 환술 해제"));
        }

        [Test]
        public void Format_BattleEnded_UsesProjectedLocalWinner()
        {
            var victory = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.BattleEnded,
                    TargetOwnerId = PlayerId.Player
                },
                ResolveCardName);
            var defeat = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.BattleEnded,
                    TargetOwnerId = PlayerId.AI
                },
                ResolveCardName);
            var draw = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.BattleEnded,
                    IsDraw = true
                },
                ResolveCardName);

            Assert.That(victory, Is.EqualTo("전투 종료: 플레이어 승리"));
            Assert.That(defeat, Is.EqualTo("전투 종료: 플레이어 패배"));
            Assert.That(draw, Is.EqualTo("전투 종료: 무승부"));
        }

        [Test]
        public void Format_ErasureApplied_UsesKoreanStatusName()
        {
            var result = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.ErasureApplied,
                    TargetOwnerId = PlayerId.AI,
                    TargetCardId = "Goblin"
                },
                ResolveCardName);

            Assert.That(result, Is.EqualTo("상대방의 고블린이 망각 상태가 됨"));
        }

        [Test]
        public void Format_SealboundAppliedAndReleased_UsesKoreanStatusText()
        {
            var applied = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.SealboundApplied,
                    TargetOwnerId = PlayerId.Player,
                    TargetCardId = "Goblin",
                    Amount = 2
                },
                ResolveCardName);
            var released = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.SealboundReleased,
                    TargetOwnerId = PlayerId.Player,
                    TargetCardId = "Goblin"
                },
                ResolveCardName);

            Assert.That(applied, Is.EqualTo("플레이어의 고블린 봉인 (해제까지 소유자 턴 시작 2회)"));
            Assert.That(released, Is.EqualTo("플레이어의 고블린 봉인 해제"));
        }

        [Test]
        public void Format_DemonKingRevivalLifecycle_UsesDedicatedKoreanText()
        {
            var started = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.DemonKingRevivalStarted,
                    TargetOwnerId = PlayerId.Player,
                    TargetCardId = DemonKingRules.CardId,
                    Amount = 3
                },
                ResolveCardName);
            var revived = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.DemonKingRevived,
                    TargetOwnerId = PlayerId.Player,
                    TargetCardId = DemonKingRules.CardId,
                    AttackBonus = 33,
                    HpBonus = 33
                },
                ResolveCardName);

            Assert.That(started, Is.EqualTo("플레이어의 마왕이 사망하여 봉인됨 (부활까지 3턴 시작)"));
            Assert.That(revived, Is.EqualTo("플레이어의 마왕이 ATK/HP +33을 얻고 부활"));
        }

        [Test]
        public void Format_HidingAppliedAndRevealed_UsesKoreanStatusText()
        {
            var applied = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HidingApplied,
                    TargetOwnerId = PlayerId.AI,
                    TargetCardId = "Goblin"
                },
                ResolveCardName);
            var revealed = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HidingRevealed,
                    TargetOwnerId = PlayerId.AI,
                    TargetCardId = "Goblin"
                },
                ResolveCardName);

            Assert.That(applied, Is.EqualTo("상대방의 고블린 은신"));
            Assert.That(revealed, Is.EqualTo("상대방의 고블린 은신 해제"));
        }

        [Test]
        public void Format_OccupantControlled_UsesNewAndPreviousOwners()
        {
            var result = BattleCombatLogEntryFormatter.Format(
                new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.OccupantControlled,
                    SourceOwnerId = PlayerId.Player,
                    TargetOwnerId = PlayerId.AI,
                    SourceCardId = "Goblin"
                },
                ResolveCardName);

            Assert.That(result, Is.EqualTo("플레이어가 상대방의 고블린을 복종시킴"));
        }

        [Test]
        public void OnlineEnvelope_RoundTrip_PreservesStructuredCombatLog()
        {
            var envelope = new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.StateView,
                StateView = new BattleStateViewDto
                {
                    CombatLogLatestSequence = 7,
                    ReplaceCombatLogEntries = true,
                    CombatLogEntries = new List<BattleCombatLogEntryDto>
                    {
                        new BattleCombatLogEntryDto
                        {
                            Sequence = 7,
                            EntryType = BattleCombatLogEntryType.UnitSummoned,
                            SourceOwnerId = PlayerId.AI,
                            SourceCardId = "Goblin",
                            TargetCoord = new TileCoordDto { Column = 1, Row = 0 }
                        }
                    }
                }
            };

            var json = OnlineBattleMessageSerializer.SerializeEnvelope(envelope);
            var roundTripped = OnlineBattleMessageSerializer.DeserializeEnvelope(json);

            Assert.That(roundTripped.StateView.ReplaceCombatLogEntries, Is.True);
            Assert.That(roundTripped.StateView.CombatLogLatestSequence, Is.EqualTo(7));
            Assert.That(roundTripped.StateView.CombatLogEntries, Has.Count.EqualTo(1));
            Assert.That(
                roundTripped.StateView.CombatLogEntries[0].EntryType,
                Is.EqualTo(BattleCombatLogEntryType.UnitSummoned));
            Assert.That(roundTripped.StateView.CombatLogEntries[0].TargetCoord.Column, Is.EqualTo(1));
        }

        private static string ResolveCardName(string cardId)
        {
            return cardId switch
            {
                "Cerberus" => "케르베로스",
                "Goblin" => "고블린",
                "RedDragon" => "레드 드래곤",
                "PowerPlant" => "발전소",
                "NuclearPowerPlant" => "원자력 발전소",
                "Shieldbearer" => "방패병",
                "DemonKing" => "마왕",
                "Hero" => "용사",
                "GaebangBranch" => "개방 분타",
                _ => cardId
            };
        }
    }
}
