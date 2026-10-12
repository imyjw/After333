using System;
using System.Collections.Generic;
using System.Globalization;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.OwnedCards
{
    /// <summary>Printed upgrade increments, independent of the owned copy's current level and battle bonuses.</summary>
    public sealed class OwnedCardsUpgradeTableData
    {
        private OwnedCardsUpgradeTableData(bool isUpgradeable, string emptyMessage,
            string effectLabel, OwnedCardUpgradeRow[] rows)
        {
            IsUpgradeable = isUpgradeable;
            EmptyMessage = emptyMessage;
            EffectLabel = effectLabel;
            ColumnHeaders = Array.AsReadOnly(new[] { "레벨", "HP", "ATK",
                string.IsNullOrEmpty(effectLabel) ? "효과" : effectLabel });
            Rows = Array.AsReadOnly(rows);
        }

        public string Title => "레벨별 강화 증가량";
        public bool IsUpgradeable { get; }
        public string EmptyMessage { get; }
        public string EffectLabel { get; }
        public IReadOnlyList<string> ColumnHeaders { get; }
        public IReadOnlyList<OwnedCardUpgradeRow> Rows { get; }

        public static OwnedCardsUpgradeTableData Build(CardDefinition definition)
        {
            if (definition == null)
                return Empty(false, "강화 정보를 확인할 수 없습니다.");
            if (!CardUpgradeRules.IsCardUpgradeable(definition))
                return Empty(false, "강화 불가");

            var rows = new OwnedCardUpgradeRow[CardUpgradeRules.MaxLevel];
            if (definition is UnitCardDefinition || definition is BuildingCardDefinition)
            {
                CardStatDisplay.TryCreate(definition, 0, out var previous);
                for (var level = 1; level <= CardUpgradeRules.MaxLevel; level++)
                {
                    CardStatDisplay.TryCreate(definition, level, out var current);
                    rows[level - 1] = new OwnedCardUpgradeRow(level,
                        current.Hp - previous.Hp, current.Attack - previous.Attack, 0);
                    previous = current;
                }
                return new OwnedCardsUpgradeTableData(true, string.Empty, string.Empty, rows);
            }

            if (!TryGetEffectValue(definition, 0, out var previousEffect, out var effectLabel))
                return Empty(true, "강화 정보 없음");
            for (var level = 1; level <= CardUpgradeRules.MaxLevel; level++)
            {
                TryGetEffectValue(definition, level, out var currentEffect, out _);
                rows[level - 1] = new OwnedCardUpgradeRow(level, 0, 0, currentEffect - previousEffect);
                previousEffect = currentEffect;
            }
            return new OwnedCardsUpgradeTableData(true, string.Empty, effectLabel, rows);
        }

        private static OwnedCardsUpgradeTableData Empty(bool isUpgradeable, string message)
        {
            return new OwnedCardsUpgradeTableData(isUpgradeable, message, string.Empty,
                Array.Empty<OwnedCardUpgradeRow>());
        }

        private static bool TryGetEffectValue(CardDefinition definition, int level,
            out int value, out string label)
        {
            value = 0;
            label = string.Empty;
            if (definition is TrapSpellCardDefinition trap && trap.CardId == SummonTrapCardRules.CardId &&
                trap.EffectType == Project333.Runtime.Domain.Effects.TrapEffectType.DamageSummonedUnitPhysical)
            {
                value = CardLevelSpellRules.ApplyDamageBonus(trap.Damage, level);
                label = "물리 피해";
                return true;
            }
            if (definition is ScriptedSpellCardDefinition scripted)
            {
                if (scripted.CardId == DaehwandanCardRules.CardId && scripted.EffectId == DaehwandanCardRules.EffectId)
                {
                    value = DaehwandanCardRules.GetAttackBonus(scripted.EffectAmount, level);
                    label = "마스터 공격력 증가량";
                    return true;
                }
                // A shield's non-damage magnitude must never appear as spell damage.
                if (scripted.CardId == PlasmaShieldRules.CardId &&
                    scripted.EffectId == PlasmaShieldRules.EffectId)
                {
                    value = PlasmaShieldRules.GetDefenseBonus(scripted.EffectAmount, level);
                    label = "물리 방어력";
                    return true;
                }
                if (scripted.CardId == ManaShieldRules.CardId &&
                    scripted.EffectId == ManaShieldRules.EffectId)
                {
                    value = ManaShieldRules.GetDefenseBonus(scripted.EffectAmount, level);
                    label = "마법 방어력";
                    return true;
                }

                // Whitelisted upgrade eligibility alone is not an implementation of an unknown effect.
                if (!IsSupportedScriptedDamage(scripted.EffectId))
                    return false;
            }
            else if (!(definition is DamageSpellCardDefinition))
            {
                return false;
            }

            if (!CardStatDisplay.TryGetSpellDamage(definition, level, 0, out value))
                return false;

            var damageType = definition is DamageSpellCardDefinition damageSpell
                ? damageSpell.DamageType : ((ScriptedSpellCardDefinition)definition).DamageType;
            switch (damageType)
            {
                case DamageType.Physical: label = "물리 피해"; break;
                case DamageType.Magic: label = "마법 피해"; break;
                case DamageType.Fixed: label = "고정 피해"; break;
                default: return false;
            }
            return true;
        }

        private static bool IsSupportedScriptedDamage(string effectId)
        {
            switch (effectId)
            {
                case "firewall":
                case BiochemicalBombRules.EffectId:
                case NuclearRules.EffectId:
                case EarthquakeRules.EffectId:
                case MeteorRules.EffectId:
                case FireSpearRules.EffectId:
                case TimedBombRules.EffectId:
                    return true;
                default:
                    return false;
            }
        }
    }

    public sealed class OwnedCardUpgradeRow
    {
        internal OwnedCardUpgradeRow(int level, int hpIncrease, int attackIncrease, int effectIncrease)
        {
            Level = level;
            HpIncrease = hpIncrease;
            AttackIncrease = attackIncrease;
            EffectIncrease = effectIncrease;
        }

        public int Level { get; }
        public int HpIncrease { get; }
        public int AttackIncrease { get; }
        public int EffectIncrease { get; }
        public string HpText => FormatIncrease(HpIncrease);
        public string AttackText => FormatIncrease(AttackIncrease);
        public string EffectText => FormatIncrease(EffectIncrease);

        private static string FormatIncrease(int increase)
        {
            return increase == 0 ? "—" :
                (increase > 0 ? "+" : string.Empty) + increase.ToString(CultureInfo.InvariantCulture);
        }
    }
}
