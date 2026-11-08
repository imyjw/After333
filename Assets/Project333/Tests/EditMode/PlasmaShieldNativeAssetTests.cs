using System.Collections.Generic;
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
    public sealed class PlasmaShieldNativeAssetTests
    {
        const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/PlasmaShield.asset";
        const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/PlasmaShield.png";
        const string AtlasPath="Assets/Project333/Resources/Project333/SpellEffects/PlasmaShield/Formation.png";
        const string FramesKey="Project333/SpellEffects/PlasmaShield/Formation";
        static JsonCardDefinitionRecord Record()=>JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(card=>card.Id==PlasmaShieldRules.CardId);

        [Test]
        public void NativeCardMatchesCanonicalNonDamageDefenseBuffAndContainsNoSummonedVisual()
        {
            var record=Record();var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(card,Is.Not.Null);var definition=card.ToDefinition() as ScriptedSpellCardDefinition;Assert.That(definition,Is.Not.Null);
            Assert.That(card.CardId,Is.EqualTo(PlasmaShieldRules.CardId));Assert.That(card.DisplayName,Is.EqualTo("플라즈마 쉴드"));
            Assert.That(card.Rarity,Is.EqualTo(CardRarity.Common));Assert.That(card.Affiliation,Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(card.Cost.Mana,Is.Zero);Assert.That(card.Cost.Qi,Is.Zero);Assert.That(card.Cost.Power,Is.EqualTo(1));Assert.That(card.Cost.Gold,Is.Zero);
            Assert.That(card.ChargeTileFootprint,Is.EqualTo(ChargeTileFootprint.None));Assert.That(card.HasReplicate,Is.False);
            Assert.That(card.EffectText,Is.EqualTo(record.EffectText).And.EqualTo(PlasmaShieldRules.EffectDescription));Assert.That(card.SpecialEffectText,Is.Empty);
            Assert.That(definition.EffectId,Is.EqualTo(PlasmaShieldRules.EffectId));Assert.That(definition.Damage,Is.EqualTo(record.Damage).And.Zero);Assert.That(definition.EffectAmount,Is.EqualTo(record.EffectAmount).And.EqualTo(10));
            Assert.That(definition.DamageType,Is.EqualTo(DamageType.None));Assert.That(definition.TriggerCount,Is.Zero);
            Assert.That(card.BoardSprite,Is.Null);Assert.That(card.BoardAnimatorController,Is.Null);
        }

        [Test]
        public void NativeShieldStaysDraftableButCannotUpgradeOrEnterRewards()
        {
            var record=Record();var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(record.IncludeInDraft,Is.True);Assert.That(record.IncludeInRewards,Is.False);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(card.ToDefinition()),Is.False);
            var definition=(ScriptedSpellCardDefinition)card.ToDefinition();var amount=definition.EffectAmount;Assert.That(definition.Damage,Is.Zero,"Defense buff never introduces damage.");
            for(int level=0;level<=13;level++)Assert.That(PlasmaShieldRules.GetDefenseBonus(amount,level),Is.EqualTo(10),"Native defense effect level"+level);
            Assert.That(PlasmaShieldRules.GetDefenseBonus(amount,13),Is.EqualTo(10));
        }

        [Test]
        public void NativeAtlasHasAuthoredSingleRowFullCellsAndUniqueSpriteIdentities()
        {
            var importer=AssetImporter.GetAtPath(AtlasPath) as TextureImporter;Assert.That(importer,Is.Not.Null);
            Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite));Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
            var frames=Resources.LoadAll<Sprite>(FramesKey).OrderBy(sprite=>sprite.name,StringComparer.Ordinal).ToArray();
            Assert.That(frames.Length,Is.EqualTo(8));
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);Assert.That(atlas.width,Is.EqualTo(8*512));Assert.That(atlas.height,Is.EqualTo(512));
            var ids=new List<long>();var expectedGuid=AssetDatabase.AssetPathToGUID(AtlasPath);
            for(int i=0;i<8;i++)
            {
                Assert.That(frames[i].name,Is.EqualTo("Formation_"+i));Assert.That(frames[i].rect,Is.EqualTo(new Rect(i*512,0,512,512)));
                Assert.That(frames[i].pivot,Is.EqualTo(new Vector2(256,256)));Assert.That(frames[i].vertices.Length,Is.EqualTo(4));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(frames[i],out string guid,out long id),Is.True);
                Assert.That(guid,Is.EqualTo(expectedGuid));Assert.That(id,Is.Not.Zero);ids.Add(id);
            }
            Assert.That(ids.Distinct().Count(),Is.EqualTo(8));
        }

        [Test]
        public void AuthoredFramesKeepTheirTransparencyWithoutAddingMarginsOrRepainting()
        {
            var image=ReadPng(AtlasPath);Assert.That(image.width,Is.EqualTo(8*512));Assert.That(image.height,Is.EqualTo(512));
            for(int frame=0;frame<8;frame++)
            {
                int visible=0,transparent=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    var alpha=image.pixels[y*image.width+frame*512+x].a;
                    if(alpha==0)transparent++;else visible++;
                }
                Assert.That(visible,Is.GreaterThan(64));Assert.That(transparent,Is.GreaterThan(64));
            }
        }

        [Test]
        public void CardPortraitIsOneWholeAuthoredImageWithStableResourceLookup()
        {
            var portrait=Resources.Load<Sprite>("Project333/CardArtwork/PlasmaShield");Assert.That(portrait,Is.Not.Null);
            var importer=AssetImporter.GetAtPath(PortraitPath) as TextureImporter;Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.isReadable,Is.False);Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            var authored=ReadPng(PortraitPath);
            Assert.That(portrait.rect,Is.EqualTo(new Rect(0,0,authored.width,authored.height)));
            Assert.That(portrait.pivot,Is.EqualTo(new Vector2(authored.width*.5f,authored.height*.5f)));Assert.That(portrait.vertices.Length,Is.EqualTo(4));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(PortraitPath).OfType<Sprite>().Count(),Is.EqualTo(1),"The existing full card artwork remains a single sprite.");
            var cardImage=ReadPng(PortraitPath);var atlas=ReadPng(AtlasPath);Assert.That(cardImage.width,Is.EqualTo(authored.width));Assert.That(cardImage.height,Is.EqualTo(authored.height));
            Assert.That(cardImage.pixels.Count(pixel=>pixel.a>=16),Is.GreaterThan(1000),"Supplied whole user portrait remains visible; it is independent of the VFX atlas.");
        }

        [Test]
        public void CatalogContainsOnePlasmaShieldAndRetainsValidUniqueExistingCardReferences()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Cards,Is.Not.Empty);Assert.That(catalog.Cards.All(asset=>asset!=null),Is.True);
            Assert.That(catalog.Cards.Count(asset=>asset.CardId==PlasmaShieldRules.CardId),Is.EqualTo(1));Assert.That(catalog.Cards.Single(asset=>asset.CardId==PlasmaShieldRules.CardId),Is.SameAs(card));
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