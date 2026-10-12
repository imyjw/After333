using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class FireSpearNativeAssetTests
    {
        const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/FireSpear.asset";
        const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/FireSpear.png";
        const string AtlasPath="Assets/Project333/Resources/Project333/SpellEffects/FireSpear/FlameSpear.png";
        const string FramesKey="Project333/SpellEffects/FireSpear/FlameSpear";
        static JsonCardDefinitionRecord Record()=>JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(card=>card.Id==FireSpearRules.CardId);

        [Test]
        public void NativeCardMatchesCanonicalScriptedSpellMetadataAndContainsNoSummonedVisual()
        {
            var record=Record();var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(card,Is.Not.Null);var definition=card.ToDefinition() as ScriptedSpellCardDefinition;Assert.That(definition,Is.Not.Null);
            Assert.That(card.CardId,Is.EqualTo(FireSpearRules.CardId));Assert.That(card.DisplayName,Is.EqualTo("파이어스피어"));
            Assert.That(card.Rarity,Is.EqualTo(CardRarity.Common));Assert.That(card.Affiliation,Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(card.Cost.Mana,Is.EqualTo(3));Assert.That(card.Cost.Qi,Is.Zero);Assert.That(card.Cost.Power,Is.Zero);Assert.That(card.Cost.Gold,Is.Zero);
            Assert.That(card.ChargeTileFootprint,Is.EqualTo(ChargeTileFootprint.None));Assert.That(card.HasReplicate,Is.False);
            Assert.That(card.EffectText,Is.EqualTo(record.EffectText).And.EqualTo(FireSpearRules.EffectDescription));Assert.That(card.SpecialEffectText,Is.Empty);
            Assert.That(definition.EffectId,Is.EqualTo(FireSpearRules.EffectId));Assert.That(definition.Damage,Is.EqualTo(record.Damage).And.EqualTo(30));
            Assert.That(definition.DamageType,Is.EqualTo(DamageType.Magic));Assert.That(definition.TriggerCount,Is.Zero);
            Assert.That(card.BoardSprite,Is.Null);Assert.That(card.BoardAnimatorController,Is.Null);
        }

        [Test]
        public void NativeDamageUsesTheConfirmedUpgradeScheduleAndBothReleasePoolsAreEnabled()
        {
            var record=Record();var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(record.IncludeInDraft,Is.True);Assert.That(record.IncludeInRewards,Is.True);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()),Is.True);
            var damage=((ScriptedSpellCardDefinition)card.ToDefinition()).Damage;
            for(int level=0;level<=13;level++)Assert.That(CardLevelSpellRules.ApplyDamageBonus(damage,level),Is.EqualTo(30+level),"Native spell level"+level);
            Assert.That(CardLevelSpellRules.ApplyDamageBonus(damage,13),Is.EqualTo(43));
        }

        [Test]
        public void NativeAtlasHasEightChronologicalFullCellsAndUniqueStableSpriteIdentities()
        {
            var importer=AssetImporter.GetAtPath(AtlasPath) as TextureImporter;Assert.That(importer,Is.Not.Null);
            Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite));Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.isReadable,Is.True);
            Assert.That(importer.alphaSource,Is.EqualTo(TextureImporterAlphaSource.FromInput));Assert.That(importer.alphaIsTransparency,Is.True);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
            var frames=Resources.LoadAll<Sprite>(FramesKey).OrderBy(sprite=>sprite.name,StringComparer.Ordinal).ToArray();Assert.That(frames.Length,Is.EqualTo(8));
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);Assert.That(atlas.width,Is.EqualTo(2048));Assert.That(atlas.height,Is.EqualTo(1024));
            var fileIds=new long[8];var atlasGuid=AssetDatabase.AssetPathToGUID(AtlasPath);
            for(int i=0;i<8;i++)
            {
                Assert.That(frames[i].name,Is.EqualTo("FlameSpear_"+i));Assert.That(frames[i].rect,Is.EqualTo(new Rect(i%4*512,(1-i/4)*512,512,512)));
                Assert.That(frames[i].pivot,Is.EqualTo(new Vector2(256,256)));Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(100));Assert.That(frames[i].vertices.Length,Is.EqualTo(4));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(frames[i],out string guid,out long id),Is.True);
                Assert.That(guid,Is.EqualTo(atlasGuid));Assert.That(id,Is.Not.Zero);fileIds[i]=id;
            }
            Assert.That(fileIds.Distinct().Count(),Is.EqualTo(8));
        }

        [Test]
        public void EveryFrameHasTransparentBackgroundAndCompleteArtworkInsideTheSafeMargin()
        {
            var image=ReadPng(AtlasPath);Assert.That(image.width,Is.EqualTo(2048));Assert.That(image.height,Is.EqualTo(1024));
            for(int frame=0;frame<8;frame++)
            {
                int visible=0,transparent=0;for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    var alpha=image.pixels[((1-frame/4)*512+y)*2048+frame%4*512+x].a;
                    if(alpha==0)transparent++;else if(alpha>=16)visible++; // Preserve generated faint alpha; count meaningful visible artwork.
                    if(x<16||y<16||x>495||y>495)Assert.That(alpha,Is.Zero,"Whole frame"+frame+" keeps16px transparent margin.");
                }
                Assert.That(visible,Is.GreaterThan(1000),"The frame contains the complete flame spear.");Assert.That(transparent,Is.GreaterThan(512*512/2),"No opaque background is introduced.");
            }
        }

        [Test]
        public void CardPortraitIsOneWholeNativeFrameAndKeepsTheFirstSpearArtworkExactly()
        {
            var portrait=Resources.Load<Sprite>("Project333/CardArtwork/FireSpear");Assert.That(portrait,Is.Not.Null);
            var importer=AssetImporter.GetAtPath(PortraitPath) as TextureImporter;Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.isReadable,Is.False);Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(portrait.rect,Is.EqualTo(new Rect(0,0,512,512)));Assert.That(portrait.pivot,Is.EqualTo(new Vector2(256,256)));Assert.That(portrait.vertices.Length,Is.EqualTo(4));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(PortraitPath).OfType<Sprite>().Count(),Is.EqualTo(1),"A whole8-frame sheet cannot become a card portrait.");
            var cardImage=ReadPng(PortraitPath);var atlas=ReadPng(AtlasPath);Assert.That(cardImage.width,Is.EqualTo(512));Assert.That(cardImage.height,Is.EqualTo(512));
            var firstFrame=new Color32[512*512];for(int y=0;y<512;y++)Array.Copy(atlas.pixels,(512+y)*2048,firstFrame,y*512,512);
            CollectionAssert.AreEqual(firstFrame,cardImage.pixels,"The whole first512px frame was extracted without crop/redraw/alpha painting.");
        }

        [Test]
        public void CatalogContainsOneFireSpearAndRetainsValidUniqueExistingCardReferences()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Cards,Is.Not.Empty);Assert.That(catalog.Cards.All(asset=>asset!=null),Is.True);
            Assert.That(catalog.Cards.Count(asset=>asset.CardId==FireSpearRules.CardId),Is.EqualTo(1));Assert.That(catalog.Cards.Single(asset=>asset.CardId==FireSpearRules.CardId),Is.SameAs(card));
            Assert.That(catalog.Cards.Select(asset=>asset.CardId).Distinct().Count(),Is.EqualTo(catalog.Cards.Count));
        }
        static (int width,int height,Color32[] pixels) ReadPng(string path)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try{Assert.That(texture.LoadImage(File.ReadAllBytes(path),false),Is.True);return(texture.width,texture.height,texture.GetPixels32());}
            finally{Object.DestroyImmediate(texture);}
        }
    }
}