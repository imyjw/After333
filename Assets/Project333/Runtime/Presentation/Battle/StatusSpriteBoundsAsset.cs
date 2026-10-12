using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Metadata only: Resources.Load must not bring every unit texture into memory.
    // VisualKey uses the shared controller name, or CardId for a static board sprite.
    public sealed class StatusSpriteBoundsAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string VisualKey;
            public string TextureName;
            public string SpriteName;
            public Rect SpriteRect;
            public Rect NormalizedBounds;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;

        // Called when an entry/sprite is first encountered, never on a steady-state frame.
        public static string CreateKey(string visualKey, string textureName, string spriteName, Rect spriteRect)
        {
            return (visualKey ?? string.Empty) + "|" + (textureName ?? string.Empty) + "|" +
                (spriteName ?? string.Empty) + "|" +
                Mathf.RoundToInt(spriteRect.x).ToString(CultureInfo.InvariantCulture) + "|" +
                Mathf.RoundToInt(spriteRect.y).ToString(CultureInfo.InvariantCulture) + "|" +
                Mathf.RoundToInt(spriteRect.width).ToString(CultureInfo.InvariantCulture) + "|" +
                Mathf.RoundToInt(spriteRect.height).ToString(CultureInfo.InvariantCulture);
        }
    }
}
