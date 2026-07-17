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
                HasCounterattack = true
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo(
                    "플레이어의 케르베로스(66→33)가 " +
                    "상대방의 고블린(27→18→9→0, 처치됨)을 공격 [반격 발생]"));
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
                Amount = 66,
                DamageType = DamageType.Physical,
                ValueCause = BattleValueChangeCause.RedDragon,
                TargetHpHistory = new List<int> { 10, 0 },
                TargetRemoved = true
            };

            var result = BattleCombatLogEntryFormatter.Format(entry, ResolveCardName);

            Assert.That(
                result,
                Is.EqualTo(
                    "플레이어의 고블린이 레드 드래곤 효과로 " +
                    "66의 물리 피해를 입음 (10→0, 처치됨)"));
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

            Assert.That(victory, Is.EqualTo("전투 종료: 플레이어 승리"));
            Assert.That(defeat, Is.EqualTo("전투 종료: 플레이어 패배"));
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
                _ => cardId
            };
        }
    }
}
