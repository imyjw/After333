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
                default:
                    return false;
            }

            damage = SpellPowerRules.ApplyCaptured(
                CardLevelSpellRules.ApplyDamageBonus(baseDamage, upgradeLevel), damageType, spellPower);
            return true;
        }
    }
}
