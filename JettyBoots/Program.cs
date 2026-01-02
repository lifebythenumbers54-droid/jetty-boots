using JettyBoots.ScreenCapture;
using OpenCvSharp;

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

    static void RunCaptureTest()
    {
        Console.WriteLine("Running screen capture test...\n");

        // Try to find game window first
        var gameHwnd = WindowHelper.FindDeepRockGalacticWindow();
        CaptureRegion region;

        if (gameHwnd != IntPtr.Zero)
        {
            var clientRegion = WindowHelper.GetClientRegion(gameHwnd);
            if (clientRegion != null)
            {
                region = clientRegion;
                Console.WriteLine($"Capturing Deep Rock Galactic window: {region.Width}x{region.Height}");
            }
            else
            {
                region = new CaptureRegion(0, 0, 800, 600);
                Console.WriteLine("Using default capture region (800x600)");
            }
        }
        else
        {
            // Capture a portion of the screen for testing
            region = new CaptureRegion(0, 0, 800, 600);
            Console.WriteLine("Game not found. Capturing screen region (800x600) for testing...");
        }

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

            // Small delay to simulate game loop
            Thread.Sleep(16); // ~60 FPS target
        }

        var finalMetrics = capture.Metrics;
        Console.WriteLine($"\n=== Results ===");
        Console.WriteLine($"Total frames captured: {finalMetrics.FrameCount}");
        Console.WriteLine($"Average capture time: {finalMetrics.CaptureTimeMs:F2}ms");
        Console.WriteLine($"Estimated max FPS: {1000.0 / finalMetrics.CaptureTimeMs:F1}");
        Console.WriteLine($"Achieved FPS: {finalMetrics.FramesPerSecond:F1}");
    }
}
