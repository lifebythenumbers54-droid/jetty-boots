using JettyBoots.GameState;
using JettyBoots.ScreenCapture;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace JettyBoots;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Jetty Boots Auto-Player ===");
        Console.WriteLine("Automated player for the Jetty Boots minigame in Deep Rock Galactic\n");

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

        if (args.Contains("--live-detection"))
        {
            RunLiveDetection();
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

        ShowHelp();
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage: JettyBoots [options]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  --test-capture    Test screen capture functionality");
        Console.WriteLine("  --test-detection  Test detection on a single frame");
        Console.WriteLine("  --live-detection  Run live detection with visual overlay");
        Console.WriteLine("  --list-windows    List all visible windows");
        Console.WriteLine("  --find-game       Find Deep Rock Galactic window");
        Console.WriteLine("  --help            Show this help message\n");
        Console.WriteLine("Press any key to run a quick capture test...");
        Console.ReadKey(true);
        RunCaptureTest();
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

    static void RunLiveDetection()
    {
        Console.WriteLine("Running live detection...");
        Console.WriteLine("Press 'Q' in the window or Ctrl+C to stop.\n");

        var region = GetCaptureRegion();
        using var capture = new GdiScreenCapture(region);
        using var analyzer = new GameAnalyzer();

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
}
