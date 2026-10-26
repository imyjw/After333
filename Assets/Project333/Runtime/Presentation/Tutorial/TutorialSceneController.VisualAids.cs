using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Presentation.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private RectTransform _civilizationResourceAid, _goldSubstitutionAid, _cardKindsAid;

        private void BuildChapterOneVisualAids()
        {
            _civilizationResourceAid = ExplanationPanel("CivilizationResources", "세 대륙의 자원");
            var civilizations = new[] { "판타지", "무림", "과학 문명" };
            var resourceNames = new[] { "마나", "기", "전력" };
            var icons = new[] { "Mana", "Qi", "Power" };
            for (var i = 0; i < 3; i++)
            {
                var tile = Panel(_civilizationResourceAid, "ResourceExample_" + icons[i],
                    new Vector2(.04f + i * .32f, .14f), new Vector2(.32f + i * .32f, .79f));
                AidLabel(tile, "Civilization", civilizations[i], 34, Gold, .05f, .79f, .95f, .95f);
                ResourceAidIcon(tile, icons[i], .20f, .27f, .80f, .74f);
                AidLabel(tile, "ResourceName", resourceNames[i], 38, Color.white, .05f, .04f, .95f, .22f);
            }
            AidLabel(_civilizationResourceAid, "ResourceHint", "카드의 소속에 맞는 자원을 사용합니다.", 26, Color.white, .04f, .02f, .96f, .12f);

            _goldSubstitutionAid = ExplanationPanel("GoldSubstitution", "골드: 모든 대륙에서 사용할 수 있는 귀중한 자원");
            AidLabel(_goldSubstitutionAid, "ExchangeRatio", "부족한 자원과 1 : 1로 대체 지불", 32, Gold, .05f, .75f, .95f, .85f);
            ResourceAidIcon(_goldSubstitutionAid, "Gold", .065f, .32f, .25f, .71f);
            AidLabel(_goldSubstitutionAid, "GoldAmount", "골드 1", 36, Gold, .045f, .17f, .27f, .29f);
            AidLabel(_goldSubstitutionAid, "PaymentArrow", "→", 64, Color.white, .28f, .37f, .39f, .60f);
            for (var i = 0; i < 3; i++)
            {
                var left = .42f + i * .19f;
                ResourceAidIcon(_goldSubstitutionAid, icons[i], left, .36f, left + .15f, .64f);
                AidLabel(_goldSubstitutionAid, "Substitute_" + icons[i], resourceNames[i] + " 1", 32, Color.white, left, .20f, left + .15f, .32f);
                if (i < 2) AidLabel(_goldSubstitutionAid, "Or_" + i, "또는", 22, Gold, left + .15f, .42f, left + .19f, .54f);
            }
            AidLabel(_goldSubstitutionAid, "PaymentHint", "마나 1 또는 기 1 또는 전력 1을 골드 1로 대신 지불합니다.", 26, Color.white, .04f, .025f, .96f, .14f);

            _cardKindsAid = ExplanationPanel("CardKindsExamples", "카드의 세 가지 종류");
            var ids = new[] { "Goblin", "ManaPond", "firebolt" };
            var kinds = new[] { "유닛", "건물", "마법" };
            var names = new[] { "고블린", "마나의 샘", "파이어볼" };
            for (var i = 0; i < 3; i++)
            {
                var tile = Rect(_cardKindsAid, "CardExample_" + ids[i],
                    new Vector2(.04f + i * .32f, .03f), new Vector2(.32f + i * .32f, .86f));
                AidLabel(tile, "Kind", kinds[i], 34, Gold, .03f, .88f, .97f, 1f);
                var artwork = AidImage(tile, "CardArtwork", .03f, .12f, .97f, .86f);
                if (CardArtworkLibrary.TryGetArtwork(ids[i], out var sprite)) artwork.sprite = sprite;
                AidLabel(tile, "CardName", names[i], 28, Color.white, .03f, 0f, .97f, .10f);
            }
            HideChapterOneVisualAids();
        }

        private RectTransform ExplanationPanel(string name, string title)
        {
            var panel = Panel(_safeRoot, name, new Vector2(.19f, .215f), new Vector2(.91f, .865f));
            // Keep navigation, guide portrait and dialogue above non-interactive illustrations.
            panel.SetSiblingIndex(1);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            AidLabel(panel, "Title", title, 36, Gold, .04f, .88f, .96f, .985f);
            return panel;
        }

        private static Image AidImage(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var image = Rect(parent, name, new Vector2(left, bottom), new Vector2(right, top)).gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
        private static void ResourceAidIcon(Transform parent, string icon, float left, float bottom, float right, float top)
        {
            // Battle HUD resource assets are textures, shared through RawImage.
            var slot = Rect(parent, icon + "IconSlot", new Vector2(left, bottom), new Vector2(right, top));
            var image = Rect(slot, icon + "Icon", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Project333/BattleUI/ResourceIcons/" + icon);
            image.raycastTarget = false;
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 1f;
        }
        private static void AidLabel(Transform parent, string name, string content, int size, Color color, float left, float bottom, float right, float top)
        {
            var label = Label(parent, name, content, size, color, TextAnchor.MiddleCenter);
            Place(label.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = size;
        }
        private void RefreshChapterOneVisualAids()
        {
            var available = _session != null && DisplayedDialogueChapter == 1 && CanNavigateDialogue && (!_session.IsComplete || IsReviewingDialogue);
            var cue = _session != null ? DisplayedDialogueStep.Cue : TutorialCue.Introduction;
            SetAidVisible(_civilizationResourceAid, available && cue == TutorialCue.Resources);
            SetAidVisible(_goldSubstitutionAid, available && (cue == TutorialCue.Gold || cue == TutorialCue.Substitution));
            SetAidVisible(_cardKindsAid, available && cue == TutorialCue.CardKinds);
        }
        private void HideChapterOneVisualAids()
        {
            SetAidVisible(_civilizationResourceAid, false);
            SetAidVisible(_goldSubstitutionAid, false);
            SetAidVisible(_cardKindsAid, false);
        }
        private static void SetAidVisible(RectTransform panel, bool visible)
        {
            if (panel != null && panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
        }
    }
}
