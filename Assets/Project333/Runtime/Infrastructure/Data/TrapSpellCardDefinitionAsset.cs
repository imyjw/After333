using System;
using Project333.Runtime.Domain.Effects;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Trap Spell Card", fileName = "TrapSpellCardDefinition")]
    public sealed class TrapSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private ResourceSetData _activationCost;
        [SerializeField] private TrapTriggerType _triggerType;
        [SerializeField] private TrapEffectType _trapEffectType;
        [SerializeField] private int _damage;

        public override bool HasTrap => true;
        public override ResourceSetData Cost => new ResourceSetData();
        public ResourceSetData ActivationCost => _activationCost;
        public TrapTriggerType TriggerType => _triggerType;
        public TrapEffectType TrapEffectType => _trapEffectType;
        public int Damage => _damage;
        public override string SpecialEffectText
        {
            get
            {
                var text = base.SpecialEffectText;
                return text.IndexOf("트랩", StringComparison.Ordinal) >= 0 || text.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0
                    ? text : string.IsNullOrWhiteSpace(text) ? "트랩" : text + Environment.NewLine + "트랩";
            }
        }

        public override CardDefinition ToDefinition() => new TrapSpellCardDefinition(CardId, DisplayName,
            _activationCost.ToRuntime(), _triggerType, _trapEffectType, HasReplicate, _damage);

        public void ConfigureTrapForTests(ResourceSetData activationCost, TrapTriggerType triggerType, TrapEffectType effectType, int damage = 0)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            _damage = damage;
            _activationCost = activationCost;
            _triggerType = triggerType;
            _trapEffectType = effectType;
        }
    }
}
