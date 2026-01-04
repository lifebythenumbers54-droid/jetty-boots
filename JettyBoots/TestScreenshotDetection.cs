using OpenCvSharp;
using JettyBoots.GameState;
using Point = OpenCvSharp.Point;

namespace JettyBoots;

/// <summary>
/// Test tool to analyze screenshots and debug player detection.
/// Uses the same PlayerDetector as the actual game loop.
/// </summary>
public static class TestScreenshotDetection
{
    public static void RunTest(string screenshotPath, bool verbose = false)
    {
        Console.WriteLine($"=== Screenshot Detection Test ===\n");
        Console.WriteLine($"Loading: {screenshotPath}\n");

        if (!File.Exists(screenshotPath))
        {
            Console.WriteLine($"ERROR: File not found: {screenshotPath}");
            return;
        }

        using var frame = Cv2.ImRead(screenshotPath);
        if (frame.Empty())
        {
            Console.WriteLine("ERROR: Could not load image");
            return;
        }

        Console.WriteLine($"Image size: {frame.Width} x {frame.Height}\n");

        // Use the actual PlayerDetector - same code as the game loop
        var detector = new PlayerDetector();
        var result = detector.Detect(frame);

        Console.WriteLine("=== PlayerDetector Result ===");
        if (result.Detected)
        {
            Console.WriteLine($"PLAYER DETECTED: YES");
            Console.WriteLine($"  Position: ({result.CenterX}, {result.CenterY})");
            Console.WriteLine($"  Bounding Box: ({result.X}, {result.Y}) {result.Width}x{result.Height}");
            Console.WriteLine($"  Confidence: {result.Confidence:F2}");
        }
        else
        {
            Console.WriteLine($"PLAYER DETECTED: NO");
            if (verbose)
            {
                Console.WriteLine("  (Frame may be a transition/dark frame where player is not visible)");
            }
        }

        // Save annotated result
        string outputPath = Path.Combine(Path.GetDirectoryName(screenshotPath) ?? ".",
            Path.GetFileNameWithoutExtension(screenshotPath) + "_analysis.png");

        using var annotated = frame.Clone();

        // Draw player detection result
        if (result.Detected)
        {
            // Draw bounding box
            Cv2.Rectangle(annotated,
                new Rect(result.X, result.Y, result.Width, result.Height),
                new Scalar(0, 255, 0), 2);

            // Draw center point
            Cv2.Circle(annotated, new Point(result.CenterX, result.CenterY), 5, new Scalar(0, 255, 255), -1);

            // Draw label
            Cv2.PutText(annotated, $"Player ({result.CenterX}, {result.CenterY}) conf={result.Confidence:F2}",
                new Point(result.X, result.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
        }
        else
        {
            Cv2.PutText(annotated, "PLAYER NOT DETECTED",
                new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 255), 2);
        }

        // Draw Y=405 boundary line (max detection area)
        Cv2.Line(annotated, new Point(0, 405), new Point(frame.Width, 405), new Scalar(255, 0, 255), 1);
        Cv2.PutText(annotated, "Y=405 limit", new Point(10, 400), HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 0, 255), 1);

        Cv2.ImWrite(outputPath, annotated);
        Console.WriteLine($"\nSaved analysis to: {outputPath}");
    }

    public static void RunTestOnFolder(string folderPath)
    {
        Console.WriteLine($"=== Testing all screenshots in: {folderPath} ===\n");

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"ERROR: Folder not found: {folderPath}");
            return;
        }

        var files = Directory.GetFiles(folderPath, "*_raw.png")
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0)
        {
            // Try any PNG
            files = Directory.GetFiles(folderPath, "*.png")
                .Where(f => !f.Contains("_analysis") && !f.Contains("_annotated"))
                .OrderBy(f => f)
                .ToList();
        }

        Console.WriteLine($"Found {files.Count} screenshots\n");

        // Track results for summary
        int detected = 0;
        int notDetected = 0;
        var results = new List<(string file, bool found, int x, int y)>();

        // Print first frame size for debugging
        bool printedSize = false;

        // Detect play area dynamically from first frame
        var playAreaDetector = new PlayAreaDetector();
        PlayAreaBounds? bounds = null;

        foreach (var file in files)
        {
            using var frame = Cv2.ImRead(file);
            if (frame.Empty())
            {
                Console.WriteLine($"ERROR: Could not load {file}");
                continue;
            }

            if (!printedSize)
            {
                Console.WriteLine($"Frame dimensions: {frame.Width} x {frame.Height}\n");

                // Try to detect play area from first frame
                if (playAreaDetector.DetectPlayArea(frame))
                {
                    bounds = playAreaDetector.Bounds;
                    Console.WriteLine($"Play area detected: X=[{bounds.MinX}-{bounds.MaxX}], Y=[{bounds.MinY}-{bounds.MaxY}]\n");
                }
                else
                {
                    Console.WriteLine("Play area detection failed, using defaults\n");
                    bounds = playAreaDetector.Bounds; // Will be defaults
                }

                printedSize = true;
            }

            // Create detector with configured bounds
            var detector = new PlayerDetector();
            if (bounds != null)
            {
                detector.SetPlayAreaBounds(bounds);
            }
            var result = detector.Detect(frame);

            string fileName = Path.GetFileName(file);
            if (result.Detected)
            {
                detected++;
                results.Add((fileName, true, result.CenterX, result.CenterY));
                Console.WriteLine($"{fileName}: FOUND at ({result.CenterX}, {result.CenterY}) conf={result.Confidence:F2}");
            }
            else
            {
                notDetected++;
                results.Add((fileName, false, 0, 0));
                Console.WriteLine($"{fileName}: NOT FOUND");
            }

            // Save annotated image with zones
            string outputPath = Path.Combine(Path.GetDirectoryName(file) ?? ".",
                Path.GetFileNameWithoutExtension(file) + "_analysis.png");

            using var annotated = frame.Clone();

            // Use detected play area boundaries (or defaults)
            int minX = bounds?.MinX ?? 250;
            int maxX = bounds?.MaxX ?? 680;
            int ceilingY = bounds?.MinY ?? 50;
            int floorY = bounds?.MaxY ?? 380;
            int playAreaHeight = floorY - ceilingY;
            int dangerZone = ceilingY + (int)(playAreaHeight * 0.70);
            int cautionZone = ceilingY + (int)(playAreaHeight * 0.55);
            int centerY = (floorY + ceilingY) / 2;

            // Draw playable zone rectangle (blue)
            Cv2.Rectangle(annotated, new Rect(minX, ceilingY, maxX - minX, floorY - ceilingY),
                new Scalar(255, 100, 0), 2);  // Blue for play area

            // Draw danger zone (red fill, semi-transparent effect via lines)
            for (int y = dangerZone; y < floorY; y += 3)
            {
                Cv2.Line(annotated, new Point(minX, y), new Point(maxX, y), new Scalar(0, 0, 200), 1);
            }
            Cv2.Line(annotated, new Point(minX, dangerZone), new Point(maxX, dangerZone),
                new Scalar(0, 0, 255), 2);  // Red line at danger zone threshold

            // Draw caution zone line (orange)
            Cv2.Line(annotated, new Point(minX, cautionZone), new Point(maxX, cautionZone),
                new Scalar(0, 165, 255), 2);  // Orange line at caution zone

            // Draw center line (green dashed effect)
            for (int x = minX; x < maxX; x += 10)
            {
                Cv2.Line(annotated, new Point(x, centerY), new Point(Math.Min(x + 5, maxX), centerY),
                    new Scalar(0, 255, 0), 2);  // Green dashed line at center
            }

            // Draw ceiling line (cyan)
            Cv2.Line(annotated, new Point(minX, ceilingY), new Point(maxX, ceilingY),
                new Scalar(255, 255, 0), 2);  // Cyan for ceiling

            // Draw floor line (magenta)
            Cv2.Line(annotated, new Point(minX, floorY), new Point(maxX, floorY),
                new Scalar(255, 0, 255), 2);  // Magenta for floor

            // Add labels
            Cv2.PutText(annotated, "CEILING (50)", new Point(maxX + 5, ceilingY + 5),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);
            Cv2.PutText(annotated, $"CENTER ({centerY})", new Point(maxX + 5, centerY + 5),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 255, 0), 1);
            Cv2.PutText(annotated, $"CAUTION ({cautionZone})", new Point(maxX + 5, cautionZone + 5),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 165, 255), 1);
            Cv2.PutText(annotated, $"DANGER ({dangerZone})", new Point(maxX + 5, dangerZone + 5),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 0, 255), 1);
            Cv2.PutText(annotated, $"FLOOR ({floorY})", new Point(maxX + 5, floorY + 5),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 0, 255), 1);

            // Draw player detection
            if (result.Detected)
            {
                // Player bounding box (bright green)
                Cv2.Rectangle(annotated,
                    new Rect(result.X, result.Y, result.Width, result.Height),
                    new Scalar(0, 255, 0), 2);
                // Player center point (yellow filled circle)
                Cv2.Circle(annotated, new Point(result.CenterX, result.CenterY), 8, new Scalar(0, 255, 255), -1);
                Cv2.Circle(annotated, new Point(result.CenterX, result.CenterY), 8, new Scalar(0, 0, 0), 2);

                // Determine zone and add label
                string zone = result.CenterY >= dangerZone ? "JUMP NOW!" :
                              result.CenterY >= cautionZone ? "Caution" :
                              result.CenterY <= ceilingY + 30 ? "Near ceiling" : "Safe";
                var zoneColor = result.CenterY >= dangerZone ? new Scalar(0, 0, 255) :
                                result.CenterY >= cautionZone ? new Scalar(0, 165, 255) :
                                new Scalar(0, 255, 0);
                Cv2.PutText(annotated, $"PLAYER: ({result.CenterX}, {result.CenterY}) - {zone}",
                    new Point(10, 25), HersheyFonts.HersheySimplex, 0.6, zoneColor, 2);
            }
            else
            {
                Cv2.PutText(annotated, "PLAYER: NOT DETECTED", new Point(10, 25),
                    HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);
            }

            Cv2.ImWrite(outputPath, annotated);
        }

        // Print summary
        Console.WriteLine($"\n{'=',-60}");
        Console.WriteLine($"=== SUMMARY ===");
        Console.WriteLine($"Total screenshots: {files.Count}");
        Console.WriteLine($"Player detected: {detected}");
        Console.WriteLine($"Player not detected: {notDetected}");
        Console.WriteLine($"Detection rate: {(files.Count > 0 ? (100.0 * detected / files.Count) : 0):F1}%");

        if (notDetected > 0)
        {
            Console.WriteLine($"\nFiles where player was NOT detected:");
            foreach (var (file, found, _, _) in results.Where(r => !r.found))
            {
                Console.WriteLine($"  - {file}");
            }
        }
    }
}
