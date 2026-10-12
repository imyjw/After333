using System;
using UnityEngine;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed class TutorialPracticeInputBlocker : MonoBehaviour, ICanvasRaycastFilter
    {
        public Func<Vector2, Camera, bool> AllowsPointer { private get; set; }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            AllowsPointer == null || !AllowsPointer(screenPoint, eventCamera);
    }
}
