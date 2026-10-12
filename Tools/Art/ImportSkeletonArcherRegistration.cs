// Run through native Unity APIs only, in the isolated validation project.
using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

public static class ImportSkeletonArcherRegistration
{
    public const string CardId = "SkeletonArcher";
    public const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/SkeletonArcher.asset";
    public const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    public const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/SkeletonArcher.png";
    public const string FallbackArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/Skeleton.png";
    public const string ExistingSkeletonPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/Skeleton.asset";
    public const string ControllerPath = "Assets/Unit/Skeleton/Skeleton.controller";

    public static string Run()
    {
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation = CardDatabaseValidator.Validate(database);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = database.Cards.Single(item => item.Id == CardId);
        RequireConfirmedRecord(record);

        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        var existingSkeleton = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(ExistingSkeletonPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (catalog == null || existingSkeleton == null || controller == null || existingSkeleton.BoardSprite == null ||
            existingSkeleton.BoardAnimatorController != controller ||
            AssetDatabase.GetAssetPath(existingSkeleton.BoardSprite) != "Assets/Unit/Skeleton/Idle.png")
            throw new InvalidOperationException("Existing Skeleton native card, Idle sprite or controller is missing or mismatched.");

        var before = catalog.Cards.ToArray();
        if (before.Any(item => item == null)) throw new InvalidOperationException("Catalog contains missing native card references.");
        var beforeGuids = before.Select(item => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item))).ToArray();
        var catalogGuid = AssetDatabase.AssetPathToGUID(CatalogPath);
        var card = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
        var cardGuidBefore = AssetDatabase.AssetPathToGUID(CardPath);
        if (card == null && File.Exists(CardPath)) throw new InvalidOperationException("SkeletonArcher asset exists with a different native type.");
        if (card != null && card.CardId != CardId) throw new InvalidOperationException("SkeletonArcher path belongs to a different card.");
        var matching = before.Where(item => item.CardId == CardId).ToArray();
        if (matching.Length > 1 || matching.Length == 1 && matching[0] != card)
            throw new InvalidOperationException("Conflicting SkeletonArcher catalog registration.");
        if (beforeGuids.Any(string.IsNullOrEmpty) || string.IsNullOrEmpty(catalogGuid))
            throw new InvalidOperationException("Existing native card/catalog GUID is missing.");

        // A dedicated user portrait takes precedence. Without one, use the existing
        // Skeleton through CardArtworkLibrary's alias, never copy/create a PNG.
        var dedicatedPortrait = File.Exists(ArtworkPath);
        var usedArtworkPath = dedicatedPortrait ? ArtworkPath : FallbackArtworkPath;
        var artworkBytes = File.ReadAllBytes(usedArtworkPath);
        var artworkGuidBefore = AssetDatabase.AssetPathToGUID(usedArtworkPath);
        var skeletonBytes = File.ReadAllBytes(ExistingSkeletonPath);
        var controllerBytes = File.ReadAllBytes(ControllerPath);
        var fallbackMetaBytes = dedicatedPortrait ? null : File.ReadAllBytes(FallbackArtworkPath + ".meta");
        if (dedicatedPortrait)
        {
            AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ArtworkPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("SkeletonArcher portrait is not a native texture import.");
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(sourceWidth, sourceHeight)));
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var dedicatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
            if (dedicatedSprite == null || dedicatedSprite.rect != new Rect(0, 0, sourceWidth, sourceHeight))
                throw new InvalidOperationException("Native dedicated portrait must include the full original image.");
        }
        var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(usedArtworkPath);
        var artworkGuid = AssetDatabase.AssetPathToGUID(usedArtworkPath);
        if (artwork == null || string.IsNullOrEmpty(artworkGuid))
            throw new InvalidOperationException("Native portrait or Skeleton fallback is missing.");
        if (!string.IsNullOrEmpty(artworkGuidBefore) && artworkGuid != artworkGuidBefore)
            throw new InvalidOperationException("Existing portrait GUID changed.");
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            card.name = CardId;
            AssetDatabase.CreateAsset(card, CardPath);
        }
        card.ConfigureBaseForTests(record.Id, record.DisplayName,
            new ResourceSetData(record.Cost.Mana, record.Cost.Qi, record.Cost.Power, record.Cost.Gold));
        card.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint,
            record.EffectText, record.SpecialEffectText);
        card.ConfigureForTests(
            attackType: record.AttackType, attack: record.Attack, health: record.Health,
            canMove: record.CanMove, isScience: record.IsScience, sciencePowerUpkeep: record.SciencePowerUpkeep,
            turnStartResourceGain: new ResourceSetData(), maxAttacksPerTurn: record.MaxAttacksPerTurn,
            canAttackOnSummon: false, hitsPerAttack: record.HitsPerAttack, hasBerserker: record.HasBerserker,
            hasEndure: false, hasShielder: false, hasLifeSteal: false, damageType: record.DamageType,
            physicalDefense: record.PhysicalDefense, magicDefense: record.MagicDefense, races: record.Races,
            sealboundOwnerTurnStarts: 0, hasHiding: false, hasFlying: false, spellPower: 0,
            invincibleDuration: InvincibleDurationType.None, invincibleOwnerTurns: 0, hasPiercing: false);
        card.ConfigureReplicateForTests(false);
        card.ConfigureBoardVisualsForTests(existingSkeleton.BoardSprite, controller);
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssetIfDirty(card);
        if (matching.Length == 0)
        {
            catalog.ConfigureForTests(before.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        if (!artworkBytes.SequenceEqual(File.ReadAllBytes(usedArtworkPath)) ||
            !dedicatedPortrait && (!fallbackMetaBytes.SequenceEqual(File.ReadAllBytes(FallbackArtworkPath + ".meta")) || File.Exists(ArtworkPath)) ||
            !skeletonBytes.SequenceEqual(File.ReadAllBytes(ExistingSkeletonPath)) ||
            !controllerBytes.SequenceEqual(File.ReadAllBytes(ControllerPath)))
            throw new InvalidOperationException("User portrait or existing Skeleton assets changed.");
        if (!catalog.Cards.Take(before.Length).SequenceEqual(before) ||
            !catalog.Cards.Take(before.Length).Select(item => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item))).SequenceEqual(beforeGuids) ||
            catalog.Cards.Count != before.Length + (matching.Length == 0 ? 1 : 0) ||
            catalog.Cards.Count(item => item.CardId == CardId) != 1 || AssetDatabase.AssetPathToGUID(CatalogPath) != catalogGuid)
            throw new InvalidOperationException("Existing ordered catalog references/GUIDs changed or new card was duplicated.");
        var cardGuid = AssetDatabase.AssetPathToGUID(CardPath);
        if (string.IsNullOrEmpty(cardGuid) || !string.IsNullOrEmpty(cardGuidBefore) && cardGuid != cardGuidBefore)
            throw new InvalidOperationException("SkeletonArcher native GUID changed.");
        RequireConfirmedDefinition((UnitCardDefinition)card.ToDefinition());
        return "SkeletonArcher native " + cardGuid + "; portrait " + usedArtworkPath + " bytes preserved " + artworkGuid +
            "; catalog " + before.Length + " -> " + catalog.Cards.Count + "; old ordered references/GUIDs preserved; Skeleton motion reused; Lv13 14/12";
    }

    private static void RequireConfirmedRecord(JsonCardDefinitionRecord record)
    {
        if (record.DefinitionType != JsonCardDefinitionKind.Unit || record.DisplayName != "스켈레톤아처" ||
            record.Rarity != CardRarity.Common || record.Affiliation != CardAffiliation.Fantasy ||
            record.ChargeTileFootprint != ChargeTileFootprint.OneByOne || !record.IncludeInDraft || !record.IncludeInRewards ||
            !string.IsNullOrEmpty(record.EffectText) || !string.IsNullOrEmpty(record.SpecialEffectText))
            throw new InvalidOperationException("SkeletonArcher JSON metadata/release pools differ from the confirmed card.");
        RequireConfirmedDefinition((UnitCardDefinition)record.ToDefinition());
    }

    private static void RequireConfirmedDefinition(UnitCardDefinition unit)
    {
        if (unit.Cost.Mana != 1 || unit.Cost.Qi != 0 || unit.Cost.Power != 0 || unit.Cost.Gold != 0 ||
            unit.Attack != 10 || unit.Health != 3 || unit.AttackType != AttackType.Ranged || unit.DamageType != DamageType.Physical ||
            unit.PhysicalDefense != 0 || unit.MagicDefense != 0 || !unit.CanMove || unit.IsScience || unit.SciencePowerUpkeep != 0 ||
            unit.HitsPerAttack != 1 || unit.MaxAttacksPerTurn != 1 || unit.HasBerserker || unit.HasEndure || unit.HasShielder ||
            unit.HasLifeSteal || unit.HasRush || unit.HasReplicate || unit.HasHiding || unit.HasFlying || unit.HasPiercing ||
            unit.SealboundOwnerTurnStarts != 0 || unit.SpellPower != 0 || unit.InvincibleDuration != InvincibleDurationType.None ||
            unit.InvincibleOwnerTurns != 0 || unit.TurnStartResourceGain.Mana != 0 || unit.TurnStartResourceGain.Qi != 0 ||
            unit.TurnStartResourceGain.Power != 0 || unit.TurnStartResourceGain.Gold != 0 ||
            !unit.Races.SequenceEqual(new[] { UnitRace.Undead }) || !CardUpgradeRules.IsCardUpgradeable(unit) ||
            CardLevelStatRules.ApplyAttackBonus(CardId, unit.Attack, 13) != 14 ||
            CardLevelStatRules.ApplyHpBonus(CardId, unit.Health, 13) != 12)
            throw new InvalidOperationException("SkeletonArcher definition/upgrade rules differ from confirmed 10/3, ranged Undead without innate traits, Lv13 14/12.");
    }
}
