using Project333.Runtime.Presentation.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class GwangmaCrossSlash : MonoBehaviour
    {
        private RectTransform _root;
        private readonly Image[] _impacts = new Image[2];
        private readonly Coroutine[] _strikes = new Coroutine[2];
        private AudioSource _audio;
        private int _generation;

        internal void PlayDash() { PlaySound("Dash", 0.3f); }

        // Two slots allow an attacking Gwangma and a counterattacking Gwangma together.
        // This never creates extra damage packets or battle commands.
        internal void PlayStrike(TileTextView attacker, TileTextView target, bool counterattack = false)
        {
            if (attacker == null || target == null || !isActiveAndEnabled || !EnsureOverlay(attacker)) return;
            var slot = counterattack ? 1 : 0;
            if (_strikes[slot] != null) StopCoroutine(_strikes[slot]);
            _impacts[slot].gameObject.SetActive(false);
            _strikes[slot] = StartCoroutine(Strike(attacker, target, slot, _generation));
        }

        private IEnumerator Strike(TileTextView attacker, TileTextView target, int slot, int generation)
        {
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var frames = GwangmaPresentationAssets.ImpactFrames;
            var startedAt = Time.unscaledTime;
            var swung = false;
            var impacted = false;
            var image = _impacts[slot];
            PlaySound("SwordDraw", 0.2f);
            try
            {
                while (Time.unscaledTime - startedAt < GwangmaPresentationAssets.WindupDuration + GwangmaPresentationAssets.ImpactDuration)
                {
                    // The coroutine starts during an existing frame. Its deltaTime includes
                    // time before this strike, so measure from the actual playback start.
                    var elapsed = Time.unscaledTime - startedAt;
                    if (generation != _generation || attacker == null || target == null ||
                        !attacker.isActiveAndEnabled || !target.isActiveAndEnabled ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    if (!swung && elapsed >= GwangmaPresentationAssets.WindupDuration - GwangmaPresentationAssets.SwingLeadDuration)
                    {
                        PlaySound("SwordSwing", 0.47f);
                        swung = true;
                    }
                    if (elapsed >= GwangmaPresentationAssets.WindupDuration)
                    {
                        if (!impacted)
                        {
                            // The target reaction owns the shared UnitHit sound.
                            var width = Vector3.Distance(
                                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
                            image.rectTransform.sizeDelta = Vector2.one * Mathf.Max(42f, width * 1.05f);
                            image.rectTransform.localScale = new Vector3(
                                attacker.GetVisualWorldPosition().x <= target.GetVisualWorldPosition().x ? 1f : -1f, 1f, 1f);
                            impacted = true;
                        }
                        // Follow the same lowered body anchor used by all other impact effects.
                        image.rectTransform.localPosition = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
                        if (frames != null && frames.Count > 0)
                        {
                            image.sprite = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(
                                (elapsed - GwangmaPresentationAssets.WindupDuration) /
                                GwangmaPresentationAssets.ImpactDuration * frames.Count))];
                            image.gameObject.SetActive(true);
                        }
                    }
                    yield return null;
                }
            }
            finally
            {
                if (image != null) image.gameObject.SetActive(false);
                _strikes[slot] = null;
            }
        }

        private bool EnsureOverlay(TileTextView attacker)
        {
            if (_root != null) return true;
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            var rootObject = new GameObject("GwangmaCrossSlash", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>();
            effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            for (var i = 0; i < _impacts.Length; i++)
            {
                var image = new GameObject(i == 0 ? "CrossSlashAttack" : "CrossSlashCounter", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_root, false);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.raycastTarget = false; image.preserveAspect = true;
                image.gameObject.SetActive(false);
                _impacts[i] = image;
            }
            return true;
        }

        private void PlaySound(string name, float volume)
        {
            var clip = GwangmaPresentationAssets.GetSound(name);
            if (clip == null || !isActiveAndEnabled) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false; _audio.spatialBlend = 0f; _audio.dopplerLevel = 0f;
            }
            After333SoundEffects.PlayOneShot(_audio, clip, volume);
        }

        internal void Cancel()
        {
            _generation++;
            StopAllCoroutines();
            for (var i = 0; i < _impacts.Length; i++)
            {
                _strikes[i] = null;
                if (_impacts[i] != null) _impacts[i].gameObject.SetActive(false);
            }
            if (_audio != null) _audio.Stop();
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}