using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Editor
{
    public static class Project333JarOfGreedCardBuilder
    {
        private const string Root = "Assets/Project333/ScriptableObjects/StarterTen";

        [MenuItem("Tools/Project333/Cards/Create or Update Jar Of Greed")]
        public static void Build()
        {
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
                "Assets/Project333/Resources/Project333/Data/cards.json"));
            var record = database.Cards.Single(card => card.Id == JarOfGreedRules.CardId);
            var validation = CardDatabaseValidator.Validate(database);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(Root + "/StarterTenCardCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("Existing card catalog is required.");
            var artworkPath = "Assets/Project333/Resources/Project333/CardArtwork/" + record.Id + ".png";
            AssetDatabase.ImportAsset(artworkPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(artworkPath) as TextureImporter;
            if (importer != null && Project333CardArtworkImporter.Normalize(importer)) importer.SaveAndReimport();
            var path = Root + "/Cards/" + record.Id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(path);
            if (asset == null)
            {
                asset = UnityEngine.ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.ConfigureBaseForTests(record.Id, record.DisplayName,
                new ResourceSetData(0, 0, 0, JarOfGreedRules.GoldCost));
            asset.ConfigureMetadataForTests(record.Rarity, record.Affiliation, record.ChargeTileFootprint,
                record.EffectText, record.SpecialEffectText);
            asset.ConfigureReplicateForTests(false);
            asset.ConfigureForTests(record.EffectId, record.Damage, record.DamageType, record.TriggerCount, record.EffectAmount);
            var cards = new List<CardDefinitionAsset>(catalog.Cards ?? Array.Empty<CardDefinitionAsset>());
            if (!cards.Any(card => card != null && card.CardId == record.Id)) cards.Add(asset);
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("JarOfGreed registered. Draft/reward eligibility remains controlled by cards.json.");
        }
    }
}
