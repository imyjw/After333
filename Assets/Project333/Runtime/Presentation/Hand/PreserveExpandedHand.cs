using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    // Explicit exception for controls that keep the battle hand expanded.
    [DisallowMultipleComponent]
    public sealed class PreserveExpandedHand : MonoBehaviour
    {
        public static void Attach(GameObject target)
        {
            if (target != null && target.GetComponent<PreserveExpandedHand>() == null)
                target.AddComponent<PreserveExpandedHand>();
        }
    }
}