using System.Collections.Generic;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Development
{
    public sealed class ServerAccountSmokePanel : MonoBehaviour
    {
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };

        private static Font s_runtimeKoreanFont;

        private Canvas _canvas;
        private RectTransform _panelRect;
        private Image _panelImage;
        private RectTransform _textRect;
        private Text _text;

        public static ServerAccountSmokePanel GetOrCreate(string objectName, int sortingOrder)
        {
            var existingObject = GameObject.Find(objectName);
            if (existingObject != null &&
                existingObject.TryGetComponent<ServerAccountSmokePanel>(out var existingPanel))
            {
                existingPanel.EnsureVisualObjects();
                existingPanel.SetSortingOrder(sortingOrder);
                return existingPanel;
            }

            var canvasObject = new GameObject(
                objectName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(ServerAccountSmokePanel));
            var panel = canvasObject.GetComponent<ServerAccountSmokePanel>();
            panel.EnsureVisualObjects();
            panel.SetSortingOrder(sortingOrder);
            return panel;
        }

        public void Configure(
            bool visible,
            Vector2 anchor,
            Vector2 offset,
            Vector2 size,
            Vector2 textPadding,
            Color panelColor,
            Color textColor,
            int fontSize)
        {
            EnsureVisualObjects();
            gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_panelRect != null)
            {
                var clampedAnchor = new Vector2(Mathf.Clamp01(anchor.x), Mathf.Clamp01(anchor.y));
                _panelRect.anchorMin = clampedAnchor;
                _panelRect.anchorMax = clampedAnchor;
                _panelRect.pivot = ResolvePivot(clampedAnchor);
                _panelRect.anchoredPosition = offset;
                _panelRect.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            }

            if (_panelImage != null)
            {
                _panelImage.color = panelColor;
                _panelImage.raycastTarget = false;
            }

            if (_textRect != null)
            {
                var horizontalPadding = Mathf.Max(0f, textPadding.x);
                var verticalPadding = Mathf.Max(0f, textPadding.y);
                _textRect.anchorMin = Vector2.zero;
                _textRect.anchorMax = Vector2.one;
                _textRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _textRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_text != null)
            {
                _text.font = ResolveRuntimeFont();
                _text.fontSize = Mathf.Max(1, fontSize);
                _text.color = textColor;
            }
        }

        public void Refresh(string sceneLabel, string nextAction, string extraLine = null)
        {
            EnsureVisualObjects();
            if (_text == null)
            {
                return;
            }

            var lines = new List<string>
            {
                $"SMOKE CHECK | {Normalize(sceneLabel, "Scene")}",
                BuildAccountLine(),
                BuildWalletLine(),
                BuildRunLine(),
                BuildDraftLine()
            };

            if (!string.IsNullOrWhiteSpace(extraLine))
            {
                lines.Add(extraLine);
            }

            lines.Add($"Next: {Normalize(nextAction, "-")}");
            _text.text = string.Join("\n", lines);
        }

        private void EnsureVisualObjects()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas != null)
                {
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }

            if (TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
            }

            if (_panelRect == null)
            {
                var panelObject = new GameObject("SmokePanel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(transform, false);
                _panelRect = panelObject.GetComponent<RectTransform>();
                _panelImage = panelObject.GetComponent<Image>();
            }

            if (_text == null)
            {
                var textObject = new GameObject("SmokePanelText", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(_panelRect, false);
                _textRect = textObject.GetComponent<RectTransform>();
                _text = textObject.GetComponent<Text>();
                _text.alignment = TextAnchor.UpperLeft;
                _text.fontStyle = FontStyle.Bold;
                _text.horizontalOverflow = HorizontalWrapMode.Wrap;
                _text.verticalOverflow = VerticalWrapMode.Truncate;
                _text.supportRichText = false;
                _text.raycastTarget = false;
            }
        }

        private void SetSortingOrder(int sortingOrder)
        {
            EnsureVisualObjects();
            if (_canvas != null)
            {
                _canvas.sortingOrder = sortingOrder;
            }
        }

        private static string BuildAccountLine()
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                return "Account: not signed in";
            }

            var name = string.IsNullOrWhiteSpace(AccountSessionState.DisplayName)
                ? "Player"
                : AccountSessionState.DisplayName;
            return $"Account: {name}";
        }

        private static string BuildWalletLine()
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                return "Wallet: tickets - / gold -";
            }

            return $"Wallet: tickets {AccountSessionState.Tickets} / gold {AccountSessionState.ResourceGold}";
        }

        private static string BuildRunLine()
        {
            if (AccountSessionState.HasResumableRun)
            {
                return $"Run: active {Normalize(AccountSessionState.ActiveRunStatus, "-")} W{AccountSessionState.ActiveRunWins}/L{AccountSessionState.ActiveRunLosses} deck:{ShortId(AccountSessionState.ActiveDeckId)}";
            }

            if (AccountSessionState.HasLatestRun)
            {
                var rewardState = AccountSessionState.IsLatestRunRewardClaimed
                    ? "claimed"
                    : AccountSessionState.HasUnclaimedLatestRunRewards ? "pending" : "none";
                return $"Run: latest {Normalize(AccountSessionState.LatestRunStatus, "-")} W{AccountSessionState.LatestRunWins}/L{AccountSessionState.LatestRunLosses} reward:{rewardState}";
            }

            return "Run: none";
        }

        private static string BuildDraftLine()
        {
            var currentCount = DraftRunSessionState.CurrentDraftDeckCardIds?.Count ?? 0;
            var completedCount = DraftRunSessionState.LastCompletedDraftDeckCardIds?.Count ?? 0;
            var visibleCount = currentCount > 0 ? currentCount : completedCount;
            var ready = DraftRunSessionState.HasDraftedDeckReady ? "ready" : "drafting";
            return $"Draft: {visibleCount}/33 {ready} local W{DraftRunSessionState.Wins}/L{DraftRunSessionState.Losses}";
        }

        private static Vector2 ResolvePivot(Vector2 anchor)
        {
            return new Vector2(
                anchor.x <= 0.01f ? 0f : anchor.x >= 0.99f ? 1f : 0.5f,
                anchor.y <= 0.01f ? 0f : anchor.y >= 0.99f ? 1f : 0.5f);
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "-";
            }

            var trimmed = id.Trim();
            return trimmed.Length <= 8 ? trimmed : trimmed.Substring(0, 8);
        }

        private static Font ResolveRuntimeFont()
        {
            if (s_runtimeKoreanFont != null)
            {
                return s_runtimeKoreanFont;
            }

            try
            {
                s_runtimeKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 24);
            }
            catch
            {
                s_runtimeKoreanFont = null;
            }

            return s_runtimeKoreanFont != null
                ? s_runtimeKoreanFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
