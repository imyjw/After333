using NUnit.Framework;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class ScriptedSpellCardDefinitionAssetTests
    {
        [Test]
        public void ToDefinition_MapsScriptedSpellEffectIdAndCost()
        {
            var asset = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            asset.ConfigureBaseForTests("scripted-spell", "Scripted Spell", new ResourceSetData(0, 5, 0, 2));
            asset.ConfigureForTests("scripted_effect");

            var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();

            Assert.That(definition.CardId, Is.EqualTo("scripted-spell"));
            Assert.That(definition.EffectId, Is.EqualTo("scripted_effect"));
            Assert.That(definition.Cost.Qi, Is.EqualTo(5));
            Assert.That(definition.Cost.Gold, Is.EqualTo(2));
        }
    }
}
