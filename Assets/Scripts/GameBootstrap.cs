using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameControllerExists()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;
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
