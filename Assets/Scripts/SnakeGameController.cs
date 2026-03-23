using System.Collections.Generic;
using UnityEngine;

public class SnakeGameController : MonoBehaviour
{
    private static readonly string[] MazeRows =
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
        "#...#.........#",
        "###############"
    };

    private readonly List<Vector2Int> snakeSegments = new();
    private readonly Dictionary<int, Transform> segmentViews = new();
    private readonly Dictionary<Vector2Int, SpriteRenderer> dotViews = new();
    private readonly HashSet<Vector2Int> walls = new();

    private static Sprite cachedSquareSprite;

    private Transform boardRoot;
    private Transform dotsRoot;
    private Transform snakeRoot;

    private Vector2Int snakeDirection = Vector2Int.right;
    private Vector2Int queuedDirection = Vector2Int.right;
    private Vector2Int playerStart;
    private int width;
    private int height;
    private int pendingGrowth;
    private float moveTimer;

    private const float MoveInterval = 0.22f;

    private void Start()
    {
        BuildWorld();
        StartRound();
    }

    private void Update()
    {
        ReadInput();

        moveTimer += Time.deltaTime;
        if (moveTimer >= MoveInterval)
        {
            moveTimer -= MoveInterval;
            StepSnake();
        }
    }

    private void BuildWorld()
    {
        width = MazeRows[0].Length;
        height = MazeRows.Length;

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

                switch (MazeRows[row][x])
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
                }
            }
        }
    }

    private void StartRound()
    {
        moveTimer = 0f;
        pendingGrowth = 0;
        snakeDirection = Vector2Int.right;
        queuedDirection = Vector2Int.right;

        snakeSegments.Clear();
        snakeSegments.Add(playerStart);
        snakeSegments.Add(playerStart + Vector2Int.left);

        foreach (var view in segmentViews.Values)
        {
            Destroy(view.gameObject);
        }

        segmentViews.Clear();

        foreach (var dot in dotViews.Values)
        {
            dot.enabled = true;
        }

        SyncSnakeVisuals();
    }

    private void StepSnake()
    {
        if (queuedDirection + snakeDirection != Vector2Int.zero || snakeSegments.Count <= 1)
        {
            snakeDirection = queuedDirection;
        }

        var nextHead = snakeSegments[0] + snakeDirection;
        var tailPosition = snakeSegments[snakeSegments.Count - 1];
        var hitsBody = snakeSegments.Contains(nextHead) && (pendingGrowth > 0 || nextHead != tailPosition);
        if (walls.Contains(nextHead) || hitsBody)
        {
            StartRound();
            return;
        }

        snakeSegments.Insert(0, nextHead);

        if (dotViews.TryGetValue(nextHead, out var dotView) && dotView.enabled)
        {
            dotView.enabled = false;
            pendingGrowth++;
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
            StartRound();
            return;
        }

        SyncSnakeVisuals();
    }

    private void SyncSnakeVisuals()
    {
        for (var i = 0; i < snakeSegments.Count; i++)
        {
            if (!segmentViews.TryGetValue(i, out var view))
            {
                view = CreateCell(snakeSegments[i], i == 0 ? new Color(0.43f, 0.9f, 0.5f) : new Color(0.26f, 0.72f, 0.34f), $"Segment_{i}", snakeRoot, new Vector3(0.84f, 0.84f, 1f)).transform;
                segmentViews[i] = view;
            }

            view.position = GridToWorld(snakeSegments[i]);
            view.GetComponent<SpriteRenderer>().color = i == 0 ? new Color(0.43f, 0.9f, 0.5f) : new Color(0.26f, 0.72f, 0.34f);
        }

        for (var i = snakeSegments.Count; i < segmentViews.Count; i++)
        {
            Destroy(segmentViews[i].gameObject);
            segmentViews.Remove(i);
            i--;
        }
    }

    private void ReadInput()
    {
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
}
