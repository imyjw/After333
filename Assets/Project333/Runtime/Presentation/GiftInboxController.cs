using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Startup
{
    [DisallowMultipleComponent]
    public sealed class GiftInboxController : MonoBehaviour
    {
        public const string RootObjectName = "GiftInboxCanvas";
        [SerializeField] private Font _font;
        [SerializeField] private RectTransform _safeAreaRoot;
        [SerializeField] private Button _giftButton;
        [SerializeField] private Text _badge;
        [SerializeField] private RectTransform _modalRoot;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Text _summary;
        [SerializeField] private Text _status;
        [SerializeField] private Button _moreButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private string _serverUrl = Project333ServerEndpointSettings.LocalHttpUrl;
        private readonly List<GiftInboxItemDto> _items = new List<GiftInboxItemDto>();
        private CancellationTokenSource _lifetime;
        private GiftInboxAccountScope _scope;
        private int _generation;
        private bool _loading;
        private bool _tutorialPracticeLoaded;
        private bool IsTutorialPracticeMode => _tutorialPracticeLoaded || TutorialRuntimeIsolation.IsActive;
        private string _claimingId;
        private string _nextCursor;
        private long _unclaimedCount;
        private float _nextRefreshAt;
        private static readonly Color Gold = new Color(1f, 0.84f, 0.48f);
        private static readonly Color PanelColor = new Color(0.022f, 0.07f, 0.086f, 0.98f);
        private string ServerUrl => Project333ServerEndpointSettings.ResolveHttpUrl(_serverUrl);

        public static GiftInboxController EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "GameStart_VSlice") return null;
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GiftInboxController>(true)).FirstOrDefault();
            if (controller == null)
            {
                var root = new GameObject(RootObjectName, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, scene);
                controller = root.AddComponent<GiftInboxController>();
            }
            controller.EnsureEditableHierarchy();
            return controller;
        }

        public void ConfigureEndpoint(string serverUrl) => _serverUrl = serverUrl;

        public void EnsureEditableHierarchy()
        {
            var canvas = GetOrAdd<Canvas>(gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // The existing settings menu retains priority when both overlays are open.
            canvas.sortingOrder = 29990;
            var scaler = GetOrAdd<CanvasScaler>(gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            ResolveFont();
            _safeAreaRoot = Rect("SafeAreaRoot", transform);
            Stretch(_safeAreaRoot);
            var safe = GetOrAdd<SafeAreaFitter>(_safeAreaRoot.gameObject);
            safe.Configure();
            _giftButton = Button("GiftButton", _safeAreaRoot, string.Empty, PanelColor);
            Place((RectTransform)_giftButton.transform, Vector2.one, Vector2.one, new Vector2(86f, 86f), new Vector2(-126f, -24f));
            Outline(_giftButton.gameObject);
            DrawGiftIcon(_giftButton.transform);
            var label = Label("Label", _giftButton.transform, "선물함", 18, Gold);
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(80, 27), new Vector2(0, 3));
            label.alignment = TextAnchor.MiddleCenter;
            _giftButton.onClick.RemoveListener(Open);
            _giftButton.onClick.AddListener(Open);
            var badgeRoot = Rect("Badge", _giftButton.transform);
            Image(badgeRoot, new Color(0.82f, 0.18f, 0.12f));
            Place(badgeRoot, Vector2.one, Vector2.one, new Vector2(40f, 30f), new Vector2(8f, 8f));
            _badge = Label("Count", badgeRoot, string.Empty, 19, Color.white);
            Stretch(_badge.rectTransform);
            _badge.alignment = TextAnchor.MiddleCenter;

            // Reserve one button slot between the wallet and the settings gear, idempotently.
            var settings = gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GlobalSettingsMenuController>(true)).FirstOrDefault();
            var wallet = settings?.transform.Find("SafeAreaRoot/WalletPanel") as RectTransform;
            if (wallet != null) wallet.anchoredPosition = new Vector2(-228f, wallet.anchoredPosition.y);

            var modalExists = transform.Find("SafeAreaRoot/GiftModal") != null;
            _modalRoot = Rect("GiftModal", _safeAreaRoot);
            Stretch(_modalRoot);
            var backdrop = Button("Backdrop", _modalRoot, string.Empty, new Color(0, 0, 0, 0.72f));
            Stretch((RectTransform)backdrop.transform);
            backdrop.onClick.RemoveListener(Close);
            backdrop.onClick.AddListener(Close);
            _panel = Rect("Panel", _modalRoot);
            _panel.anchorMin = new Vector2(0.14f, 0.105f);
            _panel.anchorMax = new Vector2(0.86f, 0.865f);
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            Image(_panel, PanelColor);
            Outline(_panel.gameObject);
            var title = Label("Title", _panel, "선물함", 38, Gold);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, 55f), new Vector2(32f, -22f));
            _summary = Label("Summary", _panel, "", 22, new Color(0.65f, 0.78f, 0.79f));
            Place(_summary.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(750f, 36f), new Vector2(34f, -80f));
            var close = Button("CloseButton", _panel, "닫기", new Color(0.1f, 0.22f, 0.26f));
            Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(90f, 56f), new Vector2(-24f, -22f));
            close.onClick.RemoveListener(Close);
            close.onClick.AddListener(Close);

            var scrollRect = Rect("Scroll", _panel);
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(30f, 90f);
            scrollRect.offsetMax = new Vector2(-30f, -132f);
            var scroll = GetOrAdd<ScrollRect>(scrollRect.gameObject);
            var viewport = Rect("Viewport", scrollRect);
            Stretch(viewport);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            _content = Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            var layout = GetOrAdd<VerticalLayoutGroup>(_content.gameObject);
            layout.spacing = 14f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = GetOrAdd<ContentSizeFitter>(_content.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
            _status = Label("Status", _panel, string.Empty, 20, Color.white);
            _status.rectTransform.anchorMin = Vector2.zero;
            _status.rectTransform.anchorMax = new Vector2(1f, 0f);
            _status.rectTransform.pivot = new Vector2(0f, 0f);
            _status.rectTransform.offsetMin = new Vector2(34f, 22f);
            _status.rectTransform.offsetMax = new Vector2(-365f, 73f);
            _moreButton = Button("MoreButton", _panel, "더 보기", new Color(0.13f, 0.29f, 0.32f));
            Place((RectTransform)_moreButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(150, 52), new Vector2(-194, 24));
            _moreButton.onClick.RemoveListener(LoadMore);
            _moreButton.onClick.AddListener(LoadMore);
            _refreshButton = Button("RefreshButton", _panel, "새로고침", new Color(0.13f, 0.29f, 0.32f));
            Place((RectTransform)_refreshButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(150, 52), new Vector2(-28, 24));
            _refreshButton.onClick.RemoveListener(Refresh);
            _refreshButton.onClick.AddListener(Refresh);
            if (!modalExists) _modalRoot.gameObject.SetActive(false);
            RefreshHeader();
            if (IsTutorialPracticeMode && _giftButton != null) _giftButton.interactable = true;
        }

        private void Awake()
        {
            _tutorialPracticeLoaded = TutorialRuntimeIsolation.IsActive;
            EnsureEditableHierarchy();
            Close();
        }
        private void OnEnable() { _nextRefreshAt = 0; }
        private void OnDisable() { ResetAccount(); }
        private void OnDestroy() { ResetAccount(); }
        private void OnApplicationFocus(bool focused) { if (focused) _nextRefreshAt = 0; }
        private void Update()
        {
            if (!UnityEngine.Application.isPlaying || IsTutorialPracticeMode) return;
            EnsureAccountScope();
            if (_scope.IsAuthenticated && !_loading && _claimingId == null && Time.unscaledTime >= _nextRefreshAt)
                _ = RefreshAsync(false);
        }

        private void EnsureAccountScope()
        {
            if (_scope.Matches(AccountSessionState.AccountId, AccountSessionState.SessionToken, ServerUrl)) return;
            if (!_scope.IsAuthenticated && string.IsNullOrWhiteSpace(AccountSessionState.AccountId)) return;
            ResetAccount();
            _scope = new GiftInboxAccountScope(AccountSessionState.AccountId, AccountSessionState.SessionToken, ServerUrl);
            if (_scope.IsAuthenticated) _lifetime = new CancellationTokenSource();
            RefreshHeader();
        }

        private void ResetAccount()
        {
            ++_generation;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _scope = default;
            _loading = false;
            _claimingId = null;
            _nextCursor = null;
            _unclaimedCount = 0;
            _nextRefreshAt = 0;
            _items.Clear();
            ClearRows();
            if (_status != null) _status.text = string.Empty;
            Close();
            RefreshHeader();
        }

        private bool IsCurrent(int generation, GiftInboxAccountScope scope) =>
            this != null && !IsTutorialPracticeMode && isActiveAndEnabled && generation == _generation &&
            scope.Matches(AccountSessionState.AccountId, AccountSessionState.SessionToken, ServerUrl);

        public void Open()
        {
            if (IsTutorialPracticeMode) return;
            EnsureAccountScope();
            _modalRoot.gameObject.SetActive(true);
            if (!_scope.IsAuthenticated)
            {
                _status.text = "로그인 후 선물함을 이용할 수 있습니다.";
                RefreshHeader();
                return;
            }
            if (_claimingId == null) _ = RefreshAsync(false);
        }
        public void Close() { if (_modalRoot != null) _modalRoot.gameObject.SetActive(false); }
        private void LoadMore() { if (!string.IsNullOrEmpty(_nextCursor)) _ = RefreshAsync(true); }
        private void Refresh() => _ = RefreshAsync(false);

        private async Task RefreshAsync(bool append)
        {
            if (IsTutorialPracticeMode) return;
            EnsureAccountScope();
            if (!_scope.IsAuthenticated || _loading || _claimingId != null) return;
            var generation = _generation;
            var scope = _scope;
            var token = _lifetime.Token;
            _loading = true;
            _status.text = "불러오는 중…";
            RefreshHeader();
            try
            {
                var cursor = append ? _nextCursor : null;
                if (!append) _items.Clear();
                do
                {
                    var result = await new GiftInboxClient(scope.ServerUrl).GetAsync(scope.SessionToken, cursor, token);
                    if (!IsCurrent(generation, scope)) return;
                    foreach (var gift in result.ResolvedGifts)
                    {
                        if (gift == null || string.IsNullOrWhiteSpace(gift.ResolvedGiftId)) continue;
                        var previous = _items.FindIndex(x => x.ResolvedGiftId == gift.ResolvedGiftId);
                        if (previous < 0) _items.Add(gift); else _items[previous] = gift;
                    }
                    _unclaimedCount = result.ResolvedUnclaimedCount;
                    _nextCursor = result.ResolvedNextCursor;
                    if (string.Equals(cursor, _nextCursor, StringComparison.Ordinal)) break;
                    cursor = _nextCursor;
                    // Older unclaimed gifts must remain reachable when a whole page is claimed history.
                } while (!_items.Any(x => !x.IsClaimed) && !string.IsNullOrEmpty(cursor));
                _status.text = !_items.Any(x => !x.IsClaimed) ? "표시할 선물이 없습니다." : string.Empty;
                BuildRows();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsCurrent(generation, scope)) _status.text = ex is GiftInboxException ? ex.Message : "선물 정보를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.";
            }
            finally
            {
                if (IsCurrent(generation, scope))
                {
                    _loading = false;
                    _nextRefreshAt = Time.unscaledTime + (_modalRoot.gameObject.activeSelf ? 15f : 30f);
                    BuildRows();
                    RefreshHeader();
                }
            }
        }

        public async void Claim(string giftId)
        {
            if (IsTutorialPracticeMode) return;
            EnsureAccountScope();
            if (_loading || _claimingId != null || !_scope.IsAuthenticated || !_items.Exists(x => x.ResolvedGiftId == giftId && x.CanClaim)) return;
            var generation = _generation;
            var scope = _scope;
            var token = _lifetime.Token;
            _claimingId = giftId;
            _status.text = "받는 중…";
            BuildRows();
            RefreshHeader();
            try
            {
                var client = new GiftInboxClient(scope.ServerUrl);
                while (IsCurrent(generation, scope))
                {
                    try
                    {
                        var result = await client.ClaimAsync(scope.SessionToken, giftId, token);
                        if (!IsCurrent(generation, scope)) return;
                        // Apply only the authoritative wallet and only to the captured current account.
                        AccountSessionState.ApplyGiftWallet(scope.AccountId, scope.SessionToken, result.ResolvedWallet);
                        foreach (var item in _items.Where(x => x.ResolvedGiftId == giftId))
                        {
                            item.Status = "claimed";
                            item.ClaimedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                        }
                        _unclaimedCount = Math.Max(0, _unclaimedCount - 1);
                        _status.text = "선물을 받았습니다.";
                        var start = gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameStartSceneController>(true)).FirstOrDefault();
                        start?.RefreshUiFromExternalState();
                        break;
                    }
                    catch (GiftInboxException ex) when (ex.IsRetryable) { await Task.Delay(3000, token); }
                    catch (TimeoutException) { await Task.Delay(3000, token); }
                }
            }
            catch (OperationCanceledException) { }
            catch (GiftInboxException ex)
            {
                if (IsCurrent(generation, scope))
                {
                    _status.text = ex.Message;
                    if (ex.Code == "gift_expired")
                        foreach (var item in _items.Where(x => x.ResolvedGiftId == giftId)) item.Status = "expired";
                }
            }
            catch (Exception) { if (IsCurrent(generation, scope)) _status.text = "선물을 받지 못했습니다. 잠시 후 다시 시도해주세요."; }
            finally
            {
                if (IsCurrent(generation, scope))
                {
                    _claimingId = null;
                    _nextRefreshAt = Time.unscaledTime + 3f;
                    BuildRows();
                    RefreshHeader();
                }
            }
        }

        private void RefreshHeader()
        {
            if (_badge != null)
            {
                _badge.text = _unclaimedCount > 99 ? "99+" : _unclaimedCount.ToString(CultureInfo.InvariantCulture);
                _badge.transform.parent.gameObject.SetActive(_unclaimedCount > 0);
            }
            if (_summary != null) _summary.text = _unclaimedCount > 0 ? $"받을 수 있는 선물 {_unclaimedCount:N0}개" : "관리자가 보낸 선물을 확인하세요.";
            if (_moreButton != null)
            {
                _moreButton.gameObject.SetActive(!string.IsNullOrEmpty(_nextCursor));
                _moreButton.interactable = !_loading && _claimingId == null;
            }
            if (_refreshButton != null) _refreshButton.interactable = _scope.IsAuthenticated && !_loading && _claimingId == null;
        }

        private void ClearRows()
        {
            if (_content == null) return;
            for (var i = _content.childCount - 1; i >= 0; --i)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                // Destroy is deferred until frame end. Remove the old name from Content now,
                // so another refresh in this frame cannot reuse a disabled, doomed row.
                child.transform.SetParent(null, false);
                if (UnityEngine.Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
        private void BuildRows()
        {
            if (_content == null) return;
            ClearRows();
            foreach (var gift in _items.Where(x => !x.IsClaimed))
            {
                var row = Rect("Gift_" + gift.ResolvedGiftId, _content);
                Image(row, gift.IsClaimed ? new Color(0.055f, 0.11f, 0.125f) : new Color(0.075f, 0.17f, 0.195f));
                var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(22, 22, 18, 18);
                layout.spacing = 10f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                Label("Title", row, gift.ResolvedTitle, 28, Gold);
                if (!string.IsNullOrWhiteSpace(gift.ResolvedMessage)) Label("Message", row, gift.ResolvedMessage, 23, new Color(0.88f, 0.92f, 0.91f));
                Label("Dates", row, BuildDateText(gift), 19, new Color(0.59f, 0.74f, 0.75f));
                var footer = Rect("Footer", row);
                var horizontal = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
                horizontal.spacing = 20f;
                horizontal.childControlWidth = horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = false;
                horizontal.childForceExpandHeight = false;
                horizontal.childAlignment = TextAnchor.MiddleLeft;
                var attachments = Label("Attachments", footer, BuildAttachmentText(gift), 25, Gold);
                var attachmentLayout = attachments.gameObject.AddComponent<LayoutElement>();
                attachmentLayout.flexibleWidth = 1f;
                var label = _claimingId == gift.ResolvedGiftId ? "받는 중…" : gift.IsClaimed ? "수령 완료" : gift.CanClaim ? "받기" : gift.ResolvedStatus == "expired" ? "기간 만료" : "받을 수 없음";
                var claim = Button("ClaimButton", footer, label, gift.CanClaim ? new Color(0.25f, 0.37f, 0.25f) : new Color(0.15f, 0.2f, 0.21f));
                var claimLayout = claim.gameObject.AddComponent<LayoutElement>();
                claimLayout.minWidth = claimLayout.preferredWidth = 152f;
                claimLayout.minHeight = claimLayout.preferredHeight = 58f;
                claim.interactable = gift.CanClaim && _claimingId == null && !_loading;
                var id = gift.ResolvedGiftId;
                claim.onClick.AddListener(() => Claim(id));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        public static string BuildAttachmentText(GiftInboxItemDto gift)
        {
            var values = new List<string>();
            if (gift.ResolvedTickets > 0) values.Add($"게임 티켓 {gift.ResolvedTickets:N0}장");
            if (gift.ResolvedResourceGold > 0) values.Add($"골드 {gift.ResolvedResourceGold:N0}");
            return string.Join("    ·    ", values);
        }
        public static string BuildDateText(GiftInboxItemDto gift)
        {
            if (gift.IsClaimed) return "수령 완료";
            if (string.IsNullOrEmpty(gift.ResolvedExpiresAtUtc)) return "수령 기한 없음";
            return DateTimeOffset.TryParse(gift.ResolvedExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires)
                ? $"수령 기한  {expires.ToLocalTime():yyyy.MM.dd HH:mm}" : string.Empty;
        }
        private void ResolveFont()
        {
            if (_font != null) return;
            if (gameObject.scene.IsValid())
                _font = gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Text>(true))
                    .Select(x => x.font).FirstOrDefault(x => x != null && x.name.IndexOf("Noto", StringComparison.OrdinalIgnoreCase) >= 0);
#if UNITY_EDITOR
            if (_font == null) _font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf");
#endif
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            // Unity's missing-component sentinel is not CLR null in the editor.
            var component = target.GetComponent<T>();
            if (component == null) component = target.AddComponent<T>();
            return component;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static Image Image(RectTransform rect, Color color)
        { var image = GetOrAdd<Image>(rect.gameObject); image.color = color; return image; }
        private static void Outline(GameObject obj)
        {
            var outline = GetOrAdd<Outline>(obj);
            outline.effectColor = new Color(0.57f, 0.4f, 0.17f);
            outline.effectDistance = new Vector2(2, -2);
        }
        private Text Label(string name, Transform parent, string value, int size, Color color)
        {
            var rect = Rect(name, parent);
            var text = GetOrAdd<Text>(rect.gameObject);
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }
        private Button Button(string name, Transform parent, string label, Color color)
        {
            var rect = Rect(name, parent);
            var image = Image(rect, color);
            var button = GetOrAdd<Button>(rect.gameObject);
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(label))
            {
                var text = Label("Label", rect, label, 22, Color.white);
                Stretch(text.rectTransform);
                text.alignment = TextAnchor.MiddleCenter;
            }
            return button;
        }
        private static void DrawGiftIcon(Transform parent)
        {
            var gold = new Color(0.95f, 0.74f, 0.31f);
            var teal = new Color(0.09f, 0.21f, 0.24f);
            IconPart("Box", new Vector2(0, 5), new Vector2(38, 28), gold);
            IconPart("Lid", new Vector2(0, 23), new Vector2(46, 8), gold);
            IconPart("Ribbon", new Vector2(0, 7), new Vector2(6, 33), teal);
            var left = IconPart("BowLeft", new Vector2(-8, 32), new Vector2(16, 6), gold);
            left.localEulerAngles = new Vector3(0, 0, -30);
            var right = IconPart("BowRight", new Vector2(8, 32), new Vector2(16, 6), gold);
            right.localEulerAngles = new Vector3(0, 0, 30);
            RectTransform IconPart(string name, Vector2 position, Vector2 size, Color color)
            {
                var rect = Rect(name, parent);
                Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
                Image(rect, color).raycastTarget = false;
                return rect;
            }
        }

#if UNITY_EDITOR
        // Preview data never enters account state and this method never performs HTTP requests.
        public void SetEditorPreview(GiftInboxItemDto[] gifts, int unclaimedCount)
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Preview is edit-mode only.");
            EnsureEditableHierarchy();
            _items.Clear();
            _items.AddRange(gifts ?? Array.Empty<GiftInboxItemDto>());
            _unclaimedCount = unclaimedCount;
            _modalRoot.gameObject.SetActive(true);
            _status.text = string.Empty;
            BuildRows();
            RefreshHeader();
        }
#endif
    }
}
