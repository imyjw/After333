using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    internal static class GaebangChiefPresentationAssets
    {
        internal const string CardId = "GaebangChief";
        internal const float WindupDuration = 3f / 12f;
        internal const float ImpactDuration = 8f / 16f;
        internal const float ApproachDuration = 0.28f;
        internal const float ReturnDuration = 0.22f;
        private static Sprite[] _impact;
        private static AudioClip _sound;

        internal static IReadOnlyList<Sprite> ImpactFrames
        {
            get
            {
                if (_impact != null) return _impact;
                // Sprites and native slicing are imported by the one-time authoring tool.
                _impact = Resources.LoadAll<Sprite>("Project333/Units/GaebangChief/CoinImpactAtlas");
                if (_impact.Length == 0) { _impact = null; return null; }
                System.Array.Sort(_impact, (a, b) => string.CompareOrdinal(a.name, b.name));
                return _impact;
            }
        }

        internal static AudioClip AttackSound
        {
            get
            {
                if (_sound == null) _sound = Resources.Load<AudioClip>("Project333/Units/GaebangChief/Audio/CoinPunch");
                return _sound;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _impact = null; _sound = null; }
    }
}
