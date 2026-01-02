using System.Diagnostics;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace JettyBoots.GameState;

/// <summary>
/// Combines all detection components to analyze game frames.
/// </summary>
public class GameAnalyzer : IDisposable
{
    private readonly PlayerDetector _playerDetector;
    private readonly ObstacleDetector _obstacleDetector;
    private readonly GameStateDetector _gameStateDetector;
    private readonly Stopwatch _stopwatch = new();

    private bool _disposed;

    public GameAnalyzer()
    {
        _playerDetector = new PlayerDetector();
        _obstacleDetector = new ObstacleDetector();
        _gameStateDetector = new GameStateDetector();
    }

    /// <summary>
    /// Gets the player detector for configuration.
    /// </summary>
    public PlayerDetector PlayerDetector => _playerDetector;

    /// <summary>
    /// Gets the obstacle detector for configuration.
    /// </summary>
    public ObstacleDetector ObstacleDetector => _obstacleDetector;

    /// <summary>
    /// Gets the game state detector for configuration.
    /// </summary>
    public GameStateDetector GameStateDetector => _gameStateDetector;

    /// <summary>
    /// Analyzes a single frame and returns all detection results.
    /// </summary>
    public FrameAnalysis Analyze(Mat frame)
    {
        if (frame == null || frame.Empty())
        {
            return new FrameAnalysis();
        }

        _stopwatch.Restart();

        // Run all detectors
        var playerResult = _playerDetector.Detect(frame);
        var obstacleResult = _obstacleDetector.Detect(frame);
        var gameStateResult = _gameStateDetector.Detect(frame);

        // Update obstacle detector with player position if detected
        if (playerResult.Detected)
        {
            _obstacleDetector.SetPlayerX(playerResult.CenterX);
        }

        _stopwatch.Stop();

        return new FrameAnalysis
        {
            Player = playerResult,
            Obstacles = obstacleResult,
            GameState = gameStateResult,
            AnalysisTimeMs = _stopwatch.Elapsed.TotalMilliseconds,
            FrameWidth = frame.Width,
            FrameHeight = frame.Height
        };
    }

    /// <summary>
    /// Draws detection overlays on a frame for debugging.
    /// </summary>
    public Mat DrawOverlay(Mat frame, FrameAnalysis analysis)
    {
        var output = frame.Clone();

        // Draw player bounding box
        if (analysis.Player.Detected)
        {
            Cv2.Rectangle(output, analysis.Player.BoundingBox, new Scalar(0, 255, 0), 2);

            // Draw center point
            Cv2.Circle(output, new Point(analysis.Player.CenterX, analysis.Player.CenterY), 5, new Scalar(0, 255, 0), -1);

            // Draw confidence
            Cv2.PutText(output, $"Player: {analysis.Player.Confidence:P0}",
                new Point(analysis.Player.X, analysis.Player.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
        }

        // Draw obstacles
        if (analysis.Obstacles.Detected)
        {
            foreach (var obstacle in analysis.Obstacles.Obstacles)
            {
                // Draw top pipe area
                Cv2.Rectangle(output,
                    new Rect(obstacle.X, 0, obstacle.Width, obstacle.GapTop),
                    new Scalar(0, 0, 255), 2);

                // Draw bottom pipe area
                Cv2.Rectangle(output,
                    new Rect(obstacle.X, obstacle.GapBottom, obstacle.Width, frame.Height - obstacle.GapBottom),
                    new Scalar(0, 0, 255), 2);

                // Draw gap area (target zone)
                Cv2.Rectangle(output,
                    new Rect(obstacle.X, obstacle.GapTop, obstacle.Width, obstacle.GapHeight),
                    new Scalar(255, 255, 0), 2);

                // Draw gap center line
                Cv2.Line(output,
                    new Point(obstacle.X, obstacle.GapCenterY),
                    new Point(obstacle.X + obstacle.Width, obstacle.GapCenterY),
                    new Scalar(255, 255, 0), 1);

                // Draw distance from player
                if (analysis.Player.Detected)
                {
                    int distance = obstacle.DistanceFrom(analysis.Player.CenterX);
                    Cv2.PutText(output, $"D:{distance}",
                        new Point(obstacle.X, obstacle.GapTop - 10),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);
                }
            }
        }

        // Draw game state
        string stateText = $"State: {analysis.GameState.State} ({analysis.GameState.Confidence:P0})";
        Cv2.PutText(output, stateText, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);

        // Draw analysis time
        string timeText = $"Analysis: {analysis.AnalysisTimeMs:F1}ms";
        Cv2.PutText(output, timeText, new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);

        // Draw trajectory line if player detected
        if (analysis.Player.Detected && analysis.Obstacles.NextObstacle != null)
        {
            var nextObs = analysis.Obstacles.NextObstacle;

            // Draw line from player to gap center
            Cv2.Line(output,
                new Point(analysis.Player.CenterX, analysis.Player.CenterY),
                new Point(nextObs.X, nextObs.GapCenterY),
                new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
        }

        return output;
    }

    /// <summary>
    /// Configures detectors based on game-specific settings.
    /// </summary>
    public void Configure(DetectionConfig config)
    {
        if (config.PlayerColorLower != null && config.PlayerColorUpper != null)
        {
            _playerDetector.SetColorRange(config.PlayerColorLower.Value, config.PlayerColorUpper.Value);
        }

        if (config.ObstacleColorLower != null && config.ObstacleColorUpper != null)
        {
            _obstacleDetector.SetColorRange(config.ObstacleColorLower.Value, config.ObstacleColorUpper.Value);
        }
    }

    /// <summary>
    /// Resets all detector states.
    /// </summary>
    public void Reset()
    {
        _playerDetector.Reset();
        _gameStateDetector.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gameStateDetector.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration for detection parameters.
/// </summary>
public record DetectionConfig
{
    public Scalar? PlayerColorLower { get; init; }
    public Scalar? PlayerColorUpper { get; init; }
    public Scalar? ObstacleColorLower { get; init; }
    public Scalar? ObstacleColorUpper { get; init; }
}
