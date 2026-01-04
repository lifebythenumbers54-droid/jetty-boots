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
        // For Jetty Boots (space game), the background is ALWAYS dark
        // We cannot use darkness to detect game over state
        // Instead, we look for the YELLOW "GAME OVER" text

        if (_gameOverTemplate != null && !_gameOverTemplate.Empty())
        {
            return MatchTemplate(frame, _gameOverTemplate);
        }

        // Detect yellow text (GAME OVER screen has yellow text)
        // Yellow in HSV: Hue ~20-40, high saturation, high value
        try
        {
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            // Yellow color range for "GAME OVER" text
            using var yellowMask = new Mat();
            Cv2.InRange(hsv, new Scalar(15, 100, 150), new Scalar(45, 255, 255), yellowMask);

            // Count yellow pixels
            int yellowPixels = Cv2.CountNonZero(yellowMask);
            double yellowRatio = (double)yellowPixels / (frame.Width * frame.Height);

            // If significant yellow text is present (more than 2% of screen), likely game over
            // Increased threshold to avoid false positives from score display or other yellow elements
            // The GAME OVER text takes up a decent portion of the screen when shown
            if (yellowRatio > 0.02)
            {
                return true;
            }
        }
        catch
        {
            // Ignore errors in detection
        }

        return false;
    }

    private bool IsLikelyMenu(Mat frame, ImageStats stats)
    {
        // For Jetty Boots, we only detect menu if we have a template
        // The game background is dark space, so we can't use simple heuristics
        // like edge density or brightness - they'd give false positives

        if (_menuTemplate != null && !_menuTemplate.Empty())
        {
            return MatchTemplate(frame, _menuTemplate);
        }

        // Without a template, assume we're playing (not menu)
        // This prevents false positives from the dark space background
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
