using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    internal static class TrapPresentationAssets
    {
        internal const string HiddenArtworkResourcePath = "Project333/CardArtwork/TrapHidden";
        private static Sprite s_hiddenArtwork;

        internal static Sprite HiddenArtwork
        {
            get
            {
                if (s_hiddenArtwork == null) s_hiddenArtwork = Resources.Load<Sprite>(HiddenArtworkResourcePath);
                return s_hiddenArtwork;
            }
        }
    }
}
