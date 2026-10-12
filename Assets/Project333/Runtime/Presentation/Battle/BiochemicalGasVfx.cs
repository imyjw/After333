using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation only: one pooled pair of wisps per affected tile, independent of occupants.
    public sealed class BiochemicalGasVfx : MonoBehaviour
    {
        public const string TextureResourcePath = "Project333/SpellEffects/BiochemicalGas";
        private const int GasSortingOrder = 240; // Above tiles, below occupants (250) and stat bars.
        private const float FadeSeconds = 0.4f;
        private readonly Dictionary<TileView, Patch> _patches = new Dictionary<TileView, Patch>();
        private Texture2D _texture;

        private sealed class Patch
        {
            public RectTransform Root;
            public RawImage[] Wisps;
            public float Visibility;
            public float Phase;
            public int Count;
            public int Triggers;
            public float Pulse;
            public bool Seen;
        }

        public static int CountAt(BattleState state, TileView tile, out int triggers)
        {
            triggers = 0;
            if (state == null || state.IsEnded || tile == null) return 0;
            var count = 0;
            foreach (var effect in state.PersistentEffects)
            {
                if (effect.IsExpired || effect.RemainingTriggers <= 0 || effect.EffectId != BiochemicalBombRules.EffectId) continue;
                var owner = effect.TargetsOwnerBoard ? effect.OwnerId : state.GetOpponent(effect.OwnerId).Id;
                if (owner != tile.OwnerId || !BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, tile.Coord.Column)) continue;
                count++;
                triggers += effect.RemainingTriggers;
            }
            return count;
        }

        public void Present(BattleState state, TileView[] playerTiles, TileView[] opponentTiles)
        {
            foreach (var patch in _patches.Values) patch.Seen = false;
            Sync(state, playerTiles);
            Sync(state, opponentTiles);
            foreach (var patch in _patches.Values)
                if (!patch.Seen) patch.Count = 0;
        }

        private void Sync(BattleState state, TileView[] tiles)
        {
            if (tiles == null) return;
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                var count = CountAt(state, tile, out var triggers);
                if (!_patches.TryGetValue(tile, out var patch))
                {
                    if (count == 0) continue;
                    var anchor = tile.GetComponent<TileTextView>()?.ActiveEffectButtonAnchor;
                    if (anchor == null) anchor = tile.transform as RectTransform;
                    if (anchor == null) continue;
                    if (_texture == null) _texture = Resources.Load<Texture2D>(TextureResourcePath);
                    if (_texture == null) continue;
                    patch = CreatePatch(anchor, tile);
                    _patches.Add(tile, patch);
                }
                patch.Seen = true;
                if (count > 0 && patch.Count == count && triggers < patch.Triggers) patch.Pulse = 1;
                patch.Count = count;
                patch.Triggers = triggers;
                if (count > 0) patch.Root.gameObject.SetActive(true);
            }
        }

        private Patch CreatePatch(RectTransform anchor, TileView tile)
        {
            var root = new GameObject("BiochemicalGas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(anchor, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = GasSortingOrder;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var patch = new Patch { Root = rect, Wisps = new RawImage[2],
                Phase = tile.Coord.Column * 1.73f + tile.Coord.Row * 2.39f + (int)tile.OwnerId * 3.1f };
            for (var i = 0; i < patch.Wisps.Length; i++)
            {
                var wisp = new GameObject("GasWisp" + i, typeof(RectTransform), typeof(RawImage));
                wisp.transform.SetParent(rect, false);
                var image = wisp.GetComponent<RawImage>();
                image.texture = _texture;
                image.raycastTarget = false;
                image.color = new Color(1, 1, 1, 0);
                image.uvRect = i == 0 ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
                patch.Wisps[i] = image;
            }
            return patch;
        }

        private void LateUpdate() => Animate(Time.unscaledTime, Time.unscaledDeltaTime);

        private void Animate(float time, float delta)
        {
            foreach (var patch in _patches.Values)
            {
                if (patch.Root == null) continue;
                patch.Visibility = Mathf.MoveTowards(patch.Visibility, patch.Count > 0 ? 1 : 0, delta / FadeSeconds);
                patch.Pulse = Mathf.MoveTowards(patch.Pulse, 0, delta * 2.5f);
                if (patch.Visibility <= 0) { patch.Root.gameObject.SetActive(false); continue; }
                var size = patch.Root.rect.size;
                for (var i = 0; i < patch.Wisps.Length; i++)
                {
                    var phase = time * (i == 0 ? 0.65f : -0.48f) + patch.Phase + i * 2.4f;
                    var image = patch.Wisps[i];
                    var rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(Mathf.Sin(phase) * size.x * 0.025f,
                        Mathf.Cos(phase * 0.8f) * size.y * 0.025f);
                    rect.sizeDelta = size * ((i == 0 ? 1.04f : 0.88f) + 0.035f * Mathf.Sin(phase));
                    rect.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * 0.6f) * 5);
                    var alpha = (i == 0 ? 0.32f : 0.20f) + Mathf.Sin(phase) * 0.025f;
                    alpha += Mathf.Min(2, Mathf.Max(0, patch.Count - 1)) * 0.025f + patch.Pulse * 0.10f;
                    image.color = new Color(0.80f, 1, 0.78f, alpha * patch.Visibility);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var patch in _patches.Values)
            {
                patch.Count = 0;
                patch.Visibility = 0;
                patch.Pulse = 0;
                if (patch.Root != null) patch.Root.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            foreach (var patch in _patches.Values)
                if (patch.Root != null)
                {
                    if (UnityEngine.Application.isPlaying) Destroy(patch.Root.gameObject);
                    else DestroyImmediate(patch.Root.gameObject);
                }
            _patches.Clear();
        }
    }
}
