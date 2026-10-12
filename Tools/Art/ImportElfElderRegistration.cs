// Run through native Unity APIs only, in the isolated validation project.
using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

public static class ImportElfElderRegistration
{
    public const string CardId = "ElfElder";
    public const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/ElfElder.asset";
    public const string ConfirmedSpecialEffectText = "관통\n속공";
    public const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    public const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/ElfElder.png";
    public const string FallbackArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/ElfLongbowScout.png";
    public const string ExistingElfPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/ElfLongbowScout.asset";
    public const string ControllerPath = "Assets/Unit/ElfLongbowScout/ElfLongbowScout.controller";

    public static string Run()
    {
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation = CardDatabaseValidator.Validate(database);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = database.Cards.Single(item => item.Id == CardId);
        RequireConfirmedRecord(record);

        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        var existingElf = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(ExistingElfPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (catalog == null || existingElf == null || controller == null || existingElf.BoardSprite == null ||
            existingElf.BoardAnimatorController != controller ||
            AssetDatabase.GetAssetPath(existingElf.BoardSprite) != "Assets/Unit/ElfLongbowScout/Idle.png")
            throw new InvalidOperationException("Existing ElfLongbowScout native card, Idle sprite or controller is missing or mismatched.");

        var before = catalog.Cards.ToArray();
        if (before.Any(item => item == null)) throw new InvalidOperationException("Catalog contains missing native card references.");
        var beforeGuids = before.Select(item => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item))).ToArray();
        var catalogGuid = AssetDatabase.AssetPathToGUID(CatalogPath);
        var card = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(CardPath);
        var cardGuidBefore = AssetDatabase.AssetPathToGUID(CardPath);
        if (card == null && File.Exists(CardPath)) throw new InvalidOperationException("ElfElder asset exists with a different native type.");
        if (card != null && card.CardId != CardId) throw new InvalidOperationException("ElfElder path belongs to a different card.");
        var matching = before.Where(item => item.CardId == CardId).ToArray();
        if (matching.Length > 1 || matching.Length == 1 && matching[0] != card)
            throw new InvalidOperationException("Conflicting ElfElder catalog registration.");
        if (beforeGuids.Any(string.IsNullOrEmpty) || string.IsNullOrEmpty(catalogGuid))
            throw new InvalidOperationException("Existing native card/catalog GUID is missing.");

        // A dedicated user portrait takes precedence. Without one, use the existing
        // ElfLongbowScout through CardArtworkLibrary's alias, never copy/create a PNG.
        var dedicatedPortrait = File.Exists(ArtworkPath);
        var usedArtworkPath = dedicatedPortrait ? ArtworkPath : FallbackArtworkPath;
        var artworkBytes = File.ReadAllBytes(usedArtworkPath);
        var artworkGuidBefore = AssetDatabase.AssetPathToGUID(usedArtworkPath);
        var elfBytes = File.ReadAllBytes(ExistingElfPath);
        var controllerBytes = File.ReadAllBytes(ControllerPath);
        var fallbackMetaBytes = dedicatedPortrait ? null : File.ReadAllBytes(FallbackArtworkPath + ".meta");
        if (dedicatedPortrait)
        {
            AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ArtworkPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("ElfElder portrait is not a native texture import.");
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
            throw new InvalidOperationException("Native portrait or ElfLongbowScout fallback is missing.");
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
            canAttackOnSummon: record.HasRush, hitsPerAttack: record.HitsPerAttack, hasBerserker: record.HasBerserker,
            hasEndure: false, hasShielder: false, hasLifeSteal: false, damageType: record.DamageType,
            physicalDefense: record.PhysicalDefense, magicDefense: record.MagicDefense, races: record.Races,
            sealboundOwnerTurnStarts: 0, hasHiding: false, hasFlying: false, spellPower: 0,
            invincibleDuration: InvincibleDurationType.None, invincibleOwnerTurns: 0, hasPiercing: record.HasPiercing);
        card.ConfigureReplicateForTests(false);
        card.ConfigureBoardVisualsForTests(existingElf.BoardSprite, controller);
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
            !elfBytes.SequenceEqual(File.ReadAllBytes(ExistingElfPath)) ||
            !controllerBytes.SequenceEqual(File.ReadAllBytes(ControllerPath)))
            throw new InvalidOperationException("User portrait or existing ElfLongbowScout assets changed.");
        if (!catalog.Cards.Take(before.Length).SequenceEqual(before) ||
            !catalog.Cards.Take(before.Length).Select(item => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item))).SequenceEqual(beforeGuids) ||
            catalog.Cards.Count != before.Length + (matching.Length == 0 ? 1 : 0) ||
            catalog.Cards.Count(item => item.CardId == CardId) != 1 || AssetDatabase.AssetPathToGUID(CatalogPath) != catalogGuid)
            throw new InvalidOperationException("Existing ordered catalog references/GUIDs changed or new card was duplicated.");
        var cardGuid = AssetDatabase.AssetPathToGUID(CardPath);
        if (string.IsNullOrEmpty(cardGuid) || !string.IsNullOrEmpty(cardGuidBefore) && cardGuid != cardGuidBefore)
            throw new InvalidOperationException("ElfElder native GUID changed.");
        RequireConfirmedDefinition((UnitCardDefinition)card.ToDefinition());
        return "ElfElder native " + cardGuid + "; portrait " + usedArtworkPath + " bytes preserved " + artworkGuid +
            "; catalog " + before.Length + " -> " + catalog.Cards.Count + "; old ordered references/GUIDs preserved; ElfLongbowScout motion reused; Lv13 34/34";
    }

    private static void RequireConfirmedRecord(JsonCardDefinitionRecord record)
    {
        if (record.DefinitionType != JsonCardDefinitionKind.Unit || record.DisplayName != "엘프 장로" ||
            record.Rarity != CardRarity.Rare || record.Affiliation != CardAffiliation.Fantasy ||
            record.ChargeTileFootprint != ChargeTileFootprint.OneByOne || !record.IncludeInDraft || !record.IncludeInRewards ||
            !string.IsNullOrEmpty(record.EffectText) || record.SpecialEffectText != ConfirmedSpecialEffectText)
            throw new InvalidOperationException("ElfElder JSON metadata/release pools differ from the confirmed card.");
        RequireConfirmedDefinition((UnitCardDefinition)record.ToDefinition());
    }

    private static void RequireConfirmedDefinition(UnitCardDefinition unit)
    {
        if (unit.Cost.Mana != 5 || unit.Cost.Qi != 0 || unit.Cost.Power != 0 || unit.Cost.Gold != 0 ||
            unit.Attack != 30 || unit.Health != 25 || unit.AttackType != AttackType.Ranged || unit.DamageType != DamageType.Physical ||
            unit.PhysicalDefense != 0 || unit.MagicDefense != 0 || !unit.CanMove || unit.IsScience || unit.SciencePowerUpkeep != 0 ||
            unit.HitsPerAttack != 1 || unit.MaxAttacksPerTurn != 1 || unit.HasBerserker || unit.HasEndure || unit.HasShielder ||
            unit.HasLifeSteal || !unit.HasRush || unit.HasReplicate || unit.HasHiding || unit.HasFlying || !unit.HasPiercing ||
            unit.SealboundOwnerTurnStarts != 0 || unit.SpellPower != 0 || unit.InvincibleDuration != InvincibleDurationType.None ||
            unit.InvincibleOwnerTurns != 0 || unit.TurnStartResourceGain.Mana != 0 || unit.TurnStartResourceGain.Qi != 0 ||
            unit.TurnStartResourceGain.Power != 0 || unit.TurnStartResourceGain.Gold != 0 ||
            !unit.Races.SequenceEqual(new[] { UnitRace.Elf }) || !CardUpgradeRules.IsCardUpgradeable(unit) ||
            CardLevelStatRules.ApplyAttackBonus(CardId, unit.Attack, 13) != 34 ||
            CardLevelStatRules.ApplyHpBonus(CardId, unit.Health, 13) != 34)
            throw new InvalidOperationException("ElfElder definition/upgrade rules differ from confirmed 30/25, ranged Elf with only Piercing and Rush, Lv13 34/34.");
    }
}
