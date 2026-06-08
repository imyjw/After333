using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Damage Spell Card", fileName = "DamageSpellCardDefinition")]
    public sealed class DamageSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private int _damage = 1;

        public override CardDefinition ToDefinition()
        {
            return new DamageSpellCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                damage: _damage);
        }

        public void ConfigureForTests(int damage)
        {
            _damage = damage;
        }
    }
}
