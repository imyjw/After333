using System;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Development;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Startup
{
    public sealed class GameStartSceneController : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int _pixelMenuLayoutVersion = 0;
        public int PixelMenuLayoutVersion => _pixelMenuLayoutVersion;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Sprite _backgroundSprite;
        [SerializeField] private string _backgroundResourcePath = "Project333/StartScene/GameStartBackground";
        [SerializeField] private Image _startButtonImage;
        [SerializeField] private Sprite _startButtonSprite;
        [SerializeField] private string _startButtonResourcePath = "Project333/StartScene/GameStartButton";
        [SerializeField] private Text _ticketText;
        [SerializeField] private Text _statusText;
        [Header("Account Login Gate")]
        [SerializeField] private AccountLoginGateView _accountLoginGate;
        [SerializeField] private RectTransform _accountInfoPanel;
        [SerializeField] private Text _accountInfoText;
        [SerializeField] private Vector2 _accountInfoPanelSize = new Vector2(560f, 148f);
        [SerializeField] private Vector2 _accountInfoPanelPosition = new Vector2(32f, -32f);
        [SerializeField] private Color _accountInfoPanelColor = new Color(0f, 0f, 0f, 0.62f);
        [SerializeField] private Color _accountInfoTextColor = Color.white;
        [SerializeField] private int _accountInfoFontSize = 24;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Text _startGameButtonLabel;
        [FormerlySerializedAs("_purchaseTicketButton")]
        [SerializeField] private Button _shopButton;
        [FormerlySerializedAs("_purchaseTicketButtonLabel")]
        [SerializeField] private Text _shopButtonLabel;
        [FormerlySerializedAs("_purchaseTicketButtonSize")]
        [SerializeField] private Vector2 _shopButtonSize = new Vector2(280f, 72f);
        [FormerlySerializedAs("_purchaseTicketButtonPosition")]
        [SerializeField] private Vector2 _shopButtonPosition = new Vector2(0f, -220f);
        [FormerlySerializedAs("_purchaseTicketButtonColor")]
        [SerializeField] private Color _shopButtonColor = new Color(0.03f, 0.32f, 0.46f, 0.92f);
        [FormerlySerializedAs("_purchaseTicketButtonTextColor")]
        [SerializeField] private Color _shopButtonTextColor = Color.white;
        [FormerlySerializedAs("_purchaseTicketButtonFontSize")]
        [SerializeField] private int _shopButtonFontSize = 26;
        [SerializeField] private Button _ownedCardsButton;
        [SerializeField] private Text _ownedCardsButtonLabel;
        [SerializeField] private bool _applyOwnedCardsButtonLayout;
        [SerializeField] private Vector2 _ownedCardsButtonSize = new Vector2(280f, 64f);
        [SerializeField] private Vector2 _ownedCardsButtonPosition = new Vector2(0f, -304f);
        [SerializeField] private Color _ownedCardsButtonColor = new Color(0.16f, 0.2f, 0.32f, 0.94f);
        [SerializeField] private Color _ownedCardsButtonTextColor = Color.white;
        [SerializeField] private int _ownedCardsButtonFontSize = 24;
        [SerializeField] private Button _reconnectPvpButton;
        [SerializeField] private Text _reconnectPvpButtonLabel;
        [SerializeField] private bool _applyReconnectPvpButtonLayout;
        [SerializeField] private Vector2 _reconnectPvpButtonSize = new Vector2(360f, 64f);
        [SerializeField] private Vector2 _reconnectPvpButtonPosition = new Vector2(0f, -384f);
        [SerializeField] private Color _reconnectPvpButtonColor = new Color(0.42f, 0.12f, 0.08f, 0.96f);
        [SerializeField] private Color _reconnectPvpButtonTextColor = Color.white;
        [SerializeField] private int _reconnectPvpButtonFontSize = 24;
        [Header("Game ID Auth")]
        [SerializeField] private RectTransform _gameIdAuthPanel;
        [SerializeField] private Text _gameIdAuthTitleText;
        [SerializeField] private InputField _gameIdInput;
        [SerializeField] private InputField _gameIdPasswordInput;
        [SerializeField] private InputField _gameIdDisplayNameInput;
        [SerializeField] private Button _gameIdRegisterButton;
        [SerializeField] private Text _gameIdRegisterButtonLabel;
        [SerializeField] private Button _gameIdLoginButton;
        [SerializeField] private Text _gameIdLoginButtonLabel;
        [SerializeField] private Button _gameIdLinkButton;
        [SerializeField] private Text _gameIdLinkButtonLabel;
        [SerializeField] private Button _gameIdLogoutButton;
        [SerializeField] private Text _gameIdLogoutButtonLabel;
        [SerializeField] private Vector2 _gameIdAuthPanelSize = new Vector2(520f, 340f);
        [SerializeField] private Vector2 _gameIdAuthPanelPosition = new Vector2(-32f, -32f);
        [SerializeField] private Color _gameIdAuthPanelColor = new Color(0.02f, 0.025f, 0.04f, 0.78f);
        [SerializeField] private Color _gameIdAuthFieldColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color _gameIdAuthButtonColor = new Color(0.12f, 0.28f, 0.38f, 0.95f);
        [SerializeField] private Color _gameIdAuthTextColor = Color.white;
        [SerializeField] private Color _gameIdAuthInputTextColor = new Color(0.06f, 0.06f, 0.08f, 1f);
        [SerializeField] private int _gameIdAuthFontSize = 22;
        [Header("Google Auth")]
        [SerializeField] private RectTransform _googleAuthPanel;
        [SerializeField] private Text _googleAuthTitleText;
        [SerializeField] private Button _googleLoginButton;
        [SerializeField] private Text _googleLoginButtonLabel;
        [SerializeField] private Button _googleLinkButton;
        [SerializeField] private Text _googleLinkButtonLabel;
        [Tooltip("Google Cloud Console에서 만든 Desktop app OAuth Client ID입니다. 비밀 값이 아닙니다.")]
        [SerializeField] private string _googleDesktopClientId = string.Empty;
        [Tooltip("Android Credential Manager가 ID 토큰의 audience로 사용할 Web application OAuth Client ID입니다. Android OAuth Client ID를 넣는 칸이 아닙니다.")]
        [SerializeField] private string _googleAndroidServerClientId = string.Empty;
        [Tooltip("저장된 After333 세션이 없을 때 Android Credential Manager가 승인된 Google 계정의 자동 로그인을 한 번 시도합니다.")]
        [SerializeField] private bool _tryAutomaticGoogleLoginOnAndroid = true;
        [SerializeField] private Vector2 _googleAuthPanelSize = new Vector2(520f, 150f);
        [SerializeField] private Vector2 _googleAuthPanelPosition = new Vector2(-32f, -396f);
        [SerializeField] private Color _googleAuthPanelColor = new Color(0.02f, 0.025f, 0.04f, 0.78f);
        [SerializeField] private Color _googleAuthButtonColor = new Color(0.12f, 0.28f, 0.38f, 0.95f);
        [SerializeField] private Color _googleAuthTextColor = Color.white;
        [SerializeField] private int _googleAuthFontSize = 21;
        [Header("Server Endpoint")]
        [SerializeField] private RectTransform _serverSettingsPanel;
        [SerializeField] private Text _serverSettingsTitleText;
        [SerializeField] private InputField _serverHttpUrlInput;
        [SerializeField] private Button _serverLocalButton;
        [SerializeField] private Text _serverLocalButtonLabel;
        [SerializeField] private Button _serverTestButton;
        [SerializeField] private Text _serverTestButtonLabel;
        [SerializeField] private Button _serverApplyButton;
        [SerializeField] private Text _serverApplyButtonLabel;
        [SerializeField] private Button _serverResetButton;
        [SerializeField] private Text _serverResetButtonLabel;
        [SerializeField] private Vector2 _serverSettingsPanelSize = new Vector2(560f, 210f);
        [SerializeField] private Vector2 _serverSettingsPanelPosition = new Vector2(32f, -380f);
        [SerializeField] private string _localServerHttpUrl =
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.LocalHttpUrl;
        [SerializeField] private string _testServerHttpUrl =
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.PublicHttpUrl;
        [SerializeField] private Color _serverSettingsPanelColor = new Color(0.02f, 0.035f, 0.052f, 0.78f);
        [SerializeField] private Color _serverSettingsFieldColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color _serverSettingsButtonColor = new Color(0.14f, 0.26f, 0.34f, 0.96f);
        [SerializeField] private Color _serverSettingsTextColor = Color.white;
        [SerializeField] private Color _serverSettingsInputTextColor = new Color(0.06f, 0.06f, 0.08f, 1f);
        [SerializeField] private int _serverSettingsFontSize = 20;
        [Header("Development Smoke Panel")]
        [SerializeField] private bool _showDevelopmentSmokePanel;
        [SerializeField] private Vector2 _developmentSmokePanelAnchor = new Vector2(0f, 1f);
        [SerializeField] private Vector2 _developmentSmokePanelOffset = new Vector2(32f, -168f);
        [SerializeField] private Vector2 _developmentSmokePanelSize = new Vector2(560f, 188f);
        [SerializeField] private Vector2 _developmentSmokePanelTextPadding = new Vector2(18f, 12f);
        [SerializeField] private int _developmentSmokePanelFontSize = 20;
        [SerializeField] private int _developmentSmokePanelSortingOrder = 3550;
        [SerializeField] private Color _developmentSmokePanelColor = new Color(0.03f, 0.055f, 0.08f, 0.84f);
        [SerializeField] private Color _developmentSmokePanelTextColor = new Color(0.78f, 0.95f, 1f, 1f);
        [SerializeField] private string _accountServerUrl =
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.LocalHttpUrl;
        [SerializeField] private string _clientVersion =
            Project333.Runtime.Presentation.Project333ClientBuildInfo.CurrentClientVersion;
        [SerializeField] private bool _useServerAccount = true;
        [SerializeField] private string _draftSceneName = "Draft_VSlice";
        [SerializeField] private string _deckBuildingSceneName = "DeckBuilding_VSlice";
        [SerializeField] private string _battleSceneName = "Battle_VSlice";
        [SerializeField] private string _ownedCardsSceneName = "OwnedCards_VSlice";
        [SerializeField] private string _shopSceneName = "Shop_VSlice";
        [SerializeField] private int _ticketCost = 3;

        private CancellationTokenSource _authCancellation;
        private CancellationTokenSource _gameIdAuthCancellation;
        private CancellationTokenSource _googleAuthCancellation;
        private CancellationTokenSource _logoutCancellation;
        private CancellationTokenSource _runStartCancellation;
        private bool _isAuthenticating;
        private bool _isGameIdAuthBusy;
        private bool _isGoogleAuthBusy;
        private bool _isLoggingOut;
        private bool _isStartingRun;
        private bool _authFailed;
        private string _accountLoginGateStatus = "로그인 방법을 선택해주세요.";
        private ServerStatusResponse _serverStatus;
        private string _serverStatusError = string.Empty;
        private ServerAccountSmokePanel _developmentSmokePanel;
        private bool _accountInfoPanelNeedsDefaultLayout;
        private bool _accountInfoTextNeedsDefaultLayout;
        private bool _shopButtonNeedsDefaultLayout;
        private bool _shopButtonLabelNeedsDefaultLayout;
        private bool _ownedCardsButtonNeedsDefaultLayout;
        private bool _ownedCardsButtonLabelNeedsDefaultLayout;
        private bool _reconnectPvpButtonNeedsDefaultLayout;
        private bool _reconnectPvpButtonLabelNeedsDefaultLayout;
        private bool _hasPvpReconnectableBattle;
        private string _pvpReconnectMatchId = string.Empty;
        private int _pvpReconnectRemainingSeconds;
        private float _pvpReconnectDeadlineRealtime;
        private int _lastPresentedPvpReconnectRemainingSeconds = -1;
        private bool _gameIdAuthPanelNeedsDefaultLayout;
        private bool _gameIdAuthTitleNeedsDefaultLayout;
        private bool _gameIdInputNeedsDefaultLayout;
        private bool _gameIdPasswordInputNeedsDefaultLayout;
        private bool _gameIdDisplayNameInputNeedsDefaultLayout;
        private bool _gameIdRegisterButtonNeedsDefaultLayout;
        private bool _gameIdLoginButtonNeedsDefaultLayout;
        private bool _googleAuthPanelNeedsDefaultLayout;
        private bool _googleAuthTitleNeedsDefaultLayout;
        private bool _googleLoginButtonNeedsDefaultLayout;
        private bool _serverSettingsPanelNeedsDefaultLayout;
        private bool _serverSettingsTitleNeedsDefaultLayout;
        private bool _serverHttpUrlInputNeedsDefaultLayout;
        private bool _serverLocalButtonNeedsDefaultLayout;
        private bool _serverTestButtonNeedsDefaultLayout;
        private bool _serverApplyButtonNeedsDefaultLayout;
        private bool _serverResetButtonNeedsDefaultLayout;
        private bool _hasLoadedSavedAccountCredentials;
#if UNITY_EDITOR
        private bool _hasQueuedEditorStartUiRefresh;
#endif

        private string AccountServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);

        private string ResolvedClientVersion =>
            Project333.Runtime.Presentation.Project333ClientBuildInfo.ResolveClientVersion(_clientVersion);

        private bool IsAccountAuthBusy => _isGameIdAuthBusy || _isGoogleAuthBusy || _isLoggingOut;

        public bool IsAccountLoggedIn => AccountSessionState.IsAuthenticated;

        public bool CanLogoutAccount =>
            _useServerAccount &&
            AccountSessionState.IsAuthenticated &&
            !_isAuthenticating &&
            !_isGameIdAuthBusy &&
            !_isGoogleAuthBusy &&
            !_isLoggingOut &&
            !_isStartingRun;

        private void Awake()
        {
            _useServerAccount = true;
            LoadSavedAccountCredentials();
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            AutoAssignAccountInfoPanel();
            AutoAssignShopButton();
            AutoAssignOwnedCardsButton();
            AutoAssignReconnectPvpButton();
            AutoAssignAccountLoginGate();
            AutoAssignServerSettingsPanel();
            RemoveLegacyAccountManagementPanels();
            EnsureAccountInfoPanel();
            EnsureShopButton();
            EnsureOwnedCardsButton();
            EnsureReconnectPvpButton();
            EnsureAccountLoginGate();
            EnsureServerSettingsPanel();
            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
            ApplyAccountInfoVisual();
            ApplyShopButtonVisual();
            ApplyOwnedCardsButtonVisual();
            ApplyReconnectPvpButtonVisual();
            ApplyServerSettingsPanelVisual();
            BindAccountLoginGate();
            EnsureDevelopmentSmokePanel();
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            RefreshUi();
        }

        private void OnEnable()
        {
            _useServerAccount = true;
            LoadSavedAccountCredentials();
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            AutoAssignAccountInfoPanel();
            AutoAssignShopButton();
            AutoAssignOwnedCardsButton();
            AutoAssignReconnectPvpButton();
            AutoAssignAccountLoginGate();
            AutoAssignServerSettingsPanel();
            RemoveLegacyAccountManagementPanels();
            EnsureAccountInfoPanel();
            EnsureShopButton();
            EnsureOwnedCardsButton();
            EnsureReconnectPvpButton();
            EnsureAccountLoginGate();
            EnsureServerSettingsPanel();
            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
            ApplyAccountInfoVisual();
            ApplyShopButtonVisual();
            ApplyOwnedCardsButtonVisual();
            ApplyReconnectPvpButtonVisual();
            ApplyServerSettingsPanelVisual();
            BindAccountLoginGate();
            EnsureDevelopmentSmokePanel();
            StartAccountRestoreIfNeeded();
            RefreshUi();
        }

        private void OnDisable()
        {
            CancelAccountRestore();
            CancelGameIdAuth();
            CancelGoogleAuth();
            CancelLogout();
            CancelRunStart();
        }

        private void Update()
        {
            UpdatePvpReconnectCountdown();
        }

        private void UpdatePvpReconnectCountdown()
        {
            if (!_hasPvpReconnectableBattle || _pvpReconnectDeadlineRealtime <= 0f)
            {
                return;
            }

            var remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(_pvpReconnectDeadlineRealtime - UnityEngine.Time.unscaledTime));
            if (remainingSeconds == _lastPresentedPvpReconnectRemainingSeconds)
            {
                return;
            }

            _lastPresentedPvpReconnectRemainingSeconds = remainingSeconds;
            _pvpReconnectRemainingSeconds = remainingSeconds;

            if (remainingSeconds <= 0)
            {
                ClearPvpReconnectStatus();
                SetStatus("PVP 재접속 시간이 만료되었습니다. 새 전투를 시작해 주세요.");
            }

            RefreshUi();
        }

        private void OnDestroy()
        {
            CancelAccountRestore();
            CancelGameIdAuth();
            CancelGoogleAuth();
            CancelLogout();
            CancelRunStart();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorStartUiRefresh();
#else
            MaterializeMissingStartUiObjects();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorStartUiRefresh()
        {
            if (_hasQueuedEditorStartUiRefresh)
            {
                return;
            }

            _hasQueuedEditorStartUiRefresh = true;
            EditorApplication.delayCall += MaterializeMissingStartUiObjectsAfterValidation;
        }

        private void MaterializeMissingStartUiObjectsAfterValidation()
        {
            _hasQueuedEditorStartUiRefresh = false;
            if (this == null)
            {
                return;
            }

            MaterializeMissingStartUiObjects();
        }
#endif

        [ContextMenu("Materialize Missing Start UI Objects")]
        private void MaterializeMissingStartUiObjects()
        {
            _useServerAccount = true;
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            AutoAssignAccountInfoPanel();
            AutoAssignShopButton();
            AutoAssignOwnedCardsButton();
            AutoAssignReconnectPvpButton();
            AutoAssignAccountLoginGate();
            AutoAssignServerSettingsPanel();
            RemoveLegacyAccountManagementPanels();
            if (!UnityEngine.Application.isPlaying)
            {
                EnsureAccountInfoPanel();
                EnsureShopButton();
                EnsureOwnedCardsButton();
                EnsureReconnectPvpButton();
                EnsureAccountLoginGate();
                EnsureServerSettingsPanel();
            }

            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
            ApplyAccountInfoVisual();
            ApplyShopButtonVisual();
            ApplyOwnedCardsButtonVisual();
            ApplyReconnectPvpButtonVisual();
            ApplyServerSettingsPanelVisual();
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorUtility.SetDirty(this);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        private void RemoveLegacyAccountManagementPanels()
        {
            RemoveLegacyAccountManagementPanel(ref _gameIdAuthPanel, "GameIdAuthPanel");
            RemoveLegacyAccountManagementPanel(ref _googleAuthPanel, "GoogleAuthPanel");
        }

        private void RemoveLegacyAccountManagementPanel(ref RectTransform panel, string objectName)
        {
            if (panel == null && transform.Find(objectName) is RectTransform foundPanel)
            {
                panel = foundPanel;
            }

            if (panel == null)
            {
                return;
            }

            var panelObject = panel.gameObject;
            panelObject.SetActive(false);
            panel = null;
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(panelObject);
            }
            else
            {
                DestroyImmediate(panelObject);
            }
        }

        public async void StartGameFromUi()
        {
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);

            if (!_useServerAccount)
            {
                SetStatus("서버 계정 모드에서만 게임을 시작할 수 있습니다.");
                RefreshUi();
                return;
            }

            await StartServerDraftRunFromUiAsync();
        }

        public void UseLocalServerEndpointFromUi()
        {
            SetServerHttpUrlInputText(_localServerHttpUrl);
            ApplyServerEndpointFromUi();
        }

        public void UseTestServerEndpointFromUi()
        {
            SetServerHttpUrlInputText(_testServerHttpUrl);
            ApplyServerEndpointFromUi();
        }

        public void ApplyServerEndpointFromUi()
        {
            var requestedUrl = GetInputText(_serverHttpUrlInput);
            if (string.IsNullOrWhiteSpace(requestedUrl))
            {
                SetStatus("서버 주소를 입력해주세요.");
                RefreshUi();
                return;
            }

            var previousUrl = AccountServerUrl;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.SaveHttpUrlOverride(requestedUrl);
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.SaveBattleWebSocketUrlOverride(string.Empty);
            var newUrl = AccountServerUrl;
            SetServerHttpUrlInputText(newUrl);
            HandleServerEndpointChanged(previousUrl, newUrl, $"서버 주소 저장: {newUrl}");
        }

        public void ResetServerEndpointFromUi()
        {
            var previousUrl = AccountServerUrl;
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ClearOverrides();
            var newUrl = AccountServerUrl;
            SetServerHttpUrlInputText(newUrl);
            HandleServerEndpointChanged(previousUrl, newUrl, $"서버 주소 초기화: {newUrl}");
        }

        public async void RegisterGameIdFromUi()
        {
            await RegisterGameIdAsync(
                GetInputText(_gameIdInput),
                GetInputText(_gameIdPasswordInput),
                GetInputText(_gameIdDisplayNameInput));
        }

        private async Task RegisterGameIdAsync(string gameId, string password, string displayName)
        {
            if (!GameIdRegistrationRules.TryNormalizeGameId(
                    gameId,
                    out var normalizedGameId,
                    out var gameIdError))
            {
                SetStatus(gameIdError);
                RefreshUi();
                return;
            }

            if (!GameIdRegistrationRules.TryValidatePassword(password, out var passwordError))
            {
                SetStatus(passwordError);
                RefreshUi();
                return;
            }

            await RunGameIdAuthActionAsync("회원가입", normalizedGameId, password, async (client, cancellationToken) =>
            {
                var response = await client.RegisterGameIdAsync(
                    normalizedGameId,
                    password,
                    displayName,
                    ResolvedClientVersion,
                    cancellationToken);
                AccountSessionState.ApplyGameIdAuthResponse(response);
                await RefreshServerAccountStateAsync(cancellationToken);
                SetStatus($"Game ID 회원가입 완료: {AccountSessionState.DisplayName}");
            });
        }

        public async void LoginGameIdFromUi()
        {
            await LoginGameIdAsync(
                GetInputText(_gameIdInput),
                GetInputText(_gameIdPasswordInput));
        }

        private async Task LoginGameIdAsync(string gameId, string password)
        {
            await RunGameIdAuthActionAsync("로그인", gameId, password, async (client, cancellationToken) =>
            {
                var response = await client.LoginGameIdAsync(
                    gameId,
                    password,
                    ResolvedClientVersion,
                    cancellationToken);
                AccountSessionState.ApplyGameIdAuthResponse(response);
                await RefreshServerAccountStateAsync(cancellationToken);
                SetStatus($"Game ID 로그인 완료: {AccountSessionState.DisplayName}");
            });
        }

        public async void LoginGoogleFromUi()
        {
            await RunGoogleAuthActionAsync();
        }

        public async void LogoutAccountFromSettingsMenu()
        {
            if (!CanLogoutAccount)
            {
                return;
            }

            CancelLogout();
            _logoutCancellation = new CancellationTokenSource();
            var cancellationToken = _logoutCancellation.Token;
            var sessionToken = AccountSessionState.SessionToken;
            var refreshToken = AccountSessionState.RefreshToken;
            _isLoggingOut = true;
            SetStatus("로그아웃 중입니다.");
            RefreshUi();

            try
            {
                var client = new GuestAuthClient(AccountServerUrl);
                await client.LogoutAsync(sessionToken, refreshToken, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 server logout failed; clearing the local session: {ex.Message}");
            }
            finally
            {
                AccountSessionState.ClearSavedSession();
                ClearPvpReconnectStatus();
                _authFailed = false;
                _isLoggingOut = false;
                CancelLogout();
                SetStatus("로그아웃했습니다. 다시 로그인해주세요.");
                RefreshUi();
            }
        }

        private async Task RunGoogleAuthActionAsync()
        {
            if (IsAccountAuthBusy)
            {
                return;
            }

            if (!_useServerAccount)
            {
                SetStatus("서버 계정 모드에서만 Google 로그인을 사용할 수 있습니다.");
                RefreshUi();
                return;
            }

            var providerKind = GoogleIdTokenProviderFactory.CurrentPlatformKind;
            if (providerKind == GoogleIdTokenProviderKind.Unsupported)
            {
                SetStatus("현재 플랫폼에서는 Google 로그인을 사용할 수 없습니다.");
                RefreshUi();
                return;
            }

            var googleClientId = GoogleIdTokenProviderFactory.ResolveClientId(
                providerKind,
                _googleDesktopClientId,
                _googleAndroidServerClientId);
            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                var fieldName = GoogleIdTokenProviderFactory.GetConfigurationFieldName(providerKind);
                SetStatus($"GameStartSceneController의 {fieldName}를 먼저 입력해주세요.");
                RefreshUi();
                return;
            }

            if (!IsGoogleServerConfigured())
            {
                SetStatus(providerKind == GoogleIdTokenProviderKind.WindowsDesktop
                    ? "서버의 Google Desktop Client ID/Secret 설정이 완료되지 않았습니다. 설정 후 서버를 다시 시작해주세요."
                    : "서버에 PROJECT333_GOOGLE_CLIENT_IDS가 설정되지 않았습니다. 설정 후 서버를 다시 시작해주세요.");
                RefreshUi();
                return;
            }

            CancelGoogleAuth();
            _googleAuthCancellation = new CancellationTokenSource();
            _isGoogleAuthBusy = true;
            _authFailed = false;
            var selectionSurface = providerKind == GoogleIdTokenProviderKind.AndroidCredentialManager
                ? "Google 계정 선택창에서"
                : "브라우저에서";
            SetStatus($"{selectionSurface} 로그인할 Google 계정을 선택해주세요.");
            RefreshUi();

            try
            {
                var tokenProvider = GoogleIdTokenProviderFactory.CreateForCurrentPlatform(
                    _googleDesktopClientId,
                    _googleAndroidServerClientId,
                    AccountServerUrl);
                var idToken = await tokenProvider.AcquireIdTokenAsync(_googleAuthCancellation.Token);
                var client = new GuestAuthClient(AccountServerUrl);
                var response = await client.AuthenticateGoogleAsync(
                    idToken,
                    ResolvedClientVersion,
                    _googleAuthCancellation.Token);
                AccountSessionState.ApplyGoogleAuthResponse(response);
                await RefreshServerAccountStateAsync(_googleAuthCancellation.Token);
                SetStatus($"Google 로그인 완료: {AccountSessionState.DisplayName}");

                _authFailed = false;
                Debug.Log($"After333 Google login succeeded: account={AccountSessionState.AccountId}, displayName={AccountSessionState.DisplayName}, kind={AccountSessionState.AccountKind}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 Google login failed: {ex.Message}");
                SetStatus($"Google 로그인 실패: {ex.Message}");
            }
            finally
            {
                _isGoogleAuthBusy = false;
                CancelGoogleAuth();
                RefreshUi();
            }
        }

        private async Task RunGameIdAuthActionAsync(
            string actionName,
            string gameId,
            string password,
            Func<GuestAuthClient, CancellationToken, Task> action)
        {
            if (IsAccountAuthBusy)
            {
                return;
            }

            if (!_useServerAccount)
            {
                SetStatus("서버 계정 모드에서만 Game ID 기능을 사용할 수 있습니다.");
                RefreshUi();
                return;
            }

            if (string.IsNullOrWhiteSpace(gameId) ||
                string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Game ID와 비밀번호를 입력해주세요.");
                RefreshUi();
                return;
            }

            CancelGameIdAuth();
            _gameIdAuthCancellation = new CancellationTokenSource();
            _isGameIdAuthBusy = true;
            _authFailed = false;
            SetStatus($"Game ID {actionName} 처리 중입니다.");
            RefreshUi();

            try
            {
                var client = new GuestAuthClient(AccountServerUrl);
                await action(client, _gameIdAuthCancellation.Token);
                _authFailed = false;
                Debug.Log($"After333 Game ID auth action succeeded: action={actionName}, account={AccountSessionState.AccountId}, displayName={AccountSessionState.DisplayName}, kind={AccountSessionState.AccountKind}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 Game ID auth action failed: action={actionName}, error={ex.Message}");
                SetStatus($"Game ID {actionName} 실패: {ex.Message}");
            }
            finally
            {
                _isGameIdAuthBusy = false;
                CancelGameIdAuth();
                RefreshUi();
            }
        }

        public void OpenOwnedCardsSceneFromUi()
        {
            var sceneName = string.IsNullOrWhiteSpace(_ownedCardsSceneName)
                ? "OwnedCards_VSlice"
                : _ownedCardsSceneName;

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetStatus("보유 카드 씬이 아직 생성되지 않았습니다. Tools > Project333 > Owned Cards > Create Owned Cards Scene을 실행해주세요.");
                RefreshUi();
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        public void OpenShopSceneFromUi()
        {
            var sceneName = string.IsNullOrWhiteSpace(_shopSceneName)
                ? "Shop_VSlice"
                : _shopSceneName;

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetStatus("상점 씬을 찾을 수 없습니다. Build Settings에 Shop_VSlice가 포함되어 있는지 확인해 주세요.");
                RefreshUi();
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        public async void ReconnectPvpBattleFromUi()
        {
            if (!CanReconnectPvpBattle())
            {
                const string status = "이전 PVP 전투가 이미 종료되었습니다.";
                SetStatus(status);
                SimpleNoticeToast.Show("GameStartNoticeToastCanvas", status);
                RefreshUi();
                return;
            }

            var previousWins = AccountSessionState.ActiveRunWins;
            var previousLosses = AccountSessionState.ActiveRunLosses;
            CancelRunStart();
            _runStartCancellation = new CancellationTokenSource();
            _isStartingRun = true;
            SetStatus("PVP 전투 재접속 가능 여부를 확인하는 중입니다.");
            RefreshUi();

            var canLaunchBattleScene = false;
            var reconnectUnavailableStatus = string.Empty;
            try
            {
                canLaunchBattleScene = await RefreshPvpReconnectBeforeLaunchAsync(
                        _runStartCancellation.Token);
                if (!canLaunchBattleScene)
                {
                    reconnectUnavailableStatus = BuildFinishedPvpReconnectStatusText(previousWins, previousLosses);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 PVP reconnect preflight failed on start scene: {ex.Message}");
                const string status = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
                SetStatus(status);
                SimpleNoticeToast.Show("GameStartNoticeToastCanvas", status);
                RefreshUi();
                return;
            }
            finally
            {
                _isStartingRun = false;
                CancelRunStart();
                RefreshUi();
            }

            if (!canLaunchBattleScene)
            {
                if (!string.IsNullOrWhiteSpace(reconnectUnavailableStatus))
                {
                    SetStatus(reconnectUnavailableStatus);
                    SimpleNoticeToast.Show("GameStartNoticeToastCanvas", reconnectUnavailableStatus);
                }

                return;
            }

            var battleSceneName = string.IsNullOrWhiteSpace(_battleSceneName)
                ? "Battle_VSlice"
                : _battleSceneName;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(battleSceneName))
            {
                SetStatus("전투 씬을 찾을 수 없습니다. Build Settings에 Battle_VSlice가 포함되어 있는지 확인해 주세요.");
                RefreshUi();
                return;
            }

            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.QueuePvpReconnectBattleStart();
            SetStatus("끊긴 PVP 전투에 재접속합니다.");
            RefreshUi();
            SceneManager.LoadScene(battleSceneName);
        }

        private async Task<bool> RefreshPvpReconnectBeforeLaunchAsync(CancellationToken cancellationToken)
        {
            var client = new GuestAuthClient(AccountServerUrl);
            await RefreshPvpReconnectStatusAsync(client, cancellationToken);
            if (_hasPvpReconnectableBattle)
            {
                return true;
            }

            await RefreshServerAccountStateAsync(cancellationToken);
            if (_hasPvpReconnectableBattle)
            {
                return true;
            }

            return false;
        }

        private static string BuildFinishedPvpReconnectStatusText(int previousWins, int previousLosses)
        {
            var currentWins = AccountSessionState.HasResumableRun
                ? AccountSessionState.ActiveRunWins
                : AccountSessionState.LatestRunWins;
            var currentLosses = AccountSessionState.HasResumableRun
                ? AccountSessionState.ActiveRunLosses
                : AccountSessionState.LatestRunLosses;

            if (currentWins > previousWins)
            {
                return "이전 전투에서 승리했습니다.";
            }

            if (currentLosses > previousLosses)
            {
                return "이전 전투에서 패배했습니다.";
            }

            return "이전 PVP 전투가 이미 종료되었습니다.";
        }

        private void LoadDeckBuildingSceneOrDraftFallback()
        {
            var deckBuildingSceneName = ResolveDeckBuildingSceneName();
            if (!string.IsNullOrWhiteSpace(deckBuildingSceneName) &&
                UnityEngine.Application.CanStreamedLevelBeLoaded(deckBuildingSceneName))
            {
                SceneManager.LoadScene(deckBuildingSceneName);
                return;
            }

            SceneManager.LoadScene(ResolveDraftSceneName());
        }

        private string ResolveSceneForCurrentRun()
        {
            if (IsActiveRunDrafting())
            {
                var deckBuildingSceneName = ResolveDeckBuildingSceneName();
                if (!string.IsNullOrWhiteSpace(deckBuildingSceneName) &&
                    UnityEngine.Application.CanStreamedLevelBeLoaded(deckBuildingSceneName))
                {
                    return deckBuildingSceneName;
                }
            }

            return ResolveDraftSceneName();
        }

        private static bool IsActiveRunDrafting()
        {
            return string.Equals(
                AccountSessionState.ActiveRunStatus,
                "drafting",
                StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveDraftSceneName()
        {
            return string.IsNullOrWhiteSpace(_draftSceneName)
                ? "Draft_VSlice"
                : _draftSceneName;
        }

        private string ResolveDeckBuildingSceneName()
        {
            return string.IsNullOrWhiteSpace(_deckBuildingSceneName)
                ? "DeckBuilding_VSlice"
                : _deckBuildingSceneName;
        }

        private void RefreshUi()
        {
            if (_ticketText != null)
            {
                AccountWalletView.ShowSession(_ticketText);
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = CanStartGame();
            }

            if (_startGameButtonLabel != null)
            {
                _startGameButtonLabel.gameObject.SetActive(true);
                _startGameButtonLabel.enabled = true;
                _startGameButtonLabel.text = BuildStartButtonLabel();
            }

            if (_shopButton != null)
            {
                _shopButton.gameObject.SetActive(_useServerAccount);
                _shopButton.interactable =
                    AccountSessionState.IsAuthenticated &&
                    !_isAuthenticating &&
                    !IsAccountAuthBusy &&
                    !_isStartingRun;
            }

            if (_shopButtonLabel != null)
            {
                _shopButtonLabel.text = "상점";
            }

            if (_ownedCardsButton != null)
            {
                _ownedCardsButton.gameObject.SetActive(_useServerAccount);
                _ownedCardsButton.interactable =
                    AccountSessionState.IsAuthenticated &&
                    !_isAuthenticating &&
                    !IsAccountAuthBusy &&
                    !_isStartingRun;
            }

            if (_ownedCardsButtonLabel != null)
            {
                _ownedCardsButtonLabel.text = "보유 카드";
            }

            if (_reconnectPvpButton != null)
            {
                _reconnectPvpButton.gameObject.SetActive(_useServerAccount && _hasPvpReconnectableBattle);
                _reconnectPvpButton.interactable = CanReconnectPvpBattle();
            }

            if (_reconnectPvpButtonLabel != null)
            {
                _reconnectPvpButtonLabel.text = BuildReconnectPvpButtonLabel();
            }

            RefreshServerSettingsPanelUi();

            if (_statusText != null)
            {
                _statusText.text = BuildStatusText();
            }

            if (_accountInfoText != null)
            {
                _accountInfoText.text = BuildAccountInfoText();
            }

            RefreshAccountLoginGateUi();
            RefreshDevelopmentSmokePanel();
        }

        public void RefreshUiFromExternalState()
        {
            RefreshUi();
        }

        private async void StartAccountRestoreIfNeeded()
        {
            if (!_useServerAccount || _isAuthenticating || IsAccountAuthBusy)
            {
                return;
            }

            CancelAccountRestore();
            _authCancellation = new CancellationTokenSource();
            var cancellationToken = _authCancellation.Token;
            _isAuthenticating = true;
            _authFailed = false;
            SetStatus("저장된 로그인 정보를 확인하는 중입니다.");
            RefreshUi();

            try
            {
                LoadSavedAccountCredentials();
                var hadSavedLogin = AccountSessionState.HasSavedRegisteredSession;
                var resolvedAccountServerUrl = AccountServerUrl;
                var client = new GuestAuthClient(resolvedAccountServerUrl);
                await RefreshServerStatusAsync(client, cancellationToken);
                RefreshUi();

                if (!string.IsNullOrWhiteSpace(AccountSessionState.SessionToken))
                {
                    try
                    {
                        Debug.Log($"After333 saved session restore starting: server={resolvedAccountServerUrl}, accountKind={AccountSessionState.AccountKind}");
                        await RefreshServerAccountStateAsync(cancellationToken);
                        _authFailed = false;
                        Debug.Log($"After333 saved session restored: account={AccountSessionState.AccountId}, displayName={AccountSessionState.DisplayName}, accountKind={AccountSessionState.AccountKind}, tickets={AccountSessionState.Tickets}, gold={AccountSessionState.ResourceGold}");
                        SetStatus("자동 로그인 완료.");
                        return;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Debug.LogWarning($"After333 saved session restore failed: {ex.Message}");
                        AccountSessionState.ClearAccessToken();
                    }
                }

                if (!string.IsNullOrWhiteSpace(AccountSessionState.RefreshToken))
                {
                    try
                    {
                        SetStatus("로그인 세션을 자동으로 갱신하는 중입니다.");
                        RefreshUi();
                        var refreshResponse = await client.RefreshSessionAsync(
                            AccountSessionState.RefreshToken,
                            ResolvedClientVersion,
                            cancellationToken);
                        AccountSessionState.ApplyRefreshAuthResponse(refreshResponse);
                        await RefreshServerAccountStateAsync(cancellationToken);
                        _authFailed = false;
                        Debug.Log($"After333 refresh-token login restored: account={AccountSessionState.AccountId}, displayName={AccountSessionState.DisplayName}, accountKind={AccountSessionState.AccountKind}");
                        SetStatus("자동 로그인 완료.");
                        return;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Debug.LogWarning($"After333 refresh-token restore failed: {ex.Message}");
                        AccountSessionState.ClearSavedSession();
                    }
                }

                if (await TryAutomaticGoogleLoginAsync(
                        client,
                        hadSavedLogin,
                        cancellationToken))
                {
                    return;
                }

                _authFailed = false;
                SetStatus(hadSavedLogin
                    ? "자동 로그인 정보가 만료되었습니다. Game ID 또는 Google로 다시 로그인해주세요."
                    : "게임을 이용하려면 Game ID 또는 Google로 로그인해주세요.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _authFailed = true;
                Debug.LogWarning($"After333 account restore failed: {ex.Message}");
                SetStatus("서버 계정 로그인 실패. 서버와 DB를 확인해주세요.");
            }
            finally
            {
                _isAuthenticating = false;
                RefreshUi();
            }
        }

        private void LoadSavedAccountCredentials()
        {
            if (_hasLoadedSavedAccountCredentials || !_useServerAccount)
            {
                return;
            }

            AccountSessionState.LoadSavedLoginCredentials();
            _hasLoadedSavedAccountCredentials = true;
            Debug.Log(
                $"After333 saved credential preload: storage={AccountCredentialStore.ActiveStorageName}, " +
                $"secure={AccountCredentialStore.IsUsingSecureStorage}, " +
                $"hasSession={!string.IsNullOrWhiteSpace(AccountSessionState.SessionToken)}, " +
                $"hasRefresh={!string.IsNullOrWhiteSpace(AccountSessionState.RefreshToken)}");
        }

        private async Task<bool> TryAutomaticGoogleLoginAsync(
            GuestAuthClient client,
            bool hadSavedLogin,
            CancellationToken cancellationToken)
        {
            var providerKind = GoogleIdTokenProviderFactory.CurrentPlatformKind;
            var googleClientId = GoogleIdTokenProviderFactory.ResolveClientId(
                providerKind,
                _googleDesktopClientId,
                _googleAndroidServerClientId);
            var shouldAttempt = GoogleIdTokenProviderFactory.ShouldAttemptAutomaticAndroidSignIn(
                providerKind,
                _tryAutomaticGoogleLoginOnAndroid,
                !string.IsNullOrWhiteSpace(googleClientId),
                IsGoogleServerConfigured(),
                AccountSessionState.IsAutomaticGoogleSignInSuppressed,
                hadSavedLogin,
                AccountSessionState.IsAutomaticGoogleSignInEnabled);
            if (!shouldAttempt)
            {
                return false;
            }

            SetStatus("Google 계정으로 자동 로그인하는 중입니다.");
            RefreshUi();

            try
            {
                var tokenProvider = GoogleIdTokenProviderFactory.CreateForCurrentPlatform(
                    _googleDesktopClientId,
                    _googleAndroidServerClientId,
                    AccountServerUrl);
                if (!(tokenProvider is IAutomaticGoogleIdTokenProvider automaticTokenProvider))
                {
                    return false;
                }

                var idToken = await automaticTokenProvider.AcquireAutomaticIdTokenAsync(cancellationToken);
                var response = await client.AuthenticateGoogleAsync(
                    idToken,
                    ResolvedClientVersion,
                    cancellationToken);
                AccountSessionState.ApplyGoogleAuthResponse(response);

                try
                {
                    await RefreshServerAccountStateAsync(cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.LogWarning($"After333 automatic Google login account refresh failed: {ex.Message}");
                }

                _authFailed = false;
                SetStatus("Google 계정 자동 로그인 완료.");
                Debug.Log(
                    $"After333 automatic Google login succeeded: account={AccountSessionState.AccountId}, " +
                    $"displayName={AccountSessionState.DisplayName}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.Log(
                    "After333 automatic Google login was unavailable; showing the login choices instead: " +
                    ex.Message);
                return false;
            }
        }

        private void CancelAccountRestore()
        {
            if (_authCancellation == null)
            {
                return;
            }

            _authCancellation.Cancel();
            _authCancellation.Dispose();
            _authCancellation = null;
            _isAuthenticating = false;
        }

        private void CancelGameIdAuth()
        {
            if (_gameIdAuthCancellation == null)
            {
                return;
            }

            _gameIdAuthCancellation.Cancel();
            _gameIdAuthCancellation.Dispose();
            _gameIdAuthCancellation = null;
        }

        private void CancelGoogleAuth()
        {
            if (_googleAuthCancellation == null)
            {
                _isGoogleAuthBusy = false;
                return;
            }

            _googleAuthCancellation.Cancel();
            _googleAuthCancellation.Dispose();
            _googleAuthCancellation = null;
            _isGoogleAuthBusy = false;
        }

        private void CancelLogout()
        {
            if (_logoutCancellation == null)
            {
                return;
            }

            _logoutCancellation.Cancel();
            _logoutCancellation.Dispose();
            _logoutCancellation = null;
        }

        private async Task StartServerDraftRunFromUiAsync()
        {
            if (_isStartingRun)
            {
                return;
            }

            if (_isAuthenticating)
            {
                SetStatus("계정 정보를 불러오는 중입니다.");
                RefreshUi();
                return;
            }

            if (_authFailed)
            {
                SetStatus("서버 계정 로그인에 실패했습니다. 서버를 확인해주세요.");
                RefreshUi();
                return;
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                SetStatus("게임을 이용하려면 먼저 로그인해주세요.");
                RefreshUi();
                return;
            }

            if (AccountSessionState.HasResumableRun)
            {
                SetStatus(BuildResumeStatusText());
                RefreshUi();
                SceneManager.LoadScene(ResolveSceneForCurrentRun());
                return;
            }

            if (AccountSessionState.HasUnclaimedLatestRunRewards)
            {
                SetStatus("이전 런 보상을 먼저 수령해야 새 게임을 시작할 수 있습니다.");
                RefreshUi();
                SceneManager.LoadScene(_draftSceneName);
                return;
            }

            if (AccountSessionState.Tickets < _ticketCost)
            {
                SetStatus($"서버 티켓이 부족합니다. 필요 티켓: {_ticketCost}");
                RefreshUi();
                return;
            }

            CancelRunStart();
            _runStartCancellation = new CancellationTokenSource();
            _isStartingRun = true;
            SetStatus("서버 티켓을 차감하고 새 드래프트 런을 시작하는 중입니다.");
            RefreshUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.StartDraftRunAsync(
                    AccountSessionState.SessionToken,
                    "pve",
                    _runStartCancellation.Token);
                AccountSessionState.ApplyStartRunResponse(response);
                DraftRunSessionState.ResetForNewDraft();
                Debug.Log($"After333 server draft run started: run={AccountSessionState.ActiveRunId}, mode={AccountSessionState.ActiveRunMode}, tickets={AccountSessionState.Tickets}");
                SetStatus("서버 런 생성 완료. 드래프트로 이동합니다.");
                RefreshUi();
                LoadDeckBuildingSceneOrDraftFallback();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 server draft run start failed: {ex.Message}");
                SetStatus($"서버 런 시작 실패: {ex.Message}");
            }
            finally
            {
                _isStartingRun = false;
                CancelRunStart();
                RefreshUi();
            }
        }

        private void CancelRunStart()
        {
            if (_runStartCancellation == null)
            {
                return;
            }

            _runStartCancellation.Cancel();
            _runStartCancellation.Dispose();
            _runStartCancellation = null;
        }

        private string BuildReconnectPvpButtonLabel()
        {
            var remainingText = _pvpReconnectRemainingSeconds > 0
                ? $" ({_pvpReconnectRemainingSeconds}초)"
                : string.Empty;
            return $"PVP 전투 재접속{remainingText}";
        }

        private string BuildStartButtonLabel()
        {
            if (_useServerAccount && AccountSessionState.HasResumableRun)
            {
                return AccountSessionState.HasSavedDraftDeck
                    ? "이어하기"
                    : "드래프트 이어하기";
            }

            if (_useServerAccount && AccountSessionState.HasUnclaimedLatestRunRewards)
            {
                return "보상 수령하기";
            }

            return $"게임 시작 (-{_ticketCost} Tickets)";
        }

        private string BuildStatusText()
        {
            if (_useServerAccount)
            {
                if (_isGameIdAuthBusy)
                {
                    return "Game ID 계정 요청을 처리하는 중입니다.";
                }

                if (_isGoogleAuthBusy)
                {
                    return "브라우저에서 Google 계정 인증을 처리하는 중입니다.";
                }

                if (_isStartingRun)
                {
                    return "서버 티켓을 차감하고 새 드래프트 런을 시작하는 중입니다.";
                }

                if (_isAuthenticating)
                {
                    return "서버 계정에 로그인하는 중입니다.";
                }

                if (_authFailed)
                {
                    return "서버 계정 로그인 실패. 서버와 DB를 확인해주세요.";
                }

                if (AccountSessionState.IsAuthenticated)
                {
                    var displayName = string.IsNullOrWhiteSpace(AccountSessionState.DisplayName)
                        ? "계정"
                        : AccountSessionState.DisplayName;
                    if (AccountSessionState.HasResumableRun)
                    {
                        return BuildResumeStatusText(displayName);
                    }

                    if (AccountSessionState.HasUnclaimedLatestRunRewards)
                    {
                        return $"{displayName} 계정의 이전 런 보상을 먼저 수령해야 새 게임을 시작할 수 있습니다.";
                    }

                    if (AccountSessionState.IsLatestRunRewardClaimed)
                    {
                        return $"{displayName} 계정으로 로그인됨. 이전 런 보상을 받았습니다. 새 게임 시작 시 서버 티켓 {_ticketCost}개를 차감합니다.";
                    }

                    return AccountSessionState.Tickets >= _ticketCost
                        ? $"{displayName} 계정으로 로그인됨. 게임 시작 시 서버 티켓 {_ticketCost}개를 차감합니다."
                        : $"{displayName} 계정으로 로그인됨. 서버 티켓이 부족합니다. 필요 티켓: {_ticketCost}";
                }

                return string.IsNullOrWhiteSpace(_accountLoginGateStatus)
                    ? "로그인 또는 회원가입 후 게임을 시작할 수 있습니다."
                    : _accountLoginGateStatus;
            }

            return "서버 계정으로 로그인한 뒤 게임을 시작할 수 있습니다.";
        }

        private string BuildAccountInfoText()
        {
            var serverStatusLine = BuildServerStatusLine();
            if (!_useServerAccount)
            {
                return $"서버 계정 모드가 비활성화되었습니다.\n{serverStatusLine}";
            }

            if (_isAuthenticating)
            {
                return $"서버 계정 로그인 중...\n{serverStatusLine}";
            }

            if (_isStartingRun)
            {
                return $"서버 런 시작 중...\n{serverStatusLine}";
            }

            if (_isGameIdAuthBusy)
            {
                return $"Game ID 처리 중...\n{serverStatusLine}";
            }

            if (_isGoogleAuthBusy)
            {
                return $"Google 로그인 처리 중...\n{serverStatusLine}";
            }

            if (_authFailed)
            {
                return $"서버 계정 로그인 실패\n서버 주소: {AccountServerUrl}\n{serverStatusLine}";
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                return $"서버 계정 정보 없음\n{serverStatusLine}";
            }

            var displayName = string.IsNullOrWhiteSpace(AccountSessionState.DisplayName)
                ? "계정"
                : AccountSessionState.DisplayName;
            if (AccountSessionState.HasResumableRun)
            {
                return $"서버 계정: {displayName}\nActive Run: {AccountSessionState.ActiveRunStatus}  W:{AccountSessionState.ActiveRunWins} L:{AccountSessionState.ActiveRunLosses}\n{serverStatusLine}";
            }

            if (AccountSessionState.HasUnclaimedLatestRunRewards)
            {
                return $"서버 계정: {displayName}\nRewards Pending: W:{AccountSessionState.LatestRunWins} L:{AccountSessionState.LatestRunLosses}\n{serverStatusLine}";
            }

            if (AccountSessionState.IsLatestRunRewardClaimed)
            {
                return $"서버 계정: {displayName}\nLast Run: 보상 수령 완료\n{serverStatusLine}";
            }

            return $"서버 계정: {displayName}\n{serverStatusLine}";
        }

        private string BuildServerStatusLine()
        {
            if (_serverStatus == null)
            {
                return string.IsNullOrWhiteSpace(_serverStatusError)
                    ? "Server: 확인 중"
                    : $"Server: OFF  DB: -  Cards: -  Error: {FormatShortServerError(_serverStatusError)}";
            }

            var serverStatus = FormatServerStatus(_serverStatus.ResolvedStatus);
            var databaseStatus = FormatServerStatus(_serverStatus.ResolvedDatabase?.ResolvedStatus);
            var cardStatus = FormatServerStatus(_serverStatus.ResolvedCards?.ResolvedStatus);
            var cardCount = _serverStatus.ResolvedCards?.ResolvedCardCount ?? 0;
            var cardText = string.Equals(cardStatus, "OK", StringComparison.OrdinalIgnoreCase)
                ? $"Cards: {cardCount}"
                : $"Cards: {cardStatus}";
            var statusLine = $"Server: {serverStatus}  DB: {databaseStatus}  {cardText}";
            var versionText = BuildClientVersionStatusText();
            if (!string.IsNullOrWhiteSpace(versionText))
            {
                statusLine += $"  {versionText}";
            }

            var deploymentText = BuildDeploymentStatusText();
            if (!string.IsNullOrWhiteSpace(deploymentText))
            {
                statusLine += $"  {deploymentText}";
            }

            var profileText = BuildClientProfileStatusText();
            if (!string.IsNullOrWhiteSpace(profileText))
            {
                statusLine += $"  {profileText}";
            }

            return statusLine;
        }

        private static string BuildClientProfileStatusText()
        {
            return Project333ClientProfile.HasProfile
                ? $"Client: {Project333ClientProfile.DisplayName}"
                : string.Empty;
        }

        private static string FormatShortServerError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "-";
            }

            var normalized = error.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 48
                ? normalized
                : normalized.Substring(0, 48) + "...";
        }

        private string BuildDeploymentStatusText()
        {
            var deployment = _serverStatus?.ResolvedDeployment;
            if (deployment == null)
            {
                return string.Empty;
            }

            var deploymentStatus = FormatServerStatus(deployment.ResolvedStatus);
            return string.IsNullOrWhiteSpace(deploymentStatus) || deploymentStatus == "-"
                ? string.Empty
                : $"Deploy: {deploymentStatus}";
        }

        private string BuildClientVersionStatusText()
        {
            var compatibility = _serverStatus?.ResolvedClientCompatibility;
            if (compatibility == null)
            {
                return string.Empty;
            }

            var requiredVersion = compatibility.ResolvedRequiredClientVersion;
            if (!string.IsNullOrWhiteSpace(requiredVersion))
            {
                return string.Equals(ResolvedClientVersion, requiredVersion, StringComparison.OrdinalIgnoreCase)
                    ? "Ver: OK"
                    : "Ver: CHECK";
            }

            var recommendedVersion = compatibility.ResolvedRecommendedClientVersion;
            if (!string.IsNullOrWhiteSpace(recommendedVersion))
            {
                return string.Equals(ResolvedClientVersion, recommendedVersion, StringComparison.OrdinalIgnoreCase)
                    ? "Ver: OK"
                    : "Ver: CHECK";
            }

            return string.Empty;
        }

        private static string FormatServerStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "-";
            }

            if (string.Equals(status, "Ok", StringComparison.OrdinalIgnoreCase))
            {
                return "OK";
            }

            if (string.Equals(status, "NotConfigured", StringComparison.OrdinalIgnoreCase))
            {
                return "OFF";
            }

            if (string.Equals(status, "Degraded", StringComparison.OrdinalIgnoreCase))
            {
                return "CHECK";
            }

            if (string.Equals(status, "Check", StringComparison.OrdinalIgnoreCase))
            {
                return "CHECK";
            }

            return status;
        }

        private bool CanStartGame()
        {
            if (!_useServerAccount)
            {
                return false;
            }

            return !_isAuthenticating &&
                   !IsAccountAuthBusy &&
                   !_isStartingRun &&
                   !_authFailed &&
                   AccountSessionState.IsAuthenticated &&
                   (AccountSessionState.HasResumableRun ||
                     AccountSessionState.HasUnclaimedLatestRunRewards ||
                     AccountSessionState.Tickets >= _ticketCost);
        }

        private bool CanReconnectPvpBattle()
        {
            return _useServerAccount &&
                   _hasPvpReconnectableBattle &&
                   AccountSessionState.IsAuthenticated &&
                   !_isAuthenticating &&
                   !IsAccountAuthBusy &&
                   !_isStartingRun &&
                   !_authFailed;
        }

        private void RefreshGameIdAuthPanelUi()
        {
            var canUseGameIdButtons =
                _useServerAccount &&
                !_isAuthenticating &&
                !IsAccountAuthBusy &&
                !_isStartingRun;

            if (_gameIdAuthPanel != null)
            {
                _gameIdAuthPanel.gameObject.SetActive(
                    _useServerAccount && AccountSessionState.IsAuthenticated);
            }

            if (_gameIdRegisterButton != null)
            {
                _gameIdRegisterButton.interactable = canUseGameIdButtons;
            }

            if (_gameIdLoginButton != null)
            {
                _gameIdLoginButton.interactable = canUseGameIdButtons;
            }

            if (_gameIdLinkButton != null)
            {
                _gameIdLinkButton.interactable = canUseGameIdButtons && AccountSessionState.IsAuthenticated;
            }

            if (_gameIdLogoutButton != null)
            {
                _gameIdLogoutButton.interactable = canUseGameIdButtons && AccountSessionState.IsAuthenticated;
            }

            if (_gameIdRegisterButtonLabel != null)
            {
                _gameIdRegisterButtonLabel.text = _isGameIdAuthBusy ? "처리 중" : "회원가입";
            }

            if (_gameIdLoginButtonLabel != null)
            {
                _gameIdLoginButtonLabel.text = _isGameIdAuthBusy ? "처리 중" : "로그인";
            }

            if (_gameIdLinkButtonLabel != null)
            {
                _gameIdLinkButtonLabel.text = _isGameIdAuthBusy ? "처리 중" : "현재 계정 연결";
            }

            if (_gameIdLogoutButtonLabel != null)
            {
                _gameIdLogoutButtonLabel.text = _isGameIdAuthBusy ? "처리 중" : "계정 전환";
            }
        }

        private void RefreshGoogleAuthPanelUi()
        {
            var providerKind = GoogleIdTokenProviderFactory.CurrentPlatformKind;
            var platformSupported = providerKind != GoogleIdTokenProviderKind.Unsupported;
            var clientConfigured = !string.IsNullOrWhiteSpace(
                GoogleIdTokenProviderFactory.ResolveClientId(
                    providerKind,
                    _googleDesktopClientId,
                    _googleAndroidServerClientId));
            var serverConfigured = IsGoogleServerConfigured();
            var canUseGoogleButtons =
                _useServerAccount &&
                platformSupported &&
                clientConfigured &&
                serverConfigured &&
                !_isAuthenticating &&
                !IsAccountAuthBusy &&
                !_isStartingRun;

            if (_googleAuthPanel != null)
            {
                _googleAuthPanel.gameObject.SetActive(
                    _useServerAccount && AccountSessionState.IsAuthenticated);
            }

            if (_googleLoginButton != null)
            {
                _googleLoginButton.interactable = canUseGoogleButtons;
            }

            if (_googleLinkButton != null)
            {
                _googleLinkButton.interactable =
                    canUseGoogleButtons && AccountSessionState.IsAuthenticated;
            }

            if (_googleLoginButtonLabel != null)
            {
                _googleLoginButtonLabel.text = _isGoogleAuthBusy ? "처리 중" : "Google 로그인";
            }

            if (_googleLinkButtonLabel != null)
            {
                _googleLinkButtonLabel.text = _isGoogleAuthBusy ? "처리 중" : "현재 계정 연결";
            }

            if (_googleAuthTitleText != null)
            {
                if (!platformSupported)
                {
                    _googleAuthTitleText.text = "Google 계정 (현재 플랫폼 미지원)";
                }
                else if (!clientConfigured)
                {
                    _googleAuthTitleText.text = "Google 계정 (Client ID 필요)";
                }
                else if (!serverConfigured)
                {
                    _googleAuthTitleText.text = "Google 계정 (서버 설정 필요)";
                }
                else
                {
                    var platformName = GoogleIdTokenProviderFactory.GetPlatformDisplayName(providerKind);
                    _googleAuthTitleText.text = $"Google 계정 ({platformName})";
                }
            }
        }

        private bool IsGoogleServerConfigured()
        {
            var googleStatus = _serverStatus?.ResolvedAuthentication?.ResolvedGoogle;
            return GoogleIdTokenProviderFactory.IsServerConfiguredForProvider(
                GoogleIdTokenProviderFactory.CurrentPlatformKind,
                googleStatus?.ResolvedConfigured == true,
                googleStatus?.ResolvedDesktopCodeExchangeConfigured == true);
        }

        private void RefreshServerSettingsPanelUi()
        {
            if (_serverSettingsPanel != null)
            {
                _serverSettingsPanel.gameObject.SetActive(_useServerAccount);
            }

            if (_serverHttpUrlInput != null && !_serverHttpUrlInput.isFocused)
            {
                var resolvedServerUrl = AccountServerUrl;
                if (!string.Equals(
                        _serverHttpUrlInput.text,
                        resolvedServerUrl,
                        StringComparison.OrdinalIgnoreCase))
                {
                    SetServerHttpUrlInputText(resolvedServerUrl);
                }
            }

            var canChangeServer =
                _useServerAccount &&
                !_isAuthenticating &&
                !IsAccountAuthBusy &&
                !_isStartingRun;

            if (_serverHttpUrlInput != null)
            {
                _serverHttpUrlInput.interactable = canChangeServer;
            }

            if (_serverLocalButton != null)
            {
                _serverLocalButton.interactable = canChangeServer;
            }

            if (_serverTestButton != null)
            {
                _serverTestButton.interactable = canChangeServer;
            }

            if (_serverApplyButton != null)
            {
                _serverApplyButton.interactable = canChangeServer;
            }

            if (_serverResetButton != null)
            {
                _serverResetButton.interactable = canChangeServer;
            }

            if (_serverLocalButtonLabel != null)
            {
                _serverLocalButtonLabel.text = "Local";
            }

            if (_serverTestButtonLabel != null)
            {
                _serverTestButtonLabel.text = "Test";
            }

            if (_serverApplyButtonLabel != null)
            {
                _serverApplyButtonLabel.text = "Apply";
            }

            if (_serverResetButtonLabel != null)
            {
                _serverResetButtonLabel.text = "Reset";
            }
        }

        private static string GetInputText(InputField inputField)
        {
            return inputField == null ? string.Empty : inputField.text;
        }

        private void SetServerHttpUrlInputText(string url)
        {
            if (_serverHttpUrlInput == null)
            {
                return;
            }

            _serverHttpUrlInput.SetTextWithoutNotify(
                Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(url));
        }

        private void HandleServerEndpointChanged(string previousUrl, string newUrl, string statusMessage)
        {
            var hasChanged = !string.Equals(previousUrl, newUrl, StringComparison.OrdinalIgnoreCase);
            if (hasChanged)
            {
                CancelAccountRestore();
                CancelGameIdAuth();
                CancelGoogleAuth();
                ClearPvpReconnectStatus();
                AccountSessionState.ClearSavedSession();
                _serverStatus = null;
                _serverStatusError = string.Empty;
                _authFailed = false;
                SetStatus($"{statusMessage}. 계정 세션을 다시 연결합니다.");
                RefreshUi();
                StartAccountRestoreIfNeeded();
                return;
            }

            SetStatus(statusMessage);
            RefreshUi();
        }

        private void EnsureDevelopmentSmokePanel()
        {
            if (!_showDevelopmentSmokePanel)
            {
                if (_developmentSmokePanel != null)
                {
                    _developmentSmokePanel.Configure(
                        false,
                        _developmentSmokePanelAnchor,
                        _developmentSmokePanelOffset,
                        _developmentSmokePanelSize,
                        _developmentSmokePanelTextPadding,
                        _developmentSmokePanelColor,
                        _developmentSmokePanelTextColor,
                        _developmentSmokePanelFontSize);
                }

                return;
            }

            _developmentSmokePanel = _developmentSmokePanel != null
                ? _developmentSmokePanel
                : ServerAccountSmokePanel.GetOrCreate("GameStartSmokePanelCanvas", _developmentSmokePanelSortingOrder);
            _developmentSmokePanel.Configure(
                true,
                _developmentSmokePanelAnchor,
                _developmentSmokePanelOffset,
                _developmentSmokePanelSize,
                _developmentSmokePanelTextPadding,
                _developmentSmokePanelColor,
                _developmentSmokePanelTextColor,
                _developmentSmokePanelFontSize);
        }

        private void RefreshDevelopmentSmokePanel()
        {
            EnsureDevelopmentSmokePanel();
            if (_developmentSmokePanel == null || !_showDevelopmentSmokePanel)
            {
                return;
            }

            _developmentSmokePanel.Refresh(
                "Start",
                BuildDevelopmentSmokeNextAction(),
                BuildDevelopmentSmokeExtraLine());
        }

        private string BuildDevelopmentSmokeNextAction()
        {
            if (!_useServerAccount)
            {
                return "Enable server account mode before testing Game Start.";
            }

            if (_isAuthenticating)
            {
                return "Wait for account sign-in restoration.";
            }

            if (_isStartingRun)
            {
                return "Wait for run start response.";
            }

            if (_authFailed)
            {
                return "Restart server/DB, then replay Start scene.";
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                return "Waiting for server account.";
            }

            if (AccountSessionState.HasUnclaimedLatestRunRewards)
            {
                return "Click Game Start, then claim rewards in Draft scene.";
            }

            if (AccountSessionState.HasResumableRun)
            {
                return "Click Game Start to resume current run.";
            }

            if (AccountSessionState.Tickets >= _ticketCost)
            {
                return "Click Game Start. Expected: tickets -3, Draft scene.";
            }

            return "Open Shop to buy tickets or earn more rewards.";
        }

        private string BuildDevelopmentSmokeExtraLine()
        {
            var startState = CanStartGame() ? "ON" : "OFF";
            var shopState = _shopButton != null && _shopButton.interactable ? "ON" : "OFF";
            return $"Buttons: Start {startState} / Shop {shopState}";
        }

        private async Task RefreshServerStatusAsync(
            GuestAuthClient client,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                _serverStatus = null;
                _serverStatusError = "클라이언트 없음";
                return;
            }

            try
            {
                _serverStatus = await client.GetServerStatusAsync(cancellationToken);
                _serverStatusError = string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _serverStatus = null;
                _serverStatusError = ex.Message;
                Debug.LogWarning($"After333 server status check failed: {ex.Message}");
            }
        }

        private async Task RefreshServerAccountStateAsync(CancellationToken cancellationToken)
        {
            var client = new GuestAuthClient(AccountServerUrl);
            await RefreshServerStatusAsync(client, cancellationToken);

            if (!AccountSessionState.IsAuthenticated)
            {
                return;
            }

            var response = await client.GetMeAsync(
                AccountSessionState.SessionToken,
                cancellationToken);
            AccountSessionState.ApplyMeResponse(response);
            await RefreshPvpReconnectStatusAsync(client, cancellationToken);
        }

        private async Task RefreshPvpReconnectStatusAsync(
            GuestAuthClient client,
            CancellationToken cancellationToken)
        {
            ClearPvpReconnectStatus();

            if (client == null || !AccountSessionState.IsAuthenticated)
            {
                return;
            }

            try
            {
                var response = await client.GetPvpReconnectStatusAsync(
                    AccountSessionState.SessionToken,
                    cancellationToken);
                if (response == null || !response.ResolvedHasReconnectableBattle)
                {
                    return;
                }

                var remainingSeconds = Math.Max(0, response.ResolvedRemainingSeconds);
                if (remainingSeconds <= 0)
                {
                    return;
                }

                _hasPvpReconnectableBattle = true;
                _pvpReconnectMatchId = response.ResolvedMatchId ?? string.Empty;
                _pvpReconnectRemainingSeconds = remainingSeconds;
                _pvpReconnectDeadlineRealtime = UnityEngine.Time.unscaledTime + _pvpReconnectRemainingSeconds;
                _lastPresentedPvpReconnectRemainingSeconds = _pvpReconnectRemainingSeconds;
                Debug.Log(
                    $"[After333 Start] PVP reconnect available: match={FormatLogValue(_pvpReconnectMatchId)} remaining={_pvpReconnectRemainingSeconds}s");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 PVP reconnect status refresh failed on start scene: {ex.Message}");
            }
        }

        private void ClearPvpReconnectStatus()
        {
            _hasPvpReconnectableBattle = false;
            _pvpReconnectMatchId = string.Empty;
            _pvpReconnectRemainingSeconds = 0;
            _pvpReconnectDeadlineRealtime = 0f;
            _lastPresentedPvpReconnectRemainingSeconds = -1;
        }

        private string BuildResumeStatusText(string displayName = null)
        {
            var name = string.IsNullOrWhiteSpace(displayName)
                ? string.IsNullOrWhiteSpace(AccountSessionState.DisplayName) ? "계정" : AccountSessionState.DisplayName
                : displayName;
            if (AccountSessionState.HasSavedDraftDeck)
            {
                return $"{name} 계정의 진행 중인 덱을 이어갑니다. W:{AccountSessionState.ActiveRunWins} L:{AccountSessionState.ActiveRunLosses}";
            }

            return $"{name} 계정의 진행 중인 드래프트를 이어갑니다. 티켓은 추가로 차감하지 않습니다.";
        }

        private void SetStatus(string status)
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                _accountLoginGateStatus = status ?? string.Empty;
            }

            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        private static string FormatLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private void AutoAssignAccountInfoPanel()
        {
            if (_accountInfoPanel == null && transform.Find("AccountInfoPanel") is RectTransform panelTransform)
            {
                _accountInfoPanel = panelTransform;
            }

            if (_accountInfoText == null && _accountInfoPanel != null)
            {
                _accountInfoText = _accountInfoPanel.GetComponentInChildren<Text>(true);
            }
        }

        private void EnsureAccountInfoPanel()
        {
            var parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (_accountInfoPanel == null)
            {
                var panelObject = new GameObject("AccountInfoPanel", typeof(RectTransform), typeof(Image));
                RegisterEditorCreatedObject(panelObject);
                _accountInfoPanel = panelObject.GetComponent<RectTransform>();
                _accountInfoPanel.SetParent(parent, false);
                _accountInfoPanelNeedsDefaultLayout = true;
            }

            if (_accountInfoText == null)
            {
                if (_accountInfoPanel.Find("AccountInfoText") is RectTransform textTransform)
                {
                    _accountInfoText = textTransform.GetComponent<Text>();
                    if (_accountInfoText == null)
                    {
                        _accountInfoText = textTransform.gameObject.AddComponent<Text>();
                        _accountInfoTextNeedsDefaultLayout = true;
                    }
                }
                else
                {
                    var textObject = new GameObject("AccountInfoText", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(textObject);
                    var textRect = textObject.GetComponent<RectTransform>();
                    textRect.SetParent(_accountInfoPanel, false);
                    _accountInfoText = textObject.GetComponent<Text>();
                    _accountInfoTextNeedsDefaultLayout = true;
                }
            }

            ApplyAccountInfoVisual();
        }

        private void ApplyAccountInfoVisual()
        {
            if (_accountInfoPanel != null)
            {
                if (_accountInfoPanelNeedsDefaultLayout)
                {
                    _accountInfoPanel.anchorMin = new Vector2(0f, 1f);
                    _accountInfoPanel.anchorMax = new Vector2(0f, 1f);
                    _accountInfoPanel.pivot = new Vector2(0f, 1f);
                    _accountInfoPanel.sizeDelta = _accountInfoPanelSize;
                    _accountInfoPanel.anchoredPosition = _accountInfoPanelPosition;
                    _accountInfoPanelNeedsDefaultLayout = false;
                }

                _accountInfoPanel.SetAsLastSibling();

                var panelImage = _accountInfoPanel.GetComponent<Image>();
                if (panelImage == null)
                {
                    panelImage = _accountInfoPanel.gameObject.AddComponent<Image>();
                }

                if (panelImage != null)
                {
                    panelImage.color = _accountInfoPanelColor;
                    panelImage.raycastTarget = false;
                }
            }

            if (_accountInfoText == null)
            {
                return;
            }

            _accountInfoText.font = ResolveRuntimeFont();
            _accountInfoText.fontSize = _accountInfoFontSize;
            _accountInfoText.fontStyle = FontStyle.Bold;
            _accountInfoText.alignment = TextAnchor.MiddleCenter;
            _accountInfoText.color = _accountInfoTextColor;
            _accountInfoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _accountInfoText.verticalOverflow = VerticalWrapMode.Overflow;
            _accountInfoText.supportRichText = false;
            _accountInfoText.raycastTarget = false;

            if (_accountInfoTextNeedsDefaultLayout)
            {
                var textRect = _accountInfoText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 10f);
                textRect.offsetMax = new Vector2(-18f, -10f);
                _accountInfoTextNeedsDefaultLayout = false;
            }
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

        private void AutoAssignStartButtonImage()
        {
            if (_startButtonImage != null)
            {
                return;
            }

            if (_startGameButton != null)
            {
                _startButtonImage = _startGameButton.GetComponent<Image>();
                return;
            }

            if (transform.Find("CenterPanel/StartGameButton") is RectTransform buttonTransform)
            {
                _startGameButton = buttonTransform.GetComponent<Button>();
                _startButtonImage = buttonTransform.GetComponent<Image>();
            }
        }

        private void AutoAssignShopButton()
        {
            if (_shopButton != null &&
                string.Equals(_shopButton.name, "PurchaseTicketButton", StringComparison.Ordinal))
            {
                _shopButton.name = "ShopButton";
            }

            if (_shopButton == null)
            {
                RectTransform buttonTransform = null;
                if (transform.Find("CenterPanel/ShopButton") is RectTransform centerButtonTransform)
                {
                    buttonTransform = centerButtonTransform;
                }
                else if (transform.Find("ShopButton") is RectTransform rootButtonTransform)
                {
                    buttonTransform = rootButtonTransform;
                }
                else if (transform.Find("CenterPanel/PurchaseTicketButton") is RectTransform legacyCenterButtonTransform)
                {
                    buttonTransform = legacyCenterButtonTransform;
                    buttonTransform.name = "ShopButton";
                }
                else if (transform.Find("PurchaseTicketButton") is RectTransform legacyRootButtonTransform)
                {
                    buttonTransform = legacyRootButtonTransform;
                    buttonTransform.name = "ShopButton";
                }

                if (buttonTransform != null)
                {
                    _shopButton = buttonTransform.GetComponent<Button>();
                }
            }

            if (_shopButtonLabel == null && _shopButton != null)
            {
                _shopButtonLabel = _shopButton.GetComponentInChildren<Text>(true);
            }

            if (_shopButtonLabel != null &&
                (string.Equals(_shopButtonLabel.name, "PurchaseTicketButtonLabel", StringComparison.Ordinal) ||
                 string.Equals(_shopButtonLabel.name, "Label", StringComparison.Ordinal)))
            {
                _shopButtonLabel.name = "ShopButtonLabel";
            }
        }

        private void AutoAssignOwnedCardsButton()
        {
            if (_ownedCardsButton == null)
            {
                RectTransform buttonTransform = null;
                if (transform.Find("CenterPanel/OwnedCardsButton") is RectTransform centerButtonTransform)
                {
                    buttonTransform = centerButtonTransform;
                }
                else if (transform.Find("OwnedCardsButton") is RectTransform rootButtonTransform)
                {
                    buttonTransform = rootButtonTransform;
                }

                if (buttonTransform != null)
                {
                    _ownedCardsButton = buttonTransform.GetComponent<Button>();
                }
            }

            if (_ownedCardsButtonLabel == null && _ownedCardsButton != null)
            {
                _ownedCardsButtonLabel = _ownedCardsButton.GetComponentInChildren<Text>(true);
            }
        }

        private void AutoAssignReconnectPvpButton()
        {
            if (_reconnectPvpButton == null)
            {
                RectTransform buttonTransform = null;
                if (transform.Find("CenterPanel/ReconnectPvpButton") is RectTransform centerButtonTransform)
                {
                    buttonTransform = centerButtonTransform;
                }
                else if (transform.Find("ReconnectPvpButton") is RectTransform rootButtonTransform)
                {
                    buttonTransform = rootButtonTransform;
                }

                if (buttonTransform != null)
                {
                    _reconnectPvpButton = buttonTransform.GetComponent<Button>();
                }
            }

            if (_reconnectPvpButtonLabel == null && _reconnectPvpButton != null)
            {
                _reconnectPvpButtonLabel = _reconnectPvpButton.GetComponentInChildren<Text>(true);
            }
        }

        private void AutoAssignAccountLoginGate()
        {
            if (_accountLoginGate == null &&
                transform.Find("AccountLoginGateCanvas") is RectTransform gateTransform)
            {
                _accountLoginGate = gateTransform.GetComponent<AccountLoginGateView>();
            }
        }

        private void EnsureAccountLoginGate()
        {
            if (_accountLoginGate == null)
            {
                var gateObject = new GameObject(
                    "AccountLoginGateCanvas",
                    typeof(RectTransform),
                    typeof(AccountLoginGateView));
                RegisterEditorCreatedObject(gateObject);
                gateObject.transform.SetParent(transform, false);
                _accountLoginGate = gateObject.GetComponent<AccountLoginGateView>();
            }

            _accountLoginGate.EnsureEditableHierarchy();
            if (!UnityEngine.Application.isPlaying)
            {
                _accountLoginGate.gameObject.SetActive(false);
            }
        }

        private void BindAccountLoginGate()
        {
            if (_accountLoginGate == null)
            {
                return;
            }

            _accountLoginGate.Bind(
                LoginGameIdFromLoginGate,
                RegisterGameIdFromLoginGate,
                LoginGoogleFromUi);
        }

        private void RefreshAccountLoginGateUi()
        {
            if (_accountLoginGate == null)
            {
                return;
            }

            var isRestoringSavedLogin = _isAuthenticating;
            var visible =
                _useServerAccount &&
                !AccountSessionState.IsAuthenticated &&
                !isRestoringSavedLogin;
            var googleProviderKind = GoogleIdTokenProviderFactory.CurrentPlatformKind;
            var googleAvailable =
                googleProviderKind != GoogleIdTokenProviderKind.Unsupported &&
                !string.IsNullOrWhiteSpace(
                    GoogleIdTokenProviderFactory.ResolveClientId(
                        googleProviderKind,
                        _googleDesktopClientId,
                        _googleAndroidServerClientId)) &&
                IsGoogleServerConfigured();
            _accountLoginGate.SetState(
                _isAuthenticating || IsAccountAuthBusy,
                _accountLoginGateStatus,
                googleAvailable);
            _accountLoginGate.SetVisible(visible);
        }

        private async void LoginGameIdFromLoginGate()
        {
            if (_accountLoginGate != null)
            {
                await LoginGameIdAsync(_accountLoginGate.GameId, _accountLoginGate.Password);
            }
        }

        private async void RegisterGameIdFromLoginGate()
        {
            if (_accountLoginGate != null)
            {
                await RegisterGameIdAsync(
                    _accountLoginGate.GameId,
                    _accountLoginGate.Password,
                    _accountLoginGate.DisplayName);
            }
        }

        private void AutoAssignGameIdAuthPanel()
        {
            if (_gameIdAuthPanel == null && transform.Find("GameIdAuthPanel") is RectTransform panelTransform)
            {
                _gameIdAuthPanel = panelTransform;
            }

            if (_gameIdAuthPanel == null)
            {
                return;
            }

            if (_gameIdAuthTitleText == null && _gameIdAuthPanel.Find("TitleText") is RectTransform titleTransform)
            {
                _gameIdAuthTitleText = titleTransform.GetComponent<Text>();
            }

            if (_gameIdInput == null && _gameIdAuthPanel.Find("GameIdInput") is RectTransform gameIdTransform)
            {
                _gameIdInput = gameIdTransform.GetComponent<InputField>();
            }

            if (_gameIdPasswordInput == null && _gameIdAuthPanel.Find("PasswordInput") is RectTransform passwordTransform)
            {
                _gameIdPasswordInput = passwordTransform.GetComponent<InputField>();
            }

            if (_gameIdDisplayNameInput == null && _gameIdAuthPanel.Find("DisplayNameInput") is RectTransform displayNameTransform)
            {
                _gameIdDisplayNameInput = displayNameTransform.GetComponent<InputField>();
            }

            if (_gameIdRegisterButton == null && _gameIdAuthPanel.Find("RegisterButton") is RectTransform registerTransform)
            {
                _gameIdRegisterButton = registerTransform.GetComponent<Button>();
            }

            if (_gameIdLoginButton == null && _gameIdAuthPanel.Find("LoginButton") is RectTransform loginTransform)
            {
                _gameIdLoginButton = loginTransform.GetComponent<Button>();
            }

            if (_gameIdLinkButton == null && _gameIdAuthPanel.Find("LinkButton") is RectTransform linkTransform)
            {
                _gameIdLinkButton = linkTransform.GetComponent<Button>();
            }

            if (_gameIdLogoutButton == null && _gameIdAuthPanel.Find("LogoutButton") is RectTransform logoutTransform)
            {
                _gameIdLogoutButton = logoutTransform.GetComponent<Button>();
            }

            if (_gameIdRegisterButtonLabel == null && _gameIdRegisterButton != null)
            {
                _gameIdRegisterButtonLabel = _gameIdRegisterButton.GetComponentInChildren<Text>(true);
            }

            if (_gameIdLoginButtonLabel == null && _gameIdLoginButton != null)
            {
                _gameIdLoginButtonLabel = _gameIdLoginButton.GetComponentInChildren<Text>(true);
            }

            if (_gameIdLinkButtonLabel == null && _gameIdLinkButton != null)
            {
                _gameIdLinkButtonLabel = _gameIdLinkButton.GetComponentInChildren<Text>(true);
            }

            if (_gameIdLogoutButtonLabel == null && _gameIdLogoutButton != null)
            {
                _gameIdLogoutButtonLabel = _gameIdLogoutButton.GetComponentInChildren<Text>(true);
            }
        }

        private void AutoAssignGoogleAuthPanel()
        {
            if (_googleAuthPanel == null && transform.Find("GoogleAuthPanel") is RectTransform panelTransform)
            {
                _googleAuthPanel = panelTransform;
            }

            if (_googleAuthPanel == null)
            {
                return;
            }

            if (_googleAuthTitleText == null && _googleAuthPanel.Find("TitleText") is RectTransform titleTransform)
            {
                _googleAuthTitleText = titleTransform.GetComponent<Text>();
            }

            if (_googleLoginButton == null && _googleAuthPanel.Find("LoginButton") is RectTransform loginTransform)
            {
                _googleLoginButton = loginTransform.GetComponent<Button>();
            }

            if (_googleLinkButton == null && _googleAuthPanel.Find("LinkButton") is RectTransform linkTransform)
            {
                _googleLinkButton = linkTransform.GetComponent<Button>();
            }

            if (_googleLoginButtonLabel == null && _googleLoginButton != null)
            {
                _googleLoginButtonLabel = _googleLoginButton.GetComponentInChildren<Text>(true);
            }

            if (_googleLinkButtonLabel == null && _googleLinkButton != null)
            {
                _googleLinkButtonLabel = _googleLinkButton.GetComponentInChildren<Text>(true);
            }
        }

        private void AutoAssignServerSettingsPanel()
        {
            if (_serverSettingsPanel == null && transform.Find("ServerSettingsPanel") is RectTransform panelTransform)
            {
                _serverSettingsPanel = panelTransform;
            }

            if (_serverSettingsPanel == null)
            {
                return;
            }

            if (_serverSettingsTitleText == null && _serverSettingsPanel.Find("TitleText") is RectTransform titleTransform)
            {
                _serverSettingsTitleText = titleTransform.GetComponent<Text>();
            }

            if (_serverHttpUrlInput == null && _serverSettingsPanel.Find("ServerHttpUrlInput") is RectTransform inputTransform)
            {
                _serverHttpUrlInput = inputTransform.GetComponent<InputField>();
            }

            if (_serverLocalButton == null && _serverSettingsPanel.Find("LocalButton") is RectTransform localTransform)
            {
                _serverLocalButton = localTransform.GetComponent<Button>();
            }

            if (_serverTestButton == null && _serverSettingsPanel.Find("TestButton") is RectTransform testTransform)
            {
                _serverTestButton = testTransform.GetComponent<Button>();
            }

            if (_serverApplyButton == null && _serverSettingsPanel.Find("ApplyButton") is RectTransform applyTransform)
            {
                _serverApplyButton = applyTransform.GetComponent<Button>();
            }

            if (_serverResetButton == null && _serverSettingsPanel.Find("ResetButton") is RectTransform resetTransform)
            {
                _serverResetButton = resetTransform.GetComponent<Button>();
            }

            if (_serverLocalButtonLabel == null && _serverLocalButton != null)
            {
                _serverLocalButtonLabel = _serverLocalButton.GetComponentInChildren<Text>(true);
            }

            if (_serverTestButtonLabel == null && _serverTestButton != null)
            {
                _serverTestButtonLabel = _serverTestButton.GetComponentInChildren<Text>(true);
            }

            if (_serverApplyButtonLabel == null && _serverApplyButton != null)
            {
                _serverApplyButtonLabel = _serverApplyButton.GetComponentInChildren<Text>(true);
            }

            if (_serverResetButtonLabel == null && _serverResetButton != null)
            {
                _serverResetButtonLabel = _serverResetButton.GetComponentInChildren<Text>(true);
            }
        }

        private void EnsureGameIdAuthPanel()
        {
            var parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (_gameIdAuthPanel == null)
            {
                var panelObject = new GameObject("GameIdAuthPanel", typeof(RectTransform), typeof(Image));
                RegisterEditorCreatedObject(panelObject);
                _gameIdAuthPanel = panelObject.GetComponent<RectTransform>();
                _gameIdAuthPanel.SetParent(parent, false);
                _gameIdAuthPanelNeedsDefaultLayout = true;
            }

            if (_gameIdAuthTitleText == null)
            {
                var titleObject = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                RegisterEditorCreatedObject(titleObject);
                titleObject.transform.SetParent(_gameIdAuthPanel, false);
                _gameIdAuthTitleText = titleObject.GetComponent<Text>();
                _gameIdAuthTitleNeedsDefaultLayout = true;
            }

            if (_gameIdInput == null)
            {
                _gameIdInput = CreateGameIdInputField("GameIdInput", "Game ID");
                _gameIdInputNeedsDefaultLayout = true;
            }

            if (_gameIdPasswordInput == null)
            {
                _gameIdPasswordInput = CreateGameIdInputField("PasswordInput", "Password");
                _gameIdPasswordInput.contentType = InputField.ContentType.Password;
                _gameIdPasswordInputNeedsDefaultLayout = true;
            }

            if (_gameIdDisplayNameInput == null)
            {
                _gameIdDisplayNameInput = CreateGameIdInputField("DisplayNameInput", "Display Name (optional)");
                _gameIdDisplayNameInputNeedsDefaultLayout = true;
            }

            if (_gameIdRegisterButton == null)
            {
                _gameIdRegisterButton = CreateGameIdButton("RegisterButton", "회원가입", out _gameIdRegisterButtonLabel);
                _gameIdRegisterButtonNeedsDefaultLayout = true;
            }

            if (_gameIdLoginButton == null)
            {
                _gameIdLoginButton = CreateGameIdButton("LoginButton", "로그인", out _gameIdLoginButtonLabel);
                _gameIdLoginButtonNeedsDefaultLayout = true;
            }

            if (_gameIdLinkButton == null)
            {
                _gameIdLinkButton = CreateGameIdButton("LinkButton", "현재 계정 연결", out _gameIdLinkButtonLabel);
            }

            if (_gameIdLogoutButton == null)
            {
                _gameIdLogoutButton = CreateGameIdButton("LogoutButton", "계정 전환", out _gameIdLogoutButtonLabel);
            }

            ApplyGameIdAuthPanelVisual();
        }

        private void EnsureGoogleAuthPanel()
        {
            var parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (_googleAuthPanel == null)
            {
                var panelObject = new GameObject("GoogleAuthPanel", typeof(RectTransform), typeof(Image));
                RegisterEditorCreatedObject(panelObject);
                _googleAuthPanel = panelObject.GetComponent<RectTransform>();
                _googleAuthPanel.SetParent(parent, false);
                _googleAuthPanelNeedsDefaultLayout = true;
            }

            if (_googleAuthTitleText == null)
            {
                var titleObject = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                RegisterEditorCreatedObject(titleObject);
                titleObject.transform.SetParent(_googleAuthPanel, false);
                _googleAuthTitleText = titleObject.GetComponent<Text>();
                _googleAuthTitleNeedsDefaultLayout = true;
            }

            if (_googleLoginButton == null)
            {
                _googleLoginButton = CreateGoogleAuthButton(
                    "LoginButton",
                    "Google 로그인",
                    out _googleLoginButtonLabel);
                _googleLoginButtonNeedsDefaultLayout = true;
            }

            if (_googleLinkButton == null)
            {
                _googleLinkButton = CreateGoogleAuthButton(
                    "LinkButton",
                    "현재 계정 연결",
                    out _googleLinkButtonLabel);
            }

            ApplyGoogleAuthPanelVisual();
        }

        private void EnsureServerSettingsPanel()
        {
            var parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (_serverSettingsPanel == null)
            {
                var panelObject = new GameObject("ServerSettingsPanel", typeof(RectTransform), typeof(Image));
                RegisterEditorCreatedObject(panelObject);
                _serverSettingsPanel = panelObject.GetComponent<RectTransform>();
                _serverSettingsPanel.SetParent(parent, false);
                _serverSettingsPanelNeedsDefaultLayout = true;
            }

            if (_serverSettingsTitleText == null)
            {
                var titleObject = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                RegisterEditorCreatedObject(titleObject);
                titleObject.transform.SetParent(_serverSettingsPanel, false);
                _serverSettingsTitleText = titleObject.GetComponent<Text>();
                _serverSettingsTitleNeedsDefaultLayout = true;
            }

            if (_serverHttpUrlInput == null)
            {
                _serverHttpUrlInput = CreateServerSettingsInputField("ServerHttpUrlInput", "http://127.0.0.1:7333");
                _serverHttpUrlInputNeedsDefaultLayout = true;
                SetServerHttpUrlInputText(AccountServerUrl);
            }

            if (_serverLocalButton == null)
            {
                _serverLocalButton = CreateServerSettingsButton("LocalButton", "Local", out _serverLocalButtonLabel);
                _serverLocalButtonNeedsDefaultLayout = true;
            }

            if (_serverTestButton == null)
            {
                _serverTestButton = CreateServerSettingsButton("TestButton", "Test", out _serverTestButtonLabel);
                _serverTestButtonNeedsDefaultLayout = true;
            }

            if (_serverApplyButton == null)
            {
                _serverApplyButton = CreateServerSettingsButton("ApplyButton", "Apply", out _serverApplyButtonLabel);
                _serverApplyButtonNeedsDefaultLayout = true;
            }

            if (_serverResetButton == null)
            {
                _serverResetButton = CreateServerSettingsButton("ResetButton", "Reset", out _serverResetButtonLabel);
                _serverResetButtonNeedsDefaultLayout = true;
            }

            ApplyServerSettingsPanelVisual();
        }

        private InputField CreateServerSettingsInputField(string objectName, string placeholder)
        {
            var inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
            RegisterEditorCreatedObject(inputObject);
            inputObject.transform.SetParent(_serverSettingsPanel, false);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(textObject);
            textObject.transform.SetParent(inputObject.transform, false);

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(placeholderObject);
            placeholderObject.transform.SetParent(inputObject.transform, false);

            var inputField = inputObject.GetComponent<InputField>();
            inputField.textComponent = textObject.GetComponent<Text>();
            inputField.placeholder = placeholderObject.GetComponent<Text>();
            ((Text)inputField.placeholder).text = placeholder;
            return inputField;
        }

        private Button CreateServerSettingsButton(string objectName, string label, out Text labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            buttonObject.transform.SetParent(_serverSettingsPanel, false);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            labelObject.transform.SetParent(buttonObject.transform, false);

            labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private InputField CreateGameIdInputField(string objectName, string placeholder)
        {
            var inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
            RegisterEditorCreatedObject(inputObject);
            inputObject.transform.SetParent(_gameIdAuthPanel, false);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(textObject);
            textObject.transform.SetParent(inputObject.transform, false);

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(placeholderObject);
            placeholderObject.transform.SetParent(inputObject.transform, false);

            var inputField = inputObject.GetComponent<InputField>();
            inputField.textComponent = textObject.GetComponent<Text>();
            inputField.placeholder = placeholderObject.GetComponent<Text>();
            ((Text)inputField.placeholder).text = placeholder;
            return inputField;
        }

        private Button CreateGameIdButton(string objectName, string label, out Text labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            buttonObject.transform.SetParent(_gameIdAuthPanel, false);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            labelObject.transform.SetParent(buttonObject.transform, false);

            labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private Button CreateGoogleAuthButton(string objectName, string label, out Text labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            buttonObject.transform.SetParent(_googleAuthPanel, false);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            labelObject.transform.SetParent(buttonObject.transform, false);

            labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private void ApplyGoogleAuthPanelVisual()
        {
            if (_googleAuthPanel == null)
            {
                return;
            }

            var panelImage = _googleAuthPanel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _googleAuthPanel.gameObject.AddComponent<Image>();
            }

            if (_googleAuthPanelNeedsDefaultLayout)
            {
                _googleAuthPanel.anchorMin = new Vector2(1f, 1f);
                _googleAuthPanel.anchorMax = new Vector2(1f, 1f);
                _googleAuthPanel.pivot = new Vector2(1f, 1f);
                _googleAuthPanel.sizeDelta = _googleAuthPanelSize;
                _googleAuthPanel.anchoredPosition = _googleAuthPanelPosition;
                panelImage.color = _googleAuthPanelColor;
                _googleAuthPanelNeedsDefaultLayout = false;
            }

            panelImage.raycastTarget = false;

            if (_googleAuthTitleText != null && _googleAuthTitleNeedsDefaultLayout)
            {
                _googleAuthTitleText.font = ResolveRuntimeFont();
                _googleAuthTitleText.fontSize = _googleAuthFontSize + 3;
                _googleAuthTitleText.fontStyle = FontStyle.Bold;
                _googleAuthTitleText.alignment = TextAnchor.MiddleCenter;
                _googleAuthTitleText.color = _googleAuthTextColor;
                _googleAuthTitleText.raycastTarget = false;
                _googleAuthTitleText.text = "Google 계정 (Windows)";

                var titleRect = _googleAuthTitleText.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(18f, -54f);
                titleRect.offsetMax = new Vector2(-18f, -10f);
                _googleAuthTitleNeedsDefaultLayout = false;
            }

            ConfigureGoogleAuthButton(
                _googleLoginButton,
                _googleLoginButtonLabel,
                _googleLoginButtonNeedsDefaultLayout,
                new Vector2(-120f, -102f),
                LoginGoogleFromUi);
            _googleLoginButtonNeedsDefaultLayout = false;

        }

        private void ConfigureGoogleAuthButton(
            Button button,
            Text label,
            bool needsDefaultLayout,
            Vector2 position,
            UnityEngine.Events.UnityAction clickAction)
        {
            if (button == null)
            {
                return;
            }

            var buttonImage = button.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = button.gameObject.AddComponent<Image>();
            }

            if (needsDefaultLayout)
            {
                var buttonRect = button.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.anchorMin = new Vector2(0.5f, 1f);
                    buttonRect.anchorMax = new Vector2(0.5f, 1f);
                    buttonRect.pivot = new Vector2(0.5f, 0.5f);
                    buttonRect.sizeDelta = new Vector2(220f, 54f);
                    buttonRect.anchoredPosition = position;
                }

                buttonImage.color = _googleAuthButtonColor;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
                colors.pressedColor = new Color(0.78f, 0.92f, 1f, 0.9f);
                colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
                button.colors = colors;

                if (label != null)
                {
                    label.font = ResolveRuntimeFont();
                    label.fontSize = _googleAuthFontSize;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = _googleAuthTextColor;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.supportRichText = false;
                    label.raycastTarget = false;

                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(8f, 4f);
                    labelRect.offsetMax = new Vector2(-8f, -4f);
                }
            }

            buttonImage.raycastTarget = true;
            button.targetGraphic = buttonImage;
            button.onClick.RemoveListener(clickAction);
            button.onClick.AddListener(clickAction);
        }

        private void ApplyGameIdAuthPanelVisual()
        {
            if (_gameIdAuthPanel == null)
            {
                return;
            }

            if (_gameIdAuthPanelNeedsDefaultLayout)
            {
                _gameIdAuthPanel.anchorMin = new Vector2(1f, 1f);
                _gameIdAuthPanel.anchorMax = new Vector2(1f, 1f);
                _gameIdAuthPanel.pivot = new Vector2(1f, 1f);
                _gameIdAuthPanel.sizeDelta = _gameIdAuthPanelSize;
                _gameIdAuthPanel.anchoredPosition = _gameIdAuthPanelPosition;
                _gameIdAuthPanelNeedsDefaultLayout = false;
            }

            var panelImage = _gameIdAuthPanel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _gameIdAuthPanel.gameObject.AddComponent<Image>();
            }

            panelImage.color = _gameIdAuthPanelColor;
            panelImage.raycastTarget = false;

            ConfigureGameIdTitle();
            ConfigureGameIdInput(_gameIdInput, _gameIdInputNeedsDefaultLayout, new Vector2(0f, -78f));
            _gameIdInputNeedsDefaultLayout = false;
            ConfigureGameIdInput(_gameIdPasswordInput, _gameIdPasswordInputNeedsDefaultLayout, new Vector2(0f, -132f));
            if (_gameIdPasswordInput != null)
            {
                _gameIdPasswordInput.contentType = InputField.ContentType.Password;
                _gameIdPasswordInput.ForceLabelUpdate();
            }

            _gameIdPasswordInputNeedsDefaultLayout = false;
            ConfigureGameIdInput(_gameIdDisplayNameInput, _gameIdDisplayNameInputNeedsDefaultLayout, new Vector2(0f, -186f));
            _gameIdDisplayNameInputNeedsDefaultLayout = false;

            ConfigureGameIdButton(
                _gameIdRegisterButton,
                _gameIdRegisterButtonLabel,
                _gameIdRegisterButtonNeedsDefaultLayout,
                new Vector2(-156f, -270f),
                RegisterGameIdFromUi);
            _gameIdRegisterButtonNeedsDefaultLayout = false;

            ConfigureGameIdButton(
                _gameIdLoginButton,
                _gameIdLoginButtonLabel,
                _gameIdLoginButtonNeedsDefaultLayout,
                new Vector2(0f, -270f),
                LoginGameIdFromUi);
            _gameIdLoginButtonNeedsDefaultLayout = false;

        }

        private void ConfigureGameIdTitle()
        {
            if (_gameIdAuthTitleText == null)
            {
                return;
            }

            _gameIdAuthTitleText.font = ResolveRuntimeFont();
            _gameIdAuthTitleText.fontSize = _gameIdAuthFontSize + 4;
            _gameIdAuthTitleText.fontStyle = FontStyle.Bold;
            _gameIdAuthTitleText.alignment = TextAnchor.MiddleCenter;
            _gameIdAuthTitleText.color = _gameIdAuthTextColor;
            _gameIdAuthTitleText.raycastTarget = false;
            _gameIdAuthTitleText.text = "Game ID 계정";

            if (_gameIdAuthTitleNeedsDefaultLayout)
            {
                var titleRect = _gameIdAuthTitleText.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(18f, -56f);
                titleRect.offsetMax = new Vector2(-18f, -10f);
                _gameIdAuthTitleNeedsDefaultLayout = false;
            }
        }

        private void ConfigureGameIdInput(InputField inputField, bool needsDefaultLayout, Vector2 position)
        {
            if (inputField == null)
            {
                return;
            }

            var inputRect = inputField.GetComponent<RectTransform>();
            if (inputRect != null && needsDefaultLayout)
            {
                inputRect.anchorMin = new Vector2(0.5f, 1f);
                inputRect.anchorMax = new Vector2(0.5f, 1f);
                inputRect.pivot = new Vector2(0.5f, 0.5f);
                inputRect.sizeDelta = new Vector2(456f, 44f);
                inputRect.anchoredPosition = position;
            }

            var inputImage = inputField.GetComponent<Image>();
            if (inputImage != null)
            {
                inputImage.color = _gameIdAuthFieldColor;
                inputImage.raycastTarget = true;
                inputField.targetGraphic = inputImage;
            }

            ConfigureInputText(inputField.textComponent as Text, _gameIdAuthInputTextColor, TextAnchor.MiddleLeft);
            ConfigureInputText(inputField.placeholder as Text, new Color(0.32f, 0.32f, 0.38f, 0.72f), TextAnchor.MiddleLeft);
            inputField.caretColor = _gameIdAuthInputTextColor;
            inputField.selectionColor = new Color(0.2f, 0.55f, 0.8f, 0.35f);
        }

        private void ConfigureInputText(Text text, Color color, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            text.font = ResolveRuntimeFont();
            text.fontSize = _gameIdAuthFontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14f, 4f);
            rect.offsetMax = new Vector2(-14f, -4f);
        }

        private void ConfigureGameIdButton(
            Button button,
            Text label,
            bool needsDefaultLayout,
            Vector2 position,
            UnityEngine.Events.UnityAction clickAction)
        {
            if (button == null)
            {
                return;
            }

            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null && needsDefaultLayout)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(144f, 50f);
                buttonRect.anchoredPosition = position;
            }

            var buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = _gameIdAuthButtonColor;
                buttonImage.raycastTarget = true;
                button.targetGraphic = buttonImage;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.92f, 1f, 0.9f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
            button.colors = colors;
            button.onClick.RemoveListener(clickAction);
            button.onClick.AddListener(clickAction);

            if (label == null)
            {
                return;
            }

            label.font = ResolveRuntimeFont();
            label.fontSize = _gameIdAuthFontSize - 2;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = _gameIdAuthTextColor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
        }

        private void ApplyServerSettingsPanelVisual()
        {
            if (_serverSettingsPanel == null)
            {
                return;
            }

            if (_serverSettingsPanelNeedsDefaultLayout)
            {
                _serverSettingsPanel.anchorMin = new Vector2(0f, 1f);
                _serverSettingsPanel.anchorMax = new Vector2(0f, 1f);
                _serverSettingsPanel.pivot = new Vector2(0f, 1f);
                _serverSettingsPanel.sizeDelta = _serverSettingsPanelSize;
                _serverSettingsPanel.anchoredPosition = _serverSettingsPanelPosition;
                _serverSettingsPanelNeedsDefaultLayout = false;
            }

            var panelImage = _serverSettingsPanel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _serverSettingsPanel.gameObject.AddComponent<Image>();
            }

            panelImage.color = _serverSettingsPanelColor;
            panelImage.raycastTarget = false;

            ConfigureServerSettingsTitle();
            ConfigureServerSettingsInput();
            ConfigureServerSettingsButton(
                _serverLocalButton,
                _serverLocalButtonLabel,
                _serverLocalButtonNeedsDefaultLayout,
                new Vector2(-201f, -158f),
                UseLocalServerEndpointFromUi);
            _serverLocalButtonNeedsDefaultLayout = false;
            ConfigureServerSettingsButton(
                _serverTestButton,
                _serverTestButtonLabel,
                _serverTestButtonNeedsDefaultLayout,
                new Vector2(-67f, -158f),
                UseTestServerEndpointFromUi);
            _serverTestButtonNeedsDefaultLayout = false;
            ConfigureServerSettingsButton(
                _serverApplyButton,
                _serverApplyButtonLabel,
                _serverApplyButtonNeedsDefaultLayout,
                new Vector2(67f, -158f),
                ApplyServerEndpointFromUi);
            _serverApplyButtonNeedsDefaultLayout = false;
            ConfigureServerSettingsButton(
                _serverResetButton,
                _serverResetButtonLabel,
                _serverResetButtonNeedsDefaultLayout,
                new Vector2(201f, -158f),
                ResetServerEndpointFromUi);
            _serverResetButtonNeedsDefaultLayout = false;
        }

        private void ConfigureServerSettingsTitle()
        {
            if (_serverSettingsTitleText == null)
            {
                return;
            }

            _serverSettingsTitleText.font = ResolveRuntimeFont();
            _serverSettingsTitleText.fontSize = _serverSettingsFontSize + 2;
            _serverSettingsTitleText.fontStyle = FontStyle.Bold;
            _serverSettingsTitleText.alignment = TextAnchor.MiddleLeft;
            _serverSettingsTitleText.color = _serverSettingsTextColor;
            _serverSettingsTitleText.raycastTarget = false;
            _serverSettingsTitleText.text = "Server Endpoint";

            if (_serverSettingsTitleNeedsDefaultLayout)
            {
                var titleRect = _serverSettingsTitleText.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(18f, -48f);
                titleRect.offsetMax = new Vector2(-18f, -10f);
                _serverSettingsTitleNeedsDefaultLayout = false;
            }
        }

        private void ConfigureServerSettingsInput()
        {
            if (_serverHttpUrlInput == null)
            {
                return;
            }

            var inputRect = _serverHttpUrlInput.GetComponent<RectTransform>();
            if (inputRect != null && _serverHttpUrlInputNeedsDefaultLayout)
            {
                inputRect.anchorMin = new Vector2(0.5f, 1f);
                inputRect.anchorMax = new Vector2(0.5f, 1f);
                inputRect.pivot = new Vector2(0.5f, 0.5f);
                inputRect.sizeDelta = new Vector2(504f, 46f);
                inputRect.anchoredPosition = new Vector2(0f, -88f);
                _serverHttpUrlInputNeedsDefaultLayout = false;
            }

            var inputImage = _serverHttpUrlInput.GetComponent<Image>();
            if (inputImage != null)
            {
                inputImage.color = _serverSettingsFieldColor;
                inputImage.raycastTarget = true;
                _serverHttpUrlInput.targetGraphic = inputImage;
            }

            ConfigureServerSettingsInputText(_serverHttpUrlInput.textComponent as Text, _serverSettingsInputTextColor, TextAnchor.MiddleLeft);
            ConfigureServerSettingsInputText(_serverHttpUrlInput.placeholder as Text, new Color(0.32f, 0.32f, 0.38f, 0.72f), TextAnchor.MiddleLeft);
            _serverHttpUrlInput.caretColor = _serverSettingsInputTextColor;
            _serverHttpUrlInput.selectionColor = new Color(0.2f, 0.55f, 0.8f, 0.35f);
        }

        private void ConfigureServerSettingsInputText(Text text, Color color, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            text.font = ResolveRuntimeFont();
            text.fontSize = _serverSettingsFontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14f, 4f);
            rect.offsetMax = new Vector2(-14f, -4f);
        }

        private void ConfigureServerSettingsButton(
            Button button,
            Text label,
            bool needsDefaultLayout,
            Vector2 position,
            UnityEngine.Events.UnityAction clickAction)
        {
            if (button == null)
            {
                return;
            }

            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null && needsDefaultLayout)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(124f, 44f);
                buttonRect.anchoredPosition = position;
            }

            var buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = _serverSettingsButtonColor;
                buttonImage.raycastTarget = true;
                button.targetGraphic = buttonImage;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.92f, 1f, 0.9f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
            button.colors = colors;
            button.onClick.RemoveListener(clickAction);
            button.onClick.AddListener(clickAction);

            if (label == null)
            {
                return;
            }

            label.font = ResolveRuntimeFont();
            label.fontSize = _serverSettingsFontSize - 2;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = _serverSettingsTextColor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
        }

        private void EnsureOwnedCardsButton()
        {
            var parent = ResolveCenterButtonParent();
            if (parent == null)
            {
                return;
            }

            if (_ownedCardsButton == null)
            {
                var buttonObject = new GameObject("OwnedCardsButton", typeof(RectTransform), typeof(Image), typeof(Button));
                RegisterEditorCreatedObject(buttonObject);
                var buttonTransform = buttonObject.GetComponent<RectTransform>();
                buttonTransform.SetParent(parent, false);
                _ownedCardsButton = buttonObject.GetComponent<Button>();
                _ownedCardsButtonNeedsDefaultLayout = true;
            }

            if (_ownedCardsButtonLabel == null)
            {
                if (_ownedCardsButton.transform.Find("OwnedCardsButtonLabel") is RectTransform labelTransform)
                {
                    _ownedCardsButtonLabel = labelTransform.GetComponent<Text>();
                    if (_ownedCardsButtonLabel == null)
                    {
                        _ownedCardsButtonLabel = labelTransform.gameObject.AddComponent<Text>();
                        _ownedCardsButtonLabelNeedsDefaultLayout = true;
                    }
                }
                else if (_ownedCardsButton.transform.Find("Label") is RectTransform genericLabelTransform)
                {
                    _ownedCardsButtonLabel = genericLabelTransform.GetComponent<Text>();
                    if (_ownedCardsButtonLabel == null)
                    {
                        _ownedCardsButtonLabel = genericLabelTransform.gameObject.AddComponent<Text>();
                        _ownedCardsButtonLabelNeedsDefaultLayout = true;
                    }
                }
                else
                {
                    var labelObject = new GameObject("OwnedCardsButtonLabel", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(_ownedCardsButton.transform, false);
                    _ownedCardsButtonLabel = labelObject.GetComponent<Text>();
                    _ownedCardsButtonLabelNeedsDefaultLayout = true;
                }
            }

            ApplyOwnedCardsButtonVisual();
        }

        private void EnsureReconnectPvpButton()
        {
            var parent = ResolveCenterButtonParent();
            if (parent == null)
            {
                return;
            }

            if (_reconnectPvpButton == null)
            {
                var buttonObject = new GameObject("ReconnectPvpButton", typeof(RectTransform), typeof(Image), typeof(Button));
                RegisterEditorCreatedObject(buttonObject);
                var buttonTransform = buttonObject.GetComponent<RectTransform>();
                buttonTransform.SetParent(parent, false);
                _reconnectPvpButton = buttonObject.GetComponent<Button>();
                _reconnectPvpButtonNeedsDefaultLayout = true;
            }

            if (_reconnectPvpButtonLabel == null)
            {
                if (_reconnectPvpButton.transform.Find("ReconnectPvpButtonLabel") is RectTransform labelTransform)
                {
                    _reconnectPvpButtonLabel = labelTransform.GetComponent<Text>();
                    if (_reconnectPvpButtonLabel == null)
                    {
                        _reconnectPvpButtonLabel = labelTransform.gameObject.AddComponent<Text>();
                        _reconnectPvpButtonLabelNeedsDefaultLayout = true;
                    }
                }
                else if (_reconnectPvpButton.transform.Find("Label") is RectTransform genericLabelTransform)
                {
                    _reconnectPvpButtonLabel = genericLabelTransform.GetComponent<Text>();
                    if (_reconnectPvpButtonLabel == null)
                    {
                        _reconnectPvpButtonLabel = genericLabelTransform.gameObject.AddComponent<Text>();
                        _reconnectPvpButtonLabelNeedsDefaultLayout = true;
                    }
                }
                else
                {
                    var labelObject = new GameObject("ReconnectPvpButtonLabel", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(_reconnectPvpButton.transform, false);
                    _reconnectPvpButtonLabel = labelObject.GetComponent<Text>();
                    _reconnectPvpButtonLabelNeedsDefaultLayout = true;
                }
            }

            ApplyReconnectPvpButtonVisual();
        }

        private void EnsureShopButton()
        {
            var parent = ResolveCenterButtonParent();
            if (parent == null)
            {
                return;
            }

            if (_shopButton == null)
            {
                var buttonObject = new GameObject("ShopButton", typeof(RectTransform), typeof(Image), typeof(Button));
                RegisterEditorCreatedObject(buttonObject);
                var buttonTransform = buttonObject.GetComponent<RectTransform>();
                buttonTransform.SetParent(parent, false);
                _shopButton = buttonObject.GetComponent<Button>();
                _shopButtonNeedsDefaultLayout = true;
            }

            if (_shopButtonLabel == null)
            {
                if (_shopButton.transform.Find("ShopButtonLabel") is RectTransform labelTransform)
                {
                    _shopButtonLabel = labelTransform.GetComponent<Text>();
                    if (_shopButtonLabel == null)
                    {
                        _shopButtonLabel = labelTransform.gameObject.AddComponent<Text>();
                        _shopButtonLabelNeedsDefaultLayout = true;
                    }
                }
                else
                {
                    var labelObject = new GameObject("ShopButtonLabel", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(_shopButton.transform, false);
                    _shopButtonLabel = labelObject.GetComponent<Text>();
                    _shopButtonLabelNeedsDefaultLayout = true;
                }
            }

            ApplyShopButtonVisual();
        }

        private RectTransform ResolveCenterButtonParent()
        {
            if (transform.Find("CenterPanel") is RectTransform centerPanel)
            {
                return centerPanel;
            }

            return transform as RectTransform;
        }

        private void ApplyShopButtonVisual()
        {
            if (_shopButton == null)
            {
                return;
            }

            var applyDefaultButtonVisual = _shopButtonNeedsDefaultLayout;
            var buttonRect = _shopButton.GetComponent<RectTransform>();
            if (buttonRect != null && _shopButtonNeedsDefaultLayout)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = _shopButtonSize;
                buttonRect.anchoredPosition = _shopButtonPosition;
                _shopButtonNeedsDefaultLayout = false;
            }

            if (buttonRect != null)
            {
                buttonRect.SetAsLastSibling();
            }

            var buttonImage = _shopButton.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = _shopButton.gameObject.AddComponent<Image>();
                applyDefaultButtonVisual = true;
            }

            if (buttonImage != null && applyDefaultButtonVisual)
            {
                buttonImage.color = _shopButtonColor;
                buttonImage.raycastTarget = true;
            }

            if (_shopButton.targetGraphic == null && buttonImage != null)
            {
                _shopButton.targetGraphic = buttonImage;
            }

            if (applyDefaultButtonVisual)
            {
                var colors = _shopButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
                colors.pressedColor = new Color(0.78f, 0.92f, 1f, 0.9f);
                colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.62f);
                _shopButton.colors = colors;
            }

            _shopButton.onClick.RemoveListener(OpenShopSceneFromUi);
            _shopButton.onClick.AddListener(OpenShopSceneFromUi);

            if (_shopButtonLabel == null)
            {
                return;
            }

            if (_shopButtonLabelNeedsDefaultLayout)
            {
                _shopButtonLabel.font = ResolveRuntimeFont();
                _shopButtonLabel.fontSize = _shopButtonFontSize;
                _shopButtonLabel.fontStyle = FontStyle.Bold;
                _shopButtonLabel.alignment = TextAnchor.MiddleCenter;
                _shopButtonLabel.color = _shopButtonTextColor;
                _shopButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                _shopButtonLabel.verticalOverflow = VerticalWrapMode.Overflow;
                _shopButtonLabel.supportRichText = false;
                _shopButtonLabel.raycastTarget = false;
            }

            if (_shopButtonLabelNeedsDefaultLayout)
            {
                var labelRect = _shopButtonLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -8f);
                _shopButtonLabelNeedsDefaultLayout = false;
            }

            _shopButtonLabel.text = "상점";
        }

        private void ApplyOwnedCardsButtonVisual()
        {
            if (_ownedCardsButton == null)
            {
                return;
            }

            var applyDefaultVisual = _ownedCardsButtonNeedsDefaultLayout;
            var buttonRect = _ownedCardsButton.GetComponent<RectTransform>();
            if (buttonRect != null && (_applyOwnedCardsButtonLayout || _ownedCardsButtonNeedsDefaultLayout))
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = _ownedCardsButtonSize;
                buttonRect.anchoredPosition = _ownedCardsButtonPosition;
                _ownedCardsButtonNeedsDefaultLayout = false;
            }

            if (buttonRect != null)
            {
                buttonRect.SetAsLastSibling();
            }

            var buttonImage = _ownedCardsButton.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = _ownedCardsButton.gameObject.AddComponent<Image>();
                applyDefaultVisual = true;
            }

            if (buttonImage != null && applyDefaultVisual)
            {
                buttonImage.color = _ownedCardsButtonColor;
                buttonImage.raycastTarget = true;
            }

            if (_ownedCardsButton.targetGraphic == null)
            {
                _ownedCardsButton.targetGraphic = buttonImage;
            }

            if (applyDefaultVisual)
            {
                var colors = _ownedCardsButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
                colors.pressedColor = new Color(0.78f, 0.88f, 1f, 0.9f);
                colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.62f);
                _ownedCardsButton.colors = colors;
            }
            _ownedCardsButton.onClick.RemoveListener(OpenOwnedCardsSceneFromUi);
            _ownedCardsButton.onClick.AddListener(OpenOwnedCardsSceneFromUi);

            if (_ownedCardsButtonLabel == null)
            {
                return;
            }

            if (_ownedCardsButtonLabelNeedsDefaultLayout)
            {
                _ownedCardsButtonLabel.font = ResolveRuntimeFont();
                _ownedCardsButtonLabel.fontSize = _ownedCardsButtonFontSize;
                _ownedCardsButtonLabel.fontStyle = FontStyle.Bold;
                _ownedCardsButtonLabel.alignment = TextAnchor.MiddleCenter;
                _ownedCardsButtonLabel.color = _ownedCardsButtonTextColor;
                _ownedCardsButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                _ownedCardsButtonLabel.verticalOverflow = VerticalWrapMode.Overflow;
                _ownedCardsButtonLabel.supportRichText = false;
                _ownedCardsButtonLabel.raycastTarget = false;
            }
            _ownedCardsButtonLabel.text = "보유 카드";

            if (_ownedCardsButtonLabelNeedsDefaultLayout)
            {
                var labelRect = _ownedCardsButtonLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -8f);
                _ownedCardsButtonLabelNeedsDefaultLayout = false;
            }
        }

        private void ApplyReconnectPvpButtonVisual()
        {
            if (_reconnectPvpButton == null)
            {
                return;
            }

            var applyDefaultButtonVisual = _reconnectPvpButtonNeedsDefaultLayout;
            var buttonRect = _reconnectPvpButton.GetComponent<RectTransform>();
            if (buttonRect != null && (_applyReconnectPvpButtonLayout || _reconnectPvpButtonNeedsDefaultLayout))
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = _reconnectPvpButtonSize;
                buttonRect.anchoredPosition = _reconnectPvpButtonPosition;
                _reconnectPvpButtonNeedsDefaultLayout = false;
            }

            var buttonImage = _reconnectPvpButton.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = _reconnectPvpButton.gameObject.AddComponent<Image>();
                applyDefaultButtonVisual = true;
            }

            if (buttonImage != null && applyDefaultButtonVisual)
            {
                buttonImage.color = _reconnectPvpButtonColor;
                buttonImage.raycastTarget = true;
            }

            if (_reconnectPvpButton.targetGraphic == null && buttonImage != null)
            {
                _reconnectPvpButton.targetGraphic = buttonImage;
            }

            if (applyDefaultButtonVisual)
            {
                var colors = _reconnectPvpButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
                colors.pressedColor = new Color(1f, 0.82f, 0.72f, 0.92f);
                colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.62f);
                _reconnectPvpButton.colors = colors;
            }

            _reconnectPvpButton.onClick.RemoveListener(ReconnectPvpBattleFromUi);
            _reconnectPvpButton.onClick.AddListener(ReconnectPvpBattleFromUi);

            if (_reconnectPvpButtonLabel == null)
            {
                return;
            }

            if (_reconnectPvpButtonLabelNeedsDefaultLayout)
            {
                _reconnectPvpButtonLabel.font = ResolveRuntimeFont();
                _reconnectPvpButtonLabel.fontSize = _reconnectPvpButtonFontSize;
                _reconnectPvpButtonLabel.fontStyle = FontStyle.Bold;
                _reconnectPvpButtonLabel.alignment = TextAnchor.MiddleCenter;
                _reconnectPvpButtonLabel.color = _reconnectPvpButtonTextColor;
                _reconnectPvpButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                _reconnectPvpButtonLabel.verticalOverflow = VerticalWrapMode.Overflow;
                _reconnectPvpButtonLabel.supportRichText = false;
                _reconnectPvpButtonLabel.raycastTarget = false;
            }
            _reconnectPvpButtonLabel.text = BuildReconnectPvpButtonLabel();

            if (_reconnectPvpButtonLabelNeedsDefaultLayout)
            {
                var labelRect = _reconnectPvpButtonLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -8f);
                _reconnectPvpButtonLabelNeedsDefaultLayout = false;
            }
        }

        private void EnsureBackgroundSprite()
        {
            if (_backgroundImage != null && _backgroundImage.sprite != null)
            {
                // The scene Image is the editable source of truth. Do not replace an
                // Inspector-assigned background when entering Play Mode.
                _backgroundSprite = _backgroundImage.sprite;
                return;
            }

            if (_backgroundSprite != null || string.IsNullOrWhiteSpace(_backgroundResourcePath))
            {
                return;
            }

            _backgroundSprite = LoadSpriteFromResources(_backgroundResourcePath);
        }

        private void EnsureStartButtonSprite()
        {
            if (_startButtonImage != null && _startButtonImage.sprite != null)
            {
                _startButtonSprite = _startButtonImage.sprite;
                return;
            }

            if (_startButtonSprite != null || string.IsNullOrWhiteSpace(_startButtonResourcePath))
            {
                return;
            }

            _startButtonSprite = LoadSpriteFromResources(_startButtonResourcePath);
        }

        private void ApplyBackgroundVisual()
        {
            if (_backgroundImage == null ||
                _backgroundImage.sprite != null ||
                _backgroundSprite == null)
            {
                return;
            }

            _backgroundImage.sprite = _backgroundSprite;
            _backgroundImage.color = Color.white;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.preserveAspect = false;
        }

        private void ApplyStartButtonVisual()
        {
            if (_startButtonImage == null ||
                _startButtonImage.sprite != null ||
                _startButtonSprite == null)
            {
                return;
            }

            _startButtonImage.sprite = _startButtonSprite;
            _startButtonImage.color = Color.white;
            _startButtonImage.type = Image.Type.Simple;
            _startButtonImage.preserveAspect = true;
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

        private static void RegisterEditorCreatedObject(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && gameObject != null)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Game Start UI");
            }
#endif
        }

        private static Font ResolveRuntimeFont()
        {
            try
            {
                var dynamicFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial Unicode MS" }, 32);
                if (dynamicFont != null)
                {
                    return dynamicFont;
                }
            }
            catch
            {
                // Unity can fail to resolve OS fonts on some platforms. Use the built-in fallback then.
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
