using Project333.Runtime.Presentation.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class RedDragonFireBreath : MonoBehaviour
    {
        private RectTransform _root;
        private readonly Image[] _flames = new Image[2];
        private readonly Coroutine[] _strikes = new Coroutine[2];
        private readonly AudioSource[] _audio = new AudioSource[2];
        private int _generation;

        // Independent slots preserve a simultaneous RedDragon counterattack.
        internal void PlayStrike(TileTextView attacker, TileTextView target, bool counterattack = false)
        {
            if (attacker == null || target == null || !isActiveAndEnabled || !EnsureOverlay(attacker)) return;
            var slot = counterattack ? 1 : 0;
            if (_strikes[slot] != null) StopCoroutine(_strikes[slot]);
            if (_audio[slot] != null) _audio[slot].Stop();
            _flames[slot].gameObject.SetActive(false);
            _strikes[slot] = StartCoroutine(Strike(attacker, target, slot, _generation));
        }

        private IEnumerator Strike(TileTextView attacker, TileTextView target, int slot, int generation)
        {
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var frames = RedDragonPresentationAssets.FireFrames;
            var startedAt = Time.unscaledTime;
            var breathed = false;
            var completed = false;
            var image = _flames[slot];
            try
            {
                while (Time.unscaledTime - startedAt < RedDragonPresentationAssets.WindupDuration + RedDragonPresentationAssets.BreathDuration)
                {
                    if (generation != _generation || attacker == null || target == null ||
                        !attacker.isActiveAndEnabled || !target.isActiveAndEnabled ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    var elapsed = Time.unscaledTime - startedAt;
                    if (elapsed >= RedDragonPresentationAssets.WindupDuration)
                    {
                        if (!breathed) { PlayBreathSound(slot); breathed = true; }
                        PlaceFlame(image.rectTransform, attacker, target, elapsed);
                        if (frames != null && frames.Count > 0)
                        {
                            image.sprite = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(
                                (elapsed - RedDragonPresentationAssets.WindupDuration) /
                                RedDragonPresentationAssets.BreathDuration * frames.Count))];
                            image.gameObject.SetActive(true);
                        }
                    }
                    yield return null;
                }
                completed = true;
            }
            finally
            {
                if (image != null) image.gameObject.SetActive(false);
                if (!completed && _audio[slot] != null) _audio[slot].Stop();
                _strikes[slot] = null;
            }
        }

        private void PlaceFlame(RectTransform flame, TileTextView attacker, TileTextView target, float attackElapsed)
        {
            var start = _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(RedDragonPresentationAssets.GetMouthAnchor(attackElapsed)));
            var end = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
            var direction = end - start;
            var width = Vector3.Distance(
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
            // The authored inlet is fixed at the mouth; the outlet follows the shared lower hit anchor.
            flame.localPosition = start;
            flame.sizeDelta = new Vector2(direction.magnitude /
                (RedDragonPresentationAssets.FlameOutlet - RedDragonPresentationAssets.FlameInlet),
                Mathf.Clamp(direction.magnitude * 0.9f, width * 0.28f, width) * 4.5f);
            flame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        internal static Vector3 GetMeleeContactPosition(TileTextView attacker, TileTextView target, Vector3 baseContact)
        {
            if (attacker == null || target == null) return baseContact;
            var center = attacker.GetVisualWorldPosition();
            var mouth = attacker.GetVisualNormalizedWorldPosition(RedDragonPresentationAssets.MouthAnchor);
            var facing = attacker.GetVisualNormalizedWorldPosition(Vector2.right) -
                         attacker.GetVisualNormalizedWorldPosition(Vector2.zero);
            var targetBody = target.GetVisualImpactWorldPosition();
            // Leave room in front of the jaws for a short cone, without changing the ground line or scale.
            var axis = facing.normalized;
            var desired = targetBody - axis * facing.magnitude * 0.15f - (mouth - center);
            return baseContact + Vector3.Project(desired - baseContact, axis);
        }

        private bool EnsureOverlay(TileTextView attacker)
        {
            if (_root != null) return true;
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            var rootObject = new GameObject("RedDragonFireBreath", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>();
            effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            for (var i = 0; i < _flames.Length; i++)
            {
                var image = new GameObject(i == 0 ? "FireBreathAttack" : "FireBreathCounter", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_root, false);
                image.rectTransform.pivot = new Vector2(RedDragonPresentationAssets.FlameInlet, 0.5f);
                image.raycastTarget = false;
                image.preserveAspect = false;
                image.gameObject.SetActive(false);
                _flames[i] = image;
            }
            return true;
        }

        private void PlayBreathSound(int slot)
        {
            var clip = RedDragonPresentationAssets.BreathSound;
            if (clip == null || !isActiveAndEnabled) return;
            if (_audio[slot] == null)
            {
                _audio[slot] = gameObject.AddComponent<AudioSource>();
                _audio[slot].playOnAwake = false; _audio[slot].spatialBlend = 0f; _audio[slot].dopplerLevel = 0f;
            }
            After333SoundEffects.PlayOneShot(_audio[slot], clip, 0.45f);
        }

        internal void Cancel()
        {
            _generation++;
            StopAllCoroutines();
            for (var i = 0; i < _flames.Length; i++)
            {
                _strikes[i] = null;
                if (_flames[i] != null) _flames[i].gameObject.SetActive(false);
                if (_audio[i] != null) _audio[i].Stop();
            }
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}