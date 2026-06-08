using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333DraftPoolExpansionGenerator
    {
        private const string ScriptableObjectsRoot = "Assets/Project333/ScriptableObjects";
        private const string StarterRoot = ScriptableObjectsRoot + "/StarterTen";
        private const string CardsRoot = StarterRoot + "/Cards";

        [MenuItem("Tools/Project333/Generate Draft Pool Expansion")]
        public static void Generate()
        {
            EnsureFolderExists("Assets", "Project333");
            EnsureFolderExists("Assets/Project333", "ScriptableObjects");
            EnsureFolderExists(ScriptableObjectsRoot, "StarterTen");
            EnsureFolderExists(StarterRoot, "Cards");

            CreateBlueDragon();
            CreateRedDragon();
            CreateGoblin();
            CreateVampire();
            CreateCheonraJimang();
            CreateDaehwandan();

            var cards = LoadExistingStarterCards();
            var catalog = LoadOrCreate<CardDefinitionCatalogAsset>($"{StarterRoot}/StarterTenCardCatalog.asset");
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;
            Debug.Log(
                $"Generated or updated draft pool expansion assets. StarterTen catalog now contains {cards.Count} cards.");
        }

        private static UnitCardDefinitionAsset CreateBlueDragon()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/BlueDragon.asset");
            asset.ConfigureBaseForTests("BlueDragon", "블루 드래곤", new ResourceSetData(8, 0, 0, 0));
            asset.ConfigureMetadataForTests(
                CardRarity.Legendary,
                "Aqua",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                "내 턴 종료 시 내 타일들 위의 유닛들의 체력회복+33",
                string.Empty);
            asset.ConfigureForTests(
                attackType: AttackType.Ranged,
                attack: 40,
                health: 100,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateRedDragon()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/RedDragon.asset");
            asset.ConfigureBaseForTests("RedDragon", "레드 드래곤", new ResourceSetData(9, 0, 0, 0));
            asset.ConfigureMetadataForTests(
                CardRarity.Legendary,
                "Ignis",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                "내 턴 종료 시 상대방 타일 전체에 66데미지",
                string.Empty);
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 66,
                health: 66,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateGoblin()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/Goblin.asset");
            asset.ConfigureBaseForTests("Goblin", "고블린", new ResourceSetData(1, 0, 0, 0));
            asset.ConfigureMetadataForTests(
                CardRarity.Common,
                "Neutral",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                string.Empty,
                string.Empty);
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 10,
                health: 10,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static UnitCardDefinitionAsset CreateVampire()
        {
            var asset = LoadOrCreate<UnitCardDefinitionAsset>($"{CardsRoot}/Vampire.asset");
            asset.ConfigureBaseForTests("Vampire", "뱀파이어", new ResourceSetData(4, 0, 0, 0));
            asset.ConfigureMetadataForTests(
                CardRarity.Uncommon,
                "Blood",
                CardAffiliation.Fantasy,
                ChargeTileFootprint.OneByOne,
                "흡혈(공격 시 입힌 피해만큼 체력 회복)",
                string.Empty);
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 20,
                health: 50,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ScriptedSpellCardDefinitionAsset CreateCheonraJimang()
        {
            var asset = LoadOrCreate<ScriptedSpellCardDefinitionAsset>($"{CardsRoot}/CheonraJimang.asset");
            asset.ConfigureBaseForTests("CheonraJimang", "천라지망", new ResourceSetData(0, 5, 0, 0));
            asset.ConfigureMetadataForTests(
                CardRarity.Rare,
                "Neutral",
                CardAffiliation.Murim,
                ChargeTileFootprint.None,
                "유닛을 하나 선택하고, 내 다음턴 시작 시 그 유닛 파괴",
                string.Empty);
            asset.ConfigureForTests("cheonra_jimang");
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ScriptedSpellCardDefinitionAsset CreateDaehwandan()
        {
            var asset = LoadOrCreate<ScriptedSpellCardDefinitionAsset>($"{CardsRoot}/Daehwandan.asset");
            asset.ConfigureBaseForTests("Daehwandan", "대환단", new ResourceSetData(0, 0, 0, 3));
            asset.ConfigureMetadataForTests(
                CardRarity.Legendary,
                "Terra",
                CardAffiliation.Murim,
                ChargeTileFootprint.None,
                "사용 시 내 마스터의 공격력 +30, 다음 3번의 자기 턴 시작마다 기 +3",
                string.Empty);
            asset.ConfigureForTests("daehwandan");
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static List<CardDefinitionAsset> LoadExistingStarterCards()
        {
            var guids = AssetDatabase.FindAssets("t:CardDefinitionAsset", new[] { CardsRoot });
            var cards = new List<CardDefinitionAsset>(guids.Length);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
                if (card == null)
                {
                    continue;
                }

                cards.Add(card);
            }

            cards.Sort((left, right) =>
            {
                var rarityComparison = left.Rarity.CompareTo(right.Rarity);
                if (rarityComparison != 0)
                {
                    return rarityComparison;
                }

                var displayNameComparison = string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
                if (displayNameComparison != 0)
                {
                    return displayNameComparison;
                }

                return string.Compare(left.CardId, right.CardId, System.StringComparison.Ordinal);
            });

            return cards;
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
