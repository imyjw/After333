using System.Linq;
using System.IO;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class EditableSceneLayoutTests
    {
        [TestCase("DeckBuilding_VSlice")]
        [TestCase("Draft_VSlice")]
        [TestCase("GameStart_VSlice")]
        [TestCase("OwnedCards_VSlice")]
        [TestCase("Battle_VSlice")]
        public void SavedScene_ShowsCurrentUiWithoutStartingGame(string name)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/" + name + ".unity");
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                var transforms = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
                if (name == "GameStart_VSlice")
                {
                    Assert.That(transforms.Any(x => x.name == "TutorialButton"), Is.True, "Tutorial is part of the current start UI.");
                    Assert.That(transforms.Any(x => x.name == "GiftButton"), Is.True);
                    Assert.That(transforms.Single(x => x.name == "AccountLoginGateCanvas").gameObject.activeSelf, Is.False,
                        "Default authoring view shows the menu; runtime chooses login visibility.");
                }
                else if (name == "DeckBuilding_VSlice")
                {
                    var presenter = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DraftOverlayPresenter>(true)).Single();
                    Assert.That(presenter.GetComponentsInChildren<Text>(true).Count(x => x.name == "AttackValueText"), Is.EqualTo(3));
                    Assert.That(presenter.GetComponentsInChildren<Text>(true).Count(x => x.name == "HpValueText"), Is.EqualTo(3));
                    Assert.That(presenter.GetComponentsInChildren<Button>(true).Any(x => x.name == "BackToStartButton"), Is.True);
                }
                else if (name == "Draft_VSlice")
                {
                    Assert.That(transforms.Any(x => x.name == "RuntimeDeckPanelSurface"), Is.True);
                    var draft = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DraftSceneController>(true)).Single();
                    var serialized = new SerializedObject(draft);
                    Assert.That(serialized.FindProperty("_startPveBattleButton").objectReferenceValue, Is.Not.Null);
                    Assert.That(serialized.FindProperty("_startPvpBattleButton").objectReferenceValue, Is.Not.Null);
                    Assert.That(((Text)serialized.FindProperty("_startPveBattleButtonLabel").objectReferenceValue).text, Is.EqualTo("골드 던전"));
                    Assert.That(((Text)serialized.FindProperty("_startPvpBattleButtonLabel").objectReferenceValue).text, Is.EqualTo("전투"));
                    Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DraftPvpMatchmakingOverlayView>(true)).Count(), Is.EqualTo(1));
                }
                else if (name == "Battle_VSlice")
                {
                    foreach (var expected in new[] { "OpponentHandCanvas", "OpponentDeckCanvas", "PlayerDeckCanvas", "RuntimeHandCanvas", "BattleSceneBackgroundCanvas", "YourTurnBannerCanvas", "OnlineErrorToastCanvas", "OnlineTurnTimerCanvas", "RobotFusionSelectionCanvas" })
                        Assert.That(transforms.Count(x => x.name == expected), Is.EqualTo(1), expected);
                    var bootstrapper = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
                    Assert.That(bootstrapper.CurrentBattleState, Is.Null, "Authoring must not start a local or online battle.");
                    foreach (var hidden in new[] { "YourTurnBannerCanvas", "OnlineErrorToastCanvas", "OnlineTurnTimerCanvas", "RobotFusionSelectionCanvas" })
                    {
                        var item = transforms.Single(x => x.name == hidden);
                        Assert.That(!item.gameObject.activeInHierarchy || item.GetComponent<CanvasGroup>()?.alpha == 0f, Is.True, hidden);
                    }
                    var screen = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleScreenPresenter>(true)).Single();
                    Assert.That(screen.EditableUiVersion, Is.EqualTo(3));
                    Assert.That(transforms.Single(x => x.name == "TurnText").GetComponent<TMPro.TMP_Text>().text, Does.Contain("Turn — / —"));
                    Assert.That(transforms.Where(x => x.GetComponent<TileTextView>() != null).Select(x => new SerializedObject(x.GetComponent<TileTextView>())).All(x => x.FindProperty("_showTileText").boolValue || string.IsNullOrEmpty(((TMPro.TMP_Text)x.FindProperty("_text").objectReferenceValue).text)), Is.True);
                }
                else
                {
                    Assert.That(transforms.Count(x => x.name.StartsWith("CardSlot_")), Is.EqualTo(12));
                    Assert.That(transforms.Any(x => x.name == "CardDetailModal"), Is.True);
                    Assert.That(transforms.Any(x => x.name == "UpgradeTableBack"), Is.True);
                    Assert.That(transforms.Any(x => x.name == "ArtworkFlipHitArea"), Is.True);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("DeckBuilding_VSlice")]
        [TestCase("Draft_VSlice")]
        [TestCase("GameStart_VSlice")]
        [TestCase("OwnedCards_VSlice")]
        public void PreparedScene_HasWalletAndNoAccountMutation(string name)
        {
            var account = AccountSessionState.AccountId;
            var token = AccountSessionState.SessionToken;
            var gold = AccountSessionState.ResourceGold;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/" + name + ".unity");
            try
            {
                var wallet = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AccountWalletView>(true)).FirstOrDefault();
                Assert.That(wallet, Is.Not.Null);
                Assert.That(wallet.GetComponentsInChildren<Image>(true).Any(x => x.name == "ServerTicketIcon"), Is.True);
                Assert.That(wallet.GetComponentsInChildren<Image>(true).Any(x => x.name == "ServerGoldIcon"), Is.True);
                Assert.That(AccountSessionState.AccountId, Is.EqualTo(account));
                Assert.That(AccountSessionState.SessionToken, Is.EqualTo(token));
                Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(gold));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("DeckBuilding_VSlice")]
        [TestCase("Draft_VSlice")]
        [TestCase("GameStart_VSlice")]
        [TestCase("OwnedCards_VSlice")]
        [TestCase("Battle_VSlice")]
        public void Upgrade_PreservesUnsavedCustomizationAndButtonComponents(string name)
        {
            var path = "Assets/" + name + ".unity";
            var saved = File.ReadAllBytes(path);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var owner = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                    .Single(x => x is GameStartSceneController || x is DraftSceneController || x is OwnedCardsSceneController || x is BattleScreenPresenter);
                var button = roots.SelectMany(x => x.GetComponentsInChildren<Button>(true)).First();
                var sound = button.GetComponent<ButtonTouchSound>();
                if (sound == null) sound = button.gameObject.AddComponent<ButtonTouchSound>();
                var extra = new GameObject("UserCustomDecoration", typeof(RectTransform), typeof(Image));
                extra.transform.SetParent(owner.transform, false);
                var position = new Vector2(91, -47);
                ((RectTransform)extra.transform).anchoredPosition = position;
                var sprite = Resources.Load<Sprite>(AccountWalletView.TicketResourcePath);
                extra.GetComponent<Image>().sprite = sprite;
                var serialized = new SerializedObject(owner);
                serialized.FindProperty("_editableUiVersion").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);

                Project333.Editor.Project333EditableScenesBuilder.UpdateScene(path);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previous));
                Assert.That(extra, Is.Not.Null);
                Assert.That(((RectTransform)extra.transform).anchoredPosition, Is.EqualTo(position));
                Assert.That(extra.GetComponent<Image>().sprite, Is.SameAs(sprite));
                Assert.That(button.GetComponent<ButtonTouchSound>(), Is.SameAs(sound));
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
                var count = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Count();
                Project333.Editor.Project333EditableScenesBuilder.UpdateScene(path);
                Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Count(), Is.EqualTo(count));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void BattleMigration_RebuildsMissingPresentationWithoutTouchingBattleOrOtherScenes()
        {
            const string path = "Assets/Battle_VSlice.unity";
            var saved = File.ReadAllBytes(path);
            var previous = SceneManager.GetActiveScene();
            var foreignRoots = previous.GetRootGameObjects().Select(x => x.GetInstanceID()).ToArray();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var screen = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleScreenPresenter>(true)).Single();
                var bootstrapper = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
                var serialized = new SerializedObject(screen);
                var background = (Image)serialized.FindProperty("_backgroundImage").objectReferenceValue;
                Object.DestroyImmediate(background.GetComponentInParent<Canvas>().gameObject);
                serialized.Update();
                serialized.FindProperty("_backgroundImage").objectReferenceValue = null;
                serialized.FindProperty("_runtimeBackgroundCanvas").objectReferenceValue = null;
                serialized.FindProperty("_editableUiVersion").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Project333.Editor.Project333EditableScenesBuilder.UpdateScene(path);
                serialized.Update();
                var rebuilt = (Image)serialized.FindProperty("_backgroundImage").objectReferenceValue;
                Assert.That(rebuilt, Is.Not.Null);
                Assert.That(rebuilt.sprite, Is.Not.Null);
                Assert.That(rebuilt.gameObject.scene, Is.EqualTo(scene));
                Assert.That(bootstrapper.CurrentBattleState, Is.Null);
                CollectionAssert.AreEqual(foreignRoots, previous.GetRootGameObjects().Select(x => x.GetInstanceID()).ToArray());
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void AlreadyOpenCleanScene_IsPreparedButNeverSavedOverDisk()
        {
            const string path = "Assets/Draft_VSlice.unity";
            var original = File.ReadAllBytes(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DraftSceneController>(true)).Single();
                var data = new SerializedObject(owner);
                data.FindProperty("_editableUiVersion").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
                Assert.That(scene.isDirty, Is.False);
                var beforeUpgrade = File.ReadAllBytes(path);
                Project333.Editor.Project333EditableScenesBuilder.UpdateScene(path);
                Assert.That(scene.isDirty, Is.True);
                CollectionAssert.AreEqual(beforeUpgrade, File.ReadAllBytes(path));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                File.WriteAllBytes(path, original);
            }
        }

        [Test]
        public void NewWalletCanvas_IsSavedInTargetSceneWithoutLeakingIntoTheActiveScene()
        {
            const string path = "Assets/Draft_VSlice.unity";
            var saved = File.ReadAllBytes(path);
            var previous = SceneManager.GetActiveScene();
            var foreignRoots = previous.GetRootGameObjects().Select(x => x.GetInstanceID()).ToArray();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var draft = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DraftSceneController>(true)).Single();
                var serialized = new SerializedObject(draft);
                var wallet = (Text)serialized.FindProperty("_serverWalletText").objectReferenceValue;
                Object.DestroyImmediate(wallet.GetComponentInParent<Canvas>().gameObject);
                serialized.Update();
                serialized.FindProperty("_serverWalletText").objectReferenceValue = null;
                serialized.FindProperty("_editableUiVersion").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Project333.Editor.Project333EditableScenesBuilder.UpdateScene(path);
                serialized.Update();
                var rebuilt = (Text)serialized.FindProperty("_serverWalletText").objectReferenceValue;
                Assert.That(rebuilt, Is.Not.Null);
                Assert.That(rebuilt.gameObject.scene, Is.EqualTo(scene));
                CollectionAssert.AreEqual(foreignRoots, previous.GetRootGameObjects().Select(x => x.GetInstanceID()).ToArray());
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
