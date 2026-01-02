using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace JettyBoots.GameState;

/// <summary>
/// Detects the current game state (menu, playing, game over).
/// </summary>
public class GameStateDetector
{
    // Templates for state detection
    private Mat? _gameOverTemplate;
    private Mat? _menuTemplate;
    private Mat? _playingIndicatorTemplate;

    // Color ranges for state detection
    private Scalar _gameOverColorLower = new(0, 0, 0);
    private Scalar _gameOverColorUpper = new(180, 50, 50);

    // Detection thresholds
    private double _templateMatchThreshold = 0.7;
    private double _colorMatchThreshold = 0.3;

    // State tracking
    private GameState _lastState = GameState.Unknown;
    private int _stateConfirmationCount = 0;
    private const int RequiredConfirmations = 3;

    /// <summary>
    /// Loads a template image for game over detection.
    /// </summary>
    public void SetGameOverTemplate(string imagePath)
    {
        _gameOverTemplate?.Dispose();
        _gameOverTemplate = Cv2.ImRead(imagePath);
    }

    /// <summary>
    /// Loads a template image for menu detection.
    /// </summary>
    public void SetMenuTemplate(string imagePath)
    {
        _menuTemplate?.Dispose();
        _menuTemplate = Cv2.ImRead(imagePath);
    }

    /// <summary>
    /// Sets color range for detecting dark/game over screens.
    /// </summary>
    public void SetGameOverColorRange(Scalar lower, Scalar upper)
    {
        _gameOverColorLower = lower;
        _gameOverColorUpper = upper;
    }

    /// <summary>
    /// Detects the current game state.
    /// </summary>
    public GameStateResult Detect(Mat frame)
    {
        if (frame == null || frame.Empty())
            return GameStateResult.Unknown;

        try
        {
            // Try template matching first if templates are available
            if (_gameOverTemplate != null && !_gameOverTemplate.Empty())
            {
                if (MatchTemplate(frame, _gameOverTemplate))
                {
                    return ConfirmState(GameState.GameOver, 0.9);
                }
            }

            if (_menuTemplate != null && !_menuTemplate.Empty())
            {
                if (MatchTemplate(frame, _menuTemplate))
                {
                    return ConfirmState(GameState.Menu, 0.9);
                }
            }

            // Use heuristics-based detection
            var heuristicState = DetectUsingHeuristics(frame);
            return ConfirmState(heuristicState.State, heuristicState.Confidence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Game state detection error: {ex.Message}");
            return GameStateResult.Unknown;
        }
    }

    private bool MatchTemplate(Mat frame, Mat template)
    {
        try
        {
            // Resize template if needed
            using var resizedTemplate = new Mat();
            if (template.Width > frame.Width || template.Height > frame.Height)
            {
                double scale = Math.Min(
                    (double)frame.Width / template.Width,
                    (double)frame.Height / template.Height
                ) * 0.8;
                Cv2.Resize(template, resizedTemplate, new Size(0, 0), scale, scale);
            }
            else
            {
                template.CopyTo(resizedTemplate);
            }

            using var result = new Mat();
            Cv2.MatchTemplate(frame, resizedTemplate, result, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
            return maxVal >= _templateMatchThreshold;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects game state using visual heuristics.
    /// </summary>
    private GameStateResult DetectUsingHeuristics(Mat frame)
    {
        // Calculate overall image statistics
        var stats = CalculateImageStats(frame);

        // Game Over detection: often has dark overlay or specific UI elements
        if (IsLikelyGameOver(frame, stats))
        {
            return new GameStateResult
            {
                State = GameState.GameOver,
                Confidence = 0.7
            };
        }

        // Menu detection: usually has less motion, centered elements
        if (IsLikelyMenu(frame, stats))
        {
            return new GameStateResult
            {
                State = GameState.Menu,
                Confidence = 0.6
            };
        }

        // Default to playing if none of the above
        return new GameStateResult
        {
            State = GameState.Playing,
            Confidence = 0.5
        };
    }

    private ImageStats CalculateImageStats(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        Cv2.MeanStdDev(gray, out var mean, out var stdDev);

        // Calculate edge density
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);
        double edgeDensity = Cv2.CountNonZero(edges) / (double)(edges.Width * edges.Height);

        // Calculate color variance
        var channels = Cv2.Split(frame);
        double colorVariance = 0;
        foreach (var channel in channels)
        {
            Cv2.MeanStdDev(channel, out _, out var channelStdDev);
            colorVariance += channelStdDev.Val0;
            channel.Dispose();
        }
        colorVariance /= 3;

        return new ImageStats
        {
            MeanBrightness = mean.Val0,
            StdDeviation = stdDev.Val0,
            EdgeDensity = edgeDensity,
            ColorVariance = colorVariance
        };
    }

    private bool IsLikelyGameOver(Mat frame, ImageStats stats)
    {
        // Game over screens often have:
        // - Dark overlay (lower mean brightness)
        // - Text/UI elements (moderate edge density)
        // - Less color variance (often grayed out)

        // Check for dark regions in center of screen
        int centerX = frame.Width / 4;
        int centerY = frame.Height / 4;
        int roiWidth = frame.Width / 2;
        int roiHeight = frame.Height / 2;

        using var centerRoi = new Mat(frame, new Rect(centerX, centerY, roiWidth, roiHeight));
        using var grayRoi = new Mat();
        Cv2.CvtColor(centerRoi, grayRoi, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(grayRoi, out var centerMean, out _);

        // Dark center with some UI elements suggests game over
        if (centerMean.Val0 < 80 && stats.EdgeDensity > 0.02 && stats.EdgeDensity < 0.15)
        {
            return true;
        }

        // Check for dominant dark colors (overlay)
        using var hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);
        using var darkMask = new Mat();
        Cv2.InRange(hsv, _gameOverColorLower, _gameOverColorUpper, darkMask);

        double darkRatio = Cv2.CountNonZero(darkMask) / (double)(darkMask.Width * darkMask.Height);
        if (darkRatio > _colorMatchThreshold)
        {
            return true;
        }

        return false;
    }

    private bool IsLikelyMenu(Mat frame, ImageStats stats)
    {
        // Menu screens often have:
        // - Centered content
        // - Static elements (low motion between frames)
        // - Specific UI patterns

        // For now, use simple heuristics
        // This would be improved with actual game analysis

        // Very high or very low edge density might indicate menu
        if (stats.EdgeDensity < 0.01)
        {
            return true; // Very static/simple screen
        }

        return false;
    }

    private GameStateResult ConfirmState(GameState state, double confidence)
    {
        // Require multiple confirmations before changing state
        if (state == _lastState)
        {
            _stateConfirmationCount++;
        }
        else
        {
            _stateConfirmationCount = 1;
        }

        _lastState = state;

        // Only report state if confirmed multiple times
        if (_stateConfirmationCount >= RequiredConfirmations)
        {
            return new GameStateResult
            {
                State = state,
                Confidence = confidence
            };
        }

        // Return previous stable state if not confirmed
        return new GameStateResult
        {
            State = _lastState,
            Confidence = confidence * 0.5
        };
    }

    /// <summary>
    /// Resets state tracking.
    /// </summary>
    public void Reset()
    {
        _lastState = GameState.Unknown;
        _stateConfirmationCount = 0;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _gameOverTemplate?.Dispose();
        _menuTemplate?.Dispose();
    }

    private record ImageStats
    {
        public double MeanBrightness { get; init; }
        public double StdDeviation { get; init; }
        public double EdgeDensity { get; init; }
        public double ColorVariance { get; init; }
    }
}
