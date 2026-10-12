using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;

// Execute only in the retained isolated validation project. No scene or existing artwork is modified.
public static class ImportExplosiveReactiveArmourRegistration
{
    private const string CardId = ExplosiveReactiveArmourCardRules.CardId;
    private const int ActivationPowerCost = ExplosiveReactiveArmourCardRules.ActivationPowerCost;
    private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/ExplosiveReactiveArmour.asset";
    private const string ArtPath = "Assets/Project333/Resources/Project333/CardArtwork/ExplosiveReactiveArmour.png";
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

    private static long LocalId(Sprite sprite)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id))
            throw new InvalidOperationException("Missing native reactive armour portrait identity.");
        return id;
    }

    private static ISpriteEditorDataProvider Provider()
    {
        var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing reviewed reactive armour artwork.");
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

    private static void ImportPortrait()
    {
        var provider = Provider();
        var previous = provider.GetSpriteRects();
        if (previous.Length > 1 || previous.Any(rect => rect.name != CardId))
            throw new InvalidOperationException("Unexpected existing reactive armour sprite layout/name.");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        int width, height;
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(ArtPath), false))
                throw new InvalidOperationException("Reactive armour PNG decode failed.");
            width = texture.width; height = texture.height;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        var prior = previous.SingleOrDefault();
        var rect = new SpriteRect
        {
            name = CardId, rect = new Rect(0, 0, width, height),
            border = Vector4.zero, pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
            spriteID = prior == null ? GUID.Generate() : prior.spriteID
        };
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(.5f, .5f);
        importer.SetTextureSettings(settings);
        provider.SetSpriteRects(new[] { rect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            new[] { new SpriteNameFileIdPair(rect.name, rect.spriteID) });
        provider.Apply();
        importer.SaveAndReimport();
        var sprite = Resources.Load<Sprite>("Project333/CardArtwork/" + CardId);
        if (sprite == null || sprite.rect != rect.rect || sprite.pivot != new Vector2(width / 2f, height / 2f)
            || sprite.vertices.Length != 4 || sprite.pixelsPerUnit != 100)
            throw new InvalidOperationException("Native full reactive armour portrait mismatch.");
    }

    private static void Run()
    {
        var json = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        var validation = CardDatabaseValidator.Validate(json);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = json.Cards.Single(card => card.Id == CardId);
        if (record.DefinitionType != JsonCardDefinitionKind.TrapSpell || !record.HasTrap
            || record.Rarity != CardRarity.Rare || record.Affiliation != CardAffiliation.ScienceCivilization
            || record.ChargeTileFootprint != ChargeTileFootprint.None || !record.IncludeInDraft || record.IncludeInRewards
            || record.Cost.Mana + record.Cost.Qi + record.Cost.Power + record.Cost.Gold != 0
            || record.TrapActivationCost.Power != ActivationPowerCost
            || record.TrapActivationCost.Mana + record.TrapActivationCost.Qi + record.TrapActivationCost.Gold != 0
            || record.TrapTrigger != TrapTriggerType.EnemyMeleeUnitAttacksMaster
            || record.TrapEffect != TrapEffectType.DestroyAttacker)
            throw new InvalidOperationException("Reviewed reactive armour JSON rules mismatch.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null || catalog.Cards.Any(card => card == null))
            throw new InvalidOperationException("Invalid prior native catalog.");
        var before = catalog.Cards.ToArray();
        var prefix = before.Select(Identity).ToArray();
        var catalogGuid = AssetDatabase.AssetPathToGUID(CatalogPath);
        var card = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
        if ((card == null && File.Exists(CardPath)) || (card != null && card.CardId != record.Id))
            throw new InvalidOperationException("Conflicting prior reactive armour card asset.");
        var matches = before.Where(entry => entry.CardId == record.Id).ToArray();
        if (matches.Length > 1 || (matches.Length == 1 && matches[0] != card))
            throw new InvalidOperationException("Conflicting reactive armour catalog entry.");
        var sourceHash = Hash(ArtPath);
        ImportPortrait();
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<TrapSpellCardDefinitionAsset>();
            card.name = record.Id;
            AssetDatabase.CreateAsset(card, CardPath);
        }
        card.ConfigureBaseForTests(record.Id, record.DisplayName, new ResourceSetData());
        card.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint,
            record.EffectText, record.SpecialEffectText);
        card.ConfigureTrapForTests(new ResourceSetData(record.TrapActivationCost.Mana,
            record.TrapActivationCost.Qi, record.TrapActivationCost.Power, record.TrapActivationCost.Gold),
            record.TrapTrigger, record.TrapEffect);
        card.ConfigureReplicateForTests(false);
        card.ConfigureBoardVisualsForTests(null, null);
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssetIfDirty(card);
        if (matches.Length == 0)
        {
            catalog.ConfigureForTests(before.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (!catalog.Cards.Take(before.Length).Select(Identity).SequenceEqual(prefix)
            || AssetDatabase.AssetPathToGUID(CatalogPath) != catalogGuid
            || catalog.Cards.Count(entry => entry.CardId == record.Id) != 1 || Hash(ArtPath) != sourceHash)
            throw new InvalidOperationException("Existing catalog identities or reactive armour PNG changed.");
        var definition = (TrapSpellCardDefinition)card.ToDefinition();
        if (definition.ActivationCost.Power != ActivationPowerCost
            || definition.ActivationCost.Mana + definition.ActivationCost.Qi + definition.ActivationCost.Gold != 0
            || definition.Cost.Mana + definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold != 0
            || definition.TriggerType != TrapTriggerType.EnemyMeleeUnitAttacksMaster
            || definition.EffectType != TrapEffectType.DestroyAttacker
            || CardUpgradeRules.IsCardUpgradeable(definition) || !definition.HasTrap
            || card.BoardSprite != null || card.BoardAnimatorController != null)
            throw new InvalidOperationException("Native reactive armour trap rules mismatch.");
    }

    [Serializable]
    public sealed class Report
    {
        public int Before, After;
        public bool Idempotent, PriorPrefixPreserved, PriorAssetsPreserved, SourcePngPreserved;
        public string CardGuid, ArtworkGuid, SourcePngSha256;
        public long ArtworkFileId;
        public string[] PriorCatalogIdentities;
    }

    public static void Batch()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null || catalog.Cards.Any(card => card == null))
            throw new InvalidOperationException("Missing valid prior catalog.");
        var prefix = catalog.Cards.Select(Identity).ToArray();
        var oldPaths = catalog.Cards.Where(card => card.CardId != CardId)
            .Select(AssetDatabase.GetAssetPath).SelectMany(path => new[] { path, path + ".meta" })
            .Concat(new[] { "Assets/Project333/Resources/Project333/CardArtwork/TrapHidden.png",
                "Assets/Project333/Resources/Project333/CardArtwork/TrapHidden.png.meta",
                "Assets/Project333/Resources/Project333/CardArtwork/TrapReturn.png",
                "Assets/Project333/Resources/Project333/CardArtwork/TrapReturn.png.meta",
                "Assets/Project333/Resources/Project333/CardArtwork/TrapMirrorField.png",
                "Assets/Project333/Resources/Project333/CardArtwork/TrapMirrorField.png.meta" }).ToArray();
        var oldHashes = oldPaths.Select(Hash).ToArray();
        var sourceHash = Hash(ArtPath);
        var expectedAfter = catalog.Cards.Count + (catalog.Cards.Any(card => card.CardId == CardId) ? 0 : 1);
        var report = new Report { Before = catalog.Cards.Count, PriorCatalogIdentities = prefix, SourcePngSha256 = sourceHash };
        Run();
        var registeredPaths = new[] { CardPath, CardPath + ".meta", CatalogPath, CatalogPath + ".meta", ArtPath + ".meta" };
        var firstHashes = registeredPaths.Select(Hash).ToArray();
        Run();
        report.After = catalog.Cards.Count;
        report.Idempotent = registeredPaths.Select(Hash).SequenceEqual(firstHashes);
        report.PriorPrefixPreserved = catalog.Cards.Take(prefix.Length).Select(Identity).SequenceEqual(prefix);
        report.PriorAssetsPreserved = oldPaths.Select(Hash).SequenceEqual(oldHashes);
        report.SourcePngPreserved = Hash(ArtPath) == sourceHash;
        report.CardGuid = AssetDatabase.AssetPathToGUID(CardPath);
        report.ArtworkGuid = AssetDatabase.AssetPathToGUID(ArtPath);
        report.ArtworkFileId = LocalId(Resources.Load<Sprite>("Project333/CardArtwork/" + CardId));
        var reportPath = Path.GetFullPath("../ExplosiveReactiveArmour_20261001/native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        if (!report.Idempotent || !report.PriorPrefixPreserved || !report.PriorAssetsPreserved
            || !report.SourcePngPreserved || report.After != expectedAfter)
            throw new InvalidOperationException("Native reactive armour registration proof failed.");
        Debug.Log("PASS native ExplosiveReactiveArmour registration: idempotent, existing ordered references/assets and original PNG preserved.");
    }
}
