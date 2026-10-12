// One-time native authoring. Root runs this only in the exclusive isolated Editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportManaShieldRegistration
{
    public const string CardId=ManaShieldRules.CardId;
    public const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/ManaShield.asset";
    public const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    public const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/ManaShield.png";
    public const string AtlasPath="Assets/Project333/Resources/Project333/SpellEffects/ManaShield/Formation.png";
    public const string FramesResourcePath="Project333/SpellEffects/ManaShield/Formation";
    public const string JsonPath="Assets/Project333/Resources/Project333/Data/cards.json";
    const int CellSize=512;

    public static string Run()
    {
        var database=JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        var validation=CardDatabaseValidator.Validate(database);
        if(!validation.IsValid)throw new InvalidOperationException(string.Join("\n",validation.Errors));
        var record=database.Cards.Single(r=>r.Id==CardId);RequireRecord(record);
        var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if(catalog==null||catalog.Cards.Any(c=>c==null))throw new InvalidOperationException("Missing/invalid existing card catalog.");
        var before=catalog.Cards.ToArray();var prefix=before.Select(Identity).ToArray();
        var catalogGuid=AssetDatabase.AssetPathToGUID(CatalogPath);
        var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
        if(card==null&&File.Exists(CardPath)||card!=null&&card.CardId!=CardId)throw new InvalidOperationException("ManaShield native path belongs to a different asset; preserve it.");
        var matching=before.Where(c=>c.CardId==CardId).ToArray();
        if(matching.Length>1||matching.Length==1&&matching[0]!=card)throw new InvalidOperationException("Conflicting ManaShield catalog reference.");
        var pngs=new[]{PortraitPath,AtlasPath}.ToDictionary(path=>path,File.ReadAllBytes);
        ValidateSource(PortraitPath,1,1,false);ValidateSource(AtlasPath,4,2,true);ValidatePortraitFrameFour();
        // Every requested capability is checked on both sources before either importer is mutated.
        foreach(var path in pngs.Keys)RequireProvider(path);
        var retainedGuid=pngs.Keys.Concat(new[]{CardPath}).Select(path=>new{Path=path,Guid=AssetDatabase.AssetPathToGUID(path)})
            .Where(row=>!string.IsNullOrEmpty(row.Guid)).ToDictionary(row=>row.Path,row=>row.Guid);
        var retainedIds=pngs.Keys.ToDictionary(path=>path,path=>Sprites(path).ToDictionary(sprite=>sprite.name,LocalId));
        if(retainedIds[AtlasPath].Keys.Any(name=>!Enumerable.Range(0,8).Select(i=>"Formation_"+i).Contains(name)))
            throw new InvalidOperationException("Unexpected existing VFX sprite names; preserve artist metadata.");
        if(retainedIds[PortraitPath].Keys.Any(name=>name!=CardId))throw new InvalidOperationException("Unexpected existing portrait sprite name.");
        Slice(PortraitPath,CardId,1,1,false);Slice(AtlasPath,"Formation",4,2,true);
        if(card==null){card=ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();card.name=CardId;AssetDatabase.CreateAsset(card,CardPath);}
        card.ConfigureBaseForTests(record.Id,record.DisplayName,new ResourceSetData(record.Cost.Mana,record.Cost.Qi,record.Cost.Power,record.Cost.Gold));
        card.ConfigureMetadataForTests(record.Rarity,record.Affiliation,record.ChargeTileFootprint,record.EffectText,record.SpecialEffectText);
        card.ConfigureForTests(record.EffectId,record.Damage,record.DamageType,record.TriggerCount,record.EffectAmount);card.ConfigureReplicateForTests(record.HasReplicate);
        // Spell artwork is loaded through CardArtworkLibrary; it has no summoned board visual.
        card.ConfigureBoardVisualsForTests(null,null);EditorUtility.SetDirty(card);AssetDatabase.SaveAssetIfDirty(card);
        if(matching.Length==0){catalog.ConfigureForTests(before.Concat(new[]{card}).ToArray());EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
        foreach(var row in pngs)if(!row.Value.SequenceEqual(File.ReadAllBytes(row.Key)))throw new InvalidOperationException("Native import changed source PNG bytes: "+row.Key);
        foreach(var row in retainedGuid)if(AssetDatabase.AssetPathToGUID(row.Key)!=row.Value)throw new InvalidOperationException("Retained native GUID changed: "+row.Key);
        foreach(var row in retainedIds)foreach(var sprite in Sprites(row.Key))
            if(row.Value.TryGetValue(sprite.name,out var id)&&LocalId(sprite)!=id)throw new InvalidOperationException("Retained native fileID changed: "+sprite.name);
        if(catalog.Cards.Count!=before.Length+(matching.Length==0?1:0)||catalog.Cards.Count(c=>c.CardId==CardId)!=1||
            !catalog.Cards.Take(before.Length).SequenceEqual(before)||!catalog.Cards.Take(before.Length).Select(Identity).SequenceEqual(prefix)||
            AssetDatabase.AssetPathToGUID(CatalogPath)!=catalogGuid)throw new InvalidOperationException("Catalog old ordered references/GUID changed or ManaShield duplicated.");
        return Validate()+"; catalog "+before.Length+" -> "+catalog.Cards.Count+"; card GUID "+AssetDatabase.AssetPathToGUID(CardPath)+"; original PNGs/GUIDs/named fileIDs preserved";
    }
    static void RequireRecord(JsonCardDefinitionRecord record)
    {
        if(record.Id!=CardId||record.DisplayName!="마나쉴드"||record.DefinitionType!=JsonCardDefinitionKind.ScriptedSpell||
            record.EffectId!=ManaShieldRules.EffectId||record.Rarity!=CardRarity.Common||record.Affiliation!=CardAffiliation.Fantasy||
            record.Cost.Mana!=ManaShieldRules.ManaCost||record.Cost.Qi!=0||record.Cost.Power!=0||record.Cost.Gold!=0||
            record.Damage!=0||record.DamageType!=DamageType.None||record.EffectAmount!=ManaShieldRules.BaseDefenseBonus||
            record.TriggerCount!=0||record.ChargeTileFootprint!=ChargeTileFootprint.None||!record.IncludeInDraft||record.IncludeInRewards||
            record.HasReplicate||!string.IsNullOrEmpty(record.SpecialEffectText)||record.EffectText!=ManaShieldRules.EffectDescription)
            throw new InvalidOperationException("ManaShield JSON differs from confirmed non-damage defense-buff metadata.");
        if(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()))throw new InvalidOperationException("ManaShield must remain non-upgradeable.");
        for(int level=0;level<=13;level++)if(ManaShieldRules.GetDefenseBonus(record.EffectAmount,level)!=10)
            throw new InvalidOperationException("ManaShield defense bonus must stay 10 even with an old stored level.");
    }
    static ISpriteEditorDataProvider RequireProvider(string path)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException("Stage reviewed PNG first: "+path);
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Native sprite provider absent; abort.");provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null)throw new InvalidOperationException("Native edit capability absent; abort.");
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported native sprite operation "+edit+"; abort.");
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Native sprite name/fileID provider absent; abort.");
        return provider;
    }
    static void Slice(string path,string name,int columns,int rows,bool multiple)
    {
        var provider=RequireProvider(path);var previous=provider.GetSpriteRects();
        var rects=Enumerable.Range(0,columns*rows).Select(i=>{
            var spriteName=multiple?name+"_"+i:name;var prior=previous.FirstOrDefault(rect=>rect.name==spriteName);
            return new SpriteRect {name=spriteName,rect=FrameRect(i,columns,rows),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,
                border=Vector4.zero,spriteID=prior==null?GUID.Generate():prior.spriteID};}).ToArray();
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=multiple?SpriteImportMode.Multiple:SpriteImportMode.Single;importer.maxTextureSize=2048;
        importer.npotScale=TextureImporterNPOTScale.None;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.isReadable=multiple;
        importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        provider=RequireProvider(path);provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect=>new SpriteNameFileIdPair(rect.name,rect.spriteID)).ToList());
        provider.Apply();importer.SaveAndReimport();
    }
    static Rect FrameRect(int i,int columns,int rows)=>new Rect(i%columns*CellSize,(rows-1-i/columns)*CellSize,CellSize,CellSize);
    static Sprite[] Sprites(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite=>sprite.name,StringComparer.Ordinal).ToArray();
    static long LocalId(Sprite sprite){if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long id)||string.IsNullOrEmpty(guid)||id==0)throw new InvalidOperationException("Missing native sprite identity.");return id;}
    static string Identity(CardDefinitionAsset card)=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(card))+":"+card.CardId;
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    static void ValidateSource(string path,int columns,int rows,bool margin)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path),false)||texture.width!=columns*CellSize||texture.height!=rows*CellSize)throw new InvalidOperationException("Require whole512 cells: "+path);
            var pixels=texture.GetPixels32();for(int i=0;i<columns*rows;i++)
            {
                int count=0;for(int y=0;y<CellSize;y++)for(int x=0;x<CellSize;x++)
                {
                    var alpha=pixels[((rows-1-i/columns)*CellSize+y)*texture.width+i%columns*CellSize+x].a;if(alpha==0)continue;count++;
                    if(margin&&(x<16||y<16||x>495||y>495))throw new InvalidOperationException("VFX touches16px margin: "+i);
                }
                if(count==0)throw new InvalidOperationException("Empty ManaShield frame "+i);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    // Card art is one completed sphere, never the whole sheet. Verify before native metadata changes.
    static void ValidatePortraitFrameFour()
    {
        var portrait=new Texture2D(2,2,TextureFormat.RGBA32,false);
        var atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!portrait.LoadImage(File.ReadAllBytes(PortraitPath),false)||!atlas.LoadImage(File.ReadAllBytes(AtlasPath),false))
                throw new InvalidOperationException("Cannot decode ManaShield portrait/Formation source.");
            if(portrait.width!=512||portrait.height!=512||atlas.width!=2048||atlas.height!=1024)
                throw new InvalidOperationException("ManaShield whole512 native inputs required.");
            var actual=portrait.GetPixels32();var pixels=atlas.GetPixels32();
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                if(!actual[y*512+x].Equals(pixels[y*2048+x]))
                    throw new InvalidOperationException("ManaShield portrait must preserve whole Formation frame4 rawRGBA.");
        }
        finally{UnityEngine.Object.DestroyImmediate(portrait);UnityEngine.Object.DestroyImmediate(atlas);}
    }
    static void ValidateSprites(string path,string name,int columns,int rows,bool multiple)
    {
        ValidateSource(path,columns,rows,multiple);var importer=AssetImporter.GetAtPath(path) as TextureImporter;var sprites=Sprites(path);
        if(importer==null||importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=(multiple?SpriteImportMode.Multiple:SpriteImportMode.Single)||
            importer.spritePixelsPerUnit!=100||importer.mipmapEnabled||importer.filterMode!=FilterMode.Point||importer.alphaSource!=TextureImporterAlphaSource.FromInput||
            !importer.alphaIsTransparency||importer.isReadable!=multiple||importer.textureCompression!=TextureImporterCompression.Uncompressed||sprites.Length!=columns*rows)
            throw new InvalidOperationException("ManaShield native texture settings/count mismatch.");
        for(int i=0;i<sprites.Length;i++)if(sprites[i].name!=(multiple?name+"_"+i:name)||sprites[i].rect!=FrameRect(i,columns,rows)||
            sprites[i].pivot!=new Vector2(256,256)||sprites[i].pixelsPerUnit!=100||sprites[i].vertices.Length!=4)throw new InvalidOperationException("ManaShield native fullrect/grid/center mismatch.");
        foreach(var sprite in sprites)LocalId(sprite);
    }
    public static string Validate()
    {
        ValidateSprites(PortraitPath,CardId,1,1,false);ValidateSprites(AtlasPath,"Formation",4,2,true);
        if(Resources.Load<Sprite>("Project333/CardArtwork/ManaShield")!=Sprites(PortraitPath).Single()||
            !Resources.LoadAll<Sprite>(FramesResourcePath).OrderBy(sprite=>sprite.name,StringComparer.Ordinal).SequenceEqual(Sprites(AtlasPath)))throw new InvalidOperationException("Native resource lookup mismatch.");
        var database=JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));var record=database.Cards.Single(card=>card.Id==CardId);RequireRecord(record);
        var card=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);var definition=card==null?null:card.ToDefinition() as ScriptedSpellCardDefinition;
        if(card==null||definition==null||definition.CardId!=record.Id||definition.DisplayName!=record.DisplayName||definition.EffectId!=record.EffectId||
            definition.Damage!=record.Damage||definition.DamageType!=record.DamageType||definition.EffectAmount!=record.EffectAmount||definition.TriggerCount!=record.TriggerCount||definition.Cost.Power!=0||definition.Cost.Mana!=1||definition.Cost.Qi!=0||definition.Cost.Gold!=0||
            card.Rarity!=record.Rarity||card.Affiliation!=record.Affiliation||card.ChargeTileFootprint!=record.ChargeTileFootprint||
            card.EffectText!=record.EffectText||card.SpecialEffectText!=record.SpecialEffectText||card.HasReplicate||card.BoardSprite!=null||card.BoardAnimatorController!=null)
            throw new InvalidOperationException("Native ManaShield card differs from canonical JSON.");
        return "ManaShield native: Fantasy/Common/Mana1, EffectAmount MagicDefense10+Lv0..13, Damage0/None, permanent stackable target buff, draft/rewardsOn; full512portrait from Formation frame4 +8 full512VFX/top-left row order/center/PPU100/Point/FullRect/noMip/inputAlpha/uncompressed; VFX readable; portrait CPU copy disabled";
    }
    [Serializable] public sealed class AssetIdentity {public string Path,Guid,SHA256,MetaSHA256;public string[] SpriteNames;public long[] SpriteFileIds;}
    [Serializable] public sealed class Report
    {
        public string First,Second;public int CatalogBefore,CatalogAfter,ProtectedInputs;public string[] OriginalOrderedReferences;
        public bool Idempotent,OriginalOrderedPrefixPreserved,SourcePngsPreserved,ProtectedInputsPreserved;public AssetIdentity[] Assets;
    }
    static AssetIdentity Capture(string path)=>new AssetIdentity {Path=path,Guid=AssetDatabase.AssetPathToGUID(path),SHA256=Hash(path),MetaSHA256=Hash(path+".meta"),SpriteNames=Sprites(path).Select(sprite=>sprite.name).ToArray(),SpriteFileIds=Sprites(path).Select(LocalId).ToArray()};
    static IEnumerable<string> ProtectedPaths()
    {
        foreach(var folder in new[]{"Assets/Unit","Assets/Project333/ScriptableObjects/StarterTen/Cards","Assets/Project333/Resources/Project333"})
            if(Directory.Exists(folder))foreach(var path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                if(path.Replace('\\','/')!=CardPath&&path.Replace('\\','/')!=CardPath+".meta"&&path.Replace('\\','/')!=PortraitPath&&path.Replace('\\','/')!=PortraitPath+".meta"&&path.Replace('\\','/')!=AtlasPath&&path.Replace('\\','/')!=AtlasPath+".meta")yield return path;
    }
    // Root only: -executeMethod ImportManaShieldRegistration.Batch, optional PROJECT333_MANA_SHIELD_NATIVE_REPORT.
    public static void Batch()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);if(catalog==null)throw new InvalidOperationException("Missing catalog.");
        var protectedInputs=ProtectedPaths().Distinct().ToDictionary(path=>path,Hash);var source=new[]{PortraitPath,AtlasPath}.ToDictionary(path=>path,Hash);
        var report=new Report{CatalogBefore=catalog.Cards.Count,OriginalOrderedReferences=catalog.Cards.Select(Identity).ToArray(),ProtectedInputs=protectedInputs.Count,First=Run()};
        var paths=new[]{PortraitPath,AtlasPath,CardPath,CatalogPath};report.Assets=paths.Select(Capture).ToArray();report.Second=Run();
        var after=paths.Select(Capture).ToArray();report.CatalogAfter=catalog.Cards.Count;
        report.Idempotent=report.Assets.Zip(after,(a,b)=>a.Guid==b.Guid&&a.SHA256==b.SHA256&&a.MetaSHA256==b.MetaSHA256&&a.SpriteFileIds.SequenceEqual(b.SpriteFileIds)).All(equal=>equal);
        report.OriginalOrderedPrefixPreserved=catalog.Cards.Take(report.CatalogBefore).Select(Identity).SequenceEqual(report.OriginalOrderedReferences)&&catalog.Cards.Count(card=>card.CardId==CardId)==1;
        report.SourcePngsPreserved=source.All(row=>row.Value==Hash(row.Key));report.ProtectedInputsPreserved=protectedInputs.All(row=>File.Exists(row.Key)&&row.Value==Hash(row.Key));
        var target=Environment.GetEnvironmentVariable("PROJECT333_MANA_SHIELD_NATIVE_REPORT");if(string.IsNullOrWhiteSpace(target))target=Path.GetFullPath("ManaShield-native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));File.WriteAllText(target,JsonUtility.ToJson(report,true));
        if(!report.Idempotent||!report.OriginalOrderedPrefixPreserved||!report.SourcePngsPreserved||!report.ProtectedInputsPreserved)throw new InvalidOperationException("Native ManaShield invariants failed: "+target);
        Debug.Log("PASS ManaShield native import twice: "+target);
    }
}