using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Startup
{
    [DisallowMultipleComponent]
    public sealed class AccountLoginGateView : MonoBehaviour
    {
        [SerializeField] private Image _backdrop;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private InputField _gameIdInput;
        [SerializeField] private InputField _passwordInput;
        [SerializeField] private InputField _displayNameInput;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Text _loginButtonLabel;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Text _registerButtonLabel;
        [SerializeField] private Button _googleButton;
        [SerializeField] private Text _googleButtonLabel;

        public string GameId => _gameIdInput == null ? string.Empty : _gameIdInput.text.Trim();
        public string Password => _passwordInput == null ? string.Empty : _passwordInput.text;
        public string DisplayName => _displayNameInput == null ? string.Empty : _displayNameInput.text.Trim();

        public void EnsureEditableHierarchy()
        {
            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _backdrop = ResolveOrCreateImage(
                _backdrop,
                "Backdrop",
                transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.005f, 0.012f, 0.02f, 0.9f));
            _backdrop.raycastTarget = true;

            _panel = ResolveOrCreatePanel(_panel);
            _titleText = ResolveOrCreateText(
                _titleText,
                "TitleText",
                _panel,
                new Vector2(0f, -52f),
                new Vector2(660f, 54f),
                34,
                FontStyle.Bold,
                "After333 로그인");
            _statusText = ResolveOrCreateText(
                _statusText,
                "StatusText",
                _panel,
                new Vector2(0f, -108f),
                new Vector2(660f, 52f),
                21,
                FontStyle.Normal,
                "로그인 방법을 선택해주세요.");

            _gameIdInput = ResolveOrCreateInput(
                _gameIdInput,
                "GameIdInput",
                "Game ID",
                new Vector2(0f, -178f),
                InputField.ContentType.Standard);
            _passwordInput = ResolveOrCreateInput(
                _passwordInput,
                "PasswordInput",
                "비밀번호",
                new Vector2(0f, -246f),
                InputField.ContentType.Password);
            _displayNameInput = ResolveOrCreateInput(
                _displayNameInput,
                "DisplayNameInput",
                "표시 이름 (회원가입 시 선택)",
                new Vector2(0f, -314f),
                InputField.ContentType.Standard);

            _loginButton = ResolveOrCreateButton(
                _loginButton,
                "LoginButton",
                "로그인",
                new Vector2(-167f, -394f),
                new Vector2(316f, 62f),
                out _loginButtonLabel);
            _registerButton = ResolveOrCreateButton(
                _registerButton,
                "RegisterButton",
                "회원가입",
                new Vector2(167f, -394f),
                new Vector2(316f, 62f),
                out _registerButtonLabel);
            RemoveObsoleteControl("GuestButton");
            RemoveObsoleteControl("CloseButton");
            _googleButton = ResolveOrCreateButton(
                _googleButton,
                "GoogleButton",
                "Google로 로그인",
                new Vector2(0f, -470f),
                new Vector2(650f, 62f),
                out _googleButtonLabel);
        }

        public void Bind(
            UnityAction loginAction,
            UnityAction registerAction,
            UnityAction googleAction)
        {
            BindButton(_loginButton, loginAction);
            BindButton(_registerButton, registerAction);
            BindButton(_googleButton, googleAction);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void SetState(bool busy, string status, bool googleAvailable)
        {
            if (_statusText != null)
            {
                _statusText.text = string.IsNullOrWhiteSpace(status)
                    ? "로그인 방법을 선택해주세요."
                    : status;
            }

            SetInteractable(_loginButton, !busy);
            SetInteractable(_registerButton, !busy);
            SetInteractable(_googleButton, !busy && googleAvailable);

            if (_loginButtonLabel != null)
            {
                _loginButtonLabel.text = busy ? "처리 중" : "로그인";
            }

            if (_registerButtonLabel != null)
            {
                _registerButtonLabel.text = busy ? "처리 중" : "회원가입";
            }

            if (_googleButtonLabel != null)
            {
                _googleButtonLabel.text = googleAvailable
                    ? busy ? "처리 중" : "Google로 로그인"
                    : "Google 로그인 준비 중";
            }
        }

        private RectTransform ResolveOrCreatePanel(RectTransform current)
        {
            if (current == null && transform.Find("Panel") is RectTransform found)
            {
                current = found;
            }

            if (current != null)
            {
                return current;
            }

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            current = panelObject.GetComponent<RectTransform>();
            current.SetParent(transform, false);
            current.anchorMin = new Vector2(0.5f, 0.5f);
            current.anchorMax = new Vector2(0.5f, 0.5f);
            current.pivot = new Vector2(0.5f, 0.5f);
            current.sizeDelta = new Vector2(760f, 580f);
            current.anchoredPosition = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0.025f, 0.05f, 0.075f, 0.98f);
            return current;
        }

        private void RemoveObsoleteControl(string objectName)
        {
            if (_panel == null || string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            var obsoleteTransform = _panel.Find(objectName);
            if (obsoleteTransform == null)
            {
                return;
            }

            obsoleteTransform.gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(obsoleteTransform.gameObject);
            }
            else
            {
                DestroyImmediate(obsoleteTransform.gameObject);
            }
        }

        private InputField ResolveOrCreateInput(
            InputField current,
            string objectName,
            string placeholder,
            Vector2 position,
            InputField.ContentType contentType)
        {
            if (current == null && _panel.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<InputField>();
            }

            if (current != null)
            {
                current.contentType = contentType;
                return current;
            }

            var inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
            var inputRect = inputObject.GetComponent<RectTransform>();
            inputRect.SetParent(_panel, false);
            inputRect.anchorMin = new Vector2(0.5f, 1f);
            inputRect.anchorMax = new Vector2(0.5f, 1f);
            inputRect.pivot = new Vector2(0.5f, 1f);
            inputRect.sizeDelta = new Vector2(650f, 56f);
            inputRect.anchoredPosition = position;
            inputObject.GetComponent<Image>().color = new Color(0.94f, 0.96f, 0.98f, 1f);

            current = inputObject.GetComponent<InputField>();
            var inputText = CreateTextChild(
                "Text",
                inputRect,
                string.Empty,
                22,
                new Color(0.04f, 0.06f, 0.08f, 1f),
                TextAnchor.MiddleLeft);
            var placeholderText = CreateTextChild(
                "Placeholder",
                inputRect,
                placeholder,
                22,
                new Color(0.25f, 0.3f, 0.36f, 0.72f),
                TextAnchor.MiddleLeft);
            current.textComponent = inputText;
            current.placeholder = placeholderText;
            current.targetGraphic = inputObject.GetComponent<Image>();
            current.contentType = contentType;
            current.lineType = InputField.LineType.SingleLine;
            current.ForceLabelUpdate();
            return current;
        }

        private Button ResolveOrCreateButton(
            Button current,
            string objectName,
            string label,
            Vector2 position,
            Vector2 size,
            out Text labelText)
        {
            if (current == null && _panel.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Button>();
            }

            if (current != null)
            {
                labelText = current.GetComponentInChildren<Text>(true);
                return current;
            }

            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(_panel, false);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = size;
            buttonRect.anchoredPosition = position;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.07f, 0.34f, 0.48f, 0.98f);

            current = buttonObject.GetComponent<Button>();
            current.targetGraphic = image;
            labelText = CreateTextChild(
                "Label",
                buttonRect,
                label,
                24,
                Color.white,
                TextAnchor.MiddleCenter);
            return current;
        }

        private static Image ResolveOrCreateImage(
            Image current,
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Image>();
            }

            if (current != null)
            {
                return current;
            }

            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            var rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            current = imageObject.GetComponent<Image>();
            current.color = color;
            return current;
        }

        private static Text ResolveOrCreateText(
            Text current,
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            FontStyle fontStyle,
            string value)
        {
            if (current == null && parent.Find(objectName) is RectTransform found)
            {
                current = found.GetComponent<Text>();
            }

            if (current != null)
            {
                return current;
            }

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            current = textObject.GetComponent<Text>();
            current.font = ResolveRuntimeFont();
            current.fontSize = fontSize;
            current.fontStyle = fontStyle;
            current.color = Color.white;
            current.alignment = TextAnchor.MiddleCenter;
            current.horizontalOverflow = HorizontalWrapMode.Wrap;
            current.verticalOverflow = VerticalWrapMode.Truncate;
            current.raycastTarget = false;
            current.text = value;
            return current;
        }

        private static Text CreateTextChild(
            string objectName,
            RectTransform parent,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 4f);
            rect.offsetMax = new Vector2(-18f, -4f);
            var text = textObject.GetComponent<Text>();
            text.font = ResolveRuntimeFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void BindButton(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static Font ResolveRuntimeFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial Unicode MS" },
                    32);
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
