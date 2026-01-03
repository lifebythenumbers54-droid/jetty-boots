namespace JettyBoots.Logging;

/// <summary>
/// Tracks statistics for a game session.
/// </summary>
public class SessionStats
{
    // Game statistics
    public int GamesPlayed { get; set; }
    public int HighScore { get; set; }
    public int TotalScore { get; set; }
    public int TotalObstaclesPassed { get; set; }

    // Performance statistics
    public int TotalFrames { get; set; }
    public int TotalJumps { get; set; }

    // FPS tracking (running average)
    private double _fpsSum;
    private int _fpsCount;

    // Analysis time tracking
    private double _analysisTimeSum;
    private int _analysisTimeCount;

    /// <summary>
    /// Average score across all games.
    /// </summary>
    public double AverageScore => GamesPlayed > 0 ? (double)TotalScore / GamesPlayed : 0;

    /// <summary>
    /// Average obstacles passed per game.
    /// </summary>
    public double AverageObstaclesPerGame => GamesPlayed > 0 ? (double)TotalObstaclesPassed / GamesPlayed : 0;

    /// <summary>
    /// Average frames per second.
    /// </summary>
    public double AverageFps => _fpsCount > 0 ? _fpsSum / _fpsCount : 0;

    /// <summary>
    /// Average analysis time in milliseconds.
    /// </summary>
    public double AverageAnalysisTimeMs => _analysisTimeCount > 0 ? _analysisTimeSum / _analysisTimeCount : 0;

    /// <summary>
    /// Jumps per minute rate.
    /// </summary>
    public double JumpsPerMinute(TimeSpan sessionDuration)
    {
        if (sessionDuration.TotalMinutes <= 0)
            return 0;
        return TotalJumps / sessionDuration.TotalMinutes;
    }

    /// <summary>
    /// Updates the running FPS average.
    /// </summary>
    public void UpdateFps(double fps)
    {
        _fpsSum += fps;
        _fpsCount++;
    }

    /// <summary>
    /// Updates the running analysis time average.
    /// </summary>
    public void UpdateAnalysisTime(double analysisTimeMs)
    {
        _analysisTimeSum += analysisTimeMs;
        _analysisTimeCount++;
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void Reset()
    {
        GamesPlayed = 0;
        HighScore = 0;
        TotalScore = 0;
        TotalObstaclesPassed = 0;
        TotalFrames = 0;
        TotalJumps = 0;
        _fpsSum = 0;
        _fpsCount = 0;
        _analysisTimeSum = 0;
        _analysisTimeCount = 0;
    }

    /// <summary>
    /// Creates a snapshot of current statistics.
    /// </summary>
    public SessionStatsSnapshot CreateSnapshot(TimeSpan sessionDuration)
    {
        return new SessionStatsSnapshot
        {
            GamesPlayed = GamesPlayed,
            HighScore = HighScore,
            TotalScore = TotalScore,
            AverageScore = AverageScore,
            TotalObstaclesPassed = TotalObstaclesPassed,
            AverageObstaclesPerGame = AverageObstaclesPerGame,
            TotalFrames = TotalFrames,
            TotalJumps = TotalJumps,
            AverageFps = AverageFps,
            AverageAnalysisTimeMs = AverageAnalysisTimeMs,
            JumpsPerMinute = JumpsPerMinute(sessionDuration),
            SessionDuration = sessionDuration
        };
    }

    public override string ToString()
    {
        return $"Games: {GamesPlayed}, High: {HighScore}, Avg: {AverageScore:F1}, Frames: {TotalFrames}, Jumps: {TotalJumps}, FPS: {AverageFps:F1}";
    }
}

/// <summary>
/// Immutable snapshot of session statistics at a point in time.
/// </summary>
public record SessionStatsSnapshot
{
    public int GamesPlayed { get; init; }
    public int HighScore { get; init; }
    public int TotalScore { get; init; }
    public double AverageScore { get; init; }
    public int TotalObstaclesPassed { get; init; }
    public double AverageObstaclesPerGame { get; init; }
    public int TotalFrames { get; init; }
    public int TotalJumps { get; init; }
    public double AverageFps { get; init; }
    public double AverageAnalysisTimeMs { get; init; }
    public double JumpsPerMinute { get; init; }
    public TimeSpan SessionDuration { get; init; }
}
