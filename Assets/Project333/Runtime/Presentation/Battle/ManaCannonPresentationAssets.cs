using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation clocks only. Legality, resources and damage belong to the shared gateway.
    internal static class ManaCannonPresentationAssets
    {
        internal const string CardId = "ManaCannon";
        internal const float WindupDuration = 3f / 10f;
        internal const float FlightDuration = .35f;
        internal const float ImpactDuration = 8f / 18f;
        internal const float ContactAt = WindupDuration + FlightDuration;
        internal const float TotalDuration = ContactAt + ImpactDuration;
        // Measured blue muzzle (Attack_3) and registered white projectile head (Frame_0) in packed 512px cells.
        internal static readonly Vector2 EmitterPoint = new Vector2(.700727865f, .404778212f);
        internal static readonly Vector2 ProjectileCorePoint = new Vector2(.768758039f, .400744872f);
        private static Sprite[] _projectile, _impact;
        private static AudioClip _attackSound, _impactSound;

        internal static IReadOnlyList<Sprite> ProjectileFrames => LoadFrames(ref _projectile, "ProjectileAtlas");
        internal static IReadOnlyList<Sprite> ImpactFrames => LoadFrames(ref _impact, "ExplosionAtlas");
        private static Sprite[] LoadFrames(ref Sprite[] cache, string name)
        {
            if (cache != null) return cache;
            cache = Resources.LoadAll<Sprite>("Project333/Units/ManaCannon/" + name);
            if (cache.Length == 0) { cache = null; return null; }
            System.Array.Sort(cache, (a, b) => string.CompareOrdinal(a.name, b.name));
            return cache;
        }
        internal static AudioClip AttackSound
        {
            get
            {
                if (_attackSound == null) _attackSound = Resources.Load<AudioClip>("Project333/Units/ManaCannon/Audio/ManaShot");
                return _attackSound;
            }
        }
        internal static AudioClip ImpactSound
        {
            get
            {
                if (_impactSound == null) _impactSound = Resources.Load<AudioClip>("Project333/Units/ManaCannon/Audio/ManaExplosion");
                return _impactSound;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _projectile = _impact = null; _attackSound = _impactSound = null; }
    }
}
