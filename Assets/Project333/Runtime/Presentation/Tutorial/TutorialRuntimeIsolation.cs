using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation.Tutorial
{
    // Entry intent closes the asynchronous account boundary before scene loading starts.
    // This is a runtime gate only; it never resets or writes the real account/session.
    public static class TutorialRuntimeIsolation
    {
        public const string SceneName = "Tutorial_VSlice";
        private static bool s_entryRequested;
        private static bool s_lastActive;

        public static bool IsActive => s_entryRequested ||
            string.Equals(SceneManager.GetActiveScene().name, SceneName, StringComparison.Ordinal);

        public static event Action<bool> Changed;

        public static void RequestEntry()
        {
            s_entryRequested = true;
            PublishCurrentState();
        }

        public static void CancelEntry()
        {
            s_entryRequested = false;
            PublishCurrentState();
        }

        internal static void ThrowIfActive()
        {
            if (IsActive) throw new OperationCanceledException("Account requests are suspended during the tutorial.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            s_entryRequested = false;
            s_lastActive = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            PublishCurrentState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RecognizeInitialScene() => PublishCurrentState();

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || string.Equals(scene.name, SceneName, StringComparison.Ordinal))
                s_entryRequested = false;
            PublishCurrentState();
        }

        private static void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            s_entryRequested = false;
            PublishCurrentState();
        }

        private static void PublishCurrentState()
        {
            var active = IsActive;
            if (s_lastActive == active) return;
            s_lastActive = active;
            Changed?.Invoke(active);
        }
    }
}
