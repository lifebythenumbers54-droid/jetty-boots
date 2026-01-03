using JettyBoots.Configuration;
using JettyBoots.Decision;
using JettyBoots.GameState;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace JettyBoots.Debug;

/// <summary>
/// Manages debug visualization and overlay drawing.
/// </summary>
public class DebugOverlay : IDisposable
{
    private readonly DebugConfig _config;
    private readonly DebugStats _stats = new();
    private bool _disposed;

    // Window management
    private const string WindowName = "Jetty Boots - Debug View";
    private bool _windowCreated = false;

    // Frame saving
    private int _framesSinceLastSave = 0;
    private int _savedFrameCount = 0;

    // Colors for visualization
    private static readonly Scalar ColorPlayer = new(0, 255, 0);          // Green
    private static readonly Scalar ColorPlayerCenter = new(0, 255, 255);  // Yellow
    private static readonly Scalar ColorObstacle = new(0, 0, 255);        // Red
    private static readonly Scalar ColorGap = new(255, 255, 0);           // Cyan
    private static readonly Scalar ColorGapTarget = new(255, 200, 0);     // Light cyan
    private static readonly Scalar ColorTrajectory = new(255, 165, 0);    // Orange
    private static readonly Scalar ColorTrajectoryJump = new(0, 255, 0);  // Green
    private static readonly Scalar ColorText = new(255, 255, 255);        // White
    private static readonly Scalar ColorTextBackground = new(0, 0, 0);    // Black
    private static readonly Scalar ColorJumpAction = new(0, 255, 0);      // Green
    private static readonly Scalar ColorNoAction = new(200, 200, 200);    // Gray

    public DebugOverlay(DebugConfig config)
    {
        _config = config;

        if (_config.SaveDebugFrames && !string.IsNullOrEmpty(_config.DebugFrameDirectory))
        {
            Directory.CreateDirectory(_config.DebugFrameDirectory);
        }
    }

    /// <summary>
    /// Gets the current debug statistics.
    /// </summary>
    public DebugStats Stats => _stats;

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsEnabled => _config.Enabled;

    /// <summary>
    /// Creates the debug window if not already created.
    /// </summary>
    public void CreateWindow()
    {
        if (_config.ShowDebugWindow && !_windowCreated)
        {
            Cv2.NamedWindow(WindowName, WindowFlags.Normal);
            Cv2.ResizeWindow(WindowName, 800, 600);
            _windowCreated = true;
        }
    }

    /// <summary>
    /// Destroys the debug window.
    /// </summary>
    public void DestroyWindow()
    {
        if (_windowCreated)
        {
            Cv2.DestroyWindow(WindowName);
            _windowCreated = false;
        }
    }

    /// <summary>
    /// Draws the complete debug overlay on the frame.
    /// </summary>
    public Mat DrawOverlay(
        Mat frame,
        FrameAnalysis analysis,
        ActionDecision decision,
        DecisionDebugInfo debugInfo,
        TrajectoryCalculator? trajectory = null)
    {
        if (!_config.Enabled)
            return frame.Clone();

        var overlay = frame.Clone();

        // Update stats
        _stats.FrameCount++;
        _stats.LastAnalysisTimeMs = analysis.AnalysisTimeMs;

        // Draw components based on config
        if (_config.ShowPlayerBox && analysis.Player.Detected)
        {
            DrawPlayerOverlay(overlay, analysis.Player);
        }

        if (_config.ShowObstacles && analysis.Obstacles.Detected)
        {
            DrawObstacleOverlays(overlay, analysis.Obstacles, analysis.Player);
        }

        if (_config.ShowGapTargets && analysis.Obstacles.Detected)
        {
            DrawGapTargets(overlay, analysis.Obstacles);
        }

        if (_config.ShowTrajectory && analysis.Player.Detected && trajectory != null)
        {
            DrawTrajectoryPrediction(overlay, analysis.Player, trajectory, debugInfo);
        }

        if (_config.ShowStats)
        {
            DrawStats(overlay, analysis, decision, debugInfo);
        }

        // Draw action indicator
        DrawActionIndicator(overlay, decision);

        // Draw controls hint
        DrawControlsHint(overlay);

        // Handle frame saving
        if (_config.SaveDebugFrames)
        {
            _framesSinceLastSave++;
            if (_framesSinceLastSave >= _config.DebugFrameSaveInterval)
            {
                SaveDebugFrame(overlay);
                _framesSinceLastSave = 0;
            }
        }

        return overlay;
    }

    /// <summary>
    /// Shows the overlay in the debug window.
    /// </summary>
    public int ShowFrame(Mat overlay)
    {
        if (!_config.ShowDebugWindow || !_windowCreated)
            return -1;

        Cv2.ImShow(WindowName, overlay);
        return Cv2.WaitKey(1);
    }

    /// <summary>
    /// Handles key press events.
    /// </summary>
    /// <returns>True if the debug overlay handled the key, false otherwise.</returns>
    public bool HandleKeyPress(int key, out DebugAction action)
    {
        action = DebugAction.None;

        switch (key)
        {
            case 'q':
            case 'Q':
            case 27: // ESC
                action = DebugAction.Quit;
                return true;

            case 'p':
            case 'P':
                action = DebugAction.TogglePause;
                return true;

            case 'r':
            case 'R':
                action = DebugAction.Reset;
                return true;

            case 'd':
            case 'D':
                _config.Enabled = !_config.Enabled;
                action = DebugAction.ToggleDebug;
                return true;

            case 't':
            case 'T':
                _config.ShowTrajectory = !_config.ShowTrajectory;
                return true;

            case 's':
            case 'S':
                _config.ShowStats = !_config.ShowStats;
                return true;

            case 'f':
            case 'F':
                SaveDebugFrame(null);
                return true;
        }

        return false;
    }

    private void DrawPlayerOverlay(Mat frame, PlayerDetectionResult player)
    {
        // Draw bounding box
        Cv2.Rectangle(frame, player.BoundingBox, ColorPlayer, 2);

        // Draw center point
        Cv2.Circle(frame, new Point(player.CenterX, player.CenterY), 5, ColorPlayerCenter, -1);

        // Draw crosshairs
        int crossSize = 15;
        Cv2.Line(frame,
            new Point(player.CenterX - crossSize, player.CenterY),
            new Point(player.CenterX + crossSize, player.CenterY),
            ColorPlayerCenter, 1);
        Cv2.Line(frame,
            new Point(player.CenterX, player.CenterY - crossSize),
            new Point(player.CenterX, player.CenterY + crossSize),
            ColorPlayerCenter, 1);

        // Draw confidence label
        DrawLabel(frame,
            $"Player {player.Confidence:P0}",
            new Point(player.X, player.Y - 5),
            ColorPlayer);
    }

    private void DrawObstacleOverlays(Mat frame, ObstacleDetectionResult obstacles, PlayerDetectionResult player)
    {
        foreach (var obstacle in obstacles.Obstacles)
        {
            // Draw top pipe area
            Cv2.Rectangle(frame,
                new Rect(obstacle.X, 0, obstacle.Width, obstacle.GapTop),
                ColorObstacle, 2);

            // Draw bottom pipe area
            Cv2.Rectangle(frame,
                new Rect(obstacle.X, obstacle.GapBottom, obstacle.Width, frame.Height - obstacle.GapBottom),
                ColorObstacle, 2);

            // Draw gap area
            Cv2.Rectangle(frame,
                new Rect(obstacle.X, obstacle.GapTop, obstacle.Width, obstacle.GapHeight),
                ColorGap, 2);

            // Draw distance from player
            if (player.Detected)
            {
                int distance = obstacle.DistanceFrom(player.CenterX);
                if (distance > 0)
                {
                    DrawLabel(frame,
                        $"D: {distance}px",
                        new Point(obstacle.X, obstacle.GapTop - 5),
                        ColorGap);
                }
            }
        }
    }

    private void DrawGapTargets(Mat frame, ObstacleDetectionResult obstacles)
    {
        foreach (var obstacle in obstacles.Obstacles)
        {
            // Draw gap center line
            Cv2.Line(frame,
                new Point(obstacle.X, obstacle.GapCenterY),
                new Point(obstacle.X + obstacle.Width, obstacle.GapCenterY),
                ColorGapTarget, 2);

            // Draw target zone (center area)
            int safeZone = obstacle.GapHeight / 4;
            int safeTop = obstacle.GapCenterY - safeZone;
            int safeBottom = obstacle.GapCenterY + safeZone;

            Cv2.Rectangle(frame,
                new Rect(obstacle.X + 5, safeTop, obstacle.Width - 10, safeBottom - safeTop),
                ColorGapTarget, 1);

            // Draw gap center marker
            Cv2.Circle(frame,
                new Point(obstacle.X + obstacle.Width / 2, obstacle.GapCenterY),
                8, ColorGapTarget, 2);
        }
    }

    private void DrawTrajectoryPrediction(
        Mat frame,
        PlayerDetectionResult player,
        TrajectoryCalculator trajectory,
        DecisionDebugInfo debugInfo)
    {
        // Draw predicted trajectory points (no jump)
        var points = trajectory.PredictTrajectory(player.CenterY, 0.5, 0.033);

        Point? lastPoint = null;
        for (int i = 1; i < points.Count; i++)
        {
            int x = player.CenterX + (int)(i * trajectory.HorizontalSpeed * 0.033);
            int y = (int)points[i].Y;

            if (x >= 0 && x < frame.Width && y >= 0 && y < frame.Height)
            {
                // Fade color based on time
                int alpha = Math.Max(50, 255 - (i * 10));
                var color = new Scalar(alpha, alpha / 2, 0); // Orange fading

                var currentPoint = new Point(x, y);

                if (lastPoint.HasValue)
                {
                    Cv2.Line(frame, lastPoint.Value, currentPoint, color, 1);
                }

                Cv2.Circle(frame, currentPoint, 2, color, -1);
                lastPoint = currentPoint;
            }
        }

        // Draw predicted position at obstacle if available
        if (debugInfo.NextObstacleX > 0 && debugInfo.PredictedYAtObstacle > 0)
        {
            var predictedPoint = new Point(debugInfo.NextObstacleX, (int)debugInfo.PredictedYAtObstacle);

            if (predictedPoint.X >= 0 && predictedPoint.X < frame.Width &&
                predictedPoint.Y >= 0 && predictedPoint.Y < frame.Height)
            {
                var color = debugInfo.WillHitObstacle ? ColorObstacle : ColorTrajectoryJump;
                Cv2.Circle(frame, predictedPoint, 8, color, 2);
                Cv2.Circle(frame, predictedPoint, 3, color, -1);

                DrawLabel(frame,
                    debugInfo.WillHitObstacle ? "COLLISION" : "CLEAR",
                    new Point(predictedPoint.X + 15, predictedPoint.Y),
                    color);
            }
        }
    }

    private void DrawStats(
        Mat frame,
        FrameAnalysis analysis,
        ActionDecision decision,
        DecisionDebugInfo debugInfo)
    {
        int y = 25;
        int lineHeight = 22;
        int x = 10;

        // Game state
        DrawLabel(frame, $"State: {analysis.GameState.State} ({analysis.GameState.Confidence:P0})",
            new Point(x, y), ColorText);
        y += lineHeight;

        // Analysis time
        DrawLabel(frame, $"Analysis: {analysis.AnalysisTimeMs:F1}ms",
            new Point(x, y), ColorText);
        y += lineHeight;

        // FPS
        DrawLabel(frame, $"FPS: {_stats.CurrentFps:F1}",
            new Point(x, y), ColorText);
        y += lineHeight;

        // Player info
        if (analysis.Player.Detected)
        {
            DrawLabel(frame, $"Player: ({analysis.Player.CenterX}, {analysis.Player.CenterY})",
                new Point(x, y), ColorPlayer);
            y += lineHeight;
        }

        // Decision info
        y += 5;
        DrawLabel(frame, $"Action: {decision.Action}",
            new Point(x, y), decision.Action == GameAction.Jump ? ColorJumpAction : ColorNoAction);
        y += lineHeight;

        if (debugInfo.TimeToObstacle > 0)
        {
            DrawLabel(frame, $"Time to obstacle: {debugInfo.TimeToObstacle:F2}s",
                new Point(x, y), ColorText);
            y += lineHeight;
        }

        // Right side stats
        int rightX = frame.Width - 150;
        int rightY = 25;

        DrawLabel(frame, $"Frames: {debugInfo.FrameCount}",
            new Point(rightX, rightY), ColorText);
        rightY += lineHeight;

        DrawLabel(frame, $"Jumps: {debugInfo.JumpCount}",
            new Point(rightX, rightY), ColorText);
        rightY += lineHeight;

        DrawLabel(frame, $"Style: {debugInfo.PlayStyle}",
            new Point(rightX, rightY), ColorText);
    }

    private void DrawActionIndicator(Mat frame, ActionDecision decision)
    {
        int indicatorSize = 20;
        int x = frame.Width - 40;
        int y = frame.Height - 40;

        var color = decision.Action switch
        {
            GameAction.Jump => ColorJumpAction,
            GameAction.StartGame => new Scalar(255, 255, 0),
            _ => ColorNoAction
        };

        if (decision.Action == GameAction.Jump)
        {
            // Flash effect for jump
            Cv2.Circle(frame, new Point(x, y), indicatorSize, color, -1);
            Cv2.Circle(frame, new Point(x, y), indicatorSize + 5, color, 2);
        }
        else
        {
            Cv2.Circle(frame, new Point(x, y), indicatorSize, color, 2);
        }
    }

    private void DrawControlsHint(Mat frame)
    {
        int y = frame.Height - 15;
        DrawLabel(frame, "Q:Quit P:Pause R:Reset D:Debug T:Trajectory S:Stats F:SaveFrame",
            new Point(10, y), new Scalar(150, 150, 150), 0.4);
    }

    private void DrawLabel(Mat frame, string text, Point position, Scalar color, double scale = 0.5)
    {
        // Draw background
        var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, 1, out _);
        var bgRect = new Rect(position.X - 2, position.Y - textSize.Height - 2,
            textSize.Width + 4, textSize.Height + 6);

        // Ensure rect is within frame bounds
        bgRect.X = Math.Max(0, bgRect.X);
        bgRect.Y = Math.Max(0, bgRect.Y);
        bgRect.Width = Math.Min(bgRect.Width, frame.Width - bgRect.X);
        bgRect.Height = Math.Min(bgRect.Height, frame.Height - bgRect.Y);

        if (bgRect.Width > 0 && bgRect.Height > 0)
        {
            using var roi = new Mat(frame, bgRect);
            roi.SetTo(new Scalar(0, 0, 0, 128));
        }

        // Draw text
        Cv2.PutText(frame, text, position,
            HersheyFonts.HersheySimplex, scale, color, 1);
    }

    private void SaveDebugFrame(Mat? frame)
    {
        if (frame == null)
            return;

        _savedFrameCount++;
        var filename = Path.Combine(
            _config.DebugFrameDirectory,
            $"debug_{DateTime.Now:yyyyMMdd_HHmmss}_{_savedFrameCount:D6}.png");

        Cv2.ImWrite(filename, frame);
        Console.WriteLine($"Saved debug frame: {filename}");
    }

    /// <summary>
    /// Updates the FPS calculation.
    /// </summary>
    public void UpdateFps(double fps)
    {
        _stats.CurrentFps = fps;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DestroyWindow();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Debug actions that can be triggered by key presses.
/// </summary>
public enum DebugAction
{
    None,
    Quit,
    TogglePause,
    Reset,
    ToggleDebug
}

/// <summary>
/// Statistics tracked by the debug overlay.
/// </summary>
public class DebugStats
{
    public int FrameCount { get; set; }
    public double LastAnalysisTimeMs { get; set; }
    public double CurrentFps { get; set; }
    public int SavedFrameCount { get; set; }
}
