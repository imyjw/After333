using System.Text.RegularExpressions;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public readonly struct CardStatDisplay
    {
        private CardStatDisplay(int attack, int hp, bool hasHp)
        {
            Attack = attack;
            Hp = hp;
            HasHp = hasHp;
        }

        // The lower-left badge shows attack for occupants or damage per hit/tick for spells.
        public int Attack { get; }
        public int Hp { get; }
        public bool HasHp { get; }

        public static bool TryCreate(CardDefinition definition, int upgradeLevel,
            out CardStatDisplay stats, int spellPower = 0)
        {
            stats = default;
            switch (definition)
            {
                case UnitCardDefinition unit:
                    stats = new CardStatDisplay(
                        CardLevelStatRules.ApplyAttackBonus(unit.CardId, unit.Attack, upgradeLevel),
                        CardLevelStatRules.ApplyHpBonus(unit.CardId, unit.Health, upgradeLevel), true);
                    return true;
                case BuildingCardDefinition building:
                    stats = new CardStatDisplay(
                        CardLevelStatRules.ApplyAttackBonus(building.CardId, building.Attack, upgradeLevel),
                        CardLevelStatRules.ApplyHpBonus(building.CardId, building.Health, upgradeLevel), true);
                    return true;
                default:
                    if (!TryGetSpellDamage(definition, upgradeLevel, spellPower, out var damage))
                        return false;
                    stats = new CardStatDisplay(damage, 0, false);
                    return true;
            }
        }

        public static bool TryGetSpellDamage(CardDefinition definition, int upgradeLevel,
            int spellPower, out int damage)
        {
            damage = 0;
            int baseDamage;
            DamageType damageType;
            switch (definition)
            {
                case DamageSpellCardDefinition spell when spell.DamageType != DamageType.None:
                    baseDamage = spell.Damage;
                    damageType = spell.DamageType;
                    break;
                case ScriptedSpellCardDefinition spell when spell.Damage > 0 && spell.DamageType != DamageType.None:
                    baseDamage = spell.Damage;
                    damageType = spell.DamageType;
                    break;
                case TrapSpellCardDefinition trap when trap.EffectType == Project333.Runtime.Domain.Effects.TrapEffectType.DamageSummonedUnitPhysical:
                    baseDamage = trap.Damage;
                    damageType = DamageType.Physical;
                    break;
                default:
                    return false;
            }

            damage = SpellPowerRules.AddBonus(
                CardLevelSpellRules.ApplyDamageBonus(baseDamage, upgradeLevel), damageType, spellPower);
            return true;
        }

        public static string GetEffectText(CardDefinition definition, string baseText, int upgradeLevel)
        {
            var powerConsumption = definition is UnitCardDefinition unit ? unit.SciencePowerUpkeep :
                definition is BuildingCardDefinition building ? building.SciencePowerUpkeep : 0;
            if (powerConsumption > 0 && !string.IsNullOrEmpty(baseText))
            {
                // Server/native legacy metadata may still have the old upkeep name; don't rewrite cost sentences.
                baseText = Regex.Replace(baseText,
                    @"(^|(?<=[,\n]))([ \t]*)전력[ \t]*(?:소모[ \t]*:[ \t]*|-)[ \t]*[0-9]+[ \t]*(?=$|[,\r\n])",
                    "$2전력 소모:" + powerConsumption, RegexOptions.Multiline);
            }
            if (definition is ScriptedSpellCardDefinition fusion && fusion.CardId == RobotFusionRules.CardId &&
                fusion.EffectId == RobotFusionRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                baseText = baseText.Replace("전력 유지비", "전력 소모");
            }
            if (definition is TrapSpellCardDefinition trap && trap.CardId == SummonTrapCardRules.CardId &&
                trap.EffectType == Project333.Runtime.Domain.Effects.TrapEffectType.DamageSummonedUnitPhysical && !string.IsNullOrEmpty(baseText))
            {
                var trapDamage = CardLevelSpellRules.ApplyDamageBonus(trap.Damage, upgradeLevel);
                return baseText.Replace("물리 데미지 " + trap.Damage, "물리 데미지 " + trapDamage);
            }
            if (definition is ScriptedSpellCardDefinition daehwandan && daehwandan.CardId == DaehwandanCardRules.CardId &&
                daehwandan.EffectId == DaehwandanCardRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var bonus = DaehwandanCardRules.GetAttackBonus(daehwandan.EffectAmount, upgradeLevel);
                return $"사용 시 내 마스터의 공격력을 {bonus} 증가시킨다.";
            }
            if (definition is ScriptedSpellCardDefinition manaShield && manaShield.CardId == ManaShieldRules.CardId &&
                manaShield.EffectId == ManaShieldRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var bonus = ManaShieldRules.GetDefenseBonus(manaShield.EffectAmount, upgradeLevel);
                return baseText.Replace("영구적으로 " + manaShield.EffectAmount + " 증가", "영구적으로 " + bonus + " 증가");
            }
            if (definition is ScriptedSpellCardDefinition shield && shield.CardId == PlasmaShieldRules.CardId &&
                shield.EffectId == PlasmaShieldRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var bonus = PlasmaShieldRules.GetDefenseBonus(shield.EffectAmount, upgradeLevel);
                return baseText.Replace("영구적으로 " + shield.EffectAmount + " 증가", "영구적으로 " + bonus + " 증가");
            }
            if (definition is ScriptedSpellCardDefinition fireSpear && fireSpear.CardId == FireSpearRules.CardId &&
                fireSpear.EffectId == FireSpearRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var fireSpearDamage = CardLevelSpellRules.ApplyDamageBonus(fireSpear.Damage, upgradeLevel);
                return baseText.Replace("마법 데미지 " + fireSpear.Damage, "마법 데미지 " + fireSpearDamage);
            }
            if (definition is ScriptedSpellCardDefinition meteor && meteor.CardId == MeteorRules.CardId &&
                meteor.EffectId == MeteorRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var meteorDamage = CardLevelSpellRules.ApplyDamageBonus(meteor.Damage, upgradeLevel);
                return baseText.Replace("마법 데미지 " + meteor.Damage, "마법 데미지 " + meteorDamage);
            }
            if (definition is ScriptedSpellCardDefinition earthquake && earthquake.CardId == EarthquakeRules.CardId &&
                earthquake.EffectId == EarthquakeRules.EffectId && !string.IsNullOrEmpty(baseText))
            {
                var earthquakeDamage = CardLevelSpellRules.ApplyDamageBonus(earthquake.Damage, upgradeLevel);
                return baseText.Replace("물리 데미지 " + earthquake.Damage, "물리 데미지 " + earthquakeDamage);
            }
            if (!(definition is ScriptedSpellCardDefinition spell) || spell.CardId != NuclearRules.CardId ||
                spell.EffectId != NuclearRules.EffectId || string.IsNullOrEmpty(baseText))
                return baseText;

            // Replace the immediate hit's complete phrase, preserving the separate +1 stack and 10/tick.
            var damage = CardLevelSpellRules.ApplyDamageBonus(spell.Damage, upgradeLevel);
            return baseText.Replace("고정 데미지 " + spell.Damage, "고정 데미지 " + damage);
        }
    }
}
