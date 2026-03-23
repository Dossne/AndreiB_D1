using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SnakeGameController : MonoBehaviour
{
    private enum GameState
    {
        StartScreen,
        Playing,
        Won,
        Lost
    }

    private static readonly string[] SourceMazeRows =
    {
        "###############",
        "#P....#.......#",
        "#.###.#.#####.#",
        "#.....#.....#.#",
        "#.#####.###.#.#",
        "#.......#...#.#",
        "#.#####.#.###.#",
        "#.#...#.#.....#",
        "#.#.#.#.#####.#",
        "#...#.....G...#",
        "###############"
    };

    private readonly List<Vector2Int> snakeSegments = new();
    private readonly List<Vector2Int> previousSnakeSegments = new();
    private readonly List<Transform> segmentViews = new();
    private readonly Dictionary<Vector2Int, SpriteRenderer> dotViews = new();
    private readonly HashSet<Vector2Int> walls = new();
    private readonly List<Vector2Int> ghostOptions = new();
    private readonly List<string> mazeRows = new();

    private static Sprite cachedSquareSprite;
    private static Sprite cachedSnakeHeadSprite;
    private static Sprite cachedSnakeBodySprite;
    private static Sprite cachedSnakeTailSprite;

    private Transform boardRoot;
    private Transform dotsRoot;
    private Transform snakeRoot;
    private Transform ghostView;
    private Canvas uiCanvas;
    private Text titleText;
    private Text statusText;
    private Text scoreText;
    private Button primaryButton;
    private EventSystem eventSystem;

    private GameState gameState;
    private Vector2Int snakeDirection = Vector2Int.right;
    private Vector2Int queuedDirection = Vector2Int.right;
    private Vector2Int ghostDirection = Vector2Int.left;
    private Vector2Int playerStart;
    private Vector2Int ghostStart;
    private Vector2Int ghostPosition;
    private Vector2Int previousGhostPosition;
    private int width;
    private int height;
    private int pendingGrowth;
    private int totalDots;
    private int dotsSinceLastGrowth;
    private float moveTimer;
    private float ghostMoveTimer;
    private Vector3 ghostTargetPosition;
    private Vector2 swipeStartPosition;
    private bool isSwipeTracking;

    private const float MoveInterval = 0.22f;
    private const float GhostMoveInterval = 0.3f;
    private const float SwipeThreshold = 35f;
    private const int CorridorWidth = 2;

    private void Start()
    {
        BuildExpandedMaze();
        BuildWorld();
        BuildUi();
        ShowStartScreen();
    }

    private void Update()
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        ReadInput();

        moveTimer += Time.deltaTime;
        if (moveTimer >= MoveInterval)
        {
            moveTimer -= MoveInterval;
            StepSnake();
        }

        if (gameState != GameState.Playing)
        {
            return;
        }

        ghostMoveTimer += Time.deltaTime;
        if (ghostMoveTimer >= GhostMoveInterval)
        {
            ghostMoveTimer -= GhostMoveInterval;
            StepGhost();
        }

        UpdateVisualMotion();
    }

    private void BuildWorld()
    {
        width = mazeRows[0].Length;
        height = mazeRows.Count;

        boardRoot = new GameObject("Board").transform;
        boardRoot.SetParent(transform, false);

        dotsRoot = new GameObject("Dots").transform;
        dotsRoot.SetParent(transform, false);

        snakeRoot = new GameObject("Snake").transform;
        snakeRoot.SetParent(transform, false);

        BuildMaze();
        ConfigureCamera();
    }

    private void BuildMaze()
    {
        walls.Clear();
        dotViews.Clear();

        for (var row = 0; row < height; row++)
        {
            var y = height - 1 - row;
            for (var x = 0; x < width; x++)
            {
                var position = new Vector2Int(x, y);
                CreateCell(position, new Color(0.08f, 0.1f, 0.13f), "Floor", boardRoot, new Vector3(0.96f, 0.96f, 1f));

                switch (mazeRows[row][x])
                {
                    case '#':
                        walls.Add(position);
                        CreateCell(position, new Color(0.23f, 0.52f, 0.8f), "Wall", boardRoot);
                        break;
                    case '.':
                        dotViews[position] = CreateCell(position, new Color(0.99f, 0.9f, 0.45f), "Dot", dotsRoot, new Vector3(0.22f, 0.22f, 1f)).GetComponent<SpriteRenderer>();
                        break;
                    case 'P':
                        playerStart = position;
                        break;
                    case 'G':
                        ghostStart = position;
                        break;
                }
            }
        }

        totalDots = dotViews.Count;
    }

    private void StartRound()
    {
        gameState = GameState.Playing;
        moveTimer = 0f;
        ghostMoveTimer = 0f;
        pendingGrowth = 0;
        dotsSinceLastGrowth = 0;
        snakeDirection = Vector2Int.right;
        queuedDirection = Vector2Int.right;
        ghostDirection = Vector2Int.left;
        ghostPosition = ghostStart;
        previousGhostPosition = ghostStart;

        snakeSegments.Clear();
        snakeSegments.Add(playerStart);
        snakeSegments.Add(playerStart + Vector2Int.left);
        previousSnakeSegments.Clear();
        previousSnakeSegments.AddRange(snakeSegments);

        foreach (var view in segmentViews)
        {
            Destroy(view.gameObject);
        }

        segmentViews.Clear();

        foreach (var dot in dotViews.Values)
        {
            dot.enabled = true;
        }

        if (ghostView == null)
        {
            ghostView = CreateCell(ghostPosition, new Color(0.95f, 0.4f, 0.45f), "Ghost", transform, new Vector3(0.82f, 0.82f, 1f)).transform;
        }
        else
        {
            ghostView.gameObject.SetActive(true);
        }

        SyncSnakeVisuals();
        ghostView.position = GridToWorld(ghostPosition);
        ghostTargetPosition = ghostView.position;
        UpdateUi();
    }

    private void StepSnake()
    {
        if (queuedDirection + snakeDirection != Vector2Int.zero || snakeSegments.Count <= 1)
        {
            snakeDirection = queuedDirection;
        }

        previousSnakeSegments.Clear();
        previousSnakeSegments.AddRange(snakeSegments);

        var nextHead = snakeSegments[0] + snakeDirection;
        var tailPosition = snakeSegments[snakeSegments.Count - 1];
        var hitsBody = snakeSegments.Contains(nextHead) && (pendingGrowth > 0 || nextHead != tailPosition);
        if (walls.Contains(nextHead) || hitsBody)
        {
            SetGameState(GameState.Lost);
            return;
        }

        snakeSegments.Insert(0, nextHead);

        if (dotViews.TryGetValue(nextHead, out var dotView) && dotView.enabled)
        {
            dotView.enabled = false;
            dotsSinceLastGrowth++;
            if (dotsSinceLastGrowth >= 3)
            {
                pendingGrowth++;
                dotsSinceLastGrowth = 0;
            }

            UpdateUi();
        }

        if (pendingGrowth > 0)
        {
            pendingGrowth--;
        }
        else
        {
            snakeSegments.RemoveAt(snakeSegments.Count - 1);
        }

        if (AllDotsCollected())
        {
            SetGameState(GameState.Won);
            return;
        }

        SyncSnakeVisuals();

        if (nextHead == ghostPosition)
        {
            SetGameState(GameState.Lost);
        }
    }

    private void StepGhost()
    {
        ghostOptions.Clear();
        previousGhostPosition = ghostPosition;

        var directions = new[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (var direction in directions)
        {
            var nextPosition = ghostPosition + direction;
            if (walls.Contains(nextPosition))
            {
                continue;
            }

            ghostOptions.Add(direction);
        }

        if (ghostOptions.Count == 0)
        {
            return;
        }

        var bestDirection = ghostOptions[0];
        var bestDistance = int.MaxValue;

        foreach (var option in ghostOptions)
        {
            if (ghostOptions.Count > 1 && option == -ghostDirection)
            {
                continue;
            }

            var predictedHead = snakeSegments[0] + snakeDirection;
            var distance = Mathf.Abs((ghostPosition + option).x - predictedHead.x) + Mathf.Abs((ghostPosition + option).y - predictedHead.y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestDirection = option;
            }
        }

        ghostDirection = bestDirection;
        ghostPosition += ghostDirection;
        ghostTargetPosition = GridToWorld(ghostPosition);

        if (ghostPosition == snakeSegments[0])
        {
            SetGameState(GameState.Lost);
        }
    }

    private void SyncSnakeVisuals()
    {
        EnsureSnakeVisualCount();

        for (var i = 0; i < snakeSegments.Count; i++)
        {
            var view = segmentViews[i];
            var renderer = view.GetComponent<SpriteRenderer>();
            view.name = $"Segment_{i}";
            renderer.color = Color.white;
            renderer.sprite = GetSnakeSegmentSprite(i);
            view.rotation = GetSnakeSegmentRotation(i);
        }
    }

    private void UpdateVisualMotion()
    {
        var snakeLerp = Mathf.Clamp01(moveTimer / MoveInterval);
        for (var i = 0; i < snakeSegments.Count && i < segmentViews.Count; i++)
        {
            var fromGrid = i == 0
                ? previousSnakeSegments[0]
                : previousSnakeSegments[Mathf.Min(i - 1, previousSnakeSegments.Count - 1)];
            var toGrid = snakeSegments[i];
            segmentViews[i].position = Vector3.Lerp(
                GridToWorld(fromGrid),
                GridToWorld(toGrid),
                snakeLerp);
        }

        if (ghostView != null)
        {
            var ghostLerp = Mathf.Clamp01(ghostMoveTimer / GhostMoveInterval);
            ghostView.position = Vector3.Lerp(
                GridToWorld(previousGhostPosition),
                ghostTargetPosition,
                ghostLerp);
        }
    }

    private void EnsureSnakeVisualCount()
    {
        while (segmentViews.Count < snakeSegments.Count)
        {
            var spawnPosition = segmentViews.Count > 0
                ? segmentViews[segmentViews.Count - 1].position
                : GridToWorld(snakeSegments[segmentViews.Count]);

            var view = CreateCell(
                snakeSegments[Mathf.Min(segmentViews.Count, snakeSegments.Count - 1)],
                Color.white,
                $"Segment_{segmentViews.Count}",
                snakeRoot,
                new Vector3(0.84f, 0.84f, 1f)).transform;

            view.position = spawnPosition;
            segmentViews.Add(view);
        }
    }

    private void ReadInput()
    {
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                swipeStartPosition = touch.position;
                isSwipeTracking = true;
            }
            else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) && isSwipeTracking)
            {
                ApplySwipeDirection(touch.position - swipeStartPosition);
                isSwipeTracking = false;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            swipeStartPosition = Input.mousePosition;
            isSwipeTracking = true;
        }
        else if (Input.GetMouseButtonUp(0) && isSwipeTracking)
        {
            ApplySwipeDirection((Vector2)Input.mousePosition - swipeStartPosition);
            isSwipeTracking = false;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            queuedDirection = Vector2Int.up;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            queuedDirection = Vector2Int.down;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            queuedDirection = Vector2Int.left;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            queuedDirection = Vector2Int.right;
        }
    }

    private bool AllDotsCollected()
    {
        foreach (var dot in dotViews.Values)
        {
            if (dot.enabled)
            {
                return false;
            }
        }

        return true;
    }

    private void ShowStartScreen()
    {
        gameState = GameState.StartScreen;
        UpdateUi();
    }

    private void SetGameState(GameState newState)
    {
        gameState = newState;
        if (ghostView != null)
        {
            ghostView.gameObject.SetActive(newState == GameState.Playing);
        }

        UpdateUi();
    }

    private void ConfigureCamera()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = new GameObject("Main Camera").AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
        }

        mainCamera.orthographic = true;
        mainCamera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);
        mainCamera.transform.position = new Vector3((width - 1) * 0.5f, (height - 1) * 0.5f, -10f);
        mainCamera.orthographicSize = Mathf.Max(height * 0.55f, width * 0.34f);
    }

    private GameObject CreateCell(Vector2Int position, Color color, string objectName, Transform parent, Vector3? scale = null)
    {
        var gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = GridToWorld(position);
        gameObject.transform.localScale = scale ?? Vector3.one;

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite();
        renderer.color = color;

        return gameObject;
    }

    private Vector3 GridToWorld(Vector2Int position)
    {
        return new Vector3(position.x, position.y, 0f);
    }

    private static Sprite CreateSquareSprite()
    {
        if (cachedSquareSprite == null)
        {
            cachedSquareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        return cachedSquareSprite;
    }

    private Sprite GetSnakeSegmentSprite(int index)
    {
        if (index == 0)
        {
            return CreateSnakeHeadSprite();
        }

        if (index == snakeSegments.Count - 1)
        {
            return CreateSnakeTailSprite();
        }

        return CreateSnakeBodySprite();
    }

    private Quaternion GetSnakeSegmentRotation(int index)
    {
        Vector2Int direction;

        if (index == 0)
        {
            if (snakeSegments.Count == 1)
            {
                direction = snakeDirection;
            }
            else
            {
                direction = snakeSegments[0] - snakeSegments[1];
            }
        }
        else if (index == snakeSegments.Count - 1)
        {
            direction = snakeSegments[snakeSegments.Count - 2] - snakeSegments[snakeSegments.Count - 1];
        }
        else
        {
            direction = snakeSegments[index - 1] - snakeSegments[index + 1];
        }

        return Quaternion.Euler(0f, 0f, DirectionToAngle(direction));
    }

    private static float DirectionToAngle(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            return 90f;
        }

        if (direction == Vector2Int.left)
        {
            return 180f;
        }

        if (direction == Vector2Int.down)
        {
            return 270f;
        }

        return 0f;
    }

    private static Sprite CreateSnakeHeadSprite()
    {
        if (cachedSnakeHeadSprite == null)
        {
            cachedSnakeHeadSprite = CreateSnakeSprite(texture =>
            {
                var bodyColor = new Color32(110, 215, 90, 255);
                var eyeColor = new Color32(28, 33, 35, 255);
                var tongueColor = new Color32(250, 95, 95, 255);

                FillRect(texture, 2, 2, 12, 12, bodyColor);
                FillRect(texture, 10, 5, 3, 2, eyeColor);
                FillRect(texture, 10, 9, 3, 2, eyeColor);
                FillRect(texture, 13, 7, 2, 2, tongueColor);
            });
        }

        return cachedSnakeHeadSprite;
    }

    private static Sprite CreateSnakeBodySprite()
    {
        if (cachedSnakeBodySprite == null)
        {
            cachedSnakeBodySprite = CreateSnakeSprite(texture =>
            {
                var bodyColor = new Color32(78, 176, 70, 255);
                var stripeColor = new Color32(128, 224, 112, 255);

                FillRect(texture, 2, 2, 12, 12, bodyColor);
                FillRect(texture, 5, 4, 6, 8, stripeColor);
            });
        }

        return cachedSnakeBodySprite;
    }

    private static Sprite CreateSnakeTailSprite()
    {
        if (cachedSnakeTailSprite == null)
        {
            cachedSnakeTailSprite = CreateSnakeSprite(texture =>
            {
                var bodyColor = new Color32(62, 152, 56, 255);

                FillRect(texture, 2, 4, 8, 8, bodyColor);
                FillRect(texture, 9, 5, 3, 6, bodyColor);
                FillRect(texture, 12, 6, 2, 4, bodyColor);
            });
        }

        return cachedSnakeTailSprite;
    }

    private static Sprite CreateSnakeSprite(System.Action<Texture2D> painter)
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var transparent = new Color32(0, 0, 0, 0);
        var pixels = new Color32[16 * 16];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        texture.SetPixels32(pixels);
        painter(texture);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color32 color)
    {
        for (var x = startX; x < startX + width; x++)
        {
            for (var y = startY; y < startY + height; y++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private void BuildExpandedMaze()
    {
        mazeRows.Clear();

        foreach (var sourceRow in SourceMazeRows)
        {
            var expandedRows = new char[CorridorWidth][];
            for (var expandedRowIndex = 0; expandedRowIndex < CorridorWidth; expandedRowIndex++)
            {
                expandedRows[expandedRowIndex] = new string('#', sourceRow.Length * CorridorWidth).ToCharArray();
            }

            for (var sourceColumnIndex = 0; sourceColumnIndex < sourceRow.Length; sourceColumnIndex++)
            {
                var sourceCell = sourceRow[sourceColumnIndex];
                for (var offsetY = 0; offsetY < CorridorWidth; offsetY++)
                {
                    for (var offsetX = 0; offsetX < CorridorWidth; offsetX++)
                    {
                        expandedRows[offsetY][sourceColumnIndex * CorridorWidth + offsetX] = GetExpandedCellValue(sourceCell, offsetX, offsetY);
                    }
                }
            }

            for (var expandedRowIndex = 0; expandedRowIndex < CorridorWidth; expandedRowIndex++)
            {
                mazeRows.Add(new string(expandedRows[expandedRowIndex]));
            }
        }
    }

    private static char GetExpandedCellValue(char sourceCell, int offsetX, int offsetY)
    {
        switch (sourceCell)
        {
            case '#':
                return '#';
            case '.':
                return '.';
            case 'P':
                return offsetX == 0 && offsetY == 0 ? 'P' : '.';
            case 'G':
                return offsetX == 0 && offsetY == 0 ? 'G' : '.';
            default:
                return '.';
        }
    }

    private void BuildUi()
    {
        eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        uiCanvas = new GameObject("UI").AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        uiCanvas.gameObject.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        scoreText = CreateText("Score", new Vector2(0.5f, 0.93f), font, 22, TextAnchor.MiddleCenter);
        titleText = CreateText("Title", new Vector2(0.5f, 0.79f), font, 34, TextAnchor.MiddleCenter);
        statusText = CreateText("Status", new Vector2(0.5f, 0.69f), font, 20, TextAnchor.MiddleCenter);
        primaryButton = CreateButton(font);
        primaryButton.onClick.AddListener(StartRound);
    }

    private void UpdateUi()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = $"Dots: {totalDots - RemainingDots()}/{totalDots}";

        switch (gameState)
        {
            case GameState.StartScreen:
                titleText.text = "Maze Snake";
                statusText.text = "Swipe or use keyboard to turn.\nCollect all dots and avoid the ghost.";
                primaryButton.GetComponentInChildren<Text>().text = "Start";
                primaryButton.gameObject.SetActive(true);
                break;
            case GameState.Playing:
                titleText.text = "Playing";
                statusText.text = "Collect every dot.";
                primaryButton.GetComponentInChildren<Text>().text = "Restart";
                primaryButton.gameObject.SetActive(true);
                break;
            case GameState.Won:
                titleText.text = "You Win";
                statusText.text = "All dots collected.";
                primaryButton.GetComponentInChildren<Text>().text = "Restart";
                primaryButton.gameObject.SetActive(true);
                break;
            case GameState.Lost:
                titleText.text = "Game Over";
                statusText.text = "A ghost touched the snake head.";
                primaryButton.GetComponentInChildren<Text>().text = "Restart";
                primaryButton.gameObject.SetActive(true);
                break;
        }
    }

    private int RemainingDots()
    {
        var remaining = 0;
        foreach (var dot in dotViews.Values)
        {
            if (dot.enabled)
            {
                remaining++;
            }
        }

        return remaining;
    }

    private void ApplySwipeDirection(Vector2 delta)
    {
        if (delta.magnitude < SwipeThreshold)
        {
            return;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            queuedDirection = delta.x > 0f ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            queuedDirection = delta.y > 0f ? Vector2Int.up : Vector2Int.down;
        }
    }

    private Text CreateText(string objectName, Vector2 anchor, Font font, int size, TextAnchor alignment)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(uiCanvas.transform, false);

        var rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = new Vector2(720f, 80f);

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private Button CreateButton(Font font)
    {
        var buttonObject = new GameObject("PrimaryButton");
        buttonObject.transform.SetParent(uiCanvas.transform, false);

        var rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.12f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.12f);
        rectTransform.sizeDelta = new Vector2(220f, 54f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.13f, 0.58f, 0.31f, 0.92f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = new GameObject("Label");
        label.transform.SetParent(buttonObject.transform, false);

        var labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = label.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 22;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = "Start";

        return button;
    }
}
