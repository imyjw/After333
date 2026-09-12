using System;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.HUD;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleScreenPresenter : MonoBehaviour
    {
        private const string DefaultBackgroundResourcePath = "Project333/BattleScene/BattleBackground";
        private const string RuntimeBackgroundCanvasName = "BattleSceneBackgroundCanvas";

        private sealed class ManagedBattleUiObject
        {
            public GameObject GameObject;
            public bool WasInitiallyActive;
        }

        [System.Serializable]
        public sealed class SummaryChangedEvent : UnityEvent<string>
        {
        }

        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private BoardPresenter _boardPresenter;
        [SerializeField] private HandPresenter _handPresenter;
        [SerializeField] private OpponentHandPresenter _opponentHandPresenter;
        [SerializeField] private OpponentDeckPresenter _opponentDeckPresenter;
        [SerializeField] private PlayerDeckPresenter _playerDeckPresenter;
        [SerializeField] private ResourceBarPresenter _resourceBarPresenter;
        [SerializeField] private BattleMulliganOverlayPresenter _mulliganOverlayPresenter;
        [SerializeField] private Button _endTurnButton;
        [SerializeField, HideInInspector] private int _endTurnButtonVisualVersion;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Sprite _backgroundSprite;
        [SerializeField] private string _backgroundResourcePath = DefaultBackgroundResourcePath;

        [Header("Battle Sound Effects")]
        [SerializeField] private AudioSource _soundEffectAudioSource;
        [SerializeField] private AudioClip _cardDrawAudioClip;
        [SerializeField, Range(0f, 1f)] private float _cardDrawVolume = 1f;
        [SerializeField, Min(0f)] private float _cardDrawRepeatInterval = 0.1f;

        [SerializeField] private GameObject[] _draftHiddenUiObjects = Array.Empty<GameObject>();
        [SerializeField] private SummaryChangedEvent _onSummaryChanged = new SummaryChangedEvent();
        [TextArea(8, 20)]
        [SerializeField] private string _debugSummary = "No active battle.";

        private readonly List<ManagedBattleUiObject> _managedBattleUiObjects = new List<ManagedBattleUiObject>();
        private bool _hasBuiltBattleUiCache;
        private int _queuedCardDrawSoundCount;
        private Coroutine _cardDrawSoundCoroutine;
        [SerializeField, HideInInspector] private Canvas _runtimeBackgroundCanvas;
#if UNITY_EDITOR
        private bool _hasQueuedEditorUiMaterialization;
#endif

        public string DebugSummary => _debugSummary;

        public BoardPresenter BoardPresenter => _boardPresenter;

        public bool IsMulliganPresentationActive =>
            _mulliganOverlayPresenter != null && _mulliganOverlayPresenter.IsSessionActive;

        private void Awake()
        {
            ApplyEndTurnButtonVisual();
            EnsureSoundEffectAudioSource();
            EnsureOpponentHandPresenter();
            EnsureOpponentDeckPresenter();
            EnsurePlayerDeckPresenter();
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            EnsureBackgroundImage();
            ApplyBackgroundVisual();
        }

        private void OnEnable()
        {
            ApplyEndTurnButtonVisual();
            EnsureSoundEffectAudioSource();
            EnsureOpponentHandPresenter();
            EnsureOpponentDeckPresenter();
            EnsurePlayerDeckPresenter();
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            EnsureBackgroundImage();
            ApplyBackgroundVisual();
        }

        private void ApplyEndTurnButtonVisual()
        {
            if (_endTurnButtonVisualVersion >= 1 || _endTurnButton == null) return;
            var sprite = Resources.Load<Sprite>("Project333/UI/PixelEndTurnButtonFinal");
            var image = _endTurnButton.GetComponent<Image>();
            if (sprite == null || image == null) return;

            image.sprite = sprite;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            _endTurnButton.targetGraphic = image;
            // The caption is already baked into the supplied sprite.
            foreach (var label in _endTurnButton.GetComponentsInChildren<TMPro.TMP_Text>(true))
                label.enabled = false;
            foreach (var label in _endTurnButton.GetComponentsInChildren<Text>(true))
                label.enabled = false;
            _endTurnButtonVisualVersion = 1;
        }

        private void OnValidate()
        {
            AutoAssignOpponentHandPresenter();
            AutoAssignOpponentDeckPresenter();
            AutoAssignPlayerDeckPresenter();
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            ApplyBackgroundVisual();
#if UNITY_EDITOR
            QueuePersistentBattleUiMaterialization();
#endif
        }

#if UNITY_EDITOR
        private void QueuePersistentBattleUiMaterialization()
        {
            if (_hasQueuedEditorUiMaterialization || UnityEngine.Application.isPlaying)
            {
                return;
            }

            _hasQueuedEditorUiMaterialization = true;
            EditorApplication.delayCall += MaterializePersistentBattleUiForEditor;
        }

        [ContextMenu("Materialize Missing Persistent Battle UI")]
        public void MaterializePersistentBattleUiForEditor()
        {
            _hasQueuedEditorUiMaterialization = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            ApplyEndTurnButtonVisual();
            EnsureOpponentHandPresenter(allowCreationInEditMode: true);
            _opponentHandPresenter?.EnsureEditableCardBackSlots();
            EnsureOpponentDeckPresenter(allowCreationInEditMode: true);
            _opponentDeckPresenter?.EnsureEditableDeckUi();
            EnsurePlayerDeckPresenter(allowCreationInEditMode: true);
            _playerDeckPresenter?.EnsureEditableDeckUi();
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            if (_backgroundImage == null)
            {
                CreateRuntimeBackgroundImage();
            }

            ApplyBackgroundVisual();
            EditorUtility.SetDirty(this);
            if (_opponentHandPresenter != null)
            {
                EditorUtility.SetDirty(_opponentHandPresenter);
            }

            if (_opponentDeckPresenter != null)
            {
                EditorUtility.SetDirty(_opponentDeckPresenter);
            }

            if (_playerDeckPresenter != null)
            {
                EditorUtility.SetDirty(_playerDeckPresenter);
            }

            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        public void Bind(BattleBootstrapper battleBootstrapper)
        {
            _battleBootstrapper = battleBootstrapper;
            _mulliganOverlayPresenter?.Bind(battleBootstrapper);
        }

        public void Present(BattleState battleState)
        {
            _debugSummary = BattleStateSummaryFormatter.Format(battleState);
            _boardPresenter?.Present(battleState);
            _handPresenter?.Present(battleState?.Player.Hand,
                battleState == null ? 0 : Project333.Runtime.Application.Services.SpellPowerRules.GetTotal(battleState, PlayerId.Player));
            _opponentHandPresenter?.Present(battleState?.AI.Hand);
            _opponentDeckPresenter?.Present(battleState?.AI.Deck);
            _playerDeckPresenter?.Present(battleState?.Player.Deck);
            _resourceBarPresenter?.Present(battleState);
            PresentMulliganControls(battleState);
            _mulliganOverlayPresenter?.Present(battleState);
            _onSummaryChanged.Invoke(_debugSummary);
        }

        public void QueueCardDrawSounds(int drawCount)
        {
            if (drawCount <= 0 || _cardDrawAudioClip == null)
            {
                return;
            }

            EnsureSoundEffectAudioSource();
            if (_soundEffectAudioSource == null)
            {
                return;
            }

            _queuedCardDrawSoundCount += drawCount;
            if (_cardDrawSoundCoroutine == null)
            {
                _cardDrawSoundCoroutine = StartCoroutine(PlayQueuedCardDrawSounds());
            }
        }

        private IEnumerator PlayQueuedCardDrawSounds()
        {
            while (_queuedCardDrawSoundCount > 0)
            {
                _queuedCardDrawSoundCount--;
                _soundEffectAudioSource.PlayOneShot(_cardDrawAudioClip, _cardDrawVolume);

                if (_queuedCardDrawSoundCount > 0 && _cardDrawRepeatInterval > 0f)
                {
                    yield return new WaitForSecondsRealtime(_cardDrawRepeatInterval);
                }
                else
                {
                    yield return null;
                }
            }

            _cardDrawSoundCoroutine = null;
        }

        private void EnsureSoundEffectAudioSource()
        {
            if (_soundEffectAudioSource == null)
            {
                _soundEffectAudioSource = GetComponent<AudioSource>();
            }

            if (_soundEffectAudioSource == null && UnityEngine.Application.isPlaying)
            {
                _soundEffectAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (_soundEffectAudioSource == null)
            {
                return;
            }

            _soundEffectAudioSource.playOnAwake = false;
            _soundEffectAudioSource.loop = false;
            _soundEffectAudioSource.spatialBlend = 0f;
        }

        public void SetBattleUiVisible(bool isVisible)
        {
            EnsureManagedBattleUiObjects();

            foreach (var managedObject in _managedBattleUiObjects)
            {
                if (managedObject?.GameObject == null)
                {
                    continue;
                }

                managedObject.GameObject.SetActive(isVisible && managedObject.WasInitiallyActive);
            }
        }

        public void PassMulliganFromUi()
        {
            if (_mulliganOverlayPresenter != null)
            {
                _mulliganOverlayPresenter.ConfirmSelectionFromUi();
                return;
            }

            _battleBootstrapper?.PassPlayerMulligan();
        }

        private void PresentMulliganControls(BattleState battleState)
        {
            var isMulligan = battleState != null && battleState.Phase == PhaseType.Mulligan;
            if (_endTurnButton != null)
            {
                _endTurnButton.gameObject.SetActive(!isMulligan);
            }
        }

        public void ResolveTurnStartFromUi()
        {
            _battleBootstrapper?.ResolveTurnStart();
        }

        public void EndTurnFromUi()
        {
            _battleBootstrapper?.EndTurn();
        }

        private void EnsureManagedBattleUiObjects()
        {
            if (_hasBuiltBattleUiCache)
            {
                return;
            }

            _hasBuiltBattleUiCache = true;

            RegisterManagedObject(_boardPresenter == null ? null : _boardPresenter.gameObject);
            RegisterManagedObject(_handPresenter == null ? null : _handPresenter.gameObject);
            RegisterManagedObject(_opponentHandPresenter == null ? null : _opponentHandPresenter.gameObject);
            RegisterManagedObject(_opponentDeckPresenter == null ? null : _opponentDeckPresenter.gameObject);
            RegisterManagedObject(_playerDeckPresenter == null ? null : _playerDeckPresenter.gameObject);
            RegisterManagedObject(_resourceBarPresenter == null ? null : _resourceBarPresenter.gameObject);

            RegisterManagedObjects(UnityEngine.Object.FindObjectsByType<BattleSummaryTextView>(FindObjectsSortMode.None));
            RegisterManagedObjects(UnityEngine.Object.FindObjectsByType<CombatLogTextView>(FindObjectsSortMode.None));
            RegisterManagedObjects(UnityEngine.Object.FindObjectsByType<BattlePlayerInputTextView>(FindObjectsSortMode.None));
            RegisterManagedObjects(UnityEngine.Object.FindObjectsByType<BattleOutcomeTextView>(FindObjectsSortMode.None));

            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button != null && IsBoundToPresenter(button))
                {
                    RegisterManagedObject(button.gameObject);
                }
            }

            if (_draftHiddenUiObjects == null)
            {
                return;
            }

            foreach (var draftHiddenUiObject in _draftHiddenUiObjects)
            {
                RegisterManagedObject(draftHiddenUiObject);
            }
        }

        private void RegisterManagedObjects<T>(T[] components) where T : Component
        {
            if (components == null)
            {
                return;
            }

            foreach (var component in components)
            {
                RegisterManagedObject(component == null ? null : component.gameObject);
            }
        }

        private void RegisterManagedObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            foreach (var managedObject in _managedBattleUiObjects)
            {
                if (managedObject != null && managedObject.GameObject == gameObject)
                {
                    return;
                }
            }

            _managedBattleUiObjects.Add(new ManagedBattleUiObject
            {
                GameObject = gameObject,
                WasInitiallyActive = gameObject.activeSelf,
            });
        }

        private bool IsBoundToPresenter(Button button)
        {
            if (button == null)
            {
                return false;
            }

            var onClick = button.onClick;
            var listenerCount = onClick.GetPersistentEventCount();
            for (var i = 0; i < listenerCount; i++)
            {
                if (onClick.GetPersistentTarget(i) == this)
                {
                    return true;
                }
            }

            return false;
        }

        private void AutoAssignBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                return;
            }

            if (transform.Find("Background") is RectTransform backgroundTransform)
            {
                _backgroundImage = backgroundTransform.GetComponent<Image>();
            }
        }

        private void EnsureOpponentHandPresenter(bool allowCreationInEditMode = false)
        {
            AutoAssignOpponentHandPresenter();
            if (_opponentHandPresenter != null ||
                (!UnityEngine.Application.isPlaying && !allowCreationInEditMode))
            {
                return;
            }

            var presenterObject = new GameObject(
                "OpponentHandCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(OpponentHandPresenter));
            presenterObject.transform.SetParent(transform, false);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(presenterObject, "Create Opponent Hand UI");
            }
#endif
            _opponentHandPresenter = presenterObject.GetComponent<OpponentHandPresenter>();
        }

        private void AutoAssignOpponentHandPresenter()
        {
            if (_opponentHandPresenter != null)
            {
                return;
            }

            _opponentHandPresenter = GetComponentInChildren<OpponentHandPresenter>(includeInactive: true);
            if (_opponentHandPresenter != null)
            {
                return;
            }

            foreach (var presenter in UnityEngine.Object.FindObjectsByType<OpponentHandPresenter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (presenter != null && presenter.gameObject.scene == gameObject.scene)
                {
                    _opponentHandPresenter = presenter;
                    return;
                }
            }
        }

        private void EnsurePlayerDeckPresenter(bool allowCreationInEditMode = false)
        {
            AutoAssignPlayerDeckPresenter();
            if (_playerDeckPresenter != null ||
                (!UnityEngine.Application.isPlaying && !allowCreationInEditMode))
            {
                return;
            }

            var presenterObject = new GameObject(
                "PlayerDeckCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlayerDeckPresenter));
            presenterObject.transform.SetParent(transform, false);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(presenterObject, "Create Player Deck UI");
            }
#endif
            _playerDeckPresenter = presenterObject.GetComponent<PlayerDeckPresenter>();
        }

        private void AutoAssignPlayerDeckPresenter()
        {
            if (_playerDeckPresenter != null)
            {
                return;
            }

            if (transform.Find("PlayerDeckCanvas") is Transform playerDeckTransform)
            {
                var namedPresenter = playerDeckTransform.GetComponent<PlayerDeckPresenter>();
                if (namedPresenter != null && !(namedPresenter is OpponentDeckPresenter))
                {
                    _playerDeckPresenter = namedPresenter;
                    return;
                }
            }

            foreach (var presenter in UnityEngine.Object.FindObjectsByType<PlayerDeckPresenter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (presenter != null &&
                    !(presenter is OpponentDeckPresenter) &&
                    presenter.gameObject.scene == gameObject.scene)
                {
                    _playerDeckPresenter = presenter;
                    return;
                }
            }
        }

        private void EnsureOpponentDeckPresenter(bool allowCreationInEditMode = false)
        {
            AutoAssignOpponentDeckPresenter();
            if (_opponentDeckPresenter != null ||
                (!UnityEngine.Application.isPlaying && !allowCreationInEditMode))
            {
                return;
            }

            var presenterObject = new GameObject(
                "OpponentDeckCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(OpponentDeckPresenter));
            presenterObject.transform.SetParent(transform, false);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(presenterObject, "Create Opponent Deck UI");
            }
#endif
            _opponentDeckPresenter = presenterObject.GetComponent<OpponentDeckPresenter>();
        }

        private void AutoAssignOpponentDeckPresenter()
        {
            if (_opponentDeckPresenter != null)
            {
                return;
            }

            if (transform.Find("OpponentDeckCanvas") is Transform opponentDeckTransform)
            {
                _opponentDeckPresenter = opponentDeckTransform.GetComponent<OpponentDeckPresenter>();
                if (_opponentDeckPresenter != null)
                {
                    return;
                }
            }

            _opponentDeckPresenter = GetComponentInChildren<OpponentDeckPresenter>(includeInactive: true);
            if (_opponentDeckPresenter != null)
            {
                return;
            }

            foreach (var presenter in UnityEngine.Object.FindObjectsByType<OpponentDeckPresenter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (presenter != null && presenter.gameObject.scene == gameObject.scene)
                {
                    _opponentDeckPresenter = presenter;
                    return;
                }
            }
        }

        private void EnsureBackgroundSprite()
        {
            if (_backgroundSprite != null || string.IsNullOrWhiteSpace(_backgroundResourcePath))
            {
                return;
            }

            _backgroundSprite = LoadSpriteFromResources(_backgroundResourcePath);
        }

        private void EnsureBackgroundImage()
        {
            if (_backgroundImage != null || !UnityEngine.Application.isPlaying)
            {
                return;
            }

            CreateRuntimeBackgroundImage();
        }

        private void CreateRuntimeBackgroundImage()
        {
            if (_runtimeBackgroundCanvas != null && _backgroundImage != null)
            {
                return;
            }

            var existingCanvasTransform = UnityEngine.GameObject.Find(RuntimeBackgroundCanvasName)?.transform;
            if (existingCanvasTransform != null)
            {
                _runtimeBackgroundCanvas = existingCanvasTransform.GetComponent<Canvas>();
                _backgroundImage = existingCanvasTransform.GetComponentInChildren<Image>(includeInactive: true);
                return;
            }

            var canvasObject = new GameObject(RuntimeBackgroundCanvasName, typeof(Canvas), typeof(CanvasScaler));
            if (gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
            }
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Battle Background UI");
            }
#endif
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var imageObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var backgroundRect = imageObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var backgroundImage = imageObject.GetComponent<Image>();
            backgroundImage.raycastTarget = false;
            backgroundImage.color = Color.white;

            _runtimeBackgroundCanvas = canvas;
            _backgroundImage = backgroundImage;
        }

        private void ApplyBackgroundVisual()
        {
            if (_backgroundImage == null || _backgroundSprite == null)
            {
                return;
            }

            _backgroundImage.sprite = _backgroundSprite;
            _backgroundImage.color = Color.white;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.preserveAspect = false;
            _backgroundImage.raycastTarget = false;
        }

        private static Sprite LoadSpriteFromResources(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            var runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeSprite.name = texture.name;
            return runtimeSprite;
        }
    }
}
