using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CardDefinitionAssetTests
    {
        [Test]
        public void UnitCardDefinitionAsset_ToDefinition_MapsAllFields()
        {
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            asset.ConfigureBaseForTests("unit-01", "Unit 01", new ResourceSetData(1, 2, 3, 4));
            asset.ConfigureForTests(
                attackType: AttackType.Ranged,
                attack: 5,
                health: 6,
                canMove: false,
                isScience: true,
                sciencePowerUpkeep: 2,
                turnStartResourceGain: new ResourceSetData(0, 1, 1, 0),
                maxAttacksPerTurn: 2,
                canAttackOnSummon: true,
                hitsPerAttack: 3,
                hasBerserker: true,
                hasEndure: true,
                hasGuard: true);

            var definition = (UnitCardDefinition)asset.ToDefinition();

            Assert.That(definition.CardId, Is.EqualTo("unit-01"));
            Assert.That(definition.DisplayName, Is.EqualTo("Unit 01"));
            Assert.That(definition.Cost.Mana, Is.EqualTo(1));
            Assert.That(definition.Cost.Qi, Is.EqualTo(2));
            Assert.That(definition.Cost.Power, Is.EqualTo(3));
            Assert.That(definition.Cost.Gold, Is.EqualTo(4));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Ranged));
            Assert.That(definition.Attack, Is.EqualTo(5));
            Assert.That(definition.Health, Is.EqualTo(6));
            Assert.That(definition.CanMove, Is.False);
            Assert.That(definition.IsScience, Is.True);
            Assert.That(definition.SciencePowerUpkeep, Is.EqualTo(2));
            Assert.That(definition.TurnStartResourceGain.Qi, Is.EqualTo(1));
            Assert.That(definition.TurnStartResourceGain.Power, Is.EqualTo(1));
            Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(2));
            Assert.That(definition.CanAttackOnSummon, Is.True);
            Assert.That(definition.HitsPerAttack, Is.EqualTo(3));
            Assert.That(definition.HasBerserker, Is.True);
            Assert.That(definition.HasEndure, Is.True);
            Assert.That(definition.HasGuard, Is.True);
        }

        [Test]
        public void UnitCardDefinitionAsset_SpecialEffectText_WhenScienceUpkeepExists_ReturnsPowerDrainText()
        {
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 2,
                health: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 2,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);

            Assert.That(asset.SpecialEffectText, Is.EqualTo("전력 -2"));
        }

        [Test]
        public void UnitCardDefinitionAsset_SpecialEffectText_WhenScienceUpkeepAndExplicitTextExist_AppendsPowerDrainText()
        {
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Neutral",
                CardAffiliation.ScienceCivilization,
                ChargeTileFootprint.OneByOne,
                string.Empty,
                "Guard");
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 2,
                health: 3,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 3,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false,
                hasGuard: true);

            Assert.That(asset.SpecialEffectText, Is.EqualTo("Guard" + System.Environment.NewLine + "전력 -3"));
        }

        [Test]
        public void DamageSpellCardDefinitionAsset_ToDefinition_MapsDamageAndCost()
        {
            var asset = ScriptableObject.CreateInstance<DamageSpellCardDefinitionAsset>();
            asset.ConfigureBaseForTests("spell-01", "Spell 01", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureForTests(7);

            var definition = (DamageSpellCardDefinition)asset.ToDefinition();

            Assert.That(definition.CardId, Is.EqualTo("spell-01"));
            Assert.That(definition.Cost.Gold, Is.EqualTo(2));
            Assert.That(definition.Damage, Is.EqualTo(7));
        }

        [Test]
        public void CardDefinitionCatalogAsset_CreateProvider_ResolvesConvertedDefinitions()
        {
            var unitAsset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            unitAsset.ConfigureBaseForTests("unit-02", "Unit 02", new ResourceSetData(0, 0, 0, 1));
            unitAsset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 2,
                health: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);

            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(new CardDefinitionAsset[] { unitAsset });

            var provider = catalog.CreateProvider();
            var definition = (UnitCardDefinition)provider.GetRequired("unit-02");

            Assert.That(definition.DisplayName, Is.EqualTo("Unit 02"));
            Assert.That(definition.Attack, Is.EqualTo(2));
            Assert.That(definition.Health, Is.EqualTo(3));
        }

        [Test]
        public void CardDefinitionCatalogAsset_TryGetCardAsset_ReturnsOriginalAssetReference()
        {
            var unitAsset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            unitAsset.ConfigureBaseForTests("unit-visual", "Unit Visual", new ResourceSetData(0, 0, 0, 1));
            unitAsset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 2,
                health: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);

            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(new CardDefinitionAsset[] { unitAsset });

            var found = catalog.TryGetCardAsset("unit-visual", out var resolvedAsset);

            Assert.That(found, Is.True);
            Assert.That(resolvedAsset, Is.SameAs(unitAsset));
        }

        [Test]
        public void CardDefinitionAsset_ConfigureBoardVisualsForTests_StoresBoardVisualReferences()
        {
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            var controller = new AnimatorOverrideController();

            asset.ConfigureBoardVisualsForTests(sprite, controller);

            Assert.That(asset.BoardSprite, Is.SameAs(sprite));
            Assert.That(asset.BoardAnimatorController, Is.SameAs(controller));

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(controller);
        }
    }
}
