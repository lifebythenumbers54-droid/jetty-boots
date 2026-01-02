namespace JettyBoots.Decision;

/// <summary>
/// Calculates player trajectory based on Flappy Bird physics.
/// </summary>
public class TrajectoryCalculator
{
    // Physics constants (can be calibrated)
    private double _gravity = 800.0;        // Pixels per second squared (downward)
    private double _jumpVelocity = -300.0;  // Pixels per second (upward, negative Y)
    private double _terminalVelocity = 500.0; // Max fall speed
    private double _horizontalSpeed = 150.0; // Pixels per second (rightward)

    // Current state
    private double _velocityY = 0;
    private double _lastUpdateTime = 0;

    /// <summary>
    /// Sets the physics parameters. Values should be calibrated from actual game observation.
    /// </summary>
    public void SetPhysics(double gravity, double jumpVelocity, double terminalVelocity, double horizontalSpeed)
    {
        _gravity = gravity;
        _jumpVelocity = jumpVelocity;
        _terminalVelocity = terminalVelocity;
        _horizontalSpeed = horizontalSpeed;
    }

    /// <summary>
    /// Gets the current gravity value.
    /// </summary>
    public double Gravity => _gravity;

    /// <summary>
    /// Gets the jump velocity value.
    /// </summary>
    public double JumpVelocity => _jumpVelocity;

    /// <summary>
    /// Gets the horizontal speed value.
    /// </summary>
    public double HorizontalSpeed => _horizontalSpeed;

    /// <summary>
    /// Updates the current velocity based on player position changes.
    /// Call this each frame to track actual velocity.
    /// </summary>
    public void UpdateFromPosition(int currentY, int previousY, double deltaTimeSeconds)
    {
        if (deltaTimeSeconds > 0)
        {
            _velocityY = (currentY - previousY) / deltaTimeSeconds;
        }
    }

    /// <summary>
    /// Sets the current velocity directly (e.g., after a detected jump).
    /// </summary>
    public void SetVelocity(double velocityY)
    {
        _velocityY = velocityY;
    }

    /// <summary>
    /// Simulates a jump, setting velocity to jump velocity.
    /// </summary>
    public void SimulateJump()
    {
        _velocityY = _jumpVelocity;
    }

    /// <summary>
    /// Predicts player position after a given time, assuming no jumps.
    /// </summary>
    public TrajectoryPoint PredictPosition(int currentY, double timeSeconds)
    {
        // Using kinematic equation: y = y0 + v0*t + 0.5*g*t^2
        double newVelocity = _velocityY + _gravity * timeSeconds;

        // Clamp to terminal velocity
        newVelocity = Math.Min(newVelocity, _terminalVelocity);

        // Calculate new Y position
        double displacement = _velocityY * timeSeconds + 0.5 * _gravity * timeSeconds * timeSeconds;
        double newY = currentY + displacement;

        return new TrajectoryPoint
        {
            Y = newY,
            VelocityY = newVelocity,
            TimeOffset = timeSeconds
        };
    }

    /// <summary>
    /// Predicts the full trajectory for a given duration.
    /// </summary>
    public List<TrajectoryPoint> PredictTrajectory(int currentY, double durationSeconds, double stepSeconds = 0.016)
    {
        var points = new List<TrajectoryPoint>();
        double time = 0;
        double y = currentY;
        double velocity = _velocityY;

        while (time <= durationSeconds)
        {
            points.Add(new TrajectoryPoint
            {
                Y = y,
                VelocityY = velocity,
                TimeOffset = time
            });

            // Update for next step
            velocity += _gravity * stepSeconds;
            velocity = Math.Min(velocity, _terminalVelocity);
            y += velocity * stepSeconds;
            time += stepSeconds;
        }

        return points;
    }

    /// <summary>
    /// Predicts trajectory with a jump at a specific time.
    /// </summary>
    public List<TrajectoryPoint> PredictTrajectoryWithJump(int currentY, double jumpAtTime, double durationSeconds, double stepSeconds = 0.016)
    {
        var points = new List<TrajectoryPoint>();
        double time = 0;
        double y = currentY;
        double velocity = _velocityY;
        bool jumped = false;

        while (time <= durationSeconds)
        {
            // Check if it's time to jump
            if (!jumped && time >= jumpAtTime)
            {
                velocity = _jumpVelocity;
                jumped = true;
            }

            points.Add(new TrajectoryPoint
            {
                Y = y,
                VelocityY = velocity,
                TimeOffset = time,
                JumpOccurred = jumped && time == jumpAtTime
            });

            // Update for next step
            velocity += _gravity * stepSeconds;
            velocity = Math.Min(velocity, _terminalVelocity);
            y += velocity * stepSeconds;
            time += stepSeconds;
        }

        return points;
    }

    /// <summary>
    /// Calculates the time to reach a specific Y position (falling).
    /// Returns null if the position won't be reached.
    /// </summary>
    public double? TimeToReachY(int currentY, int targetY)
    {
        // Using quadratic formula to solve: targetY = currentY + v0*t + 0.5*g*t^2
        // 0.5*g*t^2 + v0*t + (currentY - targetY) = 0

        double a = 0.5 * _gravity;
        double b = _velocityY;
        double c = currentY - targetY;

        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return null; // Won't reach this position

        double t1 = (-b + Math.Sqrt(discriminant)) / (2 * a);
        double t2 = (-b - Math.Sqrt(discriminant)) / (2 * a);

        // Return the positive, smaller time (first time we reach the position)
        if (t1 > 0 && t2 > 0)
            return Math.Min(t1, t2);
        if (t1 > 0)
            return t1;
        if (t2 > 0)
            return t2;

        return null;
    }

    /// <summary>
    /// Calculates the time for the player to reach an obstacle's X position.
    /// </summary>
    public double TimeToReachX(int playerX, int obstacleX)
    {
        if (_horizontalSpeed <= 0 || obstacleX <= playerX)
            return 0;

        return (obstacleX - playerX) / _horizontalSpeed;
    }

    /// <summary>
    /// Predicts where the player will be when reaching an obstacle.
    /// </summary>
    public TrajectoryPoint PredictPositionAtObstacle(int playerX, int playerY, int obstacleX)
    {
        double timeToObstacle = TimeToReachX(playerX, obstacleX);
        return PredictPosition(playerY, timeToObstacle);
    }

    /// <summary>
    /// Calculates the Y position after a jump, at a specific time.
    /// </summary>
    public double PositionAfterJump(int currentY, double timeAfterJump)
    {
        // After jump, velocity starts at jumpVelocity
        double displacement = _jumpVelocity * timeAfterJump + 0.5 * _gravity * timeAfterJump * timeAfterJump;
        return currentY + displacement;
    }

    /// <summary>
    /// Finds the optimal jump time to pass through a gap.
    /// </summary>
    public JumpSolution? FindOptimalJumpTime(int playerX, int playerY, int obstacleX, int gapTop, int gapBottom, int safetyMargin = 10)
    {
        double timeToObstacle = TimeToReachX(playerX, obstacleX);
        if (timeToObstacle <= 0)
            return null;

        int gapCenter = (gapTop + gapBottom) / 2;
        int safeTop = gapTop + safetyMargin;
        int safeBottom = gapBottom - safetyMargin;

        // Try different jump times and find the best one
        double bestJumpTime = -1;
        double bestDistance = double.MaxValue;
        double stepSize = 0.01; // 10ms steps

        for (double jumpTime = 0; jumpTime <= timeToObstacle; jumpTime += stepSize)
        {
            // Calculate position at obstacle if we jump at this time
            double timeAfterJump = timeToObstacle - jumpTime;

            // Simulate: fall until jump, then jump trajectory
            var beforeJump = PredictPosition(playerY, jumpTime);
            double yAtJump = beforeJump.Y;

            // After jump
            double yAtObstacle = PositionAfterJump((int)yAtJump, timeAfterJump);

            // Check if within safe gap
            if (yAtObstacle >= safeTop && yAtObstacle <= safeBottom)
            {
                double distanceFromCenter = Math.Abs(yAtObstacle - gapCenter);
                if (distanceFromCenter < bestDistance)
                {
                    bestDistance = distanceFromCenter;
                    bestJumpTime = jumpTime;
                }
            }
        }

        if (bestJumpTime >= 0)
        {
            var beforeJump = PredictPosition(playerY, bestJumpTime);
            double yAtObstacle = PositionAfterJump((int)beforeJump.Y, timeToObstacle - bestJumpTime);

            return new JumpSolution
            {
                JumpTime = bestJumpTime,
                TimeToObstacle = timeToObstacle,
                PredictedYAtObstacle = yAtObstacle,
                GapCenter = gapCenter,
                Confidence = 1.0 - (bestDistance / ((gapBottom - gapTop) / 2.0))
            };
        }

        return null;
    }

    /// <summary>
    /// Resets the velocity tracking.
    /// </summary>
    public void Reset()
    {
        _velocityY = 0;
        _lastUpdateTime = 0;
    }
}

/// <summary>
/// A point on the predicted trajectory.
/// </summary>
public record TrajectoryPoint
{
    public double Y { get; init; }
    public double VelocityY { get; init; }
    public double TimeOffset { get; init; }
    public bool JumpOccurred { get; init; }
}

/// <summary>
/// Solution for when to jump to pass through a gap.
/// </summary>
public record JumpSolution
{
    public double JumpTime { get; init; }
    public double TimeToObstacle { get; init; }
    public double PredictedYAtObstacle { get; init; }
    public double GapCenter { get; init; }
    public double Confidence { get; init; }

    /// <summary>
    /// Whether we should jump immediately (within threshold).
    /// </summary>
    public bool ShouldJumpNow(double thresholdSeconds = 0.05) => JumpTime <= thresholdSeconds;
}
