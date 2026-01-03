using JettyBoots.Configuration;
using JettyBoots.Decision;
using JettyBoots.GameState;
using Serilog;
using Serilog.Events;

namespace JettyBoots.Logging;

/// <summary>
/// Centralized logging service for JettyBoots using Serilog.
/// </summary>
public class GameLogger : IDisposable
{
    private readonly LoggingConfig _config;
    private readonly ILogger _logger;
    private readonly SessionStats _sessionStats = new();
    private bool _disposed;

    // Session tracking
    private DateTime _sessionStart;
    private Guid _sessionId;
    private int _currentGameNumber;
    private DateTime _currentGameStart;

    public GameLogger(LoggingConfig config)
    {
        _config = config;
        _sessionId = Guid.NewGuid();
        _sessionStart = DateTime.Now;

        _logger = CreateLogger();

        if (_config.Enabled)
        {
            LogSessionStart();
        }
    }

    /// <summary>
    /// Gets the current session statistics.
    /// </summary>
    public SessionStats SessionStats => _sessionStats;

    /// <summary>
    /// Gets the underlying Serilog logger.
    /// </summary>
    public ILogger Logger => _logger;

    private ILogger CreateLogger()
    {
        if (!_config.Enabled)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Fatal()
                .CreateLogger();
        }

        var logConfig = new LoggerConfiguration();

        // Set minimum level
        var level = _config.MinimumLevel.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
        logConfig.MinimumLevel.Is(level);

        // Add console sink
        if (_config.LogToConsole)
        {
            logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        // Add file sink
        if (_config.LogToFile && !string.IsNullOrEmpty(_config.LogFilePath))
        {
            var rollInterval = _config.RollingInterval.ToLowerInvariant() switch
            {
                "minute" => RollingInterval.Minute,
                "hour" => RollingInterval.Hour,
                "day" => RollingInterval.Day,
                "month" => RollingInterval.Month,
                "year" => RollingInterval.Year,
                _ => RollingInterval.Day
            };

            // Ensure log directory exists
            var logDir = Path.GetDirectoryName(_config.LogFilePath);
            if (!string.IsNullOrEmpty(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            logConfig.WriteTo.File(
                _config.LogFilePath,
                rollingInterval: rollInterval,
                retainedFileCountLimit: _config.RetainedFileCount,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        // Enrich with session info
        logConfig.Enrich.WithProperty("SessionId", _sessionId);

        return logConfig.CreateLogger();
    }

    #region Session Logging

    private void LogSessionStart()
    {
        _logger.Information("=== JettyBoots Session Started ===");
        _logger.Information("Session ID: {SessionId}", _sessionId);
        _logger.Information("Start Time: {StartTime}", _sessionStart);
        _logger.Information("Configuration: Logging level {Level}, Decisions: {LogDecisions}",
            _config.MinimumLevel, _config.LogDecisions);
    }

    /// <summary>
    /// Logs session end and generates summary.
    /// </summary>
    public void LogSessionEnd()
    {
        var duration = DateTime.Now - _sessionStart;

        _logger.Information("=== JettyBoots Session Ended ===");
        _logger.Information("Session Duration: {Duration}", duration);
        _logger.Information("Games Played: {GamesPlayed}", _sessionStats.GamesPlayed);
        _logger.Information("High Score: {HighScore}", _sessionStats.HighScore);
        _logger.Information("Average Score: {AverageScore:F1}", _sessionStats.AverageScore);
        _logger.Information("Total Frames: {TotalFrames}", _sessionStats.TotalFrames);
        _logger.Information("Total Jumps: {TotalJumps}", _sessionStats.TotalJumps);
        _logger.Information("Average FPS: {AverageFps:F1}", _sessionStats.AverageFps);
    }

    #endregion

    #region Game Logging

    /// <summary>
    /// Logs the start of a new game.
    /// </summary>
    public void LogGameStart()
    {
        _currentGameNumber++;
        _currentGameStart = DateTime.Now;
        _sessionStats.GamesPlayed++;

        _logger.Information("Game {GameNumber} started", _currentGameNumber);
    }

    /// <summary>
    /// Logs game over with score.
    /// </summary>
    public void LogGameEnd(int? score = null, int obstaclesPassed = 0)
    {
        var gameDuration = DateTime.Now - _currentGameStart;

        // Update session stats
        if (score.HasValue)
        {
            _sessionStats.TotalScore += score.Value;
            if (score.Value > _sessionStats.HighScore)
            {
                _sessionStats.HighScore = score.Value;
            }
        }
        _sessionStats.TotalObstaclesPassed += obstaclesPassed;

        _logger.Information("Game {GameNumber} ended - Duration: {Duration}, Score: {Score}, Obstacles: {Obstacles}",
            _currentGameNumber, gameDuration, score?.ToString() ?? "N/A", obstaclesPassed);
    }

    #endregion

    #region Decision Logging

    /// <summary>
    /// Logs a decision made by the decision engine.
    /// </summary>
    public void LogDecision(ActionDecision decision, DecisionDebugInfo debugInfo)
    {
        if (!_config.LogDecisions)
            return;

        if (decision.Action == GameAction.Jump)
        {
            _sessionStats.TotalJumps++;
            _logger.Debug("JUMP - Reason: {Reason}, Confidence: {Confidence:P0}, PlayerY: {PlayerY}, TimeToObs: {TimeToObstacle:F2}s",
                decision.Reason, decision.Confidence, debugInfo.PlayerY, debugInfo.TimeToObstacle);
        }
        else if (decision.Action != GameAction.None)
        {
            _logger.Debug("ACTION: {Action} - Reason: {Reason}",
                decision.Action, decision.Reason);
        }
    }

    /// <summary>
    /// Logs a jump action specifically.
    /// </summary>
    public void LogJump(int playerY, int? targetY, string reason)
    {
        _sessionStats.TotalJumps++;

        _logger.Debug("JUMP at Y={PlayerY}, Target={TargetY}, Reason: {Reason}",
            playerY, targetY?.ToString() ?? "N/A", reason);
    }

    #endregion

    #region Detection Logging

    /// <summary>
    /// Logs detection results.
    /// </summary>
    public void LogDetection(FrameAnalysis analysis)
    {
        _sessionStats.TotalFrames++;

        // Only log at verbose level unless something interesting happens
        if (analysis.Player.Detected && analysis.Obstacles.Detected)
        {
            _logger.Verbose("Frame: Player at ({X},{Y}), {ObstacleCount} obstacles, State: {State}",
                analysis.Player.CenterX, analysis.Player.CenterY,
                analysis.Obstacles.Obstacles.Count, analysis.GameState.State);
        }
        else if (!analysis.Player.Detected)
        {
            _logger.Debug("Player not detected in frame");
        }
    }

    /// <summary>
    /// Logs obstacle passage.
    /// </summary>
    public void LogObstaclePassed(int obstacleX)
    {
        _sessionStats.TotalObstaclesPassed++;
        _logger.Debug("Obstacle passed at X={ObstacleX}", obstacleX);
    }

    #endregion

    #region Performance Logging

    /// <summary>
    /// Logs performance metrics.
    /// </summary>
    public void LogPerformance(double fps, double analysisTimeMs, double captureTimeMs)
    {
        _sessionStats.UpdateFps(fps);
        _sessionStats.UpdateAnalysisTime(analysisTimeMs);

        _logger.Verbose("Performance - FPS: {Fps:F1}, Analysis: {AnalysisMs:F1}ms, Capture: {CaptureMs:F1}ms",
            fps, analysisTimeMs, captureTimeMs);
    }

    /// <summary>
    /// Logs an error.
    /// </summary>
    public void LogError(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.Error(ex, message);
        }
        else
        {
            _logger.Error(message);
        }
    }

    /// <summary>
    /// Logs a warning.
    /// </summary>
    public void LogWarning(string message)
    {
        _logger.Warning(message);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void LogInfo(string message)
    {
        _logger.Information(message);
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public void LogDebug(string message)
    {
        _logger.Debug(message);
    }

    #endregion

    #region Session Report

    /// <summary>
    /// Generates a session summary report.
    /// </summary>
    public string GenerateSessionReport()
    {
        var duration = DateTime.Now - _sessionStart;
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== JettyBoots Session Report ===");
        report.AppendLine();
        report.AppendLine($"Session ID: {_sessionId}");
        report.AppendLine($"Start Time: {_sessionStart:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Duration: {duration:hh\\:mm\\:ss}");
        report.AppendLine();
        report.AppendLine("--- Game Statistics ---");
        report.AppendLine($"Games Played: {_sessionStats.GamesPlayed}");
        report.AppendLine($"High Score: {_sessionStats.HighScore}");
        report.AppendLine($"Average Score: {_sessionStats.AverageScore:F1}");
        report.AppendLine($"Total Obstacles Passed: {_sessionStats.TotalObstaclesPassed}");
        report.AppendLine($"Avg Obstacles/Game: {_sessionStats.AverageObstaclesPerGame:F1}");
        report.AppendLine();
        report.AppendLine("--- Performance ---");
        report.AppendLine($"Total Frames: {_sessionStats.TotalFrames}");
        report.AppendLine($"Total Jumps: {_sessionStats.TotalJumps}");
        report.AppendLine($"Average FPS: {_sessionStats.AverageFps:F1}");
        report.AppendLine($"Average Analysis Time: {_sessionStats.AverageAnalysisTimeMs:F2}ms");
        report.AppendLine();
        report.AppendLine("=================================");

        return report.ToString();
    }

    /// <summary>
    /// Saves the session report to a file.
    /// </summary>
    public void SaveSessionReport(string? filePath = null)
    {
        filePath ??= $"session_report_{_sessionId:N}.txt";

        var report = GenerateSessionReport();
        File.WriteAllText(filePath, report);

        _logger.Information("Session report saved to: {FilePath}", filePath);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_config.Enabled)
        {
            LogSessionEnd();
        }

        (_logger as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
