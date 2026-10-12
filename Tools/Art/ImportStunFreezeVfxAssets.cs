// Root invokes this in the exclusive isolated Editor; never edit sprite .meta by hand.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportStunFreezeVfxAssets
{
    sealed class SheetSpec
    {
        public readonly string Name, AssetPath, ResourceKey;
        public readonly int Columns, Rows;
        public int Count=>Columns*Rows;
        public SheetSpec(string status,string name,int columns,int rows)
        {
            Name=name;Columns=columns;Rows=rows;
            ResourceKey="Project333/StatusEffects/"+status+"/"+name;
            AssetPath="Assets/Project333/Resources/"+ResourceKey+".png";
        }
    }
    static readonly SheetSpec[] Sheets={new SheetSpec("Stun","OrbitStars",4,2),new SheetSpec("Freeze","IceCrystal",1,1)};
    public static string Run()
    {
        // Abort before mutating either importer if any native capability or source constraint fails.
        foreach(var sheet in Sheets){ValidateSource(sheet);CheckedProvider(sheet);}
        var results=new List<string>();
        foreach(var sheet in Sheets)results.Add(Import(sheet));
        results.Add(Project333.Editor.StatusSpriteBoundsEditor.Rebuild());
        return string.Join("\n",results);
    }
    static string Import(SheetSpec sheet)
    {
        var bytes=File.ReadAllBytes(sheet.AssetPath);var guid=AssetDatabase.AssetPathToGUID(sheet.AssetPath);
        var provider=CheckedProvider(sheet);var previous=provider.GetSpriteRects();
        var retained=Sprites(sheet).ToDictionary(s=>s.name,LocalId);
        var expected=Enumerable.Range(0,sheet.Count).Select(i=>sheet.Name+"_"+i).ToArray();
        if(retained.Keys.Any(name=>!expected.Contains(name)))throw new InvalidOperationException("Unexpected existing sprite names; preserve artist metadata: "+sheet.AssetPath);
        var rects=Enumerable.Range(0,sheet.Count).Select(i=>{
            var name=expected[i];var prior=previous.FirstOrDefault(s=>s.name==name);
            return new SpriteRect {name=name,rect=Rect(sheet,i),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,
                border=Vector4.zero,spriteID=prior==null?GUID.Generate():prior.spriteID};
        }).ToArray();
        var importer=(TextureImporter)AssetImporter.GetAtPath(sheet.AssetPath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.isReadable=true;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);importer.SaveAndReimport();
        provider=CheckedProvider(sheet);provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)).ToList());
        provider.Apply();importer.SaveAndReimport();
        if(!bytes.SequenceEqual(File.ReadAllBytes(sheet.AssetPath)))throw new InvalidOperationException("Native import changed source pixels: "+sheet.AssetPath);
        if(!string.IsNullOrEmpty(guid)&&AssetDatabase.AssetPathToGUID(sheet.AssetPath)!=guid)throw new InvalidOperationException("Existing atlas GUID changed: "+sheet.AssetPath);
        foreach(var sprite in Sprites(sheet))if(retained.TryGetValue(sprite.name,out long id)&&id!=LocalId(sprite))
            throw new InvalidOperationException("Existing named sprite fileID changed: "+sprite.name);
        return Validate(sheet)+"; GUID "+AssetDatabase.AssetPathToGUID(sheet.AssetPath)+"; PNG SHA256 "+Hash(bytes)+"; existing named fileIDs preserved";
    }
    static ISpriteEditorDataProvider CheckedProvider(SheetSpec sheet)
    {
        var importer=AssetImporter.GetAtPath(sheet.AssetPath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Stage reviewed new VFX texture first: "+sheet.AssetPath);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Native sprite provider unavailable; abort.");
        provider.InitSpriteEditorDataProvider();var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Native sprite edit capability unavailable; abort.");
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported native edit capability "+edit+"; abort.");
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Named sprite ID provider unavailable; abort.");
        return provider;
    }
    static Rect Rect(SheetSpec sheet,int frame)=>new Rect(frame%sheet.Columns*512,(sheet.Rows-1-frame/sheet.Columns)*512,512,512);
    static Sprite[] Sprites(SheetSpec sheet)=>AssetDatabase.LoadAllAssetsAtPath(sheet.AssetPath).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
    static long LocalId(Sprite sprite)
    {
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long id)||string.IsNullOrEmpty(guid)||id==0)
            throw new InvalidOperationException("Missing native sprite ID: "+sprite.name);
        return id;
    }
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    static void ValidateSource(SheetSpec sheet)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(sheet.AssetPath),false)||texture.width!=sheet.Columns*512||texture.height!=sheet.Rows*512)
                throw new InvalidOperationException("Require full512px native grid: "+sheet.AssetPath);
            var pixels=texture.GetPixels32();
            for(int frame=0;frame<sheet.Count;frame++)
            {
                int visible=0;long centerAlpha=0;int inner64Visible=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    var alpha=pixels[((sheet.Rows-1-frame/sheet.Columns)*512+y)*texture.width+frame%sheet.Columns*512+x].a;
                    if(alpha==0)continue;
                    visible++;
                    if(x<16||y<16||x>495||y>495)throw new InvalidOperationException(sheet.Name+" frame crosses16px margin "+frame);
                    if(x>=208&&x<=303&&y>=208&&y<=303)centerAlpha+=alpha;
                    if(x>=224&&x<=287&&y>=224&&y<=287)inner64Visible++;
                }
                if(visible==0)throw new InvalidOperationException(sheet.Name+" empty frame "+frame);
                if(sheet.Name=="OrbitStars"&&inner64Visible!=0)throw new InvalidOperationException("Stun orbit must keep central64px transparent.");

            }
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    static string Validate(SheetSpec sheet)
    {
        ValidateSource(sheet);var importer=AssetImporter.GetAtPath(sheet.AssetPath) as TextureImporter;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.AssetPath);var sprites=Sprites(sheet);
        if(texture==null||texture.width!=sheet.Columns*512||texture.height!=sheet.Rows*512||sprites.Length!=sheet.Count||importer==null||
            importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Multiple||importer.spritePixelsPerUnit!=100||
            importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||!importer.isReadable||!importer.alphaIsTransparency||
            importer.alphaSource!=TextureImporterAlphaSource.FromInput||importer.textureCompression!=TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(sheet.Name+" native sprite import settings/count mismatch.");
        for(int i=0;i<sheet.Count;i++)
        {
            var sprite=sprites[i];
            if(sprite.name!=sheet.Name+"_"+i||sprite.rect!=Rect(sheet,i)||sprite.pivot!=new Vector2(256,256)||sprite.pixelsPerUnit!=100||sprite.vertices.Length!=4)
                throw new InvalidOperationException(sheet.Name+" native rect/order/center/fullrect mismatch "+i);
            LocalId(sprite);
        }
        if(!Resources.LoadAll<Sprite>(sheet.ResourceKey).OrderBy(s=>s.name,StringComparer.Ordinal).SequenceEqual(sprites))
            throw new InvalidOperationException(sheet.Name+" Resources lookup differs from native sprites.");
        return sheet.Name+": "+sheet.Count+" full512 native sprites, atlas"+texture.width+"x"+texture.height+", top-left row order, center pivot256,256; PPU100/Point/fullrect/readable/uncompressed/noMip/inputAlpha; margins>=16px; no unit/controller/card mutation";
    }
    [Serializable] public sealed class NativeSheetReport
    {
        public string AssetPath,Guid,PngSHA256,MetaSHA256;
        public string[] SpriteNames;
        public long[] SpriteFileIds;
    }
    [Serializable] public sealed class NativeReport
    {
        public string First,Second,BodyBoundsGuid,BodyBoundsSHA256,BodyBoundsMetaSHA256;
        public bool MetadataIdempotent,SourcePngsPreserved,UnitFilesPreserved;
        public int BodyBoundsEntries,ProtectedUnitFiles,BodyBoundsImageDependencies;
        public NativeSheetReport[] Sheets;
    }
    static NativeSheetReport Capture(SheetSpec sheet)=>new NativeSheetReport {
        AssetPath=sheet.AssetPath,Guid=AssetDatabase.AssetPathToGUID(sheet.AssetPath),
        PngSHA256=Hash(File.ReadAllBytes(sheet.AssetPath)),MetaSHA256=Hash(File.ReadAllBytes(sheet.AssetPath+".meta")),
        SpriteNames=Sprites(sheet).Select(s=>s.name).ToArray(),SpriteFileIds=Sprites(sheet).Select(LocalId).ToArray()
    };
    // Called by root's exclusive isolated -batchmode -quit -executeMethod invocation.
    public static void Batch()
    {
        var protectedUnit=Directory.GetFiles("Assets/Unit","*",SearchOption.AllDirectories).ToDictionary(path=>path,path=>Hash(File.ReadAllBytes(path)));
        var sourceHashes=Sheets.ToDictionary(sheet=>sheet.AssetPath,sheet=>Hash(File.ReadAllBytes(sheet.AssetPath)));
        var report=new NativeReport {First=Run(),ProtectedUnitFiles=protectedUnit.Count};
        report.Sheets=Sheets.Select(Capture).ToArray();
        var boundsPath=Project333.Editor.StatusSpriteBoundsEditor.AssetPath;
        var boundsHash=Hash(File.ReadAllBytes(boundsPath));var boundsMetaHash=Hash(File.ReadAllBytes(boundsPath+".meta"));
        report.Second=Run();var after=Sheets.Select(Capture).ToArray();
        report.MetadataIdempotent=report.Sheets.Zip(after,(a,b)=>a.Guid==b.Guid&&a.MetaSHA256==b.MetaSHA256&&a.SpriteFileIds.SequenceEqual(b.SpriteFileIds)).All(equal=>equal)&&
            boundsHash==Hash(File.ReadAllBytes(boundsPath))&&boundsMetaHash==Hash(File.ReadAllBytes(boundsPath+".meta"));
        report.SourcePngsPreserved=Sheets.All(sheet=>sourceHashes[sheet.AssetPath]==Hash(File.ReadAllBytes(sheet.AssetPath)));
        report.UnitFilesPreserved=protectedUnit.All(row=>File.Exists(row.Key)&&row.Value==Hash(File.ReadAllBytes(row.Key)));
        report.BodyBoundsGuid=AssetDatabase.AssetPathToGUID(boundsPath);report.BodyBoundsSHA256=boundsHash;report.BodyBoundsMetaSHA256=boundsMetaHash;
        report.BodyBoundsEntries=AssetDatabase.LoadAssetAtPath<Project333.Runtime.Presentation.Battle.StatusSpriteBoundsAsset>(boundsPath).Entries.Count;
        report.BodyBoundsImageDependencies=AssetDatabase.GetDependencies(boundsPath,true).Count(path=>path.EndsWith(".png",StringComparison.OrdinalIgnoreCase));
        var reportPath=Environment.GetEnvironmentVariable("PROJECT333_STUN_FREEZE_NATIVE_REPORT");
        if(string.IsNullOrWhiteSpace(reportPath))reportPath=Path.GetFullPath("StunFreezeVfx-native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
        if(!report.MetadataIdempotent||!report.SourcePngsPreserved||!report.UnitFilesPreserved||report.BodyBoundsEntries<=0||report.BodyBoundsImageDependencies!=0)
            throw new InvalidOperationException("Native VFX/BodyBounds import invariants failed. Read "+reportPath);
        Debug.Log("PASS native Stun/Freeze/BodyBounds import twice: "+reportPath);
    }
    public static string Validate()=>string.Join("\n",Sheets.Select(Validate));
}
