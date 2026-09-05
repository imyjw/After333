using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class OccupantSpecialEffectTooltip
    {
        public OccupantSpecialEffectTooltip(string title, string description)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }
    }

    public static class OccupantSpecialEffectTooltipCatalog
    {
        public static IReadOnlyList<OccupantSpecialEffectTooltip> Build(
            CardDefinition definition,
            OccupantState occupant)
        {
            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            var entries = new List<OccupantSpecialEffectTooltip>();
            var powerUpkeep = GetPowerUpkeep(occupant);
            if (powerUpkeep > 0)
            {
                Add(
                    entries,
                    $"전력 -{powerUpkeep}",
                    $"내 턴 시작 시 전력 {powerUpkeep}을 소모, 부족하면 방전 상태이상에 진입합니다.");
            }

            if (occupant.HasBerserker)
            {
                Add(entries, "버서커", "잃은 HP 1당 ATK 1 증가");
            }

            if (occupant.HitsPerAttack > 1)
            {
                Add(
                    entries,
                    $"{occupant.HitsPerAttack}연타",
                    $"한 번 공격할 때 {occupant.HitsPerAttack}회 타격합니다(반격 제외).");
            }

            if (occupant.HasPiercing)
            {
                Add(
                    entries,
                    "관통",
                    "대상을 일반공격 시 같은 열의 반대편 대상에게도 피해를 입힙니다.");
            }

            if (occupant.MaxAttacksPerTurn > 1)
            {
                var additionalAttacks = occupant.MaxAttacksPerTurn - 1;
                Add(
                    entries,
                    $"추가 공격 {additionalAttacks}",
                    $"한 턴에 {occupant.MaxAttacksPerTurn}회 공격할 수 있습니다.");
            }

            if (occupant.HasEndure)
            {
                Add(
                    entries,
                    "불굴",
                    "처음 받는 치명적인 피해를 버티고 HP 1로 생존합니다.");
            }

            if (occupant.HasShielder)
            {
                Add(
                    entries,
                    "쉴더",
                    "바로 뒤의 아군이 받는 일반 공격과 단일 대상 마법 피해를 대신 받습니다. 초과한 피해는 원래 대상이 받습니다.");
            }

            if (occupant.HasLifeSteal)
            {
                Add(
                    entries,
                    "흡혈",
                    "공격·반격 후 생존하면 적에게 실제로 입힌 피해만큼 회복합니다.");
            }

            if (occupant.HasRush)
            {
                Add(entries, "속공", "소환한 턴에 즉시 공격할 수 있습니다.");
            }

            if (definition != null && definition.HasReplicate)
            {
                Add(
                    entries,
                    "복제",
                    "사용 후 원래 카드의 임시 사본을 손패에 추가합니다. 사본은 턴 종료 시 사라집니다.");
            }

            if (occupant.IsDrained)
            {
                Add(
                    entries,
                    "방전",
                    "공격·반격·이동·효과 발동·전열 차단 불가. 모든 방어력이 0이 되고, 받는 피해가 3배가 됩니다.");
            }

            if (occupant.IsErasure)
            {
                Add(entries, "망각", "카드의 효과와 버프·디버프가 무효화됩니다(봉인 제외).");
            }

            if (DemonKingRules.IsDemonKing(occupant))
            {
                var revivalDescription =
                    "사망 시 그 타일에서 봉인됩니다. 사망 당시 턴 플레이어의 다음 세 번째 턴 시작에 ATK/HP +33을 얻고 부활합니다.";
                if (occupant.IsDemonKingRevivalPending)
                {
                    revivalDescription +=
                        $"\n부활까지 남은 턴 시작: {Math.Max(0, occupant.DemonKingRevivalTurnStartsRemaining)}";
                }

                Add(entries, "마왕의 귀환", revivalDescription);
            }

            var sealboundTurns = GetSealboundOwnerTurnStarts(definition);
            if (sealboundTurns <= 0 && occupant.IsSealbound)
            {
                sealboundTurns = occupant.SealboundOwnerTurnStartsRemaining;
            }

            if (sealboundTurns > 0 && !occupant.IsDemonKingRevivalPending)
            {
                Add(
                    entries,
                    $"봉인 {sealboundTurns}",
                    $"내 턴 시작을 {sealboundTurns}번 맞을 때까지 봉인됩니다. 봉인 중 행동·대상 지정·효과 발동이 불가능하고 피해를 받지 않습니다.");
            }

            if (occupant.HasHiding)
            {
                Add(
                    entries,
                    "은신",
                    "상대의 일반 공격과 단일 대상 마법의 대상이 되지 않으며 전열을 막지 않습니다. 일반 공격 시 해제됩니다.");
            }

            if (occupant.IsUnderHuanShu)
            {
                Add(
                    entries,
                    "환술",
                    "일반 공격 시 공격 대상이 무작위로 변경됩니다.");
            }

            if (occupant.HasFlying)
            {
                Add(
                    entries,
                    "비행",
                    "원거리 유닛 또는 비행 소환물만 이 소환물을 일반 공격할 수 있습니다. 전열 차단을 무시하며, 전열차단을 하지 않습니다.");
            }

            if (occupant.SpellPower > 0)
            {
                Add(
                    entries,
                    $"주문력 +{occupant.SpellPower}",
                    $"마법 피해를 주는 마법 카드의 피해가 {occupant.SpellPower} 증가합니다. 지속 마법은 사용 시점의 주문력으로 고정됩니다.");
            }

            if (HeroRules.IsHero(occupant))
            {
                Add(
                    entries,
                    "용사의 성장",
                    $"매 턴 종료 시 ATK +{HeroRules.GrowthAmount} 또는 HP +{HeroRules.GrowthAmount} 중 하나를 무작위로 얻습니다.");
            }

            if (HasInvincible(definition, occupant))
            {
                var description = "물리·마법·고정 피해로 HP가 감소하지 않습니다.";
                var remainingTurnEnds = GetGlobalTurnEndsRemaining(definition, occupant);
                if (remainingTurnEnds > 0)
                {
                    description += $"\n남은 턴 종료: {remainingTurnEnds}";
                }

                Add(entries, "무적", description);
            }

            return entries;
        }

        public static int GetLeftColumnCount(int totalCount)
        {
            if (totalCount <= 0)
            {
                return 0;
            }

            return totalCount <= 2
                ? totalCount
                : (totalCount + 1) / 2;
        }

        private static int GetPowerUpkeep(OccupantState occupant)
        {
            return occupant switch
            {
                UnitState unit => Math.Max(0, unit.SciencePowerUpkeep),
                BuildingState building => Math.Max(0, building.SciencePowerUpkeep),
                _ => 0,
            };
        }

        private static int GetSealboundOwnerTurnStarts(CardDefinition definition)
        {
            return definition switch
            {
                UnitCardDefinition unit => unit.SealboundOwnerTurnStarts,
                BuildingCardDefinition building => building.SealboundOwnerTurnStarts,
                _ => 0,
            };
        }

        private static bool HasInvincible(CardDefinition definition, OccupantState occupant)
        {
            if (occupant.InvincibleEffects.Count > 0)
            {
                return true;
            }

            return definition switch
            {
                UnitCardDefinition unit => unit.InvincibleDuration != InvincibleDurationType.None,
                BuildingCardDefinition building => building.InvincibleDuration != InvincibleDurationType.None,
                _ => false,
            };
        }

        private static int GetGlobalTurnEndsRemaining(
            CardDefinition definition,
            OccupantState occupant)
        {
            foreach (var effect in occupant.InvincibleEffects)
            {
                if (effect.Duration == InvincibleDurationType.GlobalTurnEnds)
                {
                    return Math.Max(0, effect.OwnerTurnsRemaining);
                }
            }

            return definition switch
            {
                UnitCardDefinition unit
                    when unit.InvincibleDuration == InvincibleDurationType.GlobalTurnEnds =>
                    Math.Max(0, unit.InvincibleOwnerTurns),
                BuildingCardDefinition building
                    when building.InvincibleDuration == InvincibleDurationType.GlobalTurnEnds =>
                    Math.Max(0, building.InvincibleOwnerTurns),
                _ => 0,
            };
        }

        private static void Add(
            ICollection<OccupantSpecialEffectTooltip> entries,
            string title,
            string description)
        {
            entries.Add(new OccupantSpecialEffectTooltip(title, description));
        }
    }
}
