using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    // Read-only integration: authored pixels -> native sprites/clips/controller -> registered card.
    public sealed class GaebangChiefAssetTests
    {
        const string UnitFolder="Assets/Unit/GaebangChief/";
        const string ResourcesFolder="Assets/Project333/Resources/Project333/Units/GaebangChief/";
        const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/GaebangChief.asset";
        const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png";
        const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";

        [Test]
        public void PrintedNativeCardMatchesConfirmedJsonAndIsRegisteredExactlyOnce()
        {
            var record=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(r=>r.Id=="GaebangChief");
            var asset=Card();var unit=(UnitCardDefinition)asset.ToDefinition();
            var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Cards,Does.Not.Contain(null));
            Assert.That(catalog.Cards.Count(c=>c.CardId=="GaebangChief"),Is.EqualTo(1));
            Assert.That(catalog.Cards.Single(c=>c.CardId=="GaebangChief"),Is.SameAs(asset));
            Assert.That(AssetDatabase.AssetPathToGUID(CatalogPath),Is.EqualTo("d932562c5c4766d43adce3f999c666c8"));
            Assert.That(asset.CardId,Is.EqualTo(record.Id));Assert.That(asset.DisplayName,Is.EqualTo(record.DisplayName).And.EqualTo("개방 방주"));
            Assert.That(asset.Rarity,Is.EqualTo(record.Rarity).And.EqualTo(CardRarity.Unique));
            Assert.That(asset.Affiliation,Is.EqualTo(record.Affiliation).And.EqualTo(CardAffiliation.Murim));
            Assert.That(asset.ChargeTileFootprint,Is.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(asset.EffectText,Is.EqualTo(record.EffectText).And.EqualTo(GaebangChiefRules.EffectDescription));
            Assert.That(asset.SpecialEffectText,Is.Empty);Assert.That(record.IncludeInDraft&&record.IncludeInRewards,Is.True);
            Assert.That(unit.Cost.Qi,Is.EqualTo(7));Assert.That(unit.Cost.Mana+unit.Cost.Power+unit.Cost.Gold,Is.Zero);
            Assert.That(unit.Attack,Is.EqualTo(record.Attack).And.EqualTo(10),"Gold is a live battle bonus, not a native printed stat.");
            Assert.That(unit.Health,Is.EqualTo(record.Health).And.EqualTo(60));
            Assert.That(unit.AttackType,Is.EqualTo(AttackType.Melee));Assert.That(unit.DamageType,Is.EqualTo(DamageType.Physical));
            Assert.That(unit.PhysicalDefense,Is.Zero);Assert.That(unit.MagicDefense,Is.Zero);Assert.That(unit.CanMove,Is.True);
            Assert.That(unit.Races,Is.EqualTo(new[]{UnitRace.Human}));
            Assert.That(unit.HasBerserker||unit.HasEndure||unit.HasShielder||unit.HasLifeSteal||unit.HasRush||
                unit.HasReplicate||unit.HasHiding||unit.HasFlying||unit.HasPiercing||unit.IsScience,Is.False);
            Assert.That(unit.SealboundOwnerTurnStarts+unit.SpellPower+unit.InvincibleOwnerTurns+unit.SciencePowerUpkeep,Is.Zero);
            Assert.That(unit.InvincibleDuration,Is.EqualTo(InvincibleDurationType.None));
            Assert.That(unit.MaxAttacksPerTurn,Is.EqualTo(1));Assert.That(unit.HitsPerAttack,Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(unit),Is.True);
            Assert.That(CardLevelStatRules.ApplyAttackBonus("GaebangChief",unit.Attack,13),Is.EqualTo(14));
            Assert.That(CardLevelStatRules.ApplyHpBonus("GaebangChief",unit.Health,13),Is.EqualTo(69));
        }

        [TestCase("Idle",12,true)]
        [TestCase("Attack",12,false)]
        [TestCase("Run",12,true)]
        [TestCase("Death",8,false)]
        public void NativeMotionKeepsFullCellsFrameOrderTimingAndFloor(string motion,int fps,bool loop)
        {
            string path=UnitFolder+motion+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites=Sprites(path);
            bool idle=motion=="Idle";int frameCount=idle?4:8;
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(idle?2048:4096));Assert.That(texture.height,Is.EqualTo(512));
            Assert.That(sprites.Length,Is.EqualTo(frameCount));AssertImporter(path,false);
            for(int i=0;i<frameCount;i++)
            {
                Assert.That(sprites[i].name,Is.EqualTo(motion+"_"+i));
                Assert.That(sprites[i].rect,Is.EqualTo(new Rect(i*512,0,512,512)));
                Assert.That(sprites[i].pivot,Is.EqualTo(new Vector2(256,256)));
                Assert.That(sprites[i].pixelsPerUnit,Is.EqualTo(100));
                Assert.That(sprites[i].vertices.Length,Is.EqualTo(4),"FullRect must include all weapons/effects.");
            }
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+motion+".anim");
            Assert.That(clip,Is.Not.Null);Assert.That(clip.frameRate,Is.EqualTo(fps));
            Assert.That(clip.length,Is.EqualTo(frameCount/(float)fps).Within(.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.EqualTo(loop));
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
            Assert.That(keys.Length,Is.EqualTo(frameCount));
            for(int i=0;i<frameCount;i++){Assert.That(keys[i].time,Is.EqualTo(i/(float)fps).Within(.0001f));Assert.That(keys[i].value,Is.SameAs(sprites[i]));}
            if(motion=="Attack")Assert.That(keys[3].time,Is.EqualTo(.25f).Within(.0001f));
            AssertVisibleMargins(path,false);
        }

        [Test]
        public void ControllerHasSixExplicitStatesWithFixedIdleFallbacksAndNativeBoardReferences()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(UnitFolder+"GaebangChief.controller");
            Assert.That(controller,Is.Not.Null);Assert.That(controller.layers.Length,Is.EqualTo(1));
            var machine=controller.layers[0].stateMachine;
            Assert.That(machine.defaultState.name,Is.EqualTo("Idle"));
            Assert.That(machine.states.Select(s=>s.state.name),Is.EquivalentTo(new[]{"Idle","Attack","Run","Death","BeAttacked","Drained"}));
            Assert.That(machine.anyStateTransitions,Is.Empty);Assert.That(machine.entryTransitions,Is.Empty);
            foreach(var child in machine.states)
            {
                var state=child.state;
                Assert.That(state.speed,Is.EqualTo(1));Assert.That(state.transitions,Is.Empty);
                Assert.That(state.motion,Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+state.name+".anim")));
            }
            var firstIdle=Sprites(UnitFolder+"Idle.png")[0];
            foreach(string name in new[]{"BeAttacked","Drained"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+name+".anim");
                var keys=AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
                Assert.That(keys.Length,Is.EqualTo(1));Assert.That(keys[0].time,Is.Zero);Assert.That(keys[0].value,Is.SameAs(firstIdle));
                Assert.That(clip.frameRate,Is.EqualTo(name=="BeAttacked"?12:1));
                Assert.That(clip.length,Is.EqualTo(name=="BeAttacked"?1f/12:1f).Within(.001f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.EqualTo(name=="Drained"));
            }
            Assert.That(Card().BoardAnimatorController,Is.SameAs(controller));Assert.That(Card().BoardSprite,Is.SameAs(firstIdle));
        }

        [Test]
        public void CoinImpactHasEightNativeSpritesInTopLeftReadingOrderAndWholePaddedCells()
        {
            string path=ResourcesFolder+"CoinImpactAtlas.png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var sprites=Sprites(path);
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(2048));Assert.That(texture.height,Is.EqualTo(1024));
            Assert.That(sprites.Length,Is.EqualTo(8));AssertImporter(path,true);
            for(int i=0;i<8;i++)
            {
                Assert.That(sprites[i].name,Is.EqualTo("CoinImpactAtlas_"+i));
                Assert.That(sprites[i].rect,Is.EqualTo(new Rect(i%4*512,(1-i/4)*512,512,512)));
                Assert.That(sprites[i].pivot,Is.EqualTo(new Vector2(256,256)));
                Assert.That(sprites[i].vertices.Length,Is.EqualTo(4));
            }
            var resourceSprites=Resources.LoadAll<Sprite>("Project333/Units/GaebangChief/CoinImpactAtlas").OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            Assert.That(resourceSprites,Is.EqualTo(sprites));
            AssertVisibleMargins(path,true);
        }

        [Test]
        public void QuietCoinPunchUsesPcmMonoPreservedSampleRateAndPreloadedShortClip()
        {
            string path=ResourcesFolder+"Audio/CoinPunch.wav";
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.That(clip,Is.Not.Null);Assert.That(importer,Is.Not.Null);
            Assert.That(clip.channels,Is.EqualTo(1));Assert.That(clip.frequency,Is.EqualTo(44100));
            Assert.That(clip.length,Is.EqualTo(.44f).Within(.002f));
            Assert.That(importer.forceToMono||importer.loadInBackground,Is.False);
            Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(importer.defaultSampleSettings.compressionFormat,Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(importer.defaultSampleSettings.sampleRateSetting,Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(importer.defaultSampleSettings.preloadAudioData,Is.True);
            Assert.That(Resources.Load<AudioClip>("Project333/Units/GaebangChief/Audio/CoinPunch"),Is.SameAs(clip));
        }

        [Test]
        public void PortraitImportsEntireOriginalImageAndDedicatedResourceResolves()
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
            var importer=AssetImporter.GetAtPath(PortraitPath) as TextureImporter;
            Assert.That(sprite,Is.Not.Null);Assert.That(importer,Is.Not.Null);
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(sprite.rect,Is.EqualTo(new Rect(0,0,width,height)));
            Assert.That(sprite.pivot,Is.EqualTo(new Vector2(width*.5f,height*.5f)));
            CardArtworkLibrary.ClearCacheForTests();
            try{Assert.That(CardArtworkLibrary.TryGetArtwork("GaebangChief",out var resolved),Is.True);Assert.That(resolved,Is.SameAs(sprite));}
            finally{CardArtworkLibrary.ClearCacheForTests();}
        }

        static void AssertImporter(string path,bool readable)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;Assert.That(importer,Is.Not.Null);
            Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite));Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));Assert.That(importer.alphaIsTransparency,Is.True);
            Assert.That(importer.maxTextureSize,Is.GreaterThanOrEqualTo(4096));Assert.That(importer.isReadable,Is.EqualTo(readable));
            Assert.That(importer.GetPlatformTextureSettings("Standalone").overridden,Is.False);
            Assert.That(importer.GetPlatformTextureSettings("Android").overridden,Is.False);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType,Is.EqualTo(SpriteMeshType.FullRect));
        }
        static void AssertVisibleMargins(string path,bool coin)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path),false),Is.True);
                bool idle=path==UnitFolder+"Idle.png";
                var data=texture.GetPixels32();int columns=coin||idle?4:8,rows=coin?2:1;
                int frameCount=idle?4:8;
                for(int f=0;f<frameCount;f++)
                {
                    int xmin=512,ymin=512,xmax=-1,ymax=-1,x0=f%columns*512,y0=(rows-1-f/columns)*512;
                    for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(data[(y0+y)*texture.width+x0+x].a>=16)
                    {xmin=Math.Min(xmin,x);ymin=Math.Min(ymin,y);xmax=Math.Max(xmax,x);ymax=Math.Max(ymax,y);}
                    Assert.That(xmax,Is.GreaterThanOrEqualTo(0),"Each full frame contains visible original pixels.");
                    Assert.That(xmin,Is.GreaterThanOrEqualTo(coin?16:8));Assert.That(xmax,Is.LessThanOrEqualTo(coin?495:503));
                    Assert.That(ymax,Is.LessThanOrEqualTo(coin?495:503));
                    if(coin)Assert.That(ymin,Is.GreaterThanOrEqualTo(16));
                    else Assert.That(ymin,Is.EqualTo(16),"Fixed foot/collapse contact baseline, not per-frame height resizing.");
                }
            }finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        static Sprite[] Sprites(string path){return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();}
        static UnitCardDefinitionAsset Card(){var card=AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);Assert.That(card,Is.Not.Null);return card;}
    }
}
