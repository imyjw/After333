using Project333.Runtime.Presentation.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class A301LaserAttack : MonoBehaviour
    {
        private RectTransform _root;
        private readonly Image[] _beam = new Image[3];
        private readonly Image[] _muzzle = new Image[2];
        private readonly Image[] _impact = new Image[5];
        private static readonly Color[] BeamColors = { new Color(1f, .11f, .17f, .24f),
            new Color(1f, .10f, .16f), new Color(1f, .96f, .94f) };
        private static readonly float[] BeamThickness = { .040f, .022f, .008f };
        private AudioSource _audio;
        private Coroutine _shot;
        private int _generation;

        // A hitscan beam spans emitter to target immediately at the packet's presentation time.
        internal void PlayShot(TileTextView attacker, TileTextView target)
        {
            Cancel();
            if (!isActiveAndEnabled || attacker == null || target == null ||
                !attacker.isActiveAndEnabled || !target.isActiveAndEnabled || !EnsureOverlay(attacker)) return;
            _shot = StartCoroutine(Shot(attacker, target, _generation));
        }

        private IEnumerator Shot(TileTextView attacker, TileTextView target, int generation)
        {
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var clip = A301PresentationAssets.AttackSound;
            if (clip != null)
            {
                if (_audio == null)
                {
                    _audio = gameObject.AddComponent<AudioSource>();
                    _audio.playOnAwake = false; _audio.spatialBlend = 0f; _audio.dopplerLevel = 0f;
                }
                After333SoundEffects.PlayOneShot(_audio, clip, 0.30f);
            }
            var startedAt = Time.unscaledTime;
            var duration = Mathf.Max(A301PresentationAssets.ImpactDuration, clip == null ? 0f : clip.length);
            var completed = false;
            try
            {
                while (Time.unscaledTime - startedAt < duration)
                {
                    if (generation != _generation || attacker == null || target == null ||
                        !attacker.isActiveAndEnabled || !target.isActiveAndEnabled ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    UpdateShapes(attacker, target, Time.unscaledTime - startedAt);
                    yield return null;
                }
                completed = true;
            }
            finally
            {
                if (generation == _generation)
                {
                    HideShapes();
                    if (!completed && _audio != null) _audio.Stop();
                    _shot = null;
                }
            }
        }

        private void UpdateShapes(TileTextView attacker, TileTextView target, float elapsed)
        {
            var start = _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(A301PresentationAssets.EmitterPoint));
            var end = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
            var delta = end - start;
            var width = Vector3.Distance(
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var alpha = Mathf.Clamp01((A301PresentationAssets.BeamDuration - elapsed) / 0.07f);
            for (var i = 0; i < _beam.Length; i++)
            {
                var image = _beam[i];
                image.gameObject.SetActive(elapsed < A301PresentationAssets.BeamDuration && delta.sqrMagnitude > .001f);
                image.rectTransform.localPosition = start;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                image.rectTransform.sizeDelta = new Vector2(delta.magnitude, Mathf.Max(1f, width * BeamThickness[i]));
                var color = BeamColors[i]; color.a *= alpha; image.color = color;
            }
            for (var i = 0; i < _muzzle.Length; i++)
            {
                var image = _muzzle[i]; image.gameObject.SetActive(elapsed < A301PresentationAssets.BeamDuration);
                image.rectTransform.localPosition = start;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : 0f);
                image.rectTransform.sizeDelta = Vector2.one * Mathf.Max(2f, width * (i == 0 ? .065f : .03f));
                image.color = i == 0 ? new Color(.08f, .9f, 1f, alpha * .85f) : new Color(.88f, 1f, 1f, alpha);
            }
            var impactT = Mathf.Clamp01(elapsed / A301PresentationAssets.ImpactDuration);
            for (var i = 0; i < _impact.Length; i++)
            {
                var image = _impact[i]; image.gameObject.SetActive(impactT < 1f);
                var heading = Quaternion.Euler(0, 0, angle + 45f + i * 90f);
                image.rectTransform.localPosition = end + (i == 0 ? Vector3.zero :
                    heading * Vector3.right * width * (.025f + impactT * .065f));
                image.rectTransform.localRotation = i == 0 ? Quaternion.Euler(0f, 0f, 45f) : heading;
                image.rectTransform.sizeDelta = i == 0 ? Vector2.one * width * .09f * (1f - impactT * .6f) :
                    new Vector2(Mathf.Max(2f, width * .05f * (1f - impactT)), Mathf.Max(1f, width * .01f));
                image.color = i == 0 ? new Color(1f, .93f, .9f, 1f - impactT) : new Color(1f, .19f, .2f, 1f - impactT);
            }
        }

        private bool EnsureOverlay(TileTextView attacker)
        {
            if (_root != null) return true;
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            var rootObject = new GameObject("A301LaserAttack", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>(); _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>(); effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            for (var i = 0; i < _beam.Length; i++) _beam[i] = CreateImage("LaserBeam" + i, new Vector2(0f, .5f));
            for (var i = 0; i < _muzzle.Length; i++) _muzzle[i] = CreateImage("LaserMuzzle" + i, Vector2.one * .5f);
            for (var i = 0; i < _impact.Length; i++) _impact[i] = CreateImage("LaserImpact" + i, Vector2.one * .5f);
            return true;
        }

        private Image CreateImage(string name, Vector2 pivot)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false); image.rectTransform.pivot = pivot;
            image.raycastTarget = false; image.gameObject.SetActive(false); return image;
        }

        private void HideShapes()
        {
            foreach (var image in _beam) if (image != null) image.gameObject.SetActive(false);
            foreach (var image in _muzzle) if (image != null) image.gameObject.SetActive(false);
            foreach (var image in _impact) if (image != null) image.gameObject.SetActive(false);
        }

        internal void Cancel()
        {
            _generation++;
            if (_shot != null) StopCoroutine(_shot);
            _shot = null; HideShapes();
            if (_audio != null) _audio.Stop();
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
