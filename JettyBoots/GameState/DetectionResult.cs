using OpenCvSharp;

namespace JettyBoots.GameState;

/// <summary>
/// Result of player detection.
/// </summary>
public record PlayerDetectionResult
{
    public bool Detected { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double Confidence { get; init; }

    /// <summary>
    /// Center Y position of the player (most important for gameplay).
    /// </summary>
    public int CenterY => Y + Height / 2;

    /// <summary>
    /// Center X position of the player.
    /// </summary>
    public int CenterX => X + Width / 2;

    public Rect BoundingBox => new(X, Y, Width, Height);

    public static PlayerDetectionResult NotDetected => new() { Detected = false };
}

/// <summary>
/// Represents a single obstacle (pipe pair with gap).
/// </summary>
public record Obstacle
{
    /// <summary>
    /// X position of the obstacle (left edge).
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Width of the obstacle.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Y position of the gap's top edge.
    /// </summary>
    public int GapTop { get; init; }

    /// <summary>
    /// Y position of the gap's bottom edge.
    /// </summary>
    public int GapBottom { get; init; }

    /// <summary>
    /// Height of the gap.
    /// </summary>
    public int GapHeight => GapBottom - GapTop;

    /// <summary>
    /// Center Y position of the gap (target for player).
    /// </summary>
    public int GapCenterY => GapTop + GapHeight / 2;

    /// <summary>
    /// Right edge of the obstacle.
    /// </summary>
    public int RightEdge => X + Width;

    /// <summary>
    /// Distance from a given X position.
    /// </summary>
    public int DistanceFrom(int x) => X - x;
}

/// <summary>
/// Result of obstacle detection.
/// </summary>
public record ObstacleDetectionResult
{
    public bool Detected { get; init; }
    public List<Obstacle> Obstacles { get; init; } = new();
    public double Confidence { get; init; }

    /// <summary>
    /// The next obstacle the player needs to pass.
    /// </summary>
    public Obstacle? NextObstacle => Obstacles.OrderBy(o => o.X).FirstOrDefault();

    public static ObstacleDetectionResult NotDetected => new() { Detected = false };
}

/// <summary>
/// Current state of the game.
/// </summary>
public enum GameState
{
    Unknown,
    Menu,
    Playing,
    GameOver
}

/// <summary>
/// Result of game state detection.
/// </summary>
public record GameStateResult
{
    public GameState State { get; init; }
    public double Confidence { get; init; }
    public int? Score { get; init; }

    public static GameStateResult Unknown => new() { State = GameState.Unknown };
}

/// <summary>
/// Combined detection results for a single frame.
/// </summary>
public record FrameAnalysis
{
    public PlayerDetectionResult Player { get; init; } = PlayerDetectionResult.NotDetected;
    public ObstacleDetectionResult Obstacles { get; init; } = ObstacleDetectionResult.NotDetected;
    public GameStateResult GameState { get; init; } = GameStateResult.Unknown;
    public double AnalysisTimeMs { get; init; }
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
}
