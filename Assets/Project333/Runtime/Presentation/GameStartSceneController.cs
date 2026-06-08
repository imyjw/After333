using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Startup
{
    public sealed class GameStartSceneController : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Sprite _backgroundSprite;
        [SerializeField] private string _backgroundResourcePath = "Project333/StartScene/GameStartBackground";
        [SerializeField] private Image _startButtonImage;
        [SerializeField] private Sprite _startButtonSprite;
        [SerializeField] private string _startButtonResourcePath = "Project333/StartScene/GameStartButton";
        [SerializeField] private Text _ticketText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Text _startGameButtonLabel;
        [SerializeField] private string _draftSceneName = "Draft_VSlice";
        [SerializeField] private string _battleSceneName = "Battle_VSlice";
        [SerializeField] private int _ticketCost = 3;

        private void Awake()
        {
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            RefreshUi();
        }

        private void OnEnable()
        {
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
            RefreshUi();
        }

        private void OnValidate()
        {
            AutoAssignBackgroundImage();
            AutoAssignStartButtonImage();
            EnsureBackgroundSprite();
            EnsureStartButtonSprite();
            ApplyBackgroundVisual();
            ApplyStartButtonVisual();
        }

        public void StartGameFromUi()
        {
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);

            if (!DraftRunSessionState.TrySpendTicketsForNewRun(_ticketCost))
            {
                SetStatus("티켓이 부족합니다.");
                RefreshUi();
                return;
            }

            SetStatus("드래프트를 시작합니다.");
            RefreshUi();
            SceneManager.LoadScene(_draftSceneName);
        }

        private void RefreshUi()
        {
            if (_ticketText != null)
            {
                _ticketText.text = $"Tickets: {DraftRunSessionState.AvailableTickets}";
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = DraftRunSessionState.CanSpendTickets(_ticketCost);
            }

            if (_startGameButtonLabel != null)
            {
                _startGameButtonLabel.text = $"게임 시작 (-{_ticketCost} Tickets)";
                _startGameButtonLabel.enabled = _startButtonSprite == null;
            }

            if (_statusText != null)
            {
                _statusText.text = DraftRunSessionState.CanSpendTickets(_ticketCost)
                    ? "게임을 시작하면 티켓 3개를 차감하고 드래프트로 이동합니다."
                    : "티켓이 부족합니다.";
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        private void AutoAssignBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                return;
            }

            if (transform.Find("Background") is RectTransform backgroundTransform)
            {
                _backgroundImage = backgroundTransform.GetComponent<Image>();
            }
        }

        private void AutoAssignStartButtonImage()
        {
            if (_startButtonImage != null)
            {
                return;
            }

            if (_startGameButton != null)
            {
                _startButtonImage = _startGameButton.GetComponent<Image>();
                return;
            }

            if (transform.Find("CenterPanel/StartGameButton") is RectTransform buttonTransform)
            {
                _startGameButton = buttonTransform.GetComponent<Button>();
                _startButtonImage = buttonTransform.GetComponent<Image>();
            }
        }

        private void EnsureBackgroundSprite()
        {
            if (_backgroundSprite != null || string.IsNullOrWhiteSpace(_backgroundResourcePath))
            {
                return;
            }

            _backgroundSprite = LoadSpriteFromResources(_backgroundResourcePath);
        }

        private void EnsureStartButtonSprite()
        {
            if (_startButtonSprite != null || string.IsNullOrWhiteSpace(_startButtonResourcePath))
            {
                return;
            }

            _startButtonSprite = LoadSpriteFromResources(_startButtonResourcePath);
        }

        private void ApplyBackgroundVisual()
        {
            if (_backgroundImage == null || _backgroundSprite == null)
            {
                return;
            }

            _backgroundImage.sprite = _backgroundSprite;
            _backgroundImage.color = Color.white;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.preserveAspect = false;
        }

        private void ApplyStartButtonVisual()
        {
            if (_startButtonImage == null || _startButtonSprite == null)
            {
                return;
            }

            _startButtonImage.sprite = _startButtonSprite;
            _startButtonImage.color = Color.white;
            _startButtonImage.type = Image.Type.Simple;
            _startButtonImage.preserveAspect = true;

            if (_startGameButtonLabel != null)
            {
                _startGameButtonLabel.enabled = false;
            }
        }

        private static Sprite LoadSpriteFromResources(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            var runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeSprite.name = texture.name;
            return runtimeSprite;
        }
    }
}
