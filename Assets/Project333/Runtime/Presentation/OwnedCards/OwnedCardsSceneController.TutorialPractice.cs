using System;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.OwnedCards
{
    public sealed partial class OwnedCardsSceneController
    {
        private bool _tutorialCollectionLoaded, _tutorialCollectionAllowDismiss;
        private TutorialUpgradePractice _tutorialUpgrade;
        private Func<TutorialAction, bool> _tutorialCollectionAllowed;
        private Action<TutorialAction> _tutorialCollectionCompleted;
        private bool IsTutorialCollectionMode => _tutorialCollectionLoaded || TutorialRuntimeIsolation.IsActive;
        private long DisplayedCollectionGold => IsTutorialCollectionMode ? _tutorialUpgrade?.Gold ?? 3 : AccountSessionState.ResourceGold;
        public RectTransform TutorialGoblinCardRect => _gridSlots.Count > 0 ? _gridSlots[0].Root : null;
        public RectTransform TutorialCollectionGridRect => _cardGridRoot;
        public RectTransform TutorialArtworkRect => _detailArtworkImage?.rectTransform;
        public Image TutorialArtwork => _detailArtworkImage;
        public RectTransform TutorialUpgradeTableRect => _detailUpgradeTableRoot;
        public RectTransform TutorialOwnershipRect => _detailOwnershipText?.rectTransform;
        public RectTransform TutorialMetaRect => _detailMetaText?.GetComponentInParent<ScrollRect>()?.GetComponent<RectTransform>();
        public RectTransform TutorialUpgradeButtonRect => _upgradeButton?.GetComponent<RectTransform>();
        public RectTransform TutorialHpRect => _detailHpValueText?.rectTransform;
        public bool IsTutorialFlipAnimating => _detailArtworkFlipRoutine != null;
        public bool TutorialTableIsVisible => _detailArtworkBackVisible && !IsTutorialFlipAnimating;

        private bool TryInitializeTutorialCollection()
        {
            if (!IsTutorialCollectionMode) return false;
            if (_tutorialCollectionLoaded) { SetBackButtonInteractable(false); RefreshTutorialCollectionWallet(); return true; }
            _tutorialCollectionLoaded = true;
            AutoAssignSceneReferences(); EnsureCardGridUi(); ApplyStaticVisuals();
            SetBackButtonInteractable(false);
            RefreshTutorialCollectionWallet();
            SetStatus("튜토리얼 전용 · 실제 카드와 골드는 변경되지 않습니다.");
            return true;
        }
        public void ConfigureTutorialCollection(TutorialUpgradePractice practice, Func<TutorialAction, bool> allowed, Action<TutorialAction> completed)
        {
            if (!TutorialRuntimeIsolation.IsActive) throw new InvalidOperationException("A tutorial host must be active.");
            TryInitializeTutorialCollection();
            _tutorialCollectionAllowed = allowed; _tutorialCollectionCompleted = completed;
            if (!ReferenceEquals(_tutorialUpgrade, practice))
            {
                _tutorialUpgrade = practice ?? throw new ArgumentNullException(nameof(practice));
                _selectedCardId = string.Empty; _currentPageIndex = 0;
                _tutorialCollectionAllowDismiss = true; CloseCardDetailFromUi(); _tutorialCollectionAllowDismiss = false;
                PresentTutorialInventory();
                _lastModalAvailableSize = Vector2.zero; ResizeCardDetailWindow();
            }
            RefreshUpgradeButtonState();
        }
        private void PresentTutorialInventory()
        {
            RebuildOwnedCardLookup(new[]
            {
                new OwnedCardDto { CardId = TutorialUpgradePractice.CardId, CopyCount = _tutorialUpgrade.Copies, UpgradeLevel = _tutorialUpgrade.Level },
                new OwnedCardDto { CardId = "ManaShield", CopyCount = 1 },
                new OwnedCardDto { CardId = "ManaPond", CopyCount = 1 },
                new OwnedCardDto { CardId = "firebolt", CopyCount = 1 }
            });
            RebuildCardGridEntries(); RenderCurrentPage(); RenderSelectedCardDetail(); RefreshTutorialCollectionWallet();
        }
        private void RebuildTutorialCardEntries()
        {
            foreach (var id in new[] { "Goblin", "ManaShield", "ManaPond", "firebolt" })
            {
                var asset = ResolveCardAsset(id);
                if (asset != null) _cardGridEntries.Add(new CardGridEntry(id, asset.DisplayName, ResolveCardArtwork(id, asset)));
            }
        }
        private void RefreshTutorialCollectionWallet()
        {
            if (_accountText != null) AccountWalletView.Attach(_accountText, false, true)?.Render(true, 3, DisplayedCollectionGold, "튜토리얼 전용");
        }
        private bool TutorialCollectionActionAllowed(TutorialAction action) => _tutorialCollectionAllowed?.Invoke(action) == true;
        private void UpgradeTutorialCard()
        {
            if (_tutorialUpgrade == null || _selectedCardId != TutorialUpgradePractice.CardId || !TutorialCollectionActionAllowed(TutorialAction.UpgradePracticeCard)) return;
            var asset = ResolveCardAsset(_selectedCardId);
            if (asset == null || !_tutorialUpgrade.TryUpgrade(ResolveCurrentDefinition(asset))) return;
            PresentTutorialInventory();
            SetStatus("연습 강화 완료 · 고블린 Lv.1 · 카드 -3 · 골드 -3 · HP +1");
            _tutorialCollectionCompleted?.Invoke(TutorialAction.UpgradePracticeCard);
        }
        private void NotifyTutorialFlipCompleted(bool back)
        {
            if (IsTutorialCollectionMode) _tutorialCollectionCompleted?.Invoke(back ? TutorialAction.ShowUpgradeTable : TutorialAction.RestoreCardArtwork);
        }
    }
}
