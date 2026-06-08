using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Building Card", fileName = "BuildingCardDefinition")]
    public sealed class BuildingCardDefinitionAsset : CardDefinitionAsset
    {
        [Header("Combat")]
        [SerializeField] private bool _canAttack;
        [SerializeField] private int _attack;
        [SerializeField] private int _health = 1;
        [Header("Slice Effects")]
        [SerializeField] private ResourceSetData _turnStartResourceGain;
        [SerializeField] private bool _canAttackOnSummon;

        public override CardDefinition ToDefinition()
        {
            return new BuildingCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                canAttack: _canAttack,
                attack: _attack,
                health: _health,
                turnStartResourceGain: _turnStartResourceGain.ToRuntime(),
                canAttackOnSummon: _canAttackOnSummon);
        }

        public void ConfigureForTests(
            bool canAttack,
            int attack,
            int health,
            ResourceSetData turnStartResourceGain,
            bool canAttackOnSummon)
        {
            _canAttack = canAttack;
            _attack = attack;
            _health = health;
            _turnStartResourceGain = turnStartResourceGain;
            _canAttackOnSummon = canAttackOnSummon;
        }
    }
}
