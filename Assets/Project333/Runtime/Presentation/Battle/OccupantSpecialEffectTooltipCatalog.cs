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
                    "처음 받는 치명적인 일반 공격·반격 피해를 버티고 HP 1로 생존합니다.");
            }

            if (occupant.HasGuard)
            {
                Add(
                    entries,
                    "가드",
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

            var sealboundTurns = GetSealboundOwnerTurnStarts(definition);
            if (sealboundTurns <= 0 && occupant.IsSealbound)
            {
                sealboundTurns = occupant.SealboundOwnerTurnStartsRemaining;
            }

            if (sealboundTurns > 0)
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

            if (HasInvincible(definition, occupant))
            {
                Add(entries, "무적", "물리·마법·고정 피해로 HP가 감소하지 않습니다.");
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

        private static void Add(
            ICollection<OccupantSpecialEffectTooltip> entries,
            string title,
            string description)
        {
            entries.Add(new OccupantSpecialEffectTooltip(title, description));
        }
    }
}
