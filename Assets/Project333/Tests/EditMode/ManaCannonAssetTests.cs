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
    public sealed class ManaCannonAssetTests
    {
        const string UnitFolder="Assets/Unit/ManaCannon/";
        const string ResourcesFolder="Assets/Project333/Resources/Project333/Units/ManaCannon/";
        const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/ManaCannon.asset";
        const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/ManaCannon.png";
        const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";

        [Test]
        public void PrintedNativeCardMatchesConfirmedJsonAndIsRegisteredExactlyOnce()
        {
            var record=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(r=>r.Id=="ManaCannon");
            var asset=Card();var unit=(UnitCardDefinition)asset.ToDefinition();
            var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Cards,Does.Not.Contain(null));
            Assert.That(catalog.Cards.Count(c=>c.CardId=="ManaCannon"),Is.EqualTo(1));
            Assert.That(catalog.Cards.Single(c=>c.CardId=="ManaCannon"),Is.SameAs(asset));
            Assert.That(AssetDatabase.AssetPathToGUID(CatalogPath),Is.EqualTo("d932562c5c4766d43adce3f999c666c8"));
            Assert.That(asset.CardId,Is.EqualTo(record.Id));Assert.That(asset.DisplayName,Is.EqualTo(record.DisplayName).And.EqualTo("마나 대포"));
            Assert.That(asset.Rarity,Is.EqualTo(record.Rarity).And.EqualTo(CardRarity.Uncommon));
            Assert.That(asset.Affiliation,Is.EqualTo(record.Affiliation).And.EqualTo(CardAffiliation.Fantasy));
            Assert.That(asset.ChargeTileFootprint,Is.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(asset.EffectText,Is.EqualTo(record.EffectText).And.EqualTo(ManaCannonRules.EffectDescription));
            Assert.That(asset.SpecialEffectText,Is.Empty);Assert.That(record.IncludeInDraft&&record.IncludeInRewards,Is.True);
            Assert.That(unit.Cost.Mana,Is.EqualTo(2));Assert.That(unit.Cost.Qi+unit.Cost.Power+unit.Cost.Gold,Is.Zero);
            Assert.That(unit.Attack,Is.EqualTo(record.Attack).And.EqualTo(40),"Attack payment does not change the printed stat.");
            Assert.That(unit.Health,Is.EqualTo(record.Health).And.EqualTo(10));
            Assert.That(unit.AttackType,Is.EqualTo(AttackType.Ranged));Assert.That(unit.DamageType,Is.EqualTo(DamageType.Magic));
            Assert.That(unit.PhysicalDefense,Is.Zero);Assert.That(unit.MagicDefense,Is.Zero);Assert.That(unit.CanMove,Is.True);
            Assert.That(unit.Races,Is.Empty);
            Assert.That(unit.HasBerserker||unit.HasEndure||unit.HasShielder||unit.HasLifeSteal||unit.HasRush||
                unit.HasReplicate||unit.HasHiding||unit.HasFlying||unit.HasPiercing||unit.IsScience,Is.False);
            Assert.That(unit.SealboundOwnerTurnStarts+unit.SpellPower+unit.InvincibleOwnerTurns+unit.SciencePowerUpkeep,Is.Zero);
            Assert.That(unit.InvincibleDuration,Is.EqualTo(InvincibleDurationType.None));
            Assert.That(unit.MaxAttacksPerTurn,Is.EqualTo(1));Assert.That(unit.HitsPerAttack,Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(unit),Is.True);
            Assert.That(CardLevelStatRules.ApplyAttackBonus("ManaCannon",unit.Attack,13),Is.EqualTo(44));
            Assert.That(CardLevelStatRules.ApplyHpBonus("ManaCannon",unit.Health,13),Is.EqualTo(19));
        }

        [TestCase("Idle",8,true)]
        [TestCase("Attack",10,false)]
        [TestCase("Death",8,false)]
        public void NativeMotionKeepsFullCellsFrameOrderTimingAndFloor(string motion,int fps,bool loop)
        {
            string path=UnitFolder+motion+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites=Sprites(path);
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(4096));Assert.That(texture.height,Is.EqualTo(512));
            Assert.That(sprites.Length,Is.EqualTo(8));AssertImporter(path,false);
            for(int i=0;i<8;i++)
            {
                Assert.That(sprites[i].name,Is.EqualTo(motion+"_"+i));
                Assert.That(sprites[i].rect,Is.EqualTo(new Rect(i*512,0,512,512)));
                Assert.That(sprites[i].pivot,Is.EqualTo(new Vector2(256,256)));
                Assert.That(sprites[i].pixelsPerUnit,Is.EqualTo(100));
                Assert.That(sprites[i].vertices.Length,Is.EqualTo(4),"FullRect must include all weapons/effects.");
            }
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+motion+".anim");
            Assert.That(clip,Is.Not.Null);Assert.That(clip.frameRate,Is.EqualTo(fps));
            Assert.That(clip.length,Is.EqualTo(8f/fps).Within(.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.EqualTo(loop));
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
            Assert.That(keys.Length,Is.EqualTo(8));
            for(int i=0;i<8;i++){Assert.That(keys[i].time,Is.EqualTo(i/(float)fps).Within(.0001f));Assert.That(keys[i].value,Is.SameAs(sprites[i]));}
            if(motion=="Attack")Assert.That(keys[3].time,Is.EqualTo(.3f).Within(.0001f));
            AssertVisibleMargins(path,false);
        }

        [Test]
        public void ControllerHasSixExplicitStatesWithFixedIdleFallbacksAndNativeBoardReferences()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(UnitFolder+"ManaCannon.controller");
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
            var binding=EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite");
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+"Idle.anim");
            var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+"Run.anim");
            var idleKeys=AnimationUtility.GetObjectReferenceCurve(idle,binding);var runKeys=AnimationUtility.GetObjectReferenceCurve(run,binding);
            Assert.That(run.frameRate,Is.EqualTo(8));Assert.That(run.length,Is.EqualTo(1f).Within(.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(run).loopTime,Is.True);Assert.That(runKeys.Length,Is.EqualTo(8));
            for(int i=0;i<8;i++){Assert.That(runKeys[i].value,Is.SameAs(idleKeys[i].value));Assert.That(runKeys[i].time,Is.EqualTo(idleKeys[i].time));}
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

        [TestCase("ProjectileAtlas")]
        [TestCase("ExplosionAtlas")]
        public void VfxHasEightNativeSpritesInTopLeftReadingOrderAndWholePaddedCells(string atlas)
        {
            string path=ResourcesFolder+atlas+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var sprites=Sprites(path);
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(2048));Assert.That(texture.height,Is.EqualTo(1024));
            Assert.That(sprites.Length,Is.EqualTo(8));AssertImporter(path,true);
            for(int i=0;i<8;i++)
            {
                Assert.That(sprites[i].name,Is.EqualTo(atlas+"_"+i));
                Assert.That(sprites[i].rect,Is.EqualTo(new Rect(i%4*512,(1-i/4)*512,512,512)));
                Assert.That(sprites[i].pivot,Is.EqualTo(new Vector2(256,256)));
                Assert.That(sprites[i].vertices.Length,Is.EqualTo(4));
            }
            var resourceSprites=Resources.LoadAll<Sprite>("Project333/Units/ManaCannon/"+atlas).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            Assert.That(resourceSprites,Is.EqualTo(sprites));AssertVisibleMargins(path,true);
        }

        [TestCase("ManaShot",.400f)]
        [TestCase("ManaExplosion",.350f)]
        public void QuietShotAndExplosionUsePcmMonoPreservedSampleRateAndPreloadedShortClips(string sound,float duration)
        {
            string path=ResourcesFolder+"Audio/"+sound+".wav";
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.That(clip,Is.Not.Null);Assert.That(importer,Is.Not.Null);
            Assert.That(clip.channels,Is.EqualTo(1));Assert.That(clip.frequency,Is.EqualTo(44100));
            Assert.That(clip.length,Is.EqualTo(duration).Within(.002f));
            Assert.That(importer.forceToMono||importer.loadInBackground,Is.False);
            Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(importer.defaultSampleSettings.compressionFormat,Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(importer.defaultSampleSettings.sampleRateSetting,Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(importer.defaultSampleSettings.preloadAudioData,Is.True);
            Assert.That(Resources.Load<AudioClip>("Project333/Units/ManaCannon/Audio/"+sound),Is.SameAs(clip));
        }

        [Test]
        public void PortraitImportsWholeCompletedSingleImageAndDedicatedResourceResolves()
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
            try{Assert.That(CardArtworkLibrary.TryGetArtwork("ManaCannon",out var resolved),Is.True);Assert.That(resolved,Is.SameAs(sprite));}
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
        static void AssertVisibleMargins(string path,bool effect)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path),false),Is.True);
                var data=texture.GetPixels32();int columns=effect?4:8,rows=effect?2:1;
                for(int f=0;f<8;f++)
                {
                    int xmin=512,ymin=512,xmax=-1,ymax=-1,x0=f%columns*512,y0=(rows-1-f/columns)*512;
                    for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(data[(y0+y)*texture.width+x0+x].a>=16)
                    {xmin=Math.Min(xmin,x);ymin=Math.Min(ymin,y);xmax=Math.Max(xmax,x);ymax=Math.Max(ymax,y);}
                    Assert.That(xmax,Is.GreaterThanOrEqualTo(0),"Each full frame contains visible original pixels.");
                    Assert.That(xmin,Is.GreaterThanOrEqualTo(effect?16:8));Assert.That(xmax,Is.LessThanOrEqualTo(effect?495:503));
                    Assert.That(ymax,Is.LessThanOrEqualTo(effect?495:503));
                    if(effect)Assert.That(ymin,Is.GreaterThanOrEqualTo(16));else Assert.That(ymin,Is.EqualTo(16),"Fixed foot/collapse contact baseline, not per-frame height resizing.");
                }
            }finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        static Sprite[] Sprites(string path){return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();}
        static UnitCardDefinitionAsset Card(){var card=AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);Assert.That(card,Is.Not.Null);return card;}
    }
}

