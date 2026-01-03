using System.Text.Json.Serialization;

namespace JettyBoots.Configuration;

/// <summary>
/// Root configuration class for JettyBoots auto-player.
/// </summary>
public class JettyBootsConfig
{
    /// <summary>
    /// Version of the configuration format.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Screen capture settings.
    /// </summary>
    public CaptureConfig Capture { get; set; } = new();

    /// <summary>
    /// Detection color thresholds and parameters.
    /// </summary>
    public DetectionConfig Detection { get; set; } = new();

    /// <summary>
    /// Gameplay parameters (physics, timing, margins).
    /// </summary>
    public GameplayConfig Gameplay { get; set; } = new();

    /// <summary>
    /// Debug and visualization settings.
    /// </summary>
    public DebugConfig Debug { get; set; } = new();

    /// <summary>
    /// Input simulation settings.
    /// </summary>
    public InputConfig Input { get; set; } = new();

    /// <summary>
    /// Logging settings.
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static JettyBootsConfig Default => new();
}

/// <summary>
/// Screen capture configuration.
/// </summary>
public class CaptureConfig
{
    /// <summary>
    /// Whether to use a custom capture region instead of auto-detecting the game window.
    /// </summary>
    public bool UseCustomRegion { get; set; } = false;

    /// <summary>
    /// Custom capture region X coordinate.
    /// </summary>
    public int RegionX { get; set; } = 0;

    /// <summary>
    /// Custom capture region Y coordinate.
    /// </summary>
    public int RegionY { get; set; } = 0;

    /// <summary>
    /// Custom capture region width.
    /// </summary>
    public int RegionWidth { get; set; } = 800;

    /// <summary>
    /// Custom capture region height.
    /// </summary>
    public int RegionHeight { get; set; } = 600;

    /// <summary>
    /// Target frames per second for capture.
    /// </summary>
    public int TargetFps { get; set; } = 30;
}

/// <summary>
/// Detection color thresholds configuration.
/// </summary>
public class DetectionConfig
{
    /// <summary>
    /// Player detection settings.
    /// </summary>
    public ColorRangeConfig PlayerColor { get; set; } = new()
    {
        HueLower = 0,
        HueUpper = 30,
        SaturationLower = 100,
        SaturationUpper = 255,
        ValueLower = 100,
        ValueUpper = 255
    };

    /// <summary>
    /// Obstacle detection settings.
    /// </summary>
    public ColorRangeConfig ObstacleColor { get; set; } = new()
    {
        HueLower = 35,
        HueUpper = 85,
        SaturationLower = 50,
        SaturationUpper = 255,
        ValueLower = 50,
        ValueUpper = 255
    };

    /// <summary>
    /// Minimum area in pixels for player detection.
    /// </summary>
    public int PlayerMinArea { get; set; } = 100;

    /// <summary>
    /// Maximum area in pixels for player detection.
    /// </summary>
    public int PlayerMaxArea { get; set; } = 10000;

    /// <summary>
    /// Minimum confidence threshold for player detection (0.0 - 1.0).
    /// </summary>
    public double PlayerMinConfidence { get; set; } = 0.5;

    /// <summary>
    /// Minimum obstacle width in pixels.
    /// </summary>
    public int ObstacleMinWidth { get; set; } = 30;

    /// <summary>
    /// Maximum obstacle width in pixels.
    /// </summary>
    public int ObstacleMaxWidth { get; set; } = 150;

    /// <summary>
    /// Minimum gap height in pixels.
    /// </summary>
    public int MinGapHeight { get; set; } = 50;
}

/// <summary>
/// HSV color range configuration.
/// </summary>
public class ColorRangeConfig
{
    public int HueLower { get; set; }
    public int HueUpper { get; set; }
    public int SaturationLower { get; set; }
    public int SaturationUpper { get; set; }
    public int ValueLower { get; set; }
    public int ValueUpper { get; set; }
}

/// <summary>
/// Gameplay parameters configuration.
/// </summary>
public class GameplayConfig
{
    /// <summary>
    /// Physics settings.
    /// </summary>
    public PhysicsConfig Physics { get; set; } = new();

    /// <summary>
    /// Safety margin from gap edges in pixels.
    /// </summary>
    public int SafetyMargin { get; set; } = 15;

    /// <summary>
    /// Seconds before predicted jump time to actually jump.
    /// </summary>
    public double JumpLeadTime { get; set; } = 0.05;

    /// <summary>
    /// Minimum seconds between jumps.
    /// </summary>
    public double MinJumpInterval { get; set; } = 0.15;

    /// <summary>
    /// Floor Y position (death zone).
    /// </summary>
    public int FloorY { get; set; } = 550;

    /// <summary>
    /// Ceiling Y position (death zone).
    /// </summary>
    public int CeilingY { get; set; } = 50;

    /// <summary>
    /// Play style: Safe, Balanced, or Aggressive.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayStyleOption PlayStyle { get; set; } = PlayStyleOption.Balanced;
}

/// <summary>
/// Physics simulation parameters.
/// </summary>
public class PhysicsConfig
{
    /// <summary>
    /// Gravity in pixels per second squared (positive = down).
    /// </summary>
    public double Gravity { get; set; } = 800.0;

    /// <summary>
    /// Jump velocity in pixels per second (negative = up).
    /// </summary>
    public double JumpVelocity { get; set; } = -300.0;

    /// <summary>
    /// Terminal falling velocity in pixels per second.
    /// </summary>
    public double TerminalVelocity { get; set; } = 500.0;

    /// <summary>
    /// Horizontal scroll speed in pixels per second.
    /// </summary>
    public double HorizontalSpeed { get; set; } = 150.0;
}

/// <summary>
/// Play style enumeration.
/// </summary>
public enum PlayStyleOption
{
    Safe,
    Balanced,
    Aggressive
}

/// <summary>
/// Debug and visualization settings.
/// </summary>
public class DebugConfig
{
    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Show the debug overlay window.
    /// </summary>
    public bool ShowDebugWindow { get; set; } = true;

    /// <summary>
    /// Show player detection bounding box.
    /// </summary>
    public bool ShowPlayerBox { get; set; } = true;

    /// <summary>
    /// Show obstacle detection overlays.
    /// </summary>
    public bool ShowObstacles { get; set; } = true;

    /// <summary>
    /// Show gap target zones.
    /// </summary>
    public bool ShowGapTargets { get; set; } = true;

    /// <summary>
    /// Show predicted trajectory line.
    /// </summary>
    public bool ShowTrajectory { get; set; } = true;

    /// <summary>
    /// Show real-time stats (FPS, detection confidence).
    /// </summary>
    public bool ShowStats { get; set; } = true;

    /// <summary>
    /// Save debug frames to disk.
    /// </summary>
    public bool SaveDebugFrames { get; set; } = false;

    /// <summary>
    /// Directory to save debug frames.
    /// </summary>
    public string DebugFrameDirectory { get; set; } = "debug_frames";

    /// <summary>
    /// Interval between saved debug frames (in frames).
    /// </summary>
    public int DebugFrameSaveInterval { get; set; } = 30;
}

/// <summary>
/// Input simulation settings.
/// </summary>
public class InputConfig
{
    /// <summary>
    /// Key to use for jumping (as string representation of VirtualKeyCode).
    /// </summary>
    public string JumpKey { get; set; } = "SPACE";

    /// <summary>
    /// Whether to use mouse click instead of keyboard for jumping.
    /// </summary>
    public bool UseMouseClick { get; set; } = false;

    /// <summary>
    /// Minimum milliseconds between inputs.
    /// </summary>
    public int MinInputIntervalMs { get; set; } = 50;

    /// <summary>
    /// Whether to run in dry-run mode (no inputs sent).
    /// </summary>
    public bool DryRun { get; set; } = false;
}

/// <summary>
/// Logging configuration.
/// </summary>
public class LoggingConfig
{
    /// <summary>
    /// Whether logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum log level: Verbose, Debug, Information, Warning, Error, Fatal.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Log to console.
    /// </summary>
    public bool LogToConsole { get; set; } = true;

    /// <summary>
    /// Log to file.
    /// </summary>
    public bool LogToFile { get; set; } = true;

    /// <summary>
    /// Log file path (relative or absolute).
    /// </summary>
    public string LogFilePath { get; set; } = "logs/jettyboots-.log";

    /// <summary>
    /// Rolling interval for log files.
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Number of log files to retain.
    /// </summary>
    public int RetainedFileCount { get; set; } = 7;

    /// <summary>
    /// Whether to track detailed session statistics.
    /// </summary>
    public bool TrackSessionStats { get; set; } = true;

    /// <summary>
    /// Whether to log individual decisions for analysis.
    /// </summary>
    public bool LogDecisions { get; set; } = false;
}
