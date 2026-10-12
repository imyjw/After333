using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class ManaCannonProjectile : MonoBehaviour
    {
        private RectTransform _root;
        private Image _projectile, _impact;
        private AudioSource _audio;
        private Coroutine _shot;
        private int _generation;

        internal void PlayShot(TileTextView attacker, TileTextView target)
        {
            Cancel();
            if (!isActiveAndEnabled || attacker == null || target == null ||
                !attacker.isActiveAndEnabled || !target.isActiveAndEnabled || !EnsureOverlay(attacker)) return;
            // Load before starting the clock so resource IO cannot shorten charging or flight.
            var projectile = ManaCannonPresentationAssets.ProjectileFrames;
            var impact = ManaCannonPresentationAssets.ImpactFrames;
            var shotSound = ManaCannonPresentationAssets.AttackSound;
            var impactSound = ManaCannonPresentationAssets.ImpactSound;
            if (projectile == null || impact == null) return;
            _shot = StartCoroutine(Shot(attacker, target, _generation));
        }

        private IEnumerator Shot(TileTextView attacker, TileTextView target, int generation)
        {
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var projectile = ManaCannonPresentationAssets.ProjectileFrames;
            var impact = ManaCannonPresentationAssets.ImpactFrames;
            PlaySound(ManaCannonPresentationAssets.AttackSound, .45f);
            var startedAt = Time.unscaledTime;
            var launched = false;
            var impacted = false;
            var completed = false;
            var start = Vector3.zero;
            var width = 0f;
            try
            {
                while (Time.unscaledTime - startedAt < ManaCannonPresentationAssets.TotalDuration)
                {
                    if (generation != _generation || attacker == null || target == null ||
                        !attacker.isActiveAndEnabled || !target.isActiveAndEnabled ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    var elapsed = Time.unscaledTime - startedAt;
                    var end = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
                    if (elapsed >= ManaCannonPresentationAssets.WindupDuration && !launched)
                    {
                        launched = true;
                        start = _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(ManaCannonPresentationAssets.EmitterPoint));
                        width = Vector3.Distance(
                            _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                            _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
                        _projectile.rectTransform.sizeDelta = Vector2.one * Mathf.Max(32f, width * .85f);
                    }
                    if (launched && elapsed < ManaCannonPresentationAssets.ContactAt)
                    {
                        var flight = elapsed - ManaCannonPresentationAssets.WindupDuration;
                        var delta = end - start;
                        _projectile.sprite = projectile[Mathf.FloorToInt(flight * 24f) % projectile.Count];
                        _projectile.rectTransform.localPosition = Vector3.Lerp(start, end,
                            Mathf.Clamp01(flight / ManaCannonPresentationAssets.FlightDuration));
                        _projectile.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                        _projectile.gameObject.SetActive(true);
                    }
                    if (elapsed >= ManaCannonPresentationAssets.ContactAt)
                    {
                        if (!impacted)
                        {
                            impacted = true;
                            _projectile.gameObject.SetActive(false);
                            _impact.rectTransform.sizeDelta = Vector2.one * Mathf.Max(48f, width * 1.4f);
                            _impact.gameObject.SetActive(true);
                            PlaySound(ManaCannonPresentationAssets.ImpactSound, .40f);
                        }
                        _impact.rectTransform.localPosition = end;
                        _impact.sprite = impact[Mathf.Min(impact.Count - 1,
                            Mathf.FloorToInt((elapsed - ManaCannonPresentationAssets.ContactAt) * 18f))];
                    }
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

        private bool EnsureOverlay(TileTextView attacker)
        {
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            if (_root != null)
            {
                // An effect component may survive a screen/canvas rebind.
                if (_root.parent != canvas.transform)
                {
                    _root.SetParent(canvas.transform, false);
                    var reboundCanvas = _root.GetComponent<Canvas>();
                    reboundCanvas.overrideSorting = true; reboundCanvas.sortingOrder = 4905;
                }
                return true;
            }
            var rootObject = new GameObject("ManaCannonShotOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>(); effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            _projectile = CreateImage("ManaCannonProjectile", ManaCannonPresentationAssets.ProjectileCorePoint);
            _impact = CreateImage("ManaCannonImpact", Vector2.one * .5f);
            return true;
        }
        private Image CreateImage(string name, Vector2 pivot)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false); image.rectTransform.pivot = pivot;
            image.raycastTarget = false; image.preserveAspect = true;
            image.gameObject.SetActive(false); return image;
        }
        private void PlaySound(AudioClip clip, float gain)
        {
            if (clip == null) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false; _audio.spatialBlend = 0f; _audio.dopplerLevel = 0f;
            }
            _audio.PlayOneShot(clip, gain);
        }
        private void HideShapes()
        {
            if (_projectile != null) _projectile.gameObject.SetActive(false);
            if (_impact != null) _impact.gameObject.SetActive(false);
        }
        internal void Cancel()
        {
            _generation++;
            if (_shot != null) StopCoroutine(_shot);
            _shot = null; HideShapes();
            if (_audio != null) _audio.Stop();
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy()
        {
            Cancel();
            if (_root != null) Destroy(_root.gameObject);
            if (_audio != null) Destroy(_audio);
        }
    }
}
