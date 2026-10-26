using Project333.Runtime.Presentation.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    internal sealed class A212SawAttack : MonoBehaviour
    {
        private RectTransform _root;
        private readonly Image[] _sparks = new Image[2];
        private readonly Coroutine[] _strikes = new Coroutine[2];
        private readonly AudioSource[] _audio = new AudioSource[2];
        private readonly int[] _slotGenerations = new int[2];

        // Separate slots preserve both sides of a melee exchange without stacking four motor sounds.
        internal void PlayStrike(TileTextView attacker, TileTextView target, int hitCount, float hitInterval,
            float windup, bool counterattack = false)
        {
            if (attacker == null || target == null || !isActiveAndEnabled || !EnsureOverlay(attacker)) return;
            var slot = counterattack ? 1 : 0;
            StopSlot(slot);
            var generation = _slotGenerations[slot];
            _strikes[slot] = StartCoroutine(Strike(attacker, target, Mathf.Max(1, hitCount),
                Mathf.Max(A212PresentationAssets.ImpactDuration, hitInterval), Mathf.Max(0f, windup), slot, generation));
        }

        private IEnumerator Strike(TileTextView attacker, TileTextView target, int hitCount, float hitInterval,
            float windup, int slot, int generation)
        {
            var sourceId = attacker.VisualRuntimeId;
            var targetId = target.VisualRuntimeId;
            var frames = A212PresentationAssets.ImpactFrames;
            var startedAt = Time.unscaledTime;
            var sounded = false;
            var completed = false;
            var image = _sparks[slot];
            var sound = A212PresentationAssets.AttackSound;
            var duration = windup + Mathf.Max(A212PresentationAssets.GetGrindDuration(hitCount, hitInterval),
                sound == null ? 0f : sound.length);
            try
            {
                while (Time.unscaledTime - startedAt < duration)
                {
                    if (generation != _slotGenerations[slot] || attacker == null || target == null ||
                        !attacker.isActiveAndEnabled || !target.isActiveAndEnabled ||
                        attacker.VisualRuntimeId != sourceId || target.VisualRuntimeId != targetId) yield break;
                    var contactElapsed = Time.unscaledTime - startedAt - windup;
                    if (contactElapsed >= 0f)
                    {
                        if (!sounded)
                        {
                            PlaySawSound(slot); sounded = true;
                            // A late contact frame must still allow the whole one-shot to finish.
                            if (sound != null) duration = Mathf.Max(duration,
                                Time.unscaledTime - startedAt + sound.length);
                        }
                        var pulse = Mathf.Min(hitCount - 1, Mathf.FloorToInt(contactElapsed / hitInterval));
                        var pulseElapsed = contactElapsed - pulse * hitInterval;
                        image.gameObject.SetActive(frames != null && frames.Count > 0 &&
                            pulseElapsed < A212PresentationAssets.ImpactDuration);
                        if (image.gameObject.activeSelf)
                        {
                            image.sprite = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(
                                pulseElapsed / A212PresentationAssets.ImpactDuration * frames.Count))];
                            var width = Vector3.Distance(
                                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.zero)),
                                _root.InverseTransformPoint(attacker.GetVisualNormalizedWorldPosition(Vector2.right)));
                            image.rectTransform.sizeDelta = Vector2.one * Mathf.Max(36f, width * 0.72f);
                            image.rectTransform.localScale = new Vector3(
                                attacker.GetVisualWorldPosition().x <= target.GetVisualWorldPosition().x ? 1f : -1f, 1f, 1f);
                            // Match all damaged occupants' lowered impact anchor, including responsive boards.
                            image.rectTransform.localPosition = _root.InverseTransformPoint(target.GetVisualImpactWorldPosition());
                        }
                    }
                    yield return null;
                }
                completed = true;
            }
            finally
            {
                // Replaced/aborted work cannot hide a newer strike occupying the same slot.
                if (generation == _slotGenerations[slot])
                {
                    if (image != null) image.gameObject.SetActive(false);
                    if (!completed && _audio[slot] != null) _audio[slot].Stop();
                    _strikes[slot] = null;
                }
            }
        }

        private bool EnsureOverlay(TileTextView attacker)
        {
            if (_root != null) return true;
            var canvas = attacker.GetVisualRootCanvas();
            if (canvas == null) return false;
            var rootObject = new GameObject("A212SawAttack", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = rootObject.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;
            var effectCanvas = rootObject.GetComponent<Canvas>();
            effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = 4905;
            var group = rootObject.GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            for (var i = 0; i < _sparks.Length; i++)
            {
                var image = new GameObject(i == 0 ? "SawAttackSparks" : "SawCounterSparks",
                    typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(_root, false);
                image.rectTransform.pivot = Vector2.one * 0.5f;
                image.raycastTarget = false; image.preserveAspect = true;
                image.gameObject.SetActive(false);
                _sparks[i] = image;
            }
            return true;
        }

        private void PlaySawSound(int slot)
        {
            var clip = A212PresentationAssets.AttackSound;
            if (clip == null || !isActiveAndEnabled) return;
            if (_audio[slot] == null)
            {
                _audio[slot] = gameObject.AddComponent<AudioSource>();
                _audio[slot].playOnAwake = false; _audio[slot].spatialBlend = 0f; _audio[slot].dopplerLevel = 0f;
            }
            After333SoundEffects.PlayOneShot(_audio[slot], clip, 0.30f);
        }

        private void StopSlot(int slot)
        {
            _slotGenerations[slot]++;
            if (_strikes[slot] != null) StopCoroutine(_strikes[slot]);
            _strikes[slot] = null;
            if (_sparks[slot] != null) _sparks[slot].gameObject.SetActive(false);
            if (_audio[slot] != null) _audio[slot].Stop();
        }

        internal void Cancel()
        {
            for (var i = 0; i < _strikes.Length; i++) StopSlot(i);
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}

