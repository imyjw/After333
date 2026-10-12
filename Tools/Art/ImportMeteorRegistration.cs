// Native Unity registration; run only in the isolated validation project.
using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

public static class ImportMeteorRegistration
{
    public const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/Meteor.asset";
    public const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/Meteor.png";

    public static string Run()
    {
        var artworkBytes = File.ReadAllBytes(ArtworkPath);
        var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        if (artwork == null)
        {
            var importer = AssetImporter.GetAtPath(ArtworkPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing user's staged Meteor artwork.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePivot = Vector2.one * .5f;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        }
        var artworkGuid = AssetDatabase.AssetPathToGUID(ArtworkPath);
        if (artwork == null || string.IsNullOrEmpty(artworkGuid))
            throw new InvalidOperationException("Meteor artwork must have native Sprite metadata.");
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation = CardDatabaseValidator.Validate(database);
        if (validation.Errors.Count != 0) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = database.Cards.Single(card => card.Id == MeteorRules.CardId);
        if (!record.IncludeInDraft || !record.IncludeInRewards) throw new InvalidOperationException("Meteor release pools disabled.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Missing existing StarterTen catalog.");
        var before = catalog.Cards.ToArray();
        if (before.Any(card => card == null)) throw new InvalidOperationException("Catalog has a missing reference.");
        var card = AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
        if (card == null)
        {
            if (File.Exists(CardPath)) throw new InvalidOperationException("Meteor path has an unexpected asset type.");
            card = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            card.name = MeteorRules.CardId;
            AssetDatabase.CreateAsset(card, CardPath);
        }
        card.ConfigureBaseForTests(record.Id, record.DisplayName,
            new ResourceSetData(record.Cost.Mana, record.Cost.Qi, record.Cost.Power, record.Cost.Gold));
        card.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint, record.EffectText, record.SpecialEffectText);
        card.ConfigureForTests(record.EffectId, record.Damage, record.DamageType, record.TriggerCount);
        card.ConfigureReplicateForTests(record.HasReplicate);
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssetIfDirty(card);
        var matching = before.Where(asset => asset.CardId == MeteorRules.CardId).ToArray();
        if (matching.Length > 1 || matching.Length == 1 && matching[0] != card) throw new InvalidOperationException("Conflicting Meteor registration.");
        if (matching.Length == 0)
        {
            catalog.ConfigureForTests(before.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (!artworkBytes.SequenceEqual(File.ReadAllBytes(ArtworkPath))) throw new InvalidOperationException("User Meteor PNG changed.");
        if (catalog.Cards.Count(asset => asset.CardId == MeteorRules.CardId) != 1 || before.Any(asset => !catalog.Cards.Contains(asset)))
            throw new InvalidOperationException("Lost an existing card or duplicated Meteor.");
        var definition = (ScriptedSpellCardDefinition)card.ToDefinition();
        if (definition.Cost.Mana != 7 || definition.Damage != 40 || definition.DamageType != DamageType.Magic ||
            card.EffectText != MeteorRules.EffectDescription || !CardUpgradeRules.IsCardUpgradeable(definition))
            throw new InvalidOperationException("Meteor asset does not match shared rules.");
        return "Meteor native card " + AssetDatabase.AssetPathToGUID(CardPath) + "; artwork PNG preserved " + artworkGuid +
            "; catalog " + catalog.Cards.Count + "; damage40+Lv; " + record.Rarity + ", Mana7; affiliation " + record.Affiliation;
    }
}
