// Stage in the isolated validation Editor; this registers the existing artist-owned card image.
using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

public static class ImportEarthquakeRegistration
{
    public const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/Earthquake.asset";
    public const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/Earthquake.png";

    public static string Run()
    {
        var artworkBytes = File.ReadAllBytes(ArtworkPath);
        var artworkMeta = File.ReadAllBytes(ArtworkPath + ".meta");
        var artworkGuid = AssetDatabase.AssetPathToGUID(ArtworkPath);
        var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        if (artwork == null || string.IsNullOrEmpty(artworkGuid))
            throw new InvalidOperationException("Stage the user's Earthquake PNG and original metadata before registration.");
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation = CardDatabaseValidator.Validate(database);
        if (validation.Errors.Count != 0) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = database.Cards.Single(card => card.Id == EarthquakeRules.CardId);
        if (!record.IncludeInDraft || !record.IncludeInRewards)
            throw new InvalidOperationException("Earthquake must be enabled in both requested release pools.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Missing existing StarterTen card catalog.");
        var oldCards = catalog.Cards.ToArray();
        if (oldCards.Any(card => card == null)) throw new InvalidOperationException("Catalog already contains a missing reference.");
        var card = AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
        if (card == null)
        {
            if (File.Exists(CardPath)) throw new InvalidOperationException("Earthquake path has an unexpected asset type.");
            card = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            card.name = EarthquakeRules.CardId;
            AssetDatabase.CreateAsset(card, CardPath);
        }
        card.ConfigureBaseForTests(record.Id, record.DisplayName,
            new ResourceSetData(record.Cost.Mana, record.Cost.Qi, record.Cost.Power, record.Cost.Gold));
        card.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint,
            record.EffectText, record.SpecialEffectText);
        card.ConfigureForTests(record.EffectId, record.Damage, record.DamageType, record.TriggerCount);
        card.ConfigureReplicateForTests(record.HasReplicate);
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssetIfDirty(card);
        var registered = oldCards.Where(asset => asset.CardId == EarthquakeRules.CardId).ToArray();
        if (registered.Length > 1 || (registered.Length == 1 && registered[0] != card))
            throw new InvalidOperationException("Catalog contains a conflicting Earthquake registration.");
        if (registered.Length == 0)
        {
            catalog.ConfigureForTests(oldCards.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (!artworkBytes.SequenceEqual(File.ReadAllBytes(ArtworkPath)) ||
            !artworkMeta.SequenceEqual(File.ReadAllBytes(ArtworkPath + ".meta")))
            throw new InvalidOperationException("The artist-owned Earthquake PNG/metadata changed during registration.");
        if (catalog.Cards.Count(asset => asset.CardId == EarthquakeRules.CardId) != 1 ||
            oldCards.Where(asset => asset.CardId != EarthquakeRules.CardId).Any(asset => !catalog.Cards.Contains(asset)))
            throw new InvalidOperationException("Registration removed an existing card or duplicated Earthquake.");
        var definition = (ScriptedSpellCardDefinition)card.ToDefinition();
        if (definition.Damage != EarthquakeRules.BaseDamage || definition.DamageType != DamageType.Physical ||
            card.EffectText != EarthquakeRules.EffectDescription || !CardUpgradeRules.IsCardUpgradeable(definition))
            throw new InvalidOperationException("Registered Earthquake does not match the current shared rules.");
        return "Earthquake registered: " + AssetDatabase.AssetPathToGUID(CardPath) +
            "; artwork preserved: " + artworkGuid + "; catalog cards: " + catalog.Cards.Count;
    }
}
