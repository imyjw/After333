using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Visual clocks only; the battle gateway remains authoritative for damage and multi-hit.
    internal static class GwangmaPresentationAssets
    {
        internal const string CardId = "Gwangma";
        internal const float BoardScale = 1f;
        internal const float WindupDuration = 0.25f;
        internal const float SwingLeadDuration = 0.09f;
        internal const float ImpactDuration = 0.4f;
        internal const float ApproachDuration = 0.28f;
        internal const float ReturnDuration = 0.22f;
        private static Sprite[] _impact;
        private static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();

        internal static IReadOnlyList<Sprite> ImpactFrames
        {
            get
            {
                if (_impact == null)
                {
                    var texture = Resources.Load<Texture2D>("Project333/Presentation/Gwangma/CrossSlashAtlas");
                    if (texture != null) _impact = Slice(texture, 4, 2, 0, 8);
                }
                return _impact;
            }
        }

        internal static AudioClip GetSound(string name)
        {
            if (!Sounds.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Project333/Audio/Gwangma/" + name);
                if (clip != null) Sounds[name] = clip;
            }
            return clip;
        }

        private static Sprite[] Slice(Texture2D texture, int columns, int rows, int start, int count)
        {
            var frames = new Sprite[count];
            var width = (float)texture.width / columns;
            var height = (float)texture.height / rows;
            for (var i = 0; i < count; i++)
            {
                var index = start + i;
                frames[i] = Sprite.Create(texture, new Rect(index % columns * width,
                    texture.height - (index / columns + 1) * height, width, height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                frames[i].name = texture.name + "_" + index;
                frames[i].hideFlags = HideFlags.DontSave;
            }
            return frames;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            Release(_impact);
            _impact = null; Sounds.Clear();
        }

        private static void Release(Sprite[] frames)
        {
            if (frames == null) return;
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(frame);
                else UnityEngine.Object.DestroyImmediate(frame);
            }
        }
    }
}