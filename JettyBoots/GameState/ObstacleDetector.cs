using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace JettyBoots.GameState;

/// <summary>
/// Detects obstacles (pipes) and their gaps in game frames.
/// </summary>
public class ObstacleDetector
{
    // HSV color range for obstacle detection
    private Scalar _lowerBound;
    private Scalar _upperBound;

    // Detection parameters
    private int _minObstacleWidth = 30;
    private int _maxObstacleWidth = 150;
    private int _minGapHeight = 50;
    private int _playerX = 100; // Approximate X position of player for distance calculation

    public ObstacleDetector()
    {
        // Default to green color range (typical pipe color)
        SetColorRange(
            new Scalar(35, 50, 50),    // Lower HSV (green)
            new Scalar(85, 255, 255)   // Upper HSV
        );
    }

    /// <summary>
    /// Sets the HSV color range for obstacle detection.
    /// </summary>
    public void SetColorRange(Scalar lower, Scalar upper)
    {
        _lowerBound = lower;
        _upperBound = upper;
    }

    /// <summary>
    /// Sets the approximate X position of the player for distance calculations.
    /// </summary>
    public void SetPlayerX(int x)
    {
        _playerX = x;
    }

    /// <summary>
    /// Detects obstacles in the given frame.
    /// </summary>
    public ObstacleDetectionResult Detect(Mat frame)
    {
        if (frame == null || frame.Empty())
            return ObstacleDetectionResult.NotDetected;

        try
        {
            // Convert to HSV
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            // Create mask for obstacle color
            using var mask = new Mat();
            Cv2.InRange(hsv, _lowerBound, _upperBound, mask);

            // Clean up mask
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

            // Find obstacles using vertical projection
            var obstacles = FindObstaclesUsingProjection(mask, frame.Height);

            if (obstacles.Count > 0)
            {
                return new ObstacleDetectionResult
                {
                    Detected = true,
                    Obstacles = obstacles.OrderBy(o => o.X).ToList(),
                    Confidence = 0.8
                };
            }

            // Fallback: try contour-based detection
            obstacles = FindObstaclesUsingContours(mask, frame.Height);

            return new ObstacleDetectionResult
            {
                Detected = obstacles.Count > 0,
                Obstacles = obstacles.OrderBy(o => o.X).ToList(),
                Confidence = obstacles.Count > 0 ? 0.6 : 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Obstacle detection error: {ex.Message}");
            return ObstacleDetectionResult.NotDetected;
        }
    }

    /// <summary>
    /// Finds obstacles using vertical projection analysis.
    /// This works well for vertical pipe-like obstacles.
    /// </summary>
    private List<Obstacle> FindObstaclesUsingProjection(Mat mask, int frameHeight)
    {
        var obstacles = new List<Obstacle>();

        // Calculate vertical projection (sum of white pixels in each column)
        var projection = new int[mask.Width];
        for (int x = 0; x < mask.Width; x++)
        {
            int sum = 0;
            for (int y = 0; y < mask.Height; y++)
            {
                if (mask.At<byte>(y, x) > 0)
                    sum++;
            }
            projection[x] = sum;
        }

        // Find regions with high density (potential obstacles)
        bool inObstacle = false;
        int obstacleStart = 0;
        int threshold = frameHeight / 4; // At least 25% of height should be obstacle

        for (int x = 0; x < projection.Length; x++)
        {
            if (!inObstacle && projection[x] > threshold)
            {
                inObstacle = true;
                obstacleStart = x;
            }
            else if (inObstacle && (projection[x] <= threshold || x == projection.Length - 1))
            {
                inObstacle = false;
                int width = x - obstacleStart;

                if (width >= _minObstacleWidth && width <= _maxObstacleWidth)
                {
                    // Find the gap in this obstacle region
                    var gap = FindGapInRegion(mask, obstacleStart, x, frameHeight);
                    if (gap != null)
                    {
                        obstacles.Add(new Obstacle
                        {
                            X = obstacleStart,
                            Width = width,
                            GapTop = gap.Value.top,
                            GapBottom = gap.Value.bottom
                        });
                    }
                }
            }
        }

        return obstacles;
    }

    /// <summary>
    /// Finds the gap (opening) in a vertical obstacle region.
    /// </summary>
    private (int top, int bottom)? FindGapInRegion(Mat mask, int xStart, int xEnd, int frameHeight)
    {
        // Calculate horizontal projection for this region
        var projection = new int[frameHeight];
        for (int y = 0; y < frameHeight; y++)
        {
            int sum = 0;
            for (int x = xStart; x < xEnd; x++)
            {
                if (mask.At<byte>(y, x) > 0)
                    sum++;
            }
            projection[y] = sum;
        }

        // Find the longest run of low values (the gap)
        int regionWidth = xEnd - xStart;
        int gapThreshold = regionWidth / 3; // Gap has fewer pixels

        int bestGapStart = -1;
        int bestGapEnd = -1;
        int bestGapLength = 0;

        int currentGapStart = -1;

        for (int y = 0; y < frameHeight; y++)
        {
            if (projection[y] < gapThreshold)
            {
                if (currentGapStart < 0)
                    currentGapStart = y;
            }
            else
            {
                if (currentGapStart >= 0)
                {
                    int gapLength = y - currentGapStart;
                    if (gapLength > bestGapLength && gapLength >= _minGapHeight)
                    {
                        bestGapStart = currentGapStart;
                        bestGapEnd = y;
                        bestGapLength = gapLength;
                    }
                    currentGapStart = -1;
                }
            }
        }

        // Check if gap extends to bottom
        if (currentGapStart >= 0)
        {
            int gapLength = frameHeight - currentGapStart;
            if (gapLength > bestGapLength && gapLength >= _minGapHeight)
            {
                bestGapStart = currentGapStart;
                bestGapEnd = frameHeight;
            }
        }

        if (bestGapStart >= 0)
            return (bestGapStart, bestGapEnd);

        return null;
    }

    /// <summary>
    /// Finds obstacles using contour detection (fallback method).
    /// </summary>
    private List<Obstacle> FindObstaclesUsingContours(Mat mask, int frameHeight)
    {
        var obstacles = new List<Obstacle>();

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // Group contours by X position to find pipe pairs
        var contoursByX = new Dictionary<int, List<Rect>>();

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            var area = Cv2.ContourArea(contour);

            // Filter small contours
            if (area < 500) continue;

            // Group by X position (within tolerance)
            int xKey = (rect.X / 20) * 20; // Round to nearest 20 pixels
            if (!contoursByX.ContainsKey(xKey))
                contoursByX[xKey] = new List<Rect>();
            contoursByX[xKey].Add(rect);
        }

        // Find gaps between vertically separated contours at same X
        foreach (var kvp in contoursByX)
        {
            var rects = kvp.Value.OrderBy(r => r.Y).ToList();

            if (rects.Count >= 2)
            {
                // Find gap between top and bottom pipes
                var topPipe = rects.First();
                var bottomPipe = rects.Last();

                int gapTop = topPipe.Y + topPipe.Height;
                int gapBottom = bottomPipe.Y;
                int gapHeight = gapBottom - gapTop;

                if (gapHeight >= _minGapHeight)
                {
                    int x = Math.Min(topPipe.X, bottomPipe.X);
                    int width = Math.Max(topPipe.Width, bottomPipe.Width);

                    obstacles.Add(new Obstacle
                    {
                        X = x,
                        Width = width,
                        GapTop = gapTop,
                        GapBottom = gapBottom
                    });
                }
            }
        }

        return obstacles;
    }

    /// <summary>
    /// Detects obstacles using edge detection (alternative method).
    /// </summary>
    public ObstacleDetectionResult DetectUsingEdges(Mat frame)
    {
        if (frame == null || frame.Empty())
            return ObstacleDetectionResult.NotDetected;

        try
        {
            using var gray = new Mat();
            using var edges = new Mat();

            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Canny(gray, edges, 50, 150);

            // Find vertical lines (potential pipe edges)
            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 50, 100, 10);

            // Process detected lines to find obstacles
            // This is a simplified implementation
            var obstacles = new List<Obstacle>();

            // Group vertical lines and find gaps
            // ... (complex line processing would go here)

            return new ObstacleDetectionResult
            {
                Detected = obstacles.Count > 0,
                Obstacles = obstacles,
                Confidence = obstacles.Count > 0 ? 0.5 : 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Edge detection error: {ex.Message}");
            return ObstacleDetectionResult.NotDetected;
        }
    }
}
