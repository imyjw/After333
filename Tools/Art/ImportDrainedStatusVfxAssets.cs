// Root invokes this in the isolated Editor. Sprite metadata is always written through native providers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportDrainedStatusVfxAssets
{
    public const string Sheet="Assets/Project333/Resources/Project333/StatusEffects/Drained/ElectricArcs.png";
    public const string ResourceKey="Project333/StatusEffects/Drained/ElectricArcs";
    public static string Run()
    {
        ValidateSource();
        var bytes=File.ReadAllBytes(Sheet);var guid=AssetDatabase.AssetPathToGUID(Sheet);
        var provider=CheckedProvider();var previous=provider.GetSpriteRects();
        var retained=Sprites().ToDictionary(s=>s.name,LocalId);
        var rects=Enumerable.Range(0,8).Select(i=>{
            var name="ElectricArcs_"+i;var prior=previous.FirstOrDefault(s=>s.name==name);
            return new SpriteRect{name=name,rect=new Rect(i%4*512,(1-i/4)*512,512,512),
                pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=Vector4.zero,
                spriteID=prior==null?GUID.Generate():prior.spriteID};
        }).ToArray();
        var importer=(TextureImporter)AssetImporter.GetAtPath(Sheet);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.isReadable=true;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);importer.SaveAndReimport();
        // Switching a newly staged Default texture requires a fresh native provider.
        provider=CheckedProvider();provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)).ToList());
        provider.Apply();importer.SaveAndReimport();
        if(!bytes.SequenceEqual(File.ReadAllBytes(Sheet)))throw new InvalidOperationException("Import changed source PNG bytes.");
        if(!string.IsNullOrEmpty(guid)&&AssetDatabase.AssetPathToGUID(Sheet)!=guid)throw new InvalidOperationException("Native atlas GUID changed.");
        foreach(var sprite in Sprites())if(retained.TryGetValue(sprite.name,out long fileId)&&LocalId(sprite)!=fileId)
            throw new InvalidOperationException("Existing named sprite local ID changed: "+sprite.name);
        return Validate()+"; GUID "+AssetDatabase.AssetPathToGUID(Sheet)+"; PNG SHA256 "+Hash(bytes)+"; existing named fileIDs preserved";
    }
    static ISpriteEditorDataProvider CheckedProvider()
    {
        var importer=AssetImporter.GetAtPath(Sheet) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Stage reviewed atlas before native import.");
        var factory=new SpriteDataProviderFactories();factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Native sprite provider unavailable.");
        provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Native sprite edit capability unavailable; abort.");
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported native sprite capability "+edit+"; abort.");
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Named fileID provider unavailable; abort.");
        return provider;
    }
    static Sprite[] Sprites()=>AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
    static long LocalId(Sprite sprite)
    {
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long id)||string.IsNullOrEmpty(guid)||id==0)
            throw new InvalidOperationException("Missing native sprite identity: "+sprite.name);
        return id;
    }
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    static void ValidateSource()
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(Sheet),false)||texture.width!=2048||texture.height!=1024)
                throw new InvalidOperationException("Require full 4x2 512px Drained atlas.");
            var pixels=texture.GetPixels32();
            for(int f=0;f<8;f++)
            {
                int visible=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    if(pixels[((1-f/4)*512+y)*2048+f%4*512+x].a==0)continue;
                    if(x<16||y<16||x>495||y>495)throw new InvalidOperationException("Drained frame crosses16px margin "+f);
                    if(x>=208&&x<=303&&y>=208&&y<=303)throw new InvalidOperationException("Drained frame fills central96px empty space "+f);
                    visible++;
                }
                if(visible==0)throw new InvalidOperationException("Empty Drained frame "+f);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    public static string Validate()
    {
        ValidateSource();var importer=AssetImporter.GetAtPath(Sheet) as TextureImporter;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);var sprites=Sprites();
        if(texture==null||texture.width!=2048||texture.height!=1024||sprites.Length!=8||importer==null||
            importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Multiple||
            importer.spritePixelsPerUnit!=100||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||!importer.isReadable||
            !importer.alphaIsTransparency||importer.textureCompression!=TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException("Native Drained sprite import settings/count mismatch.");
        for(int i=0;i<8;i++)
        {
            var s=sprites[i];
            if(s.name!="ElectricArcs_"+i||s.rect!=new Rect(i%4*512,(1-i/4)*512,512,512)||
                s.pivot!=new Vector2(256,256)||s.pixelsPerUnit!=100||s.vertices.Length!=4)
                throw new InvalidOperationException("Native Drained frame/order/fullrect/pivot mismatch "+i);
            LocalId(s);
        }
        if(!Resources.LoadAll<Sprite>(ResourceKey).OrderBy(s=>s.name,StringComparer.Ordinal).SequenceEqual(sprites))
            throw new InvalidOperationException("Resource sprite lookup differs from native atlas.");
        return "Drained ElectricArcs: 8 full512px native sprites, atlas2048x1024, top-left row order, center pivot256,256; PPU100/Point/noMip/uncompressed/readable; every margin>=16px and central96px empty; no controller/card/schema mutation";
    }
}
