using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class TrapRegistrationTests
    {
        private const string JsonPath = "Assets/Project333/Resources/Project333/Data/cards.json";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/TrapReturn.asset";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        private static JsonCardDefinitionRecord Record(JsonCardDefinitionDatabase database) =>
            database.Cards.Single(card => card.Id == TrapReturnCardRules.CardId);

        [Test]
        public void CanonicalDatabaseContainsExactlyFiveReleasedTrapsAndAllOtherDefinitionsRemainNonTrap()
        {
            var json = File.ReadAllText(JsonPath);
            var database = Database();
            var validation = CardDatabaseValidator.ValidateJson(json);
            Assert.That(validation.IsValid, Is.True, string.Join(Environment.NewLine, validation.Errors));
            Assert.That(database.Cards.Count, Is.EqualTo(60));
            Assert.That(database.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(60));
            var definitions = database.ToDefinitions();
            Assert.That(definitions.Count, Is.EqualTo(60));
            Assert.That(definitions.Count(definition => definition.HasTrap), Is.EqualTo(5));
            Assert.That(definitions.Where(definition => definition.CardId != TrapReturnCardRules.CardId && definition.CardId != TrapMirrorFieldCardRules.CardId && definition.CardId != ExplosiveReactiveArmourCardRules.CardId && definition.CardId != IronDomeCardRules.CardId && definition.CardId != SummonTrapCardRules.CardId)
                .All(definition => !definition.HasTrap), Is.True);
            var record = Record(database);
            var trap = record.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(trap, Is.Not.Null);
            Assert.That(record.DisplayName, Is.EqualTo("트랩: 리턴"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.TrapSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.True, "Explicit released non-upgradeable reward exception.");
            Assert.That(record.EffectText, Is.Empty);
            Assert.That(record.SpecialEffectText, Is.EqualTo(TrapReturnCardRules.SpecialEffectText));
            Assert.That(record.Damage, Is.Zero);
            Assert.That(record.EffectAmount, Is.Zero);
            Assert.That(record.TriggerCount, Is.Zero);
            Assert.That(trap.CardType, Is.EqualTo(CardType.Spell));
            Assert.That(trap.HasTrap, Is.True);
            Assert.That(trap.HasReplicate, Is.False);
            Assert.That(new[] { trap.Cost.Mana, trap.Cost.Qi, trap.Cost.Power, trap.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(new[] { trap.ActivationCost.Mana, trap.ActivationCost.Qi,
                trap.ActivationCost.Power, trap.ActivationCost.Gold }, Is.EqualTo(new[] { 0, 0, 0, 4 }));
            Assert.That(trap.TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitAttacksMaster));
            Assert.That(trap.EffectType, Is.EqualTo(TrapEffectType.ReturnAttackerToHand));
            Assert.That((CardDefinition)trap, Is.Not.InstanceOf<DamageSpellCardDefinition>());
            Assert.That((CardDefinition)trap, Is.Not.InstanceOf<ScriptedSpellCardDefinition>());
        }

        [Test]
        public void ReleasedTrapHasNoUpgradeRowsEvenAtMaximumAccountLevel()
        {
            var trap = Record(Database()).ToDefinition();
            Assert.That(CardUpgradeRules.IsCardUpgradeable(trap), Is.False);
            var table = OwnedCardsUpgradeTableData.Build(trap);
            Assert.That(table.IsUpgradeable, Is.False);
            Assert.That(table.Rows, Is.Empty);
            Assert.That(table.EmptyMessage, Is.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void NegativeActivationResourcesAreRejectedByDefinitionBeforeTheyCanBeCaptured(int resource)
        {
            var values = new int[4]; values[resource] = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrapSpellCardDefinition("invalid", "invalid",
                new ResourceSet(values[0], values[1], values[2], values[3]),
                TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.ReturnAttackerToHand));
        }

        [TestCase(true, 0)]
        [TestCase(true, 99)]
        [TestCase(false, 0)]
        [TestCase(false, 99)]
        public void UnsupportedTriggersAndEffectsCannotCreateAnArmedDefinition(bool invalidTrigger, int value)
        {
            var trigger = invalidTrigger ? (TrapTriggerType)value : TrapTriggerType.EnemyUnitAttacksMaster;
            var effect = invalidTrigger ? TrapEffectType.ReturnAttackerToHand : (TrapEffectType)value;
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrapSpellCardDefinition("invalid", "invalid",
                new ResourceSet(), trigger, effect));
        }

        [TestCase("flag")]
        [TestCase("kind")]
        [TestCase("text")]
        [TestCase("setup")]
        [TestCase("negativeGold")]
        [TestCase("negativeMana")]
        [TestCase("negativeQi")]
        [TestCase("negativePower")]
        [TestCase("triggerNone")]
        [TestCase("triggerUnknown")]
        [TestCase("effectNone")]
        [TestCase("effectUnknown")]
        [TestCase("goldThree")]
        [TestCase("damage")]
        public void InvalidTrapMetadataIsRejectedWithoutAcceptingAnOrdinarySpellAsASecret(string mutation)
        {
            var database = Database();
            var record = Record(database);
            switch (mutation)
            {
                case "flag": record.HasTrap = false; break;
                case "kind": record.DefinitionType = JsonCardDefinitionKind.DamageSpell; break;
                case "text": record.SpecialEffectText = string.Empty; break;
                case "setup": record.Cost.Mana = 1; break;
                case "negativeGold": record.TrapActivationCost.Gold = -1; break;
                case "negativeMana": record.TrapActivationCost.Mana = -1; break;
                case "negativeQi": record.TrapActivationCost.Qi = -1; break;
                case "negativePower": record.TrapActivationCost.Power = -1; break;
                case "triggerNone": record.TrapTrigger = TrapTriggerType.None; break;
                case "triggerUnknown": record.TrapTrigger = (TrapTriggerType)99; break;
                case "effectNone": record.TrapEffect = TrapEffectType.None; break;
                case "effectUnknown": record.TrapEffect = (TrapEffectType)99; break;
                case "goldThree": record.TrapActivationCost.Gold = 3; break;
                case "damage": record.Damage = 1; break;
                default: throw new InvalidOperationException("Unknown fixture mutation.");
            }
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, string.Join(Environment.NewLine, validation.Errors));
            if (mutation == "flag" || mutation == "kind" || mutation == "setup")
                Assert.Throws<InvalidOperationException>(() => record.ToDefinition());
        }

        [Test]
        public void NativeTrapCardUsesTheSameFreeSetupAndCapturedGoldFourWithoutSummonedVisuals()
        {
            var record = Record(Database());
            var asset = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.CardId, Is.EqualTo(record.Id));
            Assert.That(asset.DisplayName, Is.EqualTo(record.DisplayName));
            Assert.That(asset.Rarity, Is.EqualTo(record.Rarity));
            Assert.That(asset.Affiliation, Is.EqualTo(record.Affiliation));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(asset.HasTrap, Is.True);
            Assert.That(asset.HasReplicate, Is.False);
            Assert.That(asset.EffectText, Is.Empty);
            Assert.That(asset.SpecialEffectText, Is.EqualTo(record.SpecialEffectText));
            Assert.That(asset.BoardSprite, Is.Null);
            Assert.That(asset.BoardAnimatorController, Is.Null);
            var definition = asset.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(definition, Is.Not.Null);
            Assert.That(new[] { asset.Cost.Mana, asset.Cost.Qi, asset.Cost.Power, asset.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(definition.ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(definition.TriggerType, Is.EqualTo(record.TrapTrigger));
            Assert.That(definition.EffectType, Is.EqualTo(record.TrapEffect));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
        }

        [Test]
        public void NativeCatalogAppendsFiveTrapsAmongFiftyThreeValidUniqueReferences()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cards.Count, Is.EqualTo(53));
            Assert.That(catalog.Cards.All(card => card != null), Is.True);
            Assert.That(catalog.Cards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(53));
            Assert.That(catalog.Cards.Count(card => card.HasTrap), Is.EqualTo(5));
            Assert.That(catalog.Cards[48].CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(catalog.Cards.Single(card => card.CardId == TrapReturnCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath)));
        }

        [Test]
        public void NativeTrapAssetAlwaysForcesFreeSetupAndAddsTheTraitToBlankSpecialText()
        {
            var asset = ScriptableObject.CreateInstance<TrapSpellCardDefinitionAsset>();
            try
            {
                asset.ConfigureBaseForTests("synthetic-trap", "synthetic-trap", new ResourceSetData(1, 2, 3, 4));
                asset.ConfigureMetadataForTests(CardRarity.Rare, CardAffiliation.Neutral, ChargeTileFootprint.None, "", "");
                asset.ConfigureTrapForTests(new ResourceSetData(0, 0, 0, 4),
                    TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.ReturnAttackerToHand);
                Assert.That(asset.Cost.ToRuntime().Gold, Is.Zero);
                Assert.That(asset.ToDefinition().Cost.Gold, Is.Zero);
                Assert.That(asset.SpecialEffectText, Does.Contain("트랩"));
                Assert.That(asset.ToDefinition().HasTrap, Is.True);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void AnonymousAndTriggeredArtworkUseSeparateRealNativeSpriteResources()
        {
            var hidden = Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden");
            var real = Resources.Load<Sprite>("Project333/CardArtwork/TrapReturn");
            Assert.That(hidden, Is.Not.Null);
            Assert.That(real, Is.Not.Null);
            Assert.That(hidden, Is.Not.SameAs(real));
            Assert.That(hidden.texture, Is.Not.SameAs(real.texture));
            Assert.That(hidden.rect.width, Is.GreaterThan(0));
            Assert.That(real.rect.width, Is.GreaterThan(0));
            Assert.That(AssetDatabase.GetAssetPath(hidden), Does.EndWith("TrapHidden.png"));
            Assert.That(AssetDatabase.GetAssetPath(real), Does.EndWith("TrapReturn.png"));
        }
    }
}
