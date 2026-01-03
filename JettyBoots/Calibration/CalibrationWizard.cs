using JettyBoots.Configuration;
using JettyBoots.ScreenCapture;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace JettyBoots.Calibration;

/// <summary>
/// Interactive calibration wizard for setting up detection parameters.
/// </summary>
public class CalibrationWizard : IDisposable
{
    private readonly ConfigurationManager _configManager;
    private readonly IScreenCapture _capture;
    private bool _disposed;

    // Calibration state
    private CalibrationStep _currentStep = CalibrationStep.SelectWindow;
    private Mat? _currentFrame;
    private Rect? _selectedRegion;
    private List<Point> _sampledPoints = new();

    // Mouse state
    private Point _mousePosition;
    private bool _mouseDown;
    private Point _selectionStart;

    // Window constants
    private const string WindowName = "Jetty Boots - Calibration";

    // Calibration results
    private CaptureRegion? _calibratedRegion;
    private ColorRangeConfig? _playerColorRange;
    private ColorRangeConfig? _obstacleColorRange;

    public CalibrationWizard(ConfigurationManager configManager, IScreenCapture capture)
    {
        _configManager = configManager;
        _capture = capture;
    }

    /// <summary>
    /// Runs the complete calibration wizard.
    /// </summary>
    public bool Run()
    {
        Console.WriteLine("\n=== Jetty Boots Calibration Wizard ===\n");
        Console.WriteLine("This wizard will help you configure detection settings for your game.\n");

        try
        {
            // Step 1: Select game window/region
            if (CalibrateRegion() == null)
            {
                Console.WriteLine("Region calibration cancelled.");
                return false;
            }

            // Step 2: Capture player color
            if (!CalibratePlayerColor())
            {
                Console.WriteLine("Player color calibration cancelled.");
                return false;
            }

            // Step 3: Capture obstacle color
            if (!CalibrateObstacleColor())
            {
                Console.WriteLine("Obstacle color calibration cancelled.");
                return false;
            }

            // Save calibration to configuration
            ApplyCalibrationToConfig();

            Console.WriteLine("\n=== Calibration Complete! ===\n");
            Console.WriteLine("Configuration has been updated with calibrated values.");

            // Ask to save
            Console.Write("\nSave configuration to file? (y/n): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response == "y" || response == "yes")
            {
                if (_configManager.SaveDefault())
                {
                    Console.WriteLine($"Configuration saved to: {ConfigurationManager.GetDefaultConfigPath()}");
                }
                else
                {
                    Console.WriteLine("Failed to save configuration.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Calibration error: {ex.Message}");
            return false;
        }
        finally
        {
            Cv2.DestroyAllWindows();
        }
    }

    /// <summary>
    /// Runs only the region selection step.
    /// </summary>
    public CaptureRegion? CalibrateRegion()
    {
        _currentStep = CalibrationStep.SelectWindow;

        Console.WriteLine("Step 1: Select Game Window/Region");
        Console.WriteLine("----------------------------------");
        Console.WriteLine("Options:");
        Console.WriteLine("  1. Auto-detect Deep Rock Galactic window");
        Console.WriteLine("  2. Use current capture region");
        Console.WriteLine("  3. Manually select region");
        Console.WriteLine("  4. Enter coordinates manually");
        Console.Write("\nChoice (1-4): ");

        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                return AutoDetectGameWindow();

            case "2":
                _calibratedRegion = _capture.Region;
                Console.WriteLine($"Using current region: {_capture.Region}");
                return _calibratedRegion;

            case "3":
                return ManualRegionSelection();

            case "4":
                return ManualCoordinateEntry();

            default:
                Console.WriteLine("Invalid choice.");
                return null;
        }
    }

    private CaptureRegion? AutoDetectGameWindow()
    {
        Console.WriteLine("\nSearching for Deep Rock Galactic window...");

        var hwnd = WindowHelper.FindDeepRockGalacticWindow();
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine("Game window not found. Make sure Deep Rock Galactic is running.");
            return null;
        }

        var region = WindowHelper.GetClientRegion(hwnd);
        if (region == null)
        {
            Console.WriteLine("Failed to get window region.");
            return null;
        }

        Console.WriteLine($"Found game window: {region.Width}x{region.Height} at ({region.X}, {region.Y})");
        _calibratedRegion = region;
        return region;
    }

    private CaptureRegion? ManualRegionSelection()
    {
        Console.WriteLine("\nManual Region Selection:");
        Console.WriteLine("- A window will show the screen capture");
        Console.WriteLine("- Click and drag to select the game area");
        Console.WriteLine("- Press ENTER to confirm, ESC to cancel");

        Cv2.NamedWindow(WindowName, WindowFlags.Normal);
        Cv2.SetMouseCallback(WindowName, OnMouseCallback);

        _selectedRegion = null;
        _selectionStart = new Point(0, 0);

        while (true)
        {
            using var frame = CaptureFullScreen();
            if (frame == null)
            {
                Thread.Sleep(100);
                continue;
            }

            var display = frame.Clone();

            // Draw selection rectangle
            if (_selectedRegion.HasValue)
            {
                Cv2.Rectangle(display, _selectedRegion.Value, new Scalar(0, 255, 0), 2);
            }
            else if (_mouseDown)
            {
                var rect = GetSelectionRect(_selectionStart, _mousePosition);
                Cv2.Rectangle(display, rect, new Scalar(0, 255, 255), 2);
            }

            // Draw instructions
            Cv2.PutText(display, "Click and drag to select game region. ENTER=Confirm, ESC=Cancel",
                new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);

            Cv2.ImShow(WindowName, display);

            int key = Cv2.WaitKey(30);
            if (key == 27) // ESC
            {
                Cv2.DestroyWindow(WindowName);
                return null;
            }
            else if (key == 13 && _selectedRegion.HasValue) // ENTER
            {
                var region = new CaptureRegion(
                    _selectedRegion.Value.X,
                    _selectedRegion.Value.Y,
                    _selectedRegion.Value.Width,
                    _selectedRegion.Value.Height);

                _calibratedRegion = region;
                Cv2.DestroyWindow(WindowName);
                Console.WriteLine($"Selected region: {region.Width}x{region.Height} at ({region.X}, {region.Y})");
                return region;
            }

            display.Dispose();
        }
    }

    private CaptureRegion? ManualCoordinateEntry()
    {
        Console.WriteLine("\nEnter capture region coordinates:");

        Console.Write("X: ");
        if (!int.TryParse(Console.ReadLine(), out int x)) return null;

        Console.Write("Y: ");
        if (!int.TryParse(Console.ReadLine(), out int y)) return null;

        Console.Write("Width: ");
        if (!int.TryParse(Console.ReadLine(), out int width) || width <= 0) return null;

        Console.Write("Height: ");
        if (!int.TryParse(Console.ReadLine(), out int height) || height <= 0) return null;

        var region = new CaptureRegion(x, y, width, height);
        _calibratedRegion = region;
        Console.WriteLine($"Region set to: {width}x{height} at ({x}, {y})");
        return region;
    }

    /// <summary>
    /// Calibrates the player character color.
    /// </summary>
    public bool CalibratePlayerColor()
    {
        _currentStep = CalibrationStep.CapturePlayer;

        Console.WriteLine("\nStep 2: Calibrate Player Color");
        Console.WriteLine("-------------------------------");
        Console.WriteLine("Instructions:");
        Console.WriteLine("  1. Position your character in the game");
        Console.WriteLine("  2. Click on several parts of your character");
        Console.WriteLine("  3. The wizard will calculate the color range");
        Console.WriteLine("  4. Press ENTER when done, ESC to cancel");

        _playerColorRange = CaptureColorSamples("Player");
        return _playerColorRange != null;
    }

    /// <summary>
    /// Calibrates the obstacle color.
    /// </summary>
    public bool CalibrateObstacleColor()
    {
        _currentStep = CalibrationStep.CaptureObstacle;

        Console.WriteLine("\nStep 3: Calibrate Obstacle Color");
        Console.WriteLine("---------------------------------");
        Console.WriteLine("Instructions:");
        Console.WriteLine("  1. Wait for obstacles to appear in the game");
        Console.WriteLine("  2. Click on several parts of the obstacles");
        Console.WriteLine("  3. The wizard will calculate the color range");
        Console.WriteLine("  4. Press ENTER when done, ESC to cancel");

        _obstacleColorRange = CaptureColorSamples("Obstacle");
        return _obstacleColorRange != null;
    }

    private ColorRangeConfig? CaptureColorSamples(string objectName)
    {
        Cv2.NamedWindow(WindowName, WindowFlags.Normal);
        Cv2.SetMouseCallback(WindowName, OnColorSampleCallback);

        _sampledPoints.Clear();
        var sampledColors = new List<Vec3b>();

        while (true)
        {
            _currentFrame = _capture.CaptureFrame();
            if (_currentFrame == null)
            {
                Thread.Sleep(100);
                continue;
            }

            var display = _currentFrame.Clone();

            // Draw sampled points
            foreach (var point in _sampledPoints)
            {
                Cv2.Circle(display, point, 5, new Scalar(0, 255, 0), -1);
                Cv2.Circle(display, point, 7, new Scalar(255, 255, 255), 1);
            }

            // Draw instructions
            Cv2.PutText(display, $"Click on {objectName} to sample colors. Samples: {_sampledPoints.Count}",
                new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);
            Cv2.PutText(display, "ENTER=Confirm, ESC=Cancel, R=Reset samples",
                new Point(10, 60), HersheyFonts.HersheySimplex, 0.5, new Scalar(200, 200, 200), 1);

            // Show color info if we have samples
            if (_sampledPoints.Count > 0)
            {
                var range = CalculateColorRange(sampledColors);
                Cv2.PutText(display, $"H: {range.HueLower}-{range.HueUpper} S: {range.SaturationLower}-{range.SaturationUpper} V: {range.ValueLower}-{range.ValueUpper}",
                    new Point(10, display.Height - 20), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);
            }

            Cv2.ImShow(WindowName, display);

            int key = Cv2.WaitKey(30);
            if (key == 27) // ESC
            {
                Cv2.DestroyWindow(WindowName);
                return null;
            }
            else if (key == 13) // ENTER
            {
                if (_sampledPoints.Count < 3)
                {
                    Console.WriteLine("Please sample at least 3 points.");
                    continue;
                }

                Cv2.DestroyWindow(WindowName);

                // Collect colors from sampled points
                foreach (var point in _sampledPoints)
                {
                    if (point.X >= 0 && point.X < _currentFrame.Width &&
                        point.Y >= 0 && point.Y < _currentFrame.Height)
                    {
                        sampledColors.Add(_currentFrame.At<Vec3b>(point.Y, point.X));
                    }
                }

                var colorRange = CalculateColorRange(sampledColors);
                Console.WriteLine($"{objectName} color range: H({colorRange.HueLower}-{colorRange.HueUpper}), S({colorRange.SaturationLower}-{colorRange.SaturationUpper}), V({colorRange.ValueLower}-{colorRange.ValueUpper})");
                return colorRange;
            }
            else if (key == 'r' || key == 'R')
            {
                _sampledPoints.Clear();
                sampledColors.Clear();
                Console.WriteLine("Samples reset.");
            }

            display.Dispose();
        }
    }

    private void OnMouseCallback(MouseEventTypes @event, int x, int y, MouseEventFlags flags, IntPtr userdata)
    {
        _mousePosition = new Point(x, y);

        switch (@event)
        {
            case MouseEventTypes.LButtonDown:
                _mouseDown = true;
                _selectionStart = new Point(x, y);
                _selectedRegion = null;
                break;

            case MouseEventTypes.LButtonUp:
                _mouseDown = false;
                _selectedRegion = GetSelectionRect(_selectionStart, _mousePosition);
                break;

            case MouseEventTypes.MouseMove:
                // Position is updated automatically
                break;
        }
    }

    private void OnColorSampleCallback(MouseEventTypes @event, int x, int y, MouseEventFlags flags, IntPtr userdata)
    {
        if (@event == MouseEventTypes.LButtonDown)
        {
            _sampledPoints.Add(new Point(x, y));
            Console.WriteLine($"Sampled point at ({x}, {y})");
        }
    }

    private static Rect GetSelectionRect(Point start, Point end)
    {
        int x = Math.Min(start.X, end.X);
        int y = Math.Min(start.Y, end.Y);
        int width = Math.Abs(end.X - start.X);
        int height = Math.Abs(end.Y - start.Y);
        return new Rect(x, y, width, height);
    }

    private static ColorRangeConfig CalculateColorRange(List<Vec3b> bgrColors)
    {
        if (bgrColors.Count == 0)
        {
            return new ColorRangeConfig
            {
                HueLower = 0, HueUpper = 180,
                SaturationLower = 0, SaturationUpper = 255,
                ValueLower = 0, ValueUpper = 255
            };
        }

        // Convert BGR to HSV
        var hsvColors = new List<Vec3b>();
        foreach (var bgr in bgrColors)
        {
            using var bgrMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(bgr.Item0, bgr.Item1, bgr.Item2));
            using var hsvMat = new Mat();
            Cv2.CvtColor(bgrMat, hsvMat, ColorConversionCodes.BGR2HSV);
            hsvColors.Add(hsvMat.At<Vec3b>(0, 0));
        }

        // Calculate min/max with margin
        int margin = 15;

        int minH = hsvColors.Min(c => c.Item0);
        int maxH = hsvColors.Max(c => c.Item0);
        int minS = hsvColors.Min(c => c.Item1);
        int maxS = hsvColors.Max(c => c.Item1);
        int minV = hsvColors.Min(c => c.Item2);
        int maxV = hsvColors.Max(c => c.Item2);

        return new ColorRangeConfig
        {
            HueLower = Math.Max(0, minH - margin),
            HueUpper = Math.Min(180, maxH + margin),
            SaturationLower = Math.Max(0, minS - margin * 2),
            SaturationUpper = Math.Min(255, maxS + margin * 2),
            ValueLower = Math.Max(0, minV - margin * 2),
            ValueUpper = Math.Min(255, maxV + margin * 2)
        };
    }

    private Mat? CaptureFullScreen()
    {
        var originalRegion = _capture.Region;
        _capture.SetRegion(CaptureRegion.FullScreen);
        var frame = _capture.CaptureFrame();
        _capture.SetRegion(originalRegion);
        return frame;
    }

    private void ApplyCalibrationToConfig()
    {
        var config = _configManager.Config;

        if (_calibratedRegion != null)
        {
            config.Capture.UseCustomRegion = true;
            config.Capture.RegionX = _calibratedRegion.X;
            config.Capture.RegionY = _calibratedRegion.Y;
            config.Capture.RegionWidth = _calibratedRegion.Width;
            config.Capture.RegionHeight = _calibratedRegion.Height;
        }

        if (_playerColorRange != null)
        {
            config.Detection.PlayerColor = _playerColorRange;
        }

        if (_obstacleColorRange != null)
        {
            config.Detection.ObstacleColor = _obstacleColorRange;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _currentFrame?.Dispose();
        Cv2.DestroyAllWindows();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Calibration step enumeration.
/// </summary>
public enum CalibrationStep
{
    SelectWindow,
    CapturePlayer,
    CaptureObstacle,
    Complete
}
