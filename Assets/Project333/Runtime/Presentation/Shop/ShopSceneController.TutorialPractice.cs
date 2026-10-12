using System;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;

namespace Project333.Runtime.Presentation.Shop
{
    public sealed partial class ShopSceneController
    {
        private bool _tutorialShopLoaded;
        private bool IsTutorialShopMode => _tutorialShopLoaded || TutorialRuntimeIsolation.IsActive;
        public RectTransform TutorialTicketPanel => transform.Find("TicketProductPanel") as RectTransform;
        public RectTransform TutorialAdPanel => transform.Find("RewardedTicketProductPanel") as RectTransform;
        public RectTransform TutorialPackPanel => transform.Find("CardPackProductPanel") as RectTransform;
        public RectTransform TutorialWalletRect => _accountText?.rectTransform;
        private bool TryInitializeTutorialShop()
        {
            if (!IsTutorialShopMode) return false;
            _tutorialShopLoaded = true; EnsureShopProducts(); RefreshTutorialShopUi(); return true;
        }
        public void ConfigureTutorialShop()
        {
            if (!TutorialRuntimeIsolation.IsActive) throw new InvalidOperationException("A tutorial host must be active.");
            TryInitializeTutorialShop();
            foreach (var panel in new[] { TutorialTicketPanel, TutorialPackPanel, TutorialAdPanel })
                if (panel != null) { panel.anchorMin = new Vector2(panel.anchorMin.x, .24f); panel.anchorMax = new Vector2(panel.anchorMax.x, .80f); }
            SetStatus("튜토리얼 전용 · 실제 구매와 광고 재생은 하지 않습니다.");
        }
        private void RefreshTutorialShopUi()
        {
            if (_accountText != null) AccountWalletView.Attach(_accountText, false, true)?.Render(true, 3, 3, "튜토리얼 전용");
            if (_purchaseTicketButton != null) _purchaseTicketButton.interactable = false;
            if (_purchaseCardPackButton != null) _purchaseCardPackButton.interactable = false;
            if (_backButton != null) _backButton.interactable = false;
            _rewardedTicketController?.RefreshUi();
        }
    }
}
