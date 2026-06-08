using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.HUD;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
        [SerializeField] private ResourceBarPresenter _resourceBarPresenter;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Sprite _backgroundSprite;
        [SerializeField] private string _backgroundResourcePath = DefaultBackgroundResourcePath;
        [SerializeField] private GameObject[] _draftHiddenUiObjects = Array.Empty<GameObject>();
        [SerializeField] private SummaryChangedEvent _onSummaryChanged = new SummaryChangedEvent();
        [TextArea(8, 20)]
        [SerializeField] private string _debugSummary = "No active battle.";

        private readonly List<ManagedBattleUiObject> _managedBattleUiObjects = new List<ManagedBattleUiObject>();
        private bool _hasBuiltBattleUiCache;
        private Canvas _runtimeBackgroundCanvas;

        public string DebugSummary => _debugSummary;

        public BoardPresenter BoardPresenter => _boardPresenter;

        private void Awake()
        {
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            EnsureBackgroundImage();
            ApplyBackgroundVisual();
        }

        private void OnEnable()
        {
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            EnsureBackgroundImage();
            ApplyBackgroundVisual();
        }

        private void OnValidate()
        {
            AutoAssignBackgroundImage();
            EnsureBackgroundSprite();
            ApplyBackgroundVisual();
        }

        public void Bind(BattleBootstrapper battleBootstrapper)
        {
            _battleBootstrapper = battleBootstrapper;
        }

        public void Present(BattleState battleState)
        {
            _debugSummary = BattleStateSummaryFormatter.Format(battleState);
            _boardPresenter?.Present(battleState);
            _handPresenter?.Present(battleState?.Player.Hand);
            _resourceBarPresenter?.Present(battleState);
            _onSummaryChanged.Invoke(_debugSummary);
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

        public void StartBattleFromUi()
        {
            _battleBootstrapper?.StartBattle();
        }

        public void PassMulliganFromUi()
        {
            _battleBootstrapper?.PassPlayerMulligan();
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
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

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
