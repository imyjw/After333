// One-time native import. Root owns the isolated Editor; this script never starts an Editor.
// Sprite slicing uses capability-checked providers. No hand-written .meta or serialized YAML.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ImportGaebangChiefPresentationAssets
{
    public const string CardId="GaebangChief";
    public const string UnitFolder="Assets/Unit/GaebangChief/";
    public const string ResourceFolder="Assets/Project333/Resources/Project333/Units/GaebangChief/";
    public const string CardPath="Assets/Project333/ScriptableObjects/StarterTen/Cards/GaebangChief.asset";
    public const string CatalogPath="Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    public const string PortraitPath="Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png";
    public const string ControllerPath=UnitFolder+"GaebangChief.controller";
    static readonly string[] Motions={"Idle","Attack","Run","Death"};
    static readonly float[] Rates={8,12,12,8};
    static readonly bool[] Loops={true,false,true,false};

    public static string Run()
    {
        var database=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation=CardDatabaseValidator.Validate(database);
        if(!validation.IsValid)throw new InvalidOperationException(string.Join("\n",validation.Errors));
        var record=database.Cards.Single(r=>r.Id==CardId);RequireConfirmedRecord(record);
        var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if(catalog==null||catalog.Cards.Any(c=>c==null))throw new InvalidOperationException("Existing catalog missing.");
        var before=catalog.Cards.ToArray();
        var beforeGuids=before.Select(c=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c))).ToArray();
        string catalogGuid=AssetDatabase.AssetPathToGUID(CatalogPath);
        if(catalogGuid!="d932562c5c4766d43adce3f999c666c8")throw new InvalidOperationException("Unexpected catalog identity.");
        var card=AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
        if(card==null&&File.Exists(CardPath)||card!=null&&card.CardId!=CardId)throw new InvalidOperationException("Card path belongs to a different asset.");
        var matching=before.Where(c=>c.CardId==CardId).ToArray();
        if(matching.Length>1||matching.Length==1&&matching[0]!=card)throw new InvalidOperationException("Conflicting registration.");

        var paths=Motions.Select(m=>UnitFolder+m+".png").Concat(new[]{ResourceFolder+"CoinImpactAtlas.png"}).ToArray();
        var bytes=paths.Concat(new[]{PortraitPath,ResourceFolder+"Audio/CoinPunch.wav"}).ToDictionary(p=>p,File.ReadAllBytes);
        if(!(AssetImporter.GetAtPath(PortraitPath) is TextureImporter)||!(AssetImporter.GetAtPath(ResourceFolder+"Audio/CoinPunch.wav") is AudioImporter))
            throw new InvalidOperationException("Stage native portrait/audio importers before animation mutation.");
        var retainedGuids=new Dictionary<string,string>();
        var retainedIds=new Dictionary<string,Dictionary<string,long>>();
        foreach(string path in paths)
        {
            ValidatePixels(path,path.EndsWith("CoinImpactAtlas.png",StringComparison.Ordinal));
            var textureImporter=AssetImporter.GetAtPath(path) as TextureImporter;
            if(textureImporter==null)throw new InvalidOperationException("Stage/import reviewed textures first "+path);
            RequireProvider(path);
            retainedIds[path]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name,LocalId);
            RecordGuid(path,retainedGuids);
        }
        foreach(string path in Motions.Select(m=>UnitFolder+m+".anim").Concat(new[]{UnitFolder+"BeAttacked.anim",UnitFolder+"Drained.anim",ControllerPath,CardPath,PortraitPath,ResourceFolder+"Audio/CoinPunch.wav"}))
            RecordGuid(path,retainedGuids);

        var sprites=new Dictionary<string,Sprite[]>();
        for(int m=0;m<4;m++)
        {
            sprites[Motions[m]]=Slice(UnitFolder+Motions[m]+".png",Motions[m],8,1,false);
            WriteClip(UnitFolder+Motions[m]+".anim",sprites[Motions[m]],Rates[m],Loops[m]);
        }
        Slice(ResourceFolder+"CoinImpactAtlas.png","CoinImpactAtlas",4,2,true);
        // Only the four requested motions get generated artwork. Hurt/drained use fixed Idle frame zero.
        WriteClip(UnitFolder+"BeAttacked.anim",new[]{sprites["Idle"][0]},12,false);
        WriteClip(UnitFolder+"Drained.anim",new[]{sprites["Idle"][0]},1,true);
        var controller=WriteController();
        ImportPortrait();
        ImportAudio();
        if(card==null){card=ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();AssetDatabase.CreateAsset(card,CardPath);}
        card.ConfigureBaseForTests(record.Id,record.DisplayName,new ResourceSetData(record.Cost.Mana,record.Cost.Qi,record.Cost.Power,record.Cost.Gold));
        card.ConfigureMetadataForTests(record.Rarity,record.Affiliation,record.ChargeTileFootprint,record.EffectText,record.SpecialEffectText);
        card.ConfigureForTests(attackType:record.AttackType,attack:record.Attack,health:record.Health,
            canMove:record.CanMove,isScience:false,sciencePowerUpkeep:0,turnStartResourceGain:new ResourceSetData(),
            maxAttacksPerTurn:1,canAttackOnSummon:false,hitsPerAttack:1,hasBerserker:false,hasEndure:false,
            hasShielder:false,hasLifeSteal:false,damageType:record.DamageType,physicalDefense:0,magicDefense:0,
            races:record.Races,sealboundOwnerTurnStarts:0,hasHiding:false,hasFlying:false,spellPower:0,
            invincibleDuration:InvincibleDurationType.None,invincibleOwnerTurns:0,hasPiercing:false);
        card.ConfigureReplicateForTests(false);
        card.ConfigureBoardVisualsForTests(sprites["Idle"][0],controller);
        EditorUtility.SetDirty(card);AssetDatabase.SaveAssetIfDirty(card);
        if(matching.Length==0){catalog.ConfigureForTests(before.Concat(new[]{card}).ToArray());EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
        AssetDatabase.SaveAssets();
        foreach(var pair in bytes)if(!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)))throw new InvalidOperationException("Source PNG/audio bytes changed "+pair.Key);
        foreach(var pair in retainedGuids)if(AssetDatabase.AssetPathToGUID(pair.Key)!=pair.Value)throw new InvalidOperationException("Retained GUID changed "+pair.Key);
        foreach(var pair in retainedIds)foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(pair.Key).OfType<Sprite>())
            if(pair.Value.TryGetValue(sprite.name,out long id)&&LocalId(sprite)!=id)throw new InvalidOperationException("Retained named sprite fileID changed.");
        if(!catalog.Cards.Take(before.Length).SequenceEqual(before)||
            !catalog.Cards.Take(before.Length).Select(c=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c))).SequenceEqual(beforeGuids)||
            catalog.Cards.Count!=before.Length+(matching.Length==0?1:0)||catalog.Cards.Count(c=>c.CardId==CardId)!=1||
            AssetDatabase.AssetPathToGUID(CatalogPath)!=catalogGuid)throw new InvalidOperationException("Catalog ordered prefix/GUID changed or card duplicated.");
        return Validate()+"; catalog "+before.Length+" -> "+catalog.Cards.Count+"; card "+AssetDatabase.AssetPathToGUID(CardPath)+"; retained GUIDs/named fileIDs and source bytes preserved";
    }

    static void RecordGuid(string path,Dictionary<string,string> ids){string id=AssetDatabase.AssetPathToGUID(path);if(!string.IsNullOrEmpty(id))ids.Add(path,id);}
    static ISpriteEditorDataProvider RequireProvider(string path)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing texture importer "+path);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("No sprite provider "+path);
        provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Missing edit capability "+path);
        foreach(var edit in new[]{EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.CreateAndDeleteSprite})
            if(!capability.GetEditCapability().HasCapability(edit))throw new InvalidOperationException("Unsupported native sprite capability "+edit+" "+path);
        if(provider.GetDataProvider<ISpriteNameFileIdDataProvider>()==null)throw new InvalidOperationException("Missing named fileID provider.");
        return provider;
    }
    static Sprite[] Slice(string path,string prefix,int columns,int rows,bool readable)
    {
        var provider=RequireProvider(path);var old=provider.GetSpriteRects();
        var rects=Enumerable.Range(0,8).Select(i=>{
            string name=prefix+"_"+i;var prior=old.FirstOrDefault(r=>r.name==name);
            return new SpriteRect{name=name,rect=new Rect(i%columns*512,(rows-1-i/columns)*512,512,512),
                pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=Vector4.zero,
                spriteID=prior==null?GUID.Generate():prior.spriteID};
        }).ToArray();
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
        importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=readable;
        importer.ClearPlatformTextureSettings("Standalone");importer.ClearPlatformTextureSettings("Android");
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        importer.SaveAndReimport();provider=RequireProvider(path);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
        if(sprites.Length!=8||sprites.Any(s=>s.rect.size!=new Vector2(512,512)||s.pivot!=new Vector2(256,256)))
            throw new InvalidOperationException("Native slicing mismatch "+path);
        return sprites;
    }
    static void WriteClip(string path,Sprite[] sprites,float fps,bool loop)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){if(File.Exists(path))throw new InvalidOperationException("Clip path occupied "+path);clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
        clip.frameRate=fps;
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),
            sprites.Select((s,i)=>new ObjectReferenceKeyframe{time=i/fps,value=s}).ToArray());
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.startTime=0;settings.stopTime=sprites.Length/fps;
        AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
    }
    static AnimatorController WriteController()
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller==null){if(File.Exists(ControllerPath))throw new InvalidOperationException("Controller path occupied.");controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);}
        if(controller.layers.Length!=1)throw new InvalidOperationException("Expected one native animator layer.");
        var machine=controller.layers[0].stateMachine;
        var names=Motions.Concat(new[]{"BeAttacked","Drained"}).ToArray();
        if(machine.states.Any(s=>!names.Contains(s.state.name))||machine.anyStateTransitions.Length!=0||machine.entryTransitions.Length!=0)
            throw new InvalidOperationException("Unexpected artist-edited controller; do not replace.");
        foreach(string name in names)
        {
            var matches=machine.states.Where(s=>s.state.name==name).ToArray();
            if(matches.Length>1)throw new InvalidOperationException("Duplicate state "+name);
            var state=matches.Length==0?machine.AddState(name):matches[0].state;
            if(state.transitions.Length!=0)throw new InvalidOperationException("Automatic transitions would override explicit playback.");
            state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+name+".anim");state.speed=1;
            if(name=="Idle")machine.defaultState=state;
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);return controller;
    }
    static void ImportPortrait()
    {
        var importer=AssetImporter.GetAtPath(PortraitPath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Stage full original portrait.");
        importer.GetSourceTextureWidthAndHeight(out int width,out int height);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=Mathf.Max(2048,Mathf.NextPowerOfTwo(Mathf.Max(width,height)));
        importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var portrait=AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        if(portrait==null||portrait.rect!=new Rect(0,0,width,height))throw new InvalidOperationException("Whole portrait pixels must be preserved.");
    }
    static void ImportAudio()
    {
        var importer=AssetImporter.GetAtPath(ResourceFolder+"Audio/CoinPunch.wav") as AudioImporter;
        if(importer==null)throw new InvalidOperationException("Stage quiet CoinPunch.");
        importer.forceToMono=false;importer.loadInBackground=false;
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
        settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(ResourceFolder+"Audio/CoinPunch.wav");
        if(clip==null||clip.channels!=1||clip.frequency!=44100||Math.Abs(clip.length-.44f)>.002f)throw new InvalidOperationException("CoinPunch must remain quiet .44s mono44100 PCM.");
    }
    public static void ValidatePixels(string path,bool coin)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path),false)||texture.width!=(coin?2048:4096)||texture.height!=(coin?1024:512))
                throw new InvalidOperationException("Expected full512 cells "+path);
            var data=texture.GetPixels32();int columns=coin?4:8,rows=coin?2:1;
            for(int f=0;f<8;f++)
            {
                int xmin=512,ymin=512,xmax=-1,ymax=-1,x0=f%columns*512,y0=(rows-1-f/columns)*512;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(data[(y0+y)*texture.width+x0+x].a>=16)
                {xmin=Math.Min(xmin,x);ymin=Math.Min(ymin,y);xmax=Math.Max(xmax,x);ymax=Math.Max(ymax,y);}
                if(xmax<0||xmin<(coin?16:8)||xmax>(coin?495:503)||ymax>(coin?495:503)||ymin<16||!coin&&ymin!=16)
                    throw new InvalidOperationException("Complete art/baseline check failed "+path+" "+f);
            }
        }finally{Object.DestroyImmediate(texture);}
    }
    public static string Validate()
    {
        for(int m=0;m<4;m++)
        {
            string name=Motions[m],path=UnitFolder+name+".png";
            ValidatePixels(path,false);
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+name+".anim");
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
            if(sprites.Length!=8||clip.frameRate!=Rates[m]||Math.Abs(clip.length-8/Rates[m])>.001f||
                AnimationUtility.GetAnimationClipSettings(clip).loopTime!=Loops[m]||keys==null||keys.Length!=8||
                Enumerable.Range(0,8).Any(i=>keys[i].value!=sprites[i]||Math.Abs(keys[i].time-i/Rates[m])>.0001f))
                throw new InvalidOperationException("Animation contract mismatch "+name);
        }
        ValidatePixels(ResourceFolder+"CoinImpactAtlas.png",true);
        var coin=AssetDatabase.LoadAllAssetsAtPath(ResourceFolder+"CoinImpactAtlas.png").OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
        if(coin.Length!=8||Enumerable.Range(0,8).Any(i=>coin[i].name!="CoinImpactAtlas_"+i||
            coin[i].rect!=new Rect(i%4*512,(1-i/4)*512,512,512)||coin[i].pivot!=new Vector2(256,256)))
            throw new InvalidOperationException("Coin native sprite order/rect/pivot mismatch.");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var states=controller.layers[0].stateMachine.states;
        if(states.Length!=6||controller.layers[0].stateMachine.defaultState.name!="Idle"||
            states.Any(s=>s.state.speed!=1||s.state.transitions.Length!=0||s.state.motion!=AssetDatabase.LoadAssetAtPath<AnimationClip>(UnitFolder+s.state.name+".anim")))
            throw new InvalidOperationException("Native six-state controller mismatch.");
        var card=AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
        RequireConfirmedDefinition((UnitCardDefinition)card.ToDefinition());
        if(card.BoardAnimatorController!=controller||card.BoardSprite.name!="Idle_0")throw new InvalidOperationException("Native board visuals mismatch.");
        return "GaebangChief: 4x8 full512 centered sprites, Idle8/Attack12/Run12/Death8 fps; attack frame3 at .25s; all32 bottom16; fixedIdle hurt/drained; eight CoinImpactAtlas sprites16fps contract; quiet .44s PCM sound; full original portrait; printed Lv0 10/60 and Lv13 14/69";
    }
    static void RequireConfirmedRecord(JsonCardDefinitionRecord r)
    {
        if(r.DefinitionType!=JsonCardDefinitionKind.Unit||r.DisplayName!="개방 방주"||r.Rarity!=CardRarity.Unique||
            r.Affiliation!=CardAffiliation.Murim||r.ChargeTileFootprint!=ChargeTileFootprint.OneByOne||!r.IncludeInDraft||!r.IncludeInRewards||
            r.EffectText!=GaebangChiefRules.EffectDescription||!string.IsNullOrEmpty(r.SpecialEffectText))throw new InvalidOperationException("Confirmed GaebangChief metadata differs.");
        RequireConfirmedDefinition((UnitCardDefinition)r.ToDefinition());
    }
    static void RequireConfirmedDefinition(UnitCardDefinition u)
    {
        if(u.Cost.Qi!=7||u.Cost.Mana+u.Cost.Power+u.Cost.Gold!=0||u.Attack!=10||u.Health!=60||
            u.AttackType!=AttackType.Melee||u.DamageType!=DamageType.Physical||u.PhysicalDefense+u.MagicDefense!=0||!u.CanMove||
            !u.Races.SequenceEqual(new[]{UnitRace.Human})||u.IsScience||u.HasBerserker||u.HasEndure||u.HasShielder||u.HasLifeSteal||
            u.HasRush||u.HasReplicate||u.HasHiding||u.HasFlying||u.HasPiercing||u.SealboundOwnerTurnStarts+u.SpellPower+u.InvincibleOwnerTurns+u.SciencePowerUpkeep!=0||
            u.InvincibleDuration!=InvincibleDurationType.None||u.MaxAttacksPerTurn!=1||u.HitsPerAttack!=1||
            !CardUpgradeRules.IsCardUpgradeable(u)||CardLevelStatRules.ApplyAttackBonus(CardId,u.Attack,13)!=14||
            CardLevelStatRules.ApplyHpBonus(CardId,u.Health,13)!=69)throw new InvalidOperationException("Confirmed printed stats/no innate traits/Lv13 differ.");
    }
    static long LocalId(Object asset)
    {
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string _,out long id))throw new InvalidOperationException("Missing native localID.");
        return id;
    }
}
