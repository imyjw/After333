using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation only: sprites and clocks never resolve damage or send battle commands.
    internal static class BlueDragonPresentationAssets
    {
        internal const string CardId = "BlueDragon";
        internal const float WindupDuration = 0.25f;
        internal const float FlightDuration = 0.38f;
        internal const float ImpactDuration = 0.4f;
        // All motions share the same 512px frame convention as Idle.
        internal const float BoardScale = 1f;
        private const string ResourceRoot = "Project333/Presentation/BlueDragon/";
        private static Sprite[] _projectile;
        private static Sprite[] _impact;
        private static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
        internal static IReadOnlyList<Sprite> ProjectileFrames { get { LoadWater(); return _projectile; } }
        internal static IReadOnlyList<Sprite> ImpactFrames { get { LoadWater(); return _impact; } }

        internal static AudioClip GetSound(string name)
        {
            if (!Sounds.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Project333/Audio/BlueDragon/" + name);
                Sounds[name] = clip;
            }
            return clip;
        }

        private static void LoadWater()
        {
            if (_projectile != null) return;
            var texture = Resources.Load<Texture2D>(ResourceRoot + "WaterAtlas");
            if (texture == null) return;
            _projectile = Slice(texture, 4, 3, 0, 4);
            _impact = Slice(texture, 4, 3, 4, 8);
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
            Release(_projectile); Release(_impact);
            _projectile = null; _impact = null; Sounds.Clear();
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
