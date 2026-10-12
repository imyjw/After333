using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    public enum RunRewardRevealPhase { Hidden, Closed, Opening, WaitingForReceipt, Rewards, Error }

    /// <summary>Run reward presentation only. Payment and the original receipt belong to the server.</summary>
    public sealed class RunRewardRevealView : MonoBehaviour
    {
        private const float OpeningDuration = 1.15f;
        private static readonly Color Gold = new Color(1f, .78f, .32f);
        private Action _openRequested;
        private Action _confirmed;
        private RectTransform _safeArea;
        private RectTransform _chest;
        private RectTransform _backEffects;
        private RectTransform _frontEffects;
        private CanvasGroup _chestGroup;
        private Image _closedImage;
        private Image _openImage;
        private Image _glow;
        private Button _chestButton;
        private Text _title;
        private Text _record;
        private Text _hint;
        private RectTransform _panel;
        private Text _panelRecord;
        private Text _goldValue;
        private Text _ticketValue;
        private GameObject _ticketTile;
        private Text _cardSummary;
        private Text _wallet;
        private Button _confirmButton;
        private ScrollRect _scroll;
        private RectTransform _content;
        private readonly List<RectTransform> _rays = new List<RectTransform>();
        private readonly List<Image> _sparks = new List<Image>();
        private readonly List<GameObject> _rows = new List<GameObject>();
        private Sprite _glowSprite;
        private Texture2D _glowTexture;
        private RunRewardDisplayContent _receipt;
        private float _elapsed;
        private float _ambientTime;
        private float _confirmDelay;
        private float _chestLayoutScale = 1f;
        private bool _canOpen;
        private Vector2 _lastSize;
        private Rect _lastSafeArea;

        public RunRewardRevealPhase Phase { get; private set; }
        public bool IsBusy => Phase == RunRewardRevealPhase.Opening || Phase == RunRewardRevealPhase.WaitingForReceipt;
        public bool IsShowingRewards => Phase == RunRewardRevealPhase.Rewards;

        public static RunRewardRevealView Create(RectTransform parent, Action openRequested, Action confirmed)
        {
            var root = new GameObject("RunRewardReveal", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);
            var view = root.AddComponent<RunRewardRevealView>();
            view.Build();
            view._openRequested = openRequested;
            view._confirmed = confirmed;
            view.Hide();
            return view;
        }

        public void ShowClosed(int wins, int losses, bool canOpen, bool checkingResult = false)
        {
            if (IsBusy || IsShowingRewards) return;
            gameObject.SetActive(true);
            Phase = RunRewardRevealPhase.Closed;
            _receipt = null;
            _canOpen = canOpen;
            _title.text = "런 종료";
            _record.text = $"{wins}승  ·  {losses}패";
            _hint.text = canOpen ? "상자를 눌러 보상을 확인하세요" :
                checkingResult ? "전투 결과를 확인하고 있습니다" : "전투 결과 확인을 다시 시도하려면 상자를 누르세요";
            _chestButton.interactable = canOpen || !checkingResult;
            _panel.gameObject.SetActive(false);
            _chest.gameObject.SetActive(true);
            _chestGroup.alpha = 1;
            SetChestPose(0);
            RefreshLayout();
        }

        public bool TryBeginOpening()
        {
            if (!_canOpen || (Phase != RunRewardRevealPhase.Closed && Phase != RunRewardRevealPhase.Error)) return false;
            Phase = RunRewardRevealPhase.Opening;
            _elapsed = 0;
            _receipt = null;
            _chestButton.interactable = false;
            _hint.text = "보상 상자를 여는 중입니다";
            return true;
        }

        public void SetReceipt(RunRewardDisplayContent receipt)
        {
            if (receipt == null || (Phase != RunRewardRevealPhase.Opening && Phase != RunRewardRevealPhase.WaitingForReceipt)) return;
            _receipt = receipt;
            if (Phase == RunRewardRevealPhase.WaitingForReceipt) ShowReceipt();
        }

        public void ShowError(string message)
        {
            if (Phase == RunRewardRevealPhase.Hidden || IsShowingRewards) return;
            Phase = RunRewardRevealPhase.Error;
            _receipt = null;
            _canOpen = true;
            _hint.text = message;
            _chestButton.interactable = true;
            _panel.gameObject.SetActive(false);
            _chest.gameObject.SetActive(true);
            _chestGroup.alpha = 1;
            SetChestPose(0);
        }

        public void Hide()
        {
            Phase = RunRewardRevealPhase.Hidden;
            _receipt = null;
            gameObject.SetActive(false);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void Advance(float delta)
        {
            if (Phase == RunRewardRevealPhase.Hidden) return;
            _ambientTime += Mathf.Max(0, delta);
            if (IsBusy)
            {
                _elapsed += Mathf.Max(0, delta);
                SetChestPose(Mathf.Clamp01(_elapsed / OpeningDuration));
                if (_elapsed >= OpeningDuration)
                {
                    if (_receipt != null) ShowReceipt();
                    else
                    {
                        Phase = RunRewardRevealPhase.WaitingForReceipt;
                        _hint.text = "보상을 확인하고 있습니다…";
                    }
                }
            }
            else if (Phase == RunRewardRevealPhase.Closed || Phase == RunRewardRevealPhase.Error)
            {
                _chest.localScale = Vector3.one * _chestLayoutScale * (1f + .014f * Mathf.Sin(_ambientTime * 2.4f));
                _glow.color = new Color(1, .68f, .18f, .15f + .035f * Mathf.Sin(_ambientTime * 2.4f));
            }
            else if (IsShowingRewards)
            {
                _confirmDelay = Mathf.Max(0, _confirmDelay - delta);
                _confirmButton.interactable = _confirmDelay <= 0;
            }
            RefreshLayout();
        }

        private void OnDisable()
        {
            // An interrupted reveal restarts from authoritative run state on the next enable.
            Phase = RunRewardRevealPhase.Hidden;
            _receipt = null;
        }

        private void OnDestroy()
        {
            Release(_glowSprite);
            Release(_glowTexture);
        }

        private void Build()
        {
            var backdrop = Image("DimmedBackground", transform, new Color(0, 0, 0, .74f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true; // Outside clicks never dismiss or reach the scene beneath.
            _safeArea = Rect("SafeArea", transform);
            Stretch(_safeArea);
            _title = Text("Title", _safeArea, "런 종료", 46, Gold);
            _record = Text("FinalRecord", _safeArea, "", 30, Color.white);
            _hint = Text("Hint", _safeArea, "", 26, new Color(.86f, .89f, .95f));
            _chest = Rect("RewardChest", _safeArea);
            _chest.sizeDelta = new Vector2(480, 480);
            _chestGroup = _chest.gameObject.AddComponent<CanvasGroup>();
            _backEffects = Rect("ChestBackEffects", _chest);
            _glow = Image("GoldenLight", _backEffects, new Color(1, .68f, .18f, .18f));
            _glow.sprite = CreateGlowSprite();
            Center(_glow.rectTransform, new Vector2(600, 560), new Vector2(0, 55));
            for (var i = 0; i < 12; i++)
            {
                var ray = Image("LightRay" + i, _backEffects, new Color(1f, .77f, .38f, 0));
                Center(ray.rectTransform, new Vector2(12, 260), new Vector2(0, 24));
                ray.rectTransform.pivot = new Vector2(.5f, 0);
                ray.rectTransform.localEulerAngles = new Vector3(0, 0, -77 + i * 14);
                _rays.Add(ray.rectTransform);
            }
            var sprites = Resources.LoadAll<Sprite>("Project333/UI/RunRewards/RewardChestStates");
            Sprite closed = null, opened = null;
            foreach (var sprite in sprites)
            {
                if (sprite.name == "RewardChestClosed") closed = sprite;
                if (sprite.name == "RewardChestOpen") opened = sprite;
            }
            _closedImage = Image("ClosedChest", _chest, Color.white);
            Stretch(_closedImage.rectTransform);
            _closedImage.sprite = closed;
            _closedImage.preserveAspect = true;
            _openImage = Image("OpenChest", _chest, new Color(1, 1, 1, 0));
            Stretch(_openImage.rectTransform);
            _openImage.sprite = opened;
            _openImage.preserveAspect = true;
            // The authored open frame has a slightly left-shifted body; align the body, not the light.
            _openImage.rectTransform.offsetMin = new Vector2(11, 0);
            _openImage.rectTransform.offsetMax = new Vector2(11, 0);
            _closedImage.raycastTarget = true;
            _chestButton = _chest.gameObject.AddComponent<Button>();
            _chestButton.targetGraphic = _closedImage;
            _chestButton.transition = Selectable.Transition.None;
            _chestButton.onClick.AddListener(() =>
            {
                if (_chestButton.interactable && !IsBusy && !IsShowingRewards) _openRequested?.Invoke();
            });
            _frontEffects = Rect("ChestFrontEffects", _chest);
            for (var i = 0; i < 14; i++)
            {
                var spark = Image("GoldenSpark" + i, _frontEffects, new Color(1f, .86f, .42f, 0));
                Center(spark.rectTransform, new Vector2(i % 3 == 0 ? 8 : 4, i % 3 == 0 ? 8 : 4), Vector2.zero);
                spark.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
                _sparks.Add(spark);
            }
            BuildReceiptPanel();
            if (closed == null || opened == null)
                Debug.LogWarning("Run reward chest sprites are missing. Check RewardChestStates native sprite import.");
        }

        private void BuildReceiptPanel()
        {
            var panelImage = Image("RewardPanel", _safeArea, Color.white);
            NoticeTheme.Panel(panelImage, true);
            _panel = panelImage.rectTransform;
            Center(_panel, new Vector2(1440, 880), Vector2.zero);
            var heading = Text("RewardTitle", _panel, "보상 획득", 54, Gold);
            Top(heading.rectTransform, new Vector2(28, -24), new Vector2(-28, -82));
            _panelRecord = Text("RewardRecord", _panel, "", 34, new Color(.83f, .87f, .94f));
            Top(_panelRecord.rectTransform, new Vector2(28, -83), new Vector2(-28, -123));
            var goldTile = Image("GoldReward", _panel, new Color(.11f, .15f, .22f));
            NoticeTheme.Panel(goldTile, false);
            Top(goldTile.rectTransform, new Vector2(28, -145), new Vector2(-28, -244));
            var goldLabel = Text("GoldLabel", goldTile.transform, "획득 골드", 30, new Color(.8f, .85f, .93f));
            Span(goldLabel.rectTransform, new Vector2(0, 0), new Vector2(.58f, 1), new Vector2(24, 0), new Vector2(-8, 0));
            goldLabel.alignment = TextAnchor.MiddleLeft;
            _goldValue = Text("GoldValue", goldTile.transform, "+0", 52, Gold);
            Span(_goldValue.rectTransform, new Vector2(.58f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-24, 0));
            _goldValue.alignment = TextAnchor.MiddleRight;
            var ticketTile = Image("TicketReward", _panel, Color.white);
            NoticeTheme.Panel(ticketTile, false);
            Top(ticketTile.rectTransform, new Vector2(28, -251), new Vector2(-28, -310));
            _ticketTile = ticketTile.gameObject;
            var ticketLabel = Text("TicketLabel", ticketTile.transform, "획득 티켓", 30, new Color(.8f, .85f, .93f));
            Span(ticketLabel.rectTransform, Vector2.zero, new Vector2(.6f, 1), new Vector2(24, 0), Vector2.zero);
            ticketLabel.alignment = TextAnchor.MiddleLeft;
            _ticketValue = Text("TicketValue", ticketTile.transform, "", 38, Gold);
            Span(_ticketValue.rectTransform, new Vector2(.6f, 0), Vector2.one, Vector2.zero, new Vector2(-24, 0));
            _ticketValue.alignment = TextAnchor.MiddleRight;
            _cardSummary = Text("CardSummary", _panel, "획득 카드", 34, Color.white);
            _cardSummary.alignment = TextAnchor.MiddleLeft;
            var scrollRoot = Rect("RewardList", _panel);
            _scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40;
            var viewport = Image("Viewport", scrollRoot, new Color(.03f, .045f, .07f, .55f));
            Stretch(viewport.rectTransform);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _scroll.viewport = viewport.rectTransform;
            _content = Rect("Content", viewport.transform);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(.5f, 1);
            _content.sizeDelta = Vector2.zero;
            _content.anchoredPosition = Vector2.zero;
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _content;
            var scrollbarImage = Image("Scrollbar", scrollRoot, new Color(.3f, .36f, .46f, .35f));
            Span(scrollbarImage.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-12, 6), new Vector2(-4, -6));
            var handle = Image("Handle", scrollbarImage.transform, new Color(.63f, .69f, .8f));
            Stretch(handle.rectTransform);
            scrollbarImage.raycastTarget = true;
            handle.raycastTarget = true;
            var scrollbar = scrollbarImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            viewport.rectTransform.offsetMax = new Vector2(-20, 0);
            _wallet = Text("Wallet", _panel, "", 28, new Color(.77f, .82f, .89f));
            Span(_wallet.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(28, 106), new Vector2(-28, 144));
            var confirmImage = Image("Confirm", _panel, Color.white);
            Center(confirmImage.rectTransform, new Vector2(360, 82), Vector2.zero);
            confirmImage.rectTransform.anchorMin = confirmImage.rectTransform.anchorMax = new Vector2(.5f, 0);
            confirmImage.rectTransform.anchoredPosition = new Vector2(0, 64);
            _confirmButton = confirmImage.gameObject.AddComponent<Button>();
            var label = Text("Label", confirmImage.transform, "확인", 36, Color.white);
            Stretch(label.rectTransform);
            NoticeTheme.Button(_confirmButton);
            label.fontSize = 36;
            _confirmButton.onClick.AddListener(() =>
            {
                if (!IsShowingRewards || !_confirmButton.interactable) return;
                Hide();
                _confirmed?.Invoke();
            });
            _panel.gameObject.SetActive(false);
        }

        private void ShowReceipt()
        {
            if (_receipt == null) return;
            Phase = RunRewardRevealPhase.Rewards;
            _confirmDelay = .25f;
            _confirmButton.interactable = false;
            _chest.gameObject.SetActive(false);
            _title.gameObject.SetActive(false);
            _record.gameObject.SetActive(false);
            _hint.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            _panelRecord.text = _receipt.RecordText;
            _goldValue.text = _receipt.GoldText;
            _ticketTile.SetActive(_receipt.TicketAmount > 0);
            _ticketValue.text = _receipt.TicketText;
            _cardSummary.text = _receipt.CardSummaryText;
            _wallet.text = _receipt.CurrentGoldText;
            foreach (var row in _rows) { row.SetActive(false); Release(row); }
            _rows.Clear();
            foreach (var row in _receipt.Rows) AddRow(row.Label, "×" + row.Count);
            if (_receipt.Rows.Count == 0) AddRow(string.IsNullOrEmpty(_receipt.EmptyMessage) ? "획득한 카드가 없습니다." : _receipt.EmptyMessage, "");
            LayoutReceiptList();
            MeasureRows();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1;
            RefreshLayout();
        }

        private void AddRow(string name, string count)
        {
            var image = Image("RewardRow", _content, new Color(.12f, .16f, .23f, .8f));
            var size = image.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 72;
            size.preferredHeight = 72;
            var nameText = Text("Name", image.transform, name, 34, Color.white);
            Span(nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-126, -10));
            nameText.alignment = TextAnchor.MiddleLeft;
            var countText = Text("Count", image.transform, count, 36, Gold);
            Span(countText.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-122, 0), new Vector2(-18, 0));
            countText.alignment = TextAnchor.MiddleRight;
            _rows.Add(image.gameObject);
        }

        private void SetChestPose(float progress)
        {
            var reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.13f, .47f, progress));
            _closedImage.color = new Color(1, 1, 1, 1 - reveal);
            _openImage.color = new Color(1, 1, 1, reveal);
            _chest.localScale = Vector3.one * _chestLayoutScale * (1 + .06f * Mathf.Sin(progress * Mathf.PI));
            _chest.localEulerAngles = new Vector3(0, 0, progress < .25f ? Mathf.Sin(progress * 60) * (1 - progress * 4) * 2 : 0);
            _glow.color = new Color(1, .71f, .26f, .18f + .45f * reveal);
            _glow.rectTransform.localScale = Vector3.one * (1 + .4f * reveal);
            for (var i = 0; i < _rays.Count; i++)
            {
                _rays[i].GetComponent<Image>().color = new Color(1, .77f, .38f, .10f * reveal);
                _rays[i].localScale = new Vector3(1, .8f + .4f * reveal, 1);
            }
            for (var i = 0; i < _sparks.Count; i++)
            {
                var t = Mathf.Clamp01((progress - .2f) / .8f);
                var angle = i * 2.39996f;
                _sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * 230 * t, 40 + 210 * t + Mathf.Sin(angle) * 90 * t);
                _sparks[i].color = new Color(1, .86f, .42f, Mathf.Sin(t * Mathf.PI) * .8f);
            }
            _title.gameObject.SetActive(true);
            _record.gameObject.SetActive(true);
            _hint.gameObject.SetActive(true);
        }

        private void RefreshLayout()
        {
            var rootRect = (RectTransform)transform;
            var size = rootRect.rect.size;
            if (size == _lastSize && Screen.safeArea == _lastSafeArea) return;
            _lastSize = size;
            _lastSafeArea = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0 && Screen.safeArea.width > 0)
            {
                _safeArea.anchorMin = new Vector2(Screen.safeArea.xMin / Screen.width, Screen.safeArea.yMin / Screen.height);
                _safeArea.anchorMax = new Vector2(Screen.safeArea.xMax / Screen.width, Screen.safeArea.yMax / Screen.height);
            }
            var safeSize = new Vector2(Mathf.Max(1, size.x * (_safeArea.anchorMax.x - _safeArea.anchorMin.x)),
                Mathf.Max(1, size.y * (_safeArea.anchorMax.y - _safeArea.anchorMin.y)));
            // Fill the safe area instead of keeping the desktop-sized reward window capped at scale 1.
            var scale = Mathf.Min(safeSize.x * .94f / 1440, safeSize.y * .94f / 880);
            _panel.sizeDelta = new Vector2(safeSize.x * .94f / scale, 880);
            _panel.localScale = Vector3.one * scale;
            var chestScale = Mathf.Min(safeSize.x / 1000, safeSize.y / 1100);
            var chestLayoutScale = 1.8f * chestScale;
            // Scale the whole chest, including its light and particles, without losing the animation pulse.
            _chest.localScale *= chestLayoutScale / _chestLayoutScale;
            _chestLayoutScale = chestLayoutScale;
            _chest.anchoredPosition = new Vector2(0, -10 * chestScale);
            // Keep the expanded light and particles inside the safe area at the largest opening pulse.
            var effectRoom = Mathf.Min(safeSize.x * .5f, safeSize.y * .5f - Mathf.Abs(_chest.anchoredPosition.y));
            var effectScale = Mathf.Min(1f, effectRoom / (470f * 1.06f * chestLayoutScale));
            _backEffects.localScale = _frontEffects.localScale = Vector3.one * effectScale;
            _closedImage.rectTransform.offsetMin = _closedImage.rectTransform.offsetMax = new Vector2(-20, 0);
            _openImage.rectTransform.offsetMin = _openImage.rectTransform.offsetMax = new Vector2(11, 0);
            _title.fontSize = Mathf.Max(1, Mathf.RoundToInt(60 * chestScale));
            _record.fontSize = Mathf.Max(1, Mathf.RoundToInt(40 * chestScale));
            _hint.fontSize = Mathf.Max(1, Mathf.RoundToInt(34 * chestScale));
            Center(_title.rectTransform, new Vector2(safeSize.x * .92f, 80 * chestScale), new Vector2(0, safeSize.y * .5f - 70 * chestScale));
            Center(_record.rectTransform, new Vector2(safeSize.x * .92f, 60 * chestScale), new Vector2(0, safeSize.y * .5f - 140 * chestScale));
            Center(_hint.rectTransform, new Vector2(safeSize.x * .92f, 120 * chestScale), new Vector2(0, -safeSize.y * .5f + 90 * chestScale));
            MeasureRows();
        }

        private void MeasureRows()
        {
            foreach (var row in _rows)
            {
                var name = row.transform.Find("Name").GetComponent<Text>();
                var settings = name.GetGenerationSettings(new Vector2(Mathf.Max(240, _panel.rect.width - 250), 0));
                var height = name.cachedTextGeneratorForLayout.GetPreferredHeight(name.text, settings) / name.pixelsPerUnit;
                row.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(72, height + 20);
            }
        }

        private void LayoutReceiptList()
        {
            var top = _receipt.TicketAmount > 0 ? 324 : 266;
            Top(_cardSummary.rectTransform, new Vector2(30, -top), new Vector2(-30, -top - 43));
            Span((RectTransform)_scroll.transform, Vector2.zero, Vector2.one, new Vector2(28, 156), new Vector2(-28, -top - 56));
        }

        private Sprite CreateGlowSprite()
        {
            const int size = 64;
            _glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RewardChestGlow", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) / 32;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 2));
            }
            _glowTexture.SetPixels(pixels);
            _glowTexture.Apply(false, true);
            _glowSprite = Sprite.Create(_glowTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            return _glowSprite;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private static Image Image(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static Text Text(string name, Transform parent, string value, int size, Color color)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            NoticeTheme.Text(text, size);
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.text = value;
            text.supportRichText = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private static void Stretch(RectTransform rect) => Span(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void Span(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
        private static void Top(RectTransform rect, Vector2 min, Vector2 max) =>
            Span(rect, new Vector2(0, 1), Vector2.one, new Vector2(min.x, max.y), new Vector2(max.x, min.y));
        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
    }
}
