using System.Text.Json;
using System.Text.Json.Serialization;

namespace JettyBoots.Configuration;

/// <summary>
/// Manages loading, saving, and validation of JettyBoots configuration.
/// </summary>
public class ConfigurationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string DefaultConfigFileName = "jettyboots.json";

    /// <summary>
    /// Current loaded configuration.
    /// </summary>
    public JettyBootsConfig Config { get; set; } = JettyBootsConfig.Default;

    /// <summary>
    /// Path to the loaded configuration file.
    /// </summary>
    public string? ConfigFilePath { get; private set; }

    /// <summary>
    /// Validation errors from the last load operation.
    /// </summary>
    public List<string> ValidationErrors { get; private set; } = new();

    /// <summary>
    /// Loads configuration from the default file location.
    /// </summary>
    public bool LoadDefault()
    {
        return Load(GetDefaultConfigPath());
    }

    /// <summary>
    /// Loads configuration from the specified file.
    /// </summary>
    public bool Load(string filePath)
    {
        ValidationErrors.Clear();

        if (!File.Exists(filePath))
        {
            ValidationErrors.Add($"Configuration file not found: {filePath}");
            Config = JettyBootsConfig.Default;
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<JettyBootsConfig>(json, JsonOptions);

            if (config == null)
            {
                ValidationErrors.Add("Failed to deserialize configuration (null result)");
                Config = JettyBootsConfig.Default;
                return false;
            }

            var errors = Validate(config);
            if (errors.Count > 0)
            {
                ValidationErrors.AddRange(errors);
                // Still use the config, but report validation issues
            }

            Config = config;
            ConfigFilePath = filePath;
            return errors.Count == 0;
        }
        catch (JsonException ex)
        {
            ValidationErrors.Add($"JSON parsing error: {ex.Message}");
            Config = JettyBootsConfig.Default;
            return false;
        }
        catch (Exception ex)
        {
            ValidationErrors.Add($"Error loading configuration: {ex.Message}");
            Config = JettyBootsConfig.Default;
            return false;
        }
    }

    /// <summary>
    /// Saves the current configuration to the specified file.
    /// </summary>
    public bool Save(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(filePath, json);
            ConfigFilePath = filePath;
            return true;
        }
        catch (Exception ex)
        {
            ValidationErrors.Add($"Error saving configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Saves the current configuration to the default file location.
    /// </summary>
    public bool SaveDefault()
    {
        return Save(GetDefaultConfigPath());
    }

    /// <summary>
    /// Creates a default configuration file if it doesn't exist.
    /// </summary>
    public bool CreateDefaultIfNotExists()
    {
        var path = GetDefaultConfigPath();
        if (File.Exists(path))
        {
            return true;
        }

        Config = JettyBootsConfig.Default;
        return Save(path);
    }

    /// <summary>
    /// Applies command-line argument overrides to the configuration.
    /// </summary>
    public void ApplyCommandLineOverrides(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Handle --key=value format
            if (arg.StartsWith("--") && arg.Contains('='))
            {
                var parts = arg[2..].Split('=', 2);
                if (parts.Length == 2)
                {
                    ApplyOverride(parts[0], parts[1]);
                }
                continue;
            }

            // Handle --key value format
            if (arg.StartsWith("--") && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                ApplyOverride(arg[2..], args[i + 1]);
                i++; // Skip the value
                continue;
            }

            // Handle boolean flags
            if (arg.StartsWith("--"))
            {
                ApplyBooleanFlag(arg[2..]);
            }
        }
    }

    private void ApplyOverride(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            // Capture settings
            case "fps":
            case "target-fps":
                if (int.TryParse(value, out var fps))
                    Config.Capture.TargetFps = fps;
                break;

            case "region-x":
                if (int.TryParse(value, out var x))
                    Config.Capture.RegionX = x;
                break;

            case "region-y":
                if (int.TryParse(value, out var y))
                    Config.Capture.RegionY = y;
                break;

            case "region-width":
                if (int.TryParse(value, out var w))
                    Config.Capture.RegionWidth = w;
                break;

            case "region-height":
                if (int.TryParse(value, out var h))
                    Config.Capture.RegionHeight = h;
                break;

            // Gameplay settings
            case "safety-margin":
                if (int.TryParse(value, out var margin))
                    Config.Gameplay.SafetyMargin = margin;
                break;

            case "play-style":
                if (Enum.TryParse<PlayStyleOption>(value, true, out var style))
                    Config.Gameplay.PlayStyle = style;
                break;

            case "gravity":
                if (double.TryParse(value, out var gravity))
                    Config.Gameplay.Physics.Gravity = gravity;
                break;

            case "jump-velocity":
                if (double.TryParse(value, out var jumpVel))
                    Config.Gameplay.Physics.JumpVelocity = jumpVel;
                break;

            case "horizontal-speed":
                if (double.TryParse(value, out var hSpeed))
                    Config.Gameplay.Physics.HorizontalSpeed = hSpeed;
                break;

            // Input settings
            case "jump-key":
                Config.Input.JumpKey = value.ToUpperInvariant();
                break;

            // Logging settings
            case "log-level":
                Config.Logging.MinimumLevel = value;
                break;

            case "log-file":
                Config.Logging.LogFilePath = value;
                break;
        }
    }

    private void ApplyBooleanFlag(string key)
    {
        switch (key.ToLowerInvariant())
        {
            case "dry-run":
                Config.Input.DryRun = true;
                break;

            case "no-debug":
                Config.Debug.Enabled = false;
                break;

            case "debug":
                Config.Debug.Enabled = true;
                break;

            case "no-debug-window":
                Config.Debug.ShowDebugWindow = false;
                break;

            case "save-frames":
                Config.Debug.SaveDebugFrames = true;
                break;

            case "use-mouse":
                Config.Input.UseMouseClick = true;
                break;

            case "use-custom-region":
                Config.Capture.UseCustomRegion = true;
                break;

            case "no-logging":
                Config.Logging.Enabled = false;
                break;

            case "log-decisions":
                Config.Logging.LogDecisions = true;
                break;
        }
    }

    /// <summary>
    /// Validates a configuration and returns a list of errors.
    /// </summary>
    public static List<string> Validate(JettyBootsConfig config)
    {
        var errors = new List<string>();

        // Capture validation
        if (config.Capture.TargetFps < 1 || config.Capture.TargetFps > 120)
            errors.Add("TargetFps must be between 1 and 120");

        if (config.Capture.UseCustomRegion)
        {
            if (config.Capture.RegionWidth <= 0)
                errors.Add("RegionWidth must be positive");
            if (config.Capture.RegionHeight <= 0)
                errors.Add("RegionHeight must be positive");
        }

        // Detection validation
        ValidateColorRange(config.Detection.PlayerColor, "PlayerColor", errors);
        ValidateColorRange(config.Detection.ObstacleColor, "ObstacleColor", errors);

        if (config.Detection.PlayerMinArea < 0)
            errors.Add("PlayerMinArea must be non-negative");
        if (config.Detection.PlayerMaxArea <= config.Detection.PlayerMinArea)
            errors.Add("PlayerMaxArea must be greater than PlayerMinArea");
        if (config.Detection.PlayerMinConfidence < 0 || config.Detection.PlayerMinConfidence > 1)
            errors.Add("PlayerMinConfidence must be between 0 and 1");

        // Gameplay validation
        if (config.Gameplay.SafetyMargin < 0)
            errors.Add("SafetyMargin must be non-negative");
        if (config.Gameplay.JumpLeadTime < 0)
            errors.Add("JumpLeadTime must be non-negative");
        if (config.Gameplay.MinJumpInterval < 0)
            errors.Add("MinJumpInterval must be non-negative");

        // Physics validation
        if (config.Gameplay.Physics.Gravity <= 0)
            errors.Add("Gravity must be positive");
        if (config.Gameplay.Physics.TerminalVelocity <= 0)
            errors.Add("TerminalVelocity must be positive");
        if (config.Gameplay.Physics.HorizontalSpeed <= 0)
            errors.Add("HorizontalSpeed must be positive");

        // Input validation
        if (config.Input.MinInputIntervalMs < 0)
            errors.Add("MinInputIntervalMs must be non-negative");

        return errors;
    }

    private static void ValidateColorRange(ColorRangeConfig color, string name, List<string> errors)
    {
        if (color.HueLower < 0 || color.HueLower > 180)
            errors.Add($"{name}.HueLower must be between 0 and 180");
        if (color.HueUpper < 0 || color.HueUpper > 180)
            errors.Add($"{name}.HueUpper must be between 0 and 180");
        if (color.SaturationLower < 0 || color.SaturationLower > 255)
            errors.Add($"{name}.SaturationLower must be between 0 and 255");
        if (color.SaturationUpper < 0 || color.SaturationUpper > 255)
            errors.Add($"{name}.SaturationUpper must be between 0 and 255");
        if (color.ValueLower < 0 || color.ValueLower > 255)
            errors.Add($"{name}.ValueLower must be between 0 and 255");
        if (color.ValueUpper < 0 || color.ValueUpper > 255)
            errors.Add($"{name}.ValueUpper must be between 0 and 255");
    }

    /// <summary>
    /// Gets the default configuration file path.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        var exePath = AppContext.BaseDirectory;
        return Path.Combine(exePath, DefaultConfigFileName);
    }

    /// <summary>
    /// Prints the current configuration to the console.
    /// </summary>
    public void PrintConfig()
    {
        Console.WriteLine("Current Configuration:");
        Console.WriteLine("======================");
        Console.WriteLine();

        Console.WriteLine("Capture:");
        Console.WriteLine($"  Target FPS: {Config.Capture.TargetFps}");
        Console.WriteLine($"  Use Custom Region: {Config.Capture.UseCustomRegion}");
        if (Config.Capture.UseCustomRegion)
        {
            Console.WriteLine($"  Region: ({Config.Capture.RegionX}, {Config.Capture.RegionY}) {Config.Capture.RegionWidth}x{Config.Capture.RegionHeight}");
        }
        Console.WriteLine();

        Console.WriteLine("Gameplay:");
        Console.WriteLine($"  Play Style: {Config.Gameplay.PlayStyle}");
        Console.WriteLine($"  Safety Margin: {Config.Gameplay.SafetyMargin}px");
        Console.WriteLine($"  Jump Lead Time: {Config.Gameplay.JumpLeadTime}s");
        Console.WriteLine($"  Min Jump Interval: {Config.Gameplay.MinJumpInterval}s");
        Console.WriteLine();

        Console.WriteLine("Physics:");
        Console.WriteLine($"  Gravity: {Config.Gameplay.Physics.Gravity} px/s^2");
        Console.WriteLine($"  Jump Velocity: {Config.Gameplay.Physics.JumpVelocity} px/s");
        Console.WriteLine($"  Horizontal Speed: {Config.Gameplay.Physics.HorizontalSpeed} px/s");
        Console.WriteLine();

        Console.WriteLine("Input:");
        Console.WriteLine($"  Jump Key: {Config.Input.JumpKey}");
        Console.WriteLine($"  Use Mouse: {Config.Input.UseMouseClick}");
        Console.WriteLine($"  Dry Run: {Config.Input.DryRun}");
        Console.WriteLine();

        Console.WriteLine("Debug:");
        Console.WriteLine($"  Enabled: {Config.Debug.Enabled}");
        Console.WriteLine($"  Show Window: {Config.Debug.ShowDebugWindow}");
        Console.WriteLine();

        Console.WriteLine("Logging:");
        Console.WriteLine($"  Enabled: {Config.Logging.Enabled}");
        Console.WriteLine($"  Level: {Config.Logging.MinimumLevel}");
        Console.WriteLine($"  Log to File: {Config.Logging.LogToFile}");
    }
}
