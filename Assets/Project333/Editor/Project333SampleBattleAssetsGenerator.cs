using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333SampleBattleAssetsGenerator
    {
        private const string ScriptableObjectsRoot = "Assets/Project333/ScriptableObjects";
        private const string SampleRoot = ScriptableObjectsRoot + "/SampleBattle";
        private const string CardsRoot = SampleRoot + "/Cards";
        private const string DecksRoot = SampleRoot + "/Decks";

        [MenuItem("Tools/Project333/Generate Sample Battle Assets")]
        public static void Generate()
        {
            EnsureFolderExists("Assets", "Project333");
            EnsureFolderExists("Assets/Project333", "ScriptableObjects");
            EnsureFolderExists(ScriptableObjectsRoot, "SampleBattle");
            EnsureFolderExists(SampleRoot, "Cards");
            EnsureFolderExists(SampleRoot, "Decks");

            var cards = new List<CardDefinitionAsset>
            {
                CreateIronShielder(),
                CreateLongbowScout(),
                CreateGoldMiner(),
                CreateManaAcolyte(),
                CreateQiDisciple(),
                CreatePowerTurret(),
                CreateRushRaider(),
                CreateTwinBladeAdept(),
                CreateTeslaSentinel(),
                CreateFirebolt(),
                CreatePowerContract()
            };

            var catalog = LoadOrCreate<CardDefinitionCatalogAsset>($"{SampleRoot}/SampleCardCatalog.asset");
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(catalog);

            var playerDeck = LoadOrCreate<DeckDefinitionAsset>($"{DecksRoot}/SamplePlayerDeck.asset");
            playerDeck.ConfigureForTests(BuildMirrorDeck(cards).ToArray());
            EditorUtility.SetDirty(playerDeck);

            var aiDeck = LoadOrCreate<DeckDefinitionAsset>($"{DecksRoot}/SampleAiDeck.asset");
            aiDeck.ConfigureForTests(BuildMirrorDeck(cards).ToArray());
            EditorUtility.SetDirty(aiDeck);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;

            Debug.Log(
                "Generated sample Project333 battle assets under Assets/Project333/ScriptableObjects/SampleBattle. " +
                "These are sample playtest assets, not locked final content.");
        }

        private static UnitCardDefinitionAsset CreateIronShielder()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleIronShielder.asset");
            asset.ConfigureBaseForTests("sample_iron_shielder", "Sample Iron Shielder", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                CardAffiliation.Neutral,
                ChargeTileFootprint.OneByOne,
                string.Empty,
                "Shielder");
            asset.ConfigureForTests(AttackType.Melee, 2, 7, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, false, hasShielder: true);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateLongbowScout()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleLongbowScout.asset");
            asset.ConfigureBaseForTests("sample_longbow_scout", "Sample Longbow Scout", new ResourceSetData(0, 0, 0, 3));
            asset.ConfigureForTests(AttackType.Ranged, 3, 4, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateGoldMiner()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleGoldMiner.asset");
            asset.ConfigureBaseForTests("sample_gold_miner", "Sample Gold Miner", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureForTests(AttackType.Melee, 1, 4, true, false, 0, new ResourceSetData(0, 0, 0, 1), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateManaAcolyte()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleManaAcolyte.asset");
            asset.ConfigureBaseForTests("sample_mana_acolyte", "Sample Mana Acolyte", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureForTests(AttackType.Ranged, 1, 3, true, false, 0, new ResourceSetData(1, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateQiDisciple()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleQiDisciple.asset");
            asset.ConfigureBaseForTests("sample_qi_disciple", "Sample Qi Disciple", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureForTests(AttackType.Melee, 2, 4, true, false, 0, new ResourceSetData(0, 1, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BuildingCardDefinitionAsset CreatePowerTurret()
        {
            var asset = LoadOrCreate<BuildingCardDefinitionAsset>($"{CardsRoot}/SamplePowerTurret.asset");
            asset.ConfigureBaseForTests("sample_power_turret", "Sample Power Turret", new ResourceSetData(0, 0, 0, 4));
            asset.ConfigureForTests(true, 2, 7, new ResourceSetData(0, 0, 1, 0), false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateRushRaider()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleRushRaider.asset");
            asset.ConfigureBaseForTests("sample_rush_raider", "Sample Rush Raider", new ResourceSetData(0, 1, 0, 2));
            asset.ConfigureForTests(AttackType.Melee, 3, 3, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, true);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateTwinBladeAdept()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleTwinBladeAdept.asset");
            asset.ConfigureBaseForTests("sample_twin_blade_adept", "Sample Twin Blade Adept", new ResourceSetData(1, 0, 0, 3));
            asset.ConfigureForTests(AttackType.Melee, 2, 5, true, false, 0, new ResourceSetData(0, 0, 0, 0), 2, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateTeslaSentinel()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/SampleTeslaSentinel.asset");
            asset.ConfigureBaseForTests("sample_tesla_sentinel", "Sample Tesla Sentinel", new ResourceSetData(0, 0, 0, 4));
            asset.ConfigureForTests(AttackType.Melee, 4, 6, true, true, 2, new ResourceSetData(0, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DamageSpellCardDefinitionAsset CreateFirebolt()
        {
            var asset = LoadOrCreate<DamageSpellCardDefinitionAsset>($"{CardsRoot}/SampleFirebolt.asset");
            asset.ConfigureBaseForTests("sample_firebolt", "Sample Firebolt", new ResourceSetData(1, 0, 0, 1));
            asset.ConfigureForTests(4);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static PersistentResourceSpellCardDefinitionAsset CreatePowerContract()
        {
            var asset = LoadOrCreate<PersistentResourceSpellCardDefinitionAsset>($"{CardsRoot}/SamplePowerContract.asset");
            asset.ConfigureBaseForTests("sample_power_contract", "Sample Power Contract", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureForTests(
                "sample_power_contract_effect",
                new ResourceSetData(0, 0, 1, 0),
                "Expires at the end of the caster's next turn.");
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static List<CardDefinitionAsset> BuildMirrorDeck(IReadOnlyList<CardDefinitionAsset> cards)
        {
            var deck = new List<CardDefinitionAsset>(cards.Count * 3);

            for (var copyIndex = 0; copyIndex < 3; copyIndex++)
            {
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    deck.Add(cards[cardIndex]);
                }
            }

            return deck;
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static void EnsureFolderExists(string parentFolder, string childFolder)
        {
            var fullPath = $"{parentFolder}/{childFolder}";
            if (AssetDatabase.IsValidFolder(fullPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}

