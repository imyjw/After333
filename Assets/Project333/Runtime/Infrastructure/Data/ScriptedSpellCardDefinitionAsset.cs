using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Scripted Spell Card", fileName = "ScriptedSpellCardDefinition")]
    public sealed class ScriptedSpellCardDefinitionAsset : CardDefinitionAsset
    {
        [SerializeField] private string _effectId;

        public string EffectId => _effectId;

        public override CardDefinition ToDefinition()
        {
            return new ScriptedSpellCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                effectId: _effectId);
        }

        public void ConfigureForTests(string effectId)
        {
            _effectId = effectId;
        }
    }
}
