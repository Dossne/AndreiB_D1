using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SnakeGameController : MonoBehaviour
{
    private class GhostAgent
    {
        public Transform View;
        public Vector2Int Position;
        public Vector2Int PreviousPosition;
        public Vector2Int Direction = Vector2Int.left;
        public float StunTimer;
        public Vector3 TargetPosition;
    }

    private enum GameState
    {
        StartScreen,
        Playing,
        Paused,
        Won,
        Lost
    }

    private static readonly string[] PauseQuotes =
    {
        "Если у тебя мало хп — не умирай.",
        "Если хочешь выиграть — постарайся не проиграть.",
        "Если темно — включи свет.",
        "Если персонаж медленный — он не быстрый.",
        "Если игра лагает — она лагает не просто так.",
        "Если тебе скучно — тебе скучно.",
        "Если хочешь пройти уровень — дойди до конца уровня.",
        "Если враг сильнее — он сильнее тебя.",
        "Если кнопка не нажимается — значит, она не нажимается.",
        "Если персонаж упал — он больше не стоит.",
        "Если не знаешь, куда идти — ты не знаешь, куда идти.",
        "Если хочешь победить босса — убей босса.",
        "Если играешь плохо — играешь плохо.",
        "Если тебя убили — значит, ты проиграл этот момент.",
        "Если устал — отдохни.",
        "Если хочешь жить лучше — живи лучше.",
        "Если экран чёрный — ты ничего не видишь.",
        "Если прыгаешь — ты находишься в воздухе.",
        "Если не нажал кнопку — она не нажмётся.",
        "Если игра не запущена — ты в неё не играешь.",
        "Если враг исчез — его больше нет на экране.",
        "Если стоишь — ты не идёшь.",
        "Если идёшь — ты уже не стоишь.",
        "Если персонаж умер дважды — второй раз он тоже умер.",
        "Если победил — ты не проиграл.",
        "Если проиграл — ты не победил.",
        "Если ничего не делаешь — ничего и не происходит.",
        "Если открыл дверь — она теперь открыта.",
        "Если закрыл дверь — она теперь закрыта.",
        "Если не попал — значит, ты промахнулся.",
        "Если попал — значит, не промахнулся.",
        "Если загрузка идёт — она загружается.",
        "Если игра закончилась — играть дальше нельзя (пока не начнёшь заново).",
        "Если ты здесь — значит, ты не там."
    };

    private static readonly string[] SourceMazeRows =
    {
        "#####################",
        "#P....#.....#...#...#",
        "#.###.#.###.#.#.#.#.#",
        "#...#...#...#.#...#.#",
        "###.#####.###.#####.#",
        "#...#.....#...#.....#",
        "#.###.###.#.###.###.#",
        "#.#...#...#.....#...#",
        "#.#.###.#####.###.#.#",
        "#...#.....#...#...#.#",
        "#.#####.#.#.###.###.#",
        "#.....#.#.#...#.....#",
        "#.###.#.#.###.#####.#",
        "#...#...#.....#...#.#",
        "###.#########.#.#.#.#",
        "#.....#.....#...#G..#",
        "#####################"
    };

    private readonly List<Vector2Int> snakeSegments = new();
    private readonly List<Vector2Int> previousSnakeSegments = new();
    private readonly List<Transform> segmentViews = new();
    private readonly Dictionary<Vector2Int, SpriteRenderer> dotViews = new();
    private readonly HashSet<Vector2Int> walls = new();
    private readonly List<Vector2Int> ghostOptions = new();
    private readonly List<Vector2Int> ghostSpawnCandidates = new();
    private readonly List<GhostAgent> ghosts = new();
    private readonly List<string> mazeRows = new();

    private static Sprite cachedSquareSprite;
    private static Sprite cachedSnakeHeadSprite;
    private static Sprite cachedSnakeBodySprite;
    private static Sprite cachedSnakeTailSprite;
    private static Sprite cachedGhostSprite;
    private static Sprite cachedPauseSprite;

    private Transform boardRoot;
    private Transform dotsRoot;
    private Transform snakeRoot;
    private Canvas uiCanvas;
    private Text titleText;
    private Text statusText;
    private Text scoreText;
    private Button menuButton;
    private GameObject lossPopup;
    private GameObject pausePopup;
    private Text lossTitleText;
    private Text lossScoreText;
    private Text pauseQuoteText;
    private Button retryButton;
    private Button continueButton;
    private Button exitToMenuButton;
    private Button pauseExitToMenuButton;
    private EventSystem eventSystem;

    private GameState gameState;
    private Vector2Int snakeDirection = Vector2Int.right;
    private Vector2Int queuedDirection = Vector2Int.right;
    private Vector2Int playerStart;
    private int width;
    private int height;
    private int pendingGrowth;
    private int totalDots;
    private int dotsSinceLastGrowth;
    private int score;
    private int lastAttemptScore;
    private float moveTimer;
    private float ghostMoveTimer;
    private Vector2 swipeStartPosition;
    private bool isSwipeTracking;

    private const float MoveInterval = 0.22f;
    private const float GhostMoveInterval = 0.3f;
    private const float GhostStunDuration = 2f;
    private const float SwipeThreshold = 35f;
    private const int CorridorWidth = 2;
    private const int GhostCount = 3;
    private const int MinGhostSpawnDistance = 10;
    private const int DotsPerGrowth = 5;
    private static readonly Vector3 SnakeSegmentScale = new(0.94f, 0.94f, 1f);
    private static readonly Vector3 GhostScale = new(0.96f, 0.96f, 1f);

    private void Start()
    {
        BuildExpandedMaze();
        BuildWorld();
        BuildUi();
        StartRound();
    }

    private void Update()
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        ReadInput();

        moveTimer += Time.deltaTime;
        UpdateGhostTimers();

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
            StepGhosts();
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
        ghostSpawnCandidates.Clear();

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
                    case 'P':
                        playerStart = position;
                        ghostSpawnCandidates.Add(position);
                        break;
                    case 'G':
                    case '.':
                        ghostSpawnCandidates.Add(position);
                        break;
                }
            }
        }
    }

    private void StartRound()
    {
        gameState = GameState.Playing;
        moveTimer = 0f;
        ghostMoveTimer = 0f;
        pendingGrowth = 0;
        dotsSinceLastGrowth = 0;
        score = 0;
        snakeDirection = Vector2Int.right;
        queuedDirection = Vector2Int.right;

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

        ResetDots();

        foreach (var ghost in ghosts)
        {
            if (ghost.View != null)
            {
                Destroy(ghost.View.gameObject);
            }
        }

        ghosts.Clear();
        SpawnGhosts();

        SyncSnakeVisuals();
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
            score++;
            dotsSinceLastGrowth++;
            if (dotsSinceLastGrowth >= DotsPerGrowth)
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

        for (var i = 0; i < ghosts.Count; i++)
        {
            if (nextHead == ghosts[i].Position)
            {
                SetGameState(GameState.Lost);
                return;
            }
        }
    }

    private void StepGhosts()
    {
        var reservedPositions = new HashSet<Vector2Int>();
        for (var i = 0; i < ghosts.Count; i++)
        {
            reservedPositions.Add(ghosts[i].Position);
        }

        for (var ghostIndex = 0; ghostIndex < ghosts.Count; ghostIndex++)
        {
            var ghost = ghosts[ghostIndex];
            reservedPositions.Remove(ghost.Position);

            if (ghost.StunTimer > 0f)
            {
                reservedPositions.Add(ghost.Position);
                continue;
            }

            ghost.PreviousPosition = ghost.Position;
            var nextGhostPosition = FindNextGhostPosition(ghost, reservedPositions);

            if (nextGhostPosition == snakeSegments[0])
            {
                ghost.Position = nextGhostPosition;
                ghost.TargetPosition = GridToWorld(ghost.Position);
                reservedPositions.Add(ghost.Position);
                SetGameState(GameState.Lost);
                return;
            }

            if (HitsSnakeBody(nextGhostPosition))
            {
                ghost.StunTimer = GhostStunDuration;
                ghost.TargetPosition = GridToWorld(ghost.Position);
                UpdateGhostVisual(ghost);
                reservedPositions.Add(ghost.Position);
                continue;
            }

            ghost.Direction = nextGhostPosition - ghost.Position;
            ghost.Position = nextGhostPosition;
            ghost.TargetPosition = GridToWorld(ghost.Position);
            UpdateGhostVisual(ghost);
            reservedPositions.Add(ghost.Position);
        }
    }

    private Vector2Int FindNextGhostPosition(GhostAgent ghost, HashSet<Vector2Int> reservedPositions)
    {
        var target = snakeSegments[0];
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int> { ghost.Position };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(ghost.Position);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target)
            {
                break;
            }

            foreach (var direction in GetCardinalDirections())
            {
                var next = current + direction;
                if (visited.Contains(next) || walls.Contains(next) || reservedPositions.Contains(next))
                {
                    continue;
                }

                if (HitsSnakeBody(next))
                {
                    continue;
                }

                visited.Add(next);
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!visited.Contains(target))
        {
            return FindFallbackGhostPosition(ghost, reservedPositions);
        }

        var step = target;
        while (cameFrom.TryGetValue(step, out var previous) && previous != ghost.Position)
        {
            step = previous;
        }

        return step;
    }

    private Vector2Int FindFallbackGhostPosition(GhostAgent ghost, HashSet<Vector2Int> reservedPositions)
    {
        ghostOptions.Clear();

        foreach (var direction in GetCardinalDirections())
        {
            var nextPosition = ghost.Position + direction;
            if (walls.Contains(nextPosition) || reservedPositions.Contains(nextPosition))
            {
                continue;
            }

            ghostOptions.Add(nextPosition);
        }

        if (ghostOptions.Count == 0)
        {
            return ghost.Position;
        }

        var bestPosition = ghostOptions[0];
        var bestDistance = int.MaxValue;
        for (var i = 0; i < ghostOptions.Count; i++)
        {
            var distance = Mathf.Abs(ghostOptions[i].x - snakeSegments[0].x) + Mathf.Abs(ghostOptions[i].y - snakeSegments[0].y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPosition = ghostOptions[i];
            }
        }

        return bestPosition;
    }

    private static Vector2Int[] GetCardinalDirections()
    {
        return new[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };
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
            renderer.sortingOrder = 6;
            view.rotation = GetSnakeSegmentRotation(i);
        }
    }

    private void UpdateVisualMotion()
    {
        var snakeLerp = Mathf.Clamp01(moveTimer / MoveInterval);
        for (var i = 0; i < snakeSegments.Count && i < segmentViews.Count; i++)
        {
            var fromGrid = i < previousSnakeSegments.Count
                ? previousSnakeSegments[i]
                : previousSnakeSegments[previousSnakeSegments.Count - 1];
            var toGrid = snakeSegments[i];
            segmentViews[i].position = Vector3.Lerp(
                GridToWorld(fromGrid),
                GridToWorld(toGrid),
                snakeLerp);
        }

        for (var i = 0; i < ghosts.Count; i++)
        {
            var ghostLerp = Mathf.Clamp01(ghostMoveTimer / GhostMoveInterval);
            ghosts[i].View.position = Vector3.Lerp(
                GridToWorld(ghosts[i].PreviousPosition),
                ghosts[i].TargetPosition,
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
                SnakeSegmentScale).transform;

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
        if (newState == GameState.Lost)
        {
            lastAttemptScore = score;
            LeaderboardStorage.RecordScore(lastAttemptScore);
        }

        gameState = newState;
        var showGhosts = newState == GameState.Playing || newState == GameState.Paused;
        for (var i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].View.gameObject.SetActive(showGhosts);
        }

        UpdateUi();
    }

    private bool HitsSnakeBody(Vector2Int position)
    {
        for (var i = 1; i < snakeSegments.Count; i++)
        {
            if (snakeSegments[i] == position)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateGhostVisual(GhostAgent ghost)
    {
        if (ghost.View == null)
        {
            return;
        }

        var ghostRenderer = ghost.View.GetComponent<SpriteRenderer>();
        ghostRenderer.color = ghost.StunTimer > 0f
            ? new Color(0.7f, 0.85f, 1f, 1f)
            : Color.white;
    }

    private void UpdateGhostTimers()
    {
        for (var i = 0; i < ghosts.Count; i++)
        {
            if (ghosts[i].StunTimer <= 0f)
            {
                continue;
            }

            ghosts[i].StunTimer = Mathf.Max(0f, ghosts[i].StunTimer - Time.deltaTime);
            UpdateGhostVisual(ghosts[i]);
        }
    }

    private void SpawnGhosts()
    {
        var availablePositions = new List<Vector2Int>(ghostSpawnCandidates);
        availablePositions.Remove(playerStart);
        availablePositions.Remove(playerStart + Vector2Int.left);

        var preferredPositions = new List<Vector2Int>();
        var fallbackPositions = new List<Vector2Int>();
        for (var i = 0; i < availablePositions.Count; i++)
        {
            if (ManhattanDistance(availablePositions[i], playerStart) >= MinGhostSpawnDistance)
            {
                preferredPositions.Add(availablePositions[i]);
            }
            else
            {
                fallbackPositions.Add(availablePositions[i]);
            }
        }

        for (var ghostIndex = 0; ghostIndex < GhostCount; ghostIndex++)
        {
            var sourceList = preferredPositions.Count > 0 ? preferredPositions : fallbackPositions;
            if (sourceList.Count == 0)
            {
                break;
            }

            var spawnIndex = Random.Range(0, sourceList.Count);
            var spawnPosition = sourceList[spawnIndex];
            sourceList.RemoveAt(spawnIndex);

            var ghost = new GhostAgent
            {
                Position = spawnPosition,
                PreviousPosition = spawnPosition,
                TargetPosition = GridToWorld(spawnPosition)
            };

            ghost.View = CreateCell(spawnPosition, Color.white, $"Ghost_{ghostIndex}", transform, GhostScale).transform;
            var ghostRenderer = ghost.View.GetComponent<SpriteRenderer>();
            ghostRenderer.sprite = CreateGhostSprite();
            ghostRenderer.color = Color.white;
            ghostRenderer.sortingOrder = 5;

            ghosts.Add(ghost);
            UpdateGhostVisual(ghost);
        }
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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
                var outlineColor = new Color32(24, 72, 36, 255);
                var bodyColor = new Color32(132, 232, 96, 255);
                var eyeColor = new Color32(28, 33, 35, 255);
                var tongueColor = new Color32(250, 95, 95, 255);

                FillRect(texture, 1, 1, 13, 14, outlineColor);
                FillRect(texture, 2, 2, 11, 12, bodyColor);
                FillRect(texture, 10, 4, 2, 2, eyeColor);
                FillRect(texture, 10, 10, 2, 2, eyeColor);
                FillRect(texture, 12, 7, 3, 2, tongueColor);
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
                var outlineColor = new Color32(22, 66, 34, 255);
                var bodyColor = new Color32(92, 198, 76, 255);
                var stripeColor = new Color32(150, 236, 120, 255);

                FillRect(texture, 1, 1, 14, 14, outlineColor);
                FillRect(texture, 2, 2, 12, 12, bodyColor);
                FillRect(texture, 4, 3, 8, 10, stripeColor);
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
                var outlineColor = new Color32(20, 60, 30, 255);
                var bodyColor = new Color32(88, 188, 72, 255);

                FillRect(texture, 1, 3, 11, 10, outlineColor);
                FillRect(texture, 2, 4, 9, 8, bodyColor);
                FillRect(texture, 10, 5, 3, 6, bodyColor);
                FillRect(texture, 13, 6, 2, 4, outlineColor);
            });
        }

        return cachedSnakeTailSprite;
    }

    private static Sprite CreateGhostSprite()
    {
        if (cachedGhostSprite == null)
        {
            cachedGhostSprite = CreateSnakeSprite(texture =>
            {
                var outlineColor = new Color32(104, 26, 64, 255);
                var bodyColor = new Color32(248, 92, 150, 255);
                var eyeWhite = new Color32(250, 250, 255, 255);
                var eyePupil = new Color32(52, 88, 220, 255);

                FillRect(texture, 1, 2, 14, 11, outlineColor);
                FillRect(texture, 2, 3, 12, 9, bodyColor);
                FillRect(texture, 2, 12, 3, 2, outlineColor);
                FillRect(texture, 6, 12, 3, 2, outlineColor);
                FillRect(texture, 10, 12, 3, 2, outlineColor);
                FillRect(texture, 4, 5, 3, 4, eyeWhite);
                FillRect(texture, 9, 5, 3, 4, eyeWhite);
                FillRect(texture, 5, 6, 1, 2, eyePupil);
                FillRect(texture, 9, 6, 1, 2, eyePupil);
            });
        }

        return cachedGhostSprite;
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

        var rowHeights = new int[SourceMazeRows.Length];
        for (var rowIndex = 0; rowIndex < SourceMazeRows.Length; rowIndex++)
        {
            rowHeights[rowIndex] = IsSolidWallRow(SourceMazeRows[rowIndex]) ? 1 : CorridorWidth;
        }

        var columnWidths = new int[SourceMazeRows[0].Length];
        for (var columnIndex = 0; columnIndex < columnWidths.Length; columnIndex++)
        {
            columnWidths[columnIndex] = IsSolidWallColumn(columnIndex) ? 1 : CorridorWidth;
        }

        for (var sourceRowIndex = 0; sourceRowIndex < SourceMazeRows.Length; sourceRowIndex++)
        {
            var sourceRow = SourceMazeRows[sourceRowIndex];
            var rowHeight = rowHeights[sourceRowIndex];
            var expandedWidth = 0;
            for (var columnIndex = 0; columnIndex < columnWidths.Length; columnIndex++)
            {
                expandedWidth += columnWidths[columnIndex];
            }

            var expandedRows = new char[rowHeight][];
            for (var expandedRowIndex = 0; expandedRowIndex < rowHeight; expandedRowIndex++)
            {
                expandedRows[expandedRowIndex] = new string('#', expandedWidth).ToCharArray();
            }

            var writeX = 0;
            for (var sourceColumnIndex = 0; sourceColumnIndex < sourceRow.Length; sourceColumnIndex++)
            {
                var sourceCell = sourceRow[sourceColumnIndex];
                var columnWidth = columnWidths[sourceColumnIndex];

                for (var offsetY = 0; offsetY < rowHeight; offsetY++)
                {
                    for (var offsetX = 0; offsetX < columnWidth; offsetX++)
                    {
                        expandedRows[offsetY][writeX + offsetX] = GetExpandedCellValue(sourceCell, offsetX, offsetY);
                    }
                }

                writeX += columnWidth;
            }

            for (var expandedRowIndex = 0; expandedRowIndex < rowHeight; expandedRowIndex++)
            {
                mazeRows.Add(new string(expandedRows[expandedRowIndex]));
            }
        }
    }

    private static bool IsSolidWallRow(string row)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] != '#')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSolidWallColumn(int columnIndex)
    {
        for (var rowIndex = 0; rowIndex < SourceMazeRows.Length; rowIndex++)
        {
            if (SourceMazeRows[rowIndex][columnIndex] != '#')
            {
                return false;
            }
        }

        return true;
    }

    private void ResetDots()
    {
        foreach (Transform child in dotsRoot)
        {
            Destroy(child.gameObject);
        }

        dotViews.Clear();

        for (var row = 0; row < height; row++)
        {
            var y = height - 1 - row;
            for (var x = 0; x < width; x++)
            {
                if (mazeRows[row][x] != '.')
                {
                    continue;
                }

                var position = new Vector2Int(x, y);
                dotViews[position] = CreateCell(
                    position,
                    new Color(0.99f, 0.9f, 0.45f),
                    "Dot",
                    dotsRoot,
                    new Vector3(0.22f, 0.22f, 1f)).GetComponent<SpriteRenderer>();
            }
        }

        totalDots = dotViews.Count;
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

        var font = LoadUiFont();

        scoreText = CreateText("Score", new Vector2(0.5f, 0.93f), font, 11, TextAnchor.MiddleCenter);
        scoreText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        titleText = CreateText("Title", new Vector2(0.5f, 0.79f), font, 34, TextAnchor.MiddleCenter);
        statusText = CreateText("Status", new Vector2(0.5f, 0.69f), font, 20, TextAnchor.MiddleCenter);
        menuButton = CreateMenuButton(font);
        menuButton.onClick.AddListener(OpenPausePopup);
        BuildPausePopup(font);
        BuildLossPopup(font);
    }

    private void UpdateUi()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = $"Score: {score}";
        lossPopup.SetActive(gameState == GameState.Lost);
        pausePopup.SetActive(gameState == GameState.Paused);

        switch (gameState)
        {
            case GameState.StartScreen:
                titleText.text = "Maze Snake";
                statusText.text = "Swipe or use keyboard to turn.\nCollect all dots and avoid the ghost.";
                titleText.gameObject.SetActive(true);
                statusText.gameObject.SetActive(true);
                menuButton.gameObject.SetActive(true);
                break;
            case GameState.Playing:
                titleText.gameObject.SetActive(false);
                statusText.gameObject.SetActive(false);
                menuButton.gameObject.SetActive(true);
                break;
            case GameState.Paused:
                titleText.gameObject.SetActive(false);
                statusText.gameObject.SetActive(false);
                menuButton.gameObject.SetActive(false);
                break;
            case GameState.Won:
                titleText.text = "You Win";
                statusText.text = "All dots collected.";
                titleText.gameObject.SetActive(true);
                statusText.gameObject.SetActive(true);
                menuButton.gameObject.SetActive(true);
                break;
            case GameState.Lost:
                titleText.gameObject.SetActive(false);
                statusText.gameObject.SetActive(false);
                menuButton.gameObject.SetActive(false);
                lossTitleText.text = "Ты проиграл";
                lossScoreText.text = $"Очки за попытку: {lastAttemptScore}";
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

    private Button CreateMenuButton(Font font)
    {
        var buttonObject = new GameObject("MenuButton");
        buttonObject.transform.SetParent(uiCanvas.transform, false);

        var rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(13f, -18f);
        rectTransform.sizeDelta = new Vector2(21f, 21f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.13f, 0.44f, 0.62f, 0.94f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = new GameObject("Label");
        label.transform.SetParent(buttonObject.transform, false);

        var labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelImage = label.AddComponent<Image>();
        labelImage.sprite = CreatePauseSprite();
        labelImage.color = Color.white;
        labelImage.preserveAspect = true;

        labelRect.sizeDelta = new Vector2(11f, 11f);

        return button;
    }

    private void OpenPausePopup()
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        pauseQuoteText.text = PauseQuotes[Random.Range(0, PauseQuotes.Length)];
        SetGameState(GameState.Paused);
    }

    private void ResumeGame()
    {
        if (gameState != GameState.Paused)
        {
            return;
        }

        SetGameState(GameState.Playing);
    }

    private void OpenMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void BuildPausePopup(Font font)
    {
        pausePopup = new GameObject("PausePopup");
        pausePopup.transform.SetParent(uiCanvas.transform, false);

        var popupRect = pausePopup.AddComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(460f, 250f);
        popupRect.anchoredPosition = Vector2.zero;

        var popupImage = pausePopup.AddComponent<Image>();
        popupImage.color = new Color(0.08f, 0.11f, 0.18f, 0.96f);

        var pauseTitleText = CreateText("PauseTitle", new Vector2(0.5f, 0.76f), font, 28, TextAnchor.MiddleCenter);
        pauseTitleText.transform.SetParent(pausePopup.transform, false);
        pauseTitleText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        pauseTitleText.text = "Пауза";

        pauseQuoteText = CreateText("PauseQuote", new Vector2(0.5f, 0.54f), font, 18, TextAnchor.MiddleCenter);
        pauseQuoteText.transform.SetParent(pausePopup.transform, false);
        pauseQuoteText.rectTransform.sizeDelta = new Vector2(380f, 90f);
        pauseQuoteText.rectTransform.anchoredPosition = new Vector2(0f, -5f);
        pauseQuoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
        pauseQuoteText.verticalOverflow = VerticalWrapMode.Overflow;

        continueButton = CreateActionButton(pausePopup.transform, font, "Продолжить", new Vector2(-85f, -78f));
        continueButton.onClick.AddListener(ResumeGame);

        pauseExitToMenuButton = CreateActionButton(pausePopup.transform, font, "Выйти в меню", new Vector2(85f, -78f));
        pauseExitToMenuButton.onClick.AddListener(OpenMainMenu);

        pausePopup.SetActive(false);
    }

    private void BuildLossPopup(Font font)
    {
        lossPopup = new GameObject("LossPopup");
        lossPopup.transform.SetParent(uiCanvas.transform, false);

        var popupRect = lossPopup.AddComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(420f, 220f);
        popupRect.anchoredPosition = Vector2.zero;

        var popupImage = lossPopup.AddComponent<Image>();
        popupImage.color = new Color(0.08f, 0.11f, 0.18f, 0.96f);

        lossTitleText = CreateText("LossTitle", new Vector2(0.5f, 0.72f), font, 30, TextAnchor.MiddleCenter);
        lossTitleText.transform.SetParent(lossPopup.transform, false);
        lossTitleText.rectTransform.anchoredPosition = new Vector2(0f, 35f);

        lossScoreText = CreateText("LossScore", new Vector2(0.5f, 0.5f), font, 18, TextAnchor.MiddleCenter);
        lossScoreText.transform.SetParent(lossPopup.transform, false);
        lossScoreText.rectTransform.anchoredPosition = new Vector2(0f, -5f);

        retryButton = CreateActionButton(lossPopup.transform, font, "Повторить", new Vector2(-85f, -70f));
        retryButton.onClick.AddListener(StartRound);

        exitToMenuButton = CreateActionButton(lossPopup.transform, font, "Выйти в меню", new Vector2(85f, -70f));
        exitToMenuButton.onClick.AddListener(OpenMainMenu);

        lossPopup.SetActive(false);
    }

    private Button CreateActionButton(Transform parent, Font font, string label, Vector2 anchoredPosition)
    {
        var buttonObject = new GameObject($"{label}Button");
        buttonObject.transform.SetParent(parent, false);

        var rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(150f, 42f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.55f, 0.34f, 1f);

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
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
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

    private static Sprite CreatePauseSprite()
    {
        if (cachedPauseSprite == null)
        {
            cachedPauseSprite = CreateSnakeSprite(texture =>
            {
                var pauseColor = new Color32(245, 248, 252, 255);

                FillRect(texture, 3, 2, 4, 12, pauseColor);
                FillRect(texture, 9, 2, 4, 12, pauseColor);
            });
        }

        return cachedPauseSprite;
    }
}

