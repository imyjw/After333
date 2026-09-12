using UnityEngine;

namespace Project333.Runtime.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AccountWalletView))]
    public sealed class AccountWalletHeaderLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform _backButton;
        [SerializeField] private RectTransform _title;
        [SerializeField, Min(0f)] private float _buttonGap = 24f;
        [SerializeField, Min(1f)] private float _preferredWidth = 360f;
        [SerializeField, Min(1f)] private float _preferredHeight = 80f;
        [SerializeField, Min(0f)] private float _titleGap = 24f;
        [SerializeField, Min(0f)] private float _verticalPadding = 8f;
        private readonly Vector3[] _corners = new Vector3[4];
        private AccountWalletView _wallet;

        public void Configure(RectTransform backButton, RectTransform title)
        {
            _backButton = backButton;
            _title = title;
            ApplyLayout();
        }

        private void OnEnable() => ApplyLayout();
        private void Update() => ApplyLayout();

        public void ApplyLayout()
        {
            if (_backButton == null || !(transform.parent is RectTransform parent)) return;
            if (_wallet == null) _wallet = GetComponent<AccountWalletView>();
            var rect = (RectTransform)transform;
            var buttonBounds = GetBoundsInParent(_backButton, parent);
            var left = parent.rect.xMin + Mathf.Max(0f, _titleGap);
            if (_title != null)
                left = Mathf.Max(left, GetBoundsInParent(_title, parent).xMax + Mathf.Max(0f, _titleGap));
            var right = buttonBounds.xMin - Mathf.Max(0f, _buttonGap);
            var width = Mathf.Min(Mathf.Max(1f, _preferredWidth), Mathf.Max(0f, right - left));
            var padding = Mathf.Min(Mathf.Max(0f, _verticalPadding), parent.rect.height / 2f);
            var height = Mathf.Min(Mathf.Max(1f, _preferredHeight), Mathf.Max(0f, parent.rect.height - padding * 2f));

            // Keep the currency row on the button's centerline, with the account name above it.
            var centerY = buttonBounds.center.y + (1f - _wallet.CurrencyAreaFraction) * height / 2f;
            centerY = Mathf.Clamp(centerY, parent.rect.yMin + padding + height / 2f,
                parent.rect.yMax - padding - height / 2f);
            var anchor = new Vector2(0f, 0.5f);
            var position = new Vector2(right - parent.rect.xMin, centerY - parent.rect.center.y);
            var size = new Vector2(width, height);
            var pivot = new Vector2(1f, 0.5f);
            if (rect.anchorMin != anchor) rect.anchorMin = anchor;
            if (rect.anchorMax != anchor) rect.anchorMax = anchor;
            if (rect.pivot != pivot) rect.pivot = pivot;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            _wallet.ApplyLayout();
        }

        private Rect GetBoundsInParent(RectTransform target, RectTransform parent)
        {
            target.GetWorldCorners(_corners);
            var first = parent.InverseTransformPoint(_corners[0]);
            var min = new Vector2(first.x, first.y);
            var max = min;
            for (var i = 1; i < _corners.Length; i++)
            {
                var point = parent.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, new Vector2(point.x, point.y));
                max = Vector2.Max(max, new Vector2(point.x, point.y));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

