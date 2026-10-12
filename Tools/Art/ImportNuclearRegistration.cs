// Stage in the isolated validation Editor; this registers the existing artist-owned card image.
using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

public static class ImportNuclearRegistration
{
    public const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/Nuclear.asset";
    public const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
    const string ArtworkPath = "Assets/Project333/Resources/Project333/CardArtwork/Nuclear.png";

    public static string Run()
    {
        var artworkBytes = File.ReadAllBytes(ArtworkPath);
        var artworkMeta = File.ReadAllBytes(ArtworkPath + ".meta");
        var artworkGuid = AssetDatabase.AssetPathToGUID(ArtworkPath);
        var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        if (artwork == null || string.IsNullOrEmpty(artworkGuid))
            throw new InvalidOperationException("Stage the user's Nuclear PNG and original metadata before registration.");
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
        var validation = CardDatabaseValidator.Validate(database);
        if (validation.Errors.Count != 0) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        var record = database.Cards.Single(card => card.Id == NuclearRules.CardId);
        if (!record.IncludeInDraft || !record.IncludeInRewards)
            throw new InvalidOperationException("Nuclear must be enabled in both requested release pools.");
        var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Missing existing StarterTen card catalog.");
        var oldCards = catalog.Cards.ToArray();
        if (oldCards.Any(card => card == null)) throw new InvalidOperationException("Catalog already contains a missing reference.");
        var card = AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(CardPath);
        if (card == null)
        {
            if (File.Exists(CardPath)) throw new InvalidOperationException("Nuclear path has an unexpected asset type.");
            card = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            card.name = NuclearRules.CardId;
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
        var registered = oldCards.Where(asset => asset.CardId == NuclearRules.CardId).ToArray();
        if (registered.Length > 1 || (registered.Length == 1 && registered[0] != card))
            throw new InvalidOperationException("Catalog contains a conflicting Nuclear registration.");
        if (registered.Length == 0)
        {
            catalog.ConfigureForTests(oldCards.Concat(new[] { card }).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        if (!artworkBytes.SequenceEqual(File.ReadAllBytes(ArtworkPath)) ||
            !artworkMeta.SequenceEqual(File.ReadAllBytes(ArtworkPath + ".meta")))
            throw new InvalidOperationException("The artist-owned Nuclear PNG/metadata changed during registration.");
        if (catalog.Cards.Count(asset => asset.CardId == NuclearRules.CardId) != 1 ||
            oldCards.Where(asset => asset.CardId != NuclearRules.CardId).Any(asset => !catalog.Cards.Contains(asset)))
            throw new InvalidOperationException("Registration removed an existing card or duplicated Nuclear.");
        var definition = (ScriptedSpellCardDefinition)card.ToDefinition();
        if (definition.Damage != NuclearRules.BaseDamage || definition.DamageType != DamageType.Fixed ||
            card.EffectText != NuclearRules.EffectDescription || !CardUpgradeRules.IsCardUpgradeable(definition))
            throw new InvalidOperationException("Registered Nuclear does not match the current shared rules.");
        return "Nuclear registered: " + AssetDatabase.AssetPathToGUID(CardPath) +
            "; artwork preserved: " + artworkGuid + "; catalog cards: " + catalog.Cards.Count;
    }
}
