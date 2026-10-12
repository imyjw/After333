using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Battle
{
    public enum OccupantTooltipKind
    {
        Ability,
        Buff,
        Debuff,
        State,
        Influence,
    }

    public sealed class OccupantSpecialEffectTooltip
    {
        public OccupantSpecialEffectTooltip(string title, string description, OccupantTooltipKind kind = OccupantTooltipKind.Ability)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Kind = kind;
        }

        public OccupantTooltipKind Kind { get; }

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
            if (Project333.Runtime.Application.Services.ActiveEffectService.HasActiveEffect(occupant))
                Add(entries, "액티브", "소환물의 액티브 버튼을 눌러 액티브 효과를 발동시킬 수 있다.");
            var powerUpkeep = GetPowerUpkeep(occupant);
            if (powerUpkeep > 0)
            {
                Add(
                    entries,
                    $"전력소모: {powerUpkeep}",
                    $"내 턴 시작 시 {powerUpkeep}의 전력을 지불해야한다. 지불하지 못하면 '방전' 디버프가 부여된다.");
                // This describes a possible result of the innate rule, not a current status.
                Add(entries, "방전",
                    "공격·반격·액티브 발동·이동·전열 차단이 불가능하고 카드 효과와 특수효과가 무효화된다. 물리·마법 방어력을 0으로 취급하며 받는 데미지가 3배가 된다.");
            }

            if (occupant.HasBerserker)
            {
                Add(entries, "버서커", "잃은 HP 1당 공격력이 1 증가한다.");
            }

            if (occupant.HitsPerAttack > 1)
            {
                Add(
                    entries,
                    $"{occupant.HitsPerAttack}연타",
                    $"공격·반격 시 {occupant.HitsPerAttack}번 데미지를 가한다.");
            }

            if (occupant.HasPiercing)
            {
                Add(
                    entries,
                    "관통",
                    "일반공격 시 대상과 같은 열의 모두에게 피해를 입힌다.");
            }

            if (occupant.MaxAttacksPerTurn > 1)
            {
                var additionalAttacks = occupant.MaxAttacksPerTurn - 1;
                Add(
                    entries,
                    $"추가 공격 {additionalAttacks}",
                    $"한 턴에 {occupant.MaxAttacksPerTurn}회 공격할 수 있다.");
            }

            if (occupant.HasEndure)
            {
                Add(
                    entries,
                    "불굴",
                    "처음 받는 치명적인 피해를 버티고 HP 1로 생존한다.");
            }

            if (occupant.HasShielder)
            {
                Add(
                    entries,
                    "쉴더",
                    "바로 뒤의 아군이 받는 일반 공격과 단일 대상 스펠의 피해를 대신 받는다. 초과한 피해는 원래 대상이 받는다.");
            }

            if (occupant.HasLifeSteal)
            {
                Add(
                    entries,
                    "흡혈",
                    "공격·반격 후 생존하면 적에게 실제로 입힌 피해만큼 회복한다.");
            }

            if (occupant.HasRush)
            {
                Add(entries, "속공", "소환한 턴에 즉시 공격할 수 있다.");
            }

            if (definition != null && definition.HasReplicate)
            {
                Add(
                    entries,
                    "복제",
                    "사용 후 원래 카드의 임시 사본을 손패에 추가한다. 사본은 턴 종료 시 사라진다.");
            }

            if (DemonKingRules.IsDemonKing(occupant))
            {
                var revivalDescription =
                    "사망 시 그 타일에서 봉인된다. 사망 당시 턴 플레이어의 다음 세 번째 턴 시작에 ATK/HP +33을 얻고 부활한다.";
                Add(entries, "마왕의 귀환", revivalDescription);
            }

            var sealboundTurns = GetSealboundOwnerTurnStarts(definition);
            if (sealboundTurns > 0)
            {
                Add(
                    entries,
                    $"봉인 {sealboundTurns}",
                    $"내 턴 시작을 {sealboundTurns}번 맞을 때까지 봉인된다. 봉인 중 행동·대상 지정·효과 발동이 불가능하고 피해를 받지 않는다.");
            }

            if (occupant.HasHiding)
            {
                Add(
                    entries,
                    "은신",
                    "상대의 일반 공격과 단일 대상 마법의 대상이 되지 않으며 전열을 막지 않는다. 은신 상태에서 일반 공격 시 해당 공격의 모든 타격이 치명타가 되고 은신이 해제된다. 피해를 받으면 은신이 해제된다.");
            }

            if (occupant.HasFlying)
            {
                Add(
                    entries,
                    "비행",
                    "비행이 없는 근접 유닛의 일반 공격 데미지를 절반만 받는다. 전열 차단을 무시하며, 전열 차단을 하지 않는다.");
            }

            if (occupant.SpellPower > 0)
            {
                Add(
                    entries,
                    $"주문력 +{occupant.SpellPower}",
                    "마법 데미지를 입힐 때, 주문력만큼의 추가 마법 데미지를 준다.");
            }

            if (HasInnateInvincible(definition))
            {
                Add(entries, "무적", "물리·마법·고정 피해로 HP가 감소하지 않는다.");
            }

            return entries;
        }

        // Cards outside the battlefield have innate rules but no occupant status.
        public static IReadOnlyList<OccupantSpecialEffectTooltip> Build(CardDefinition definition)
        {
            if (definition is UnitCardDefinition unit)
                return Build(definition, new UnitState("preview", unit.CardId,
                    Project333.Runtime.Domain.Battle.PlayerId.Player, new Project333.Runtime.Domain.Board.TileCoord(0, 0),
                    unit.AttackType, unit.Attack, unit.Health, unit.CanMove, unit.IsScience, unit.SciencePowerUpkeep,
                    maxAttacksPerTurn: unit.MaxAttacksPerTurn, hitsPerAttack: unit.HitsPerAttack,
                    hasBerserker: unit.HasBerserker, hasEndure: unit.HasEndure, hasShielder: unit.HasShielder,
                    hasLifeSteal: unit.HasLifeSteal, hasRush: unit.HasRush, hasHiding: unit.HasHiding,
                    hasFlying: unit.HasFlying, spellPower: unit.SpellPower, hasPiercing: unit.HasPiercing));
            if (definition is BuildingCardDefinition building)
                return Build(definition, new BuildingState("preview", building.CardId,
                    Project333.Runtime.Domain.Battle.PlayerId.Player, new Project333.Runtime.Domain.Board.TileCoord(0, 0),
                    building.CanAttack, building.Attack, building.Health, sciencePowerUpkeep: building.SciencePowerUpkeep,
                    hasFlying: building.HasFlying, spellPower: building.SpellPower, hasPiercing: building.HasPiercing));
            var entries = new List<OccupantSpecialEffectTooltip>();
            if (definition is ScriptedSpellCardDefinition spell &&
                spell.CardId == NuclearRules.CardId && spell.EffectId == NuclearRules.EffectId)
                Add(entries, "낙진", "양쪽 턴 시작마다 10의 고정 피해를 받는다.");
            if (definition != null && definition.HasReplicate)
                Add(entries, "복제", "사용 후 원래 카드의 임시 사본을 손패에 추가한다. 사본은 턴 종료 시 사라진다.");
            if (definition != null && definition.HasTrap)
                Add(entries, "트랩", "무료로 설치한다. 조건 충족 시 발동 비용을 지불하고 한 번 발동한다. 비용이 부족하면 설치 상태를 유지한다. 발동 전에는 상대에게 종류가 공개되지 않는다. 같은 트랩은 동시에 하나만 설치할 수 있다.");
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

        private static bool HasInnateInvincible(CardDefinition definition)
        {
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
