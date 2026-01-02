using JettyBoots.GameState;

namespace JettyBoots.Decision;

/// <summary>
/// Coordinates game analysis and decision making.
/// </summary>
public class DecisionEngine
{
    private readonly TrajectoryCalculator _trajectory;
    private readonly JumpDecider _jumpDecider;

    // State tracking
    private int _lastPlayerY = -1;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private int _frameCount = 0;
    private int _jumpCount = 0;
    private int _obstaclesPassed = 0;

    // Configuration
    private PlayStyle _playStyle = PlayStyle.Balanced;

    public DecisionEngine()
    {
        _trajectory = new TrajectoryCalculator();
        _jumpDecider = new JumpDecider(_trajectory);
    }

    /// <summary>
    /// Gets the trajectory calculator for external configuration.
    /// </summary>
    public TrajectoryCalculator Trajectory => _trajectory;

    /// <summary>
    /// Gets the jump decider for external configuration.
    /// </summary>
    public JumpDecider JumpDecider => _jumpDecider;

    /// <summary>
    /// Gets the current frame count.
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Gets the total jump count.
    /// </summary>
    public int JumpCount => _jumpCount;

    /// <summary>
    /// Sets the play style which affects decision parameters.
    /// </summary>
    public void SetPlayStyle(PlayStyle style)
    {
        _playStyle = style;

        switch (style)
        {
            case PlayStyle.Safe:
                _jumpDecider.SetSafetyMargin(25);
                break;
            case PlayStyle.Balanced:
                _jumpDecider.SetSafetyMargin(15);
                break;
            case PlayStyle.Aggressive:
                _jumpDecider.SetSafetyMargin(5);
                break;
        }
    }

    /// <summary>
    /// Processes a frame analysis and returns an action decision.
    /// </summary>
    public ActionDecision ProcessFrame(FrameAnalysis analysis)
    {
        _frameCount++;

        // Update timing
        var now = DateTime.Now;
        double deltaTime = _lastFrameTime == DateTime.MinValue
            ? 0.016 // Assume ~60fps for first frame
            : (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // Update trajectory with observed movement
        if (analysis.Player.Detected && _lastPlayerY >= 0)
        {
            _jumpDecider.UpdateTrajectory(analysis.Player.CenterY, _lastPlayerY, deltaTime);
        }

        // Store for next frame
        if (analysis.Player.Detected)
        {
            _lastPlayerY = analysis.Player.CenterY;
        }

        // Check game state
        if (analysis.GameState.State == GameState.GameState.GameOver)
        {
            return new ActionDecision
            {
                Action = GameAction.None,
                Reason = "Game Over",
                JumpDecision = null
            };
        }

        if (analysis.GameState.State == GameState.GameState.Menu)
        {
            return new ActionDecision
            {
                Action = GameAction.StartGame,
                Reason = "At menu, starting game",
                JumpDecision = null
            };
        }

        // Make jump decision
        var jumpDecision = _jumpDecider.Decide(analysis);

        if (jumpDecision.ShouldJump)
        {
            _jumpDecider.RecordJump();
            _jumpCount++;

            return new ActionDecision
            {
                Action = GameAction.Jump,
                Reason = jumpDecision.Reason,
                Confidence = jumpDecision.Confidence,
                JumpDecision = jumpDecision
            };
        }

        return new ActionDecision
        {
            Action = GameAction.None,
            Reason = jumpDecision.Reason,
            JumpDecision = jumpDecision
        };
    }

    /// <summary>
    /// Gets a debug summary of the current state.
    /// </summary>
    public DecisionDebugInfo GetDebugInfo(FrameAnalysis analysis)
    {
        var info = new DecisionDebugInfo
        {
            FrameCount = _frameCount,
            JumpCount = _jumpCount,
            LastPlayerY = _lastPlayerY,
            PlayStyle = _playStyle
        };

        if (analysis.Player.Detected)
        {
            info.PlayerY = analysis.Player.CenterY;
            info.PlayerX = analysis.Player.CenterX;

            // Predict future positions
            info.PredictedY_100ms = _trajectory.PredictPosition(analysis.Player.CenterY, 0.1).Y;
            info.PredictedY_200ms = _trajectory.PredictPosition(analysis.Player.CenterY, 0.2).Y;
            info.PredictedY_500ms = _trajectory.PredictPosition(analysis.Player.CenterY, 0.5).Y;
        }

        if (analysis.Obstacles.NextObstacle != null)
        {
            var obs = analysis.Obstacles.NextObstacle;
            info.NextObstacleX = obs.X;
            info.NextObstacleGapTop = obs.GapTop;
            info.NextObstacleGapBottom = obs.GapBottom;

            if (analysis.Player.Detected)
            {
                info.TimeToObstacle = _trajectory.TimeToReachX(analysis.Player.CenterX, obs.X);
                var predicted = _trajectory.PredictPositionAtObstacle(
                    analysis.Player.CenterX, analysis.Player.CenterY, obs.X);
                info.PredictedYAtObstacle = predicted.Y;
                info.WillHitObstacle = predicted.Y < obs.GapTop || predicted.Y > obs.GapBottom;
            }
        }

        return info;
    }

    /// <summary>
    /// Resets the engine state (e.g., for a new game).
    /// </summary>
    public void Reset()
    {
        _lastPlayerY = -1;
        _lastFrameTime = DateTime.MinValue;
        _frameCount = 0;
        _jumpCount = 0;
        _obstaclesPassed = 0;
        _jumpDecider.Reset();
    }
}

/// <summary>
/// Possible game actions.
/// </summary>
public enum GameAction
{
    None,
    Jump,
    StartGame,
    Restart
}

/// <summary>
/// Play style affecting decision aggressiveness.
/// </summary>
public enum PlayStyle
{
    Safe,       // Large safety margins, early jumps
    Balanced,   // Moderate margins
    Aggressive  // Small margins, risky but potentially higher scores
}

/// <summary>
/// Result of decision processing for a frame.
/// </summary>
public record ActionDecision
{
    public GameAction Action { get; init; }
    public string Reason { get; init; } = "";
    public double Confidence { get; init; }
    public JumpDecision? JumpDecision { get; init; }
}

/// <summary>
/// Debug information about the decision state.
/// </summary>
public record DecisionDebugInfo
{
    public int FrameCount { get; init; }
    public int JumpCount { get; init; }
    public int LastPlayerY { get; init; }
    public PlayStyle PlayStyle { get; init; }

    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public double PredictedY_100ms { get; set; }
    public double PredictedY_200ms { get; set; }
    public double PredictedY_500ms { get; set; }

    public int NextObstacleX { get; set; }
    public int NextObstacleGapTop { get; set; }
    public int NextObstacleGapBottom { get; set; }
    public double TimeToObstacle { get; set; }
    public double PredictedYAtObstacle { get; set; }
    public bool WillHitObstacle { get; set; }
}
