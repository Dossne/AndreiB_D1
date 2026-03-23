using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameControllerExists()
    {
        if (Object.FindFirstObjectByType<SnakeGameController>() != null)
        {
            return;
        }

        var root = new GameObject("SnakeGame");
        root.AddComponent<SnakeGameController>();
    }
}
