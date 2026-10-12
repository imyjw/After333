using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed partial class BattleMulliganOverlayPresenter
    {
        [Header("Opening Hand Animation")]
        [SerializeField] private bool _dealOpeningHand = true;
        [SerializeField, Min(0.1f)] private float _openingCardFlightSeconds = 0.48f;
        [SerializeField, Min(0f)] private float _openingCardStaggerSeconds = 0.13f;

        private RectTransform _designRoot;
        private RectTransform _drawSourceRect;
        private Sprite _drawCardBack;
        private Text _instructionText;
        private Text _turnOrderText;
        private RectTransform[] _keepAuras;
        private RectTransform[] _replaceCrosses;
        private Text[] _replaceLabels;
        private CanvasGroup[] _cardVisualGroups;
        private CanvasGroup[] _auraGroups;
        private Image[] _dealGhosts;
        private int[] _dealSlotIndices = Array.Empty<int>();
        private int[] _pendingReplacementSlots = Array.Empty<int>();
        private Vector2[] _dealStarts;
        private Vector2[] _dealTargets;
        private Vector2[] _dealStartSizes;
        private Vector2[] _dealEndSizes;
        private Coroutine _openingDeal;
        private bool _isDealingOpeningHand;
        private bool _isDealingReplacementHand;
        private bool _finalResultPending;
        private bool _designApplied;
        private Vector2 _designCanvasSize;
        private Rect _designSafeArea;
        private int _designCardCount;
        private static Sprite s_frameSprite;

        public bool IsDealingOpeningHand => _isDealingOpeningHand;
        public bool IsDealingReplacementHand => _isDealingReplacementHand;
        private bool IsDealingCards => _isDealingOpeningHand || _isDealingReplacementHand;

        public void ConfigureDrawSource(RectTransform source, Sprite cardBack)
        {
            _drawSourceRect = source;
            _drawCardBack = cardBack;
        }

        // Upgrade the existing serialized UI in place; the authored scene references remain valid.
        public void ApplyPresentationDesign()
        {
            ApplyCanvasSortingOrder();
            if (_canvas == null) return;
            if (_designRoot == null)
                _designRoot = transform.Find("CenterPanel") as RectTransform;
            if (_designRoot == null) return;

            if (!_designApplied)
            {
                var dimmer = transform.Find("Dimmer")?.GetComponent<UnityEngine.UI.Image>();
                if (dimmer != null) dimmer.color = new Color(0.015f, 0.025f, 0.045f, 0.78f);
                var panel = _designRoot.GetComponent<UnityEngine.UI.Image>();
                if (panel != null) { panel.color = Color.clear; panel.raycastTarget = false; }

                var plate = EnsureDesignImage(_designRoot, "HeaderPlate");
                plate.sprite = Resources.Load<Sprite>("Project333/StartScene/PixelMenuButton");
                plate.type = UnityEngine.UI.Image.Type.Sliced;
                plate.color = new Color(0.42f, 0.61f, 0.65f, 0.96f);
                if (_titleText != null) _titleText.transform.SetParent(plate.transform, false);
                _instructionText = EnsureDesignText(_designRoot, "InstructionText", 30);
                _turnOrderText = EnsureDesignText(_designRoot, "TurnOrderBadge", 25);
                _instructionText.text = "교환할 카드 선택";
                _instructionText.color = new Color(1f, 0.86f, 0.46f, 1f);
                _turnOrderText.color = new Color(0.85f, 0.94f, 0.95f, 1f);
                StyleDesignText(_titleText, 48, new Color(1f, 0.93f, 0.70f, 1f));
                StyleDesignText(_statusText, 23, new Color(0.78f, 0.85f, 0.87f, 1f));
                StyleDesignText(_confirmButtonText, 32, new Color(1f, 0.94f, 0.76f, 1f));
                if (_confirmButton != null)
                {
                    var image = _confirmButton.GetComponent<UnityEngine.UI.Image>();
                    if (image != null)
                    {
                        image.sprite = Resources.Load<Sprite>("Project333/StartScene/PixelMenuButton");
                        image.type = UnityEngine.UI.Image.Type.Sliced;
                        image.color = new Color(0.46f, 0.84f, 0.80f, 1f);
                    }
                    var colors = _confirmButton.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1f, 1f, 0.86f, 1f);
                    colors.pressedColor = new Color(0.67f, 0.83f, 0.79f, 1f);
                    colors.selectedColor = colors.normalColor;
                    colors.disabledColor = new Color(0.48f, 0.57f, 0.58f, 0.85f);
                    _confirmButton.colors = colors;
                }

                var count = _cardButtons.Length;
                _keepAuras = new RectTransform[count];
                _replaceCrosses = new RectTransform[count];
                _replaceLabels = new Text[count];
                _cardVisualGroups = new CanvasGroup[count];
                _auraGroups = new CanvasGroup[count];
                for (var i = 0; i < count; i++)
                {
                    var card = _cardButtons[i]?.transform as RectTransform;
                    if (card == null) continue;
                    var layout = card.parent.GetComponent<HorizontalLayoutGroup>();
                    if (layout != null) layout.enabled = false;
                    _cardVisualGroups[i] = card.GetComponent<CanvasGroup>();
                    if (_cardVisualGroups[i] == null) _cardVisualGroups[i] = card.gameObject.AddComponent<CanvasGroup>();
                    var backing = card.GetComponent<UnityEngine.UI.Image>();
                    if (backing != null) backing.color = Color.clear;
                    _cardButtons[i].transition = UnityEngine.UI.Selectable.Transition.None;
                    _keepAuras[i] = EnsureDesignRect(card, "KeepAura");
                    _keepAuras[i].SetAsFirstSibling();
                    _auraGroups[i] = _keepAuras[i].GetComponent<CanvasGroup>();
                    if (_auraGroups[i] == null) _auraGroups[i] = _keepAuras[i].gameObject.AddComponent<CanvasGroup>();
                    _auraGroups[i].blocksRaycasts = false;
                    for (var layer = 0; layer < 7; layer++)
                    {
                        var glow = EnsureDesignImage(_keepAuras[i], "Glow_" + layer);
                        glow.sprite = FrameSprite;
                        glow.type = UnityEngine.UI.Image.Type.Sliced;
                        glow.color = layer == 6
                            ? new Color(0.60f, 1f, 0.34f, 0.92f)
                            : new Color(0.24f, 1f, 0.14f, 0.04f + layer * 0.035f);
                        StretchDesignRect(glow.rectTransform, -(layer == 6 ? 2f : 18f - layer * 3f));
                    }
                    var overlay = GetAt(_selectionOverlays, i);
                    if (overlay != null) overlay.color = new Color(0.19f, 0.015f, 0.02f, 0.14f);
                    _replaceCrosses[i] = EnsureDesignRect(card, "ReplaceCross");
                    var replaceMark = EnsureDesignImage(_replaceCrosses[i], "ReplaceMark");
                    replaceMark.sprite = Resources.Load<Sprite>("Project333/UI/MulliganReplaceMark");
                    replaceMark.preserveAspect = true;
                    replaceMark.color = Color.white;
                    replaceMark.gameObject.SetActive(replaceMark.sprite != null);
                    // Keep the old geometry solely as an asset-missing fallback.
                    for (var line = 0; line < 2; line++)
                    {
                        var shadow = EnsureDesignImage(_replaceCrosses[i], "Shadow_" + line);
                        var stroke = EnsureDesignImage(_replaceCrosses[i], "Stroke_" + line);
                        shadow.color = new Color(0.24f, 0.015f, 0.018f, 0.93f);
                        stroke.color = new Color(0.98f, 0.16f, 0.13f, 0.98f);
                        shadow.gameObject.SetActive(replaceMark.sprite == null);
                        stroke.gameObject.SetActive(replaceMark.sprite == null);
                    }
                    _replaceCrosses[i].Find("Shadow_0").SetSiblingIndex(0);
                    _replaceCrosses[i].Find("Shadow_1").SetSiblingIndex(1);
                    replaceMark.transform.SetAsLastSibling();

                    _replaceLabels[i] = EnsureDesignText(card, "ReplaceLabel", 28);
                    _replaceLabels[i].text = "교체";
                    _replaceLabels[i].color = new Color(1f, 0.43f, 0.36f, 1f);
                    _replaceCrosses[i].SetAsLastSibling();
                    _replaceLabels[i].transform.SetAsLastSibling();
                }
                _designApplied = true;
            }
            RefreshPresentationLayout(force: true);
            RefreshSelectionVisuals();
        }

        private void RefreshPresentationLayout(bool force = false)
        {
            if (!_designApplied || _canvas == null || _designRoot == null) return;
            var canvasRect = _canvas.transform as RectTransform;
            var size = canvasRect.rect.size;
            var safe = Screen.safeArea;
            var count = Mathf.Max(1, Mathf.Min(_cardButtons.Length, _displayCardIds.Count > 0 ? _displayCardIds.Count : 3));
            if (!force && size == _designCanvasSize && safe == _designSafeArea && count == _designCardCount) return;
            if (IsDealingCards) CancelOpeningDeal();
            _designCanvasSize = size;
            _designSafeArea = safe;
            _designCardCount = count;
            var screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            _designRoot.anchorMin = new Vector2(safe.xMin / screenSize.x, safe.yMin / screenSize.y);
            _designRoot.anchorMax = new Vector2(safe.xMax / screenSize.x, safe.yMax / screenSize.y);
            _designRoot.pivot = new Vector2(0.5f, 0.5f);
            _designRoot.offsetMin = new Vector2(30f, 20f);
            _designRoot.offsetMax = new Vector2(-30f, -20f);
            var available = _designRoot.rect.size;
            var scale = Mathf.Clamp(available.y / 980f, 0.55f, 1f);
            SetDesignRect(_designRoot.Find("HeaderPlate") as RectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140f * scale), new Vector2(640f, 100f) * scale);
            if (_titleText != null) StretchDesignRect(_titleText.rectTransform, 10f);
            SetDesignRect(_instructionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -220f * scale), new Vector2(1000f, 48f) * scale);
            SetDesignRect(_turnOrderText.rectTransform, new Vector2(1f, 1f), new Vector2(-115f, -65f) * scale, new Vector2(180f, 42f) * scale);
            if (_statusText != null) SetDesignRect(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 155f * scale), new Vector2(Mathf.Min(available.x - 40f, 1260f), 42f * scale));
            if (_confirmButton != null) SetDesignRect(_confirmButton.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0, 82f * scale), new Vector2(300f, 86f) * scale);
            if (_confirmButtonText != null) StretchDesignRect(_confirmButtonText.rectTransform, 10f);
            // Space the visible hand, preserving unused authored card slots.
            var spacing = 68f * scale;
            var height = Mathf.Min(530f * scale, available.y - 380f * scale);
            var width = Mathf.Min(height * (2f / 3f), (available.x - (count - 1) * spacing - 130f * scale) / count);
            height = Mathf.Min(height, width * 1.5f);
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                var card = _cardButtons[i]?.transform as RectTransform;
                if (card == null) continue;
                var row = card.parent as RectTransform;
                SetDesignRect(row, new Vector2(0.5f, 0.5f), new Vector2(0, -5f * scale), new Vector2(available.x, height));
                SetDesignRect(card, new Vector2(0.5f, 0.5f), new Vector2((i - (count - 1) * 0.5f) * (width + spacing), 0), new Vector2(width, height));
            }
            if (_titleText != null) _titleText.fontSize = Mathf.RoundToInt(48f * scale);
            _instructionText.fontSize = Mathf.RoundToInt(30f * scale);
            _turnOrderText.fontSize = Mathf.RoundToInt(25f * scale);
            if (_statusText != null) _statusText.fontSize = Mathf.RoundToInt(23f * scale);
            if (_confirmButtonText != null) _confirmButtonText.fontSize = Mathf.RoundToInt(32f * scale);
            Canvas.ForceUpdateCanvases();
            RefreshDecorationLayouts();
            if (_sessionActive) SetSelectionInteractable(!_confirmationSent && !_hasReceivedConfirmedHand);
        }

        private void RefreshDecorationLayouts()
        {
            if (!_designApplied) return;
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                var artwork = GetAt(_cardArtworkImages, i);
                if (artwork == null || _keepAuras[i] == null) continue;
                var rendered = Hand.HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork);
                var parent = _cardButtons[i].transform as RectTransform;
                var center = (Vector2)parent.InverseTransformPoint(artwork.rectTransform.TransformPoint(rendered.center));
                SetDesignRect(_keepAuras[i], new Vector2(0.5f, 0.5f), center, rendered.size);
                SetDesignRect(_replaceCrosses[i], new Vector2(0.5f, 0.5f), center, rendered.size);
                var mark = _replaceCrosses[i].Find("ReplaceMark") as RectTransform;
                SetDesignRect(mark, new Vector2(0.5f, 0.5f), Vector2.zero, rendered.size * 0.90f);
                var dx = rendered.width * 0.74f;
                var dy = rendered.height * 0.74f;
                var length = Mathf.Sqrt(dx * dx + dy * dy);
                var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                var thickness = Mathf.Max(12f, rendered.width * 0.058f);
                for (var line = 0; line < 2; line++)
                {
                    var shadow = _replaceCrosses[i].Find("Shadow_" + line) as RectTransform;
                    var stroke = _replaceCrosses[i].Find("Stroke_" + line) as RectTransform;
                    SetDesignRect(shadow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(length + 6f, thickness + 8f));
                    SetDesignRect(stroke, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(length, thickness));
                    shadow.localRotation = stroke.localRotation = Quaternion.Euler(0, 0, line == 0 ? angle : -angle);
                }
                SetDesignRect(_replaceLabels[i].rectTransform, new Vector2(0.5f, 0.5f), center + new Vector2(0, -rendered.height * 0.5f - 32f), new Vector2(rendered.width, 42f));
            }
        }

        private void RefreshDesignSelection(int slot, bool hasCard, bool selected)
        {
            if (!_designApplied || slot >= _keepAuras.Length) return;
            var choosing = !_hasReceivedConfirmedHand && !_isShowingFinalResult;
            if (_keepAuras[slot] != null) _keepAuras[slot].gameObject.SetActive(hasCard && choosing && !selected && !_confirmationSent);
            if (_replaceCrosses[slot] != null) _replaceCrosses[slot].gameObject.SetActive(hasCard && choosing && selected);
            if (_replaceLabels[slot] != null) _replaceLabels[slot].gameObject.SetActive(hasCard && choosing && selected);
            if (_turnOrderText != null) _turnOrderText.text = _isLocalFirstPlayer ? "선공" : "후공";
            if (_instructionText != null) _instructionText.text = _hasReceivedConfirmedHand ? (_isShowingFinalResult ? "전투 준비 완료" : "교환 완료") : "교환할 카드 선택";
        }

        private void AnimateKeepAuras()
        {
            if (!_designApplied || !_sessionActive || IsDealingCards) return;
            var alpha = 0.84f + Mathf.Sin(Time.unscaledTime * 2.7f) * 0.13f;
            for (var i = 0; i < _auraGroups.Length; i++) if (_auraGroups[i] != null) _auraGroups[i].alpha = alpha;
        }

        private void HidePendingReplacementSlots()
        {
            var slots = new List<int>(_selectedSlotIndices.Count);
            for (var slot = 0; slot < _openingHandCardIds.Count; slot++)
                if (_selectedSlotIndices.Contains(slot)) slots.Add(slot);
            _pendingReplacementSlots = slots.ToArray();
            ApplyPendingReplacementVisibility();
        }

        private void ApplyPendingReplacementVisibility()
        {
            if (!_confirmationSent || _hasReceivedConfirmedHand || _cardVisualGroups == null) return;
            foreach (var slot in _pendingReplacementSlots)
            {
                if (slot >= 0 && slot < _cardVisualGroups.Length && _cardVisualGroups[slot] != null)
                    _cardVisualGroups[slot].alpha = 0f;
            }
        }

        private void RestorePendingReplacementSlots()
        {
            _pendingReplacementSlots = Array.Empty<int>();
            if (_cardVisualGroups != null)
                foreach (var group in _cardVisualGroups) if (group != null) group.alpha = 1f;
        }

        private void StartOpeningDeal()
        {
            if (!_dealOpeningHand || _openingHandCardIds.Count == 0) return;
            var slots = new int[_openingHandCardIds.Count];
            for (var i = 0; i < slots.Length; i++) slots[i] = i;
            StartCardDeal(slots, replacement: false);
        }

        private void StartCardDeal(IReadOnlyList<int> slots, bool replacement)
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || !_designApplied || slots == null || slots.Count == 0) return;
            CancelOpeningDeal();
            Canvas.ForceUpdateCanvases();
            RefreshPresentationLayout();
            var count = slots.Count;
            _dealGhosts = new Image[count];
            _dealSlotIndices = new int[count];
            _dealStarts = new Vector2[count]; _dealTargets = new Vector2[count];
            _dealStartSizes = new Vector2[count]; _dealEndSizes = new Vector2[count];
            var canvasRect = _canvas.transform as RectTransform;
            var targetCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            for (var i = 0; i < count; i++)
            {
                var slot = slots[i];
                _dealSlotIndices[i] = slot;
                if (slot < 0 || slot >= _cardButtons.Length || _cardButtons[slot] == null) continue;
                var artwork = GetAt(_cardArtworkImages, slot);
                var target = artwork != null ? artwork.rectTransform : _cardButtons[slot].transform as RectTransform;
                var rendered = artwork != null ? Hand.HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork) : target.rect;
                var targetScreen = RectTransformUtility.WorldToScreenPoint(targetCamera, target.TransformPoint(rendered.center));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, targetCamera, out _dealTargets[i]);
                var sourceScreen = new Vector2(Screen.safeArea.xMax - 75f, Screen.safeArea.yMin + 95f);
                var startSize = new Vector2(100f, 133f);
                if (_drawSourceRect != null)
                {
                    var sourceCanvas = _drawSourceRect.GetComponentInParent<Canvas>();
                    var sourceCamera = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
                    sourceScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, _drawSourceRect.TransformPoint(_drawSourceRect.rect.center));
                    var minScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, _drawSourceRect.TransformPoint(_drawSourceRect.rect.min));
                    var maxScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, _drawSourceRect.TransformPoint(_drawSourceRect.rect.max));
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, minScreen, targetCamera, out var minLocal);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, maxScreen, targetCamera, out var maxLocal);
                    startSize = new Vector2(Mathf.Abs(maxLocal.x - minLocal.x), Mathf.Abs(maxLocal.y - minLocal.y));
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sourceScreen, targetCamera, out _dealStarts[i]);
                _dealStartSizes[i] = startSize;
                _dealEndSizes[i] = rendered.size * (Vector2)target.lossyScale / (Vector2)canvasRect.lossyScale;
                var ghost = EnsureDesignImage(canvasRect, "DrawingCard_" + i.ToString("00"));
                ghost.sprite = _drawCardBack != null ? _drawCardBack : Resources.Load<Sprite>("Project333/CardArtwork/OpponentCardBack");
                ghost.preserveAspect = true;
                ghost.color = Color.white;
                ghost.transform.SetAsLastSibling();
                _dealGhosts[i] = ghost;
                if (_cardVisualGroups[slot] != null) _cardVisualGroups[slot].alpha = 0f;
            }
            _isDealingOpeningHand = !replacement;
            _isDealingReplacementHand = replacement;
            SetSelectionInteractable(false);
            SetStatus(replacement ? string.Empty : "시작 카드를 뽑는 중...");
            _openingDeal = StartCoroutine(DealOpeningHand());
        }

        private IEnumerator DealOpeningHand()
        {
            var elapsed = 0f;
            while (!UpdateOpeningDeal(elapsed))
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            var replacement = _isDealingReplacementHand;
            _openingDeal = null;
            CancelOpeningDeal();
            if (!_sessionActive) yield break;
            if (replacement && !_isShowingFinalResult)
                SetStatus("상대의 멀리건을 기다리는 중...");
            if (!_confirmationSent && !_hasReceivedConfirmedHand)
            {
                SetSelectionInteractable(true);
                UpdateSelectionStatus();
            }
            RefreshSelectionVisuals();
        }

        private bool UpdateOpeningDeal(float elapsedSeconds)
        {
            if (!IsDealingCards || _dealGhosts == null) return true;
            var allLanded = true;
            for (var i = 0; i < _dealGhosts.Length; i++)
            {
                var ghost = _dealGhosts[i];
                if (ghost == null) continue;
                var slot = _dealSlotIndices[i];
                var localTime = elapsedSeconds - i * Mathf.Max(0f, _openingCardStaggerSeconds);
                var t = Mathf.Clamp01(localTime / Mathf.Max(0.1f, _openingCardFlightSeconds));
                if (t < 1f) allLanded = false;
                ghost.gameObject.SetActive(localTime >= 0f && t < 1f);
                if (t >= 1f) { if (_cardVisualGroups[slot] != null) _cardVisualGroups[slot].alpha = 1f; continue; }
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                var position = Vector2.Lerp(_dealStarts[i], _dealTargets[i], ease);
                position.y += Mathf.Sin(t * Mathf.PI) * 90f;
                SetDesignRect(ghost.rectTransform, new Vector2(0.5f, 0.5f), position, Vector2.Lerp(_dealStartSizes[i], _dealEndSizes[i], ease));
                ghost.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-18f, 0f, ease));
                var flip = Mathf.InverseLerp(0.48f, 0.82f, t);
                ghost.rectTransform.localScale = new Vector3(Mathf.Max(0.035f, Mathf.Abs(1f - flip * 2f)), 1f, 1f);
                if (flip >= 0.5f) ghost.sprite = GetAt(_cardArtworkImages, slot)?.sprite;
            }
            return allLanded;
        }

        private void CancelOpeningDeal()
        {
            CancelCardDeal(finishPendingFinalResult: true);
        }

        private void CancelCardDeal(bool finishPendingFinalResult)
        {
            var wasDealing = IsDealingCards;
            var wasReplacement = _isDealingReplacementHand;
            if (_openingDeal != null) { StopCoroutine(_openingDeal); _openingDeal = null; }
            _isDealingOpeningHand = false;
            _isDealingReplacementHand = false;
            if (_cardVisualGroups != null) foreach (var group in _cardVisualGroups) if (group != null) group.alpha = 1f;
            if (_dealGhosts != null)
            {
                foreach (var ghost in _dealGhosts)
                {
                    if (ghost == null) continue;
                    ghost.gameObject.SetActive(false);
                    if (UnityEngine.Application.isPlaying) Destroy(ghost.gameObject);
                    else DestroyImmediate(ghost.gameObject);
                }
            }
            _dealGhosts = null;
            _dealSlotIndices = Array.Empty<int>();
            if (wasDealing && _sessionActive && !_confirmationSent && !_hasReceivedConfirmedHand && !_isShowingFinalResult)
                UpdateSelectionStatus();
            if (wasReplacement && _sessionActive && !_finalResultPending && !_isShowingFinalResult)
                SetStatus("상대의 멀리건을 기다리는 중...");
            if (finishPendingFinalResult && _finalResultPending && isActiveAndEnabled)
                BeginFinalResultDisplay();
        }

        private void OnDisable()
        {
            CancelCardDeal(finishPendingFinalResult: false);
            if (_resultSequence != null)
            {
                StopCoroutine(_resultSequence);
                _resultSequence = null;
                _isShowingFinalResult = false;
                _finalResultPending = _sessionActive && _hasReceivedConfirmedHand;
            }
        }

        private static Image EnsureDesignImage(RectTransform parent, string name)
        {
            var found = parent.Find(name);
            var image = found == null ? null : found.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                var child = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(parent, false);
                child.layer = parent.gameObject.layer;
                image = child.GetComponent<UnityEngine.UI.Image>();
                if (image == null) image = child.AddComponent<UnityEngine.UI.Image>();
            }
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform EnsureDesignRect(RectTransform parent, string name)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Text EnsureDesignText(RectTransform parent, string name, int size)
        {
            var rect = EnsureDesignRect(parent, name);
            var text = rect.GetComponent<UnityEngine.UI.Text>();
            if (text == null) text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            StyleDesignText(text, size, Color.white);
            return text;
        }

        private static void StyleDesignText(Text text, int size, Color color)
        {
            if (text == null) return;
            text.font = ResolveKoreanFont(); text.fontSize = size; text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
            text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = text.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.015f, 0.025f, 0.035f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void SetDesignRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static void StretchDesignRect(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite FrameSprite
        {
            get
            {
                if (s_frameSprite != null) return s_frameSprite;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "MulliganPixelFrame", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                {
                    var outer = x + y >= 3 && (size - 1 - x) + y >= 3 && x + (size - 1 - y) >= 3 && (size - 1 - x) + (size - 1 - y) >= 3;
                    var edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    pixels[y * size + x] = outer && edge ? Color.white : Color.clear;
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                s_frameSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
                return s_frameSprite;
            }
        }
    }
}
