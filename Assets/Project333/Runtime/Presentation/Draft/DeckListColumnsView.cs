using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    // Real UI columns keep costs pinned to the right without padding card names with spaces.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class DeckListColumnsView : MonoBehaviour
    {
        private sealed class Row
        {
            public string CardId;
            public RectTransform Rect;
            public Text Name;
            public Text Cost;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Text _source;
        private bool _layingOut;
        public bool HasRows => _rows.Count > 0;
        public float ContentHeight { get; private set; }

        public bool Configure(Text source, IReadOnlyList<string> cardIds,
            Func<string, CardDefinitionAsset> resolver, string emptyText)
        {
            _source = source;
            var values = new List<Tuple<string, string, string>>();
            DraftDeckListFormatter.BuildNameAndCostRows(cardIds, resolver, emptyText,
                (id, name, cost) => values.Add(Tuple.Create(id, name, cost)));
            var unchanged = _rows.Count == values.Count;
            for (var i = 0; unchanged && i < values.Count; i++)
                unchanged = _rows[i].CardId == values[i].Item1 &&
                    _rows[i].Name.text == values[i].Item2 && _rows[i].Cost.text == values[i].Item3;
            if (!unchanged)
            {
                Clear();
                foreach (var value in values)
                {
                    var rowObject = new GameObject("DeckCardRow", typeof(RectTransform), typeof(Image));
                    rowObject.transform.SetParent(transform, false);
                    var image = rowObject.GetComponent<Image>();
                    image.color = Color.clear;
                    image.raycastTarget = true;
                    var row = new Row { CardId = value.Item1, Rect = (RectTransform)rowObject.transform };
                    row.Name = CreateText(row.Rect, "CardName", value.Item2, TextAnchor.UpperLeft);
                    row.Cost = CreateText(row.Rect, "CardCost", value.Item3, TextAnchor.UpperRight);
                    _rows.Add(row);
                }
            }
            source.text = HasRows ? string.Empty : emptyText;
            RefreshLayout();
            return !unchanged;
        }

        private Text CreateText(RectTransform parent, string name, string value, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.text = value;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void CopyStyle(Text target)
        {
            target.font = _source.font;
            target.fontSize = _source.fontSize;
            target.fontStyle = _source.fontStyle;
            target.lineSpacing = _source.lineSpacing;
            target.color = _source.color;
            target.resizeTextForBestFit = false;
        }

        public void RefreshLayout()
        {
            if (_source == null || _layingOut) return;
            _layingOut = true;
            try
            {
                var width = Mathf.Max(1f, _source.rectTransform.rect.width);
                var costWidth = 0f;
                foreach (var row in _rows)
                {
                    CopyStyle(row.Name);
                    CopyStyle(row.Cost);
                    costWidth = Mathf.Max(costWidth, row.Cost.preferredWidth);
                }
                costWidth = Mathf.Min(width - 1f, Mathf.Ceil(costWidth) + 2f);
                var gap = Mathf.Min(12f, width * .05f);
                var top = 0f;
                foreach (var row in _rows)
                {
                    row.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
                    row.Cost.horizontalOverflow = HorizontalWrapMode.Overflow;
                    Position(row.Name.rectTransform, 0f, 0f, Mathf.Max(1f, width - costWidth - gap), 1f);
                    var height = Mathf.Ceil(Mathf.Max(row.Name.preferredHeight, row.Cost.preferredHeight));
                    Position(row.Rect, 0f, -top, width, height);
                    Position(row.Name.rectTransform, 0f, 0f, Mathf.Max(1f, width - costWidth - gap), height);
                    Position(row.Cost.rectTransform, width - costWidth, 0f, costWidth, height);
                    top += height + 4f;
                }
                ContentHeight = Mathf.Max(0f, top - 4f);
            }
            finally { _layingOut = false; }
        }

        private static void Position(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void OnRectTransformDimensionsChange() => RefreshLayout();

        public bool TryGetCardBounds(string cardId, out Rect bounds)
        {
            foreach (var row in _rows)
                if (string.Equals(row.CardId, cardId, StringComparison.Ordinal))
                {
                    bounds = Bounds(row);
                    return true;
                }
            bounds = default;
            return false;
        }

        public bool TryGetCardAtPoint(Vector2 point, out string cardId)
        {
            foreach (var row in _rows)
                if (Bounds(row).Contains(point)) { cardId = row.CardId; return true; }
            cardId = null;
            return false;
        }

        private Rect Bounds(Row row) => new Rect(
            _source.rectTransform.rect.xMin,
            _source.rectTransform.rect.yMax + row.Rect.anchoredPosition.y - row.Rect.rect.height,
            row.Rect.rect.width, row.Rect.rect.height);

        public void Clear()
        {
            foreach (var row in _rows)
            {
                row.Rect.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(row.Rect.gameObject);
                else DestroyImmediate(row.Rect.gameObject);
            }
            _rows.Clear();
            ContentHeight = 0f;
        }
    }
}
