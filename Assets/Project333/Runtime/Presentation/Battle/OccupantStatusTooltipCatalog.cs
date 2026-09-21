using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Battle
{
    /// <summary>Read-only tooltip projection. The battle is already filtered for the current viewer.</summary>
    public static class OccupantStatusTooltipCatalog
    {
        public static IReadOnlyList<OccupantSpecialEffectTooltip> BuildAttached(
            CardDefinition definition, OccupantState occupant, BattleState battleState)
        {
            if (occupant == null) throw new ArgumentNullException(nameof(occupant));
            var entries = new List<OccupantSpecialEffectTooltip>();
            AddStatBuffs(entries, occupant);

            var berserkerBonus = occupant.HasBerserker && !occupant.EffectsSuppressed
                ? Math.Max(0, occupant.MaxHp - occupant.CurrentHp) : 0;
            if (berserkerBonus > 0)
                Add(entries, $"버서커: 공격력 +{berserkerBonus}", OccupantTooltipKind.Buff);
            var packBonus = battleState == null ? 0
                : WerewolfRules.CalculatePackAttackBonus(occupant, battleState.GetBoard(occupant.OwnerId));
            if (packBonus > 0)
                Add(entries, $"웨어울프: 공격력 +{packBonus}", OccupantTooltipKind.Buff);

            if (occupant.IsDrained)
                Add(entries, "방전", OccupantTooltipKind.Debuff);
            if (occupant.IsErasure)
                Add(entries, "망각", OccupantTooltipKind.Debuff);
            if (occupant.IsUnderHuanShu)
                Add(entries, "환술", OccupantTooltipKind.Debuff);
            if (occupant.FalloutStacks > 0)
                Add(entries, "낙진", OccupantTooltipKind.Debuff);

            AddDelayedDestruction(entries, occupant, battleState);
            if (occupant.IsDemonKingRevivalPending)
                Add(entries, "마왕 부활 대기", OccupantTooltipKind.State);
            else if (occupant.IsSealbound)
                Add(entries, "봉인", OccupantTooltipKind.State);
            if (occupant.InvincibleEffects.Count > 0)
                Add(entries, "무적", OccupantTooltipKind.Buff);
            return entries;
        }

        public static IReadOnlyList<OccupantSpecialEffectTooltip> BuildInfluences(
            CardDefinition definition, OccupantState occupant, BattleState battleState)
        {
            if (occupant == null) throw new ArgumentNullException(nameof(occupant));
            var entries = new List<OccupantSpecialEffectTooltip>();
            if (battleState == null) return entries;

            var board = battleState.GetBoard(occupant.OwnerId);
            var auraPower = 0;
            foreach (var ally in board.EnumerateOccupants()) auraPower += ally.EffectiveSpellPower;
            if (auraPower > 0)
                Add(entries, $"주문력 +{auraPower}", OccupantTooltipKind.Influence);

            var amplificationCount = 0;
            // Same area source shares one compact label; identical damage packets retain their count.
            var areas = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var effect in battleState.PersistentEffects)
            {
                if (effect == null || effect.IsExpired) continue;
                if (effect.EffectId == MagicAmplificationCircleRules.EffectId && effect.OwnerId == occupant.OwnerId)
                {
                    amplificationCount++;
                    continue;
                }
                if (effect.RemainingTriggers <= 0) continue;
                if (effect.EffectId == "firewall")
                {
                    var targetOwner = effect.TargetsOwnerBoard ? effect.OwnerId : battleState.GetOpponent(effect.OwnerId).Id;
                    if (targetOwner == occupant.OwnerId && effect.TargetRow == occupant.Position.Row)
                        Group(areas, "파이어월", AreaValue(battleState, effect));
                }
                else if (effect.EffectId == BiochemicalBombRules.EffectId &&
                         effect.OwnerId != occupant.OwnerId &&
                         (effect.TargetStartColumn == BiochemicalBombRules.LeftAreaStartColumn ||
                          effect.TargetStartColumn == BiochemicalBombRules.RightAreaStartColumn) &&
                         occupant.Position.Column >= effect.TargetStartColumn &&
                         occupant.Position.Column < effect.TargetStartColumn + BiochemicalBombRules.AreaWidth)
                    Group(areas, "생화학폭탄", AreaValue(battleState, effect));
                else if (effect.EffectId == TimedBombRules.EffectId && effect.OwnerId != occupant.OwnerId)
                    Group(areas, "시한폭탄", AreaValue(battleState, effect));
            }
            if (amplificationCount > 0)
                Add(entries, $"마법 증폭진: 주문력 +{amplificationCount * MagicAmplificationCircleRules.SpellPowerBonus}",
                    OccupantTooltipKind.Influence);
            foreach (var group in areas)
                Add(entries, group.Key + ": " + string.Join(", ", CombineIdenticalLines(group.Value)),
                    OccupantTooltipKind.Influence);
            if (board.FalloutStacks > 0 && occupant.Kind != OccupantKind.Master)
                Add(entries, "전장 낙진", OccupantTooltipKind.Influence);
            return entries;
        }

        private static void AddStatBuffs(List<OccupantSpecialEffectTooltip> entries, OccupantState occupant)
        {
            var trackedAttack = 0;
            var trackedHp = 0;
            var trackedPhysical = 0;
            var trackedMagic = 0;
            foreach (var buff in occupant.CombatStatBuffs)
            {
                trackedAttack += buff.AttackBonus;
                trackedHp += buff.MaxHpBonus;
                trackedPhysical += buff.PhysicalDefenseBonus;
                trackedMagic += buff.MagicDefenseBonus;
                var text = StatDescription(buff.AttackBonus, buff.MaxHpBonus, buff.PhysicalDefenseBonus, buff.MagicDefenseBonus);
                if (text.Length > 0)
                    Add(entries, SourceName(buff.SourceCardId) + ": " + text, OccupantTooltipKind.Buff);
            }
            var fallback = StatDescription(
                occupant.BaseAttack - occupant.OriginalAttack - trackedAttack,
                occupant.MaxHp - occupant.OriginalMaxHp - trackedHp,
                occupant.PhysicalDefense - occupant.OriginalPhysicalDefense - trackedPhysical,
                occupant.MagicDefense - occupant.OriginalMagicDefense - trackedMagic);
            if (fallback.Length > 0)
                Add(entries, "능력치 변화: " + fallback, OccupantTooltipKind.State);
        }

        private static string StatDescription(int attack, int maxHp, int physical, int magic)
        {
            var values = new List<string>();
            if (attack != 0) values.Add("공격력 " + Signed(attack));
            if (maxHp != 0) values.Add("체력 " + Signed(maxHp));
            if (physical != 0) values.Add("물리 방어력 " + Signed(physical));
            if (magic != 0) values.Add("마법 방어력 " + Signed(magic));
            return string.Join(", ", values);
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static string SourceName(string sourceCardId)
        {
            return sourceCardId switch
            {
                "Hero" => "용사",
                "RobotFusion" => "로봇 합체",
                "Daehwandan" => "대환단",
                "DemonKing" => "마왕 부활",
                _ => string.IsNullOrWhiteSpace(sourceCardId) ? "능력치 변화" : sourceCardId,
            };
        }

        private static void AddDelayedDestruction(List<OccupantSpecialEffectTooltip> entries, OccupantState occupant, BattleState battle)
        {
            if (battle == null) return;
            foreach (var effect in battle.PersistentEffects)
            {
                if (effect == null || effect.IsExpired || effect.EffectId != "cheonra_jimang" ||
                    effect.TargetRuntimeId != occupant.RuntimeId) continue;
                Add(entries, "천라지망", OccupantTooltipKind.Debuff);
                return;
            }
        }

        private static string AreaValue(BattleState battle, PersistentEffectState effect)
        {
            var damage = SpellPowerRules.ApplyCurrent(battle, effect.OwnerId, effect.EffectDamage, effect.EffectDamageType);
            var damageType = effect.EffectDamageType switch
            {
                DamageType.Magic => "마법",
                DamageType.Fixed => "고정",
                DamageType.Physical => "물리",
                _ => string.Empty,
            };
            return $"{damageType} 피해 {damage}";
        }

        private static void Group(Dictionary<string, List<string>> groups, string title, string line)
        {
            if (!groups.TryGetValue(title, out var lines))
                groups.Add(title, lines = new List<string>());
            lines.Add(line);
        }

        private static List<string> CombineIdenticalLines(List<string> lines)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var line in lines) counts[line] = counts.TryGetValue(line, out var count) ? count + 1 : 1;
            var result = new List<string>();
            foreach (var pair in counts) result.Add(pair.Key + (pair.Value > 1 ? $" ×{pair.Value}" : string.Empty));
            return result;
        }

        private static void Add(List<OccupantSpecialEffectTooltip> entries, string title, OccupantTooltipKind kind)
            => entries.Add(new OccupantSpecialEffectTooltip(title, string.Empty, kind));
    }
}
