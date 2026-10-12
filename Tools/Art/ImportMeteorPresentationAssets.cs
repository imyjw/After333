// Reviewed standalone importer; root invokes Run in its isolated native Editor.
// No MenuItem or postprocessor; metadata is exclusively written by Unity data providers.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
public static class ImportMeteorPresentationAssets
{
    private const string Base="Assets/Project333/Resources/Project333/SpellEffects/Meteor/";
    public const string Flight=Base+"Flight.png", Impact=Base+"Impact.png";
    public const string IncomingAudio=Base+"Audio/MeteorIncoming.wav", ImpactAudio=Base+"Audio/MeteorImpact.wav";
    public const int ImpactFramesPerSecond=16;
    private static ISpriteEditorDataProvider CheckedProvider(SpriteDataProviderFactories factories,TextureImporter importer)
    {
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Missing Meteor sprite provider.");
        provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Missing sprite edit capability; aborting.");
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditPivot,EEditCapability.EditBorder,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported sprite operation "+edit+"; aborting.");
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Missing sprite name/fileID provider; aborting.");
        return provider;
    }
    public static string Run()
    {
        ValidateSource(Flight,1);ValidateSource(Impact,4);
        var factories=new SpriteDataProviderFactories();factories.Init();
        // Check both sprite capabilities and both audio importers before the first mutation.
        foreach(var path in new[]{Flight,Impact})
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing staged Meteor texture "+path);
            CheckedProvider(factories,importer);
        }
        foreach(var path in new[]{IncomingAudio,ImpactAudio})
            if(!(AssetImporter.GetAtPath(path) is AudioImporter))throw new InvalidOperationException("Missing staged Meteor audio "+path);
        ImportSheet(factories,Flight,"Flight",1);ImportSheet(factories,Impact,"Impact",4);
        ImportAudio(IncomingAudio);ImportAudio(ImpactAudio);AssetDatabase.SaveAssets();return Validate();
    }
    private static void ImportSheet(SpriteDataProviderFactories factories,string path,string stem,int grid)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var provider=CheckedProvider(factories,importer);var prior=provider.GetSpriteRects();
        var rects=Enumerable.Range(0,grid*grid).Select(i=>{
            string name=grid==1?stem:stem+"_"+i.ToString("00");
            var old=prior.FirstOrDefault(s=>s.name==name);
            return new SpriteRect{name=name,rect=new Rect(i%grid*512,(grid-1-i/grid)*512,512,512),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Custom,border=Vector4.zero,spriteID=old==null?GUID.Generate():old.spriteID};
        }).ToArray();
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.isReadable=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        provider=CheckedProvider(factories,importer);provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)).ToList());
        provider.Apply();importer.SaveAndReimport();
    }
    private static void ImportAudio(string path)
    {
        var importer=(AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono=false;importer.loadInBackground=false;
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.SaveAndReimport();
    }
    private static void ValidateSource(string path,int grid)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path),false)||texture.width!=grid*512||texture.height!=grid*512)throw new InvalidOperationException("Meteor full512 grid mismatch "+path);
            var pixels=texture.GetPixels32();
            for(int f=0;f<grid*grid;f++)
            {
                int any=0,clear=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    int alpha=pixels[((grid-1-f/grid)*512+y)*texture.width+f%grid*512+x].a;
                    if(alpha==0){clear++;continue;}any++;
                    if(x<16||x>495||y<16||y>495)throw new InvalidOperationException("Meteor alpha overflows cell "+f+" in "+path);
                }
                if(clear==0||any==0&&(grid==1||f<grid*grid-1))throw new InvalidOperationException("Empty/opaque Meteor frame "+f);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    private static void ValidateSheet(string path,string stem,int grid)
    {
        ValidateSource(path,grid);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        if(texture==null||texture.width!=grid*512||texture.height!=grid*512||sprites.Length!=grid*grid)throw new InvalidOperationException("Meteor dimensions/count mismatch "+path);
        if(importer==null||importer.spriteImportMode!=SpriteImportMode.Multiple||importer.spritePixelsPerUnit!=100||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||!importer.alphaIsTransparency||importer.isReadable||importer.textureCompression!=TextureImporterCompression.Uncompressed)throw new InvalidOperationException("Meteor importer mismatch "+path);
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        if(settings.spriteMeshType!=SpriteMeshType.FullRect)throw new InvalidOperationException("Meteor must retain the full512 cell "+path);
        for(int i=0;i<sprites.Length;i++)
        {
            string name=grid==1?stem:stem+"_"+i.ToString("00");
            if(sprites[i].name!=name||sprites[i].rect!=new Rect(i%grid*512,(grid-1-i/grid)*512,512,512)||sprites[i].pivot!=new Vector2(256,256)||sprites[i].pixelsPerUnit!=100)throw new InvalidOperationException("Meteor frame/order/pivot mismatch "+path+" frame "+i);
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprites[i],out string guid,out long fileId)||string.IsNullOrEmpty(guid)||fileId==0)throw new InvalidOperationException("Missing native Meteor sprite identity "+i);
        }
    }
    private static void ValidateAudio(string path,float duration)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(clip==null||clip.channels!=1||clip.frequency!=44100||Math.Abs(clip.length-duration)>.002f||importer==null||importer.forceToMono||importer.loadInBackground||importer.defaultSampleSettings.loadType!=AudioClipLoadType.DecompressOnLoad||importer.defaultSampleSettings.compressionFormat!=AudioCompressionFormat.PCM||importer.defaultSampleSettings.sampleRateSetting!=AudioSampleRateSetting.PreserveSampleRate||!importer.defaultSampleSettings.preloadAudioData)throw new InvalidOperationException("Meteor audio must retain quiet mono44100 PCM without normalization "+path);
    }
    public static string Validate()
    {
        ValidateSheet(Flight,"Flight",1);ValidateSheet(Impact,"Impact",4);ValidateAudio(IncomingAudio,.65f);ValidateAudio(ImpactAudio,1f);
        return "Meteor: single full512 Flight; 4x4 chronological full512 Impact_00..15 at16fps; centerpivot256,256; alpha-safe16px margins; PPU100/Point/noMip/uncompressed/fullrect; native stable name-fileIDs; quiet PCMmono44100 Incoming.65s/Impact1.0s preloaded.";
    }
}