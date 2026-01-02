using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace JettyBoots.GameState;

/// <summary>
/// Detects the player character in game frames using color-based detection.
/// </summary>
public class PlayerDetector
{
    // Default HSV color range for player detection (can be calibrated)
    private Scalar _lowerBound;
    private Scalar _upperBound;

    // Detection parameters
    private int _minArea = 100;
    private int _maxArea = 10000;
    private double _minConfidence = 0.5;

    // Last known position for tracking
    private PlayerDetectionResult? _lastDetection;
    private readonly int _searchRadius = 100;

    public PlayerDetector()
    {
        // Default colors - bright/saturated colors typical for game characters
        // These should be calibrated for the actual game
        SetColorRange(
            new Scalar(0, 100, 100),   // Lower HSV
            new Scalar(30, 255, 255)   // Upper HSV (orange/yellow range)
        );
    }

    /// <summary>
    /// Sets the HSV color range for player detection.
    /// </summary>
    public void SetColorRange(Scalar lower, Scalar upper)
    {
        _lowerBound = lower;
        _upperBound = upper;
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
    /// Detects the player in the given frame.
    /// </summary>
    public PlayerDetectionResult Detect(Mat frame)
    {
        if (frame == null || frame.Empty())
            return PlayerDetectionResult.NotDetected;

        try
        {
            // Convert to HSV color space
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            // Create mask for player color
            using var mask = new Mat();
            Cv2.InRange(hsv, _lowerBound, _upperBound, mask);

            // Apply morphological operations to clean up the mask
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

            // Find contours
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

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

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);

            // Filter by area
            if (area < _minArea || area > _maxArea)
                continue;

            var rect = Cv2.BoundingRect(contour);

            // Calculate aspect ratio (player is usually roughly square or slightly tall)
            double aspectRatio = (double)rect.Width / rect.Height;
            if (aspectRatio < 0.3 || aspectRatio > 3.0)
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

        // Prefer detections near the left side (where player typically is in Flappy Bird)
        double xRatio = (double)rect.X / frameWidth;
        if (xRatio < 0.4)
            score += 0.2;

        // Prefer larger areas (up to a point)
        double areaScore = Math.Min(area / 2000.0, 0.2);
        score += areaScore;

        // If we have a previous detection, prefer nearby detections
        if (_lastDetection != null)
        {
            int dx = Math.Abs(rect.X + rect.Width / 2 - _lastDetection.CenterX);
            int dy = Math.Abs(rect.Y + rect.Height / 2 - _lastDetection.CenterY);
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < _searchRadius)
            {
                score += 0.3 * (1.0 - distance / _searchRadius);
            }
        }

        return Math.Min(score, 1.0);
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
