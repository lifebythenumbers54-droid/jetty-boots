using JettyBoots.Calibration;
using JettyBoots.Configuration;
using JettyBoots.Debug;
using JettyBoots.Decision;
using JettyBoots.GameState;
using JettyBoots.Input;
using JettyBoots.Logging;
using JettyBoots.ScreenCapture;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace JettyBoots;

class Program
{
    private static ConfigurationManager _configManager = new();
    private static GameLogger? _logger;

    static void Main(string[] args)
    {
        Console.WriteLine("=== Jetty Boots Auto-Player ===");
        Console.WriteLine("Automated player for the Jetty Boots minigame in Deep Rock Galactic\n");

        // Load configuration
        LoadConfiguration(args);

        // Initialize logging
        _logger = new GameLogger(_configManager.Config.Logging);

        try
        {
            // Parse command and execute
            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            if (args.Contains("--test-capture"))
            {
                RunCaptureTest();
                return;
            }

            if (args.Contains("--test-detection"))
            {
                RunDetectionTest();
                return;
            }

            if (args.Contains("--test-decision"))
            {
                RunDecisionTest();
                return;
            }

            if (args.Contains("--live-detection"))
            {
                RunLiveDetection();
                return;
            }

            if (args.Contains("--live-decision"))
            {
                RunLiveDecision();
                return;
            }

            if (args.Contains("--list-windows"))
            {
                ListWindows();
                return;
            }

            if (args.Contains("--find-game"))
            {
                FindGameWindow();
                return;
            }

            if (args.Contains("--calibrate"))
            {
                RunCalibration();
                return;
            }

            if (args.Contains("--show-config"))
            {
                _configManager.PrintConfig();
                return;
            }

            if (args.Contains("--create-config"))
            {
                CreateDefaultConfig();
                return;
            }

            if (args.Contains("--play"))
            {
                bool dryRun = args.Contains("--dry-run") || _configManager.Config.Input.DryRun;
                bool autoStart = args.Contains("--auto-start");
                bool verbose = args.Contains("--verbose") || args.Contains("-v");
                RunAutoPlayer(dryRun, autoStart, verbose);
                return;
            }

            ShowHelp();
        }
        finally
        {
            _logger?.Dispose();
        }
    }

    static void LoadConfiguration(string[] args)
    {
        // Check for custom config file
        string? configPath = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config" || args[i] == "-c")
            {
                configPath = args[i + 1];
                break;
            }
        }

        // Load config
        if (!string.IsNullOrEmpty(configPath))
        {
            if (_configManager.Load(configPath))
            {
                Console.WriteLine($"Loaded configuration from: {configPath}");
            }
            else
            {
                Console.WriteLine($"Warning: Failed to load config from {configPath}");
                foreach (var error in _configManager.ValidationErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Console.WriteLine("Using default configuration.");
            }
        }
        else if (File.Exists(ConfigurationManager.GetDefaultConfigPath()))
        {
            if (_configManager.LoadDefault())
            {
                Console.WriteLine($"Loaded configuration from: {ConfigurationManager.GetDefaultConfigPath()}");
            }
            else
            {
                foreach (var error in _configManager.ValidationErrors)
                {
                    Console.WriteLine($"  Warning: {error}");
                }
            }
        }

        // Apply command-line overrides
        _configManager.ApplyCommandLineOverrides(args);
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage: JettyBoots [options]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  --play              Run the auto-player (sends inputs to game)");
        Console.WriteLine("  --play --dry-run    Run auto-player without sending inputs");
        Console.WriteLine("  --play --auto-start Auto-focus window, hold E, and start minigame");
        Console.WriteLine("  --play --verbose    Enable detailed logging for debugging");
        Console.WriteLine("  --calibrate         Run the calibration wizard");
        Console.WriteLine("  --test-capture      Test screen capture functionality");
        Console.WriteLine("  --test-detection    Test detection on a single frame");
        Console.WriteLine("  --test-decision     Test decision engine with trajectory simulation");
        Console.WriteLine("  --live-detection    Run live detection with visual overlay");
        Console.WriteLine("  --live-decision     Run live detection + decision making");
        Console.WriteLine("  --list-windows      List all visible windows");
        Console.WriteLine("  --find-game         Find Deep Rock Galactic window");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  --config, -c <file> Load configuration from specified file");
        Console.WriteLine("  --show-config       Display current configuration");
        Console.WriteLine("  --create-config     Create default configuration file");
        Console.WriteLine();
        Console.WriteLine("Configuration Overrides:");
        Console.WriteLine("  --fps <value>           Target frames per second");
        Console.WriteLine("  --safety-margin <px>    Safety margin from gap edges");
        Console.WriteLine("  --play-style <style>    Play style: Safe, Balanced, Aggressive");
        Console.WriteLine("  --jump-key <key>        Key for jumping (e.g., SPACE, UP)");
        Console.WriteLine("  --use-mouse             Use mouse click instead of keyboard");
        Console.WriteLine("  --no-debug              Disable debug overlay");
        Console.WriteLine("  --no-debug-window       Disable debug window");
        Console.WriteLine("  --save-frames           Save debug frames to disk");
        Console.WriteLine("  --log-level <level>     Log level: Verbose, Debug, Information, Warning, Error");
        Console.WriteLine("  --log-decisions         Log individual decisions for analysis");
        Console.WriteLine("  --no-logging            Disable all logging");
        Console.WriteLine();
        Console.WriteLine("  --help, -h          Show this help message");
        Console.WriteLine();
        Console.WriteLine("Press any key to run a quick capture test...");
        Console.ReadKey(true);
        RunCaptureTest();
    }

    static void CreateDefaultConfig()
    {
        var path = ConfigurationManager.GetDefaultConfigPath();
        if (File.Exists(path))
        {
            Console.Write($"Configuration file already exists at {path}. Overwrite? (y/n): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        _configManager.Config = JettyBootsConfig.Default;
        if (_configManager.Save(path))
        {
            Console.WriteLine($"Default configuration created at: {path}");
        }
        else
        {
            Console.WriteLine("Failed to create configuration file.");
        }
    }

    static void RunCalibration()
    {
        Console.WriteLine("Starting calibration wizard...\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);
        using var wizard = new CalibrationWizard(_configManager, capture);

        if (wizard.Run())
        {
            Console.WriteLine("\nCalibration completed successfully!");
        }
        else
        {
            Console.WriteLine("\nCalibration was cancelled or failed.");
        }
    }

    static void ListWindows()
    {
        Console.WriteLine("Visible Windows:\n");
        var windows = WindowHelper.GetAllVisibleWindows();

        foreach (var (handle, title) in windows.OrderBy(w => w.Title))
        {
            Console.WriteLine($"  [{handle:X8}] {title}");
        }

        Console.WriteLine($"\nTotal: {windows.Count} windows");
    }

    static void FindGameWindow()
    {
        Console.WriteLine("Searching for Deep Rock Galactic...\n");

        var hwnd = WindowHelper.FindDeepRockGalacticWindow();

        if (hwnd != IntPtr.Zero)
        {
            var title = WindowHelper.GetWindowTitle(hwnd);
            Console.WriteLine($"Found: {title}");
            Console.WriteLine($"Handle: 0x{hwnd:X8}");

            if (WindowHelper.GetWindowRect(hwnd, out var rect))
            {
                Console.WriteLine($"Position: ({rect.Left}, {rect.Top})");
                Console.WriteLine($"Size: {rect.Right - rect.Left} x {rect.Bottom - rect.Top}");
            }

            var clientRegion = WindowHelper.GetClientRegion(hwnd);
            if (clientRegion != null)
            {
                Console.WriteLine($"Client Area: {clientRegion.Width} x {clientRegion.Height} at ({clientRegion.X}, {clientRegion.Y})");
            }
        }
        else
        {
            Console.WriteLine("Deep Rock Galactic window not found.");
            Console.WriteLine("Make sure the game is running.");
        }
    }

    static CaptureRegion GetCaptureRegion()
    {
        var config = _configManager.Config.Capture;

        // Use custom region if configured
        if (config.UseCustomRegion)
        {
            Console.WriteLine($"Using configured region: {config.RegionWidth}x{config.RegionHeight} at ({config.RegionX}, {config.RegionY})");
            return new CaptureRegion(config.RegionX, config.RegionY, config.RegionWidth, config.RegionHeight);
        }

        // Try to find game window
        var gameHwnd = WindowHelper.FindDeepRockGalacticWindow();

        if (gameHwnd != IntPtr.Zero)
        {
            var clientRegion = WindowHelper.GetClientRegion(gameHwnd);
            if (clientRegion != null)
            {
                Console.WriteLine($"Capturing Deep Rock Galactic window: {clientRegion.Width}x{clientRegion.Height}");
                return clientRegion;
            }
        }

        Console.WriteLine("Game not found. Capturing screen region (800x600) for testing...");
        return new CaptureRegion(0, 0, 800, 600);
    }

    static void ApplyConfigToAnalyzer(GameAnalyzer analyzer)
    {
        var detection = _configManager.Config.Detection;

        var config = new GameState.DetectionConfig
        {
            PlayerColorLower = new Scalar(
                detection.PlayerColor.HueLower,
                detection.PlayerColor.SaturationLower,
                detection.PlayerColor.ValueLower),
            PlayerColorUpper = new Scalar(
                detection.PlayerColor.HueUpper,
                detection.PlayerColor.SaturationUpper,
                detection.PlayerColor.ValueUpper),
            ObstacleColorLower = new Scalar(
                detection.ObstacleColor.HueLower,
                detection.ObstacleColor.SaturationLower,
                detection.ObstacleColor.ValueLower),
            ObstacleColorUpper = new Scalar(
                detection.ObstacleColor.HueUpper,
                detection.ObstacleColor.SaturationUpper,
                detection.ObstacleColor.ValueUpper)
        };

        analyzer.Configure(config);
    }

    static void ApplyConfigToDecisionEngine(DecisionEngine engine)
    {
        var gameplay = _configManager.Config.Gameplay;
        var physics = gameplay.Physics;

        // Apply physics settings
        engine.Trajectory.SetPhysics(
            physics.Gravity,
            physics.JumpVelocity,
            physics.TerminalVelocity,
            physics.HorizontalSpeed);

        // Apply play style
        var playStyle = gameplay.PlayStyle switch
        {
            PlayStyleOption.Safe => PlayStyle.Safe,
            PlayStyleOption.Aggressive => PlayStyle.Aggressive,
            _ => PlayStyle.Balanced
        };
        engine.SetPlayStyle(playStyle);

        // Apply jump decider settings
        engine.JumpDecider.SetSafetyMargin(gameplay.SafetyMargin);
        engine.JumpDecider.SetBoundaries(gameplay.FloorY, gameplay.CeilingY);
    }

    static void RunCaptureTest()
    {
        Console.WriteLine("Running screen capture test...\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);

        // Warm up
        capture.CaptureFrame()?.Dispose();

        // Capture multiple frames to test performance
        const int testFrames = 60;
        Console.WriteLine($"\nCapturing {testFrames} frames...\n");

        for (int i = 0; i < testFrames; i++)
        {
            using var frame = capture.CaptureFrame();

            if (frame == null)
            {
                Console.WriteLine($"Frame {i + 1}: FAILED");
                continue;
            }

            var metrics = capture.Metrics;

            // Show progress every 10 frames
            if ((i + 1) % 10 == 0 || i == 0)
            {
                Console.WriteLine($"Frame {i + 1,3}: {metrics.CaptureTimeMs:F1}ms | FPS: {metrics.FramesPerSecond:F1} | Size: {frame.Width}x{frame.Height}");
            }

            // Save first frame for inspection
            if (i == 0)
            {
                string filename = "test_capture.png";
                Cv2.ImWrite(filename, frame);
                Console.WriteLine($"\nSaved first frame to: {filename}\n");
            }

            Thread.Sleep(16); // ~60 FPS target
        }

        var finalMetrics = capture.Metrics;
        Console.WriteLine($"\n=== Results ===");
        Console.WriteLine($"Total frames captured: {finalMetrics.FrameCount}");
        Console.WriteLine($"Average capture time: {finalMetrics.CaptureTimeMs:F2}ms");
        Console.WriteLine($"Estimated max FPS: {1000.0 / finalMetrics.CaptureTimeMs:F1}");
        Console.WriteLine($"Achieved FPS: {finalMetrics.FramesPerSecond:F1}");
    }

    static void RunDetectionTest()
    {
        Console.WriteLine("Running detection test...\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);
        using var analyzer = new GameAnalyzer();

        ApplyConfigToAnalyzer(analyzer);

        // Capture a single frame
        using var frame = capture.CaptureFrame();

        if (frame == null)
        {
            Console.WriteLine("Failed to capture frame!");
            return;
        }

        Console.WriteLine($"Captured frame: {frame.Width}x{frame.Height}\n");

        // Analyze the frame
        var analysis = analyzer.Analyze(frame);

        // Print results
        Console.WriteLine("=== Detection Results ===\n");

        Console.WriteLine($"Analysis time: {analysis.AnalysisTimeMs:F2}ms");
        Console.WriteLine();

        Console.WriteLine("Player Detection:");
        if (analysis.Player.Detected)
        {
            Console.WriteLine($"  Position: ({analysis.Player.X}, {analysis.Player.Y})");
            Console.WriteLine($"  Size: {analysis.Player.Width}x{analysis.Player.Height}");
            Console.WriteLine($"  Center: ({analysis.Player.CenterX}, {analysis.Player.CenterY})");
            Console.WriteLine($"  Confidence: {analysis.Player.Confidence:P1}");
        }
        else
        {
            Console.WriteLine("  Not detected");
        }
        Console.WriteLine();

        Console.WriteLine("Obstacle Detection:");
        if (analysis.Obstacles.Detected)
        {
            Console.WriteLine($"  Found {analysis.Obstacles.Obstacles.Count} obstacle(s)");
            foreach (var obs in analysis.Obstacles.Obstacles)
            {
                Console.WriteLine($"  - X: {obs.X}, Width: {obs.Width}");
                Console.WriteLine($"    Gap: Y={obs.GapTop} to Y={obs.GapBottom} (height: {obs.GapHeight})");
                Console.WriteLine($"    Gap center: Y={obs.GapCenterY}");
            }
            Console.WriteLine($"  Confidence: {analysis.Obstacles.Confidence:P1}");
        }
        else
        {
            Console.WriteLine("  No obstacles detected");
        }
        Console.WriteLine();

        Console.WriteLine("Game State:");
        Console.WriteLine($"  State: {analysis.GameState.State}");
        Console.WriteLine($"  Confidence: {analysis.GameState.Confidence:P1}");
        Console.WriteLine();

        // Save annotated frame
        using var overlay = analyzer.DrawOverlay(frame, analysis);
        string filename = "detection_result.png";
        Cv2.ImWrite(filename, overlay);
        Console.WriteLine($"Saved annotated frame to: {filename}");
    }

    static void RunDecisionTest()
    {
        Console.WriteLine("Running decision engine test...\n");

        var engine = new DecisionEngine();
        ApplyConfigToDecisionEngine(engine);

        var trajectory = engine.Trajectory;

        Console.WriteLine("=== Physics Parameters ===");
        Console.WriteLine($"Gravity: {trajectory.Gravity} px/s^2");
        Console.WriteLine($"Jump Velocity: {trajectory.JumpVelocity} px/s");
        Console.WriteLine($"Horizontal Speed: {trajectory.HorizontalSpeed} px/s");
        Console.WriteLine();

        // Test trajectory prediction
        Console.WriteLine("=== Trajectory Prediction Test ===");
        int startY = 300; // Middle of screen
        Console.WriteLine($"Starting Y position: {startY}");
        Console.WriteLine();

        Console.WriteLine("Predicted positions (no jump):");
        var noJumpTrajectory = trajectory.PredictTrajectory(startY, 1.0, 0.1);
        foreach (var point in noJumpTrajectory)
        {
            Console.WriteLine($"  t={point.TimeOffset:F1}s: Y={point.Y:F0}, Velocity={point.VelocityY:F0}");
        }
        Console.WriteLine();

        // Simulate a jump
        trajectory.SimulateJump();
        Console.WriteLine("Predicted positions (after jump):");
        var jumpTrajectory = trajectory.PredictTrajectory(startY, 1.0, 0.1);
        foreach (var point in jumpTrajectory)
        {
            Console.WriteLine($"  t={point.TimeOffset:F1}s: Y={point.Y:F0}, Velocity={point.VelocityY:F0}");
        }
        Console.WriteLine();

        // Test optimal jump finding
        Console.WriteLine("=== Optimal Jump Test ===");
        trajectory.Reset();
        int playerX = 100;
        int playerY = 300;
        int obstacleX = 400;
        int gapTop = 200;
        int gapBottom = 350;

        Console.WriteLine($"Player position: ({playerX}, {playerY})");
        Console.WriteLine($"Obstacle X: {obstacleX}");
        Console.WriteLine($"Gap: Y={gapTop} to Y={gapBottom} (center: {(gapTop + gapBottom) / 2})");
        Console.WriteLine();

        var solution = trajectory.FindOptimalJumpTime(playerX, playerY, obstacleX, gapTop, gapBottom);
        if (solution != null)
        {
            Console.WriteLine($"Solution found:");
            Console.WriteLine($"  Jump at: {solution.JumpTime:F3}s");
            Console.WriteLine($"  Time to obstacle: {solution.TimeToObstacle:F3}s");
            Console.WriteLine($"  Predicted Y at obstacle: {solution.PredictedYAtObstacle:F0}");
            Console.WriteLine($"  Confidence: {solution.Confidence:P1}");
            Console.WriteLine($"  Should jump now: {solution.ShouldJumpNow()}");
        }
        else
        {
            Console.WriteLine("No solution found!");
        }
        Console.WriteLine();

        // Test with simulated frame analysis
        Console.WriteLine("=== Decision Making Test ===");
        engine.Reset();

        // Create a mock frame analysis
        var mockAnalysis = new FrameAnalysis
        {
            Player = new PlayerDetectionResult
            {
                Detected = true,
                X = 80,
                Y = 280,
                Width = 40,
                Height = 40,
                Confidence = 0.9
            },
            Obstacles = new ObstacleDetectionResult
            {
                Detected = true,
                Obstacles = new List<Obstacle>
                {
                    new Obstacle
                    {
                        X = 400,
                        Width = 60,
                        GapTop = 200,
                        GapBottom = 350
                    }
                },
                Confidence = 0.8
            },
            GameState = new GameStateResult
            {
                State = GameState.GameState.Playing,
                Confidence = 0.9
            },
            FrameWidth = 800,
            FrameHeight = 600
        };

        Console.WriteLine("Mock game state:");
        Console.WriteLine($"  Player: ({mockAnalysis.Player.CenterX}, {mockAnalysis.Player.CenterY})");
        Console.WriteLine($"  Obstacle: X={mockAnalysis.Obstacles.Obstacles[0].X}, Gap={mockAnalysis.Obstacles.Obstacles[0].GapTop}-{mockAnalysis.Obstacles.Obstacles[0].GapBottom}");
        Console.WriteLine();

        // Process several frames
        for (int i = 0; i < 5; i++)
        {
            var decision = engine.ProcessFrame(mockAnalysis);
            var debug = engine.GetDebugInfo(mockAnalysis);

            Console.WriteLine($"Frame {i + 1}:");
            Console.WriteLine($"  Action: {decision.Action}");
            Console.WriteLine($"  Reason: {decision.Reason}");
            Console.WriteLine($"  Time to obstacle: {debug.TimeToObstacle:F2}s");
            Console.WriteLine($"  Predicted Y at obstacle: {debug.PredictedYAtObstacle:F0}");
            Console.WriteLine($"  Will hit obstacle: {debug.WillHitObstacle}");
            Console.WriteLine();

            // Simulate player falling
            mockAnalysis = mockAnalysis with
            {
                Player = mockAnalysis.Player with { Y = mockAnalysis.Player.Y + 10 }
            };

            Thread.Sleep(100);
        }

        Console.WriteLine($"Total jumps made: {engine.JumpCount}");
    }

    static void RunLiveDetection()
    {
        Console.WriteLine("Running live detection...");
        Console.WriteLine("Press 'Q' in the window or Ctrl+C to stop.\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);
        using var analyzer = new GameAnalyzer();

        ApplyConfigToAnalyzer(analyzer);

        const string windowName = "Jetty Boots - Live Detection";
        Cv2.NamedWindow(windowName, WindowFlags.Normal);
        Cv2.ResizeWindow(windowName, 800, 600);

        int frameCount = 0;
        var startTime = DateTime.Now;

        while (true)
        {
            using var frame = capture.CaptureFrame();

            if (frame == null)
            {
                Console.WriteLine("Frame capture failed");
                continue;
            }

            // Analyze the frame
            var analysis = analyzer.Analyze(frame);

            // Draw overlay
            using var overlay = analyzer.DrawOverlay(frame, analysis);

            // Add FPS counter
            frameCount++;
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            double fps = elapsed > 0 ? frameCount / elapsed : 0;

            Cv2.PutText(overlay, $"FPS: {fps:F1}", new Point(10, overlay.Height - 20),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);

            // Show frame
            Cv2.ImShow(windowName, overlay);

            // Check for key press (wait 1ms)
            int key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27) // Q or ESC
            {
                break;
            }

            // Print status every second
            if (frameCount % 30 == 0)
            {
                Console.WriteLine($"Frame {frameCount}: Player={analysis.Player.Detected}, " +
                    $"Obstacles={analysis.Obstacles.Obstacles.Count}, " +
                    $"State={analysis.GameState.State}, " +
                    $"FPS={fps:F1}");
            }
        }

        Cv2.DestroyWindow(windowName);
        var totalElapsed = (DateTime.Now - startTime).TotalSeconds;
        double finalFps = totalElapsed > 0 ? frameCount / totalElapsed : 0;
        Console.WriteLine($"\nProcessed {frameCount} frames in {totalElapsed:F1} seconds ({finalFps:F1} FPS)");
    }

    static void RunLiveDecision()
    {
        Console.WriteLine("Running live decision making...");
        Console.WriteLine("Press 'Q' in the window or Ctrl+C to stop.\n");
        Console.WriteLine("NOTE: This is observation mode - no inputs are sent to the game.\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);
        using var analyzer = new GameAnalyzer();
        var engine = new DecisionEngine();

        ApplyConfigToAnalyzer(analyzer);
        ApplyConfigToDecisionEngine(engine);

        const string windowName = "Jetty Boots - Decision Making";
        Cv2.NamedWindow(windowName, WindowFlags.Normal);
        Cv2.ResizeWindow(windowName, 800, 600);

        int frameCount = 0;
        var startTime = DateTime.Now;

        while (true)
        {
            using var frame = capture.CaptureFrame();

            if (frame == null)
            {
                Console.WriteLine("Frame capture failed");
                continue;
            }

            // Analyze the frame
            var analysis = analyzer.Analyze(frame);

            // Make decision
            var decision = engine.ProcessFrame(analysis);
            var debug = engine.GetDebugInfo(analysis);

            // Draw overlay
            using var overlay = analyzer.DrawOverlay(frame, analysis);

            // Add decision info
            frameCount++;
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            double fps = elapsed > 0 ? frameCount / elapsed : 0;

            // Draw decision indicator
            var actionColor = decision.Action == GameAction.Jump
                ? new Scalar(0, 255, 0)   // Green for jump
                : new Scalar(255, 255, 255); // White for no action

            Cv2.PutText(overlay, $"Action: {decision.Action}", new Point(10, 90),
                HersheyFonts.HersheySimplex, 0.6, actionColor, 2);

            if (decision.Reason.Length > 50)
            {
                Cv2.PutText(overlay, decision.Reason.Substring(0, 50) + "...", new Point(10, 115),
                    HersheyFonts.HersheySimplex, 0.4, new Scalar(200, 200, 200), 1);
            }
            else
            {
                Cv2.PutText(overlay, decision.Reason, new Point(10, 115),
                    HersheyFonts.HersheySimplex, 0.4, new Scalar(200, 200, 200), 1);
            }

            // Draw trajectory prediction if player detected
            if (analysis.Player.Detected)
            {
                DrawTrajectoryPreview(overlay, analysis.Player.CenterX, analysis.Player.CenterY, engine.Trajectory);
            }

            // Draw debug info
            Cv2.PutText(overlay, $"Jumps: {engine.JumpCount}", new Point(overlay.Width - 120, 30),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);

            if (debug.TimeToObstacle > 0)
            {
                Cv2.PutText(overlay, $"T2O: {debug.TimeToObstacle:F2}s", new Point(overlay.Width - 120, 50),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
            }

            Cv2.PutText(overlay, $"FPS: {fps:F1}", new Point(10, overlay.Height - 20),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);

            // Show frame
            Cv2.ImShow(windowName, overlay);

            // Check for key press (wait 1ms)
            int key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27)
            {
                break;
            }

            // Print decision every second
            if (frameCount % 30 == 0)
            {
                Console.WriteLine($"Frame {frameCount}: Action={decision.Action}, " +
                    $"Jumps={engine.JumpCount}, " +
                    $"FPS={fps:F1}");
            }
        }

        Cv2.DestroyWindow(windowName);
        var totalElapsed = (DateTime.Now - startTime).TotalSeconds;
        double finalFps = totalElapsed > 0 ? frameCount / totalElapsed : 0;
        Console.WriteLine($"\nProcessed {frameCount} frames in {totalElapsed:F1} seconds ({finalFps:F1} FPS)");
        Console.WriteLine($"Total jump decisions: {engine.JumpCount}");
    }

    static void DrawTrajectoryPreview(Mat frame, int playerX, int playerY, TrajectoryCalculator trajectory)
    {
        // Draw predicted trajectory as dots
        var points = trajectory.PredictTrajectory(playerY, 0.5, 0.05);

        for (int i = 1; i < points.Count; i++)
        {
            int x = playerX + (int)(i * trajectory.HorizontalSpeed * 0.05);
            int y = (int)points[i].Y;

            // Keep within frame bounds
            if (x >= 0 && x < frame.Width && y >= 0 && y < frame.Height)
            {
                // Color based on time (fade from cyan to blue)
                int alpha = 255 - (i * 20);
                alpha = Math.Max(alpha, 50);
                Cv2.Circle(frame, new Point(x, y), 3, new Scalar(alpha, alpha, 0), -1);
            }
        }
    }

    static void RunAutoPlayer(bool dryRun, bool autoStart = false, bool verbose = false)
    {
        Console.WriteLine("=== Jetty Boots Auto-Player ===\n");

        if (verbose)
        {
            Console.WriteLine("*** VERBOSE LOGGING ENABLED ***\n");
        }

        // Find game window first
        var gameHwnd = WindowHelper.FindDeepRockGalacticWindow();

        if (dryRun)
        {
            Console.WriteLine("*** DRY RUN MODE - No inputs will be sent ***\n");
        }
        else if (autoStart)
        {
            Console.WriteLine("*** AUTO-START MODE - Will focus window and start minigame ***\n");

            if (gameHwnd == IntPtr.Zero)
            {
                Console.WriteLine("ERROR: Deep Rock Galactic window not found!");
                Console.WriteLine("Make sure the game is running and visible.");
                return;
            }

            // Perform the startup sequence
            var inputSimulator = new GameInputSimulator();
            inputSimulator.UseMouseClick = true; // Use mouse for Jetty Boots

            Console.WriteLine("Starting in 3 seconds... Make sure you're standing at the arcade machine!");
            Console.WriteLine("Press Ctrl+C to cancel.\n");
            Thread.Sleep(3000);

            if (!inputSimulator.PerformStartupSequence(gameHwnd))
            {
                Console.WriteLine("ERROR: Startup sequence failed!");
                return;
            }

            Console.WriteLine("\nMinigame should now be running. Starting auto-player...\n");
        }
        else
        {
            Console.WriteLine("*** LIVE MODE - Inputs WILL be sent to game ***");
            Console.WriteLine("Make sure Deep Rock Galactic is focused!\n");
            Console.WriteLine("TIP: Use --auto-start to automatically focus and start the minigame.\n");
            Console.WriteLine("Press Enter to start, or Ctrl+C to cancel...");
            Console.ReadLine();
        }

        _logger?.LogInfo($"Starting auto-player (Dry Run: {dryRun}, Auto Start: {autoStart}, Verbose: {verbose})");

        var region = GetCaptureRegion();

        if (region.Width == 800 && region.Height == 600 && region.X == 0 && region.Y == 0)
        {
            Console.WriteLine("\nWARNING: Game window not found. Using test region.");
            Console.WriteLine("The auto-player may not work correctly.\n");
            _logger?.LogWarning("Game window not found, using default test region");
        }

        using var capture = new GdiScreenCapture(region);
        using var gameLoop = new GameLoop(capture);

        // Configure the game loop from config
        gameLoop.DryRun = dryRun;
        gameLoop.TargetFps = _configManager.Config.Capture.TargetFps;
        gameLoop.ShowDebugWindow = _configManager.Config.Debug.ShowDebugWindow;
        gameLoop.VerboseLogging = verbose;

        // Configure input simulator to use mouse click
        gameLoop.InputSimulator.UseMouseClick = true;

        // Apply configuration to components
        ApplyConfigToAnalyzer(gameLoop.Analyzer);
        ApplyConfigToDecisionEngine(gameLoop.DecisionEngine);

        // Create debug overlay
        using var debugOverlay = new DebugOverlay(_configManager.Config.Debug);

        // Subscribe to events
        gameLoop.OnLog += message =>
        {
            Console.WriteLine(message);
            _logger?.LogInfo(message);
        };

        gameLoop.OnStatusUpdate += status =>
        {
            // Log detection
            _logger?.LogDetection(status.Analysis);

            // Log decisions
            if (status.Decision.Action != GameAction.None)
            {
                _logger?.LogDecision(status.Decision, status.DebugInfo);
            }

            // Log performance periodically
            if (status.FrameNumber % 30 == 0)
            {
                var stats = status.Statistics;
                _logger?.LogPerformance(stats.AverageFps, status.Analysis.AnalysisTimeMs, 0);
                debugOverlay.UpdateFps(stats.AverageFps);

                Console.WriteLine($"[Stats] Frame: {stats.FrameCount}, Jumps: {stats.JumpsSent}, " +
                    $"Games: {stats.GamesPlayed}, FPS: {stats.AverageFps:F1}");
            }
        };

        // Setup cancellation
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nStopping...");
            cts.Cancel();
        };

        Console.WriteLine("Starting game loop...");
        Console.WriteLine("Press 'Q' in debug window or Ctrl+C to stop.\n");

        _logger?.LogGameStart();

        try
        {
            gameLoop.Run(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }

        _logger?.LogGameEnd(null, 0);

        var finalStats = gameLoop.Statistics;
        Console.WriteLine("\n=== Final Statistics ===");
        Console.WriteLine($"Total frames: {finalStats.FrameCount}");
        Console.WriteLine($"Total jumps: {finalStats.JumpsSent}");
        Console.WriteLine($"Games played: {finalStats.GamesPlayed}");
        Console.WriteLine($"Run time: {finalStats.RunTime:mm\\:ss}");
        Console.WriteLine($"Average FPS: {finalStats.AverageFps:F1}");

        // Print session report
        if (_logger != null)
        {
            Console.WriteLine("\n" + _logger.GenerateSessionReport());
        }
    }
}
