using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private Scene _practiceScene;
        private string _requestedPracticeSceneName;
        private Coroutine _practiceSceneRoutine;
        private bool _ownsPracticeScene, _loadingPracticeScene;
        private DraftOverlayPresenter _practiceDraft;
        private DraftSceneController _practiceLobby;
        private BattleBootstrapper _practiceBattle;
        private Image _practiceInputBlocker;
        public string PracticeSceneName => _practiceScene.IsValid() ? _practiceScene.name : string.Empty;
        public bool IsPracticeSceneReady => !_loadingPracticeScene && _practiceScene.IsValid() &&
            _practiceScene.isLoaded && string.Equals(_practiceScene.name, DesiredPracticeSceneName,
                System.StringComparison.Ordinal);
        private string DesiredPracticeSceneName => _session.Chapter == 6 ? (IsConclusionLesson ? ReturnSceneName : IsShopLesson ? "Shop_VSlice" : "OwnedCards_VSlice") : _session.PracticeBattleStarted ? "Battle_VSlice" :
            _session.IsDraftReady ? "Draft_VSlice" :
            _session.Chapter == 2 ? "DeckBuilding_VSlice" : ReturnSceneName;

        private void RequestPracticeScene()
        {
            _requestedPracticeSceneName = DesiredPracticeSceneName;
            if (_combatActionHint != null && _session.Chapter != 5) _combatActionHint.gameObject.SetActive(false);
            if (_practiceResourceNote != null && _session.Chapter != 4) _practiceResourceNote.gameObject.SetActive(false);
            RefreshTutorialControlsLayout();
            HidePracticeHighlights();
            _practiceInputBlocker.gameObject.SetActive(_session.PracticeBattleStarted || _session.Chapter == 6);
            if (IsPracticeSceneReady) { PresentRealScene(); RefreshPracticeAdvanceButton(); return; }
            CancelPracticePreviews();
            RefreshPracticeAdvanceButton();
            if (UnityEngine.Application.isPlaying && _practiceSceneRoutine == null)
                _practiceSceneRoutine = StartCoroutine(LoadPracticeScenes());
        }
        private IEnumerator LoadPracticeScenes()
        {
            _loadingPracticeScene = true;
            RefreshPracticeAdvanceButton();
            while (!_returning && this != null)
            {
                var requested = _requestedPracticeSceneName;
                if (_practiceScene.IsValid() && _practiceScene.isLoaded)
                {
                    var previous = _practiceScene;
                    _practiceScene = default;
                    _practiceDraft = null; _practiceLobby = null; _practiceBattle = null; _practiceCollection = null; _practiceShop = null; _pendingUpgradeAction = TutorialAction.None;
                    if (_ownsPracticeScene)
                    {
                        _ownsPracticeScene = false;
                        var unload = SceneManager.UnloadSceneAsync(previous);
                        if (unload != null) yield return unload;
                    }
                }
                if (!UnityEngine.Application.CanStreamedLevelBeLoaded(requested))
                {
                    Debug.LogError("Tutorial practice scene is unavailable: " + requested);
                    break;
                }
                // Tutorial stays the active scene so all account gates remain closed.
                var loadedScene = default(Scene);
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> capture = (scene, mode) =>
                {
                    if (mode == LoadSceneMode.Additive && scene.name == requested) loadedScene = scene;
                };
                SceneManager.sceneLoaded += capture;
                var load = SceneManager.LoadSceneAsync(requested, LoadSceneMode.Additive);
                if (load == null) { SceneManager.sceneLoaded -= capture; break; }
                load.completed += _ =>
                {
                    SceneManager.sceneLoaded -= capture;
                    if (this != null) return;
                    // A later normal Start scene has the same name. Clean up only the
                    // exact additive scene owned by this load, never a name lookup.
                    if (loadedScene.IsValid() && loadedScene.isLoaded) SceneManager.UnloadSceneAsync(loadedScene);
                };
                yield return load;
                if (this == null) yield break;
                _practiceScene = loadedScene;
                if (!_practiceScene.IsValid() || !_practiceScene.isLoaded) break;
                _ownsPracticeScene = true;
                DisableBorrowedInputAndAudioListeners();
                if (_returning || requested != _requestedPracticeSceneName) continue;
                _loadingPracticeScene = false;
                PresentRealScene();
                RefreshPracticeAdvanceButton();
                break;
            }
            _loadingPracticeScene = false;
            _practiceSceneRoutine = null;
            RefreshPracticeAdvanceButton();
        }

        // Used by native scene validation as well as loading; never alters authored scene assets.
        public void AttachPracticeScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != DesiredPracticeSceneName)
                throw new System.ArgumentException("Expected the current tutorial's actual game scene.", nameof(scene));
            _practiceScene = scene;
            _loadingPracticeScene = false;
            DisableBorrowedInputAndAudioListeners();
            PresentRealScene();
            RefreshPracticeAdvanceButton();
        }
        private void DisableBorrowedInputAndAudioListeners()
        {
            // The host owns one EventSystem and one AudioListener throughout additive practice scenes.
            foreach (var root in _practiceScene.GetRootGameObjects())
            {
                foreach (var events in root.GetComponentsInChildren<EventSystem>(true)) events.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
        }
        private void PresentRealScene()
        {
            if (!IsPracticeSceneReady) return;
            CancelPracticePreviews();
            if (_session.Chapter == 6 && !IsConclusionLesson) { PresentUpgradePracticeScene(); return; }
            var start = FindInScene<GameStartSceneController>(_practiceScene);
            if (start != null)
            {
                start.ConfigureTutorialPractice(_session.PracticeTickets, StartPractice,
                    _session.WaitingForAction &&
                    _session.CurrentStep.RequiredAction == Project333.Runtime.Application.Tutorial.TutorialAction.StartRun);
                ConfigurePracticeHighlights();
                return;
            }
            var battle = FindInScene<BattleBootstrapper>(_practiceScene);
            if (battle != null)
            {
                _practiceBattle = battle;
                if (_session.Chapter >= 4)
                    battle.ConfigureTutorialChapterFour(_session, () => CanPractice, OnBattlePracticeCompleted, OnMulliganSelectionCanceled);
                else battle.ConfigureTutorialBattleEntry(_session.DraftCardIds);
                ConfigurePracticeHighlights();
                return;
            }
            var draft = FindInScene<DraftSceneController>(_practiceScene);
            if (draft == null) return;
            _practiceLobby = draft;
            draft.ConfigureTutorialPractice(_session.DraftCardIds, this, ReturnToStart,
                OnLobbyDeckPreviewReleased, StartPracticeGoldDungeon, () => CanPractice,
                _session.CurrentStep.Cue == TutorialCue.GoldDungeonRemaining ? 1 : _session.PracticeGoldDungeonRemaining,
                _session.CurrentStep.Cue == TutorialCue.Reconnect);
            if (_session.IsDraftReady)
            {
                _practiceDraft = null;
                ConfigurePracticeHighlights();
                return;
            }
            _practiceDraft = FindInScene<DraftOverlayPresenter>(_practiceScene);
            if (_practiceDraft == null) return;
            _practiceDraft.BindTutorial(this, OnTutorialPreviewReleased, () => CanPractice, 0);
            var ids = _session.CurrentOfferCardIds;
            var cards = new List<CardDefinitionAsset>(ids.Count);
            foreach (var id in ids)
            {
                if (!TryGetCardDefinitionAsset(id, out var card))
                    throw new System.InvalidOperationException("Missing tutorial card: " + id);
                cards.Add(card);
            }
            _practiceDraft.ShowOffer(new DraftOffer(cards, _session.ManualPickCount + 1,
                _session.IsLegendaryOpeningOffer), _session.DraftCardIds);
            _practiceDraft.SetTutorialOfferClicksEnabled(_session.WaitingForAction &&
                _session.CurrentStep.RequiredAction == Project333.Runtime.Application.Tutorial.TutorialAction.PickCard);
            // Tutorial's requested skip/selection controls occupy this same chrome area.
            if (_practiceDraft.TutorialBackButtonRect != null)
                _practiceDraft.TutorialBackButtonRect.gameObject.SetActive(false);
            ConfigurePracticeHighlights();
        }
        public void SelectDraftCard(string cardId)
        {
            for (var i = 0; i < _session.CurrentOfferCardIds.Count; i++)
                if (string.Equals(cardId, _session.CurrentOfferCardIds[i], System.StringComparison.Ordinal))
                { OnTutorialOfferSelected(i); return; }
        }
        public void ReturnFromDraftOverlay() => ReturnToStart();
        public bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset asset)
        {
            asset = null;
            return _practiceCardCatalog != null && _practiceCardCatalog.TryGetCardAsset(cardId, out asset);
        }
        private void RefreshPracticeAdvanceButton()
        {
            if (_nextButton == null) return;
            _nextButton.gameObject.SetActive(!RequiresPlayerPractice);
            _nextButton.interactable = CanPractice && !RequiresPlayerPractice;
        }
        private bool RequiresPlayerPractice => !_session.IsComplete && _session.WaitingForAction &&
            _session.CurrentStep.RequiredAction != Project333.Runtime.Application.Tutorial.TutorialAction.CompleteDeck;

        private sealed class PracticeHighlight
        {
            public RectTransform Frame, Target, Clip;
            public Image Artwork;
            public CanvasGroup Group;
            public Canvas ArtworkLayer;
            public bool MatchArtworkSorting;
            public Rect? ArtworkRegion;
            public string DeckCardId;
            public bool Assigned;
        }
        private readonly List<PracticeHighlight> _practiceHighlights = new List<PracticeHighlight>(3);
        private Texture2D _practiceFrameTexture;
        private Sprite _practiceFrameSprite;

        private void ConfigurePracticeHighlights()
        {
            HidePracticeHighlights();
            if (!HasPracticeGuidance || !CanPractice) return;
            if (_session.Chapter == 6) { ConfigureUpgradePracticeHighlights(); RefreshPracticeHighlightLayouts(); return; }
            if (_practiceBattle != null && _session.Chapter >= 4)
            { ConfigureBattlePracticeHighlights(); RefreshPracticeHighlightLayouts(); return; }
            if (_session.IsDraftReady && _practiceLobby != null)
            {
                var cue = _session.CurrentStep.Cue;
                if (cue == TutorialCue.LobbyDeckPreview && _session.DraftCardIds.Count > 0)
                    AssignPracticeHighlight(0, _practiceLobby.TutorialDeckListText?.rectTransform,
                        clip: _practiceLobby.TutorialDeckListViewport, deckCardId: _session.DraftCardIds[0]);
                else if (cue == TutorialCue.PvpMode || cue == TutorialCue.Reconnect)
                    AssignPracticeHighlight(0, _practiceLobby.TutorialBattleButtonRect);
                else if (cue == TutorialCue.RunRecord)
                    AssignPracticeHighlight(0, _practiceLobby.TutorialRunRecordRect);
                else if (cue == TutorialCue.GameModes)
                    AssignPracticeHighlight(0, _practiceLobby.TutorialDeckListViewport ?? _practiceLobby.TutorialDeckListText?.rectTransform);
                else AssignPracticeHighlight(0, _practiceLobby.TutorialGoldDungeonButtonRect);
                RefreshPracticeHighlightLayouts();
                return;
            }
            var action = _session.CurrentStep.RequiredAction;
            if (action == Project333.Runtime.Application.Tutorial.TutorialAction.StartRun)
            {
                var start = FindInScene<GameStartSceneController>(_practiceScene);
                AssignPracticeHighlight(0, start?.TutorialStartButtonRect);
            }
            else if (_practiceDraft != null && action == Project333.Runtime.Application.Tutorial.TutorialAction.PreviewDeck)
            {
                if (_session.DraftCardIds.Count > 0)
                    AssignPracticeHighlight(0, _practiceDraft.TutorialDeckListTextRect,
                        clip: _practiceDraft.TutorialDeckListRect, deckCardId: _session.DraftCardIds[0]);
            }
            else if (_practiceDraft != null)
            {
                for (var i = 0; i < _practiceDraft.TutorialOfferRects.Count; i++)
                {
                    var artwork = _practiceDraft.GetTutorialOfferArtwork(i);
                    AssignPracticeHighlight(i, artwork != null ? artwork.rectTransform : _practiceDraft.TutorialOfferRects[i], artwork);
                }
            }
            RefreshPracticeHighlightLayouts();
        }
        private bool HasPracticeGuidance => RequiresPlayerPractice ||
            ((_session.Chapter == 3 || _session.Chapter >= 4) && !_session.IsComplete);

        private void AssignPracticeHighlight(int index, RectTransform target, Image artwork = null,
            RectTransform clip = null, string deckCardId = null, bool matchArtworkSorting = false, Rect? artworkRegion = null)
        {
            if (target == null) return;
            while (_practiceHighlights.Count <= index) _practiceHighlights.Add(CreatePracticeHighlight(_practiceHighlights.Count));
            var highlight = _practiceHighlights[index];
            highlight.Target = target; highlight.Artwork = artwork; highlight.Clip = clip;
            highlight.DeckCardId = deckCardId; highlight.Assigned = true;
            highlight.MatchArtworkSorting = matchArtworkSorting; highlight.ArtworkRegion = artworkRegion;
        }
        private PracticeHighlight CreatePracticeHighlight(int index)
        {
            if (_practiceFrameSprite == null)
            {
                const int size = 32;
                _practiceFrameTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "TutorialPracticeFrame", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                {
                    var cutCorner = x + y < 3 || size - 1 - x + y < 3 ||
                        x + size - 1 - y < 3 || size - 1 - x + size - 1 - y < 3;
                    var edge = x < 4 || y < 4 || x >= size - 4 || y >= size - 4;
                    pixels[y * size + x] = !cutCorner && edge ? Color.white : Color.clear;
                }
                _practiceFrameTexture.SetPixels(pixels); _practiceFrameTexture.Apply(false, true);
                _practiceFrameSprite = Sprite.Create(_practiceFrameTexture, new Rect(0, 0, size, size),
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            }
            var frame = Rect(_safeRoot, "PracticeAura_" + index, Vector2.zero, Vector2.zero);
            frame.SetAsFirstSibling();
            var group = frame.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            for (var layer = 0; layer < 7; layer++)
            {
                var glow = Rect(frame, "Glow_" + layer, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                glow.sprite = _practiceFrameSprite; glow.type = Image.Type.Sliced; glow.raycastTarget = false;
                glow.color = layer == 6 ? new Color(.50f, 1f, .24f, .98f)
                    : new Color(.18f, 1f, .10f, .06f + layer * .045f);
                var padding = layer == 6 ? 2f : 18f - layer * 3f;
                glow.rectTransform.offsetMin = Vector2.one * -padding;
                glow.rectTransform.offsetMax = Vector2.one * padding;
            }
            frame.gameObject.SetActive(false);
            return new PracticeHighlight { Frame = frame, Group = group };
        }
        private void HidePracticeHighlights()
        {
            foreach (var highlight in _practiceHighlights)
            { highlight.Assigned = false; highlight.Frame.gameObject.SetActive(false); }
        }
        private void RefreshPracticeHighlightLayouts()
        {
            foreach (var highlight in _practiceHighlights)
            {
                var visible = highlight.Assigned && HasPracticeGuidance && CanPractice &&
                    highlight.Target != null && highlight.Target.gameObject.activeInHierarchy;
                var targetBounds = visible ? highlight.Target.rect : default;
                if (visible && highlight.Artwork != null)
                    {
                    targetBounds = Hand.HandCardStatOverlayLayout.CalculateRenderedSpriteRect(highlight.Artwork);
                    if (highlight.ArtworkRegion.HasValue)
                    {
                        var region = highlight.ArtworkRegion.Value;
                        targetBounds = new Rect(targetBounds.min + Vector2.Scale(targetBounds.size, region.min), Vector2.Scale(targetBounds.size, region.size));
                    }
                }
                if (visible && highlight.DeckCardId != null)
                {
                    if (_practiceDraft != null)
                        visible = _practiceDraft.GetTutorialDeckCardBounds(highlight.DeckCardId, out targetBounds);
                    else
                    {
                        var relay = highlight.Target.GetComponent<DeckListCardHoldPreview>();
                        visible = relay != null && relay.TryGetCardLocalBounds(highlight.DeckCardId, out targetBounds);
                    }
                }
                var bounds = visible ? PracticeOverlayBounds(highlight.Target, targetBounds, EventCameraFor(highlight.Target)) : default;
                if (visible && highlight.Clip != null)
                {
                    var clip = PracticeOverlayBounds(highlight.Clip, highlight.Clip.rect, EventCameraFor(highlight.Clip));
                    var min = Vector2.Max(bounds.min, clip.min); var max = Vector2.Min(bounds.max, clip.max);
                    bounds = new Rect(min, max - min);
                }
                visible &= bounds.width > 0f && bounds.height > 0f;
                highlight.Frame.gameObject.SetActive(visible);
                if (!visible) continue;
                // Keep guidance behind cards drawn in front of its source card.
                // Pre-battle guidance sits below held card previews and their tooltips.
                // Battle card guidance still follows its native fan sorting.
                var sourceLayer = highlight.MatchArtworkSorting ? highlight.Target.GetComponentInParent<Canvas>() : null;
                if (sourceLayer != null && highlight.Artwork != null)
                {
                    if (highlight.ArtworkLayer == null) highlight.ArtworkLayer = highlight.Frame.gameObject.AddComponent<Canvas>();
                    highlight.ArtworkLayer.overrideSorting = true;
                    highlight.ArtworkLayer.sortingLayerID = sourceLayer.sortingLayerID;
                    highlight.ArtworkLayer.sortingOrder = sourceLayer.sortingOrder;
                }
                else if (_session.Chapter < 4)
                {
                    if (highlight.ArtworkLayer == null) highlight.ArtworkLayer = highlight.Frame.gameObject.AddComponent<Canvas>();
                    highlight.ArtworkLayer.overrideSorting = true;
                    highlight.ArtworkLayer.sortingLayerID = _safeRoot.GetComponentInParent<Canvas>().sortingLayerID;
                    highlight.ArtworkLayer.sortingOrder = DeckCardPreviewOverlay.SortingOrder - 1;
                }
                else if (highlight.ArtworkLayer != null) highlight.ArtworkLayer.overrideSorting = false;
                highlight.Frame.anchorMin = highlight.Frame.anchorMax = _safeRoot.pivot;
                // Card artwork moves, scales and rotates with the native fan layout.
                // Track its oriented corners rather than an axis-aligned diagonal.
                if (highlight.Artwork != null && highlight.Clip == null)
                {
                    var camera = EventCameraFor(highlight.Target);
                    var bottomLeft = PracticeOverlayPoint(highlight.Target, targetBounds.min, camera);
                    var bottomRight = PracticeOverlayPoint(highlight.Target, new Vector2(targetBounds.xMax, targetBounds.yMin), camera);
                    var topLeft = PracticeOverlayPoint(highlight.Target, new Vector2(targetBounds.xMin, targetBounds.yMax), camera);
                    var topRight = PracticeOverlayPoint(highlight.Target, targetBounds.max, camera);
                    var width = bottomRight - bottomLeft;
                    highlight.Frame.anchoredPosition = (bottomLeft + topRight) * .5f;
                    highlight.Frame.sizeDelta = new Vector2(width.magnitude, (topLeft - bottomLeft).magnitude);
                    highlight.Frame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(width.y, width.x) * Mathf.Rad2Deg);
                }
                else
                {
                    highlight.Frame.anchoredPosition = bounds.center;
                    highlight.Frame.sizeDelta = bounds.size;
                    highlight.Frame.localRotation = Quaternion.identity;
                }
                highlight.Group.alpha = .90f + .10f * Mathf.Sin(Time.unscaledTime * 3.2f);
            }
        }
        private void RefreshPracticeDialogueLayout()
        {
            RefreshCombatDamageComparison();
            if (_safeRoot == null || _dialogue == null || _safeRoot.rect.height <= 0f) return;
            var panel = _dialogue.rectTransform.parent as RectTransform;
            if (panel == null) return;
            if (_session.Chapter == 6) { RefreshUpgradeDialogueLayout(panel); return; }
            if (_session.Chapter >= 4 && _practiceBattle != null)
            { RefreshBattlePracticeDialogueLayout(panel); return; }
            // Restore standard placement after leaving a combat chapter.
            Place(panel, new Vector2(.15f,.016f), new Vector2(.985f,.165f));
            Place(_guidePortrait.rectTransform, new Vector2(.006f,.002f), new Vector2(.15f,.17f));
            var safeBounds = _safeRoot.rect;
            var ceiling = safeBounds.yMin + safeBounds.height * .165f;
            if (!_session.IsComplete && IsPracticeSceneReady && _session.Chapter == 2 && _practiceDraft != null)
            {
                // The real game's cards can be taller on wide screens. Fit only the
                // dialogue to the free space beneath their aspect-fitted artwork.
                for (var i = 0; i < _practiceDraft.TutorialOfferRects.Count; i++)
                {
                    var image = _practiceDraft.GetTutorialOfferArtwork(i);
                    if (image == null || !image.gameObject.activeInHierarchy) continue;
                    var artwork = Hand.HandCardStatOverlayLayout.CalculateRenderedSpriteRect(image);
                    if (artwork.width <= 1f || artwork.height <= 1f) continue;
                    var bounds = PracticeOverlayBounds(image.rectTransform, artwork, EventCameraFor(image.rectTransform));
                    ceiling = Mathf.Min(ceiling, bounds.yMin - 12f);
                }
            }
            var max = panel.anchorMax;
            max.y = Mathf.Clamp((ceiling - safeBounds.yMin) / safeBounds.height, panel.anchorMin.y, .165f);
            if (!Mathf.Approximately(panel.anchorMax.y, max.y)) panel.anchorMax = max;
        }
        private Rect PracticeOverlayBounds(RectTransform source, Rect bounds, Camera eventCamera)
        {
            var bottomLeft = PracticeOverlayPoint(source, bounds.min, eventCamera);
            var bottomRight = PracticeOverlayPoint(source, new Vector2(bounds.xMax, bounds.yMin), eventCamera);
            var topLeft = PracticeOverlayPoint(source, new Vector2(bounds.xMin, bounds.yMax), eventCamera);
            var topRight = PracticeOverlayPoint(source, bounds.max, eventCamera);
            var min = Vector2.Min(Vector2.Min(bottomLeft, bottomRight), Vector2.Min(topLeft, topRight));
            var max = Vector2.Max(Vector2.Max(bottomLeft, bottomRight), Vector2.Max(topLeft, topRight));
            return new Rect(min, max - min);
        }
        private Vector2 PracticeOverlayPoint(RectTransform source, Vector2 localPoint, Camera eventCamera)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, source.TransformPoint(localPoint));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeRoot, screenPoint, EventCameraFor(_safeRoot), out var point);
            return point;
        }
        private static Camera EventCameraFor(RectTransform target)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }
        private void DisposePracticeHighlights()
        {
            if (UnityEngine.Application.isPlaying) { Destroy(_practiceFrameSprite); Destroy(_practiceFrameTexture); }
            else { DestroyImmediate(_practiceFrameSprite); DestroyImmediate(_practiceFrameTexture); }
        }
        private void CancelPracticePreviews()
        {
            _practiceDraft?.CancelTutorialPreviews();
            if (!_practiceScene.IsValid() || !_practiceScene.isLoaded) return;
            foreach (var root in _practiceScene.GetRootGameObjects())
            {
                foreach (var tile in root.GetComponentsInChildren<TileTextView>(true)) tile.HideCurrentCardPreview();
                foreach (var hold in root.GetComponentsInChildren<CardHoldPreview>(true))
                    hold.CancelHold();
                foreach (var list in root.GetComponentsInChildren<DeckListCardHoldPreview>(true))
                    list.HidePreview();
            }
        }
        private void DisposePracticeScene()
        {
            DetachCombatPreviewHandlers();
            CancelPracticePreviews();
            if (_ownsPracticeScene && _practiceScene.IsValid() && _practiceScene.isLoaded &&
                UnityEngine.Application.isPlaying)
                SceneManager.UnloadSceneAsync(_practiceScene);
            _practiceScene = default; _practiceDraft = null; _practiceLobby = null; _practiceBattle = null; _practiceCollection = null; _practiceShop = null; _pendingUpgradeAction = TutorialAction.None; _ownsPracticeScene = false;
        }
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
