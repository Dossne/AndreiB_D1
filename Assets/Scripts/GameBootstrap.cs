using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameBootstrap
{
    private static bool isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        isInitialized = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        EnsureGameControllerExists(scene.name);
    }

    private static void EnsureGameControllerExists(string activeSceneName)
    {
        if (activeSceneName == "MainMenu")
        {
            if (Object.FindFirstObjectByType<MainMenuController>() == null)
            {
                var menuRoot = new GameObject("MainMenu");
                menuRoot.AddComponent<MainMenuController>();
            }

            return;
        }

        if (Object.FindFirstObjectByType<SnakeGameController>() != null)
        {
            return;
        }

        var root = new GameObject("SnakeGame");
        root.AddComponent<SnakeGameController>();
    }
}
