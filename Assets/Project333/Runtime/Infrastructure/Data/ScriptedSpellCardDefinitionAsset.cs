using UnityEngine;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Scripted Spell Card", fileName = "ScriptedSpellCardDefinition")]
    public sealed class ScriptedSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private string _effectId;
        [SerializeField] private int _damage;
        [SerializeField] private DamageType _damageType = DamageType.None;
        [SerializeField] private int _triggerCount;

        public string EffectId => _effectId;

        public override CardDefinition ToDefinition()
        {
            return new ScriptedSpellCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                effectId: _effectId,
                damage: _damage,
                damageType: _damageType,
                triggerCount: _triggerCount,
                hasReplicate: HasReplicate);
        }

        public void ConfigureForTests(
            string effectId,
            int damage = 0,
            DamageType damageType = DamageType.None,
            int triggerCount = 0)
        {
            _effectId = effectId;
            _damage = damage;
            _damageType = damageType;
            _triggerCount = triggerCount;
        }
    }
}
