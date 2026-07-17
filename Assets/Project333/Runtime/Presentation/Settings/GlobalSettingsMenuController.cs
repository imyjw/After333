using System;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Settings
{
    [DisallowMultipleComponent]
    public sealed class GlobalSettingsMenuController : MonoBehaviour
    {
        public const string RootObjectName = "GlobalSettingsMenuCanvas";
        private const string GearSpriteResourcePath = "Project333/UI/Settings";
        private const int MenuSortingOrder = 30000;

        private static readonly string[] SupportedSceneNames =
        {
            "GameStart_VSlice",
            "Draft_VSlice",
            "DeckBuilding_VSlice",
            "Battle_VSlice",
            "OwnedCards_VSlice"
        };

        [Header("Canvas")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _safeAreaRoot;
        [SerializeField] private int _sortingOrder = MenuSortingOrder;

        [Header("Gear Button")]
        [SerializeField] private Button _gearButton;
        [SerializeField] private Image _gearIcon;

        [Header("Menu")]
        [SerializeField] private RectTransform _modalRoot;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private RectTransform _mainPanel;
        [SerializeField] private Text _mainTitleText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _surrenderButton;
        [SerializeField] private Text _surrenderButtonLabel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Text _settingsButtonLabel;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private Text _logoutButtonLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Text _quitButtonLabel;

        [Header("Audio Settings")]
        [SerializeField] private RectTransform _audioPanel;
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Text _masterVolumeValueText;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Text _musicVolumeValueText;
        [SerializeField] private Button _audioBackButton;

        [Header("Confirmation")]
        [SerializeField] private RectTransform _confirmationPanel;
        [SerializeField] private Text _confirmationTitleText;
        [SerializeField] private Text _confirmationMessageText;
        [SerializeField] private Button _confirmationCancelButton;
        [SerializeField] private Text _confirmationCancelButtonLabel;
        [SerializeField] private Button _confirmationAcceptButton;
        [SerializeField] private Text _confirmationAcceptButtonLabel;

        private Action _pendingConfirmationAction;
        private string _sceneName = string.Empty;
        private bool _isHandlingSceneBackNavigation;

        public static bool IsSupportedScene(string sceneName)
        {
            for (var i = 0; i < SupportedSceneNames.Length; i++)
            {
                if (string.Equals(SupportedSceneNames[i], sceneName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static GlobalSettingsMenuController EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsSupportedScene(scene.name))
            {
                return null;
            }

            var existing = FindSceneComponent<GlobalSettingsMenuController>(scene);
            if (existing != null)
            {
                existing.EnsureEditableHierarchy();
                return existing;
            }

            var rootObject = new GameObject(RootObjectName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            var controller = rootObject.AddComponent<GlobalSettingsMenuController>();
            controller.EnsureEditableHierarchy();
            return controller;
        }

        public void EnsureEditableHierarchy()
        {
            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            SetFullStretch(rootRect);
            _canvas = ResolveOrAddComponent(_canvas, gameObject);
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _safeAreaRoot = ResolveOrCreateRect(
                _safeAreaRoot,
                "SafeAreaRoot",
                transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                out _);
            if (!_safeAreaRoot.TryGetComponent<SafeAreaFitter>(out var safeAreaFitter))
            {
                safeAreaFitter = _safeAreaRoot.gameObject.AddComponent<SafeAreaFitter>();
            }

            safeAreaFitter.Configure();
            EnsureGearButton();
            EnsureModalHierarchy();
        }

        private void Awake()
        {
            _sceneName = gameObject.scene.name;
            EnsureEditableHierarchy();
            BindUi();
            CloseMenu();
        }

        private void OnEnable()
        {
            _sceneName = gameObject.scene.name;
            if (UnityEngine.Application.isPlaying)
            {
                BindUi();
                CloseMenu();
            }
        }

        private void OnDisable()
        {
            After333AudioSettings.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                After333AudioSettings.Save();
            }
        }

        private void OnApplicationQuit()
        {
            After333AudioSettings.Save();
        }

        private void Update()
        {
            if (_modalRoot != null &&
                _modalRoot.gameObject.activeSelf &&
                string.Equals(_sceneName, "Battle_VSlice", StringComparison.Ordinal))
            {
                RefreshContextButtons();
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (_modalRoot != null && _modalRoot.gameObject.activeSelf)
            {
                CloseMenu();
                return;
            }

            HandleSceneBackNavigation();
        }

        private void HandleSceneBackNavigation()
        {
            if (_isHandlingSceneBackNavigation)
            {
                return;
            }

            switch (_sceneName)
            {
                case "GameStart_VSlice":
                case "Battle_VSlice":
                    OpenMenu();
                    return;

                case "OwnedCards_VSlice":
                    _isHandlingSceneBackNavigation = true;
                    var ownedCardsController = FindSceneComponent<OwnedCardsSceneController>(gameObject.scene);
                    if (ownedCardsController != null)
                    {
                        ownedCardsController.ReturnToStartSceneFromUi();
                        return;
                    }

                    SceneManager.LoadScene("GameStart_VSlice");
                    return;

                case "Draft_VSlice":
                case "DeckBuilding_VSlice":
                    _isHandlingSceneBackNavigation = true;
                    var draftController = FindSceneComponent<DraftSceneController>(gameObject.scene);
                    if (draftController != null)
                    {
                        draftController.ReturnToStartSceneFromUi();
                        return;
                    }

                    SceneManager.LoadScene("GameStart_VSlice");
                    return;
            }
        }

        public void OpenMenu()
        {
            ClearBattleSelection();
            RefreshContextButtons();
            SetActive(_modalRoot, true);
            ShowOnly(_mainPanel);
        }

        public void CloseMenu()
        {
            _pendingConfirmationAction = null;
            SetActive(_modalRoot, false);
            After333AudioSettings.Save();
        }

        private void EnsureGearButton()
        {
            _gearButton = ResolveOrCreateButton(
                _gearButton,
                "GearButton",
                _safeAreaRoot,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(86f, 86f),
                new Vector2(-24f, -24f),
                new Color(0.025f, 0.075f, 0.1f, 0.94f),
                out _);

            var iconTransform = _gearButton.transform.Find("GearIcon") as RectTransform;
            if (_gearIcon == null && iconTransform != null)
            {
                _gearIcon = iconTransform.GetComponent<Image>();
            }

            if (_gearIcon == null)
            {
                var iconObject = new GameObject("GearIcon", typeof(RectTransform), typeof(Image));
                iconTransform = iconObject.GetComponent<RectTransform>();
                iconTransform.SetParent(_gearButton.transform, false);
                iconTransform.anchorMin = new Vector2(0.18f, 0.18f);
                iconTransform.anchorMax = new Vector2(0.82f, 0.82f);
                iconTransform.offsetMin = Vector2.zero;
                iconTransform.offsetMax = Vector2.zero;
                _gearIcon = iconObject.GetComponent<Image>();
                _gearIcon.preserveAspect = true;
                _gearIcon.raycastTarget = false;
            }

            if (_gearIcon.sprite == null)
            {
                _gearIcon.sprite = Resources.Load<Sprite>(GearSpriteResourcePath);
            }

            _gearIcon.color = Color.white;
        }

        private void EnsureModalHierarchy()
        {
            _modalRoot = ResolveOrCreateRect(
                _modalRoot,
                "ModalRoot",
                _safeAreaRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                out var modalCreated);

            var backdropImage = ResolveOrCreateImage(
                "Backdrop",
                _modalRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.005f, 0.012f, 0.018f, 0.72f),
                out _);
            _backdropButton = backdropImage.GetComponent<Button>();
            if (_backdropButton == null)
            {
                _backdropButton = backdropImage.gameObject.AddComponent<Button>();
            }

            _backdropButton.targetGraphic = backdropImage;

            _mainPanel = ResolveOrCreatePanel(
                _mainPanel,
                "MainPanel",
                _modalRoot,
                new Vector2(620f, 620f),
                new Color(0.025f, 0.065f, 0.085f, 0.99f));
            _mainTitleText = ResolveOrCreateText(
                _mainTitleText,
                "TitleText",
                _mainPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(420f, 70f),
                new Vector2(0f, -42f),
                36,
                FontStyle.Bold,
                "게임 메뉴");
            _closeButton = ResolveOrCreateButton(
                _closeButton,
                "CloseButton",
                _mainPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(62f, 62f),
                new Vector2(-18f, -18f),
                new Color(0.12f, 0.17f, 0.2f, 1f),
                out var closeLabel);
            ConfigureLabel(closeLabel, "X", 26);

            var stack = ResolveOrCreateRect(
                null,
                "MainButtonStack",
                _mainPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(480f, 410f),
                new Vector2(0f, -142f),
                out _);
            var layout = stack.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 18f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            _surrenderButton = ResolveOrCreateStackButton(
                _surrenderButton,
                "SurrenderButton",
                stack,
                new Color(0.65f, 0.055f, 0.045f, 1f),
                out _surrenderButtonLabel);
            ConfigureLabel(_surrenderButtonLabel, "게임 항복", 28);
            _settingsButton = ResolveOrCreateStackButton(
                _settingsButton,
                "SettingsButton",
                stack,
                new Color(0.05f, 0.3f, 0.4f, 1f),
                out _settingsButtonLabel);
            ConfigureLabel(_settingsButtonLabel, "설정", 28);
            RemoveObsoleteChild(stack, "AccountButton");
            _logoutButton = ResolveOrCreateStackButton(
                _logoutButton,
                "LogoutButton",
                stack,
                new Color(0.34f, 0.17f, 0.08f, 1f),
                out _logoutButtonLabel);
            ConfigureLabel(_logoutButtonLabel, "로그아웃", 28);
            _quitButton = ResolveOrCreateStackButton(
                _quitButton,
                "QuitButton",
                stack,
                new Color(0.16f, 0.19f, 0.21f, 1f),
                out _quitButtonLabel);
            ConfigureLabel(_quitButtonLabel, "게임 종료", 28);

            EnsureAudioPanel();
            EnsureConfirmationPanel();

            if (modalCreated)
            {
                _modalRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureAudioPanel()
        {
            _audioPanel = ResolveOrCreatePanel(
                _audioPanel,
                "AudioPanel",
                _modalRoot,
                new Vector2(720f, 560f),
                new Color(0.025f, 0.065f, 0.085f, 0.99f));
            ResolveOrCreateText(
                null,
                "TitleText",
                _audioPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(500f, 70f),
                new Vector2(0f, -46f),
                36,
                FontStyle.Bold,
                "오디오 설정");
            ResolveOrCreateText(
                null,
                "MasterVolumeLabel",
                _audioPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(300f, 48f),
                new Vector2(72f, -150f),
                26,
                FontStyle.Bold,
                "마스터 볼륨",
                TextAnchor.MiddleLeft);
            _masterVolumeSlider = ResolveOrCreateSlider(
                _masterVolumeSlider,
                "MasterVolumeSlider",
                _audioPanel,
                new Vector2(0f, -218f));
            _masterVolumeValueText = ResolveOrCreateText(
                _masterVolumeValueText,
                "MasterVolumeValueText",
                _audioPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(110f, 48f),
                new Vector2(-64f, -150f),
                25,
                FontStyle.Bold,
                "100%");

            ResolveOrCreateText(
                null,
                "MusicVolumeLabel",
                _audioPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(300f, 48f),
                new Vector2(72f, -296f),
                26,
                FontStyle.Bold,
                "배경음악 볼륨",
                TextAnchor.MiddleLeft);
            _musicVolumeSlider = ResolveOrCreateSlider(
                _musicVolumeSlider,
                "MusicVolumeSlider",
                _audioPanel,
                new Vector2(0f, -364f));
            _musicVolumeValueText = ResolveOrCreateText(
                _musicVolumeValueText,
                "MusicVolumeValueText",
                _audioPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(110f, 48f),
                new Vector2(-64f, -296f),
                25,
                FontStyle.Bold,
                "100%");

            _audioBackButton = ResolveOrCreateButton(
                _audioBackButton,
                "BackButton",
                _audioPanel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(300f, 68f),
                new Vector2(0f, 38f),
                new Color(0.06f, 0.31f, 0.4f, 1f),
                out var backLabel);
            ConfigureLabel(backLabel, "뒤로", 27);
            _audioPanel.gameObject.SetActive(false);
        }

        private void EnsureConfirmationPanel()
        {
            _confirmationPanel = ResolveOrCreatePanel(
                _confirmationPanel,
                "ConfirmationPanel",
                _modalRoot,
                new Vector2(680f, 390f),
                new Color(0.035f, 0.06f, 0.075f, 1f));
            _confirmationTitleText = ResolveOrCreateText(
                _confirmationTitleText,
                "TitleText",
                _confirmationPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(540f, 64f),
                new Vector2(0f, -44f),
                34,
                FontStyle.Bold,
                "확인");
            _confirmationMessageText = ResolveOrCreateText(
                _confirmationMessageText,
                "MessageText",
                _confirmationPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(560f, 150f),
                new Vector2(0f, 22f),
                25,
                FontStyle.Normal,
                string.Empty);
            _confirmationCancelButton = ResolveOrCreateButton(
                _confirmationCancelButton,
                "CancelButton",
                _confirmationPanel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(250f, 68f),
                new Vector2(-142f, 34f),
                new Color(0.15f, 0.18f, 0.2f, 1f),
                out _confirmationCancelButtonLabel);
            ConfigureLabel(_confirmationCancelButtonLabel, "취소", 26);
            _confirmationAcceptButton = ResolveOrCreateButton(
                _confirmationAcceptButton,
                "AcceptButton",
                _confirmationPanel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(250f, 68f),
                new Vector2(142f, 34f),
                new Color(0.58f, 0.075f, 0.055f, 1f),
                out _confirmationAcceptButtonLabel);
            ConfigureLabel(_confirmationAcceptButtonLabel, "확인", 26);
            _confirmationPanel.gameObject.SetActive(false);
        }

        private void BindUi()
        {
            BindButton(_gearButton, OpenMenu);
            BindButton(_backdropButton, CloseMenu);
            BindButton(_closeButton, CloseMenu);
            BindButton(_settingsButton, OpenAudioSettings);
            BindButton(_logoutButton, RequestLogout);
            BindButton(_quitButton, RequestQuit);
            BindButton(_surrenderButton, RequestSurrender);
            BindButton(_audioBackButton, ReturnToMainMenu);
            BindButton(_confirmationCancelButton, ReturnToMainMenu);
            BindButton(_confirmationAcceptButton, AcceptConfirmation);

            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
                _masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
            }

            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
                _musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
            }
        }

        private void RefreshContextButtons()
        {
            var isBattleScene = string.Equals(_sceneName, "Battle_VSlice", StringComparison.Ordinal);
            var battleBootstrapper = isBattleScene
                ? FindSceneComponent<BattleBootstrapper>(gameObject.scene)
                : null;
            var canShowSurrender = battleBootstrapper != null && battleBootstrapper.CanPlayerSurrender;
            SetActive(_surrenderButton == null ? null : _surrenderButton.transform as RectTransform, canShowSurrender);
            if (_surrenderButton != null)
            {
                _surrenderButton.interactable = canShowSurrender && battleBootstrapper.CanSendPlayerSurrender;
            }

            var isGameStartScene = string.Equals(_sceneName, "GameStart_VSlice", StringComparison.Ordinal);
            var gameStartController = isGameStartScene
                ? FindSceneComponent<GameStartSceneController>(gameObject.scene)
                : null;
            var canShowLogout = gameStartController != null && gameStartController.IsAccountLoggedIn;
            SetActive(_logoutButton == null ? null : _logoutButton.transform as RectTransform, canShowLogout);
            if (_logoutButton != null)
            {
                _logoutButton.interactable = canShowLogout && gameStartController.CanLogoutAccount;
            }
        }

        private void OpenAudioSettings()
        {
            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.SetValueWithoutNotify(After333AudioSettings.MasterVolume);
            }

            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.SetValueWithoutNotify(After333AudioSettings.MusicVolume);
            }

            RefreshVolumeLabels();
            ShowOnly(_audioPanel);
        }

        private void ReturnToMainMenu()
        {
            _pendingConfirmationAction = null;
            After333AudioSettings.Save();
            RefreshContextButtons();
            ShowOnly(_mainPanel);
        }

        private void HandleMasterVolumeChanged(float value)
        {
            After333AudioSettings.SetMasterVolume(value);
            RefreshVolumeLabels();
        }

        private void HandleMusicVolumeChanged(float value)
        {
            After333AudioSettings.SetMusicVolume(value);
            RefreshVolumeLabels();
        }

        private void RefreshVolumeLabels()
        {
            if (_masterVolumeValueText != null)
            {
                _masterVolumeValueText.text = $"{Mathf.RoundToInt(After333AudioSettings.MasterVolume * 100f)}%";
            }

            if (_musicVolumeValueText != null)
            {
                _musicVolumeValueText.text = $"{Mathf.RoundToInt(After333AudioSettings.MusicVolume * 100f)}%";
            }
        }

        private void RequestSurrender()
        {
            var currentBattle = FindSceneComponent<BattleBootstrapper>(gameObject.scene);
            if (currentBattle == null || !currentBattle.CanPlayerSurrender)
            {
                ShowNotice("항복 요청 실패", "현재 진행 중인 전투가 없습니다.");
                return;
            }

            ShowConfirmation(
                "게임 항복",
                "정말 항복하시겠습니까?\n이 전투는 패배로 기록됩니다.",
                "항복",
                () =>
                {
                    var bootstrapper = FindSceneComponent<BattleBootstrapper>(gameObject.scene);
                    if (bootstrapper != null && bootstrapper.TrySurrenderPlayer(out _))
                    {
                        CloseMenu();
                        return;
                    }

                    ShowNotice("항복 요청 실패", "현재 전투에 항복 요청을 보낼 수 없습니다.");
                });
        }

        private void RequestLogout()
        {
            var gameStartController = FindSceneComponent<GameStartSceneController>(gameObject.scene);
            if (gameStartController == null || !gameStartController.IsAccountLoggedIn)
            {
                ShowNotice("로그아웃 실패", "현재 로그인된 계정이 없습니다.");
                return;
            }

            if (!gameStartController.CanLogoutAccount)
            {
                ShowNotice("로그아웃 대기", "현재 계정 작업이 끝난 뒤 다시 시도해주세요.");
                return;
            }

            ShowConfirmation(
                "로그아웃",
                "현재 계정에서 로그아웃하시겠습니까?\n다시 플레이하려면 로그인이 필요합니다.",
                "로그아웃",
                () =>
                {
                    var controller = FindSceneComponent<GameStartSceneController>(gameObject.scene);
                    CloseMenu();
                    controller?.LogoutAccountFromSettingsMenu();
                });
        }

        private void RequestQuit()
        {
            var message = string.Equals(_sceneName, "Battle_VSlice", StringComparison.Ordinal)
                ? "게임을 종료하시겠습니까?\n진행 중인 온라인 전투는 재접속 대기 상태가 됩니다."
                : "게임을 종료하시겠습니까?";
            ShowConfirmation("게임 종료", message, "종료", QuitApplication);
        }

        private void ShowConfirmation(string title, string message, string acceptLabel, Action action)
        {
            _pendingConfirmationAction = action;
            if (_confirmationTitleText != null)
            {
                _confirmationTitleText.text = title;
            }

            if (_confirmationMessageText != null)
            {
                _confirmationMessageText.text = message;
            }

            ConfigureLabel(_confirmationAcceptButtonLabel, acceptLabel, 26);
            SetActive(_confirmationCancelButton == null ? null : _confirmationCancelButton.transform as RectTransform, true);
            ShowOnly(_confirmationPanel);
        }

        private void ShowNotice(string title, string message)
        {
            _pendingConfirmationAction = ReturnToMainMenu;
            if (_confirmationTitleText != null)
            {
                _confirmationTitleText.text = title;
            }

            if (_confirmationMessageText != null)
            {
                _confirmationMessageText.text = message;
            }

            ConfigureLabel(_confirmationAcceptButtonLabel, "확인", 26);
            SetActive(_confirmationCancelButton == null ? null : _confirmationCancelButton.transform as RectTransform, false);
            ShowOnly(_confirmationPanel);
        }

        private void AcceptConfirmation()
        {
            var action = _pendingConfirmationAction;
            _pendingConfirmationAction = null;
            action?.Invoke();
        }

        private void ShowOnly(RectTransform panel)
        {
            SetActive(_mainPanel, panel == _mainPanel);
            SetActive(_audioPanel, panel == _audioPanel);
            SetActive(_confirmationPanel, panel == _confirmationPanel);
        }

        private void ClearBattleSelection()
        {
            FindSceneComponent<BattlePlayerInputController>(gameObject.scene)?.ClearSelection();
        }

        private static void QuitApplication()
        {
            After333AudioSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private static void SetActive(RectTransform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void BindButton(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            var components = Resources.FindObjectsOfTypeAll<T>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.scene == scene)
                {
                    return components[i];
                }
            }

            return null;
        }

        private static T ResolveOrAddComponent<T>(T current, GameObject owner) where T : Component
        {
            if (current != null)
            {
                return current;
            }

            current = owner.GetComponent<T>();
            return current == null ? owner.AddComponent<T>() : current;
        }

        private static RectTransform ResolveOrCreateRect(
            RectTransform current,
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            out bool created)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found;
            }

            created = current == null;
            if (!created)
            {
                return current;
            }

            var child = new GameObject(objectName, typeof(RectTransform));
            current = child.GetComponent<RectTransform>();
            current.SetParent(parent, false);
            current.anchorMin = anchorMin;
            current.anchorMax = anchorMax;
            current.pivot = ResolvePivot(anchorMin, anchorMax);
            current.sizeDelta = size;
            current.anchoredPosition = position;
            return current;
        }

        private static RectTransform ResolveOrCreatePanel(
            RectTransform current,
            string objectName,
            Transform parent,
            Vector2 size,
            Color color)
        {
            current = ResolveOrCreateRect(
                current,
                objectName,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                Vector2.zero,
                out var created);
            var image = current.GetComponent<Image>();
            if (image == null)
            {
                image = current.gameObject.AddComponent<Image>();
                image.color = color;
            }
            else if (created)
            {
                image.color = color;
            }

            return current;
        }

        private static Image ResolveOrCreateImage(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color,
            out bool created)
        {
            Image image = null;
            if (parent.Find(objectName) is RectTransform found)
            {
                image = found.GetComponent<Image>();
            }

            created = image == null;
            if (!created)
            {
                return image;
            }

            var child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button ResolveOrCreateStackButton(
            Button current,
            string objectName,
            Transform parent,
            Color color,
            out Text label)
        {
            current = ResolveOrCreateButton(
                current,
                objectName,
                parent,
                Vector2.zero,
                Vector2.one,
                new Vector2(440f, 72f),
                Vector2.zero,
                color,
                out label);
            var layoutElement = current.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = current.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 72f;
                layoutElement.minHeight = 72f;
            }

            return current;
        }

        private static Button ResolveOrCreateButton(
            Button current,
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text label)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Button>();
            }

            if (current != null)
            {
                label = current.GetComponentInChildren<Text>(true);
                return current;
            }

            var child = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = ResolvePivot(anchorMin, anchorMax);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = child.GetComponent<Image>();
            image.color = color;
            current = child.GetComponent<Button>();
            current.targetGraphic = image;
            var colors = current.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            current.colors = colors;
            label = CreateButtonLabel(rect);
            return current;
        }

        private static Slider ResolveOrCreateSlider(
            Slider current,
            string objectName,
            Transform parent,
            Vector2 position)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Slider>();
            }

            if (current != null)
            {
                return current;
            }

            var sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.SetParent(parent, false);
            sliderRect.anchorMin = new Vector2(0.5f, 1f);
            sliderRect.anchorMax = new Vector2(0.5f, 1f);
            sliderRect.pivot = new Vector2(0.5f, 1f);
            sliderRect.sizeDelta = new Vector2(576f, 52f);
            sliderRect.anchoredPosition = position;

            var background = ResolveOrCreateImage(
                "Background",
                sliderRect,
                new Vector2(0f, 0.35f),
                new Vector2(1f, 0.65f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.08f, 0.12f, 0.14f, 1f),
                out _);
            var fillArea = ResolveOrCreateRect(
                null,
                "Fill Area",
                sliderRect,
                new Vector2(0f, 0.35f),
                new Vector2(1f, 0.65f),
                new Vector2(-28f, 0f),
                new Vector2(0f, 0f),
                out _);
            fillArea.offsetMin = new Vector2(14f, 0f);
            fillArea.offsetMax = new Vector2(-14f, 0f);
            var fill = ResolveOrCreateImage(
                "Fill",
                fillArea,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.65f, 0.72f, 1f),
                out _);
            var handleArea = ResolveOrCreateRect(
                null,
                "Handle Slide Area",
                sliderRect,
                Vector2.zero,
                Vector2.one,
                new Vector2(-30f, 0f),
                Vector2.zero,
                out _);
            handleArea.offsetMin = new Vector2(15f, 0f);
            handleArea.offsetMax = new Vector2(-15f, 0f);
            var handle = ResolveOrCreateImage(
                "Handle",
                handleArea,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-18f, -18f),
                new Vector2(18f, 18f),
                new Color(0.93f, 0.78f, 0.28f, 1f),
                out _);

            current = sliderObject.GetComponent<Slider>();
            current.minValue = 0f;
            current.maxValue = 1f;
            current.wholeNumbers = false;
            current.fillRect = fill.rectTransform;
            current.handleRect = handle.rectTransform;
            current.targetGraphic = handle;
            current.direction = Slider.Direction.LeftToRight;
            background.raycastTarget = true;
            return current;
        }

        private static Text ResolveOrCreateText(
            Text current,
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            int fontSize,
            FontStyle fontStyle,
            string value,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Text>();
            }

            if (current != null)
            {
                return current;
            }

            var child = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = ResolvePivot(anchorMin, anchorMax);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            current = child.GetComponent<Text>();
            current.font = ResolveRuntimeFont();
            current.fontSize = fontSize;
            current.fontStyle = fontStyle;
            current.color = Color.white;
            current.alignment = alignment;
            current.horizontalOverflow = HorizontalWrapMode.Wrap;
            current.verticalOverflow = VerticalWrapMode.Truncate;
            current.raycastTarget = false;
            current.text = value;
            return current;
        }

        private static Text CreateButtonLabel(RectTransform parent)
        {
            var child = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetFullStretch(rect);
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            var label = child.GetComponent<Text>();
            label.font = ResolveRuntimeFont();
            label.fontSize = 26;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static void ConfigureLabel(Text label, string value, int fontSize)
        {
            if (label == null)
            {
                return;
            }

            label.text = value;
            if (label.font == null)
            {
                label.font = ResolveRuntimeFont();
            }

            if (label.fontSize <= 0)
            {
                label.fontSize = fontSize;
            }
        }

        private static void RemoveObsoleteChild(Transform parent, string objectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            var obsoleteChild = parent.Find(objectName);
            if (obsoleteChild == null)
            {
                return;
            }

            obsoleteChild.gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obsoleteChild.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obsoleteChild.gameObject);
            }
        }

        private static Vector2 ResolvePivot(Vector2 anchorMin, Vector2 anchorMax)
        {
            if (anchorMin == anchorMax)
            {
                return anchorMin;
            }

            return new Vector2(0.5f, 0.5f);
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font ResolveRuntimeFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" },
                    32);
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
