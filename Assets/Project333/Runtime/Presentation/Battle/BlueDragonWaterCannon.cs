using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class BlueDragonWaterCannon : MonoBehaviour
    {
        private RectTransform _root;
        private Image _projectile;
        private Image _impact;
        private AudioSource _audio;
        private Coroutine _splash;
        private int _generation;

        internal void PlayCharge()
        {
            PlaySound("WaterCannonCharge", 0.26f);
        }

        internal IEnumerator PlayFlight(TileTextView attacker, TileTextView target)
        {
            if (attacker == null || target == null || !EnsureOverlay(attacker)) yield break;
            var frames = BlueDragonPresentationAssets.ProjectileFrames;
            if (frames == null || frames.Count == 0) yield break;
            var generation = ++_generation;
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var start = _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(new Vector2(0.90f, 0.52f)));
            var end = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
            var direction = end - start;
            var visualWidth = Vector3.Distance(
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
            _projectile.rectTransform.sizeDelta = Vector2.one * Mathf.Max(24f, visualWidth * 0.8f);
            _projectile.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            _projectile.gameObject.SetActive(true);
            PlaySound("WaterCannonLaunch", 0.55f);
            var elapsed = 0f;
            try
            {
                while (elapsed < BlueDragonPresentationAssets.FlightDuration)
                {
                    if (generation != _generation || attacker == null || target == null ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    var t = Mathf.Clamp01(elapsed / BlueDragonPresentationAssets.FlightDuration);
                    _projectile.rectTransform.localPosition = Vector3.Lerp(start, end, t);
                    _projectile.sprite = frames[Mathf.FloorToInt(elapsed * 18f) % frames.Count];
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (_projectile != null) _projectile.gameObject.SetActive(false);
            }
            if (generation != _generation || target == null || target.VisualRuntimeId != targetId) yield break;
            // The target reaction owns the shared UnitHit sound.
            if (_splash != null) StopCoroutine(_splash);
            _splash = StartCoroutine(PlaySplash(end, Mathf.Max(48f, visualWidth * 1.5f)));
        }

        private IEnumerator PlaySplash(Vector3 position, float size)
        {
            var frames = BlueDragonPresentationAssets.ImpactFrames;
            if (frames == null || frames.Count == 0) yield break;
            _impact.rectTransform.localPosition = position;
            _impact.rectTransform.sizeDelta = Vector2.one * size;
            _impact.gameObject.SetActive(true);
            var elapsed = 0f;
            while (elapsed < BlueDragonPresentationAssets.ImpactDuration)
            {
                _impact.sprite = frames[Mathf.Min(frames.Count - 1,
                    Mathf.FloorToInt(elapsed / BlueDragonPresentationAssets.ImpactDuration * frames.Count))];
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _impact.gameObject.SetActive(false);
            _splash = null;
        }

        private bool EnsureOverlay(TileTextView attacker)
        {
            if (_root != null) return true;
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            var rootObject = new GameObject("BlueDragonWaterCannon", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>();
            effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            _projectile = CreateImage("WaterCannonProjectile");
            _impact = CreateImage("WaterCannonImpact");
            _projectile.rectTransform.pivot = new Vector2(0.88f, 0.5f);
            _impact.rectTransform.pivot = new Vector2(0.5f, 0.5f); // Center the burst on the shared hit point.
            return true;
        }

        private Image CreateImage(string label)
        {
            var image = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false);
            image.raycastTarget = false; image.preserveAspect = true;
            image.gameObject.SetActive(false);
            return image;
        }

        private void PlaySound(string name, float volume)
        {
            var clip = BlueDragonPresentationAssets.GetSound(name);
            if (clip == null) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false; _audio.spatialBlend = 0f;
            }
            _audio.PlayOneShot(clip, volume);
        }

        internal void Cancel()
        {
            _generation++;
            StopAllCoroutines(); _splash = null;
            if (_projectile != null) _projectile.gameObject.SetActive(false);
            if (_impact != null) _impact.gameObject.SetActive(false);
            if (_audio != null) _audio.Stop();
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
