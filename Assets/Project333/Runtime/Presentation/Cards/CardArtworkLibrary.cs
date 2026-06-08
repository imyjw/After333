using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Cards
{
    public static class CardArtworkLibrary
    {
        private const string ResourceFolder = "Project333/CardArtwork";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingCardIds = new HashSet<string>(StringComparer.Ordinal);

        public static bool TryGetArtwork(string cardId, out Sprite artwork)
        {
            artwork = null;

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            if (SpriteCache.TryGetValue(cardId, out artwork))
            {
                return artwork != null;
            }

            if (MissingCardIds.Contains(cardId))
            {
                return false;
            }

            artwork = LoadArtwork(cardId);
            if (artwork == null)
            {
                MissingCardIds.Add(cardId);
                return false;
            }

            SpriteCache[cardId] = artwork;
            return true;
        }

        public static void ClearCacheForTests()
        {
            SpriteCache.Clear();
            MissingCardIds.Clear();
        }

        private static Sprite LoadArtwork(string cardId)
        {
            var resourcePath = $"{ResourceFolder}/{cardId}";

            var importedSprite = Resources.Load<Sprite>(resourcePath);
            if (importedSprite != null)
            {
                return importedSprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = $"{cardId}_ArtworkSprite";
            return sprite;
        }
    }
}
