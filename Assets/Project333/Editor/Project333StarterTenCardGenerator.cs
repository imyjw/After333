using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333StarterTenCardGenerator
    {
        private const string ScriptableObjectsRoot = "Assets/Project333/ScriptableObjects";
        private const string StarterRoot = ScriptableObjectsRoot + "/StarterTen";
        private const string CardsRoot = StarterRoot + "/Cards";

        [MenuItem("Tools/Project333/Generate Starter 10 Card Set")]
        public static void Generate()
        {
            EnsureFolderExists("Assets", "Project333");
            EnsureFolderExists("Assets/Project333", "ScriptableObjects");
            EnsureFolderExists(ScriptableObjectsRoot, "StarterTen");
            EnsureFolderExists(StarterRoot, "Cards");

            var cards = new List<CardDefinitionAsset>
            {
                CreateIronGuard(),
                CreateLongbowScout(),
                CreateGoldMiner(),
                CreateManaAcolyte(),
                CreatePowerTurret(),
                CreateRushRaider(),
                CreateTwinBladeAdept(),
                CreateTeslaSentinel(),
                CreateFirebolt(),
                CreatePowerContract(),
            };

            var catalog = LoadOrCreate<CardDefinitionCatalogAsset>($"{StarterRoot}/StarterTenCardCatalog.asset");
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;

            Debug.Log(
                "Generated Starter 10 card set under Assets/Project333/ScriptableObjects/StarterTen. " +
                "Edit these assets directly in the Inspector to enter your first real card data.");
        }

        private static UnitCardDefinitionAsset CreateIronGuard()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/IronGuard.asset");
            asset.ConfigureBaseForTests("starter_iron_guard", "Iron Guard", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Earth",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                "A steady front-line melee unit.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Melee, 2, 7, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, false, hasGuard: true);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateLongbowScout()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/LongbowScout.asset");
            asset.ConfigureBaseForTests("starter_longbow_scout", "Longbow Scout", new ResourceSetData(0, 0, 0, 3));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Wind",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                "A basic ranged unit.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Ranged, 3, 4, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateGoldMiner()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/GoldMiner.asset");
            asset.ConfigureBaseForTests("starter_gold_miner", "Gold Miner", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Earth",
                CardAffiliation.Neutral,
                ChargeTileFootprint.OneByOne,
                "At turn start, gain Gold +1.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Melee, 1, 4, true, false, 0, new ResourceSetData(0, 0, 0, 1), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateManaAcolyte()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/ManaAcolyte.asset");
            asset.ConfigureBaseForTests("starter_mana_acolyte", "Mana Acolyte", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Light",
                CardAffiliation.Murim,
                ChargeTileFootprint.OneByOne,
                "At turn start, gain Mana +1.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Ranged, 1, 3, true, false, 0, new ResourceSetData(1, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BuildingCardDefinitionAsset CreatePowerTurret()
        {
            var asset = LoadOrCreate<BuildingCardDefinitionAsset>($"{CardsRoot}/PowerTurret.asset");
            asset.ConfigureBaseForTests("starter_power_turret", "Power Turret", new ResourceSetData(0, 0, 0, 4));
            asset.ConfigureMetadataForTests(
                CardRarity.Uncommon,
                "Lightning",
                CardAffiliation.ScienceCivilization,
                ChargeTileFootprint.OneByOne,
                "At turn start, gain Power +1.",
                "Can attack as a ranged building.");
            asset.ConfigureForTests(true, 2, 7, new ResourceSetData(0, 0, 1, 0), false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateRushRaider()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/RushRaider.asset");
            asset.ConfigureBaseForTests("starter_rush_raider", "Rush Raider", new ResourceSetData(0, 1, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Uncommon,
                "Fire",
                CardAffiliation.Murim,
                ChargeTileFootprint.OneByOne,
                "Can attack on the turn it is summoned.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Melee, 3, 3, true, false, 0, new ResourceSetData(0, 0, 0, 0), 1, true);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateTwinBladeAdept()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/TwinBladeAdept.asset");
            asset.ConfigureBaseForTests("starter_twin_blade_adept", "Twin Blade Adept", new ResourceSetData(1, 0, 0, 3));
            asset.ConfigureMetadataForTests(
                CardRarity.Rare,
                "Shadow",
                CardAffiliation.Murim,
                ChargeTileFootprint.OneByOne,
                "Can attack twice each turn.",
                string.Empty);
            asset.ConfigureForTests(AttackType.Melee, 2, 5, true, false, 0, new ResourceSetData(0, 0, 0, 0), 2, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateTeslaSentinel()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/TeslaSentinel.asset");
            asset.ConfigureBaseForTests("starter_tesla_sentinel", "Tesla Sentinel", new ResourceSetData(0, 0, 0, 4));
            asset.ConfigureMetadataForTests(
                CardRarity.Rare,
                "Lightning",
                CardAffiliation.ScienceCivilization,
                ChargeTileFootprint.OneByOne,
                "Requires Power upkeep at turn start.",
                "Becomes disabled if upkeep cannot be paid.");
            asset.ConfigureForTests(AttackType.Melee, 4, 6, true, true, 2, new ResourceSetData(0, 0, 0, 0), 1, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DamageSpellCardDefinitionAsset CreateFirebolt()
        {
            var asset = LoadOrCreate<DamageSpellCardDefinitionAsset>($"{CardsRoot}/Firebolt.asset");
            asset.ConfigureBaseForTests("starter_firebolt", "Firebolt", new ResourceSetData(1, 0, 0, 1));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Fire",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.None,
                "Deal 4 damage to one target occupant.",
                string.Empty);
            asset.ConfigureForTests(4);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static PersistentResourceSpellCardDefinitionAsset CreatePowerContract()
        {
            var asset = LoadOrCreate<PersistentResourceSpellCardDefinitionAsset>($"{CardsRoot}/PowerContract.asset");
            asset.ConfigureBaseForTests("starter_power_contract", "Power Contract", new ResourceSetData(0, 0, 0, 2));
            asset.ConfigureMetadataForTests(
                CardRarity.Uncommon,
                "Arcane",
                CardAffiliation.ScienceCivilization,
                ChargeTileFootprint.None,
                "At turn start, gain Power +1 while active.",
                "Persistent effect with explicit expiration text.");
            asset.ConfigureForTests(
                "starter_power_contract_effect",
                new ResourceSetData(0, 0, 1, 0),
                "Expires at the end of the caster's next turn.");
            EditorUtility.SetDirty(asset);
            return asset;
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

