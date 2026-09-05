using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.OwnedCards
{
    public sealed class OwnedCardsSceneController : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int _collectionVisualVersion;
        public int CollectionVisualVersion => _collectionVisualVersion;

        private const int GridColumnCount = 6;
        private const int GridRowCount = 2;
        private const int CardsPerPage = GridColumnCount * GridRowCount;

        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _accountText;
        [SerializeField] private Text _ownedCardsText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _backButtonLabel;
        [Header("Card Grid")]
        [SerializeField] private RectTransform _cardGridRoot;
        [SerializeField] private Button _previousPageButton;
        [SerializeField] private Button _nextPageButton;
        [SerializeField] private Text _previousPageButtonLabel;
        [SerializeField] private Text _nextPageButtonLabel;
        [SerializeField] private Text _pageText;
        [SerializeField] private Vector2 _gridAnchorMin = new Vector2(0.08f, 0.13f);
        [SerializeField] private Vector2 _gridAnchorMax = new Vector2(0.68f, 0.82f);
        [SerializeField] private Vector2 _gridCellSize = new Vector2(146f, 218f);
        [SerializeField] private Vector2 _gridCellSpacing = new Vector2(16f, 20f);
        [SerializeField] private Color _gridSlotColor = new Color(0.08f, 0.105f, 0.15f, 0.96f);
        [SerializeField] private Color _gridSlotEmptyArtworkColor = new Color(0.13f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color _ownedCountTextColor = new Color(1f, 0.94f, 0.7f, 1f);
        [SerializeField] private Color _upgradeReadyGlowColor = new Color(0.24f, 1f, 0.36f, 0.95f);
        [SerializeField] private Vector2 _upgradeReadyGlowDistance = new Vector2(5f, -5f);
        [SerializeField] private Vector2 _arrowButtonSize = new Vector2(64f, 112f);
        [SerializeField] private Color _arrowButtonColor = new Color(0.16f, 0.2f, 0.32f, 0.88f);
        [Header("Detail Panel")]
        [SerializeField] private RectTransform _detailPanelRoot;
        [SerializeField] private Image _detailArtworkImage;
        [SerializeField] private Text _detailTitleText;
        [SerializeField] private Text _detailOwnershipText;
        [SerializeField] private Text _detailMetaText;
        [SerializeField] private Text _detailAttackValueText;
        [SerializeField] private Text _detailHpValueText;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Text _upgradeButtonLabel;
        [SerializeField] private Vector2 _detailPanelAnchorMin = new Vector2(0.71f, 0.13f);
        [SerializeField] private Vector2 _detailPanelAnchorMax = new Vector2(0.96f, 0.82f);
        [SerializeField] private Color _detailPanelColor = new Color(0.045f, 0.06f, 0.095f, 0.96f);
        [SerializeField] private Color _detailAccentColor = new Color(1f, 0.86f, 0.48f, 1f);
        [SerializeField] private Color _detailStatTextColor = new Color(1f, 0.93f, 0.58f, 1f);
        [SerializeField] private Color _detailBonusStatColor = new Color(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private int _detailStatFontSize = 34;
        [SerializeField] private Vector2 _detailAttackStatNormalizedPosition = new Vector2(0.07f, 0.09f);
        [SerializeField] private Vector2 _detailHpStatNormalizedPosition = new Vector2(0.92f, 0.09f);
        [SerializeField] private Color _upgradeButtonColor = new Color(0.18f, 0.36f, 0.2f, 0.96f);
        [SerializeField] private Color _upgradeButtonUnavailableColor = new Color(0.23f, 0.24f, 0.28f, 0.82f);
        [SerializeField] private string _accountServerUrl = "http://127.0.0.1:7333";
        [SerializeField] private string _startSceneName = "GameStart_VSlice";
        [SerializeField] private string _emptyCollectionText = "보유 카드가 없습니다.\n런 보상을 받으면 이곳에 카드가 표시됩니다.";

        private CancellationTokenSource _refreshCancellation;
        private CancellationTokenSource _upgradeCancellation;
        private bool _isRefreshing;
        private bool _isUpgradingCard;
        private readonly List<CardGridEntry> _cardGridEntries = new List<CardGridEntry>();
        private readonly Dictionary<string, OwnedCardDto> _ownedCardLookup = new Dictionary<string, OwnedCardDto>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CardGridSlot> _gridSlots = new List<CardGridSlot>(CardsPerPage);
        private int _currentPageIndex;
        private string _selectedCardId = string.Empty;
        private bool _detailStatLayoutDirty = true;
        private Vector2 _lastDetailArtworkRectSize = new Vector2(float.NaN, float.NaN);
        private Sprite _lastDetailArtworkSprite;
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };

        private static Font s_runtimeKoreanFont;

        private string AccountServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);

        private void Awake()
        {
            AutoAssignSceneReferences();
            EnsureCardGridUi();
            ApplyStaticVisuals();
            SetStatus("보유 카드 정보를 준비 중입니다...");
        }

        private void OnEnable()
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                ReturnToStartSceneFromUi();
                return;
            }

            AutoAssignSceneReferences();
            EnsureCardGridUi();
            ApplyStaticVisuals();
            _detailStatLayoutDirty = true;
            RefreshOwnedCardsFromUi();
        }

        private void LateUpdate()
        {
            RefreshDetailStatOverlayLayoutIfNeeded();
        }

        private void OnRectTransformDimensionsChange()
        {
            _detailStatLayoutDirty = true;
        }

        private void OnDisable()
        {
            CancelRefresh();
            CancelUpgrade();
        }

        private void OnDestroy()
        {
            CancelRefresh();
            CancelUpgrade();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEngine.Application.isPlaying)
            {
                return;
            }

            EditorApplication.delayCall -= MaterializeOwnedCardsUiForEditor;
            EditorApplication.delayCall += MaterializeOwnedCardsUiForEditor;
        }

        [ContextMenu("Materialize Owned Cards UI")]
        public void MaterializeOwnedCardsUiForEditor()
        {
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            AutoAssignSceneReferences();
            EnsureCardGridUi();
            ApplyStaticVisuals();
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        public async void RefreshOwnedCardsFromUi()
        {
            if (_isRefreshing)
            {
                return;
            }

            CancelRefresh();
            _refreshCancellation = new CancellationTokenSource();
            _isRefreshing = true;
            SetStatus("서버에서 보유 카드 목록을 불러오는 중...");
            SetBackButtonInteractable(false);

            try
            {
                var client = new GuestAuthClient(AccountServerUrl);
                if (!AccountSessionState.IsAuthenticated)
                {
                    SetStatus("로그인이 필요합니다. 시작 화면으로 돌아갑니다.");
                    ReturnToStartSceneFromUi();
                    return;
                }

                var meResponse = await client.GetMeAsync(
                    AccountSessionState.SessionToken,
                    _refreshCancellation.Token);
                AccountSessionState.ApplyMeResponse(meResponse);
                PresentOwnedCards(meResponse);
                SetStatus("보유 카드 목록을 불러왔습니다.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 owned cards refresh failed: {ex.Message}");
                SetAccountTextFromSession();
                SetOwnedCardsText($"보유 카드 목록을 불러오지 못했습니다.\n\n{ex.Message}");
                SetStatus("보유 카드 목록 불러오기 실패");
            }
            finally
            {
                _isRefreshing = false;
                CancelRefresh();
                SetBackButtonInteractable(true);
                RefreshUpgradeButtonState();
            }
        }

        public void ReturnToStartSceneFromUi()
        {
            var sceneName = string.IsNullOrWhiteSpace(_startSceneName)
                ? "GameStart_VSlice"
                : _startSceneName;
            SceneManager.LoadScene(sceneName);
        }

        public void ShowPreviousPageFromUi()
        {
            if (_currentPageIndex <= 0)
            {
                return;
            }

            _currentPageIndex--;
            RenderCurrentPage();
        }

        public void ShowNextPageFromUi()
        {
            var pageCount = CalculatePageCount();
            if (_currentPageIndex >= pageCount - 1)
            {
                return;
            }

            _currentPageIndex++;
            RenderCurrentPage();
        }

        public void SelectCardFromGrid(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            _selectedCardId = cardId;
            RenderCurrentPage();
            RenderSelectedCardDetail();
        }

        public async void UpgradeSelectedCardFromUi()
        {
            if (_isUpgradingCard)
            {
                return;
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                SetStatus("로그인이 필요합니다. 시작 화면으로 돌아갑니다.");
                ReturnToStartSceneFromUi();
                return;
            }

            if (!TryGetSelectedUpgradeCost(out _, out var blockedReason))
            {
                SetStatus(blockedReason);
                RefreshUpgradeButtonState();
                return;
            }

            CancelUpgrade();
            _upgradeCancellation = new CancellationTokenSource();
            _isUpgradingCard = true;
            SetStatus("카드를 강화하는 중...");
            SetBackButtonInteractable(false);
            RefreshUpgradeButtonState();

            try
            {
                var client = new ServerCardCollectionClient(AccountServerUrl);
                var response = await client.UpgradeCardAsync(
                    AccountSessionState.SessionToken,
                    _selectedCardId,
                    _upgradeCancellation.Token);
                AccountSessionState.ApplyUpgradeCardResponse(response);
                ApplyUpgradeResponseToOwnedCards(response);

                var upgradedCard = response.ResolvedUpgradedCard;
                var upgradedCardId = upgradedCard?.ResolvedCardId ?? _selectedCardId;
                var upgradedLevel = upgradedCard?.ResolvedUpgradeLevel ?? 0;
                SetStatus($"{ResolveDisplayName(upgradedCardId)} Lv.{upgradedLevel} 강화 완료");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 card upgrade failed: {ex.Message}");
                SetStatus($"강화 실패: {ex.Message}");
            }
            finally
            {
                _isUpgradingCard = false;
                CancelUpgrade();
                SetBackButtonInteractable(true);
                RefreshUpgradeButtonState();
            }
        }

        private void PresentOwnedCards(MeResponse meResponse)
        {
            SetAccountTextFromSession();

            var collection = meResponse?.ResolvedCollectionSummary;
            var ownedCards = collection?.ResolvedOwnedCards ?? Array.Empty<OwnedCardDto>();
            RebuildOwnedCardLookup(ownedCards);
            RebuildCardGridEntries();
            if (!HasCardEntry(_selectedCardId))
            {
                _selectedCardId = _cardGridEntries.Count > 0 ? _cardGridEntries[0].CardId : string.Empty;
            }

            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, CalculatePageCount() - 1));
            RenderCurrentPage();
            RenderSelectedCardDetail();
        }

        private void ApplyUpgradeResponseToOwnedCards(UpgradeCardResponse response)
        {
            SetAccountTextFromSession();

            var collection = response?.ResolvedCollectionSummary;
            if (collection != null)
            {
                RebuildOwnedCardLookup(collection.ResolvedOwnedCards);
            }
            else if (response?.ResolvedUpgradedCard != null)
            {
                var upgradedCard = response.ResolvedUpgradedCard;
                var upgradedCardId = upgradedCard.ResolvedCardId;
                if (!string.IsNullOrWhiteSpace(upgradedCardId))
                {
                    _ownedCardLookup[upgradedCardId] = upgradedCard;
                }
            }

            RebuildCardGridEntries();
            if (!HasCardEntry(_selectedCardId))
            {
                _selectedCardId = _cardGridEntries.Count > 0 ? _cardGridEntries[0].CardId : string.Empty;
            }

            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, CalculatePageCount() - 1));
            RenderCurrentPage();
            RenderSelectedCardDetail();
        }

        private void RebuildOwnedCardLookup(IReadOnlyList<OwnedCardDto> ownedCards)
        {
            _ownedCardLookup.Clear();

            if (ownedCards == null)
            {
                return;
            }

            foreach (var ownedCard in ownedCards)
            {
                if (ownedCard == null ||
                    string.IsNullOrWhiteSpace(ownedCard.ResolvedCardId) ||
                    (ownedCard.ResolvedCopyCount <= 0 && ownedCard.ResolvedUpgradeLevel <= 0))
                {
                    continue;
                }

                _ownedCardLookup[ownedCard.ResolvedCardId] = ownedCard;
            }
        }

        private void RebuildCardGridEntries()
        {
            _cardGridEntries.Clear();
            var seenCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_cardCatalogAsset?.Cards != null)
            {
                foreach (var cardAsset in _cardCatalogAsset.Cards)
                {
                    if (cardAsset == null ||
                        IsMasterCard(cardAsset) ||
                        string.IsNullOrWhiteSpace(cardAsset.CardId) ||
                        !seenCardIds.Add(cardAsset.CardId))
                    {
                        continue;
                    }

                    _cardGridEntries.Add(new CardGridEntry(
                        cardAsset.CardId,
                        string.IsNullOrWhiteSpace(cardAsset.DisplayName) ? cardAsset.CardId : cardAsset.DisplayName,
                        ResolveCardArtwork(cardAsset.CardId, cardAsset)));
                }
            }

            if (_cardGridEntries.Count == 0)
            {
                foreach (var pair in _ownedCardLookup)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || IsMasterCardId(pair.Key))
                    {
                        continue;
                    }

                    _cardGridEntries.Add(new CardGridEntry(
                        pair.Key,
                        ResolveDisplayName(pair.Key),
                        ResolveCardArtwork(pair.Key, null)));
                }

                _cardGridEntries.Sort((left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RenderCurrentPage()
        {
            EnsureCardGridUi();

            if (_cardGridEntries.Count == 0)
            {
                SetOwnedCardsText(_emptyCollectionText);
                for (var i = 0; i < _gridSlots.Count; i++)
                {
                    _gridSlots[i].SetEmpty(_gridSlotEmptyArtworkColor);
                }

                RefreshPageControls();
                return;
            }

            SetOwnedCardsText(string.Empty);
            var startIndex = _currentPageIndex * CardsPerPage;
            for (var slotIndex = 0; slotIndex < _gridSlots.Count; slotIndex++)
            {
                var entryIndex = startIndex + slotIndex;
                if (entryIndex >= _cardGridEntries.Count)
                {
                    _gridSlots[slotIndex].SetEmpty(_gridSlotEmptyArtworkColor);
                    continue;
                }

                var entry = _cardGridEntries[entryIndex];
                _ownedCardLookup.TryGetValue(entry.CardId, out var ownedCard);
                var copyCount = ownedCard?.ResolvedCopyCount ?? 0;
                var upgradeLevel = ownedCard?.ResolvedUpgradeLevel ?? 0;
                var cardAsset = ResolveCardAsset(entry.CardId);
                var upgradeProgressText = BuildGridUpgradeProgressText(copyCount, upgradeLevel, cardAsset);
                var isUpgradeReady = IsGridCardUpgradeReady(copyCount, upgradeLevel, cardAsset);
                _gridSlots[slotIndex].SetCard(
                    entry,
                    copyCount,
                    upgradeLevel,
                    upgradeProgressText,
                    isUpgradeReady,
                    _gridSlotColor,
                    _gridSlotEmptyArtworkColor,
                    _ownedCountTextColor,
                    _upgradeReadyGlowColor,
                    _upgradeReadyGlowDistance,
                    string.Equals(entry.CardId, _selectedCardId, StringComparison.OrdinalIgnoreCase),
                    SelectCardFromGrid);
            }

            RefreshPageControls();
        }

        private static string BuildGridUpgradeProgressText(
            int copyCount,
            int upgradeLevel,
            CardDefinitionAsset cardAsset)
        {
            var safeCopyCount = Mathf.Max(0, copyCount);
            var safeUpgradeLevel = Mathf.Max(0, upgradeLevel);
            if (!IsCardUpgradeable(cardAsset))
            {
                return "강화불가";
            }

            return CardUpgradeRules.TryGetNextUpgradeCost(safeUpgradeLevel, out var cost)
                ? $"Lv.{safeUpgradeLevel}  {safeCopyCount}/{cost.RequiredCopyCount}"
                : $"Lv.{safeUpgradeLevel}  MAX";
        }

        private static bool IsGridCardUpgradeReady(
            int copyCount,
            int upgradeLevel,
            CardDefinitionAsset cardAsset)
        {
            if (!IsCardUpgradeable(cardAsset))
            {
                return false;
            }

            var safeCopyCount = Mathf.Max(0, copyCount);
            var safeUpgradeLevel = Mathf.Max(0, upgradeLevel);
            return CardUpgradeRules.TryGetNextUpgradeCost(safeUpgradeLevel, out var cost) &&
                   safeCopyCount >= cost.RequiredCopyCount &&
                   AccountSessionState.ResourceGold >= cost.RequiredResourceGold;
        }

        private bool HasCardEntry(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            for (var i = 0; i < _cardGridEntries.Count; i++)
            {
                if (string.Equals(_cardGridEntries[i].CardId, cardId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RenderSelectedCardDetail()
        {
            EnsureDetailPanelUi();

            if (string.IsNullOrWhiteSpace(_selectedCardId))
            {
                SetDetailText("카드를 선택하세요.", string.Empty, string.Empty);
                SetDetailStatOverlayVisible(false);
                if (_detailArtworkImage != null)
                {
                    _detailArtworkImage.sprite = null;
                    _detailArtworkImage.enabled = false;
                }

                RefreshUpgradeButtonState();
                return;
            }

            var cardAsset = ResolveCardAsset(_selectedCardId);
            var displayName = cardAsset != null && !string.IsNullOrWhiteSpace(cardAsset.DisplayName)
                ? cardAsset.DisplayName
                : _selectedCardId;
            _ownedCardLookup.TryGetValue(_selectedCardId, out var ownedCard);
            var copyCount = ownedCard?.ResolvedCopyCount ?? 0;
            var upgradeLevel = ownedCard?.ResolvedUpgradeLevel ?? 0;
            var artwork = ResolveCardArtwork(_selectedCardId, cardAsset);

            if (_detailArtworkImage != null)
            {
                _detailArtworkImage.sprite = artwork;
                _detailArtworkImage.enabled = artwork != null;
                _detailArtworkImage.color = artwork != null ? Color.white : _gridSlotEmptyArtworkColor;
                _detailArtworkImage.preserveAspect = true;
                _detailStatLayoutDirty = true;
            }

            var title = string.Equals(displayName, _selectedCardId, StringComparison.Ordinal)
                ? displayName
                : $"{displayName}({_selectedCardId})";
            var ownership = BuildUpgradePreviewText(copyCount, upgradeLevel, cardAsset);
            SetDetailText(
                title,
                ownership,
                BuildDetailMetaText(cardAsset, upgradeLevel));
            RefreshDetailStatOverlay(cardAsset, upgradeLevel);
            RefreshUpgradeButtonState();
        }

        private static string BuildUpgradePreviewText(
            int copyCount,
            int upgradeLevel,
            CardDefinitionAsset cardAsset)
        {
            var safeCopyCount = Mathf.Max(0, copyCount);
            var safeUpgradeLevel = Mathf.Max(0, upgradeLevel);
            if (!IsCardUpgradeable(cardAsset))
            {
                return $"보유: x{safeCopyCount}    Lv.{safeUpgradeLevel}\n강화: 불가능";
            }

            if (!CardUpgradeRules.TryGetNextUpgradeCost(safeUpgradeLevel, out var cost))
            {
                return $"보유: x{safeCopyCount}    Lv.{safeUpgradeLevel}\n강화: 최대 레벨 ({CardUpgradeRules.MaxLevel})";
            }

            var serverGold = AccountSessionState.ResourceGold;
            var hasEnoughCopies = safeCopyCount >= cost.RequiredCopyCount;
            var hasEnoughGold = serverGold >= cost.RequiredResourceGold;
            var status = hasEnoughCopies && hasEnoughGold
                ? "강화 가능"
                : "재료 부족";
            var nextStatChangeText = BuildNextUpgradeStatChangeText(cardAsset, safeUpgradeLevel, cost.LevelTo);
            return
                $"보유: x{safeCopyCount}    Lv.{safeUpgradeLevel}\n" +
                $"다음 강화: Lv.{cost.LevelTo}{nextStatChangeText}\n" +
                $"필요 카드: {safeCopyCount}/{cost.RequiredCopyCount}    필요 골드: {serverGold}/{cost.RequiredResourceGold}\n" +
                $"상태: {status}";
        }

        private static string BuildNextUpgradeStatChangeText(
            CardDefinitionAsset cardAsset,
            int currentLevel,
            int nextLevel)
        {
            if (!HasBattleStats(cardAsset))
            {
                return string.Empty;
            }

            var currentBonus = CardLevelStatRules.CalculateBonus(cardAsset.CardId, currentLevel);
            var nextBonus = CardLevelStatRules.CalculateBonus(cardAsset.CardId, nextLevel);
            var attackDelta = nextBonus.AttackBonus - currentBonus.AttackBonus;
            var hpDelta = nextBonus.HpBonus - currentBonus.HpBonus;

            if (attackDelta > 0)
            {
                return $" (공격력+{attackDelta})";
            }

            return hpDelta > 0
                ? $" (Hp+{hpDelta})"
                : string.Empty;
        }

        private static bool HasBattleStats(CardDefinitionAsset cardAsset)
        {
            if (cardAsset == null)
            {
                return false;
            }

            try
            {
                var definition = cardAsset.ToDefinition();
                return definition is UnitCardDefinition || definition is BuildingCardDefinition;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsCardUpgradeable(CardDefinitionAsset cardAsset)
        {
            if (cardAsset == null)
            {
                return false;
            }

            try
            {
                return CardUpgradeRules.IsCardUpgradeable(cardAsset.ToDefinition());
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryGetSelectedUpgradeCost(out CardUpgradeCost cost, out string blockedReason)
        {
            cost = default;
            blockedReason = string.Empty;

            if (string.IsNullOrWhiteSpace(_selectedCardId))
            {
                blockedReason = "강화할 카드를 선택하세요.";
                return false;
            }

            if (!_ownedCardLookup.TryGetValue(_selectedCardId, out var ownedCard) || ownedCard == null)
            {
                blockedReason = "보유하지 않은 카드는 강화할 수 없습니다.";
                return false;
            }

            if (!IsCardUpgradeable(ResolveCardAsset(_selectedCardId)))
            {
                blockedReason = "강화 불가";
                return false;
            }

            var copyCount = Mathf.Max(0, ownedCard.ResolvedCopyCount);
            var upgradeLevel = Mathf.Max(0, ownedCard.ResolvedUpgradeLevel);
            if (!CardUpgradeRules.TryGetNextUpgradeCost(upgradeLevel, out cost))
            {
                blockedReason = $"최대 레벨입니다. (Lv.{CardUpgradeRules.MaxLevel})";
                return false;
            }

            if (copyCount < cost.RequiredCopyCount)
            {
                blockedReason = $"카드 부족 {copyCount}/{cost.RequiredCopyCount}";
                return false;
            }

            if (AccountSessionState.ResourceGold < cost.RequiredResourceGold)
            {
                blockedReason = $"골드 부족 {AccountSessionState.ResourceGold}/{cost.RequiredResourceGold}";
                return false;
            }

            blockedReason = $"Lv.{cost.LevelTo} 강화 가능";
            return true;
        }

        private void RefreshUpgradeButtonState()
        {
            if (_upgradeButton == null)
            {
                return;
            }

            var canUpgrade = TryGetSelectedUpgradeCost(out var cost, out var stateText);
            var isInteractable = canUpgrade && !_isRefreshing && !_isUpgradingCard;
            _upgradeButton.interactable = isInteractable;

            if (_upgradeButtonLabel != null)
            {
                _upgradeButtonLabel.text = _isUpgradingCard
                    ? "강화 중..."
                    : canUpgrade
                        ? $"강화하기 Lv.{cost.LevelTo}"
                        : stateText;
            }

            var image = _upgradeButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = canUpgrade ? _upgradeButtonColor : _upgradeButtonUnavailableColor;
            }
        }

        private CardDefinitionAsset ResolveCardAsset(string cardId)
        {
            if (!string.IsNullOrWhiteSpace(cardId) &&
                _cardCatalogAsset != null &&
                _cardCatalogAsset.TryGetCardAsset(cardId, out var cardAsset))
            {
                return cardAsset;
            }

            return null;
        }

        private void SetDetailText(string title, string ownership, string meta)
        {
            if (_detailTitleText != null)
            {
                _detailTitleText.text = title ?? string.Empty;
            }

            if (_detailOwnershipText != null)
            {
                _detailOwnershipText.text = ownership ?? string.Empty;
            }

            if (_detailMetaText != null)
            {
                if (_detailMetaText.text != (meta ?? string.Empty) &&
                    _detailMetaText.GetComponentInParent<ScrollRect>() != null)
                {
                    _detailMetaText.rectTransform.anchoredPosition = Vector2.zero;
                }
                _detailMetaText.text = meta ?? string.Empty;
            }
        }

        private void RefreshDetailStatOverlay(CardDefinitionAsset cardAsset, int upgradeLevel)
        {
            EnsureDetailPanelUi();

            if (_detailArtworkImage == null ||
                !_detailArtworkImage.enabled ||
                _detailArtworkImage.sprite == null)
            {
                SetDetailStatOverlayVisible(false);
                return;
            }

            if (!TryBuildDetailStatDisplay(cardAsset, upgradeLevel, out var statDisplay))
            {
                SetDetailStatOverlayVisible(false);
                return;
            }

            if (_detailAttackValueText != null)
            {
                _detailAttackValueText.text = statDisplay.Attack.ToString();
            }

            if (_detailHpValueText != null)
            {
                _detailHpValueText.text = statDisplay.Hp.ToString();
            }

            _detailStatLayoutDirty = true;
            RefreshDetailStatOverlayLayoutIfNeeded();
            SetDetailStatOverlayVisible(true);
        }

        private void RefreshDetailStatOverlayLayoutIfNeeded()
        {
            if (_detailArtworkImage == null || _detailArtworkImage.sprite == null)
            {
                return;
            }

            var artworkRectTransform = _detailArtworkImage.rectTransform;
            var artworkRectSize = artworkRectTransform.rect.size;
            var sizeChanged = (_lastDetailArtworkRectSize - artworkRectSize).sqrMagnitude > 0.01f;
            var spriteChanged = _lastDetailArtworkSprite != _detailArtworkImage.sprite;
            if (!_detailStatLayoutDirty && !sizeChanged && !spriteChanged)
            {
                return;
            }

            var renderedSpriteRect = CalculateRenderedSpriteRect(
                artworkRectTransform.rect,
                _detailArtworkImage.sprite,
                _detailArtworkImage.preserveAspect);
            PositionDetailStatText(
                _detailAttackValueText,
                artworkRectTransform.rect,
                renderedSpriteRect,
                _detailAttackStatNormalizedPosition);
            PositionDetailStatText(
                _detailHpValueText,
                artworkRectTransform.rect,
                renderedSpriteRect,
                _detailHpStatNormalizedPosition);

            _lastDetailArtworkRectSize = artworkRectSize;
            _lastDetailArtworkSprite = _detailArtworkImage.sprite;
            _detailStatLayoutDirty = false;
        }

        private static Rect CalculateRenderedSpriteRect(
            Rect containerRect,
            Sprite sprite,
            bool preserveAspect)
        {
            if (!preserveAspect ||
                sprite == null ||
                containerRect.width <= 0f ||
                containerRect.height <= 0f ||
                sprite.rect.width <= 0f ||
                sprite.rect.height <= 0f)
            {
                return containerRect;
            }

            var spriteAspect = sprite.rect.width / sprite.rect.height;
            var containerAspect = containerRect.width / containerRect.height;
            if (spriteAspect > containerAspect)
            {
                var renderedHeight = containerRect.width / spriteAspect;
                return new Rect(
                    containerRect.xMin,
                    containerRect.center.y - (renderedHeight * 0.5f),
                    containerRect.width,
                    renderedHeight);
            }

            var renderedWidth = containerRect.height * spriteAspect;
            return new Rect(
                containerRect.center.x - (renderedWidth * 0.5f),
                containerRect.yMin,
                renderedWidth,
                containerRect.height);
        }

        private static void PositionDetailStatText(
            Text text,
            Rect containerRect,
            Rect renderedSpriteRect,
            Vector2 normalizedSpritePosition)
        {
            if (text == null || containerRect.width <= 0f || containerRect.height <= 0f)
            {
                return;
            }

            var clampedPosition = new Vector2(
                Mathf.Clamp01(normalizedSpritePosition.x),
                Mathf.Clamp01(normalizedSpritePosition.y));
            var localPoint = new Vector2(
                Mathf.Lerp(renderedSpriteRect.xMin, renderedSpriteRect.xMax, clampedPosition.x),
                Mathf.Lerp(renderedSpriteRect.yMin, renderedSpriteRect.yMax, clampedPosition.y));
            var anchor = new Vector2(
                Mathf.InverseLerp(containerRect.xMin, containerRect.xMax, localPoint.x),
                Mathf.InverseLerp(containerRect.yMin, containerRect.yMax, localPoint.y));

            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private void SetDetailStatOverlayVisible(bool visible)
        {
            if (_detailAttackValueText != null)
            {
                _detailAttackValueText.enabled = visible;
            }

            if (_detailHpValueText != null)
            {
                _detailHpValueText.enabled = visible;
            }
        }

        private static bool TryBuildDetailStatDisplay(
            CardDefinitionAsset cardAsset,
            int upgradeLevel,
            out DetailStatDisplay statDisplay)
        {
            statDisplay = default;
            if (cardAsset == null)
            {
                return false;
            }

            CardDefinition definition;
            try
            {
                definition = cardAsset.ToDefinition();
            }
            catch (Exception)
            {
                return false;
            }

            switch (definition)
            {
                case UnitCardDefinition unit:
                    statDisplay = new DetailStatDisplay(
                        CardLevelStatRules.ApplyAttackBonus(unit.CardId, unit.Attack, upgradeLevel),
                        CardLevelStatRules.ApplyHpBonus(unit.CardId, unit.Health, upgradeLevel));
                    return true;

                case BuildingCardDefinition building:
                    statDisplay = new DetailStatDisplay(
                        CardLevelStatRules.ApplyAttackBonus(building.CardId, building.Attack, upgradeLevel),
                        CardLevelStatRules.ApplyHpBonus(building.CardId, building.Health, upgradeLevel));
                    return true;

                default:
                    return false;
            }
        }

        private string BuildDetailMetaText(CardDefinitionAsset cardAsset, int upgradeLevel)
        {
            if (cardAsset == null)
            {
                return "카드 정보를 찾을 수 없습니다.";
            }

            CardDefinition definition = null;
            try
            {
                definition = cardAsset.ToDefinition();
            }
            catch (Exception ex)
            {
                return $"카드 정의 변환 실패: {ex.Message}";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"타입: {FormatCardType(definition.CardType)}");
            builder.AppendLine($"희귀도: {FormatRarity(cardAsset.Rarity)}");
            builder.AppendLine($"비용: {FormatCost(cardAsset.Cost)}");
            builder.AppendLine($"소속: {FormatAffiliation(cardAsset.Affiliation)}");
            builder.AppendLine($"차지 타일: {FormatChargeTileFootprint(cardAsset.ChargeTileFootprint)}");

            if (definition is UnitCardDefinition unit)
            {
                var currentAttack = CardLevelStatRules.ApplyAttackBonus(unit.CardId, unit.Attack, upgradeLevel);
                var currentHp = CardLevelStatRules.ApplyHpBonus(unit.CardId, unit.Health, upgradeLevel);
                builder.AppendLine($"근거리/원거리: {FormatAttackType(unit.AttackType)}");
                builder.AppendLine($"데미지 속성: {FormatDamageType(unit.DamageType)}");
                builder.AppendLine($"물리/마법 방어력: {unit.PhysicalDefense} / {unit.MagicDefense}");
                builder.AppendLine(
                    $"ATK/HP: {FormatLeveledStat(unit.Attack, currentAttack)} / {FormatLeveledStat(unit.Health, currentHp)}");
                builder.AppendLine($"이동 가능 여부: {(unit.CanMove ? "가능" : "불가능")}");
            }
            else if (definition is BuildingCardDefinition building)
            {
                var currentAttack = CardLevelStatRules.ApplyAttackBonus(building.CardId, building.Attack, upgradeLevel);
                var currentHp = CardLevelStatRules.ApplyHpBonus(building.CardId, building.Health, upgradeLevel);
                builder.AppendLine($"데미지 속성: {FormatDamageType(building.DamageType)}");
                builder.AppendLine($"물리/마법 방어력: {building.PhysicalDefense} / {building.MagicDefense}");
                builder.AppendLine(
                    $"ATK/HP: {FormatLeveledStat(building.Attack, currentAttack)} / {FormatLeveledStat(building.Health, currentHp)}");
                builder.AppendLine($"공격 가능 여부: {(building.CanAttack ? "가능" : "불가능")}");
            }
            else if (definition is DamageSpellCardDefinition damageSpell)
            {
                builder.AppendLine($"데미지 속성: {FormatDamageType(damageSpell.DamageType)}");
                builder.AppendLine($"피해량: {damageSpell.Damage}");
            }

            builder.AppendLine();
            var hasRulesText = false;
            if (!string.IsNullOrWhiteSpace(cardAsset.EffectText))
            {
                builder.AppendLine("효과");
                builder.AppendLine(cardAsset.EffectText.Trim());
                builder.AppendLine();
                hasRulesText = true;
            }

            if (!string.IsNullOrWhiteSpace(cardAsset.SpecialEffectText))
            {
                builder.AppendLine("특수효과");
                builder.AppendLine(cardAsset.SpecialEffectText.Trim());
                hasRulesText = true;
            }

            if (!hasRulesText)
            {
                builder.AppendLine("효과 없음");
            }

            return builder.ToString().TrimEnd();
        }

        private string FormatLeveledStat(int baseValue, int currentValue)
        {
            var bonus = currentValue - baseValue;
            if (bonus <= 0)
            {
                return currentValue.ToString();
            }

            var colorHex = ColorUtility.ToHtmlStringRGB(_detailBonusStatColor);
            return $"{currentValue}({baseValue}<color=#{colorHex}>+{bonus}</color>)";
        }

        private static string FormatCardType(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.Unit:
                    return "유닛";
                case CardType.Building:
                    return "건물";
                case CardType.Spell:
                    return "마법";
                default:
                    return cardType.ToString();
            }
        }

        private static string FormatRarity(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return "커먼";
                case CardRarity.Uncommon:
                    return "언커먼";
                case CardRarity.Rare:
                    return "레어";
                case CardRarity.Unique:
                    return "유니크";
                case CardRarity.Legendary:
                    return "레전더리";
                default:
                    return rarity.ToString();
            }
        }

        private static string FormatAffiliation(CardAffiliation affiliation)
        {
            switch (affiliation)
            {
                case CardAffiliation.Murim:
                    return "무림";
                case CardAffiliation.Fantasy:
                    return "판타지";
                case CardAffiliation.ScienceCivilization:
                    return "과학문명";
                case CardAffiliation.Neutral:
                    return "중립";
                default:
                    return affiliation.ToString();
            }
        }

        private static string FormatChargeTileFootprint(ChargeTileFootprint footprint)
        {
            switch (footprint)
            {
                case ChargeTileFootprint.None:
                    return "없음";
                case ChargeTileFootprint.OneByOne:
                    return "1X1";
                case ChargeTileFootprint.TwoByOne:
                    return "2X1";
                case ChargeTileFootprint.TwoByTwo:
                    return "2X2";
                default:
                    return footprint.ToString();
            }
        }

        private static string FormatAttackType(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.Melee:
                    return "근거리";
                case AttackType.Ranged:
                    return "원거리";
                default:
                    return attackType.ToString();
            }
        }

        private static string FormatDamageType(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.None:
                    return "없음";
                case DamageType.Physical:
                    return "물리";
                case DamageType.Magic:
                    return "마법";
                case DamageType.Fixed:
                    return "고정";
                default:
                    return damageType.ToString();
            }
        }

        private static string FormatCost(ResourceSetData cost)
        {
            var parts = new List<string>();
            if (cost.Mana > 0)
            {
                parts.Add($"마나 {cost.Mana}");
            }

            if (cost.Qi > 0)
            {
                parts.Add($"기 {cost.Qi}");
            }

            if (cost.Power > 0)
            {
                parts.Add($"전력 {cost.Power}");
            }

            if (cost.Gold > 0)
            {
                parts.Add($"골드 {cost.Gold}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "없음";
        }

        private int CalculatePageCount()
        {
            return Mathf.Max(1, Mathf.CeilToInt(_cardGridEntries.Count / (float)CardsPerPage));
        }

        private void RefreshPageControls()
        {
            var pageCount = CalculatePageCount();
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, pageCount - 1);

            if (_previousPageButton != null)
            {
                _previousPageButton.interactable = _currentPageIndex > 0;
            }

            if (_nextPageButton != null)
            {
                _nextPageButton.interactable = _currentPageIndex < pageCount - 1;
            }

            if (_pageText != null)
            {
                _pageText.text = $"{_currentPageIndex + 1} / {pageCount}";
            }
        }

        private Sprite ResolveCardArtwork(string cardId, CardDefinitionAsset cardAsset)
        {
            if (CardArtworkLibrary.TryGetArtwork(cardId, out var artwork) && artwork != null)
            {
                return artwork;
            }

            return cardAsset != null ? cardAsset.BoardSprite : null;
        }

        private string ResolveDisplayName(string cardId)
        {
            if (!string.IsNullOrWhiteSpace(cardId) &&
                _cardCatalogAsset != null &&
                _cardCatalogAsset.TryGetCardAsset(cardId, out var cardAsset) &&
                !string.IsNullOrWhiteSpace(cardAsset.DisplayName))
            {
                return cardAsset.DisplayName;
            }

            return string.IsNullOrWhiteSpace(cardId) ? "Unknown Card" : cardId;
        }

        private static bool IsMasterCard(CardDefinitionAsset cardAsset)
        {
            if (cardAsset == null)
            {
                return false;
            }

            return IsMasterCardId(cardAsset.CardId) ||
                   string.Equals(cardAsset.DisplayName, "Master", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(cardAsset.DisplayName, "마스터", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMasterCardId(string cardId)
        {
            return string.Equals(cardId, "Master", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(cardId, "MasterCard", StringComparison.OrdinalIgnoreCase);
        }

        private void SetAccountTextFromSession()
        {
            if (_accountText == null)
            {
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(AccountSessionState.DisplayName)
                ? "서버 계정 정보 없음"
                : AccountSessionState.DisplayName;
            _accountText.text =
                $"Server Account: {displayName}\nTickets: {AccountSessionState.Tickets}   Gold: {AccountSessionState.ResourceGold}";
        }

        private void SetOwnedCardsText(string text)
        {
            if (_ownedCardsText != null)
            {
                _ownedCardsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
                _ownedCardsText.text = text ?? string.Empty;
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status ?? string.Empty;
            }
        }

        private void SetBackButtonInteractable(bool isInteractable)
        {
            if (_backButton != null)
            {
                _backButton.interactable = isInteractable;
            }
        }

        private void CancelRefresh()
        {
            if (_refreshCancellation == null)
            {
                return;
            }

            _refreshCancellation.Cancel();
            _refreshCancellation.Dispose();
            _refreshCancellation = null;
        }

        private void CancelUpgrade()
        {
            if (_upgradeCancellation == null)
            {
                return;
            }

            _upgradeCancellation.Cancel();
            _upgradeCancellation.Dispose();
            _upgradeCancellation = null;
        }

        private void EnsureCardGridUi()
        {
            var parent = ResolveOwnedCardsPanel();
            if (parent == null)
            {
                return;
            }

            if (_ownedCardsText != null)
            {
                _ownedCardsText.gameObject.SetActive(false);
            }

            var applyDefaultGridLayout = false;
            if (_cardGridRoot == null)
            {
                if (parent.Find("CardGridRoot") is RectTransform existingGridRoot)
                {
                    _cardGridRoot = existingGridRoot;
                }
                else
                {
                    var gridObject = new GameObject("CardGridRoot", typeof(RectTransform), typeof(GridLayoutGroup));
                    RegisterEditorCreatedObject(gridObject);
                    _cardGridRoot = gridObject.GetComponent<RectTransform>();
                    _cardGridRoot.SetParent(parent, false);
                    applyDefaultGridLayout = true;
                }
            }

            ConfigureCardGridRoot(applyDefaultGridLayout);
            EnsureCardGridSlots();
            EnsurePageButtonUi(parent);
            EnsureDetailPanelUi();
        }

        private void EnsureDetailPanelUi()
        {
            var parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            var applyDefaultPanelLayout = false;
            if (_detailPanelRoot == null)
            {
                if (transform.Find("DetailPanel") is RectTransform existingPanel)
                {
                    _detailPanelRoot = existingPanel;
                }
                else
                {
                    var panelObject = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
                    RegisterEditorCreatedObject(panelObject);
                    _detailPanelRoot = panelObject.GetComponent<RectTransform>();
                    _detailPanelRoot.SetParent(parent, false);
                    applyDefaultPanelLayout = true;
                }
            }

            if (applyDefaultPanelLayout)
            {
                _detailPanelRoot.anchorMin = _detailPanelAnchorMin;
                _detailPanelRoot.anchorMax = _detailPanelAnchorMax;
                _detailPanelRoot.pivot = new Vector2(0.5f, 0.5f);
                _detailPanelRoot.offsetMin = Vector2.zero;
                _detailPanelRoot.offsetMax = Vector2.zero;
            }

            _detailPanelRoot.SetAsLastSibling();

            var panelImage = _detailPanelRoot.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _detailPanelRoot.gameObject.AddComponent<Image>();
            }

            if (panelImage != null)
            {
                panelImage.color = _detailPanelColor;
                panelImage.raycastTarget = false;
            }

            _detailTitleText = FindOrCreateText(
                _detailPanelRoot,
                "DetailTitleText",
                ref _detailTitleText,
                new Vector2(0f, 0.86f),
                new Vector2(1f, 1f),
                new Vector2(18f, 8f),
                new Vector2(-18f, -10f),
                25,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                applyDefaultPanelLayout);
            if (_detailTitleText != null)
            {
                _detailTitleText.color = _detailAccentColor;
                _detailTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _detailTitleText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            _detailArtworkImage = FindOrCreateImage(
                _detailPanelRoot,
                "DetailArtworkImage",
                ref _detailArtworkImage,
                new Vector2(0.12f, 0.54f),
                new Vector2(0.88f, 0.86f),
                Vector2.zero,
                Vector2.zero,
                applyDefaultPanelLayout);
            if (_detailArtworkImage != null)
            {
                _detailArtworkImage.preserveAspect = true;
                _detailArtworkImage.raycastTarget = false;
                EnsureDetailStatOverlayUi(_detailArtworkImage.rectTransform);
            }

            _detailOwnershipText = FindOrCreateText(
                _detailPanelRoot,
                "DetailOwnershipText",
                ref _detailOwnershipText,
                new Vector2(0f, 0.11f),
                new Vector2(1f, 0.47f),
                new Vector2(22f, 14f),
                new Vector2(-22f, -4f),
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                applyDefaultPanelLayout);
            if (_detailOwnershipText != null)
            {
                _detailOwnershipText.color = _ownedCountTextColor;
            }

            _detailMetaText = FindOrCreateText(
                _detailPanelRoot,
                "DetailMetaText",
                ref _detailMetaText,
                new Vector2(0f, 0.47f),
                new Vector2(1f, 0.54f),
                new Vector2(18f, 0f),
                new Vector2(-18f, 0f),
                16,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                applyDefaultPanelLayout,
                preserveExistingStyle: true);
            if (_detailMetaText != null)
            {
                _detailMetaText.supportRichText = true;
            }

            _upgradeButton = FindOrCreateDetailButton(
                _detailPanelRoot,
                "UpgradeButton",
                ref _upgradeButton,
                ref _upgradeButtonLabel,
                applyDefaultPanelLayout);
            ConfigureUpgradeButton();
            RefreshUpgradeButtonState();
        }

        private void EnsureDetailStatOverlayUi(RectTransform artworkRoot)
        {
            if (artworkRoot == null)
            {
                return;
            }

            _detailAttackValueText = FindOrCreateDetailStatText(
                artworkRoot,
                "AttackValueText",
                ref _detailAttackValueText,
                isHp: false);
            _detailHpValueText = FindOrCreateDetailStatText(
                artworkRoot,
                "HpValueText",
                ref _detailHpValueText,
                isHp: true);
        }

        private Text FindOrCreateDetailStatText(
            RectTransform parent,
            string objectName,
            ref Text cachedText,
            bool isHp)
        {
            if (parent == null)
            {
                return null;
            }

            var shouldApplyDefaultLayout = false;
            var shouldApplyDefaultStyle = false;
            if (cachedText == null)
            {
                if (parent.Find(objectName) is RectTransform existingTransform)
                {
                    cachedText = existingTransform.GetComponent<Text>();
                    if (cachedText == null)
                    {
                        cachedText = existingTransform.gameObject.AddComponent<Text>();
                        shouldApplyDefaultStyle = true;
                    }
                }
                else
                {
                    var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(textObject);
                    var textRect = textObject.GetComponent<RectTransform>();
                    textRect.SetParent(parent, false);
                    cachedText = textObject.GetComponent<Text>();
                    shouldApplyDefaultLayout = true;
                    shouldApplyDefaultStyle = true;
                }
            }

            if (cachedText == null)
            {
                return null;
            }

            if (shouldApplyDefaultLayout)
            {
                ApplyDefaultDetailStatTextRect(cachedText.rectTransform, isHp);
            }

            if (shouldApplyDefaultStyle)
            {
                ConfigureDetailStatText(cachedText);
            }

            cachedText.enabled = false;
            cachedText.raycastTarget = false;
            return cachedText;
        }

        private void ApplyDefaultDetailStatTextRect(RectTransform rectTransform, bool isHp)
        {
            if (rectTransform == null)
            {
                return;
            }

            var anchor = isHp
                ? new Vector2(0.895f, 0.085f)
                : new Vector2(0.115f, 0.085f);
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(70f, 54f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private void ConfigureDetailStatText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.font = ResolveRuntimeFont();
            text.fontSize = _detailStatFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = _detailStatTextColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
        }

        private Button FindOrCreateDetailButton(
            RectTransform parent,
            string buttonName,
            ref Button cachedButton,
            ref Text cachedLabel,
            bool applyDefaultLayout)
        {
            var shouldApplyDefaultLayout = applyDefaultLayout;
            if (cachedButton == null)
            {
                if (parent.Find(buttonName) is RectTransform existingButtonTransform)
                {
                    cachedButton = existingButtonTransform.GetComponent<Button>();
                    if (cachedButton == null)
                    {
                        cachedButton = existingButtonTransform.gameObject.AddComponent<Button>();
                        shouldApplyDefaultLayout = true;
                    }

                    cachedLabel = cachedButton.GetComponentInChildren<Text>(true);
                    if (cachedLabel == null)
                    {
                        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                        RegisterEditorCreatedObject(labelObject);
                        var labelRect = labelObject.GetComponent<RectTransform>();
                        labelRect.SetParent(existingButtonTransform, false);
                        StretchFull(labelRect, 6f);
                        cachedLabel = labelObject.GetComponent<Text>();
                    }
                }
                else
                {
                    var buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
                    RegisterEditorCreatedObject(buttonObject);
                    var buttonRect = buttonObject.GetComponent<RectTransform>();
                    buttonRect.SetParent(parent, false);
                    cachedButton = buttonObject.GetComponent<Button>();
                    shouldApplyDefaultLayout = true;

                    var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(buttonRect, false);
                    StretchFull(labelRect, 6f);
                    cachedLabel = labelObject.GetComponent<Text>();
                }
            }

            if (cachedButton != null && cachedLabel == null)
            {
                cachedLabel = cachedButton.GetComponentInChildren<Text>(true);
                if (cachedLabel == null)
                {
                    var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(cachedButton.transform, false);
                    StretchFull(labelRect, 6f);
                    cachedLabel = labelObject.GetComponent<Text>();
                }
            }

            if (cachedButton != null && shouldApplyDefaultLayout)
            {
                var rectTransform = cachedButton.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.12f, 0.03f);
                rectTransform.anchorMax = new Vector2(0.88f, 0.105f);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            return cachedButton;
        }

        private void ConfigureUpgradeButton()
        {
            if (_upgradeButton == null)
            {
                return;
            }

            var image = _upgradeButton.GetComponent<Image>();
            if (image == null)
            {
                image = _upgradeButton.gameObject.AddComponent<Image>();
            }

            if (image != null)
            {
                image.raycastTarget = true;
                _upgradeButton.targetGraphic = image;
            }

            var colors = _upgradeButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            colors.pressedColor = new Color(0.74f, 0.92f, 0.74f, 0.96f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.55f);
            _upgradeButton.colors = colors;

            _upgradeButton.onClick.RemoveListener(UpgradeSelectedCardFromUi);
            _upgradeButton.onClick.AddListener(UpgradeSelectedCardFromUi);

            ConfigureText(_upgradeButtonLabel, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (_upgradeButtonLabel != null)
            {
                _upgradeButtonLabel.raycastTarget = false;
            }
        }

        private static Text FindOrCreateText(
            RectTransform parent,
            string objectName,
            ref Text cachedText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            bool applyDefaultLayout = false,
            bool preserveExistingStyle = false)
        {
            var shouldApplyDefaultLayout = applyDefaultLayout;
            var shouldApplyDefaultStyle = !preserveExistingStyle;
            if (cachedText == null)
            {
                if (parent.Find(objectName) is RectTransform existingText)
                {
                    cachedText = existingText.GetComponent<Text>();
                    if (cachedText == null)
                    {
                        cachedText = existingText.gameObject.AddComponent<Text>();
                        shouldApplyDefaultLayout = true;
                        shouldApplyDefaultStyle = true;
                    }
                }
                else
                {
                    var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(textObject);
                    var textRect = textObject.GetComponent<RectTransform>();
                    textRect.SetParent(parent, false);
                    cachedText = textObject.GetComponent<Text>();
                    shouldApplyDefaultLayout = true;
                    shouldApplyDefaultStyle = true;
                }
            }

            if (cachedText == null)
            {
                return null;
            }

            if (shouldApplyDefaultLayout)
            {
                var rectTransform = cachedText.rectTransform;
                rectTransform.anchorMin = anchorMin;
                rectTransform.anchorMax = anchorMax;
                rectTransform.offsetMin = offsetMin;
                rectTransform.offsetMax = offsetMax;
            }

            if (shouldApplyDefaultStyle)
            {
                ConfigureText(cachedText, fontSize, fontStyle, alignment);
            }

            cachedText.raycastTarget = false;
            return cachedText;
        }

        private static Image FindOrCreateImage(
            RectTransform parent,
            string objectName,
            ref Image cachedImage,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            bool applyDefaultLayout = false)
        {
            var shouldApplyDefaultLayout = applyDefaultLayout;
            if (cachedImage == null)
            {
                if (parent.Find(objectName) is RectTransform existingImage)
                {
                    cachedImage = existingImage.GetComponent<Image>();
                    if (cachedImage == null)
                    {
                        cachedImage = existingImage.gameObject.AddComponent<Image>();
                        shouldApplyDefaultLayout = true;
                    }
                }
                else
                {
                    var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                    RegisterEditorCreatedObject(imageObject);
                    var imageRect = imageObject.GetComponent<RectTransform>();
                    imageRect.SetParent(parent, false);
                    cachedImage = imageObject.GetComponent<Image>();
                    shouldApplyDefaultLayout = true;
                }
            }

            if (cachedImage == null)
            {
                return null;
            }

            if (shouldApplyDefaultLayout)
            {
                var rectTransform = cachedImage.rectTransform;
                rectTransform.anchorMin = anchorMin;
                rectTransform.anchorMax = anchorMax;
                rectTransform.offsetMin = offsetMin;
                rectTransform.offsetMax = offsetMax;
            }

            return cachedImage;
        }

        private RectTransform ResolveOwnedCardsPanel()
        {
            if (transform.Find("OwnedCardsPanel") is RectTransform ownedCardsPanel)
            {
                return ownedCardsPanel;
            }

            return transform as RectTransform;
        }

        private void ConfigureCardGridRoot(bool applyDefaultLayout)
        {
            if (_cardGridRoot == null)
            {
                return;
            }

            if (applyDefaultLayout)
            {
                _cardGridRoot.anchorMin = _gridAnchorMin;
                _cardGridRoot.anchorMax = _gridAnchorMax;
                _cardGridRoot.pivot = new Vector2(0.5f, 0.5f);
                _cardGridRoot.offsetMin = Vector2.zero;
                _cardGridRoot.offsetMax = Vector2.zero;
            }

            var gridLayout = _cardGridRoot.GetComponent<GridLayoutGroup>();
            var addedGridLayout = false;
            if (gridLayout == null)
            {
                gridLayout = _cardGridRoot.gameObject.AddComponent<GridLayoutGroup>();
                addedGridLayout = true;
            }

            if (applyDefaultLayout || addedGridLayout)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = GridColumnCount;
                gridLayout.childAlignment = TextAnchor.MiddleCenter;
                gridLayout.cellSize = _gridCellSize;
                gridLayout.spacing = _gridCellSpacing;
                gridLayout.padding = new RectOffset(0, 0, 0, 0);
                gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            }
        }

        private void EnsureCardGridSlots()
        {
            if (_cardGridRoot == null)
            {
                return;
            }

            _gridSlots.Clear();
            for (var i = 0; i < CardsPerPage; i++)
            {
                var slotName = $"CardSlot_{i:00}";
                var slotTransform = _cardGridRoot.Find(slotName) as RectTransform;
                if (slotTransform == null)
                {
                    slotTransform = CreateCardSlot(slotName, _cardGridRoot);
                }

                _gridSlots.Add(new CardGridSlot(slotTransform));
            }
        }

        private RectTransform CreateCardSlot(string slotName, RectTransform parent)
        {
            var slotObject = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(Button));
            RegisterEditorCreatedObject(slotObject);
            var slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);

            var artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            RegisterEditorCreatedObject(artworkObject);
            var artworkRect = artworkObject.GetComponent<RectTransform>();
            artworkRect.SetParent(slotRect, false);
            artworkRect.anchorMin = new Vector2(0f, 0f);
            artworkRect.anchorMax = new Vector2(1f, 1f);
            artworkRect.offsetMin = new Vector2(8f, 42f);
            artworkRect.offsetMax = new Vector2(-8f, -8f);
            var artworkImage = artworkObject.GetComponent<Image>();
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;

            var fallbackObject = new GameObject("FallbackNameText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(fallbackObject);
            var fallbackRect = fallbackObject.GetComponent<RectTransform>();
            fallbackRect.SetParent(artworkRect, false);
            StretchFull(fallbackRect, 8f);
            var fallbackText = fallbackObject.GetComponent<Text>();
            ConfigureText(fallbackText, 15, FontStyle.Bold, TextAnchor.MiddleCenter);

            var countObject = new GameObject("CountLevelText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(countObject);
            var countRect = countObject.GetComponent<RectTransform>();
            countRect.SetParent(slotRect, false);
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.offsetMin = new Vector2(8f, 6f);
            countRect.offsetMax = new Vector2(-8f, 38f);
            var countText = countObject.GetComponent<Text>();
            ConfigureText(countText, 17, FontStyle.Bold, TextAnchor.MiddleCenter);

            return slotRect;
        }

        private void EnsurePageButtonUi(RectTransform parent)
        {
            if (_previousPageButton == null)
            {
                _previousPageButton = FindOrCreatePageButton(
                    parent,
                    "PreviousPageButton",
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(34f, 0f),
                    "<",
                    out _previousPageButtonLabel);
            }

            if (_nextPageButton == null)
            {
                _nextPageButton = FindOrCreatePageButton(
                    parent,
                    "NextPageButton",
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-34f, 0f),
                    ">",
                    out _nextPageButtonLabel);
            }

            if (_pageText == null)
            {
                if (parent.Find("PageText") is RectTransform existingPageText)
                {
                    _pageText = existingPageText.GetComponent<Text>();
                    if (_pageText == null)
                    {
                        _pageText = existingPageText.gameObject.AddComponent<Text>();
                    }
                }
                else
                {
                    var pageTextObject = new GameObject("PageText", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(pageTextObject);
                    var pageTextRect = pageTextObject.GetComponent<RectTransform>();
                    pageTextRect.SetParent(parent, false);
                    pageTextRect.anchorMin = new Vector2(0.36f, 0f);
                    pageTextRect.anchorMax = new Vector2(0.4f, 0f);
                    pageTextRect.pivot = new Vector2(0.5f, 0f);
                    pageTextRect.sizeDelta = new Vector2(160f, 36f);
                    pageTextRect.anchoredPosition = new Vector2(0f, 14f);
                    _pageText = pageTextObject.GetComponent<Text>();
                }
            }

            ConfigurePageButton(_previousPageButton, _previousPageButtonLabel, "<");
            ConfigurePageButton(_nextPageButton, _nextPageButtonLabel, ">");
            ConfigureText(_pageText, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (_pageText != null)
            {
                _pageText.color = _ownedCountTextColor;
            }
        }

        private Button FindOrCreatePageButton(
            RectTransform parent,
            string buttonName,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            string label,
            out Text labelText)
        {
            Button button;
            var shouldApplyDefaultLayout = false;
            if (parent.Find(buttonName) is RectTransform existingButtonTransform)
            {
                button = existingButtonTransform.GetComponent<Button>();
                if (button == null)
                {
                    button = existingButtonTransform.gameObject.AddComponent<Button>();
                    shouldApplyDefaultLayout = true;
                }

                labelText = button.GetComponentInChildren<Text>(true);
                if (labelText == null)
                {
                    var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    RegisterEditorCreatedObject(labelObject);
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(existingButtonTransform, false);
                    StretchFull(labelRect, 4f);
                    labelText = labelObject.GetComponent<Text>();
                }
            }
            else
            {
                var buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
                RegisterEditorCreatedObject(buttonObject);
                var buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.SetParent(parent, false);
                button = buttonObject.GetComponent<Button>();
                shouldApplyDefaultLayout = true;

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                RegisterEditorCreatedObject(labelObject);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(buttonRect, false);
                StretchFull(labelRect, 4f);
                labelText = labelObject.GetComponent<Text>();
            }

            var rectTransform = button.GetComponent<RectTransform>();
            if (shouldApplyDefaultLayout)
            {
                rectTransform.anchorMin = anchor;
                rectTransform.anchorMax = anchor;
                rectTransform.pivot = pivot;
                rectTransform.sizeDelta = _arrowButtonSize;
                rectTransform.anchoredPosition = anchoredPosition;
            }

            if (labelText != null)
            {
                labelText.text = label;
            }

            return button;
        }

        private void ConfigurePageButton(Button button, Text labelText, string label)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = _arrowButtonColor;
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.75f, 0.84f, 1f, 0.88f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.52f);
            button.colors = colors;

            if (button == _previousPageButton)
            {
                button.onClick.RemoveListener(ShowPreviousPageFromUi);
                button.onClick.AddListener(ShowPreviousPageFromUi);
            }
            else if (button == _nextPageButton)
            {
                button.onClick.RemoveListener(ShowNextPageFromUi);
                button.onClick.AddListener(ShowNextPageFromUi);
            }

            ConfigureText(labelText, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.raycastTarget = false;
            }
        }

        private void AutoAssignSceneReferences()
        {
            if (_titleText == null && transform.Find("HeaderPanel/TitleText") is RectTransform titleTransform)
            {
                _titleText = titleTransform.GetComponent<Text>();
            }

            if (_accountText == null && transform.Find("HeaderPanel/AccountText") is RectTransform accountTransform)
            {
                _accountText = accountTransform.GetComponent<Text>();
            }

            if (_ownedCardsText == null && transform.Find("OwnedCardsPanel/Scroll View/Viewport/Content/OwnedCardsText") is RectTransform ownedCardsTransform)
            {
                _ownedCardsText = ownedCardsTransform.GetComponent<Text>();
            }

            if (_statusText == null && transform.Find("StatusText") is RectTransform statusTransform)
            {
                _statusText = statusTransform.GetComponent<Text>();
            }

            if (_backButton == null && transform.Find("HeaderPanel/BackButton") is RectTransform backButtonTransform)
            {
                _backButton = backButtonTransform.GetComponent<Button>();
            }

            if (_backButtonLabel == null && _backButton != null)
            {
                _backButtonLabel = _backButton.GetComponentInChildren<Text>(true);
            }

            if (_upgradeButton == null && transform.Find("DetailPanel/UpgradeButton") is RectTransform upgradeButtonTransform)
            {
                _upgradeButton = upgradeButtonTransform.GetComponent<Button>();
            }

            if (_upgradeButtonLabel == null && _upgradeButton != null)
            {
                _upgradeButtonLabel = _upgradeButton.GetComponentInChildren<Text>(true);
            }
        }

        private void ApplyStaticVisuals()
        {
            ConfigureText(_titleText, 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            ConfigureText(_accountText, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            ConfigureText(_ownedCardsText, 28, FontStyle.Normal, TextAnchor.UpperLeft);
            ConfigureText(_statusText, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            ConfigureText(_backButtonLabel, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            ConfigureText(_upgradeButtonLabel, 18, FontStyle.Bold, TextAnchor.MiddleCenter);

            if (_titleText != null && _collectionVisualVersion == 0)
            {
                _titleText.text = "Owned Cards";
            }

            if (_backButtonLabel != null && _collectionVisualVersion == 0)
            {
                _backButtonLabel.text = "Back To Start";
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(ReturnToStartSceneFromUi);
                _backButton.onClick.AddListener(ReturnToStartSceneFromUi);
            }

            ConfigurePageButton(_previousPageButton, _previousPageButtonLabel, "<");
            ConfigurePageButton(_nextPageButton, _nextPageButtonLabel, ">");
            ConfigureText(_pageText, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            ConfigureUpgradeButton();
            RefreshUpgradeButtonState();
        }

        private static void RegisterEditorCreatedObject(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && gameObject != null)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Owned Cards UI");
            }
#endif
        }

        private static void StretchFull(RectTransform rectTransform, float padding = 0f)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private static void ConfigureText(Text text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            // The authored collection layout owns typography; refresh only its data.
            var controller = text.GetComponentInParent<OwnedCardsSceneController>();
            if (controller != null && controller.CollectionVisualVersion > 0 && text.font != null)
            {
                return;
            }

            text.font = ResolveRuntimeFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
        }

        private static Font ResolveRuntimeFont()
        {
            if (s_runtimeKoreanFont != null)
            {
                return s_runtimeKoreanFont;
            }

            try
            {
                s_runtimeKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not create runtime Korean font for owned-card scene: {ex.Message}");
                s_runtimeKoreanFont = null;
            }

            return s_runtimeKoreanFont != null
                ? s_runtimeKoreanFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private readonly struct DetailStatDisplay
        {
            public DetailStatDisplay(int attack, int hp)
            {
                Attack = attack;
                Hp = hp;
            }

            public int Attack { get; }

            public int Hp { get; }
        }

        private readonly struct CardGridEntry
        {
            public CardGridEntry(string cardId, string displayName, Sprite artwork)
            {
                CardId = cardId ?? string.Empty;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? CardId : displayName;
                Artwork = artwork;
            }

            public string CardId { get; }

            public string DisplayName { get; }

            public Sprite Artwork { get; }
        }

        private sealed class CardGridSlot
        {
            private readonly RectTransform _root;
            private readonly Image _backgroundImage;
            private readonly Button _button;
            private readonly Outline _upgradeReadyOutline;
            private readonly Image _artworkImage;
            private readonly Text _fallbackNameText;
            private readonly Text _countLevelText;

            public CardGridSlot(RectTransform root)
            {
                _root = root;
                _backgroundImage = root != null ? root.GetComponent<Image>() : null;
                _button = root != null ? root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>() : null;
                _upgradeReadyOutline = root != null ? root.GetComponent<Outline>() ?? root.gameObject.AddComponent<Outline>() : null;
                _artworkImage = root != null ? root.Find("Artwork")?.GetComponent<Image>() : null;
                _fallbackNameText = root != null ? root.Find("Artwork/FallbackNameText")?.GetComponent<Text>() : null;
                _countLevelText = root != null ? root.Find("CountLevelText")?.GetComponent<Text>() : null;

                if (_upgradeReadyOutline != null)
                {
                    _upgradeReadyOutline.enabled = false;
                    _upgradeReadyOutline.useGraphicAlpha = false;
                }
            }

            public void SetEmpty(Color emptyArtworkColor)
            {
                if (_root != null)
                {
                    _root.gameObject.SetActive(true);
                }

                if (_backgroundImage != null)
                {
                    _backgroundImage.color = new Color(0f, 0f, 0f, 0f);
                    _backgroundImage.raycastTarget = false;
                }

                if (_button != null)
                {
                    _button.onClick.RemoveAllListeners();
                    _button.interactable = false;
                }

                if (_upgradeReadyOutline != null)
                {
                    _upgradeReadyOutline.enabled = false;
                }

                if (_artworkImage != null)
                {
                    _artworkImage.sprite = null;
                    _artworkImage.color = new Color(emptyArtworkColor.r, emptyArtworkColor.g, emptyArtworkColor.b, 0f);
                    _artworkImage.enabled = false;
                }

                if (_fallbackNameText != null)
                {
                    _fallbackNameText.text = string.Empty;
                    _fallbackNameText.enabled = false;
                }

                if (_countLevelText != null)
                {
                    _countLevelText.text = string.Empty;
                    _countLevelText.enabled = false;
                }
            }

            public void SetCard(
                CardGridEntry entry,
                int copyCount,
                int upgradeLevel,
                string upgradeProgressText,
                bool isUpgradeReady,
                Color slotColor,
                Color emptyArtworkColor,
                Color countTextColor,
                Color upgradeReadyGlowColor,
                Vector2 upgradeReadyGlowDistance,
                bool isSelected,
                Action<string> clickHandler)
            {
                if (_root != null)
                {
                    _root.gameObject.SetActive(true);
                }

                if (_backgroundImage != null)
                {
                    _backgroundImage.color = isSelected
                        ? new Color(0.42f, 0.34f, 0.15f, 1f)
                        : slotColor;
                    _backgroundImage.raycastTarget = true;
                }

                if (_button != null)
                {
                    _button.onClick.RemoveAllListeners();
                    _button.interactable = true;
                    var selectedCardId = entry.CardId;
                    if (clickHandler != null)
                    {
                        _button.onClick.AddListener(() => clickHandler(selectedCardId));
                    }
                }

                if (_upgradeReadyOutline != null)
                {
                    _upgradeReadyOutline.enabled = isUpgradeReady;
                    _upgradeReadyOutline.effectColor = upgradeReadyGlowColor;
                    _upgradeReadyOutline.effectDistance = upgradeReadyGlowDistance;
                    _upgradeReadyOutline.useGraphicAlpha = false;
                }

                var hasArtwork = entry.Artwork != null;
                if (_artworkImage != null)
                {
                    _artworkImage.sprite = entry.Artwork;
                    _artworkImage.enabled = true;
                    _artworkImage.color = hasArtwork ? Color.white : emptyArtworkColor;
                    _artworkImage.preserveAspect = true;
                    _artworkImage.raycastTarget = false;
                }

                if (_fallbackNameText != null)
                {
                    _fallbackNameText.enabled = !hasArtwork;
                    _fallbackNameText.text = hasArtwork ? string.Empty : entry.DisplayName;
                    _fallbackNameText.color = Color.white;
                }

                if (_countLevelText != null)
                {
                    _countLevelText.enabled = true;
                    _countLevelText.text = string.IsNullOrWhiteSpace(upgradeProgressText)
                        ? $"Lv.{Mathf.Max(0, upgradeLevel)}"
                        : upgradeProgressText;
                    _countLevelText.color = copyCount > 0
                        ? countTextColor
                        : new Color(0.72f, 0.72f, 0.76f, 0.92f);
                }
            }
        }
    }
}
