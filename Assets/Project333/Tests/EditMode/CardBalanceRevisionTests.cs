using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class CardBalanceRevisionTests
    {
        private const string CardsRoot = "Assets/Project333/ScriptableObjects/StarterTen/Cards/";
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(
            File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));

        [TestCase("A-212", 6, 30, 0, 0, 4, 0)]
        [TestCase("Shieldbearer", 0, 40, 0, 0, 0, 2)]
        [TestCase("RedDragon", 50, 40, 10, 0, 0, 0)]
        [TestCase("Zombie", 20, 1, 1, 0, 0, 0)]
        [TestCase("Shaolin_1st_Disciple", 10, 40, 0, 2, 0, 0)]
        [TestCase("Goblin", 20, 20, 2, 0, 0, 0)]
        public void SharedAndNativeUnitDefinitionsAgreeWithTheRevisedBalance(
            string cardId, int attack, int health, int mana, int qi, int power, int gold)
        {
            var shared = (UnitCardDefinition)Database().CreateProvider().GetRequired(cardId);
            var asset = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardsRoot + cardId + ".asset");
            Assert.That(asset, Is.Not.Null, cardId);
            var native = (UnitCardDefinition)asset.ToDefinition();
            foreach (var definition in new[] { shared, native })
            {
                Assert.That(definition.Attack, Is.EqualTo(attack), cardId + " attack");
                Assert.That(definition.Health, Is.EqualTo(health), cardId + " health");
                Assert.That(definition.Cost.Mana, Is.EqualTo(mana), cardId + " mana");
                Assert.That(definition.Cost.Qi, Is.EqualTo(qi), cardId + " qi");
                Assert.That(definition.Cost.Power, Is.EqualTo(power), cardId + " power");
                Assert.That(definition.Cost.Gold, Is.EqualTo(gold), cardId + " gold");
            }
        }

        [TestCase("FireSpear", 30, 3, 0)]
        [TestCase("Firewall", 15, 3, 0)]
        [TestCase("Nuclear", 100, 0, 13)]
        public void SharedAndNativeSpellDefinitionsAgreeWithTheRevisedBalance(
            string cardId, int damage, int mana, int power)
        {
            var shared = (ScriptedSpellCardDefinition)Database().CreateProvider().GetRequired(cardId);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardsRoot + cardId + ".asset");
            Assert.That(asset, Is.Not.Null, cardId);
            var native = (ScriptedSpellCardDefinition)asset.ToDefinition();
            foreach (var definition in new[] { shared, native })
            {
                Assert.That(definition.Damage, Is.EqualTo(damage), cardId + " damage");
                Assert.That(definition.Cost.Mana, Is.EqualTo(mana), cardId + " mana");
                Assert.That(definition.Cost.Power, Is.EqualTo(power), cardId + " power");
                Assert.That(definition.Cost.Qi + definition.Cost.Gold, Is.Zero, cardId + " other costs");
            }
            Assert.That(native.EffectId, Is.EqualTo(shared.EffectId));
            Assert.That(native.DamageType, Is.EqualTo(shared.DamageType));
            Assert.That(native.TriggerCount, Is.EqualTo(shared.TriggerCount));
        }

        [TestCase("Goblin")]
        [TestCase("RedDragon")]
        public void DraftPoolGeneratorUsesCurrentDefaultsAndKeepsArtworkAndAnimator(string cardId)
        {
            var database = Database();
            var expected = (UnitCardDefinition)database.CreateProvider().GetRequired(cardId);
            var record = database.Cards.Single(card => card.Id == cardId);
            var asset = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardsRoot + cardId + ".asset");
            Assert.That(asset, Is.Not.Null);
            var snapshot = EditorJsonUtility.ToJson(asset);
            var wasDirty = EditorUtility.IsDirty(asset);
            var sprite = asset.BoardSprite;
            var animator = asset.BoardAnimatorController;
            try
            {
                // Resolve the Editor assembly without adding an Editor dependency to the test assembly.
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Project333.Editor.Project333DraftPoolExpansionGenerator"))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(type, Is.Not.Null);
                var method = type.GetMethod("Create" + cardId, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                Assert.That(method.Invoke(null, null), Is.SameAs(asset));
                var generated = (UnitCardDefinition)asset.ToDefinition();
                Assert.That(generated.Attack, Is.EqualTo(expected.Attack));
                Assert.That(generated.Health, Is.EqualTo(expected.Health));
                Assert.That(generated.Cost.Mana, Is.EqualTo(expected.Cost.Mana));
                Assert.That(generated.Cost.Qi, Is.EqualTo(expected.Cost.Qi));
                Assert.That(generated.Cost.Power, Is.EqualTo(expected.Cost.Power));
                Assert.That(generated.Cost.Gold, Is.EqualTo(expected.Cost.Gold));
                Assert.That(generated.DamageType, Is.EqualTo(expected.DamageType));
                Assert.That(generated.HasFlying, Is.EqualTo(expected.HasFlying));
                Assert.That(generated.Races, Is.EquivalentTo(expected.Races));
                Assert.That(asset.EffectText, Is.EqualTo(record.EffectText ?? string.Empty));
                Assert.That(asset.BoardSprite, Is.SameAs(sprite));
                Assert.That(asset.BoardAnimatorController, Is.SameAs(animator));
            }
            finally
            {
                // Never save generated test values over the user's authored assets.
                EditorJsonUtility.FromJsonOverwrite(snapshot, asset);
                if (!wasDirty) EditorUtility.ClearDirty(asset);
            }
        }
    }
}
