using JettyBoots.GameState;

namespace JettyBoots.Decision;

/// <summary>
/// Decides when to jump based on game state and trajectory predictions.
/// </summary>
public class JumpDecider
{
    private readonly TrajectoryCalculator _trajectory;

    // Decision parameters
    private int _safetyMargin = 15;          // Pixels from gap edge to aim for
    private double _jumpLeadTime = 0.05;      // Seconds before predicted jump to actually jump
    private int _floorY = 550;                // Y position of floor (death zone)
    private int _ceilingY = 50;               // Y position of ceiling (death zone)
    private double _emergencyJumpThreshold = 0.8; // Jump if about to hit floor

    // State tracking
    private DateTime _lastJumpTime = DateTime.MinValue;
    private double _minJumpInterval = 0.15;   // Minimum seconds between jumps

    public JumpDecider(TrajectoryCalculator trajectory)
    {
        _trajectory = trajectory;
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
    }

    /// <summary>
    /// Makes a jump decision based on current game state.
    /// </summary>
    public JumpDecision Decide(FrameAnalysis analysis)
    {
        // Can't decide if player not detected
        if (!analysis.Player.Detected)
        {
            return JumpDecision.NoAction("Player not detected");
        }

        // Check jump cooldown
        var timeSinceLastJump = (DateTime.Now - _lastJumpTime).TotalSeconds;
        if (timeSinceLastJump < _minJumpInterval)
        {
            return JumpDecision.NoAction($"Jump cooldown ({_minJumpInterval - timeSinceLastJump:F2}s remaining)");
        }

        int playerY = analysis.Player.CenterY;
        int playerX = analysis.Player.CenterX;

        // Emergency floor avoidance
        var floorCheck = CheckFloorCollision(playerY);
        if (floorCheck.ShouldJump)
        {
            return floorCheck;
        }

        // Check ceiling - don't jump if too close
        if (playerY < _ceilingY + 50)
        {
            return JumpDecision.NoAction("Too close to ceiling");
        }

        // If no obstacles, maintain comfortable height
        if (!analysis.Obstacles.Detected || analysis.Obstacles.Obstacles.Count == 0)
        {
            return DecideWithoutObstacles(playerY);
        }

        // Find the next obstacle
        var nextObstacle = analysis.Obstacles.Obstacles
            .Where(o => o.X > playerX)
            .OrderBy(o => o.X)
            .FirstOrDefault();

        if (nextObstacle == null)
        {
            return DecideWithoutObstacles(playerY);
        }

        // Calculate optimal jump for this obstacle
        return DecideForObstacle(playerX, playerY, nextObstacle);
    }

    private JumpDecision CheckFloorCollision(int playerY)
    {
        // Predict position in near future
        var prediction = _trajectory.PredictPosition(playerY, 0.2);

        if (prediction.Y >= _floorY * _emergencyJumpThreshold)
        {
            return JumpDecision.Jump("Emergency: approaching floor", 1.0);
        }

        return JumpDecision.NoAction("");
    }

    private JumpDecision DecideWithoutObstacles(int playerY)
    {
        // Try to maintain a comfortable middle height
        int targetY = (_ceilingY + _floorY) / 2;

        // If we're falling below target and getting close to floor
        if (playerY > targetY + 100)
        {
            var prediction = _trajectory.PredictPosition(playerY, 0.3);
            if (prediction.Y > _floorY * 0.7)
            {
                return JumpDecision.Jump("Maintaining height", 0.6);
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
