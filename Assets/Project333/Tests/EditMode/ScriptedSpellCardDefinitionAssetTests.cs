using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class ScriptedSpellCardDefinitionAssetTests
    {
        [Test]
        public void MeteorRegisteredAsset_MatchesRulesCatalogAndArtwork()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Meteor.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(catalog.Cards.Count(card => card != null && card.CardId == MeteorRules.CardId), Is.EqualTo(1));
            Assert.That(catalog.TryGetCardAsset(MeteorRules.CardId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(asset));
            Assert.That(asset.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(asset.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(asset.EffectText, Is.EqualTo(MeteorRules.EffectDescription));
            var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();
            Assert.That(definition.EffectId, Is.EqualTo(MeteorRules.EffectId));
            Assert.That(definition.Damage, Is.EqualTo(40));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(definition.Cost.Mana, Is.EqualTo(7));
            Assert.That(definition.Cost.Gold + definition.Cost.Qi + definition.Cost.Power, Is.Zero);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Project333.Runtime.Presentation.Cards.CardArtworkLibrary.ClearCacheForTests();
            Assert.That(Project333.Runtime.Presentation.Cards.CardArtworkLibrary.TryGetArtwork(MeteorRules.CardId, out var artwork), Is.True);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(artwork), Is.EqualTo("Assets/Project333/Resources/Project333/CardArtwork/Meteor.png"));
        }

        [Test]
        public void EarthquakeRegisteredAsset_MatchesCurrentRulesCatalogAndExistingArtwork()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Earthquake.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(catalog.Cards.Count(card => card != null && card.CardId == EarthquakeRules.CardId), Is.EqualTo(1));
            Assert.That(catalog.TryGetCardAsset(EarthquakeRules.CardId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(asset));
            Assert.That(asset.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(asset.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(asset.EffectText, Is.EqualTo(EarthquakeRules.EffectDescription));
            var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();
            Assert.That(definition.EffectId, Is.EqualTo(EarthquakeRules.EffectId));
            Assert.That(definition.Damage, Is.EqualTo(10));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.Cost.Gold, Is.EqualTo(3));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Project333.Runtime.Presentation.Cards.CardArtworkLibrary.ClearCacheForTests();
            Assert.That(Project333.Runtime.Presentation.Cards.CardArtworkLibrary.TryGetArtwork(EarthquakeRules.CardId, out var artwork), Is.True);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(artwork), Is.EqualTo("Assets/Project333/Resources/Project333/CardArtwork/Earthquake.png"));
        }

        [Test]
        public void NuclearRegisteredAsset_MatchesCurrentRulesCatalogAndArtistArtwork()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Nuclear.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(catalog.Cards.Count(card => card != null && card.CardId == NuclearRules.CardId), Is.EqualTo(1));
            Assert.That(catalog.TryGetCardAsset(NuclearRules.CardId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(asset));
            Assert.That(asset.Rarity, Is.EqualTo(CardRarity.Legendary));
            Assert.That(asset.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(asset.EffectText, Is.EqualTo(NuclearRules.EffectDescription));
            var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();
            Assert.That(definition.EffectId, Is.EqualTo(NuclearRules.EffectId));
            Assert.That(definition.Damage, Is.EqualTo(100));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Fixed));
            Assert.That(definition.Cost.Power, Is.EqualTo(10));
            Project333.Runtime.Presentation.Cards.CardArtworkLibrary.ClearCacheForTests();
            Assert.That(Project333.Runtime.Presentation.Cards.CardArtworkLibrary.TryGetArtwork(NuclearRules.CardId, out var artwork), Is.True);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(artwork), Is.EqualTo("Assets/Project333/Resources/Project333/CardArtwork/Nuclear.png"));
        }

        [Test]
        public void NuclearTransientAsset_MapsImmediateFixedDamageAndCost()
        {
            var asset = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            try
            {
                asset.ConfigureBaseForTests(NuclearRules.CardId, "뉴클리어", new ResourceSetData(0, 0, 10, 0));
                asset.ConfigureForTests(NuclearRules.EffectId, damage: 100, damageType: DamageType.Fixed);
                var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();
                Assert.That(definition.Damage, Is.EqualTo(100));
                Assert.That(definition.DamageType, Is.EqualTo(DamageType.Fixed));
                Assert.That(definition.Cost.Power, Is.EqualTo(10));
                Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            }
            finally { Object.DestroyImmediate(asset); }
        }

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
