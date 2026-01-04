using System.Diagnostics;
using JettyBoots.Decision;
using JettyBoots.GameState;
using JettyBoots.ScreenCapture;
using OpenCvSharp;

namespace JettyBoots.Input;

/// <summary>
/// Main game loop that coordinates capture, analysis, decision, and input.
/// </summary>
public class GameLoop : IDisposable
{
    private readonly IScreenCapture _capture;
    private readonly GameAnalyzer _analyzer;
    private readonly DecisionEngine _decisionEngine;
    private readonly GameInputSimulator _inputSimulator;

    private bool _running;
    private bool _paused;
    private bool _dryRun;
    private bool _disposed;

    // Statistics
    private int _frameCount;
    private int _jumpsSent;
    private int _gamesPlayed;
    private int _obstaclesPassed;
    private DateTime _startTime;
    private readonly Stopwatch _frameTimer = new();

    // Configuration
    private int _targetFps = 30;
    private bool _showDebugWindow = true;
    private bool _verboseLogging = false;

    // Screenshot capture for debugging
    private bool _captureScreenshots = false;
    private string _screenshotFolder = "Screenshots";
    private int _screenshotIntervalMs = 250;
    private DateTime _lastScreenshotTime = DateTime.MinValue;
    private int _screenshotCount = 0;

    // Events
    public event Action<LoopStatus>? OnStatusUpdate;
    public event Action<string>? OnLog;

    /// <summary>
    /// Gets or sets whether to enable verbose logging for debugging.
    /// </summary>
    public bool VerboseLogging
    {
        get => _verboseLogging;
        set => _verboseLogging = value;
    }

    /// <summary>
    /// Gets or sets whether to capture screenshots for debugging player detection.
    /// </summary>
    public bool CaptureScreenshots
    {
        get => _captureScreenshots;
        set => _captureScreenshots = value;
    }

    /// <summary>
    /// Gets or sets the folder where screenshots are saved.
    /// </summary>
    public string ScreenshotFolder
    {
        get => _screenshotFolder;
        set => _screenshotFolder = value;
    }

    /// <summary>
    /// Gets or sets the interval between screenshots in milliseconds.
    /// </summary>
    public int ScreenshotIntervalMs
    {
        get => _screenshotIntervalMs;
        set => _screenshotIntervalMs = Math.Max(100, value);
    }

    public GameLoop(IScreenCapture capture)
    {
        _capture = capture;
        _analyzer = new GameAnalyzer();
        _decisionEngine = new DecisionEngine();
        _inputSimulator = new GameInputSimulator();
    }

    /// <summary>
    /// Gets or sets whether to run in dry-run mode (no inputs sent).
    /// </summary>
    public bool DryRun
    {
        get => _dryRun;
        set => _dryRun = value;
    }

    /// <summary>
    /// Gets or sets the target frames per second.
    /// </summary>
    public int TargetFps
    {
        get => _targetFps;
        set => _targetFps = Math.Clamp(value, 1, 120);
    }

    /// <summary>
    /// Gets or sets whether to show the debug window.
    /// </summary>
    public bool ShowDebugWindow
    {
        get => _showDebugWindow;
        set => _showDebugWindow = value;
    }

    /// <summary>
    /// Gets the decision engine for configuration.
    /// </summary>
    public DecisionEngine DecisionEngine => _decisionEngine;

    /// <summary>
    /// Gets the input simulator for configuration.
    /// </summary>
    public GameInputSimulator InputSimulator => _inputSimulator;

    /// <summary>
    /// Gets the game analyzer for configuration.
    /// </summary>
    public GameAnalyzer Analyzer => _analyzer;

    /// <summary>
    /// Gets current statistics.
    /// </summary>
    public LoopStatistics Statistics => new()
    {
        FrameCount = _frameCount,
        JumpsSent = _jumpsSent,
        GamesPlayed = _gamesPlayed,
        ObstaclesPassed = _obstaclesPassed,
        RunTime = DateTime.Now - _startTime,
        AverageFps = _frameCount / Math.Max(1, (DateTime.Now - _startTime).TotalSeconds)
    };

    /// <summary>
    /// Runs the game loop until stopped.
    /// </summary>
    public void Run(CancellationToken cancellationToken = default)
    {
        _running = true;
        _paused = false;
        _startTime = DateTime.Now;
        _frameCount = 0;
        _jumpsSent = 0;

        int targetFrameTimeMs = 1000 / _targetFps;
        const string windowName = "Jetty Boots - Auto Player";

        if (_showDebugWindow)
        {
            Cv2.NamedWindow(windowName, WindowFlags.Normal);
            Cv2.ResizeWindow(windowName, 800, 600);

            // Warm-up: capture and display a few frames before starting the game loop
            // This ensures the detection window is visible and stable
            Log("Warming up detection window...");
            bool playAreaDetected = false;
            for (int i = 0; i < 30; i++) // ~1 second of warm-up at 30fps
            {
                using var warmupFrame = _capture.CaptureFrame();
                if (warmupFrame != null)
                {
                    // Try to detect play area boundaries on first few frames
                    if (!playAreaDetected && i >= 5)
                    {
                        playAreaDetected = _analyzer.DetectAndConfigurePlayArea(warmupFrame);
                        if (playAreaDetected)
                        {
                            // Also configure the JumpDecider with detected boundaries
                            _decisionEngine.JumpDecider.SetBoundaries(_analyzer.PlayAreaBounds);
                            Log($"Play area detected: X=[{_analyzer.PlayAreaBounds.MinX}-{_analyzer.PlayAreaBounds.MaxX}], Y=[{_analyzer.PlayAreaBounds.MinY}-{_analyzer.PlayAreaBounds.MaxY}]");
                        }
                    }

                    var warmupAnalysis = _analyzer.Analyze(warmupFrame);
                    using var warmupOverlay = _analyzer.DrawOverlay(warmupFrame, warmupAnalysis);

                    string statusText = playAreaDetected
                        ? $"Warming up... {30 - i} (Play area detected)"
                        : $"Warming up... {30 - i} (Detecting play area...)";
                    Cv2.PutText(warmupOverlay, statusText, new OpenCvSharp.Point(10, 30),
                        HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 255, 255), 2);
                    Cv2.ImShow(windowName, warmupOverlay);
                    Cv2.WaitKey(33); // ~30fps
                }
            }

            if (!playAreaDetected)
            {
                Log("WARNING: Play area not detected, using default boundaries");
            }
            Log("Warm-up complete. Starting game loop...");
        }

        Log($"Game loop started (Target FPS: {_targetFps}, Dry Run: {_dryRun}, Verbose: {_verboseLogging})");
        Log($"Input config: UseMouseClick={_inputSimulator.UseMouseClick}, JumpKey={_inputSimulator.JumpKey}");

        GameState.GameState lastGameState = GameState.GameState.Unknown;
        int verboseLogCounter = 0;

        try
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                _frameTimer.Restart();

                // Skip processing if paused
                if (_paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                // Capture frame
                using var frame = _capture.CaptureFrame();
                if (frame == null)
                {
                    Log("Frame capture failed");
                    Thread.Sleep(10);
                    continue;
                }

                // Re-detect play area if needed (e.g., after game restart)
                if (_needsPlayAreaRedetection)
                {
                    if (_analyzer.DetectAndConfigurePlayArea(frame))
                    {
                        _decisionEngine.JumpDecider.SetBoundaries(_analyzer.PlayAreaBounds);
                        Log($"Play area re-detected: X=[{_analyzer.PlayAreaBounds.MinX}-{_analyzer.PlayAreaBounds.MaxX}], Y=[{_analyzer.PlayAreaBounds.MinY}-{_analyzer.PlayAreaBounds.MaxY}]");
                    }
                    _needsPlayAreaRedetection = false;
                }

                // Analyze frame
                var analysis = _analyzer.Analyze(frame);

                // Capture screenshots for debugging at specified interval
                if (_captureScreenshots)
                {
                    var now = DateTime.Now;
                    if ((now - _lastScreenshotTime).TotalMilliseconds >= _screenshotIntervalMs)
                    {
                        _lastScreenshotTime = now;
                        SaveDebugScreenshot(frame, analysis);
                    }
                }

                // Verbose logging every 30 frames (1 second at 30fps)
                verboseLogCounter++;
                if (_verboseLogging && verboseLogCounter % 30 == 0)
                {
                    Log($"[VERBOSE] Frame {_frameCount}: " +
                        $"Player={analysis.Player.Detected} (conf={analysis.Player.Confidence:F2}, pos=({analysis.Player.CenterX},{analysis.Player.CenterY})), " +
                        $"Obstacles={analysis.Obstacles.Obstacles.Count}, " +
                        $"GameState={analysis.GameState.State}");
                }

                // Track game state changes
                if (analysis.GameState.State != lastGameState)
                {
                    HandleGameStateChange(lastGameState, analysis.GameState.State);
                    lastGameState = analysis.GameState.State;
                }

                // Make decision
                var decision = _decisionEngine.ProcessFrame(analysis);

                // Log every jump decision
                if (_verboseLogging && decision.Action == GameAction.Jump)
                {
                    var debug = _decisionEngine.GetDebugInfo(analysis);
                    Log($"[JUMP] Frame {_frameCount}: {decision.Reason} | " +
                        $"TimeToObs={debug.TimeToObstacle:F2}s, PredY={debug.PredictedYAtObstacle:F0}, " +
                        $"WillHit={debug.WillHitObstacle}");
                }

                // Execute action
                ExecuteAction(decision);

                // Update statistics
                _frameCount++;

                // Send status update
                var status = new LoopStatus
                {
                    FrameNumber = _frameCount,
                    Analysis = analysis,
                    Decision = decision,
                    DebugInfo = _decisionEngine.GetDebugInfo(analysis),
                    Statistics = Statistics
                };
                OnStatusUpdate?.Invoke(status);

                // Draw debug overlay
                if (_showDebugWindow)
                {
                    using var overlay = DrawDebugOverlay(frame, analysis, decision);
                    Cv2.ImShow(windowName, overlay);

                    // Check for key press
                    int key = Cv2.WaitKey(1);
                    if (key == 'q' || key == 'Q' || key == 27) // Q or ESC
                    {
                        _running = false;
                    }
                    else if (key == 'p' || key == 'P') // Pause
                    {
                        _paused = !_paused;
                        Log(_paused ? "Paused" : "Resumed");
                    }
                    else if (key == 'r' || key == 'R') // Reset
                    {
                        Reset();
                        Log("Reset");
                    }
                }

                // Frame timing
                _frameTimer.Stop();
                int sleepTime = targetFrameTimeMs - (int)_frameTimer.ElapsedMilliseconds;
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }
        finally
        {
            if (_showDebugWindow)
            {
                Cv2.DestroyWindow(windowName);
            }

            Log($"Game loop stopped. Stats: {Statistics}");
        }
    }

    // Flag to trigger play area re-detection on next frame
    private bool _needsPlayAreaRedetection = false;

    private void HandleGameStateChange(GameState.GameState oldState, GameState.GameState newState)
    {
        Log($"Game state changed: {oldState} -> {newState}");

        if (newState == GameState.GameState.GameOver)
        {
            _gamesPlayed++;
            Log($"Game Over! Games played: {_gamesPlayed}");
        }
        else if (newState == GameState.GameState.Playing && oldState != GameState.GameState.Playing)
        {
            _decisionEngine.Reset();
            _needsPlayAreaRedetection = true; // Re-detect boundaries when new game starts
            Log("New game started - will re-detect play area");
        }
    }

    private void ExecuteAction(ActionDecision decision)
    {
        if (_dryRun)
        {
            if (_verboseLogging && decision.Action != GameAction.None)
            {
                Log($"[DRY RUN] Would execute: {decision.Action}");
            }
            return;
        }

        switch (decision.Action)
        {
            case GameAction.Jump:
                if (_verboseLogging)
                {
                    Log($"[INPUT] Sending jump (UseMouseClick={_inputSimulator.UseMouseClick})...");
                }
                if (_inputSimulator.SendJump())
                {
                    _jumpsSent++;
                    if (_verboseLogging)
                    {
                        Log($"[INPUT] Jump sent successfully! Total jumps: {_jumpsSent}");
                    }
                }
                else
                {
                    if (_verboseLogging)
                    {
                        Log($"[INPUT] Jump FAILED (too fast? interval not met)");
                    }
                }
                break;

            case GameAction.StartGame:
                if (_verboseLogging)
                {
                    Log($"[INPUT] Sending StartGame...");
                }
                _inputSimulator.SendStartGame();
                break;

            case GameAction.Restart:
                if (_verboseLogging)
                {
                    Log($"[INPUT] Sending Restart...");
                }
                _inputSimulator.SendStartGame();
                break;
        }
    }

    private Mat DrawDebugOverlay(Mat frame, FrameAnalysis analysis, ActionDecision decision)
    {
        var overlay = frame.Clone();

        // Get play area boundaries
        var bounds = _analyzer.PlayAreaBounds;
        int minX = bounds.MinX;
        int maxX = bounds.MaxX;
        int ceilingY = bounds.MinY;
        int floorY = bounds.MaxY;
        int playAreaHeight = floorY - ceilingY;
        int dangerZone = ceilingY + (int)(playAreaHeight * 0.70);
        int cautionZone = ceilingY + (int)(playAreaHeight * 0.55);
        int centerY = (floorY + ceilingY) / 2;

        // Draw playable zone rectangle (blue)
        Cv2.Rectangle(overlay, new OpenCvSharp.Rect(minX, ceilingY, maxX - minX, floorY - ceilingY),
            new Scalar(255, 100, 0), 2);

        // Draw danger zone (red fill, semi-transparent effect via lines)
        for (int y = dangerZone; y < floorY; y += 3)
        {
            Cv2.Line(overlay, new OpenCvSharp.Point(minX, y), new OpenCvSharp.Point(maxX, y), new Scalar(0, 0, 200), 1);
        }
        Cv2.Line(overlay, new OpenCvSharp.Point(minX, dangerZone), new OpenCvSharp.Point(maxX, dangerZone),
            new Scalar(0, 0, 255), 2);

        // Draw caution zone line (orange)
        Cv2.Line(overlay, new OpenCvSharp.Point(minX, cautionZone), new OpenCvSharp.Point(maxX, cautionZone),
            new Scalar(0, 165, 255), 2);

        // Draw center line (green dashed effect)
        for (int x = minX; x < maxX; x += 10)
        {
            Cv2.Line(overlay, new OpenCvSharp.Point(x, centerY), new OpenCvSharp.Point(Math.Min(x + 5, maxX), centerY),
                new Scalar(0, 255, 0), 2);
        }

        // Draw ceiling line (cyan)
        Cv2.Line(overlay, new OpenCvSharp.Point(minX, ceilingY), new OpenCvSharp.Point(maxX, ceilingY),
            new Scalar(255, 255, 0), 2);

        // Draw floor line (magenta)
        Cv2.Line(overlay, new OpenCvSharp.Point(minX, floorY), new OpenCvSharp.Point(maxX, floorY),
            new Scalar(255, 0, 255), 2);

        // Add zone labels on right side
        Cv2.PutText(overlay, $"CEILING ({ceilingY})", new OpenCvSharp.Point(maxX + 5, ceilingY + 5),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);
        Cv2.PutText(overlay, $"CENTER ({centerY})", new OpenCvSharp.Point(maxX + 5, centerY + 5),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 255, 0), 1);
        Cv2.PutText(overlay, $"CAUTION ({cautionZone})", new OpenCvSharp.Point(maxX + 5, cautionZone + 5),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 165, 255), 1);
        Cv2.PutText(overlay, $"DANGER ({dangerZone})", new OpenCvSharp.Point(maxX + 5, dangerZone + 5),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 0, 255), 1);
        Cv2.PutText(overlay, $"FLOOR ({floorY})", new OpenCvSharp.Point(maxX + 5, floorY + 5),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 0, 255), 1);

        // Draw player detection
        if (analysis.Player.Detected)
        {
            // Player bounding box (bright green)
            Cv2.Rectangle(overlay,
                new OpenCvSharp.Rect(analysis.Player.X, analysis.Player.Y, analysis.Player.Width, analysis.Player.Height),
                new Scalar(0, 255, 0), 2);

            // Player center point (yellow filled circle)
            Cv2.Circle(overlay, new OpenCvSharp.Point(analysis.Player.CenterX, analysis.Player.CenterY), 8, new Scalar(0, 255, 255), -1);
            Cv2.Circle(overlay, new OpenCvSharp.Point(analysis.Player.CenterX, analysis.Player.CenterY), 8, new Scalar(0, 0, 0), 2);

            // Determine zone and add label
            string zone = analysis.Player.CenterY >= dangerZone ? "JUMP NOW!" :
                          analysis.Player.CenterY >= cautionZone ? "Caution" :
                          analysis.Player.CenterY <= ceilingY + 30 ? "Near ceiling" : "Safe";
            var zoneColor = analysis.Player.CenterY >= dangerZone ? new Scalar(0, 0, 255) :
                            analysis.Player.CenterY >= cautionZone ? new Scalar(0, 165, 255) :
                            new Scalar(0, 255, 0);
            Cv2.PutText(overlay, $"PLAYER: ({analysis.Player.CenterX}, {analysis.Player.CenterY}) - {zone}",
                new OpenCvSharp.Point(10, 25), HersheyFonts.HersheySimplex, 0.6, zoneColor, 2);

            // Draw trajectory prediction
            DrawTrajectory(overlay, analysis.Player.CenterX, analysis.Player.CenterY);
        }
        else
        {
            Cv2.PutText(overlay, "PLAYER: NOT DETECTED", new OpenCvSharp.Point(10, 25),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);
        }

        // Add action indicator
        var actionColor = decision.Action switch
        {
            GameAction.Jump => new Scalar(0, 255, 0),      // Green
            GameAction.StartGame => new Scalar(255, 255, 0), // Cyan
            _ => new Scalar(255, 255, 255)                   // White
        };

        string actionText = _dryRun ? $"[DRY RUN] {decision.Action}" : decision.Action.ToString();
        Cv2.PutText(overlay, actionText, new OpenCvSharp.Point(10, 50),
            HersheyFonts.HersheySimplex, 0.6, actionColor, 2);

        // Show decision reason
        if (!string.IsNullOrEmpty(decision.Reason))
        {
            Cv2.PutText(overlay, $"Reason: {decision.Reason}", new OpenCvSharp.Point(10, 75),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(200, 200, 200), 1);
        }

        // Add statistics in top-right corner
        var stats = Statistics;
        Cv2.PutText(overlay, $"Jumps: {stats.JumpsSent}", new OpenCvSharp.Point(overlay.Width - 120, 30),
            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
        Cv2.PutText(overlay, $"Games: {stats.GamesPlayed}", new OpenCvSharp.Point(overlay.Width - 120, 50),
            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
        Cv2.PutText(overlay, $"FPS: {stats.AverageFps:F1}", new OpenCvSharp.Point(10, overlay.Height - 20),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);

        // Add control hints
        Cv2.PutText(overlay, "Q=Quit P=Pause R=Reset", new OpenCvSharp.Point(10, overlay.Height - 45),
            HersheyFonts.HersheySimplex, 0.4, new Scalar(180, 180, 180), 1);

        // Show game state
        Cv2.PutText(overlay, $"State: {analysis.GameState.State}", new OpenCvSharp.Point(overlay.Width - 150, 70),
            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);

        return overlay;
    }

    private void DrawTrajectory(Mat frame, int playerX, int playerY)
    {
        var trajectory = _decisionEngine.Trajectory;
        var points = trajectory.PredictTrajectory(playerY, 0.5, 0.033);

        for (int i = 1; i < points.Count; i++)
        {
            int x = playerX + (int)(i * trajectory.HorizontalSpeed * 0.033);
            int y = (int)points[i].Y;

            if (x >= 0 && x < frame.Width && y >= 0 && y < frame.Height)
            {
                int alpha = Math.Max(50, 255 - (i * 15));
                Cv2.Circle(frame, new OpenCvSharp.Point(x, y), 2, new Scalar(alpha, alpha, 0), -1);
            }
        }
    }

    /// <summary>
    /// Stops the game loop.
    /// </summary>
    public void Stop()
    {
        _running = false;
    }

    /// <summary>
    /// Pauses or resumes the game loop.
    /// </summary>
    public void TogglePause()
    {
        _paused = !_paused;
    }

    /// <summary>
    /// Resets statistics and state.
    /// </summary>
    public void Reset()
    {
        _frameCount = 0;
        _jumpsSent = 0;
        _startTime = DateTime.Now;
        _decisionEngine.Reset();
        _inputSimulator.Reset();
    }

    private void Log(string message)
    {
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.WriteLine($"[GameLoop] {message}");
    }

    private void SaveDebugScreenshot(Mat frame, FrameAnalysis analysis)
    {
        try
        {
            // Ensure screenshot folder exists
            if (!Directory.Exists(_screenshotFolder))
            {
                Directory.CreateDirectory(_screenshotFolder);
            }

            _screenshotCount++;

            // Save raw frame
            string rawFilename = Path.Combine(_screenshotFolder, $"frame_{_screenshotCount:D4}_raw.png");
            Cv2.ImWrite(rawFilename, frame);

            // Save annotated frame with detection info
            using var annotated = frame.Clone();

            // Draw player detection info
            string playerInfo = analysis.Player.Detected
                ? $"PLAYER: YES at ({analysis.Player.CenterX}, {analysis.Player.CenterY}) conf={analysis.Player.Confidence:F2}"
                : "PLAYER: NOT DETECTED";

            Cv2.PutText(annotated, playerInfo, new OpenCvSharp.Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7,
                analysis.Player.Detected ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

            // Draw player bounding box if detected
            if (analysis.Player.Detected)
            {
                Cv2.Rectangle(annotated,
                    new OpenCvSharp.Rect(analysis.Player.X, analysis.Player.Y, analysis.Player.Width, analysis.Player.Height),
                    new Scalar(0, 255, 0), 2);
            }

            // Draw frame info
            Cv2.PutText(annotated, $"Frame: {_frameCount} | GameState: {analysis.GameState.State}",
                new OpenCvSharp.Point(10, 60),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 1);

            string annotatedFilename = Path.Combine(_screenshotFolder, $"frame_{_screenshotCount:D4}_annotated.png");
            Cv2.ImWrite(annotatedFilename, annotated);

            Log($"[SCREENSHOT] Saved frame {_screenshotCount}: {rawFilename}");
        }
        catch (Exception ex)
        {
            Log($"[SCREENSHOT] Error saving screenshot: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all screenshots from the screenshot folder.
    /// </summary>
    public void ClearScreenshots()
    {
        try
        {
            if (Directory.Exists(_screenshotFolder))
            {
                foreach (var file in Directory.GetFiles(_screenshotFolder, "frame_*.png"))
                {
                    File.Delete(file);
                }
                _screenshotCount = 0;
                Log($"[SCREENSHOT] Cleared all screenshots from {_screenshotFolder}");
            }
        }
        catch (Exception ex)
        {
            Log($"[SCREENSHOT] Error clearing screenshots: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _running = false;
        _analyzer.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Status update from the game loop.
/// </summary>
public record LoopStatus
{
    public int FrameNumber { get; init; }
    public FrameAnalysis Analysis { get; init; } = new();
    public ActionDecision Decision { get; init; } = new();
    public DecisionDebugInfo DebugInfo { get; init; } = new();
    public LoopStatistics Statistics { get; init; } = new();
}

/// <summary>
/// Game loop statistics.
/// </summary>
public record LoopStatistics
{
    public int FrameCount { get; init; }
    public int JumpsSent { get; init; }
    public int GamesPlayed { get; init; }
    public int ObstaclesPassed { get; init; }
    public TimeSpan RunTime { get; init; }
    public double AverageFps { get; init; }

    public override string ToString() =>
        $"Frames: {FrameCount}, Jumps: {JumpsSent}, Games: {GamesPlayed}, Time: {RunTime:mm\\:ss}, FPS: {AverageFps:F1}";
}
