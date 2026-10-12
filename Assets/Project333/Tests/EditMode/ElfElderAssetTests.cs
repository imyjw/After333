using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    // Read-only native integration checks; JSON/domain behavior has separate tests.
    public sealed class ElfElderAssetTests
    {
        private const string CardId = "ElfElder";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/ElfElder.asset";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/ElfElder.png";
        private const string FallbackArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/ElfLongbowScout.png";

        [Test]
        public void NativeCardMatchesConfirmedJsonAndIsRegisteredExactlyOnce()
        {
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
            var record = database.Cards.Single(item => item.Id == CardId);
            var asset = Card();
            var unit = (UnitCardDefinition)asset.ToDefinition();
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cards, Does.Not.Contain(null));
            Assert.That(catalog.Cards.Count(item => item.CardId == CardId), Is.EqualTo(1));
            Assert.That(catalog.Cards.Single(item => item.CardId == CardId), Is.SameAs(asset));
            Assert.That(AssetDatabase.AssetPathToGUID(CatalogPath), Is.EqualTo("d932562c5c4766d43adce3f999c666c8"));
            Assert.That(asset.CardId, Is.EqualTo("ElfElder"));
            Assert.That(asset.DisplayName, Is.EqualTo(record.DisplayName).And.EqualTo("엘프 장로"));
            Assert.That(asset.Rarity, Is.EqualTo(record.Rarity).And.EqualTo(CardRarity.Rare));
            Assert.That(asset.Affiliation, Is.EqualTo(record.Affiliation).And.EqualTo(CardAffiliation.Fantasy));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(record.ChargeTileFootprint).And.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(asset.EffectText, Is.EqualTo(record.EffectText).And.Empty);
            Assert.That(asset.SpecialEffectText, Is.EqualTo(record.SpecialEffectText).And.EqualTo("관통\n속공"));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(unit.Cost.Mana, Is.EqualTo(5));
            Assert.That(unit.Cost.Qi + unit.Cost.Power + unit.Cost.Gold, Is.Zero);
            Assert.That(unit.Attack, Is.EqualTo(record.Attack).And.EqualTo(30));
            Assert.That(unit.Health, Is.EqualTo(record.Health).And.EqualTo(25));
            Assert.That(unit.AttackType, Is.EqualTo(AttackType.Ranged));
            Assert.That(unit.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(unit.PhysicalDefense, Is.Zero); Assert.That(unit.MagicDefense, Is.Zero);
            Assert.That(unit.CanMove, Is.True);
            Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Elf }));
            Assert.That(unit.HasBerserker, Is.False);
            Assert.That(unit.HasPiercing && unit.HasRush, Is.True);
            Assert.That(unit.HitsPerAttack, Is.EqualTo(1)); Assert.That(unit.MaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(unit.HasEndure || unit.HasShielder || unit.HasLifeSteal || unit.HasReplicate ||
                unit.HasHiding || unit.HasFlying || unit.IsScience, Is.False);
            Assert.That(unit.SealboundOwnerTurnStarts + unit.SpellPower + unit.InvincibleOwnerTurns + unit.SciencePowerUpkeep, Is.Zero);
            Assert.That(unit.InvincibleDuration, Is.EqualTo(InvincibleDurationType.None));
            Assert.That(unit.TurnStartResourceGain.Mana + unit.TurnStartResourceGain.Qi + unit.TurnStartResourceGain.Power + unit.TurnStartResourceGain.Gold, Is.Zero);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(unit), Is.True);
            Assert.That(CardLevelStatRules.ApplyAttackBonus(CardId, unit.Attack, 13), Is.EqualTo(34));
            Assert.That(CardLevelStatRules.ApplyHpBonus(CardId, unit.Health, 13), Is.EqualTo(34));
        }

        [Test]
        public void BoardVisualsPreserveDedicatedElfElderControllerAndIdleSprite()
        {
            var asset = Card();
            Assert.That(asset.BoardAnimatorController, Is.Not.Null);
            Assert.That(asset.BoardSprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(asset.BoardAnimatorController), Is.EqualTo("Assets/Unit/ElfElder/ElfElder.controller"));
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/Unit/ElfElder/ElfElder.controller"), Is.EqualTo("687a330ca088a914f87c1d9df654e2d2"));
            Assert.That(AssetDatabase.GetAssetPath(asset.BoardSprite), Is.EqualTo("Assets/Unit/ElfElder/Idle.png"));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.BoardSprite, out string guid, out long localId), Is.True);
            Assert.That(guid, Is.EqualTo("298f003cbb1c074499bb252aa1366975"));
            Assert.That(localId, Is.EqualTo(491772452L));
        }

        [Test]
        public void ArtworkUsesDedicatedPortraitFirstOrExistingElfLongbowScoutAliasWithoutCopying()
        {
            var dedicated = File.Exists(ArtworkPath);
            var expectedPath = dedicated ? ArtworkPath : FallbackArtworkPath;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath);
            Assert.That(sprite, Is.Not.Null);
            CardArtworkLibrary.ClearCacheForTests();
            try
            {
                Assert.That(CardArtworkLibrary.TryGetArtwork(CardId, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(sprite));
                Assert.That(AssetDatabase.GetAssetPath(resolved), Is.EqualTo(expectedPath));
                if (!dedicated)
                {
                    Assert.That(CardArtworkLibrary.TryGetArtwork("ElfLongbowScout", out var original), Is.True);
                    Assert.That(resolved, Is.SameAs(original));
                    Assert.That(File.Exists(ArtworkPath), Is.False, "Runtime alias does not create/copy a new portrait.");
                }
                else
                {
                    var importer = AssetImporter.GetAtPath(ArtworkPath) as TextureImporter;
                    Assert.That(importer, Is.Not.Null);
                    Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                    Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                    Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, width, height)));
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(width * .5f, height * .5f)));
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                }
            }
            finally { CardArtworkLibrary.ClearCacheForTests(); }
        }
        [Test]
        public void NativeCardPreviewShowsOnlyPiercingAndRushTooltips()
        {
            var definition = Card().ToDefinition();
            var tooltips = OccupantSpecialEffectTooltipCatalog.Build(definition);
            Assert.That(tooltips.Select(item => item.Title).ToArray(), Is.EqualTo(new[] { "관통", "속공" }));
            Assert.That(tooltips.All(item => item.Kind == OccupantTooltipKind.Ability), Is.True);
            Assert.That(tooltips.Single(item => item.Title == "관통").Description, Does.Contain("같은 열"));
            Assert.That(tooltips.Single(item => item.Title == "속공").Description, Does.Contain("소환한 턴"));
            Assert.That(definition.CardId, Is.EqualTo("ElfElder"), "The card uses its canonical identifier.");
        }
        private static UnitCardDefinitionAsset Card()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null, "Run ImportElfElderRegistration using native Unity APIs before this integration fixture.");
            return asset;
        }
    }
}
