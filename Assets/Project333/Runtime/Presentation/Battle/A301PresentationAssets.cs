using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Presentation clocks only; combat packets and legality remain gateway-owned.
    internal static class A301PresentationAssets
    {
        internal const string CardId = "A-301";
        internal const float WindupDuration = 0.20f;
        internal const float BeamDuration = 0.20f;
        internal const float ImpactDuration = 0.24f;
        // Average emitter center in firing frames 2 and 3 of the applied 512px artwork.
        internal static readonly Vector2 EmitterPoint = new Vector2(349.08f / 512f, (512f - 441.44f) / 512f);
        private static AudioClip _attackSound;

        internal static AudioClip AttackSound
        {
            get
            {
                if (_attackSound == null)
                    _attackSound = Resources.Load<AudioClip>("Project333/Units/A-301/Audio/LaserShot");
                return _attackSound;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _attackSound = null; }
    }
}
