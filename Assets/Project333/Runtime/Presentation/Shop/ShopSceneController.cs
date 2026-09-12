using System;
using System.Threading;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Ads;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Shop
{
    [RequireComponent(typeof(RewardedTicketController))]
    public sealed class ShopSceneController : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int _shopVisualVersion;
        public int ShopVisualVersion => _shopVisualVersion;

        [SerializeField] private Text _titleText;
        [SerializeField] private Text _accountText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _purchaseTicketButton;
        [SerializeField] private Text _purchaseTicketButtonLabel;
        [SerializeField] private Text _ticketPriceText;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _backButtonLabel;
        [SerializeField] private RewardedTicketController _rewardedTicketController;
        [SerializeField] private string _accountServerUrl =
            Project333ServerEndpointSettings.LocalHttpUrl;
        [SerializeField] private string _startSceneName = "GameStart_VSlice";
        [SerializeField] private int _ticketPurchaseGoldCost = 3;

        private CancellationTokenSource _requestCancellation;
        private bool _isBusy;
        private bool HasPendingPurchase => PendingAccountOperation.HasPending(AccountServerUrl, "ticket_purchase");

        private string AccountServerUrl =>
            Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);

        private void Awake()
        {
            var rewardedProductPanel = EnsureRewardedProductPanel();
            EnsureRewardedTicketController();
            _rewardedTicketController?.ConfigureShopUi(rewardedProductPanel);
            BindButtons();
            RefreshUi();
        }

        private void OnEnable()
        {
            AccountOperationRecovery.Resolved += OnOperationResolved;
            if (!AccountSessionState.IsAuthenticated)
            {
                ReturnToStartSceneFromUi();
                return;
            }

            var rewardedProductPanel = EnsureRewardedProductPanel();
            EnsureRewardedTicketController();
            _rewardedTicketController?.ConfigureShopUi(rewardedProductPanel);
            BindButtons();
            RefreshAccountFromServerAsync();
        }

        private void OnDisable()
        {
            AccountOperationRecovery.Resolved -= OnOperationResolved;
            CancelRequest();
        }

        private void OnDestroy()
        {
            CancelRequest();
        }

        public async void PurchaseTicketFromUi()
        {
            if (_isBusy || PendingAccountOperation.HasAnyPending(AccountServerUrl))
            {
                return;
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                ReturnToStartSceneFromUi();
                return;
            }

            if (AccountSessionState.ResourceGold < _ticketPurchaseGoldCost)
            {
                SetStatus($"골드가 부족합니다. 티켓 1개 구매에는 골드 {_ticketPurchaseGoldCost}개가 필요합니다.");
                RefreshUi();
                return;
            }

            BeginRequest();
            SetStatus("티켓을 구매하는 중입니다...");
            RefreshUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.PurchaseTicketAsync(
                    AccountSessionState.SessionToken,
                    _requestCancellation.Token);
                AccountSessionState.ApplyPurchaseTicketResponse(response);

                var goldCost = response.ResolvedResourceGoldCost > 0
                    ? response.ResolvedResourceGoldCost
                    : _ticketPurchaseGoldCost;
                var ticketCount = response.ResolvedTicketCount > 0
                    ? response.ResolvedTicketCount
                    : 1;
                SetStatus($"구매 완료: 골드 -{goldCost}, 티켓 +{ticketCount}");
                Debug.Log(
                    $"After333 shop ticket purchased: tickets=+{ticketCount}, gold=-{goldCost}, " +
                    $"remainingTickets={AccountSessionState.Tickets}, remainingGold={AccountSessionState.ResourceGold}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 shop ticket purchase failed: {ex.Message}");
                SetStatus(HasPendingPurchase ? string.Empty : $"티켓 구매 실패: {ex.Message}");
            }
            finally
            {
                EndRequest();
                RefreshUi();
            }
        }

        public void ReturnToStartSceneFromUi()
        {
            var sceneName = string.IsNullOrWhiteSpace(_startSceneName)
                ? "GameStart_VSlice"
                : _startSceneName;
            SceneManager.LoadScene(sceneName);
        }

        private async void RefreshAccountFromServerAsync()
        {
            if (_isBusy || !AccountSessionState.IsAuthenticated)
            {
                return;
            }

            BeginRequest();
            SetStatus("상점 정보를 불러오는 중입니다...");
            RefreshUi();

            try
            {
                var client = new GuestAuthClient(AccountServerUrl);
                var response = await client.GetMeAsync(
                    AccountSessionState.SessionToken,
                    _requestCancellation.Token);
                AccountSessionState.ApplyMeResponse(response);
                SetStatus("구매할 상품을 선택해주세요.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 shop account refresh failed: {ex.Message}");
                SetStatus("서버 계정 정보를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.");
            }
            finally
            {
                EndRequest();
                RefreshUi();
            }
        }

        private void OnOperationResolved(string operation, string card, bool completed, MeResponse me)
        {
            if (operation == "ticket_purchase") SetStatus(completed ? "구매 완료" : "구매 취소");
            RefreshUi();
        }

        private void BindButtons()
        {
            if (_purchaseTicketButton != null)
            {
                _purchaseTicketButton.onClick.RemoveListener(PurchaseTicketFromUi);
                _purchaseTicketButton.onClick.AddListener(PurchaseTicketFromUi);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(ReturnToStartSceneFromUi);
                _backButton.onClick.AddListener(ReturnToStartSceneFromUi);
            }
        }

        private void RefreshUi()
        {
            if (_titleText != null && _shopVisualVersion == 0)
            {
                _titleText.text = "상점";
            }

            if (_accountText != null)
            {
                AccountWalletView.ShowSession(_accountText);
            }

            if (_purchaseTicketButton != null)
            {
                _purchaseTicketButton.interactable =
                    !_isBusy &&
                    AccountSessionState.IsAuthenticated &&
                    !PendingAccountOperation.HasAnyPending(AccountServerUrl) && AccountSessionState.ResourceGold >= _ticketPurchaseGoldCost;
            }

            if (_purchaseTicketButtonLabel != null)
            {
                _purchaseTicketButtonLabel.text = _shopVisualVersion > 0 ? "티켓 구매" : $"게임 티켓 1개 구매\n골드 {_ticketPurchaseGoldCost}";
            }

            if (_ticketPriceText != null)
            {
                _ticketPriceText.text = $"골드 {_ticketPurchaseGoldCost}";
            }

            if (_backButton != null)
            {
                _backButton.interactable = !_isBusy;
            }

            if (_backButtonLabel != null && _shopVisualVersion == 0)
            {
                _backButtonLabel.text = "시작 화면으로";
            }

            _rewardedTicketController?.RefreshUi();
        }

        public void RefreshUiFromExternalState()
        {
            RefreshUi();
        }

        private void EnsureRewardedTicketController()
        {
            _rewardedTicketController ??= GetComponent<RewardedTicketController>();
            if (_rewardedTicketController == null)
            {
                _rewardedTicketController = gameObject.AddComponent<RewardedTicketController>();
            }

            _rewardedTicketController.EnsureEditableHierarchy();
        }

        private RectTransform EnsureRewardedProductPanel()
        {
            var root = transform as RectTransform;
            if (root == null)
            {
                return null;
            }

            if (root.Find("RewardedTicketProductPanel") is RectTransform existing)
            {
                return existing;
            }

            if (root.Find("TicketProductPanel") is RectTransform ticketPanel)
            {
                ticketPanel.anchorMin = new Vector2(0.07f, 0.18f);
                ticketPanel.anchorMax = new Vector2(0.48f, 0.7f);
                ticketPanel.offsetMin = Vector2.zero;
                ticketPanel.offsetMax = Vector2.zero;
            }

            var panelObject = new GameObject(
                "RewardedTicketProductPanel",
                typeof(RectTransform),
                typeof(Image));
            var panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = new Vector2(0.52f, 0.18f);
            panel.anchorMax = new Vector2(0.93f, 0.7f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0.055f, 0.11f, 0.13f, 0.97f);

            CreateRewardedPanelText(
                "RewardedProductTitleText",
                panel,
                "광고 보상",
                42,
                FontStyle.Bold,
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.94f),
                new Color(1f, 0.9f, 0.56f, 1f));
            CreateRewardedPanelText(
                "RewardedProductDescriptionText",
                panel,
                "광고를 끝까지 시청하면 게임 티켓 1개를 받습니다.",
                28,
                FontStyle.Normal,
                new Vector2(0.08f, 0.5f),
                new Vector2(0.92f, 0.7f),
                Color.white);
            return panel;
        }

        private void CreateRewardedPanelText(
            string objectName,
            RectTransform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var text = textObject.GetComponent<Text>();
            textObject.transform.SetParent(parent, false);
            text.font = _titleText != null && _titleText.font != null
                ? _titleText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status ?? string.Empty;
            }
        }

        private void BeginRequest()
        {
            CancelRequest();
            _requestCancellation = new CancellationTokenSource();
            _isBusy = true;
        }

        private void EndRequest()
        {
            _isBusy = false;
            if (_requestCancellation == null)
            {
                return;
            }

            _requestCancellation.Dispose();
            _requestCancellation = null;
        }

        private void CancelRequest()
        {
            if (_requestCancellation == null)
            {
                return;
            }

            _requestCancellation.Cancel();
            _requestCancellation.Dispose();
            _requestCancellation = null;
            _isBusy = false;
        }
    }
}
