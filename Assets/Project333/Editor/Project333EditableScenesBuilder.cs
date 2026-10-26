using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.HUD;
using TMPro;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    // Author UI once; never execute account/ad/run lifecycle methods in Edit mode.
    public static class Project333EditableScenesBuilder
    {
        private const int CurrentVersion = 3;
        private const string VersionField = "_editableUiVersion";
        private static readonly string[] Paths =
        {
            "Assets/DeckBuilding_VSlice.unity", "Assets/Draft_VSlice.unity",
            "Assets/GameStart_VSlice.unity", "Assets/OwnedCards_VSlice.unity",
            "Assets/Battle_VSlice.unity"
        };
        private static bool _updating;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += UpdateAll;
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                if (Paths.Contains(scene.path)) EditorApplication.delayCall += UpdateAll;
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += UpdateAll;
            };
        }

        [MenuItem("Tools/Project333/Scenes/Update Current Editable Layouts")]
        public static void UpdateAll()
        {
            if (_updating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _updating = true;
            try
            {
                foreach (var path in Paths)
                    if (File.Exists(path)) UpdateScene(path);
            }
            finally { _updating = false; }
        }

        public static void UpdateScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Paths.Contains(path)) return;
            var scene = SceneManager.GetSceneByPath(path);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter)
            {
                var marker = Regex.Match(File.ReadAllText(path), @"(?m)^\s*_editableUiVersion:\s*(\d+)\s*$");
                if (marker.Success && int.TryParse(marker.Groups[1].Value, out var version) && version >= CurrentVersion) return;
            }
            var previousActive = SceneManager.GetActiveScene();
            if (closeAfter) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); // Root-level generated canvases belong to this scene.
                var owner = FindOwner(scene);
                if (owner == null) throw new InvalidOperationException("UI controller was not found: " + path);
                var serialized = new SerializedObject(owner);
                var marker = serialized.FindProperty(VersionField);
                if (marker == null) throw new InvalidOperationException("Editable UI version field was not found: " + path);
                if (marker.intValue >= CurrentVersion) return;

                GlobalSettingsMenuController.EnsureForScene(scene);
                if (owner is GameStartSceneController start) start.MaterializeStartUiForEditor();
                else if (owner is OwnedCardsSceneController owned)
                {
                    owned.MaterializeOwnedCardsUiForEditor();
                    // Empty editable slots, not a fabricated collection or account balance.
                    serialized.Update();
                    var slotColor = serialized.FindProperty("_gridSlotColor").colorValue;
                    var artColor = serialized.FindProperty("_gridSlotEmptyArtworkColor").colorValue;
                    foreach (var image in owned.GetComponentsInChildren<Image>(true))
                    {
                        if (image.sprite != null) continue;
                        if (image.name.StartsWith("CardSlot_")) image.color = slotColor;
                        else if (image.name == "Artwork" && image.transform.parent.name.StartsWith("CardSlot_")) image.color = artColor;
                    }
                }
                else if (owner is BattleScreenPresenter battle)
                {
                    battle.MaterializePersistentBattleUiForEditor();
                    Find<HandPresenter>(scene)?.MaterializePersistentHandUiForEditor();
                    Find<BattleBootstrapper>(scene)?.MaterializePersistentBattleUiForEditor();
                    Find<BattlePlayerInputController>(scene)?.MaterializeRobotFusionSelectionUiForEditor();
                    var resources = Find<ResourceBarTextView>(scene);
                    resources?.EnsureEditableResourceIconRows();
                    resources?.SetResourceSummary("Turn — / —", "M:0 Q:0 P:0 G:0", "M:0 Q:0 P:0 G:0");
                    Canvas.ForceUpdateCanvases();
                    battle.BoardPresenter?.ApplyResponsiveLayoutNow();
                    // Old authored debug labels are replaced by real data during Play.
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var view in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!(view is TileTextView || view is HandCardTextView || view is BattleSummaryTextView || view is CombatLogTextView || view is BattleOutcomeTextView)) continue;
                        var data = new SerializedObject(view);
                        if (view is TileTextView && data.FindProperty("_showTileText").boolValue) continue;
                        if (data.FindProperty("_text")?.objectReferenceValue is TMP_Text text) text.text = string.Empty;
                    }
                }
                else if (owner is DraftSceneController draft)
                {
                    draft.MaterializeDraftUiForEditor();
                    if (scene.name == "Draft_VSlice")
                    {
                        // Neutral authoring labels; Play supplies the run's real availability/counts.
                        serialized.Update();
                        if (serialized.FindProperty("_startPveBattleButtonLabel").objectReferenceValue is Text pve) pve.text = "골드 던전";
                        if (serialized.FindProperty("_startPvpBattleButtonLabel").objectReferenceValue is Text pvp) pvp.text = "전투";
                    }
                    if (path == Paths[0])
                    {
                        var presenter = Find<DraftOverlayPresenter>(scene);
                        if (presenter == null) throw new InvalidOperationException("Deck-building presenter was not found.");
                        Canvas.ForceUpdateCanvases();
                        presenter.RefreshEditableSceneUi();
                    }
                }

                // Only placeholder wallet visuals, never account refresh or persisted run data.
                serialized.Update();
                var wallet = serialized.FindProperty(owner is GameStartSceneController ? "_ticketText" :
                    owner is OwnedCardsSceneController ? "_accountText" : "_serverWalletText")?.objectReferenceValue as Text;
                if (wallet != null) AccountWalletView.Attach(wallet, owner is DraftSceneController, false)?.Render(false, 0, 0);
                serialized.FindProperty(VersionField).intValue = CurrentVersion;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(owner);
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter) EditorSceneManager.SaveScene(scene);
                Debug.Log("After333 current UI prepared before Play: " + path + (!closeAfter ? " (save pending)" : ""));
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Component FindOwner(Scene scene) => scene.name == "GameStart_VSlice"
            ? (Component)Find<GameStartSceneController>(scene) : scene.name == "OwnedCards_VSlice"
                ? Find<OwnedCardsSceneController>(scene) : scene.name == "Battle_VSlice"
                    ? (Component)Find<BattleScreenPresenter>(scene) : Find<DraftSceneController>(scene);

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).SingleOrDefault();
    }
}
