using System;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation.Shop;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Ads
{
    [DisallowMultipleComponent]
    public sealed class RewardedTicketController : MonoBehaviour
    {
        private const string ProviderUserIdKey = "After333.LevelPlayProviderUserId";
        private const string LevelPlayAppKeyPreference = "After333.LevelPlayAppKey";
        private const string RewardedAdUnitIdPreference = "After333.RewardedAdUnitId";
        private const string PlacementNamePreference = "After333.RewardedAdPlacement";

        [Header("LevelPlay Dashboard")]
        [Tooltip("LevelPlay 대시보드의 Android App Key입니다.")]
        [SerializeField] private string _levelPlayAppKey = string.Empty;
        [Tooltip("LevelPlay 대시보드의 Rewarded Ad Unit ID입니다.")]
        [SerializeField] private string _rewardedAdUnitId = string.Empty;
        [SerializeField] private string _placementName = "start_ticket_reward";

        [Header("Server")]
        [SerializeField] private string _accountServerUrl = Project333ServerEndpointSettings.LocalHttpUrl;
        [SerializeField] private float _verificationTimeoutSeconds = 30f;
        [SerializeField] private float _verificationPollSeconds = 1f;

        [Header("Editable UI")]
        [SerializeField] private RectTransform _uiParent;
        [SerializeField] private Button _watchAdButton;
        [SerializeField] private Text _watchAdButtonLabel;
        [SerializeField] private Text _rewardedAdStatusText;
        [SerializeField] private Vector2 _buttonSize = new Vector2(260f, 72f);
        [SerializeField] private Vector2 _buttonPosition = new Vector2(290f, -220f);
        [SerializeField] private Vector2 _statusSize = new Vector2(300f, 52f);
        [SerializeField] private Vector2 _statusPosition = new Vector2(290f, -282f);
        [SerializeField] private Color _buttonColor = new Color(0.12f, 0.48f, 0.25f, 0.96f);
        [SerializeField] private Color _buttonTextColor = Color.white;
        [SerializeField] private Color _statusTextColor = new Color(0.86f, 1f, 0.9f, 1f);
        [SerializeField] private int _buttonFontSize = 24;
        [SerializeField] private int _statusFontSize = 18;

        private GameStartSceneController _startSceneController;
        private ShopSceneController _shopSceneController;
        private IRewardedAdService _adService;
        private CancellationTokenSource _cancellation;
        private string _providerUserId = string.Empty;
        private string _activeAttemptId = string.Empty;
        private string _lastStatusMessage = string.Empty;
        private bool _isRequestingAttempt;
        private bool _isVerifyingReward;
        private bool _buttonNeedsDefaultLayout;
        private bool _buttonLabelNeedsDefaultLayout;
        private bool _statusNeedsDefaultLayout;
#if UNITY_EDITOR
        private bool _editorRefreshQueued;
#endif

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_levelPlayAppKey) &&
            !string.IsNullOrWhiteSpace(_rewardedAdUnitId);

        private string AccountServerUrl => Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);

        private void Awake()
        {
            _startSceneController = GetComponent<GameStartSceneController>();
            _shopSceneController = GetComponent<ShopSceneController>();
            if (IsLegacyStartSceneHost())
            {
                PersistConfiguration();
                SetEditableUiActive(false);
                enabled = false;
                return;
            }

            RestorePersistedConfiguration();
            EnsureEditableHierarchy();
        }

        private void OnEnable()
        {
            _startSceneController = GetComponent<GameStartSceneController>();
            _shopSceneController = GetComponent<ShopSceneController>();
            if (IsLegacyStartSceneHost())
            {
                PersistConfiguration();
                SetEditableUiActive(false);
                enabled = false;
                return;
            }

            RestorePersistedConfiguration();
            EnsureEditableHierarchy();
            CancelOperations();
            _cancellation = new CancellationTokenSource();
            _providerUserId = LoadOrCreateProviderUserId();
            _adService = RewardedAdServiceRegistry.Shared;
            _adService.StateChanged += HandleAdStateChanged;
            _adService.Rewarded += HandleAdRewarded;
            _adService.Closed += HandleAdClosed;
            _adService.Initialize(_levelPlayAppKey, _providerUserId, _rewardedAdUnitId);
            RefreshUi();
        }

        private void OnDisable()
        {
            UnsubscribeAdService();
            CancelOperations();
        }

        private void OnDestroy()
        {
            UnsubscribeAdService();
            CancelOperations();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (_editorRefreshQueued)
            {
                return;
            }

            _editorRefreshQueued = true;
            EditorApplication.delayCall += MaterializeAfterValidation;
#else
            EnsureEditableHierarchy();
#endif
        }

#if UNITY_EDITOR
        private void MaterializeAfterValidation()
        {
            _editorRefreshQueued = false;
            if (this == null)
            {
                return;
            }

            EnsureEditableHierarchy();
        }
#endif

        [ContextMenu("Ensure Editable Rewarded Ad UI")]
        public void EnsureEditableHierarchy()
        {
            if (IsLegacyStartSceneHost())
            {
                SetEditableUiActive(false);
                return;
            }

            AutoAssignUi();
            var parent = ResolveUiParent();
            if (parent == null)
            {
                return;
            }

            if (_watchAdButton == null)
            {
                var buttonObject = new GameObject(
                    "WatchRewardedAdButton",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                RegisterCreatedObject(buttonObject);
                buttonObject.transform.SetParent(parent, false);
                _watchAdButton = buttonObject.GetComponent<Button>();
                _buttonNeedsDefaultLayout = true;
            }

            if (_watchAdButtonLabel == null)
            {
                var labelObject = new GameObject(
                    "WatchRewardedAdButtonLabel",
                    typeof(RectTransform),
                    typeof(Text));
                RegisterCreatedObject(labelObject);
                labelObject.transform.SetParent(_watchAdButton.transform, false);
                _watchAdButtonLabel = labelObject.GetComponent<Text>();
                _buttonLabelNeedsDefaultLayout = true;
            }

            if (_rewardedAdStatusText == null)
            {
                var statusObject = new GameObject(
                    "RewardedAdStatusText",
                    typeof(RectTransform),
                    typeof(Text));
                RegisterCreatedObject(statusObject);
                statusObject.transform.SetParent(parent, false);
                _rewardedAdStatusText = statusObject.GetComponent<Text>();
                _statusNeedsDefaultLayout = true;
            }

            ApplyVisuals();
            RefreshUi();
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorUtility.SetDirty(this);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        public void RefreshUi()
        {
            if (_watchAdButton == null || _watchAdButtonLabel == null)
            {
                return;
            }

            var authenticated = AccountSessionState.IsAuthenticated;
            var busy = _isRequestingAttempt || _isVerifyingReward || (_adService?.IsShowing ?? false);
            _watchAdButton.gameObject.SetActive(true);
            _watchAdButton.interactable = IsConfigured && authenticated && !busy && (_adService?.IsReady ?? false);
            _watchAdButtonLabel.text = BuildButtonLabel(authenticated, busy);

            if (_rewardedAdStatusText != null)
            {
                _rewardedAdStatusText.text = BuildStatusText(authenticated);
                _rewardedAdStatusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(_rewardedAdStatusText.text));
            }
        }

        public void ConfigureShopUi(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            var needsDefaultLayout = _uiParent != parent ||
                                     (_watchAdButton != null && _watchAdButton.transform.parent != parent);
            _uiParent = parent;
            if (_watchAdButton != null && _watchAdButton.transform.parent != parent)
            {
                _watchAdButton.transform.SetParent(parent, false);
            }

            if (_rewardedAdStatusText != null && _rewardedAdStatusText.transform.parent != parent)
            {
                _rewardedAdStatusText.transform.SetParent(parent, false);
            }

            if (needsDefaultLayout)
            {
                _buttonSize = new Vector2(430f, 118f);
                _buttonPosition = new Vector2(0f, -15f);
                _statusSize = new Vector2(500f, 96f);
                _statusPosition = new Vector2(0f, -152f);
                _buttonNeedsDefaultLayout = true;
                _statusNeedsDefaultLayout = true;
            }

            EnsureEditableHierarchy();
        }

        public async void WatchRewardedAdFromUi()
        {
            if (_isRequestingAttempt || _isVerifyingReward || (_adService?.IsShowing ?? false))
            {
                return;
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                SetStatus("로그인 후 광고 보상을 받을 수 있습니다.");
                return;
            }

            if (!IsConfigured)
            {
                SetStatus("Inspector에 LevelPlay App Key와 Rewarded Ad Unit ID를 입력해 주세요.");
                return;
            }

            if (_adService == null || !_adService.IsReady)
            {
                SetStatus(_adService?.StatusMessage ?? "광고가 아직 준비되지 않았습니다.");
                return;
            }

            var cancellation = _cancellation;
            if (cancellation == null)
            {
                return;
            }

            var cancellationToken = cancellation.Token;

            _isRequestingAttempt = true;
            SetStatus("광고 보상 가능 여부를 확인하는 중입니다.");
            try
            {
                var client = new RewardedTicketClient(AccountServerUrl);
                var attempt = await client.CreateAttemptAsync(
                    AccountSessionState.SessionToken,
                    _providerUserId,
                    cancellationToken);

                if (attempt.ResolvedWallet != null)
                {
                    AccountSessionState.ApplyRewardedAdWallet(attempt.ResolvedWallet);
                    RefreshHostWalletUi();
                }

                if (!attempt.ResolvedCanShow)
                {
                    SetStatus(BuildEligibilityMessage(attempt));
                    return;
                }

                _activeAttemptId = attempt.ResolvedAttemptId;
                var placement = string.IsNullOrWhiteSpace(attempt.ResolvedPlacement)
                    ? _placementName
                    : attempt.ResolvedPlacement;
                if (!_adService.TryShow(
                        placement,
                        attempt.ResolvedDynamicUserId,
                        out var errorMessage))
                {
                    _activeAttemptId = string.Empty;
                    SetStatus(errorMessage);
                    return;
                }

                SetStatus("광고 재생 중입니다.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 rewarded ad attempt failed: {ex.Message}");
                SetStatus(BuildAttemptFailureMessage(ex.Message));
            }
            finally
            {
                _isRequestingAttempt = false;
                RefreshUi();
            }
        }

        private void HandleAdStateChanged()
        {
            RefreshUi();
        }

        private void HandleAdRewarded()
        {
            BeginRewardVerification();
        }

        private void HandleAdClosed()
        {
            if (!string.IsNullOrWhiteSpace(_activeAttemptId))
            {
                BeginRewardVerification();
            }
            else
            {
                RefreshUi();
            }
        }

        private async void BeginRewardVerification()
        {
            if (_isVerifyingReward ||
                string.IsNullOrWhiteSpace(_activeAttemptId) ||
                _cancellation == null)
            {
                return;
            }

            _isVerifyingReward = true;
            SetStatus("광고 보상을 확인하는 중입니다.");
            var attemptId = _activeAttemptId;
            var cancellationToken = _cancellation.Token;
            try
            {
                var timeout = Mathf.Max(5f, _verificationTimeoutSeconds);
                var pollDelay = Mathf.Max(0.25f, _verificationPollSeconds);
                var deadline = Time.realtimeSinceStartup + timeout;
                var client = new RewardedTicketClient(AccountServerUrl);

                while (Time.realtimeSinceStartup < deadline)
                {
                    var attempt = await client.GetAttemptAsync(
                        AccountSessionState.SessionToken,
                        attemptId,
                        cancellationToken);
                    var status = attempt.ResolvedStatus;
                    if (string.Equals(status, "granted", StringComparison.OrdinalIgnoreCase))
                    {
                        AccountSessionState.ApplyRewardedAdWallet(attempt.ResolvedWallet);
                        RefreshHostWalletUi();
                        SetStatus($"티켓 +{Mathf.Max(1, attempt.ResolvedRewardTicketCount)} 지급 완료!");
                        return;
                    }

                    if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
                    {
                        SetStatus(BuildEligibilityMessage(attempt));
                        return;
                    }

                    await Task.Delay(
                        TimeSpan.FromSeconds(pollDelay),
                        cancellationToken);
                }

                SetStatus("광고 완료 정보가 아직 서버에 도착하지 않았습니다. LevelPlay S2S 콜백 설정을 확인해 주세요.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 rewarded ad verification failed: {ex.Message}");
                SetStatus("광고 보상 확인에 실패했습니다. 잠시 후 다시 확인해 주세요.");
            }
            finally
            {
                _activeAttemptId = string.Empty;
                _isVerifyingReward = false;
                RefreshUi();
            }
        }

        private string BuildButtonLabel(bool authenticated, bool busy)
        {
            if (!IsConfigured)
            {
                return "광고 설정 필요";
            }

            if (!authenticated)
            {
                return "로그인 후 광고 시청";
            }

            if (_isVerifyingReward)
            {
                return "보상 확인 중...";
            }

            if (busy)
            {
                return "광고 재생 중...";
            }

            return "광고 보고 티켓 +1";
        }

        private string BuildStatusText(bool authenticated)
        {
            if (!string.IsNullOrWhiteSpace(_lastStatusMessage))
            {
                return _lastStatusMessage;
            }

            if (!IsConfigured)
            {
                return "LevelPlay 설정 후 활성화됩니다.";
            }

            if (!authenticated)
            {
                return "로그인이 필요합니다.";
            }

            return _adService?.StatusMessage ?? string.Empty;
        }

        private static string BuildEligibilityMessage(RewardedAdAttemptDto attempt)
        {
            return attempt.ResolvedReasonCode switch
            {
                "daily_limit_reached" => "오늘 받을 수 있는 광고 티켓을 모두 받았습니다.",
                "reward_cooldown" => $"다음 광고 보상까지 {Mathf.Max(1, attempt.ResolvedCooldownSeconds)}초 남았습니다.",
                "server_not_configured" => "서버의 광고 보상 설정이 아직 완료되지 않았습니다.",
                "attempt_expired" => "광고 보상 확인 시간이 만료되었습니다. 다시 시도해 주세요.",
                "placement_mismatch" => "LevelPlay Placement 설정이 서버와 일치하지 않습니다.",
                "app_key_mismatch" => "LevelPlay App Key 설정이 서버와 일치하지 않습니다.",
                "attempt_rejected" => "광고 시청이 완료되지 않아 보상이 지급되지 않았습니다.",
                _ => string.IsNullOrWhiteSpace(attempt.ResolvedMessage)
                    ? "지금은 광고 보상을 받을 수 없습니다."
                    : attempt.ResolvedMessage
            };
        }

        private static string BuildAttemptFailureMessage(string errorMessage)
        {
            errorMessage ??= string.Empty;
            if (errorMessage.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            {
                return "광고 보상 서버가 이전 버전입니다. 서버를 최신 코드로 다시 실행해 주세요.";
            }

            if (errorMessage.Contains("server_not_configured", StringComparison.OrdinalIgnoreCase))
            {
                return "서버의 LevelPlay 광고 보상 설정이 아직 완료되지 않았습니다.";
            }

            if (errorMessage.Contains("db_not_configured", StringComparison.OrdinalIgnoreCase))
            {
                return "광고 보상 DB가 연결되지 않았습니다. 서버 설정을 확인해 주세요.";
            }

            if (errorMessage.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return "로그인 세션을 확인하지 못했습니다. 다시 로그인해 주세요.";
            }

            if (errorMessage.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.";
            }

            return "광고 보상 서버에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        private void SetStatus(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            RefreshUi();
        }

        private void AutoAssignUi()
        {
            var parent = ResolveUiParent();
            if (parent == null)
            {
                return;
            }

            _watchAdButton ??= parent.Find("WatchRewardedAdButton")?.GetComponent<Button>();
            if (_watchAdButtonLabel == null && _watchAdButton != null)
            {
                _watchAdButtonLabel = _watchAdButton.transform
                    .Find("WatchRewardedAdButtonLabel")
                    ?.GetComponent<Text>();
            }

            _rewardedAdStatusText ??= parent.Find("RewardedAdStatusText")?.GetComponent<Text>();
        }

        private RectTransform ResolveUiParent()
        {
            return _uiParent ??
                   transform.Find("RewardedTicketProductPanel") as RectTransform ??
                   transform.Find("CenterPanel") as RectTransform ??
                   transform as RectTransform;
        }

        private bool IsLegacyStartSceneHost()
        {
            return _startSceneController != null && _shopSceneController == null;
        }

        private void SetEditableUiActive(bool active)
        {
            AutoAssignUi();
            if (_watchAdButton != null)
            {
                _watchAdButton.gameObject.SetActive(active);
            }

            if (_rewardedAdStatusText != null)
            {
                _rewardedAdStatusText.gameObject.SetActive(active);
            }
        }

        private void PersistConfiguration()
        {
            if (!string.IsNullOrWhiteSpace(_levelPlayAppKey))
            {
                PlayerPrefs.SetString(LevelPlayAppKeyPreference, _levelPlayAppKey);
            }

            if (!string.IsNullOrWhiteSpace(_rewardedAdUnitId))
            {
                PlayerPrefs.SetString(RewardedAdUnitIdPreference, _rewardedAdUnitId);
            }

            if (!string.IsNullOrWhiteSpace(_placementName))
            {
                PlayerPrefs.SetString(PlacementNamePreference, _placementName);
            }

            PlayerPrefs.Save();
        }

        private void RestorePersistedConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_levelPlayAppKey))
            {
                _levelPlayAppKey = PlayerPrefs.GetString(LevelPlayAppKeyPreference, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(_rewardedAdUnitId))
            {
                _rewardedAdUnitId = PlayerPrefs.GetString(RewardedAdUnitIdPreference, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(_placementName))
            {
                _placementName = PlayerPrefs.GetString(PlacementNamePreference, "start_ticket_reward");
            }
        }

        private void RefreshHostWalletUi()
        {
            _startSceneController?.RefreshUiFromExternalState();
            _shopSceneController?.RefreshUiFromExternalState();
        }

        private void ApplyVisuals()
        {
            if (_watchAdButton != null)
            {
                var applyDefaultButtonVisual = _buttonNeedsDefaultLayout;
                var rect = _watchAdButton.GetComponent<RectTransform>();
                if (rect != null && _buttonNeedsDefaultLayout)
                {
                    SetCenteredLayout(rect, _buttonSize, _buttonPosition);
                    _buttonNeedsDefaultLayout = false;
                }

                var image = _watchAdButton.GetComponent<Image>();
                if (image == null)
                {
                    image = _watchAdButton.gameObject.AddComponent<Image>();
                    applyDefaultButtonVisual = true;
                }

                if (applyDefaultButtonVisual)
                {
                    image.color = _buttonColor;
                    image.raycastTarget = true;
                }

                if (_watchAdButton.targetGraphic == null)
                {
                    _watchAdButton.targetGraphic = image;
                }

                _watchAdButton.onClick.RemoveListener(WatchRewardedAdFromUi);
                _watchAdButton.onClick.AddListener(WatchRewardedAdFromUi);
            }

            if (_watchAdButtonLabel != null)
            {
                if (_buttonLabelNeedsDefaultLayout)
                {
                    _watchAdButtonLabel.font = ResolveRuntimeFont();
                    _watchAdButtonLabel.fontSize = _buttonFontSize;
                    _watchAdButtonLabel.fontStyle = FontStyle.Bold;
                    _watchAdButtonLabel.alignment = TextAnchor.MiddleCenter;
                    _watchAdButtonLabel.color = _buttonTextColor;
                    _watchAdButtonLabel.raycastTarget = false;
                    _watchAdButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                    _watchAdButtonLabel.verticalOverflow = VerticalWrapMode.Overflow;
                    var rect = _watchAdButtonLabel.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(8f, 6f);
                    rect.offsetMax = new Vector2(-8f, -6f);
                    _buttonLabelNeedsDefaultLayout = false;
                }
            }

            if (_rewardedAdStatusText != null)
            {
                // Preserve authored typography when restoring an existing shop UI.
                if (_statusNeedsDefaultLayout || _rewardedAdStatusText.font == null)
                {
                    _rewardedAdStatusText.font = ResolveRuntimeFont();
                    _rewardedAdStatusText.fontSize = _statusFontSize;
                    _rewardedAdStatusText.alignment = TextAnchor.UpperCenter;
                    _rewardedAdStatusText.color = _statusTextColor;
                    _rewardedAdStatusText.raycastTarget = false;
                    _rewardedAdStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    _rewardedAdStatusText.verticalOverflow = VerticalWrapMode.Overflow;
                }
                if (_statusNeedsDefaultLayout)
                {
                    SetCenteredLayout(
                        _rewardedAdStatusText.rectTransform,
                        _statusSize,
                        _statusPosition);
                    _statusNeedsDefaultLayout = false;
                }
            }
        }

        private static void SetCenteredLayout(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static string LoadOrCreateProviderUserId()
        {
            var value = PlayerPrefs.GetString(ProviderUserIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ProviderUserIdKey, value);
            PlayerPrefs.Save();
            return value;
        }

        private void UnsubscribeAdService()
        {
            if (_adService == null)
            {
                return;
            }

            _adService.StateChanged -= HandleAdStateChanged;
            _adService.Rewarded -= HandleAdRewarded;
            _adService.Closed -= HandleAdClosed;
            _adService = null;
        }

        private void CancelOperations()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _isRequestingAttempt = false;
            _isVerifyingReward = false;
            _activeAttemptId = string.Empty;
        }

        private static void RegisterCreatedObject(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && gameObject != null)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Rewarded Ad UI");
            }
#endif
        }

        private Font ResolveRuntimeFont()
        {
            var sceneTexts = GetComponentsInChildren<Text>(true);
            for (var i = 0; i < sceneTexts.Length; i++)
            {
                var sceneText = sceneTexts[i];
                if (sceneText == null ||
                    sceneText == _watchAdButtonLabel ||
                    sceneText == _rewardedAdStatusText ||
                    sceneText.font == null)
                {
                    continue;
                }

                return sceneText.font;
            }

            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial Unicode MS" },
                    28);
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
                // Some build targets cannot resolve desktop OS fonts.
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
