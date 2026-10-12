using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class SummonTrapRegistrationTests
    {
        private const string JsonPath = "Assets/Project333/Resources/Project333/Data/cards.json";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/SummonTrap.asset";
        private const string ArtPath = "Assets/Project333/Resources/Project333/CardArtwork/SummonTrap.png";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        private static JsonCardDefinitionRecord Record(JsonCardDefinitionDatabase database) =>
            database.Cards.Single(card => card.Id == SummonTrapCardRules.CardId);

        [Test]
        public void CanonicalSummonTrapHasConfirmedMetadataAndFreeInstallationSeparateFromActivation()
        {
            var database = Database();
            var result = CardDatabaseValidator.Validate(database);
            Assert.That(result.IsValid, Is.True, string.Join(Environment.NewLine, result.Errors));
            Assert.That(database.Cards.Count, Is.EqualTo(60));
            Assert.That(database.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(60));
            Assert.That(database.Cards.Count(card => card.DefinitionType == JsonCardDefinitionKind.TrapSpell), Is.EqualTo(5));
            var record = Record(database);
            Assert.That(record.DisplayName, Is.EqualTo("트랩 - 서먼 트랩"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.TrapSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.True);
            Assert.That(record.HasTrap, Is.True);
            Assert.That(record.HasReplicate, Is.False);
            Assert.That(record.EffectText, Is.Empty);
            Assert.That(record.SpecialEffectText, Is.EqualTo(SummonTrapCardRules.SpecialEffectText));
            Assert.That(record.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(record.Damage, Is.EqualTo(20));
            Assert.That(record.EffectAmount, Is.Zero);
            var definition = (TrapSpellCardDefinition)record.ToDefinition();
            Assert.That(definition.CardType, Is.EqualTo(CardType.Spell));
            Assert.That(new[] { definition.Cost.Mana, definition.Cost.Qi, definition.Cost.Power, definition.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(new[] { definition.ActivationCost.Mana, definition.ActivationCost.Qi,
                definition.ActivationCost.Power, definition.ActivationCost.Gold }, Is.EqualTo(new[] { 0, 0, 0, 1 }));
            Assert.That(definition.TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitSummoned));
            Assert.That(definition.EffectType, Is.EqualTo(TrapEffectType.DamageSummonedUnitPhysical));
            Assert.That(definition.Damage, Is.EqualTo(20));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True,
                "Upgrade eligibility also admits this card to the normal reward pool.");
        }

        [TestCase("setup")]
        [TestCase("activation")]
        [TestCase("trigger")]
        [TestCase("effect")]
        [TestCase("damage")]
        [TestCase("damageType")]
        [TestCase("draft")]
        [TestCase("rewards")]
        [TestCase("rarity")]
        [TestCase("affiliation")]
        public void ValidatorRejectsSummonTrapRuleDrift(string mutation)
        {
            var database = Database();
            var record = Record(database);
            switch (mutation)
            {
                case "setup": record.Cost.Gold = 1; break;
                case "activation": record.TrapActivationCost.Gold = 2; break;
                case "trigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksMaster; break;
                case "effect": record.TrapEffect = TrapEffectType.DestroyAttacker; break;
                case "damage": record.Damage = 21; break;
                case "damageType": record.DamageType = DamageType.Magic; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
                case "rarity": record.Rarity = CardRarity.Rare; break;
                case "affiliation": record.Affiliation = CardAffiliation.Fantasy; break;
            }
            var result = CardDatabaseValidator.Validate(database);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(error => error.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, string.Join(Environment.NewLine, result.Errors));
        }

        [Test]
        public void NativeDefinitionAndOrderedCatalogMatchCanonicalCard()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null);
            var record = Record(Database());
            Assert.That(asset.DisplayName, Is.EqualTo(record.DisplayName));
            Assert.That(asset.Rarity, Is.EqualTo(record.Rarity));
            Assert.That(asset.Affiliation, Is.EqualTo(record.Affiliation));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(record.ChargeTileFootprint));
            Assert.That(asset.EffectText, Is.EqualTo(record.EffectText));
            Assert.That(asset.SpecialEffectText, Is.EqualTo(record.SpecialEffectText));
            Assert.That(asset.BoardSprite, Is.Null);
            Assert.That(asset.BoardAnimatorController, Is.Null);
            var native = (TrapSpellCardDefinition)asset.ToDefinition();
            Assert.That(native.Damage, Is.EqualTo(20));
            Assert.That(native.TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitSummoned));
            Assert.That(native.EffectType, Is.EqualTo(TrapEffectType.DamageSummonedUnitPhysical));
            Assert.That(native.ActivationCost.Gold, Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(native), Is.True);
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog.Cards.Count, Is.EqualTo(53));
            Assert.That(catalog.Cards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(53));
            Assert.That(catalog.Cards[48].CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(catalog.Cards[49].CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(catalog.Cards[50].CardId, Is.EqualTo(ExplosiveReactiveArmourCardRules.CardId));
            Assert.That(catalog.Cards[51].CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(catalog.Cards[52], Is.SameAs(asset));
        }

        [Test]
        public void AllLevelsGainOnePhysicalDamageAndNeitherSpellPowerNorCostsIncrease()
        {
            foreach (var trap in new[] { (TrapSpellCardDefinition)Record(Database()).ToDefinition(),
                (TrapSpellCardDefinition)AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath).ToDefinition() })
            {
                for (var level = 0; level <= 13; level++)
                {
                    Assert.That(CardStatDisplay.TryGetSpellDamage(trap, level, 100, out var damage), Is.True);
                    Assert.That(damage, Is.EqualTo(20 + level), "Physical trap excludes SpellPower at Lv." + level);
                    Assert.That(CardStatDisplay.TryCreate(trap, level, out var stats, 100), Is.True);
                    Assert.That(stats.Attack, Is.EqualTo(20 + level));
                    Assert.That(stats.HasHp, Is.False);
                    Assert.That(trap.Cost.Gold, Is.Zero);
                    Assert.That(trap.ActivationCost.Gold, Is.EqualTo(1));
                }
                var table = OwnedCardsUpgradeTableData.Build(trap);
                Assert.That(table.IsUpgradeable, Is.True);
                Assert.That(table.EffectLabel, Is.EqualTo("물리 피해"));
                Assert.That(table.Rows.Select(row => row.Level), Is.EqualTo(Enumerable.Range(1, 13)));
                Assert.That(table.Rows.All(row => row.EffectIncrease == 1 && row.HpIncrease == 0 && row.AttackIncrease == 0), Is.True);
                Assert.That(table.Rows.Sum(row => row.EffectIncrease), Is.EqualTo(13));
                Assert.That(OccupantSpecialEffectTooltipCatalog.Build(trap).Single().Title, Is.EqualTo("트랩"));
            }
        }

        [Test]
        public void OwnedDetailsShowCurrentUpgradedDamageFreeSetupAndDistinctActivationCost()
        {
            var root = new GameObject("SummonTrapDetailsValidation"); root.SetActive(false);
            try
            {
                var controller = root.AddComponent<OwnedCardsSceneController>();
                var asset = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
                var build = typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", BindingFlags.Instance | BindingFlags.NonPublic);
                var text = (string)build.Invoke(controller, new object[] { asset, 13 });
                Assert.That(text, Does.Contain("비용: 없음"));
                Assert.That(text, Does.Contain("발동 비용: 골드 1"));
                Assert.That(text, Does.Contain("물리 데미지 33"));
                Assert.That(text, Does.Not.Contain("물리 데미지 20"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ArtworkRemainsOriginalFullRectRgbaAndUsesNativeCardResourceIdentity()
        {
            var sprite = Resources.Load<Sprite>("Project333/CardArtwork/SummonTrap");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(ArtPath));
            Assert.That(sprite.name, Is.EqualTo("SummonTrap"));
            Assert.That(sprite.vertices.Length, Is.EqualTo(4));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(ArtPath), false), Is.True);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, source.width, source.height)));
                Assert.That(sprite.texture.width, Is.EqualTo(source.width));
                Assert.That(sprite.texture.height, Is.EqualTo(source.height));
                Assert.That(source.GetPixels32().Any(pixel => pixel.a < 255), Is.True);
                Assert.That(source.GetPixels32().Any(pixel => pixel.a > 200), Is.True);
            }
            finally { Object.DestroyImmediate(source); }
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(ArtPath)));
            Assert.That(fileId, Is.Not.Zero);
            CardArtworkLibrary.ClearCacheForTests();
            Assert.That(CardArtworkLibrary.TryGetArtwork("SummonTrap", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(sprite));
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ArtPath))).Replace("-", ""),
                    Is.EqualTo("FFCBDF2BBFEA84A0003A59C43C633D54C2AB2DD8484512E51D56C97BD0FE8315"));
        }
    }
}
