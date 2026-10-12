using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using UnityEditor;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    // Read-only native integration checks; JSON/domain behavior has separate tests.
    public sealed class OrcChieftainAssetTests
    {
        private const string CardId = "OrcChieftain";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/OrcChieftain.asset";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/OrcChieftain.png";
        private const string FallbackArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/OrcWarrior.png";

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
            Assert.That(asset.DisplayName, Is.EqualTo(record.DisplayName).And.EqualTo("오크 족장"));
            Assert.That(asset.Rarity, Is.EqualTo(record.Rarity).And.EqualTo(CardRarity.Rare));
            Assert.That(asset.Affiliation, Is.EqualTo(record.Affiliation).And.EqualTo(CardAffiliation.Fantasy));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(record.ChargeTileFootprint).And.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(asset.EffectText, Is.EqualTo(record.EffectText));
            Assert.That(asset.EffectText, Is.EqualTo("내 전장의 현재·이후 오크 유닛(자신 포함)에게 공격력 +10, 최대·현재 체력 +10. 이 오크 족장이 전장을 떠나면 해당 버프가 사라집니다. 중첩 가능."));
            Assert.That(asset.SpecialEffectText, Is.EqualTo(record.SpecialEffectText).And.Empty);
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(unit.Cost.Mana, Is.EqualTo(7));
            Assert.That(unit.Cost.Qi + unit.Cost.Power + unit.Cost.Gold, Is.Zero);
            Assert.That(unit.Attack, Is.EqualTo(record.Attack).And.EqualTo(50));
            Assert.That(unit.Health, Is.EqualTo(record.Health).And.EqualTo(70));
            Assert.That(unit.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(unit.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(unit.PhysicalDefense, Is.Zero); Assert.That(unit.MagicDefense, Is.Zero);
            Assert.That(unit.CanMove, Is.True);
            Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Orc }));
            Assert.That(unit.HasBerserker, Is.False);
            Assert.That(unit.HitsPerAttack, Is.EqualTo(1)); Assert.That(unit.MaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(unit.HasEndure || unit.HasShielder || unit.HasLifeSteal || unit.HasRush || unit.HasReplicate ||
                unit.HasHiding || unit.HasFlying || unit.HasPiercing || unit.IsScience, Is.False);
            Assert.That(unit.SealboundOwnerTurnStarts + unit.SpellPower + unit.InvincibleOwnerTurns + unit.SciencePowerUpkeep, Is.Zero);
            Assert.That(unit.InvincibleDuration, Is.EqualTo(InvincibleDurationType.None));
            Assert.That(unit.TurnStartResourceGain.Mana + unit.TurnStartResourceGain.Qi + unit.TurnStartResourceGain.Power + unit.TurnStartResourceGain.Gold, Is.Zero);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(unit), Is.True);
            Assert.That(CardLevelStatRules.ApplyAttackBonus(CardId, unit.Attack, 13), Is.EqualTo(54));
            Assert.That(CardLevelStatRules.ApplyHpBonus(CardId, unit.Health, 13), Is.EqualTo(79));
        }

        [Test]
        public void BoardVisualsReuseExistingOrcWarriorControllerAndIdleSprite()
        {
            var warrior = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>("Assets/Project333/ScriptableObjects/StarterTen/Cards/OrcWarrior.asset");
            var asset = Card();
            Assert.That(warrior, Is.Not.Null);
            Assert.That(asset.BoardAnimatorController, Is.SameAs(warrior.BoardAnimatorController));
            Assert.That(asset.BoardSprite, Is.SameAs(warrior.BoardSprite));
            Assert.That(AssetDatabase.GetAssetPath(asset.BoardAnimatorController), Is.EqualTo("Assets/Unit/Orc/OrcWarrior.controller"));
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/Unit/Orc/OrcWarrior.controller"), Is.EqualTo("75813b0ae4b9707408f36e04f4ef1b3b"));
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/Project333/ScriptableObjects/StarterTen/Cards/OrcWarrior.asset"), Is.EqualTo("b48afcfbd15842e40b3ce6378c3d6ac3"));
            Assert.That(AssetDatabase.GetAssetPath(asset.BoardSprite), Is.EqualTo("Assets/Unit/Orc/Idle.png"));
            Assert.That(asset.BoardSprite.name, Is.EqualTo("Idle_0"));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.BoardSprite, out string guid, out long localId), Is.True);
            Assert.That(guid, Is.EqualTo("838b0d7542aff694f8fd33c87e250d51"));
            Assert.That(localId, Is.EqualTo(-1592338960L));
        }

        [Test]
        public void ArtworkUsesDedicatedPortraitFirstOrExistingWarriorAliasWithoutCopying()
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
                    Assert.That(CardArtworkLibrary.TryGetArtwork("OrcWarrior", out var original), Is.True);
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
        private static UnitCardDefinitionAsset Card()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null, "Run ImportOrcChieftainRegistration using native Unity APIs before this integration fixture.");
            return asset;
        }
    }
}
