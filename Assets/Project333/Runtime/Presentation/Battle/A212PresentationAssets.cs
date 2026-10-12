using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation clocks only. Effective hit counts and damage come from the battle state.
    internal static class A212PresentationAssets
    {
        internal const string CardId = "A-212";
        internal const float WindupDuration = 2f / 8f;
        internal const float ImpactDuration = 0.12f;
        internal const float HitInterval = 1f / 8f;
        internal const float ApproachDuration = 0.28f;
        internal const float ReturnDuration = 0.22f;
        private static Sprite[] _impact;
        private static AudioClip _attackSound;

        internal static IReadOnlyList<Sprite> ImpactFrames
        {
            get
            {
                if (_impact != null) return _impact;
                var texture = Resources.Load<Texture2D>("Project333/Units/A-212/SawImpactAtlas");
                if (texture == null) return null;
                _impact = new Sprite[8];
                var width = texture.width / 4f;
                var height = texture.height / 2f;
                for (var i = 0; i < _impact.Length; i++)
                {
                    _impact[i] = Sprite.Create(texture,
                        new Rect(i % 4 * width, texture.height - (i / 4 + 1) * height, width, height),
                        Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
                    _impact[i].name = "SawImpactAtlas_" + i;
                    _impact[i].hideFlags = HideFlags.DontSave;
                }
                return _impact;
            }
        }

        internal static AudioClip AttackSound
        {
            get
            {
                if (_attackSound == null)
                    _attackSound = Resources.Load<AudioClip>("Project333/Units/A-212/Audio/SawAttack");
                return _attackSound;
            }
        }

        internal static float GetGrindDuration(int hitCount, float hitInterval)
        {
            return (Mathf.Max(1, hitCount) - 1) * Mathf.Max(ImpactDuration, hitInterval) + ImpactDuration;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            if (_impact != null)
                foreach (var sprite in _impact)
                {
                    if (sprite == null) continue;
                    if (UnityEngine.Application.isPlaying) Object.Destroy(sprite);
                    else Object.DestroyImmediate(sprite);
                }
            _impact = null;
            _attackSound = null;
        }
    }
}

