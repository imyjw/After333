using Project333.Runtime.Domain.Cards;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Damage Spell Card", fileName = "DamageSpellCardDefinition")]
    public sealed class DamageSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private DamageType _damageType = DamageType.Magic;
        [SerializeField] private int _damage = 1;

        public override CardDefinition ToDefinition()
        {
            return new DamageSpellCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                damage: _damage,
                damageType: _damageType,
                hasReplicate: HasReplicate);
        }

        public void ConfigureForTests(int damage, DamageType damageType = DamageType.Magic)
        {
            _damage = damage;
            _damageType = damageType;
        }
    }
}
