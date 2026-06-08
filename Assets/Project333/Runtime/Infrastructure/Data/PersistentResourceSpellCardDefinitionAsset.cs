using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Persistent Resource Spell Card", fileName = "PersistentResourceSpellCardDefinition")]
    public sealed class PersistentResourceSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private string _effectId;
        [SerializeField] private ResourceSetData _turnStartResourceGain;
        [Min(0)]
        [SerializeField] private int _ownerTurnStartsRemaining;
        [TextArea(2, 4)]
        [SerializeField] private string _endConditionText;

        public override CardDefinition ToDefinition()
        {
            return new PersistentResourceSpellCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                effectId: _effectId,
                turnStartResourceGain: _turnStartResourceGain.ToRuntime(),
                endConditionText: _endConditionText,
                ownerTurnStartsRemaining: _ownerTurnStartsRemaining);
        }

        public void ConfigureForTests(
            string effectId,
            ResourceSetData turnStartResourceGain,
            string endConditionText,
            int ownerTurnStartsRemaining = 0)
        {
            _effectId = effectId;
            _turnStartResourceGain = turnStartResourceGain;
            _endConditionText = endConditionText;
            _ownerTurnStartsRemaining = ownerTurnStartsRemaining;
        }
    }
}
