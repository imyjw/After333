using System;
using System.Collections.Generic;
using System.Threading;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Ads;
using Project333.Runtime.Presentation.Cards;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Shop
{
    [RequireComponent(typeof(RewardedTicketController))]
    public sealed partial class ShopSceneController : MonoBehaviour
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
        [SerializeField] private string _accountServerUrl = Project333ServerEndpointSettings.LocalHttpUrl;
        [SerializeField] private string _startSceneName = "GameStart_VSlice";
        [SerializeField] private int _ticketPurchaseGoldCost = 3;

        private CancellationTokenSource _requestCancellation;
        private bool _isBusy;
        private Button _purchaseCardPackButton;
        private Text _cardPackButtonLabel;
        private ShopCardPackRevealView _cardPackReveal;
        private RectTransform _revealHost;
        private string _shownPackId, _shownPackOwner, _shownPackUrl;
        private string _displayOwner, _displayToken, _displayUrl;
        private float _nextPackRestore;
        private readonly Dictionary<string, JsonCardDefinitionRecord> _cards = new Dictionary<string, JsonCardDefinitionRecord>(StringComparer.OrdinalIgnoreCase);
        private string AccountServerUrl => Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);
        private bool HasPendingPurchase => PendingAccountOperation.HasPending(AccountServerUrl, "ticket_purchase");
        private string UnreadPackId => PendingCardPackPresentation.GetRequestId(AccountServerUrl, AccountSessionState.AccountId);
        private bool PackIsShowing => _cardPackReveal != null && _cardPackReveal.IsShowing;
        public bool IsInteractionLocked => _isBusy || PackIsShowing || PendingAccountOperation.HasAnyPending(AccountServerUrl);
        private bool RewardedAdIsActive => _rewardedTicketController != null && _rewardedTicketController.IsRewardInteractionActive;

        private void Awake()
        {
            EnsureShopProducts();
            LoadCardDisplayData();
            BindButtons();
            RefreshUi();
        }

        private void OnEnable()
        {
            if (TryInitializeTutorialShop()) return;
            AccountOperationRecovery.Resolved += OnOperationResolved;
            if (!AccountSessionState.IsAuthenticated) { ReturnToStartSceneFromUi(); return; }
            CaptureDisplayContext();
            EnsureShopProducts();
            BindButtons();
            RefreshAccountFromServerAsync();
        }

        private void OnDisable()
        {
            AccountOperationRecovery.Resolved -= OnOperationResolved;
            CancelRequest();
            HideCardPack();
        }

        private void OnDestroy()
        {
            CancelRequest();
            if (_revealHost != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_revealHost.gameObject);
                else DestroyImmediate(_revealHost.gameObject);
            }
        }

        private void Update()
        {
            if (IsTutorialShopMode) return;
            if (_displayOwner != AccountSessionState.AccountId || _displayToken != AccountSessionState.SessionToken || _displayUrl != AccountServerUrl)
            {
                CancelRequest();
                HideCardPack();
                CaptureDisplayContext();
                if (AccountSessionState.IsAuthenticated) RefreshAccountFromServerAsync();
                else RefreshUi();
            }
            if (!_isBusy && !PackIsShowing && Time.unscaledTime >= _nextPackRestore)
            {
                _nextPackRestore = Time.unscaledTime + 5f;
                if (!string.IsNullOrEmpty(UnreadPackId)) RestoreCardPackAsync();
            }
        }

        public async void PurchaseTicketFromUi()
        {
            if (IsTutorialShopMode) return;
            if (IsInteractionLocked || RewardedAdIsActive) return;
            if (!AccountSessionState.IsAuthenticated) { ReturnToStartSceneFromUi(); return; }
            if (AccountSessionState.ResourceGold < _ticketPurchaseGoldCost)
            {
                SetStatus($"골드가 부족합니다. 티켓 1개 구매에는 골드 {_ticketPurchaseGoldCost}개가 필요합니다.");
                RefreshUi(); return;
            }
            var request = BeginRequest();
            var owner = AccountSessionState.AccountId; var token = AccountSessionState.SessionToken; var url = AccountServerUrl;
            SetStatus("티켓을 구매하는 중입니다..."); RefreshUi();
            try
            {
                var response = await new ServerRunClient(url).PurchaseTicketAsync(token, request.Token);
                if (!IsCurrentRequest(request, owner, token, url)) return;
                AccountSessionState.ApplyPurchaseTicketResponse(response);
                var goldCost = response.ResolvedResourceGoldCost > 0 ? response.ResolvedResourceGoldCost : _ticketPurchaseGoldCost;
                var ticketCount = response.ResolvedTicketCount > 0 ? response.ResolvedTicketCount : 1;
                SetStatus($"구매 완료: 골드 -{goldCost}, 티켓 +{ticketCount}");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsCurrentRequest(request, owner, token, url)) SetStatus(HasPendingPurchase ? string.Empty : ex.Message);
            }
            finally { EndRequest(request); }
        }

        public async void PurchaseCardPackFromUi()
        {
            if (IsTutorialShopMode) return;
            // The next pack cannot overwrite an unread receipt or an uncertain economic operation.
            if (IsInteractionLocked || RewardedAdIsActive || !string.IsNullOrEmpty(UnreadPackId)) return;
            if (!AccountSessionState.IsAuthenticated) { ReturnToStartSceneFromUi(); return; }
            if (AccountSessionState.ResourceGold < ServerShopClient.CardPackGoldCost)
            {
                SetStatus("골드가 부족합니다. 카드팩 구매에는 골드 33개가 필요합니다."); RefreshUi(); return;
            }
            var request = BeginRequest();
            var owner = AccountSessionState.AccountId; var token = AccountSessionState.SessionToken; var url = AccountServerUrl;
            SetStatus("카드팩을 구매하는 중입니다..."); RefreshUi();
            try
            {
                var receipt = await new ServerShopClient(url).PurchaseCardPackAsync(token, request.Token);
                if (!IsCurrentRequest(request, owner, token, url)) return;
                ShowPaidCardPack(receipt, owner, url);
                SetStatus("카드 10장이 보유 카드에 추가되었습니다.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsCurrentRequest(request, owner, token, url))
                    SetStatus(PendingAccountOperation.HasPending(url, ServerShopClient.OperationType) ? string.Empty : ex.Message);
            }
            finally { EndRequest(request); }
        }

        public void ReturnToStartSceneFromUi()
        {
            if (IsTutorialShopMode) return;
            if (PackIsShowing) return;
            SceneManager.LoadScene(string.IsNullOrWhiteSpace(_startSceneName) ? "GameStart_VSlice" : _startSceneName);
        }

        private async void RefreshAccountFromServerAsync()
        {
            if (IsTutorialShopMode) return;
            if (_isBusy || !AccountSessionState.IsAuthenticated) return;
            var request = BeginRequest();
            var owner = AccountSessionState.AccountId; var token = AccountSessionState.SessionToken; var url = AccountServerUrl;
            SetStatus("상점 정보를 불러오는 중입니다..."); RefreshUi();
            try
            {
                var response = await new GuestAuthClient(url).GetMeAsync(token, request.Token);
                if (!IsCurrentRequest(request, owner, token, url)) return;
                if (response?.ResolvedAccount == null || !string.Equals(response.ResolvedAccount.ResolvedId, owner, StringComparison.OrdinalIgnoreCase) ||
                    response.ResolvedWallet == null || response.ResolvedCollectionSummary == null)
                    throw new InvalidOperationException("서버 계정 정보를 확인하지 못했습니다.");
                AccountSessionState.ApplyMeResponse(response);
                SetStatus("구매할 상품을 선택해주세요.");
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (IsCurrentRequest(request, owner, token, url)) SetStatus("서버 계정 정보를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.");
            }
            finally { EndRequest(request); }
        }

        private async void RestoreCardPackAsync()
        {
            if (IsTutorialShopMode) return;
            _nextPackRestore = Time.unscaledTime + 5f;
            if (_isBusy || PackIsShowing || RewardedAdIsActive || !AccountSessionState.IsAuthenticated || PendingAccountOperation.HasPending(AccountServerUrl, ServerShopClient.OperationType)) return;
            var id = UnreadPackId;
            if (string.IsNullOrEmpty(id)) return;
            var request = BeginRequest();
            var owner = AccountSessionState.AccountId; var token = AccountSessionState.SessionToken; var url = AccountServerUrl;
            RefreshUi();
            try
            {
                // Read-only receipt lookup. Recovery never resends the purchase or rerolls the cards.
                var receipt = await new ServerShopClient(url).GetCardPackPurchaseAsync(token, id, request.Token);
                if (!IsCurrentRequest(request, owner, token, url)) return;
                ShowPaidCardPack(receipt, owner, url);
                SetStatus("이전에 구매한 카드팩입니다. 카드 10장은 이미 지급되었습니다.");
            }
            catch (CardPackReceiptException ex)
            {
                if (!IsCurrentRequest(request, owner, token, url)) return;
                if (ex.Code == "card_pack_purchase_not_found" || ex.Code == "operation_cancelled")
                    PendingCardPackPresentation.Clear(url, owner, id);
                else SetStatus(ex.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (IsCurrentRequest(request, owner, token, url)) SetStatus("카드팩 구매 내역을 확인하는 중입니다. 잠시 후 다시 시도합니다.");
            }
            finally { EndRequest(request); }
        }

        private void ShowPaidCardPack(CardPackPurchaseResponse receipt, string owner, string url)
        {
            EnsureCardPackReveal();
            if (_cardPackReveal == null) throw new InvalidOperationException("카드팩을 표시할 화면을 찾지 못했습니다.");
            var items = new ShopCardPackRevealItem[ServerShopClient.CardPackCardCount];
            for (var i = 0; i < items.Length; i++)
            {
                var id = receipt.ResolvedCardIds[i];
                _cards.TryGetValue(id, out var card);
                CardArtworkLibrary.TryGetArtwork(id, out var artwork);
                items[i] = new ShopCardPackRevealItem(id, card?.DisplayName ?? id, card?.Rarity ?? CardRarity.Common, artwork);
            }
            _shownPackId = receipt.ResolvedRequestId; _shownPackOwner = owner; _shownPackUrl = url;
            _revealHost.gameObject.SetActive(true);
            _cardPackReveal.ShowPack(items);
        }

        private void DismissCardPack()
        {
            if (_shownPackOwner == AccountSessionState.AccountId && _shownPackUrl == AccountServerUrl)
                PendingCardPackPresentation.Clear(_shownPackUrl, _shownPackOwner, _shownPackId);
            HideCardPack();
            SetStatus("카드팩에서 얻은 카드는 보유 카드에서 확인할 수 있습니다.");
            RefreshUi();
        }

        private void HideCardPack()
        {
            if (_cardPackReveal != null) _cardPackReveal.Hide();
            if (_revealHost != null) _revealHost.gameObject.SetActive(false);
            _shownPackId = _shownPackOwner = _shownPackUrl = null;
        }

        private void EnsureCardPackReveal()
        {
            if (_cardPackReveal != null) return;
            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform as RectTransform : transform as RectTransform;
            if (parent == null) return;
            _revealHost = Rect("CardPackOverlay", parent, Vector2.zero, Vector2.one);
            var overlayCanvas = _revealHost.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true; overlayCanvas.sortingOrder = 31000;
            _revealHost.gameObject.AddComponent<GraphicRaycaster>();
            _cardPackReveal = ShopCardPackRevealView.Create(_revealHost, DismissCardPack);
            _revealHost.gameObject.SetActive(false);
        }

        private void LoadCardDisplayData()
        {
            var json = Resources.Load<TextAsset>("Project333/Data/cards");
            if (json == null) return;
            foreach (var card in JsonCardDefinitionDatabase.FromJson(json.text).Cards)
                if (card != null && !string.IsNullOrWhiteSpace(card.Id)) _cards[card.Id] = card;
        }

        private void OnOperationResolved(string operation, string card, bool completed, MeResponse me)
        {
            if (operation == "ticket_purchase") SetStatus(completed ? "구매 완료" : "구매 취소");
            else if (operation == ServerShopClient.OperationType)
            {
                SetStatus(completed ? "카드팩 구매가 완료되었습니다." : "카드팩 구매가 취소되었습니다.");
                _nextPackRestore = 0;
            }
            RefreshUi();
        }

        private void BindButtons()
        {
            if (_purchaseTicketButton != null) { _purchaseTicketButton.onClick.RemoveListener(PurchaseTicketFromUi); _purchaseTicketButton.onClick.AddListener(PurchaseTicketFromUi); }
            if (_purchaseCardPackButton != null) { _purchaseCardPackButton.onClick.RemoveListener(PurchaseCardPackFromUi); _purchaseCardPackButton.onClick.AddListener(PurchaseCardPackFromUi); }
            if (_backButton != null) { _backButton.onClick.RemoveListener(ReturnToStartSceneFromUi); _backButton.onClick.AddListener(ReturnToStartSceneFromUi); }
        }

        private void RefreshUi()
        {
            if (IsTutorialShopMode) { RefreshTutorialShopUi(); return; }
            if (_titleText != null && _shopVisualVersion == 0) _titleText.text = "상점";
            if (_accountText != null) AccountWalletView.ShowSession(_accountText);
            var canBuy = !IsInteractionLocked && !RewardedAdIsActive && AccountSessionState.IsAuthenticated;
            if (_purchaseTicketButton != null) _purchaseTicketButton.interactable = canBuy && AccountSessionState.ResourceGold >= _ticketPurchaseGoldCost;
            if (_purchaseTicketButtonLabel != null) _purchaseTicketButtonLabel.text = "티켓 구매";
            if (_ticketPriceText != null) _ticketPriceText.text = $"골드 {_ticketPurchaseGoldCost}";
            if (_purchaseCardPackButton != null) _purchaseCardPackButton.interactable = canBuy && string.IsNullOrEmpty(UnreadPackId) && AccountSessionState.ResourceGold >= ServerShopClient.CardPackGoldCost;
            if (_cardPackButtonLabel != null) _cardPackButtonLabel.text = "카드팩 구매";
            if (_backButton != null) _backButton.interactable = !_isBusy && !PackIsShowing;
            if (_backButtonLabel != null && _shopVisualVersion == 0) _backButtonLabel.text = "시작 화면으로";
            _rewardedTicketController?.RefreshUi();
        }

        public void RefreshUiFromExternalState() => RefreshUi();

        private void EnsureShopProducts()
        {
            var rewardedPanel = EnsureRewardedProductPanel();
            _rewardedTicketController ??= GetComponent<RewardedTicketController>();
            if (_rewardedTicketController == null) _rewardedTicketController = gameObject.AddComponent<RewardedTicketController>();
            _rewardedTicketController.EnsureEditableHierarchy();
            _rewardedTicketController.ConfigureShopUi(rewardedPanel);
            var root = transform as RectTransform;
            if (root == null) return;
            var ticketPanel = root.Find("TicketProductPanel") as RectTransform;
            FitProduct(ticketPanel, .055f, .335f);
            FitProduct(rewardedPanel, .665f, .945f);
            FitProduct(EnsureCardPackProductPanel(root), .36f, .64f);
            StyleTicketProduct(ticketPanel, false);
            StyleTicketProduct(rewardedPanel, true);
        }

        private RectTransform EnsureCardPackProductPanel(RectTransform root)
        {
            if (root.Find("CardPackProductPanel") is RectTransform existing)
            {
                _purchaseCardPackButton = existing.Find("PurchaseCardPackButton")?.GetComponent<Button>();
                _cardPackButtonLabel = _purchaseCardPackButton?.GetComponentInChildren<Text>();
                return existing;
            }
            var panel = Rect("CardPackProductPanel", root, Vector2.zero, Vector2.one);
            NoticeTheme.Panel(panel.gameObject.AddComponent<Image>(), false);
            ProductText("CardPackTitle", panel, "카드팩", 42, new Vector2(.08f, .84f), new Vector2(.92f, .97f), new Color(1f, .86f, .54f));
            var art = Rect("CardPackArtwork", panel, new Vector2(.15f, .48f), new Vector2(.85f, .84f)).gameObject.AddComponent<Image>();
            foreach (var sprite in Resources.LoadAll<Sprite>("Project333/UI/Shop/CardPackStates")) if (sprite.name == "CardPackClosed") art.sprite = sprite;
            art.preserveAspect = true; art.raycastTarget = false;
            ProductText("CardPackDescription", panel, "랜덤 카드 10장\n커먼 40% · 언커먼 30% · 레어 18%\n유니크 9% · 레전더리 3%", 24, new Vector2(.04f, .33f), new Vector2(.96f, .48f), Color.white);
            ProductText("CardPackPrice", panel, "골드 33", 36, new Vector2(.08f, .23f), new Vector2(.92f, .32f), new Color(1f, .86f, .54f));
            var buttonRect = Rect("PurchaseCardPackButton", panel, new Vector2(.08f, .075f), new Vector2(.92f, .21f));
            buttonRect.gameObject.AddComponent<Image>();
            _purchaseCardPackButton = buttonRect.gameObject.AddComponent<Button>();
            _cardPackButtonLabel = ProductText("Label", buttonRect, "카드팩 구매", 28, Vector2.zero, Vector2.one, Color.white);
            NoticeTheme.Button(_purchaseCardPackButton);
            return panel;
        }

        // Match the card pack's product layout while retaining the existing ticket art and controls.
        private void StyleTicketProduct(RectTransform panel, bool rewarded)
        {
            if (panel == null) return;
            NoticeTheme.Panel(panel.GetComponent<Image>(), false);
            foreach (var name in new[] { "TopAccent", "ProductCategory", "PriceGoldIcon" })
                if (panel.Find(name) != null) panel.Find(name).gameObject.SetActive(false);
            var title = panel.Find(rewarded ? "RewardedProductTitleText" : "ProductTitleText")?.GetComponent<Text>();
            if (title != null) title.text = rewarded ? "광고 보상" : "게임 티켓";
            StyleProductText(title, 42, new Vector2(.08f, .84f), new Vector2(.92f, .97f), new Color(1f, .86f, .54f));
            StyleProductText(panel.Find(rewarded ? "RewardedProductDescriptionText" : "ProductDescriptionText")?.GetComponent<Text>(),
                24, new Vector2(.04f, .33f), new Vector2(.96f, .48f), Color.white);
            if (panel.Find("TicketIllustration") is RectTransform artwork)
            {
                SetProductRegion(artwork, new Vector2(.18f, .55f), new Vector2(.82f, .78f));
                foreach (var text in artwork.GetComponentsInChildren<Text>(true))
                    if (NoticeTheme.Font != null) text.font = NoticeTheme.Font;
            }
            var priceName = rewarded ? "AdPriceText" : "TicketPriceText";
            var price = panel.Find(priceName)?.GetComponent<Text>();
            if (price == null) price = ProductText(priceName, panel, "", 36, Vector2.zero, Vector2.one, Color.white);
            price.text = rewarded ? "광고 시청" : $"골드 {_ticketPurchaseGoldCost}";
            StyleProductText(price, 36, new Vector2(.08f, .23f), new Vector2(.92f, .32f), new Color(1f, .86f, .54f));
            if (!rewarded) _ticketPriceText = price;
            var button = panel.Find(rewarded ? "WatchRewardedAdButton" : "PurchaseTicketButton")?.GetComponent<Button>();
            if (button != null)
            {
                SetProductRegion((RectTransform)button.transform, new Vector2(.08f, .075f), new Vector2(.92f, .21f));
                NoticeTheme.Button(button);
                if (_purchaseCardPackButton != null) button.colors = _purchaseCardPackButton.colors;
                foreach (var label in button.GetComponentsInChildren<Text>(true))
                    StyleProductText(label, 26, Vector2.zero, Vector2.one, Color.white);
            }
            var status = rewarded ? panel.Find("RewardedAdStatusText")?.GetComponent<Text>() : _statusText;
            StyleProductText(status, 18, new Vector2(.04f, .008f), new Vector2(.96f, .067f), new Color(.72f, .80f, .91f));
            if (status != null) status.resizeTextMinSize = 12;
        }

        private static void SetProductRegion(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void StyleProductText(Text text, int size, Vector2 min, Vector2 max, Color color)
        {
            if (text == null) return;
            SetProductRegion(text.rectTransform, min, max);
            NoticeTheme.Text(text, size); text.color = color;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = size;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void FitProduct(RectTransform panel, float left, float right)
        {
            if (panel == null) return;
            panel.anchorMin = new Vector2(left, .12f); panel.anchorMax = new Vector2(right, .77f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = text.fontSize;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private RectTransform EnsureRewardedProductPanel()
        {
            var root = transform as RectTransform;
            if (root == null) return null;
            if (root.Find("RewardedTicketProductPanel") is RectTransform existing) return existing;
            var panel = Rect("RewardedTicketProductPanel", root, new Vector2(.52f, .18f), new Vector2(.93f, .7f));
            NoticeTheme.Panel(panel.gameObject.AddComponent<Image>(), false);
            ProductText("RewardedProductTitleText", panel, "광고 보상", 42, new Vector2(.08f, .72f), new Vector2(.92f, .94f), new Color(1f, .9f, .56f));
            ProductText("RewardedProductDescriptionText", panel, "광고를 끝까지 시청하면 게임 티켓 1개를 받습니다.", 28, new Vector2(.08f, .5f), new Vector2(.92f, .7f), Color.white);
            return panel;
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text ProductText(string name, RectTransform parent, string value, int size, Vector2 min, Vector2 max, Color color)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = NoticeTheme.Font ?? (_titleText != null && _titleText.font != null ? _titleText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            NoticeTheme.Text(text, size); text.text = value; text.color = color; text.supportRichText = false;
            return text;
        }

        private void SetStatus(string status) { if (_statusText != null) _statusText.text = status ?? string.Empty; }
        private void CaptureDisplayContext()
        {
            _displayOwner = AccountSessionState.AccountId; _displayToken = AccountSessionState.SessionToken; _displayUrl = AccountServerUrl;
            _nextPackRestore = 0;
        }
        private CancellationTokenSource BeginRequest()
        {
            CancelRequest(); _requestCancellation = new CancellationTokenSource(); _isBusy = true;
            return _requestCancellation;
        }
        private bool IsCurrentRequest(CancellationTokenSource request, string owner, string token, string url) =>
            this != null && isActiveAndEnabled && ReferenceEquals(_requestCancellation, request) &&
            !request.IsCancellationRequested && AccountSessionState.IsAuthenticated &&
            AccountSessionState.AccountId == owner && AccountSessionState.SessionToken == token && AccountServerUrl == url;
        private void EndRequest(CancellationTokenSource request)
        {
            if (!ReferenceEquals(_requestCancellation, request)) return;
            _requestCancellation = null; request.Dispose(); _isBusy = false;
            if (this != null && isActiveAndEnabled) RefreshUi();
        }
        private void CancelRequest()
        {
            var previous = _requestCancellation; _requestCancellation = null; _isBusy = false;
            if (previous == null) return;
            previous.Cancel(); previous.Dispose();
        }
    }
}
