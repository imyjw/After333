using System.Globalization;
using Project333.Runtime.Application.Accounts;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class AccountWalletView : MonoBehaviour
    {
        public const string TicketResourcePath = "Project333/UI/ServerTicketIcon128";
        public const string GoldResourcePath = "Project333/UI/ServerGoldIcon128";

        [SerializeField] private bool _vertical;
        [SerializeField] private bool _showAccountName;
        [SerializeField, Min(1f)] private float _iconSize = 48f;
        [SerializeField, Min(0f)] private float _iconNumberGap = 8f;
        [SerializeField] private Sprite _ticketSprite;
        [SerializeField] private Sprite _goldSprite;
        [SerializeField] private Text _accountNameText;
        [SerializeField] private Image _ticketIcon;
        [SerializeField] private Text _ticketValue;
        [SerializeField] private Image _goldIcon;
        [SerializeField] private Text _goldValue;
        private Text _legacyText;
        private bool _building;
        private bool _layoutValid;
        private Vector2 _lastSize;
        private Vector2 _lastIconSettings;
        private bool _lastVertical;
        private bool _lastShowAccountName;

        public Text TicketValue => _ticketValue;
        public Text GoldValue => _goldValue;
        public Text AccountNameText => _accountNameText;
        public float CurrencyAreaFraction => _showAccountName ? 0.62f : 1f;

        public static AccountWalletView ShowSession(Text host, bool vertical = false, string heading = null)
        {
            var view = Attach(host, vertical, heading != null);
            if (view != null)
                view.Render(AccountSessionState.IsAuthenticated, AccountSessionState.Tickets,
                    AccountSessionState.ResourceGold, heading);
            return view;
        }

        public static AccountWalletView Attach(Text host, bool vertical, bool showAccountName)
        {
            if (host == null) return null;
            var view = host.GetComponent<AccountWalletView>() ?? host.gameObject.AddComponent<AccountWalletView>();
            view._vertical = vertical;
            view._showAccountName = showAccountName;
            view.EnsureVisuals();
            view.ApplyLayout();
            return view;
        }

        private void OnEnable()
        {
            _layoutValid = false;
            EnsureVisuals();
            ApplyLayout();
        }

        private void OnValidate()
        {
            _iconSize = Mathf.Max(1f, _iconSize);
            _iconNumberGap = Mathf.Max(0f, _iconNumberGap);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_building) ApplyLayout();
        }

        private void Update()
        {
            if (_legacyText != null && !string.IsNullOrEmpty(_legacyText.text))
                _legacyText.text = string.Empty;
            // Rect anchors respond to screen size; this also previews Inspector size changes.
            ApplyLayout();
        }

        public void Render(bool authenticated, int tickets, long gold, string heading = null)
        {
            EnsureVisuals();
            _legacyText.text = string.Empty;
            if (_accountNameText != null) _accountNameText.text = heading ?? string.Empty;
            _ticketValue.text = authenticated ? tickets.ToString(CultureInfo.InvariantCulture) : "-";
            _goldValue.text = authenticated ? gold.ToString(CultureInfo.InvariantCulture) : "-";
            ApplyLayout();
        }

        private void EnsureVisuals()
        {
            if (_building) return;
            _building = true;
            try
            {
                _legacyText = GetComponent<Text>();
                _legacyText.text = string.Empty;
                _legacyText.raycastTarget = false;
                if (_ticketSprite == null) _ticketSprite = Resources.Load<Sprite>(TicketResourcePath);
                if (_goldSprite == null) _goldSprite = Resources.Load<Sprite>(GoldResourcePath);
                if (_showAccountName && _accountNameText == null)
                    _accountNameText = CreateText("AccountNameText");
                if (_ticketIcon == null) _ticketIcon = CreateIcon("ServerTicketIcon", _ticketSprite);
                if (_ticketValue == null) _ticketValue = CreateText("ServerTicketValue");
                if (_goldIcon == null) _goldIcon = CreateIcon("ServerGoldIcon", _goldSprite);
                if (_goldValue == null) _goldValue = CreateText("ServerGoldValue");
                if (_ticketIcon.sprite == null) _ticketIcon.sprite = _ticketSprite;
                if (_goldIcon.sprite == null) _goldIcon.sprite = _goldSprite;
                _ticketIcon.enabled = _ticketIcon.sprite != null;
                _goldIcon.enabled = _goldIcon.sprite != null;
            }
            finally { _building = false; }
        }

        private Text CreateText(string childName)
        {
            var existing = transform.Find(childName)?.GetComponent<Text>();
            if (existing != null) return existing;
            var child = new GameObject(childName, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(transform, false);
            child.layer = gameObject.layer;
            _layoutValid = false;
            var text = child.GetComponent<Text>();
            text.font = _legacyText.font;
            text.fontSize = _legacyText.fontSize;
            text.fontStyle = _legacyText.fontStyle;
            text.color = _legacyText.color;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 1;
            text.resizeTextMaxSize = Mathf.Max(1, _legacyText.fontSize);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = "-";
            return text;
        }

        private Image CreateIcon(string childName, Sprite sprite)
        {
            var existing = transform.Find(childName)?.GetComponent<Image>();
            if (existing != null) return existing;
            var child = new GameObject(childName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            child.layer = gameObject.layer;
            _layoutValid = false;
            var icon = child.GetComponent<Image>();
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        public void ApplyLayout()
        {
            if (_building || _ticketIcon == null || _goldIcon == null ||
                _ticketValue == null || _goldValue == null) return;
            var rect = (RectTransform)transform;
            var size = rect.rect.size;
            var iconSettings = new Vector2(_iconSize, _iconNumberGap);
            if (_layoutValid && _lastSize == size && _lastIconSettings == iconSettings &&
                _lastVertical == _vertical && _lastShowAccountName == _showAccountName) return;
            _building = true;
            try
            {
                var height = Mathf.Max(0f, rect.rect.height);
                var width = Mathf.Max(0f, rect.rect.width);
                var currencyTop = CurrencyAreaFraction;
                if (_accountNameText != null)
                {
                    _accountNameText.gameObject.SetActive(_showAccountName);
                    Place(_accountNameText.rectTransform, new Vector2(0f, currencyTop), Vector2.one,
                        Vector2.zero, Vector2.zero);
                }
                if (_vertical)
                {
                    LayoutCurrency(_ticketIcon, _ticketValue, 0f, currencyTop / 2f, 1f, currencyTop, width, height);
                    LayoutCurrency(_goldIcon, _goldValue, 0f, 0f, 1f, currencyTop / 2f, width, height);
                }
                else
                {
                    LayoutCurrency(_ticketIcon, _ticketValue, 0f, 0f, 0.48f, currencyTop, width, height);
                    LayoutCurrency(_goldIcon, _goldValue, 0.52f, 0f, 1f, currencyTop, width, height);
                }
                _lastSize = size;
                _lastIconSettings = iconSettings;
                _lastVertical = _vertical;
                _lastShowAccountName = _showAccountName;
                _layoutValid = true;
            }
            finally { _building = false; }
        }

        private void LayoutCurrency(Image icon, Text value, float x0, float y0, float x1, float y1,
            float width, float height)
        {
            var slotWidth = width * (x1 - x0);
            var slotHeight = height * (y1 - y0);
            var size = Mathf.Min(_iconSize, slotHeight, slotWidth * 0.35f);
            var middleY = (y0 + y1) / 2f;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(x0, middleY);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(size, size);
            var gap = Mathf.Min(_iconNumberGap, slotWidth * 0.08f);
            Place(value.rectTransform, new Vector2(x0, y0), new Vector2(x1, y1),
                new Vector2(size + gap, 0f), Vector2.zero);
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
