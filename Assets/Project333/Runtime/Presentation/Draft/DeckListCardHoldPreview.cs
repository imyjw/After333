using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class DeckListCardHoldPreview : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IPointerExitHandler, IPointerMoveHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float HoldSeconds = 0.4f;
        private readonly List<CardTextRange> _entries = new List<CardTextRange>();
        private Text _text;
        private ScrollRect _scrollRect;
        private Func<string, CardDefinitionAsset> _resolver;
        private string _configuredText;
        private PointerEventData _heldPointer;
        private string _heldCardId;
        private Vector2 _pressPosition;
        private float _pressedAt;
        private bool _previewShown;

        public static void Configure(Text text, IReadOnlyList<string> cardIds,
            Func<string, CardDefinitionAsset> resolver, string emptyText,
            string linePrefix = "", bool includeSummary = false)
        {
            if (text == null) return;
            var relay = text.GetComponent<DeckListCardHoldPreview>();
            if (relay == null) relay = text.gameObject.AddComponent<DeckListCardHoldPreview>();
            relay._text = text;
            relay._resolver = resolver;
            var nextEntries = new List<CardTextRange>();
            Action<string, int, int> record = (cardId, start, length) =>
                nextEntries.Add(new CardTextRange(cardId, start, length));
            var nextText = includeSummary
                ? DraftDeckListFormatter.BuildDeckBuildingPanelText(cardIds, resolver, emptyText, record)
                : DraftDeckListFormatter.BuildDeckListText(cardIds, resolver, emptyText, linePrefix,
                    onEntry: record);
            var unchanged = string.Equals(relay._configuredText, nextText, StringComparison.Ordinal) &&
                string.Equals(text.text, nextText, StringComparison.Ordinal) &&
                relay._entries.Count == nextEntries.Count;
            for (var i = 0; unchanged && i < nextEntries.Count; i++)
                unchanged = relay._entries[i].Start == nextEntries[i].Start &&
                    relay._entries[i].End == nextEntries[i].End &&
                    string.Equals(relay._entries[i].CardId, nextEntries[i].CardId, StringComparison.Ordinal);
            if (!unchanged) relay.CancelHold();
            relay._entries.Clear();
            relay._entries.AddRange(nextEntries);
            // A card name is literal text; formatter offsets must address exactly the rendered string.
            text.supportRichText = false;
            text.raycastTarget = true;
            text.text = nextText;
            relay._configuredText = text.text;
            relay.BindScrollRect();
        }

        private void OnEnable()
        {
            _text = GetComponent<Text>();
            BindScrollRect();
        }

        private void BindScrollRect()
        {
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrolled);
            _scrollRect = GetComponentInParent<ScrollRect>();
            if (_scrollRect != null && isActiveAndEnabled)
                _scrollRect.onValueChanged.AddListener(OnScrolled);
        }

        private void Update() => AdvanceHold(Time.unscaledTime);

        private void AdvanceHold(float now)
        {
            if (_heldPointer == null) return;
            if (HasMoved(_heldPointer.position) ||
                !TryGetCardAtScreenPoint(_heldPointer.position, _heldPointer.pressEventCamera, out var cardId) ||
                !string.Equals(cardId, _heldCardId, StringComparison.Ordinal))
            {
                CancelHold();
                return;
            }
            if (_previewShown || now - _pressedAt < HoldSeconds) return;
            var asset = _resolver?.Invoke(_heldCardId);
            DeckCardPreviewOverlay.ShowFor(this, _heldCardId, asset, _text.font);
            _previewShown = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                _heldPointer != null || !isActiveAndEnabled ||
                !TryGetCardAtScreenPoint(eventData.position, eventData.pressEventCamera, out var cardId)) return;
            _heldPointer = eventData;
            _heldCardId = cardId;
            _pressPosition = eventData.position;
            _pressedAt = Time.unscaledTime;
            _previewShown = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (OwnsPointer(eventData)) CancelHold();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (OwnsPointer(eventData)) CancelHold();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (OwnsPointer(eventData) && HasMoved(eventData.position)) CancelHold();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (_scrollRect != null) _scrollRect.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (OwnsPointer(eventData)) CancelHold();
            if (_scrollRect != null) _scrollRect.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (OwnsPointer(eventData)) CancelHold();
            if (_scrollRect != null) _scrollRect.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (OwnsPointer(eventData)) CancelHold();
            if (_scrollRect != null) _scrollRect.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            CancelHold();
            if (_scrollRect != null) _scrollRect.OnScroll(eventData);
        }

        private bool OwnsPointer(PointerEventData eventData) => _heldPointer != null &&
            eventData != null && eventData.pointerId == _heldPointer.pointerId;

        private bool HasMoved(Vector2 position)
        {
            var threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            return (position - _pressPosition).sqrMagnitude > Mathf.Max(1, threshold * threshold);
        }

        private void OnScrolled(Vector2 _) => CancelHold();
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) CancelHold(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelHold(); }

        private void OnDisable()
        {
            CancelHold();
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrolled);
        }

        public void HidePreview() => CancelHold();

        private void CancelHold()
        {
            DeckCardPreviewOverlay.HideFor(this);
            _heldPointer = null;
            _heldCardId = null;
            _previewShown = false;
        }

        private bool TryGetCardAtScreenPoint(Vector2 point, Camera eventCamera, out string cardId)
        {
            cardId = null;
            if (_text == null || _text.font == null || _entries.Count == 0 ||
                !string.Equals(_text.text, _configuredText, StringComparison.Ordinal) ||
                !RectTransformUtility.RectangleContainsScreenPoint(_text.rectTransform, point, eventCamera))
                return false;

            foreach (var group in GetComponentsInParent<CanvasGroup>())
                if (group.isActiveAndEnabled && (group.alpha <= 0.001f || !group.interactable)) return false;

            // The content Text extends beyond its ScrollRect. Only its actually visible area is selectable.
            foreach (var mask in GetComponentsInParent<RectMask2D>())
                if (mask.isActiveAndEnabled && !RectTransformUtility.RectangleContainsScreenPoint(
                    mask.rectTransform, point, eventCamera)) return false;
            foreach (var mask in GetComponentsInParent<Mask>())
                if (mask.isActiveAndEnabled && !RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)mask.transform, point, eventCamera)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _text.rectTransform, point, eventCamera, out var local)) return false;

            var generator = _text.cachedTextGenerator;
            generator.Populate(_text.text, _text.GetGenerationSettings(_text.rectTransform.rect.size));
            var pixelsPerUnit = Mathf.Max(0.001f, _text.pixelsPerUnit);
            var position = local * pixelsPerUnit;
            var lines = generator.lines;
            var characters = generator.characters;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                if (position.y > line.topY || position.y < line.topY - line.height) continue;
                var start = line.startCharIdx;
                var end = lineIndex + 1 < lines.Count ? lines[lineIndex + 1].startCharIdx : characters.Count;
                end = Mathf.Min(end, Mathf.Min(characters.Count, _configuredText.Length));
                foreach (var entry in _entries)
                {
                    var first = Mathf.Max(start, entry.Start);
                    var last = Mathf.Min(end, entry.End);
                    var left = float.PositiveInfinity;
                    var right = float.NegativeInfinity;
                    for (var i = first; i < last; i++)
                    {
                        if (char.IsWhiteSpace(_configuredText[i])) continue;
                        var character = characters[i];
                        left = Mathf.Min(left, character.cursorPos.x);
                        right = Mathf.Max(right, character.cursorPos.x + character.charWidth);
                    }
                    if (position.x < left || position.x > right) continue;
                    cardId = entry.CardId;
                    return true;
                }
                return false;
            }
            return false;
        }

        private readonly struct CardTextRange
        {
            public CardTextRange(string cardId, int start, int length)
            {
                CardId = cardId;
                Start = start;
                End = start + length;
            }
            public string CardId { get; }
            public int Start { get; }
            public int End { get; }
        }
    }
}
