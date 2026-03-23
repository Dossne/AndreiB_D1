using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string LevelSceneName = "SampleScene";
    private static readonly Color MenuBackgroundColor = new(0.12f, 0.18f, 0.27f, 1f);

    private void Start()
    {
        BuildMenu();
    }

    private void BuildMenu()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
        }

        camera.orthographic = true;
        camera.backgroundColor = MenuBackgroundColor;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        var canvas = new GameObject("UI").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        var font = LoadUiFont();

        CreatePanel(canvas.transform);
        CreateText(canvas.transform, "Title", "Змейка-лабиринт", new Vector2(0.5f, 0.74f), font, 42, TextAnchor.MiddleCenter, new Vector2(760f, 90f));
        CreateText(canvas.transform, "Subtitle", "Собери все точки и выживи в лабиринте с призраками.", new Vector2(0.5f, 0.64f), font, 18, TextAnchor.MiddleCenter, new Vector2(760f, 70f));

        var startButton = CreateButton(canvas.transform, font, "Старт", new Vector2(0.34f, 0.42f), new Vector2(240f, 62f));
        startButton.onClick.AddListener(() => SceneManager.LoadScene(LevelSceneName));

        BuildLeaderboard(canvas.transform, font);
    }

    private static void CreatePanel(Transform parent)
    {
        var panel = new GameObject("Panel");
        panel.transform.SetParent(parent, false);

        var rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(860f, 360f);
        rectTransform.anchoredPosition = new Vector2(0f, 20f);

        var image = panel.AddComponent<Image>();
        image.color = MenuBackgroundColor;
    }

    private static Text CreateText(Transform parent, string objectName, string value, Vector2 anchor, Font font, int fontSize, TextAnchor alignment, Vector2 sizeDelta)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        var rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = sizeDelta;

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private static Button CreateButton(Transform parent, Font font, string label, Vector2 anchor, Vector2 sizeDelta)
    {
        var buttonObject = new GameObject($"{label}Button");
        buttonObject.transform.SetParent(parent, false);

        var rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = sizeDelta;

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.62f, 0.34f, 1f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelObject.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 24;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
    }

    private static void BuildLeaderboard(Transform parent, Font font)
    {
        var panel = new GameObject("LeaderboardPanel");
        panel.transform.SetParent(parent, false);

        var rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.72f, 0.4f);
        rectTransform.anchorMax = new Vector2(0.72f, 0.4f);
        rectTransform.sizeDelta = new Vector2(280f, 220f);
        rectTransform.anchoredPosition = new Vector2(0f, -20f);

        var image = panel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.12f, 0.19f, 0.92f);

        var leaderboardTitle = CreateText(panel.transform, "LeaderboardTitle", "Лидерборд", new Vector2(0.5f, 0.85f), font, 24, TextAnchor.MiddleCenter, new Vector2(240f, 40f));
        leaderboardTitle.rectTransform.anchoredPosition = new Vector2(0f, 10f);

        var scores = LeaderboardStorage.GetTopScores();
        var lines = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            lines.Add(i < scores.Count ? $"{i + 1}. {scores[i]}" : $"{i + 1}. ---");
        }

        CreateText(panel.transform, "LeaderboardEntries", string.Join("\n", lines), new Vector2(0.5f, 0.42f), font, 18, TextAnchor.UpperCenter, new Vector2(240f, 180f));
    }

    private static Font LoadUiFont()
    {
        var preferredFonts = new[]
        {
            "Segoe UI",
            "Trebuchet MS",
            "Calibri",
            "Arial"
        };

        foreach (var fontName in preferredFonts)
        {
            var font = Font.CreateDynamicFontFromOSFont(fontName, 16);
            if (font != null)
            {
                return font;
            }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
