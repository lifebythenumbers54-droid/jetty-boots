using JettyBoots.GameState;

namespace JettyBoots.Decision;

/// <summary>
/// Decides when to jump based on game state and trajectory predictions.
/// </summary>
public class JumpDecider
{
    private readonly TrajectoryCalculator _trajectory;

    // Decision parameters - aligned with PlayerDetector game area boundaries
    // Game area: X from 250-680, Y from 50-380
    private int _safetyMargin = 15;          // Pixels from gap edge to aim for
    private double _jumpLeadTime = 0.05;      // Seconds before predicted jump to actually jump
    private int _floorY = 380;                // Y position of floor (bottom of game area)
    private int _ceilingY = 50;               // Y position of ceiling (top of game area)
    private int _maxJumpZoneY = 380;          // Don't trigger jumps for player positions below this Y value
    private double _emergencyJumpThreshold = 0.95; // Only emergency jump when VERY close to floor

    // State tracking
    private DateTime _lastJumpTime = DateTime.MinValue;
    private double _minJumpInterval = 0.35;   // Minimum seconds between jumps (shorter cooldown for responsiveness)

    // Simple reactive jump tracking
    private int _lastPlayerY = -1;
    private int _playerFallingFrames = 0;     // Count frames where player is falling
    private const int FALLING_FRAMES_THRESHOLD = 5;  // Jump after falling for fewer frames (more responsive)

    // Centering behavior - try to stay in middle of play area
    private bool _enableCentering = true;
    private int _targetCenterY = -1;          // Will be calculated as middle of play area

    public JumpDecider(TrajectoryCalculator trajectory)
    {
        _trajectory = trajectory;
        // Initialize target center based on default boundaries (Y: 50-400)
        _targetCenterY = (_floorY + _ceilingY) / 2;  // = 225
    }

    /// <summary>
    /// Sets the safety margin from gap edges.
    /// </summary>
    public void SetSafetyMargin(int margin)
    {
        _safetyMargin = margin;
    }

    /// <summary>
    /// Sets the floor and ceiling boundaries.
    /// </summary>
    public void SetBoundaries(int floorY, int ceilingY)
    {
        _floorY = floorY;
        _ceilingY = ceilingY;
        _maxJumpZoneY = floorY; // Also update max jump zone to match floor
        // Calculate target center as the middle of the play area
        _targetCenterY = (_floorY + _ceilingY) / 2;
        Console.WriteLine($"[JumpDecider] Boundaries set: floor={floorY}, ceiling={ceilingY}, center={_targetCenterY}");
    }

    /// <summary>
    /// Sets boundaries from a PlayAreaBounds object.
    /// </summary>
    public void SetBoundaries(GameState.PlayAreaBounds bounds)
    {
        SetBoundaries(bounds.MaxY, bounds.MinY);
    }

    /// <summary>
    /// Sets the maximum Y value for the jump zone. Player positions below this Y value
    /// will be ignored for jump decisions (useful to exclude UI elements or invalid areas).
    /// </summary>
    public void SetMaxJumpZoneY(int maxY)
    {
        _maxJumpZoneY = maxY;
    }

    /// <summary>
    /// Enables or disables active centering behavior.
    /// </summary>
    public void SetCenteringEnabled(bool enabled)
    {
        _enableCentering = enabled;
    }

    /// <summary>
    /// Makes a jump decision based on current game state.
    /// Uses simple reactive logic: jump when player has been falling too long or is too low.
    /// </summary>
    public JumpDecision Decide(FrameAnalysis analysis)
    {
        // Can't decide if player not detected
        if (!analysis.Player.Detected)
        {
            _playerFallingFrames = 0;
            _lastPlayerY = -1;
            return JumpDecision.NoAction("Player not detected");
        }

        // Ignore player positions below the max jump zone (excludes UI/invalid areas)
        if (analysis.Player.CenterY > _maxJumpZoneY)
        {
            _playerFallingFrames = 0;
            _lastPlayerY = -1;
            return JumpDecision.NoAction($"Player below jump zone (Y={analysis.Player.CenterY} > {_maxJumpZoneY})");
        }

        // Check jump cooldown
        var timeSinceLastJump = (DateTime.Now - _lastJumpTime).TotalSeconds;
        if (timeSinceLastJump < _minJumpInterval)
        {
            return JumpDecision.NoAction($"Jump cooldown ({_minJumpInterval - timeSinceLastJump:F2}s remaining)");
        }

        int playerY = analysis.Player.CenterY;
        int playerX = analysis.Player.CenterX;

        // Track if player is falling (Y increasing means falling down the screen)
        if (_lastPlayerY >= 0)
        {
            if (playerY > _lastPlayerY + 2)  // Player is falling (with small threshold for noise)
            {
                _playerFallingFrames++;
            }
            else if (playerY < _lastPlayerY - 2)  // Player is rising
            {
                _playerFallingFrames = 0;
            }
            // If roughly same position, keep the count as is
        }
        _lastPlayerY = playerY;

        // Check ceiling - don't jump if too close to top
        if (playerY < _ceilingY + 30)
        {
            _playerFallingFrames = 0;  // Reset falling counter when near ceiling
            return JumpDecision.NoAction("Too close to ceiling");
        }

        // Calculate target center if not set
        if (_targetCenterY < 0)
        {
            _targetCenterY = (_floorY + _ceilingY) / 2;
        }

        // Calculate zones relative to the play area
        // With Y: 50-400, play area height = 350
        int playAreaHeight = _floorY - _ceilingY;
        int dangerZone = _ceilingY + (int)(playAreaHeight * 0.70);   // Bottom 30% is danger (Y >= 295)
        int cautionZone = _ceilingY + (int)(playAreaHeight * 0.55); // Below 55% is caution (Y >= 242)
        int centerTolerance = playAreaHeight / 8;                    // Tighter tolerance around center (~12%)

        // Emergency: In danger zone - must jump immediately
        if (playerY >= dangerZone)
        {
            return JumpDecision.Jump($"DANGER ZONE (Y={playerY} >= {dangerZone})", 1.0);
        }

        // If obstacles detected, prioritize obstacle avoidance
        if (analysis.Obstacles.Detected && analysis.Obstacles.Obstacles.Count > 0)
        {
            var nextObstacle = analysis.Obstacles.Obstacles
                .Where(o => o.X > playerX)
                .OrderBy(o => o.X)
                .FirstOrDefault();

            if (nextObstacle != null)
            {
                return DecideForObstacle(playerX, playerY, nextObstacle);
            }
        }

        // CENTERING LOGIC: Try to stay near the middle of the play area
        if (_enableCentering)
        {
            int distanceFromCenter = playerY - _targetCenterY;

            // If significantly below center and falling, jump to get back up
            if (distanceFromCenter > centerTolerance && _playerFallingFrames >= 3)
            {
                return JumpDecision.Jump($"Centering: {distanceFromCenter}px below center, falling {_playerFallingFrames} frames", 0.7);
            }

            // If in caution zone (below center by more), jump even if not falling long
            if (playerY >= cautionZone && _playerFallingFrames >= 2)
            {
                return JumpDecision.Jump($"Below safe zone (Y={playerY}), recovering to center", 0.8);
            }

            // Falling too long anywhere below center - jump to stabilize
            if (distanceFromCenter > 0 && _playerFallingFrames >= FALLING_FRAMES_THRESHOLD)
            {
                return JumpDecision.Jump($"Falling {_playerFallingFrames} frames while below center", 0.6);
            }
        }
        else
        {
            // Non-centering fallback: just avoid danger zones
            if (playerY >= cautionZone && _playerFallingFrames >= FALLING_FRAMES_THRESHOLD)
            {
                return JumpDecision.Jump($"Falling too long ({_playerFallingFrames} frames) in caution zone", 0.8);
            }

            if (_playerFallingFrames >= FALLING_FRAMES_THRESHOLD * 2)
            {
                return JumpDecision.Jump($"Falling too long ({_playerFallingFrames} frames)", 0.6);
            }
        }

        return JumpDecision.NoAction($"Stable (Y={playerY}, center={_targetCenterY}, falling={_playerFallingFrames})");
    }

    private JumpDecision CheckFloorCollision(int playerY)
    {
        // Only emergency jump if player is VERY close to floor AND predicted to hit it
        // Use current position primarily, not just prediction
        if (playerY >= _floorY * 0.92)  // Player is in bottom 8% of screen
        {
            var prediction = _trajectory.PredictPosition(playerY, 0.15);
            if (prediction.Y >= _floorY * _emergencyJumpThreshold)
            {
                return JumpDecision.Jump("Emergency: approaching floor", 1.0);
            }
        }

        return JumpDecision.NoAction("");
    }

    private JumpDecision DecideWithoutObstacles(int playerY)
    {
        // Try to maintain a comfortable middle height
        int targetY = (_ceilingY + _floorY) / 2;

        // Only jump if we're getting dangerously close to floor (not just below target)
        if (playerY > _floorY * 0.85)  // Only when in bottom 15% of play area
        {
            var prediction = _trajectory.PredictPosition(playerY, 0.3);
            if (prediction.Y > _floorY * 0.9)  // And predicted to get even closer
            {
                return JumpDecision.Jump("Maintaining height - approaching floor", 0.6);
            }
        }

        return JumpDecision.NoAction("No obstacles, comfortable height");
    }

    private JumpDecision DecideForObstacle(int playerX, int playerY, Obstacle obstacle)
    {
        // Find optimal jump time
        var solution = _trajectory.FindOptimalJumpTime(
            playerX, playerY,
            obstacle.X, obstacle.GapTop, obstacle.GapBottom,
            _safetyMargin
        );

        if (solution == null)
        {
            // Can't find a good solution - try emergency measures
            return HandleNoSolution(playerX, playerY, obstacle);
        }

        // Check if we should jump now
        if (solution.ShouldJumpNow(_jumpLeadTime))
        {
            return JumpDecision.Jump(
                $"Optimal jump for obstacle at X={obstacle.X}",
                solution.Confidence
            );
        }

        // Check if current trajectory will miss the gap
        var currentPrediction = _trajectory.PredictPositionAtObstacle(playerX, playerY, obstacle.X);
        bool willHitObstacle = currentPrediction.Y < obstacle.GapTop || currentPrediction.Y > obstacle.GapBottom;

        if (willHitObstacle)
        {
            // We need to jump at some point
            double timeUntilJump = solution.JumpTime;

            return JumpDecision.NoAction(
                $"Jump needed in {timeUntilJump:F2}s (predicted Y: {currentPrediction.Y:F0}, gap: {obstacle.GapTop}-{obstacle.GapBottom})"
            );
        }

        // Current trajectory is fine
        return JumpDecision.NoAction(
            $"On track for gap (predicted Y: {currentPrediction.Y:F0})"
        );
    }

    private JumpDecision HandleNoSolution(int playerX, int playerY, Obstacle obstacle)
    {
        // No clean solution found - use heuristics
        double timeToObstacle = _trajectory.TimeToReachX(playerX, obstacle.X);

        if (timeToObstacle <= 0)
        {
            return JumpDecision.NoAction("Obstacle behind player");
        }

        int gapCenter = obstacle.GapCenterY;

        // If we're below the gap center, we might need to jump
        if (playerY > gapCenter)
        {
            // Check if we're significantly below and have time
            int distanceBelow = playerY - gapCenter;

            if (distanceBelow > 50 && timeToObstacle > 0.2)
            {
                return JumpDecision.Jump(
                    $"Heuristic: below gap center by {distanceBelow}px",
                    0.5
                );
            }
        }

        // If we're above the gap, don't jump
        if (playerY < gapCenter - 30)
        {
            return JumpDecision.NoAction("Above gap center, letting gravity work");
        }

        return JumpDecision.NoAction("No clear action needed");
    }

    /// <summary>
    /// Records that a jump was made (for cooldown tracking).
    /// </summary>
    public void RecordJump()
    {
        _lastJumpTime = DateTime.Now;
        _trajectory.SimulateJump();
    }

    /// <summary>
    /// Updates the trajectory calculator with observed player movement.
    /// </summary>
    public void UpdateTrajectory(int currentY, int previousY, double deltaTime)
    {
        _trajectory.UpdateFromPosition(currentY, previousY, deltaTime);
    }

    /// <summary>
    /// Resets the decider state.
    /// </summary>
    public void Reset()
    {
        _lastJumpTime = DateTime.MinValue;
        _lastPlayerY = -1;
        _playerFallingFrames = 0;
        _targetCenterY = (_floorY + _ceilingY) / 2;
        _trajectory.Reset();
    }
}

/// <summary>
/// Result of a jump decision.
/// </summary>
public record JumpDecision
{
    public bool ShouldJump { get; init; }
    public string Reason { get; init; } = "";
    public double Confidence { get; init; }

    public static JumpDecision Jump(string reason, double confidence) => new()
    {
        ShouldJump = true,
        Reason = reason,
        Confidence = confidence
    };

    public static JumpDecision NoAction(string reason) => new()
    {
        ShouldJump = false,
        Reason = reason,
        Confidence = 0
    };
}
