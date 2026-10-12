using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    // Preview scenes preserve every open scene, including unsaved user edits. No login or request method runs.
    public sealed class OwnedCardsPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene _scene;
        private OwnedCardsSceneController _controller;
        private long _originalGold;

        [SetUp]
        public void OpenIsolatedSceneCopy()
        {
            _originalGold = AccountSessionState.ResourceGold;
            SetGold(100000);
            _scene = EditorSceneManager.OpenPreviewScene("Assets/OwnedCards_VSlice.unity");
            _controller = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OwnedCardsSceneController>(true)).Single();
            _controller.enabled = false;
            _controller.MaterializeOwnedCardsUiForEditor();
            Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = 100, UpgradeLevel = 1 });
        }

        [TearDown]
        public void CloseOnlyThePreviewScene()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            SetGold(_originalGold);
        }

        [Test]
        public void InitialCollection_IsTwelveCardsWideAndDoesNotOpenDetail()
        {
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            Assert.That(Field<RectTransform>("_detailPanelRoot").gameObject.activeInHierarchy, Is.False);
            var grid = Field<RectTransform>("_cardGridRoot");
            Assert.That(grid.childCount, Is.EqualTo(12));
            Assert.That(grid.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(6));
            var panel = (RectTransform)grid.parent;
            Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null, "Modal must have a gallery interaction group.");
            Assert.That(panel.anchorMax.x - panel.anchorMin.x, Is.GreaterThan(0.90f), "Collection must reclaim the former permanent detail column.");
            Assert.That(grid.GetComponentsInChildren<Button>().Count(button => button.interactable), Is.EqualTo(12));
        }

        [Test]
        public void ActualGridClick_OpensCardAndCloseReopensTheSamePage()
        {
            _controller.ShowNextPageFromUi();
            var page = Field<int>("_currentPageIndex");
            Field<RectTransform>("_cardGridRoot").GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(_controller.IsCardDetailOpen, Is.True);
            var selected = Field<string>("_selectedCardId");
            Assert.That(Field<Text>("_detailTitleText").text, Is.Not.Empty);
            Assert.That(Field<Image>("_detailArtworkImage").sprite, Is.Not.Null);
            _controller.CloseCardDetailFromUi();
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            Assert.That(Field<int>("_currentPageIndex"), Is.EqualTo(page));
            Field<RectTransform>("_cardGridRoot").GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(_controller.IsCardDetailOpen, Is.True);
            Assert.That(Field<string>("_selectedCardId"), Is.EqualTo(selected));
        }

        [Test]
        public void CloseDuringUpgrade_DoesNotCancelRequestAndResponseStaysHidden()
        {
            _controller.SelectCardFromGrid("Cerberus");
            var cancellation = new CancellationTokenSource();
            Set("_upgradeCancellation", cancellation);
            Set("_isUpgradingCard", true);
            _controller.CloseCardDetailFromUi();
            Assert.That(cancellation.IsCancellationRequested, Is.False);
            Assert.That(Field<CancellationTokenSource>("_upgradeCancellation"), Is.SameAs(cancellation));
            Assert.That(Field<bool>("_isUpgradingCard"), Is.True);
            Invoke("ApplyUpgradeResponseToOwnedCards", new UpgradeCardResponse
            {
                UpgradedCard = new OwnedCardDto { CardId = "Cerberus", CopyCount = 95, UpgradeLevel = 2 }
            });
            Assert.That(_controller.IsCardDetailOpen, Is.False, "Server completion must not unexpectedly reopen a dismissed modal.");
            _controller.SelectCardFromGrid("Cerberus");
            Assert.That(Field<Text>("_detailOwnershipText").text, Does.Contain("Lv.2"));
            Assert.That(Field<Text>("_detailOwnershipText").text, Does.Contain("95"));
        }

        [Test]
        public void PageBounds_KeepTwelveSlotsAndDisableOnlyUnusedLastPageCards()
        {
            var count = ((ICollection)typeof(OwnedCardsSceneController).GetField("_cardGridEntries", Hidden).GetValue(_controller)).Count;
            var pages = Math.Max(1, (count + 11) / 12);
            for (var i = 0; i < pages + 3; i++) _controller.ShowNextPageFromUi();
            Assert.That(Field<int>("_currentPageIndex"), Is.EqualTo(pages - 1));
            Assert.That(Field<Button>("_nextPageButton").interactable, Is.False);
            var expected = count - (pages - 1) * 12;
            var grid = Field<RectTransform>("_cardGridRoot");
            Assert.That(grid.childCount, Is.EqualTo(12));
            Assert.That(grid.GetComponentsInChildren<Button>().Count(button => button.interactable), Is.EqualTo(expected));
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            for (var i = 0; i < pages + 3; i++) _controller.ShowPreviousPageFromUi();
            Assert.That(Field<int>("_currentPageIndex"), Is.Zero);
            Assert.That(Field<Button>("_previousPageButton").interactable, Is.False);
        }

        [Test]
        public void MaterializeTwice_PreservesSerializedCardControlsAndOneModal()
        {
            var detail = Field<RectTransform>("_detailPanelRoot");
            var upgrade = Field<Button>("_upgradeButton");
            var artwork = Field<Image>("_detailArtworkImage");
            _controller.MaterializeOwnedCardsUiForEditor();
            _controller.MaterializeOwnedCardsUiForEditor();
            Assert.That(Field<RectTransform>("_detailPanelRoot"), Is.SameAs(detail));
            Assert.That(Field<Button>("_upgradeButton"), Is.SameAs(upgrade));
            Assert.That(Field<Image>("_detailArtworkImage"), Is.SameAs(artwork));
            Assert.That(_controller.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "CardDetailModal"), Is.EqualTo(1));
        }

        [Test]
        public void CollectionRefresh_DoesNotJumpTheOpenDetailBackToTop()
        {
            _controller.SelectCardFromGrid("Cerberus");
            var scroll = Field<ScrollRect>("_detailScroll");
            scroll.content.anchoredPosition = new Vector2(0f, 100f);
            Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = 99, UpgradeLevel = 1 });
            Assert.That(_controller.IsCardDetailOpen, Is.True);
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(100f).Within(.01f), "A server collection refresh must preserve the current reading position.");
        }
        [Test]
        public void SceneBackHandler_ClosesDetailWithoutBeginningSceneNavigation()
        {
            var menu = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GlobalSettingsMenuController>(true)).Single();
            typeof(GlobalSettingsMenuController).GetField("_sceneName", Hidden).SetValue(menu, "OwnedCards_VSlice");
            _controller.SelectCardFromGrid("Cerberus");
            Assert.That(_controller.IsCardDetailOpen, Is.True);
            typeof(GlobalSettingsMenuController).GetMethod("HandleSceneBackNavigation", Hidden).Invoke(menu, null);
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            Assert.That((bool)typeof(GlobalSettingsMenuController).GetField("_isHandlingSceneBackNavigation", Hidden).GetValue(menu), Is.False);
            Assert.That(_scene.IsValid() && _scene.isLoaded, Is.True);
        }
        [TestCase("Cerberus", 0, 0)]
        [TestCase("Cerberus", 100, 13)]
        [TestCase("Microreactor", 100, 0)]
        public void DetailUpgradeButton_RetainsUnavailableStates(string cardId, int copies, int level)
        {
            Present(new OwnedCardDto { CardId = cardId, CopyCount = copies, UpgradeLevel = level });
            _controller.SelectCardFromGrid(cardId);
            Assert.That(_controller.IsCardDetailOpen, Is.True);
            Assert.That(Field<Button>("_upgradeButton").interactable, Is.False);
            Assert.That(Field<Text>("_upgradeButtonLabel").text, Is.Not.Empty);
        }

        [TestCase("Golem", "물리/마법 방어력: 3 / 0")]
        [TestCase("BlueDragon", "마나 9")]
        [TestCase("RedDragon", "마나 10")]
        [TestCase("Gwangma", "ATK/HP: 30 / 60")]
        [TestCase("Werewolf", "ATK/HP: 10 / 30")]
        [TestCase("ElfLongbowScout", "종족: 엘프")]
        public void CurrentDetail_UsesBattleDatabase(string cardId, string expected)
        {
            var asset = (CardDefinitionAsset)typeof(OwnedCardsSceneController).GetMethod("ResolveCardAsset", Hidden).Invoke(_controller, new object[] { cardId });
            Assert.That(asset, Is.Not.Null);
            var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden).Invoke(_controller, new object[] { asset, 0 });
            Assert.That(text, Does.Contain(expected));
        }

        [Test]
        public void CurrentDetail_IgnoresStaleAssetStatsAndCost()
        {
            var original = (CardDefinitionAsset)typeof(OwnedCardsSceneController).GetMethod("ResolveCardAsset", Hidden).Invoke(_controller, new object[] { "Golem" });
            var stale = UnityEngine.Object.Instantiate(original);
            try
            {
                var serialized = new UnityEditor.SerializedObject(stale);
                serialized.FindProperty("_physicalDefense").intValue = 99;
                serialized.FindProperty("_attack").intValue = 999;
                serialized.FindProperty("_cost").FindPropertyRelative("_mana").intValue = 99;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden).Invoke(_controller, new object[] { stale, 0 });
                Assert.That(text, Does.Contain("물리/마법 방어력: 3 / 0"));
                Assert.That(text, Does.Contain("마나 2"));
                Assert.That(text, Does.Not.Contain("999"));
            }
            finally { UnityEngine.Object.DestroyImmediate(stale); }
        }

        [TestCase("firebolt")]
        [TestCase("TimedBomb")]
        [TestCase("BiochemicalBomb")]
        public void CurrentDetail_ShowsUpgradedSpellDamage(string cardId)
        {
            var asset = (CardDefinitionAsset)typeof(OwnedCardsSceneController).GetMethod("ResolveCardAsset", Hidden).Invoke(_controller, new object[] { cardId });
            Assert.That(asset, Is.Not.Null);
            var record = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text).Cards.Single(c => c.Id == cardId);
            var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden).Invoke(_controller, new object[] { asset, 13 });
            Assert.That(text, Does.Contain("피해량: " + (record.Damage + 13)));
            Assert.That(text, Does.Contain("효과 (기본 수치)"));
            Assert.That(text, Does.Not.Contain("종족:"));
        }

        [Test]
        public void NuclearDetail_UsesCurrentJsonAndLeveledImmediateDamageWithoutCreatingAnAssetFile()
        {
            var asset = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            try
            {
                asset.ConfigureBaseForTests("Nuclear", "뉴클리어", new ResourceSetData(0, 0, 1, 0));
                asset.ConfigureForTests("nuclear", damage: 999, damageType: Project333.Runtime.Domain.Cards.DamageType.Magic);
                foreach (var level in new[] { 0, 1, 13 })
                {
                    var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden)
                        .Invoke(_controller, new object[] { asset, level });
                    Assert.That(text, Does.Contain("피해량: " + (100 + level)));
                    Assert.That(text, Does.Contain("고정 데미지 " + (100 + level) + "을 입힌다."));
                    Assert.That(text, Does.Contain("사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩.").And.Contain("중첩당 고정 피해 10."));
                    Assert.That(text, Does.Contain("전력 13"));
                    Assert.That(text, Does.Not.Contain("999").And.Not.Contain("효과 (기본 수치)"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void EarthquakeDetail_UsesCurrentJsonAndUpgradedDamage_WithoutInventingUnitRace()
        {
            var asset = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            try
            {
                asset.ConfigureBaseForTests("Earthquake", "지진", new ResourceSetData(0, 0, 10, 0));
                asset.ConfigureForTests("earthquake", damage: 999, damageType: Project333.Runtime.Domain.Cards.DamageType.Magic);
                foreach (var level in new[] { 0, 1, 13 })
                {
                    var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden)
                        .Invoke(_controller, new object[] { asset, level });
                    Assert.That(text, Does.Contain("피해량: " + (10 + level)));
                    Assert.That(text, Does.Contain("물리 데미지 " + (10 + level) + "을 입힌다.").And.Contain("건물에는 3배"));
                    Assert.That(text, Does.Contain("골드 3").And.Contain("언커먼").And.Contain("중립"));
                    Assert.That(text, Does.Not.Contain("999").And.Not.Contain("효과 (기본 수치)").And.Not.Contain("종족:"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void MeteorDetail_UsesCurrentJsonAndEveryUpgradeLevelWithoutUnitRace()
        {
            var asset = ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
            try
            {
                asset.ConfigureBaseForTests("Meteor", "메테오", new ResourceSetData(0, 0, 10, 0));
                asset.ConfigureForTests("meteor", damage: 999, damageType: Project333.Runtime.Domain.Cards.DamageType.Physical);
                for (var level = 0; level <= 13; level++)
                {
                    var text = (string)typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden)
                        .Invoke(_controller, new object[] { asset, level });
                    Assert.That(text, Does.Contain("피해량: " + (40 + level)));
                    Assert.That(text, Does.Contain("마법 데미지 " + (40 + level) + "을 입힌다."));
                    Assert.That(text, Does.Contain("마나 7").And.Contain("유니크").And.Contain("판타지"));
                    Assert.That(text, Does.Not.Contain("999").And.Not.Contain("효과 (기본 수치)").And.Not.Contain("종족:"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void CurrentDetail_AllCatalogCardsMatchBattleStatsAtEveryLevel()
        {
            var records = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text).Cards;
            var method = typeof(OwnedCardsSceneController).GetMethod("BuildDetailMetaText", Hidden);
            var badge = typeof(OwnedCardsSceneController).GetMethod("TryBuildDetailStatDisplay", BindingFlags.Static | BindingFlags.NonPublic);
            var catalog = Field<CardDefinitionCatalogAsset>("_cardCatalogAsset");
            var checkedCards = 0;
            foreach (var asset in catalog.Cards.Where(c => c != null))
            {
                var record = records.SingleOrDefault(c => c.Id == asset.CardId);
                if (record == null) continue;
                checkedCards++;
                for (var level = 0; level <= 13; level++)
                {
                    var text = (string)method.Invoke(_controller, new object[] { asset, level });
                    Assert.That(text, Does.Not.Contain("변환 실패"), asset.CardId);
                    if (Project333.Runtime.Application.Services.CardStatDisplay.TryCreate(record.ToDefinition(), level, out var expected))
                    {
                        var arguments = new object[] { asset, level, null };
                        Assert.That((bool)badge.Invoke(null, arguments), Is.True, asset.CardId);
                        var actual = (Project333.Runtime.Application.Services.CardStatDisplay)arguments[2];
                        Assert.That(actual.Attack, Is.EqualTo(expected.Attack), asset.CardId + " attack/damage");
                        Assert.That(actual.Hp, Is.EqualTo(expected.Hp), asset.CardId + " HP");
                    }
                    if (record.DefinitionType == JsonCardDefinitionKind.Unit || record.DefinitionType == JsonCardDefinitionKind.Building)
                        Assert.That(text, Does.Contain($"물리/마법 방어력: {record.PhysicalDefense} / {record.MagicDefense}"), asset.CardId);
                    if (record.DefinitionType == JsonCardDefinitionKind.Unit)
                        Assert.That(text.IndexOf("종족:", StringComparison.Ordinal), Is.LessThan(text.IndexOf("소속:", StringComparison.Ordinal)), asset.CardId);
                    else Assert.That(text, Does.Not.Contain("종족:"), asset.CardId);
                }
            }
            Assert.That(checkedCards, Is.GreaterThanOrEqualTo(36));
        }

        [Test]
        public void UpgradeCostsAndButtonAvailabilityUseNewScheduleAtEveryTargetLevel()
        {
            var amounts = new[] { 3, 6, 10, 20, 30, 33, 33, 33, 33, 33, 33, 33, 33 };
            for (var level = 0; level < amounts.Length; level++)
            {
                var amount = amounts[level];
                SetGold(amount);
                Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = amount, UpgradeLevel = level });
                _controller.SelectCardFromGrid("Cerberus");
                var text = Field<Text>("_detailOwnershipText").text;
                Assert.That(text, Does.Contain("다음 강화: Lv." + (level + 1)));
                Assert.That(text, Does.Contain($"필요 카드: {amount}/{amount}").And.Contain($"필요 골드: {amount}/{amount}"));
                Assert.That(Field<Button>("_upgradeButton").interactable, Is.True, "Exact materials at Lv." + (level + 1));
                SetGold(amount - 1);
                Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = amount, UpgradeLevel = level });
                Assert.That(Field<Button>("_upgradeButton").interactable, Is.False, "One gold short at Lv." + (level + 1));
                SetGold(amount);
                Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = amount - 1, UpgradeLevel = level });
                Assert.That(Field<Button>("_upgradeButton").interactable, Is.False, "One card short at Lv." + (level + 1));
            }
            SetGold(1000);
            Present(new OwnedCardDto { CardId = "Cerberus", CopyCount = 1000, UpgradeLevel = 13 });
            Assert.That(Field<Text>("_detailOwnershipText").text, Does.Contain("최대 레벨"));
            Assert.That(Field<Button>("_upgradeButton").interactable, Is.False);
        }
        private void Present(params OwnedCardDto[] cards) => Invoke("PresentOwnedCards", new MeResponse
        { CollectionSummary = new CollectionSummaryDto { OwnedCards = cards } });
        private T Field<T>(string name) => (T)typeof(OwnedCardsSceneController).GetField(name, Hidden).GetValue(_controller);
        private void Set(string name, object value) => typeof(OwnedCardsSceneController).GetField(name, Hidden).SetValue(_controller, value);
        private void Invoke(string name, object value) => typeof(OwnedCardsSceneController).GetMethod(name, Hidden).Invoke(_controller, new[] { value });
        private static void SetGold(long value) => typeof(AccountSessionState).GetField("<ResourceGold>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
}
