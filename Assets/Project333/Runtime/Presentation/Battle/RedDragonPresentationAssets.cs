using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation clocks and resources only; combat stays owned by the battle gateway.
    internal static class RedDragonPresentationAssets
    {
        internal const string CardId = "RedDragon";
        internal const float WindupDuration = 2f / 12f;
        internal const float BreathDuration = 3f / 12f;
        internal const float ApproachDuration = 0.32f;
        internal const float ReturnDuration = 0.24f;
        internal const float FlameInlet = 0.04f;
        internal const float FlameOutlet = 0.94f;
        // Stand-off uses the farthest forward breathing pose; the emitter follows each authored jaw pose.
        internal static readonly Vector2 MouthAnchor = new Vector2(0.92f, 0.37f);
        internal static Vector2 GetMouthAnchor(float attackElapsed)
        {
            var frame = Mathf.FloorToInt(attackElapsed * 12f);
            if (frame <= 2) return new Vector2(0.78f, 0.385f);
            if (frame == 3) return new Vector2(0.92f, 0.37f);
            if (frame == 4) return new Vector2(0.88f, 0.33f);
            return new Vector2(0.76f, 0.39f);
        }
        private static Sprite[] _fire;
        private static AudioClip _breathSound;

        internal static IReadOnlyList<Sprite> FireFrames
        {
            get
            {
                if (_fire != null) return _fire;
                var texture = Resources.Load<Texture2D>("Project333/Presentation/RedDragon/FireAtlas");
                if (texture == null) return null;
                const int count = 6;
                var width = texture.width / (float)count;
                _fire = new Sprite[count];
                for (var i = 0; i < count; i++)
                {
                    _fire[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height),
                        Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
                    _fire[i].name = "FireAtlas_" + i;
                    _fire[i].hideFlags = HideFlags.DontSave;
                }
                return _fire;
            }
        }

        internal static AudioClip BreathSound
        {
            get
            {
                if (_breathSound == null)
                    _breathSound = Resources.Load<AudioClip>("Project333/Audio/RedDragon/FireBreath");
                return _breathSound;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            if (_fire != null)
                foreach (var sprite in _fire)
                {
                    if (sprite == null) continue;
                    if (UnityEngine.Application.isPlaying) Object.Destroy(sprite);
                    else Object.DestroyImmediate(sprite);
                }
            _fire = null;
            _breathSound = null;
        }
    }
}