using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    /// <summary>Shared hand-use classification. New targetless spells automatically use the common card-use presentation.</summary>
    public static class SpellCardUseRules
    {
        public static bool IsUntargeted(CardDefinition definition)
        {
            if (definition == null) return false;
            if (definition.HasTrap || definition is PersistentResourceSpellCardDefinition) return true;
            return definition is ScriptedSpellCardDefinition spell &&
                spell.EffectId != RobotFusionRules.EffectId && !RequiresBoardTarget(spell);
        }

        public static bool RequiresBoardTarget(ScriptedSpellCardDefinition spell)
        {
            if (spell == null) return false;
            switch (spell.EffectId)
            {
                case "cheonra_jimang":
                case "firewall":
                case FireSpearRules.EffectId:
                case PlasmaShieldRules.EffectId:
                case ManaShieldRules.EffectId:
                case BiochemicalBombRules.EffectId:
                case GuRules.EffectId:
                case HuanShuRules.EffectId:
                    return true;
                default:
                    return false;
            }
        }
    }
}
