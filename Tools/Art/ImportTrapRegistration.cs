using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Domain.Effects;

public static class ImportTrapRegistration
{
    const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/TrapReturn.asset";
    const string ArtFolder="Assets/Project333/Resources/Project333/CardArtwork/";
    const string JsonPath="Assets/Project333/Resources/Project333/Data/cards.json";
    static readonly string[] ArtIds={"TrapHidden","TrapReturn"};
    static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    static string Identity(CardDefinitionAsset c)=>AssetDatabase.GetAssetPath(c)+"|"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c));
    static long LocalId(Sprite sprite){if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string _,out long id))throw new InvalidOperationException("Missing native sprite identity.");return id;}
    static ISpriteEditorDataProvider Provider(string path)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing reviewed artwork: "+path);
        var factory=new SpriteDataProviderFactories();factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Missing native sprite provider.");provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Missing native sprite capability.");
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported operation "+edit);
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Missing sprite name/fileID provider.");
        return provider;
    }
    static void ImportPortrait(string id)
    {
        var path=ArtFolder+id+".png";var provider=Provider(path);
        var previous=provider.GetSpriteRects();
        if(previous.Any(r=>r.name!=id))throw new InvalidOperationException("Unexpected existing artist sprite name: "+path);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        int width,height;try{if(!texture.LoadImage(File.ReadAllBytes(path),false))throw new InvalidOperationException("PNG decode failed.");width=texture.width;height=texture.height;}finally{UnityEngine.Object.DestroyImmediate(texture);}
        var prior=previous.SingleOrDefault();
        var rect=new SpriteRect{name=id,rect=new Rect(0,0,width,height),border=Vector4.zero,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=prior==null?GUID.Generate():prior.spriteID};
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
        importer.isReadable=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);
        provider.SetSpriteRects(new[]{rect});
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(rect.name,rect.spriteID)});
        provider.Apply();importer.SaveAndReimport();
        var sprite=Resources.Load<Sprite>("Project333/CardArtwork/"+id);
        if(sprite==null||sprite.rect!=rect.rect||sprite.pivot!=new Vector2(width/2f,height/2f)||sprite.vertices.Length!=4||sprite.pixelsPerUnit!=100)
            throw new InvalidOperationException("Native full portrait mismatch: "+id);
    }
    static void Run()
    {
        var json=JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));var valid=CardDatabaseValidator.Validate(json);
        if(!valid.IsValid)throw new InvalidOperationException(string.Join("\n",valid.Errors));
        var r=json.Cards.Single(c=>c.Id==TrapReturnCardRules.CardId);
        var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if(catalog==null||catalog.Cards.Any(c=>c==null))throw new InvalidOperationException("Invalid prior catalog.");
        var before=catalog.Cards.ToArray();var prefix=before.Select(Identity).ToArray();var catalogGuid=AssetDatabase.AssetPathToGUID(CatalogPath);
        var card=AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
        if(card==null&&File.Exists(CardPath)||card!=null&&card.CardId!=r.Id)throw new InvalidOperationException("Conflicting prior card asset.");
        var matches=before.Where(c=>c.CardId==r.Id).ToArray();if(matches.Length>1||matches.Length==1&&matches[0]!=card)throw new InvalidOperationException("Conflicting catalog entry.");
        var hashes=ArtIds.ToDictionary(id=>id,id=>Hash(ArtFolder+id+".png"));
        foreach(var id in ArtIds)Provider(ArtFolder+id+".png");
        foreach(var id in ArtIds)ImportPortrait(id);
        if(card==null){card=ScriptableObject.CreateInstance<TrapSpellCardDefinitionAsset>();card.name=r.Id;AssetDatabase.CreateAsset(card,CardPath);}
        card.ConfigureBaseForTests(r.Id,r.DisplayName,new ResourceSetData());
        card.ConfigureMetadataForTests(r.Rarity,r.Affiliation,r.ChargeTileFootprint,r.EffectText,r.SpecialEffectText);
        card.ConfigureTrapForTests(new ResourceSetData(r.TrapActivationCost.Mana,r.TrapActivationCost.Qi,r.TrapActivationCost.Power,r.TrapActivationCost.Gold),r.TrapTrigger,r.TrapEffect);
        card.ConfigureReplicateForTests(false);card.ConfigureBoardVisualsForTests(null,null);EditorUtility.SetDirty(card);AssetDatabase.SaveAssetIfDirty(card);
        if(matches.Length==0){catalog.ConfigureForTests(before.Concat(new[]{card}).ToArray());EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
        if(!catalog.Cards.Take(before.Length).Select(Identity).SequenceEqual(prefix)||AssetDatabase.AssetPathToGUID(CatalogPath)!=catalogGuid||catalog.Cards.Count(c=>c.CardId==r.Id)!=1)
            throw new InvalidOperationException("Prior catalog references/GUID changed or duplicate trap.");
        foreach(var row in hashes)if(Hash(ArtFolder+row.Key+".png")!=row.Value)throw new InvalidOperationException("PNG source changed.");
        var def=(TrapSpellCardDefinition)card.ToDefinition();
        if(def.ActivationCost.Gold!=4||def.Cost.Mana+def.Cost.Qi+def.Cost.Power+def.Cost.Gold!=0||CardUpgradeRules.IsCardUpgradeable(def)||!def.HasTrap||card.BoardSprite!=null||card.BoardAnimatorController!=null)
            throw new InvalidOperationException("Native trap rule mismatch.");
    }
    [Serializable] public sealed class Report{public int Before,After;public bool Idempotent,PriorPrefixPreserved,SourcePngsPreserved;public string CardGuid,HiddenGuid,ReturnGuid;public long HiddenFileId,ReturnFileId;}
    public static void Batch()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        var prefix=catalog.Cards.Select(Identity).ToArray();var images=ArtIds.Select(id=>Hash(ArtFolder+id+".png")).ToArray();
        var report=new Report{Before=catalog.Cards.Count};Run();
        var files=new[]{CardPath,CardPath+".meta",CatalogPath,CatalogPath+".meta"}.Concat(ArtIds.Select(id=>ArtFolder+id+".png.meta")).ToArray();
        var hashes=files.Select(Hash).ToArray();Run();
        report.After=catalog.Cards.Count;report.Idempotent=files.Select(Hash).SequenceEqual(hashes);
        report.PriorPrefixPreserved=catalog.Cards.Take(prefix.Length).Select(Identity).SequenceEqual(prefix);
        report.SourcePngsPreserved=ArtIds.Select(id=>Hash(ArtFolder+id+".png")).SequenceEqual(images);
        report.CardGuid=AssetDatabase.AssetPathToGUID(CardPath);report.HiddenGuid=AssetDatabase.AssetPathToGUID(ArtFolder+"TrapHidden.png");report.ReturnGuid=AssetDatabase.AssetPathToGUID(ArtFolder+"TrapReturn.png");
        report.HiddenFileId=LocalId(Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden"));report.ReturnFileId=LocalId(Resources.Load<Sprite>("Project333/CardArtwork/TrapReturn"));
        File.WriteAllText(Path.GetFullPath("../Trap_20261001/native-import.json"),JsonUtility.ToJson(report,true));
        if(!report.Idempotent||!report.PriorPrefixPreserved||!report.SourcePngsPreserved||report.After!=report.Before+1)throw new InvalidOperationException("Native registration proof failed.");
        Debug.Log("PASS native TrapReturn registration, original full artwork and prior ordered catalog preserved.");
    }
}
