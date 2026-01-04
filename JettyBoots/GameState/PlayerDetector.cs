using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace JettyBoots.GameState;

/// <summary>
/// Detects the player character in game frames using color-based detection.
/// </summary>
public class PlayerDetector
{
    // Multiple HSV color ranges for player detection (to handle different game states)
    private readonly List<(Scalar Lower, Scalar Upper, string Name)> _colorRanges = new();

    // Detection parameters - tuned for small WHITE player sprite
    private int _minArea = 10;    // Minimum area - very small to catch fragmented sprites
    private int _maxArea = 3000;  // Maximum area - increased to allow for varying sizes
    private double _minConfidence = 0.15;  // Confidence threshold (very low for permissive detection)

    // Last known position for tracking
    private PlayerDetectionResult? _lastDetection;
    private readonly int _searchRadius = 150;  // Search radius for tracking

    // Dynamic play area boundaries (set from PlayAreaDetector or use defaults)
    private int _playAreaMinX = 250;   // X coordinate of left boundary
    private int _playAreaMaxX = 680;   // X coordinate of right boundary
    private int _playAreaMinY = 50;    // Y coordinate of top boundary
    private int _playAreaMaxY = 380;   // Y coordinate of bottom boundary
    private bool _boundsConfigured = false;

    public PlayerDetector()
    {
        // Initialize with multiple color ranges for Jetty Boots minigame
        InitializeJettyBootsColors();
    }

    private void InitializeJettyBootsColors()
    {
        _colorRanges.Clear();

        // Jetty Boots player is shades between green/white/bright
        // The player appears as light green, white, or bright greenish-white tones
        // On game over screens, the player can appear pinkish/salmon colored

        // LIGHT GREEN - player can be a lighter shade of green
        _colorRanges.Add((
            new Scalar(35, 20, 120),          // Green hue, low-medium sat, bright
            new Scalar(90, 180, 255),         // Light/bright green tones
            "LightGreen"
        ));

        // WHITE/BRIGHT - for when player appears very light/white
        _colorRanges.Add((
            new Scalar(0, 0, 180),            // Any hue, very low sat, very bright
            new Scalar(180, 60, 255),         // White/near-white pixels
            "White"
        ));

        // BRIGHT/PALE GREEN-WHITE - transitional colors between green and white
        _colorRanges.Add((
            new Scalar(35, 10, 150),          // Green-ish hue, very low sat, bright
            new Scalar(90, 80, 255),          // Pale/washed out green
            "PaleGreen"
        ));

        // CYAN-GREEN bright tones
        _colorRanges.Add((
            new Scalar(70, 30, 140),          // Cyan-green hue, some sat, bright
            new Scalar(100, 200, 255),        // Bright cyan-green
            "CyanGreen"
        ));

        // PINK/SALMON - player on game over screens appears pinkish
        _colorRanges.Add((
            new Scalar(0, 30, 100),           // Red/pink hue, some saturation, medium bright
            new Scalar(20, 180, 255),         // Pink/salmon tones
            "Pink"
        ));
        _colorRanges.Add((
            new Scalar(160, 30, 100),         // Pink wraps around high hue values
            new Scalar(180, 180, 255),        // Pink/magenta tones
            "PinkHigh"
        ));
    }

    /// <summary>
    /// Sets the HSV color range for player detection (replaces all existing ranges).
    /// </summary>
    public void SetColorRange(Scalar lower, Scalar upper)
    {
        _colorRanges.Clear();
        _colorRanges.Add((lower, upper, "Custom"));
    }

    /// <summary>
    /// Adds an additional color range for player detection.
    /// </summary>
    public void AddColorRange(Scalar lower, Scalar upper, string name = "Custom")
    {
        _colorRanges.Add((lower, upper, name));
    }

    /// <summary>
    /// Clears all color ranges.
    /// </summary>
    public void ClearColorRanges()
    {
        _colorRanges.Clear();
    }

    /// <summary>
    /// Sets the area constraints for valid player detections.
    /// </summary>
    public void SetAreaConstraints(int minArea, int maxArea)
    {
        _minArea = minArea;
        _maxArea = maxArea;
    }

    /// <summary>
    /// Sets the play area boundaries for player detection.
    /// </summary>
    public void SetPlayAreaBounds(int minX, int maxX, int minY, int maxY)
    {
        _playAreaMinX = minX;
        _playAreaMaxX = maxX;
        _playAreaMinY = minY;
        _playAreaMaxY = maxY;
        _boundsConfigured = true;
        Console.WriteLine($"[PlayerDetector] Bounds set: X=[{minX}-{maxX}], Y=[{minY}-{maxY}]");
    }

    /// <summary>
    /// Sets the play area boundaries from a PlayAreaBounds object.
    /// </summary>
    public void SetPlayAreaBounds(PlayAreaBounds bounds)
    {
        SetPlayAreaBounds(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY);
    }

    /// <summary>
    /// Detects the player in the given frame using multiple color ranges.
    /// </summary>
    public PlayerDetectionResult Detect(Mat frame)
    {
        if (frame == null || frame.Empty())
            return PlayerDetectionResult.NotDetected;

        if (_colorRanges.Count == 0)
            return PlayerDetectionResult.NotDetected;

        try
        {
            // Convert to HSV color space
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            // Note: We no longer skip detection on "GAME OVER" screens
            // The player may still be visible and we want to track their position

            // Create combined mask from all color ranges (green/white/bright shades)
            using var combinedMask = new Mat(frame.Rows, frame.Cols, MatType.CV_8UC1, new Scalar(0));

            foreach (var (lower, upper, _) in _colorRanges)
            {
                using var mask = new Mat();
                Cv2.InRange(hsv, lower, upper, mask);
                Cv2.BitwiseOr(combinedMask, mask, combinedMask);
            }

            // Apply morphological operations to clean up
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2, 2));
            Cv2.MorphologyEx(combinedMask, combinedMask, MorphTypes.Open, kernel);   // Remove small noise
            Cv2.MorphologyEx(combinedMask, combinedMask, MorphTypes.Close, kernel);  // Connect nearby pixels

            // Find contours
            Cv2.FindContours(combinedMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
                return PlayerDetectionResult.NotDetected;

            // Find the best candidate
            var bestCandidate = FindBestCandidate(contours, frame.Width, frame.Height);

            if (bestCandidate != null)
            {
                _lastDetection = bestCandidate;
            }

            return bestCandidate ?? PlayerDetectionResult.NotDetected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Player detection error: {ex.Message}");
            return PlayerDetectionResult.NotDetected;
        }
    }

    private PlayerDetectionResult? FindBestCandidate(Point[][] contours, int frameWidth, int frameHeight)
    {
        PlayerDetectionResult? best = null;
        double bestScore = 0;

        // Use configured play area boundaries
        int minX = _playAreaMinX;
        int maxX = _playAreaMaxX;
        int minY = _playAreaMinY;
        int maxY = _playAreaMaxY;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);

            // Filter by area - player is small
            if (area < _minArea || area > _maxArea)
                continue;

            var rect = Cv2.BoundingRect(contour);
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;

            // Filter by both X and Y - must be inside the game area
            if (centerX < minX || centerX > maxX || centerY < minY || centerY > maxY)
                continue;

            // Calculate aspect ratio - be very permissive to allow fragmented detections
            double aspectRatio = (double)rect.Width / rect.Height;
            if (aspectRatio < 0.15 || aspectRatio > 6.0)
                continue;

            // Calculate score based on various factors
            double score = CalculateScore(rect, area, frameWidth, frameHeight);

            if (score > bestScore && score >= _minConfidence)
            {
                bestScore = score;
                best = new PlayerDetectionResult
                {
                    Detected = true,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    Confidence = score
                };
            }
        }

        return best;
    }

    private double CalculateScore(Rect rect, double area, int frameWidth, int frameHeight)
    {
        double score = 0.5; // Base score

        // Prefer mid-height positions (within the play area between the green bars)
        double yRatio = (double)(rect.Y + rect.Height / 2) / frameHeight;
        if (yRatio > 0.10 && yRatio < 0.75)
        {
            score += 0.15;
        }

        // Prefer areas typical of the player sprite (small-medium size)
        // Player appears to be roughly 15-25 pixels wide/tall = ~200-600 area
        if (area >= 30 && area <= 1000)
        {
            // Best score for typical player size (~150-400 area)
            double idealArea = 250;
            double areaDiff = Math.Abs(area - idealArea) / idealArea;
            double areaScore = 0.2 * Math.Max(0, 1.0 - areaDiff * 0.5);
            score += areaScore;
        }

        // Player can be anywhere in the game area horizontally
        // No strong left/right preference - player moves across the screen

        // If we have a previous detection, strongly prefer nearby detections (tracking)
        if (_lastDetection != null)
        {
            int dx = Math.Abs(rect.X + rect.Width / 2 - _lastDetection.CenterX);
            int dy = Math.Abs(rect.Y + rect.Height / 2 - _lastDetection.CenterY);
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < _searchRadius)
            {
                // Strong tracking bonus for nearby detections
                score += 0.4 * (1.0 - distance / _searchRadius);
            }
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>
    /// Detects player using template matching (alternative method).
    /// </summary>
    public PlayerDetectionResult DetectWithTemplate(Mat frame, Mat template)
    {
        if (frame == null || frame.Empty() || template == null || template.Empty())
            return PlayerDetectionResult.NotDetected;

        try
        {
            using var result = new Mat();
            Cv2.MatchTemplate(frame, template, result, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal >= _minConfidence)
            {
                return new PlayerDetectionResult
                {
                    Detected = true,
                    X = maxLoc.X,
                    Y = maxLoc.Y,
                    Width = template.Width,
                    Height = template.Height,
                    Confidence = maxVal
                };
            }

            return PlayerDetectionResult.NotDetected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Template matching error: {ex.Message}");
            return PlayerDetectionResult.NotDetected;
        }
    }

    /// <summary>
    /// Resets tracking state.
    /// </summary>
    public void Reset()
    {
        _lastDetection = null;
    }
}
