using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;

// Standalone native import for the retained isolated validation project; never saves scenes.
public static class ImportSummonTrapRegistration
{
    private const string CardId = SummonTrapCardRules.CardId;
    private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/SummonTrap.asset";
    private const string ArtPath = "Assets/Project333/Resources/Project333/CardArtwork/SummonTrap.png";
    private const string JsonPath = "Assets/Project333/Resources/Project333/Data/cards.json";

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static string Identity(CardDefinitionAsset card)
    {
        var path = AssetDatabase.GetAssetPath(card);
        return path + "|" + AssetDatabase.AssetPathToGUID(path);
    }

    private static ISpriteEditorDataProvider Provider(TextureImporter importer)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing native sprite provider.");
        provider.InitSpriteEditorDataProvider();
        var capabilities = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capabilities == null) throw new InvalidOperationException("Missing native sprite capability.");
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditBorder, EEditCapability.EditPivot, EEditCapability.CreateAndDeleteSprite })
            if (!capabilities.GetEditCapability().HasCapability(edit))
                throw new InvalidOperationException("Unsupported sprite operation " + edit);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Missing sprite name/fileID provider.");
        return provider;
    }

    private static Sprite ImportPortrait(out int width, out int height, out bool wasAlreadyConfigured)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(ArtPath), false))
                throw new InvalidOperationException("Summon Trap PNG decode failed.");
            width = source.width; height = source.height;
            var pixels = source.GetPixels32();
            if (!pixels.Any(pixel => pixel.a < 255) || !pixels.Any(pixel => pixel.a > 200))
                throw new InvalidOperationException("Summon Trap artwork requires real alpha and visible content.");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
        var maxSize = Math.Max(32, Mathf.NextPowerOfTwo(Math.Max(width, height)));
        if (maxSize > 16384) throw new InvalidOperationException("Portrait exceeds supported native texture size.");
        var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing reviewed Summon Trap artwork.");
        var provider = Provider(importer);
        var previous = provider.GetSpriteRects();
        if (previous.Length > 1 || previous.Any(rect => rect.name != CardId))
            throw new InvalidOperationException("Unexpected existing Summon Trap sprite layout/name.");
        var prior = previous.SingleOrDefault();
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var pairs = provider.GetDataProvider<ISpriteNameFileIdDataProvider>().GetNameFileIdPairs().ToArray();
        var reviewed = prior != null && prior.rect == new Rect(0, 0, width, height) &&
            prior.border == Vector4.zero && prior.pivot == new Vector2(.5f, .5f) && prior.alignment == SpriteAlignment.Center &&
            !prior.spriteID.Empty() && pairs.Length == 1 && pairs[0].name == CardId && !pairs[0].GetFileGUID().Empty() &&
            importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
            importer.spritePixelsPerUnit == 100 && importer.maxTextureSize >= maxSize &&
            importer.npotScale == TextureImporterNPOTScale.None && !importer.mipmapEnabled &&
            importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency &&
            !importer.isReadable && importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Clamp &&
            importer.textureCompression == TextureImporterCompression.Uncompressed &&
            !importer.GetPlatformTextureSettings("Standalone").overridden &&
            !importer.GetPlatformTextureSettings("Android").overridden &&
            settings.spriteMeshType == SpriteMeshType.FullRect && settings.spriteAlignment == (int)SpriteAlignment.Center &&
            settings.spritePivot == new Vector2(.5f, .5f);
        wasAlreadyConfigured = reviewed;
        if (!reviewed)
        {
            var rect = new SpriteRect
            {
                name = CardId, rect = new Rect(0, 0, width, height), border = Vector4.zero,
                pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                spriteID = prior == null ? GUID.Generate() : prior.spriteID
            };
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = maxSize;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Standalone");
            importer.ClearPlatformTextureSettings("Android");
            // Read after setting the importer: an old settings snapshot would restore
            // Default texture/mipmaps/NPOT scaling when passed to SetTextureSettings.
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            provider = Provider(importer);
            provider.SetSpriteRects(new[] { rect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                new[] { new SpriteNameFileIdPair(rect.name, rect.spriteID) });
            provider.Apply();
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (sprite == null || sprite.rect != new Rect(0, 0, width, height) ||
            sprite.pivot != new Vector2(width / 2f, height / 2f) || sprite.vertices.Length != 4 ||
            sprite.pixelsPerUnit != 100 || sprite.texture.width != width || sprite.texture.height != height)
            throw new InvalidOperationException("Native complete Summon Trap portrait mismatch: source=" + width + "x" + height +
                ", textureType=" + importer.textureType + ", spriteMode=" + importer.spriteImportMode +
                ", npot=" + importer.npotScale + ", max=" + importer.maxTextureSize +
                (sprite == null ? ", sprite=null" : ", rect=" + sprite.rect + ", pivot=" + sprite.pivot +
                    ", vertices=" + sprite.vertices.Length + ", ppu=" + sprite.pixelsPerUnit +
                    ", texture=" + sprite.texture.width + "x" + sprite.texture.height));
        return sprite;
    }

    private static bool Matches(TrapSpellCardDefinitionAsset card, JsonCardDefinitionRecord record)
    {
        if (card == null || card.CardId != record.Id || card.DisplayName != record.DisplayName ||
            card.Rarity != record.Rarity || card.Affiliation != record.Affiliation ||
            card.ChargeTileFootprint != record.ChargeTileFootprint || card.EffectText != record.EffectText ||
            card.SpecialEffectText != record.SpecialEffectText || !card.HasTrap || card.HasReplicate ||
            card.BoardSprite != null || card.BoardAnimatorController != null) return false;
        var definition = card.ToDefinition() as TrapSpellCardDefinition;
        return definition != null && definition.TriggerType == record.TrapTrigger &&
            definition.EffectType == record.TrapEffect && CardUpgradeRules.IsCardUpgradeable(definition) && definition.Damage == record.Damage &&
            definition.Cost.Mana + definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold == 0 &&
            definition.ActivationCost.Mana == record.TrapActivationCost.Mana &&
            definition.ActivationCost.Qi == record.TrapActivationCost.Qi &&
            definition.ActivationCost.Power == record.TrapActivationCost.Power &&
            definition.ActivationCost.Gold == record.TrapActivationCost.Gold;
    }

    private static void Run()
    {
        var json = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        var validation = CardDatabaseValidator.Validate(json);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = json.Cards.Single(card => card.Id == CardId);
        if (record.DisplayName != SummonTrapCardRules.DisplayName || record.DefinitionType != JsonCardDefinitionKind.TrapSpell ||
            !record.HasTrap || record.Rarity != CardRarity.Common || record.Affiliation != CardAffiliation.Neutral ||
            record.ChargeTileFootprint != ChargeTileFootprint.None || !record.IncludeInDraft || !record.IncludeInRewards ||
            record.Cost.Mana + record.Cost.Qi + record.Cost.Power + record.Cost.Gold != 0 ||
            record.TrapActivationCost.Gold != SummonTrapCardRules.ActivationGoldCost ||
            record.TrapActivationCost.Mana + record.TrapActivationCost.Qi + record.TrapActivationCost.Power != 0 ||
            record.TrapTrigger != TrapTriggerType.EnemyUnitSummoned || record.TrapEffect != TrapEffectType.DamageSummonedUnitPhysical ||
            record.SpecialEffectText != SummonTrapCardRules.SpecialEffectText || record.Damage != SummonTrapCardRules.BaseDamage || record.DamageType != DamageType.Physical)
            throw new InvalidOperationException("Reviewed Summon Trap JSON rules mismatch.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null || catalog.Cards.Any(card => card == null))
            throw new InvalidOperationException("Invalid prior native catalog.");
        var before = catalog.Cards.ToArray();
        var prefix = before.Select(Identity).ToArray();
        var catalogGuid = AssetDatabase.AssetPathToGUID(CatalogPath);
        var card = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
        if ((card == null && File.Exists(CardPath)) || (card != null && card.CardId != record.Id))
            throw new InvalidOperationException("Conflicting prior Summon Trap card asset.");
        var matches = before.Where(entry => entry.CardId == record.Id).ToArray();
        if (matches.Length > 1 || (matches.Length == 1 && matches[0] != card))
            throw new InvalidOperationException("Conflicting Summon Trap catalog entry.");
        ImportPortrait(out _, out _, out _);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<TrapSpellCardDefinitionAsset>();
            card.name = record.Id;
            AssetDatabase.CreateAsset(card, CardPath);
        }
        if (!Matches(card, record))
        {
            card.ConfigureBaseForTests(record.Id, record.DisplayName, new ResourceSetData());
            card.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint,
                record.EffectText, record.SpecialEffectText);
            card.ConfigureTrapForTests(new ResourceSetData(record.TrapActivationCost.Mana,
                record.TrapActivationCost.Qi, record.TrapActivationCost.Power, record.TrapActivationCost.Gold),
                record.TrapTrigger, record.TrapEffect, record.Damage);
            card.ConfigureReplicateForTests(false);
            card.ConfigureBoardVisualsForTests(null, null);
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssetIfDirty(card);
        }
        if (matches.Length == 0)
        {
            catalog.ConfigureForTests(before.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (!catalog.Cards.Take(before.Length).Select(Identity).SequenceEqual(prefix) ||
            AssetDatabase.AssetPathToGUID(CatalogPath) != catalogGuid ||
            catalog.Cards.Count(entry => entry.CardId == record.Id) != 1 || !Matches(card, record))
            throw new InvalidOperationException("Existing catalog identities or native Summon Trap definition mismatch.");
    }

    [Serializable]
    public sealed class Report
    {
        public int Before, After, Width, Height;
        public bool Idempotent, PriorPrefixPreserved, PriorAssetsPreserved, SourcePngPreserved, SecondPortraitImportWasNoOp;
        public string CardGuid, ArtworkGuid, SourcePngSha256;
        public long ArtworkFileId;
        public string[] PriorCatalogIdentities;
    }

    public static void Batch()
    {
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!project.StartsWith("C:/Project_333/TempBuild/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native registration is restricted to an isolated TempBuild project.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null || catalog.Cards.Any(card => card == null))
            throw new InvalidOperationException("Missing valid prior catalog.");
        var prefix = catalog.Cards.Select(Identity).ToArray();
        var oldPaths = catalog.Cards.Where(card => card.CardId != CardId)
            .Select(AssetDatabase.GetAssetPath).SelectMany(path => new[] { path, path + ".meta" })
            .Concat(new[] { "TrapHidden", "TrapReturn", "TrapMirrorField", "ExplosiveReactiveArmour", "IronDome" }
                .SelectMany(name => new[] { "Assets/Project333/Resources/Project333/CardArtwork/" + name + ".png",
                    "Assets/Project333/Resources/Project333/CardArtwork/" + name + ".png.meta" })).ToArray();
        var oldHashes = oldPaths.Select(Hash).ToArray();
        var sourceHash = Hash(ArtPath);
        var expectedAfter = catalog.Cards.Count + (catalog.Cards.Any(card => card.CardId == CardId) ? 0 : 1);
        var report = new Report { Before = catalog.Cards.Count, PriorCatalogIdentities = prefix, SourcePngSha256 = sourceHash };
        Run();
        var registeredPaths = new[] { CardPath, CardPath + ".meta", CatalogPath, CatalogPath + ".meta", ArtPath + ".meta" };
        var firstHashes = registeredPaths.Select(Hash).ToArray();
        Run();
        ImportPortrait(out _, out _, out var secondPortraitImportWasNoOp);
        report.SecondPortraitImportWasNoOp = secondPortraitImportWasNoOp;
        report.After = catalog.Cards.Count;
        report.Idempotent = registeredPaths.Select(Hash).SequenceEqual(firstHashes);
        report.PriorPrefixPreserved = catalog.Cards.Take(prefix.Length).Select(Identity).SequenceEqual(prefix);
        report.PriorAssetsPreserved = oldPaths.Select(Hash).SequenceEqual(oldHashes);
        report.SourcePngPreserved = Hash(ArtPath) == sourceHash;
        report.CardGuid = AssetDatabase.AssetPathToGUID(CardPath);
        report.ArtworkGuid = AssetDatabase.AssetPathToGUID(ArtPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long fileId))
            throw new InvalidOperationException("Missing native Summon Trap portrait identity.");
        report.ArtworkFileId = fileId;
        report.Width = sprite.texture.width; report.Height = sprite.texture.height;
        var reportPath = Path.GetFullPath("../SummonTrap_20261001/native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        if (!report.Idempotent || !report.PriorPrefixPreserved || !report.PriorAssetsPreserved ||
            !report.SourcePngPreserved || !report.SecondPortraitImportWasNoOp || report.After != expectedAfter)
            throw new InvalidOperationException("Native Summon Trap registration proof failed.");
        Debug.Log("PASS native SummonTrap registration: idempotent, existing ordered references/assets and original PNG preserved.");
    }
}
