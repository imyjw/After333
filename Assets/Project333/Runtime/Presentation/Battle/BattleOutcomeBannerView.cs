using Project333.Runtime.Presentation.Settings;
using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    /// <summary>Presentation only: never records a result or changes battle state.</summary>
    public sealed class BattleOutcomeBannerView : MonoBehaviour
    {
        public const float MinimumDisplaySeconds = 3.2f;
        private const string ResourceRoot = "Project333/UI/BattleOutcomes/";
        private const string SoundResourceRoot = "Project333/Audio/BattleOutcomes/";
        [SerializeField, Range(0f, 1f)] private float _victorySoundVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _defeatSoundVolume = 1f;
        private AudioSource _soundSource;
        private AudioSource _victorySoundSource;
        private AudioSource _defeatSoundSource;
        private AudioClip _victorySound;
        private AudioClip _defeatSound;

        private RectTransform _root;
        private RectTransform _banner;
        private CanvasGroup _group;
        private CanvasGroup _bannerGroup;
        private UnityEngine.UI.Image _shade;
        private UnityEngine.UI.Image _art;
        private TextMeshProUGUI _title;
        private BattleOutcomeAccentGraphic _accent;
        private TMP_FontAsset _font;
        private Sprite _victory;
        private Sprite _defeat;
        private string _outcome;
        private float _elapsed;

        public void Initialize()
        {
            if (_root != null) return;
            _root = (RectTransform)transform;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 12000; // Below the tutorial dialogue/skip controls (32000).
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _shade = CreateImage("BattleDim", _root);
            Stretch(_shade.rectTransform);
            _shade.color = new Color(.015f, .02f, .035f, 0f);
            var accentObject = new GameObject("ResultLightAndMotes", typeof(RectTransform), typeof(CanvasRenderer));
            accentObject.transform.SetParent(_root, false);
            _accent = accentObject.AddComponent<BattleOutcomeAccentGraphic>();
            Stretch(_accent.rectTransform);
            _accent.raycastTarget = false;

            _banner = NewRect("AnimatedBanner", _root);
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _art = CreateImage("CrownBanner", _banner);
            Stretch(_art.rectTransform);
            _art.preserveAspect = true;
            _victory = Resources.Load<Sprite>(ResourceRoot + "Victory");
            _defeat = Resources.Load<Sprite>(ResourceRoot + "Defeat");
            _victorySound = Resources.Load<AudioClip>(SoundResourceRoot + "Victory");
            _defeatSound = Resources.Load<AudioClip>(SoundResourceRoot + "Defeat");

            var titleRect = NewRect("OutcomeTitle", _banner);
            titleRect.anchorMin = new Vector2(.15f, .25f);
            titleRect.anchorMax = new Vector2(.85f, .75f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            _title = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            // The scene's old LiberationSans font has no Korean glyphs.
            _font = TMP_FontAsset.CreateFontAsset(NoticeTheme.Font);
            _font.name = "BattleOutcomeKoreanRuntime";
            _font.TryAddCharacters("무승부");
            _title.font = _font;
            _title.alignment = TextAlignmentOptions.Center;
            _title.fontStyle = FontStyles.Bold;
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Overflow;
            _title.characterSpacing = 12f;
            _title.raycastTarget = false;
            _title.richText = false;
            Resize();
            Hide();
        }

        public void Show(string outcome)
        {
            Initialize();
            if (outcome != "Victory" && outcome != "Defeat" && outcome != "Draw")
            {
                Hide();
                return;
            }
            if (_outcome == outcome && gameObject.activeSelf) return;
            _outcome = outcome;
            _elapsed = 0f;
            gameObject.SetActive(true);
            var victory = outcome == "Victory";
            _art.sprite = victory ? _victory : _defeat;
            _art.enabled = outcome != "Draw";
            // Victory/defeat lettering is part of the user-approved artwork.
            // Do not overlay a different runtime font or duplicate its title.
            _title.text = outcome == "Draw" ? "무승부" : string.Empty;
            _title.color = new Color(.98f, .89f, .65f);
            Resize();
            ApplyAnimation(0f);
            PlayOutcomeSound(outcome);
        }

        private void PlayOutcomeSound(string outcome)
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled) return;
            if (_soundSource != null) _soundSource.Stop();
            var clip = outcome == "Victory" ? _victorySound : outcome == "Defeat" ? _defeatSound : null;
            var victory = outcome == "Victory";
            var volume = victory ? _victorySoundVolume : _defeatSoundVolume;
            if (clip == null || volume <= 0f) return;
            var source = victory ? _victorySoundSource : _defeatSoundSource;
            if (source == null)
            {
                // Each cue keeps its own original gain for the shared effects-volume binding.
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = volume;
                if (victory) _victorySoundSource = source;
                else _defeatSoundSource = source;
            }
            _soundSource = source;
            After333SoundEffects.RegisterSource(_soundSource);
            _soundSource.clip = clip;
            _soundSource.Play();
        }

        public void Hide()
        {
            _outcome = null;
            _elapsed = 0f;
            if (_group != null) _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            ApplyAnimation(_elapsed);
        }

        private void ApplyAnimation(float elapsed)
        {
            var victory = _outcome == "Victory";
            var arrival = Mathf.Clamp01(elapsed / (victory ? .65f : .42f));
            var settled = Mathf.SmoothStep(0f, 1f, arrival);
            var scale = victory
                ? Mathf.LerpUnclamped(.58f, 1f, EaseOutBack(arrival))
                : Mathf.Lerp(1.12f, 1f, settled);
            var shake = !victory && elapsed >= .32f && elapsed < .76f
                ? Mathf.Sin((elapsed - .32f) * 65f) * (1f - Mathf.Clamp01((elapsed - .32f) / .44f))
                : 0f;
            _banner.localScale = Vector3.one * scale;
            _banner.anchoredPosition = new Vector2(shake * 9f,
                (victory ? -65f : 150f) * (1f - settled));
            _banner.localRotation = Quaternion.Euler(0f, 0f, shake * .65f);
            _group.alpha = 1f;
            _bannerGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .18f));
            _shade.color = new Color(.015f, .02f, .035f,
                .66f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .4f)));
            _title.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .16f) / .27f));
            _accent.Animate(victory, elapsed);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_banner != null) Resize();
        }

        private void Resize()
        {
            if (_banner == null) return;
            var aspect = _art.sprite == null || !_art.enabled
                ? 3f : _art.sprite.rect.width / _art.sprite.rect.height;
            var width = Mathf.Min(_root.rect.width * .82f, _root.rect.height * .78f * aspect);
            if (width <= 0f) width = 1500f;
            _banner.sizeDelta = new Vector2(width, width / aspect);
            if (_title != null) _title.fontSize = width * .088f;
        }

        private void OnDisable()
        {
            if (_soundSource != null) _soundSource.Stop();
            _outcome = null;
            _elapsed = 0f;
        }

        private void OnDestroy()
        {
            if (_font == null) return;
            foreach (var texture in _font.atlasTextures) Release(texture);
            Release(_font.material);
            Release(_font);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static float EaseOutBack(float value)
        {
            var t = value - 1f;
            return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        private static UnityEngine.UI.Image CreateImage(string name, Transform parent)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
