using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleCombatLogEntryFormatter
    {
        public static string Format(
            BattleCombatLogEntryDto entry,
            Func<string, string> cardNameResolver)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var sourceName = ResolveCardName(entry.SourceCardId, cardNameResolver);
            var targetName = ResolveCardName(entry.TargetCardId, cardNameResolver);
            switch (entry.EntryType)
            {
                case BattleCombatLogEntryType.BattleStarted:
                    return $"전투 시작 - {FormatOwner(entry.SourceOwnerId)} 선공";

                case BattleCombatLogEntryType.UnitSummoned:
                    return $"{FormatActor(entry.SourceOwnerId)} {AppendObjectParticle(sourceName)} " +
                           $"{FormatCoord(entry.TargetCoord)}에 소환";

                case BattleCombatLogEntryType.BuildingConstructed:
                    return $"{FormatActor(entry.SourceOwnerId)} {AppendObjectParticle(sourceName)} " +
                           $"{FormatCoord(entry.TargetCoord)}에 건설";

                case BattleCombatLogEntryType.UpgradeApplied:
                    return $"강화 적용: {FormatPossessiveOwner(entry.SourceOwnerId)} {sourceName}에 " +
                           $"ATK +{Math.Max(0, entry.AttackBonus)}, HP +{Math.Max(0, entry.HpBonus)} 부여";

                case BattleCombatLogEntryType.OccupantMoved:
                    return $"{FormatPossessiveOwner(entry.SourceOwnerId)} {AppendSubjectParticle(sourceName)} " +
                           $"{FormatCoord(entry.SourceCoord)}에서 {FormatCoord(entry.TargetCoord)}으로 이동";

                case BattleCombatLogEntryType.OccupantsSwapped:
                    return $"{FormatPossessiveOwner(entry.SourceOwnerId)} " +
                           $"{sourceName}{FormatCoord(entry.SourceCoord)}와 " +
                           $"{targetName}{FormatCoord(entry.TargetCoord)}가 위치를 교환";

                case BattleCombatLogEntryType.Attack:
                    return FormatAttack(entry, sourceName, targetName);

                case BattleCombatLogEntryType.GuardRedirected:
                    return $"{FormatPossessiveOwner(entry.SourceOwnerId)} {sourceName}" +
                           $"({FormatHpHistory(entry.SourceHpHistory, entry.SourceRemoved, entry.SourceDamagePrevented)})가 " +
                           $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 대신 피해를 받음";

                case BattleCombatLogEntryType.SpellCast:
                    return $"{FormatActor(entry.SourceOwnerId)} {sourceName} 마법카드를 사용했다.";

                case BattleCombatLogEntryType.AttackBuffApplied:
                    return $"{sourceName} 효과로 {FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{targetName}에 ATK +{Math.Max(0, entry.AttackBonus)} 부여";

                case BattleCombatLogEntryType.DestructionMarked:
                    return $"{sourceName} 효과로 {FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{targetName}에 파괴 표식 부여";

                case BattleCombatLogEntryType.Damage:
                    return FormatDamage(entry, sourceName, targetName);

                case BattleCombatLogEntryType.Healing:
                    return FormatHealing(entry, targetName);

                case BattleCombatLogEntryType.ResourceGained:
                    return $"{sourceName} 효과로 {FormatOwner(entry.SourceOwnerId)} " +
                           $"{FormatResource(entry.ResourceType)} +{Math.Max(0, entry.Amount)}";

                case BattleCombatLogEntryType.DestroyedByMark:
                    return $"{sourceName} 효과로 {FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{AppendSubjectParticle(targetName)} 파괴됨";

                case BattleCombatLogEntryType.OccupantRemoved:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{AppendSubjectParticle(targetName)} 처치됨";

                case BattleCombatLogEntryType.DrainedApplied:
                    return $"전력 부족으로 {FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{AppendSubjectParticle(targetName)} 방전됨";

                case BattleCombatLogEntryType.DrainedCleared:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 방전 해제";

                case BattleCombatLogEntryType.ErasureApplied:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} " +
                           $"{AppendSubjectParticle(targetName)} 망각 상태가 됨";

                case BattleCombatLogEntryType.ErasureCleared:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 망각 해제";

                case BattleCombatLogEntryType.TurnEnded:
                    return $"{FormatActor(entry.SourceOwnerId)} 턴을 종료";

                case BattleCombatLogEntryType.TurnTimedOut:
                    return $"{FormatPossessiveOwner(entry.SourceOwnerId)} 제한 시간이 종료되어 턴이 자동 종료";

                case BattleCombatLogEntryType.TurnStarted:
                    return $"{Math.Max(1, entry.TurnNumber)}턴 - " +
                           $"{FormatPossessiveOwner(entry.SourceOwnerId)} 턴 시작";

                case BattleCombatLogEntryType.BattleEnded:
                    return entry.TargetOwnerId == PlayerId.Player
                        ? "전투 종료: 플레이어 승리"
                        : "전투 종료: 플레이어 패배";

                case BattleCombatLogEntryType.RobotFusion:
                    return $"{FormatActor(entry.SourceOwnerId)} 로봇 합체로 " +
                           $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName}에 " +
                           $"ATK +{Math.Max(0, entry.AttackBonus)}, HP +{Math.Max(0, entry.HpBonus)} 부여";

                case BattleCombatLogEntryType.CardGenerated:
                    return string.IsNullOrWhiteSpace(entry.TargetCardId)
                        ? "상대방이 로봇 공장으로 카드 1장을 획득"
                        : $"플레이어가 로봇 공장으로 {targetName}을 획득";

                case BattleCombatLogEntryType.SealboundApplied:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 봉인 " +
                           $"(해제까지 소유자 턴 시작 {Math.Max(1, entry.Amount)}회)";

                case BattleCombatLogEntryType.SealboundReleased:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 봉인 해제";

                case BattleCombatLogEntryType.HidingApplied:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 은신";

                case BattleCombatLogEntryType.HidingRevealed:
                    return $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName} 은신 해제";

                default:
                    return string.Empty;
            }
        }

        private static string FormatAttack(
            BattleCombatLogEntryDto entry,
            string sourceName,
            string targetName)
        {
            var sourceHpText = entry.SourceHpHistory == null || entry.SourceHpHistory.Count < 2
                ? string.Empty
                : $"({FormatHpHistory(entry.SourceHpHistory, entry.SourceRemoved, entry.SourceDamagePrevented)})";
            var targetHpText = $"({FormatHpHistory(entry.TargetHpHistory, entry.TargetRemoved, entry.TargetDamagePrevented)})";
            var attackerText = $"{FormatPossessiveOwner(entry.SourceOwnerId)} {sourceName}{sourceHpText}";
            var targetText = $"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName}{targetHpText}";
            var counterText = entry.HasCounterattack ? " [반격 발생]" : string.Empty;
            return $"{AppendSubjectParticle(attackerText)} {targetText}을 공격{counterText}";
        }

        private static string FormatDamage(
            BattleCombatLogEntryDto entry,
            string sourceName,
            string targetName)
        {
            var causeText = entry.ValueCause switch
            {
                BattleValueChangeCause.RedDragon => "레드 드래곤 효과로",
                BattleValueChangeCause.Firewall => "파이어월 효과로",
                BattleValueChangeCause.DeckExhaustion => "덱 고갈로",
                BattleValueChangeCause.Spell => $"{sourceName} 효과로",
                _ => "카드 효과로"
            };
            return $"{AppendSubjectParticle($"{FormatPossessiveOwner(entry.TargetOwnerId)} {targetName}")} " +
                   $"{causeText} {Math.Max(0, entry.Amount)}의 {FormatDamageType(entry.DamageType)} 피해를 입음 " +
                   $"({FormatHpHistory(entry.TargetHpHistory, entry.TargetRemoved, entry.TargetDamagePrevented)})";
        }

        private static string FormatHealing(BattleCombatLogEntryDto entry, string targetName)
        {
            var causeText = entry.ValueCause switch
            {
                BattleValueChangeCause.LifeSteal => "흡혈로",
                BattleValueChangeCause.BlueDragon => "블루 드래곤 효과로",
                _ => "카드 효과로"
            };
            return $"{causeText} {FormatPossessiveOwner(entry.TargetOwnerId)} {targetName}의 HP 회복 " +
                   $"({FormatHpHistory(entry.TargetHpHistory, removed: false, damagePrevented: false)})";
        }

        private static string FormatHpHistory(
            IReadOnlyList<int> hpHistory,
            bool removed,
            bool damagePrevented)
        {
            if (hpHistory == null || hpHistory.Count == 0)
            {
                return "-";
            }

            var values = new string[hpHistory.Count];
            for (var index = 0; index < hpHistory.Count; index++)
            {
                values[index] = Math.Max(0, hpHistory[index]).ToString();
            }

            var text = string.Join("→", values);
            if (removed || hpHistory[hpHistory.Count - 1] <= 0)
            {
                return $"{text}, 처치됨";
            }

            return damagePrevented ? $"{text}, 방어됨" : text;
        }

        private static string ResolveCardName(string cardId, Func<string, string> resolver)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return "카드";
            }

            if (string.Equals(cardId, "Master", StringComparison.OrdinalIgnoreCase))
            {
                return "마스터";
            }

            var resolved = resolver?.Invoke(cardId);
            return string.IsNullOrWhiteSpace(resolved) ? cardId : resolved;
        }

        private static string FormatOwner(PlayerId ownerId) =>
            ownerId == PlayerId.Player ? "플레이어" : "상대방";

        private static string FormatActor(PlayerId ownerId) =>
            ownerId == PlayerId.Player ? "플레이어가" : "상대방이";

        private static string FormatPossessiveOwner(PlayerId ownerId) =>
            ownerId == PlayerId.Player ? "플레이어의" : "상대방의";

        private static string FormatCoord(TileCoordDto coord) =>
            coord == null ? "(-,-)" : $"({coord.Column},{coord.Row})";

        private static string FormatResource(BattleCombatLogResourceType resourceType)
        {
            return resourceType switch
            {
                BattleCombatLogResourceType.Mana => "마나",
                BattleCombatLogResourceType.Qi => "기",
                BattleCombatLogResourceType.Power => "전력",
                BattleCombatLogResourceType.Gold => "골드",
                _ => "자원"
            };
        }

        private static string FormatDamageType(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => "물리",
                DamageType.Magic => "마법",
                DamageType.Fixed => "고정",
                _ => "효과"
            };
        }

        private static string AppendSubjectParticle(string text) =>
            $"{text}{(HasFinalConsonant(text) ? "이" : "가")}";

        private static string AppendObjectParticle(string text) =>
            $"{text}{(HasFinalConsonant(text) ? "을" : "를")}";

        private static bool HasFinalConsonant(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            for (var index = text.Length - 1; index >= 0; index--)
            {
                var character = text[index];
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                return character >= '\uAC00' && character <= '\uD7A3' &&
                       (character - '\uAC00') % 28 != 0;
            }

            return false;
        }
    }
}
