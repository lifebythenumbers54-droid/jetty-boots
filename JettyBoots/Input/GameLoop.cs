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

    // Events
    public event Action<LoopStatus>? OnStatusUpdate;
    public event Action<string>? OnLog;

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
        }

        Log($"Game loop started (Target FPS: {_targetFps}, Dry Run: {_dryRun})");

        GameState.GameState lastGameState = GameState.GameState.Unknown;

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

                // Analyze frame
                var analysis = _analyzer.Analyze(frame);

                // Track game state changes
                if (analysis.GameState.State != lastGameState)
                {
                    HandleGameStateChange(lastGameState, analysis.GameState.State);
                    lastGameState = analysis.GameState.State;
                }

                // Make decision
                var decision = _decisionEngine.ProcessFrame(analysis);

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
            Log("New game started");
        }
    }

    private void ExecuteAction(ActionDecision decision)
    {
        if (_dryRun)
            return;

        switch (decision.Action)
        {
            case GameAction.Jump:
                if (_inputSimulator.SendJump())
                {
                    _jumpsSent++;
                }
                break;

            case GameAction.StartGame:
                _inputSimulator.SendStartGame();
                break;

            case GameAction.Restart:
                _inputSimulator.SendStartGame();
                break;
        }
    }

    private Mat DrawDebugOverlay(Mat frame, FrameAnalysis analysis, ActionDecision decision)
    {
        var overlay = _analyzer.DrawOverlay(frame, analysis);

        // Add action indicator
        var actionColor = decision.Action switch
        {
            GameAction.Jump => new Scalar(0, 255, 0),      // Green
            GameAction.StartGame => new Scalar(255, 255, 0), // Cyan
            _ => new Scalar(255, 255, 255)                   // White
        };

        string actionText = _dryRun ? $"[DRY RUN] {decision.Action}" : decision.Action.ToString();
        Cv2.PutText(overlay, actionText, new OpenCvSharp.Point(10, 90),
            HersheyFonts.HersheySimplex, 0.6, actionColor, 2);

        // Add statistics
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

        // Draw trajectory prediction
        if (analysis.Player.Detected)
        {
            DrawTrajectory(overlay, analysis.Player.CenterX, analysis.Player.CenterY);
        }

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
