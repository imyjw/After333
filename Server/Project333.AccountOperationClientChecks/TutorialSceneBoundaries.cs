namespace UnityEngine.SceneManagement
{
    public readonly struct Scene
    {
        public string name { get; }
        public Scene(string sceneName) { name = sceneName; }
    }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        private static Scene s_current = new("ClientChecks");
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static event Action<Scene, Scene> activeSceneChanged;
        public static Scene GetActiveScene() => s_current;
        public static void SetActiveSceneName(string name)
        {
            var previous = s_current;
            s_current = new Scene(name);
            activeSceneChanged?.Invoke(previous, s_current);
        }
        public static void LoadSceneName(string name)
        {
            SetActiveSceneName(name);
            sceneLoaded?.Invoke(s_current, LoadSceneMode.Single);
        }
    }
}
