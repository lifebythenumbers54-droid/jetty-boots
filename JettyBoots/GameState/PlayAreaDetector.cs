using OpenCvSharp;

namespace JettyBoots.GameState;

/// <summary>
/// Detects the playable game area boundaries by finding the green border bars.
/// Run this once at game start to dynamically determine the play area.
/// </summary>
public class PlayAreaDetector
{
    // Default boundaries (fallback if detection fails)
    private const int DefaultMinX = 250;
    private const int DefaultMaxX = 680;
    private const int DefaultMinY = 50;
    private const int DefaultMaxY = 380;

    /// <summary>
    /// Detected play area boundaries.
    /// </summary>
    public PlayAreaBounds Bounds { get; private set; } = new()
    {
        MinX = DefaultMinX,
        MaxX = DefaultMaxX,
        MinY = DefaultMinY,
        MaxY = DefaultMaxY,
        Detected = false
    };

    /// <summary>
    /// Whether the play area has been successfully detected.
    /// </summary>
    public bool IsDetected => Bounds.Detected;

    /// <summary>
    /// Detects the play area from a game frame by finding the green boundary bars.
    /// </summary>
    /// <param name="frame">A frame from the game (ideally during gameplay)</param>
    /// <returns>True if boundaries were successfully detected</returns>
    public bool DetectPlayArea(Mat frame)
    {
        if (frame == null || frame.Empty())
            return false;

        try
        {
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            // Look for bright green bars (the game border)
            // Green in HSV: Hue 35-85, high saturation, high value
            using var greenMask = new Mat();
            Cv2.InRange(hsv, new Scalar(35, 100, 100), new Scalar(85, 255, 255), greenMask);

            int frameWidth = frame.Width;
            int frameHeight = frame.Height;

            // Detect horizontal bars (top/bottom)
            var (topBar, bottomBar) = DetectHorizontalBars(greenMask, frameWidth, frameHeight);

            // Detect vertical bars (left/right)
            var (leftEdge, rightEdge) = DetectVerticalBars(greenMask, frameWidth, frameHeight, topBar, bottomBar);

            // Validate detected boundaries
            bool valid = ValidateBoundaries(topBar, bottomBar, leftEdge, rightEdge, frameWidth, frameHeight);

            if (valid)
            {
                // Add margins to stay inside the game area
                const int margin = 10;
                Bounds = new PlayAreaBounds
                {
                    MinX = leftEdge + margin,
                    MaxX = rightEdge - margin,
                    MinY = topBar + margin,
                    MaxY = bottomBar - margin,
                    Detected = true,
                    RawTop = topBar,
                    RawBottom = bottomBar,
                    RawLeft = leftEdge,
                    RawRight = rightEdge
                };

                Console.WriteLine($"[PlayAreaDetector] Detected play area: X=[{Bounds.MinX}-{Bounds.MaxX}], Y=[{Bounds.MinY}-{Bounds.MaxY}]");
                return true;
            }
            else
            {
                Console.WriteLine($"[PlayAreaDetector] Detection failed, using defaults. Raw: top={topBar}, bottom={bottomBar}, left={leftEdge}, right={rightEdge}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayAreaDetector] Error: {ex.Message}");
            return false;
        }
    }

    private (int top, int bottom) DetectHorizontalBars(Mat greenMask, int frameWidth, int frameHeight)
    {
        int minBarWidth = frameWidth / 4; // Bar should span at least 1/4 of width
        int topBar = -1;
        int bottomBar = -1;

        // Scan from top to find top bar (in upper 40% of screen)
        for (int y = 0; y < frameHeight * 0.4; y++)
        {
            int greenPixels = CountGreenPixelsInRow(greenMask, y, frameWidth);

            if (greenPixels > minBarWidth)
            {
                // Found start of bar, find the bottom edge
                int barBottom = y;
                while (barBottom < frameHeight * 0.4)
                {
                    int pixels = CountGreenPixelsInRow(greenMask, barBottom, frameWidth);
                    if (pixels < minBarWidth / 2)
                        break;
                    barBottom++;
                }
                topBar = barBottom; // Use bottom edge of bar as play area start
                break;
            }
        }

        // Scan from bottom to find bottom bar (in lower 40% of screen)
        for (int y = frameHeight - 1; y > frameHeight * 0.6; y--)
        {
            int greenPixels = CountGreenPixelsInRow(greenMask, y, frameWidth);

            if (greenPixels > minBarWidth)
            {
                // Found start of bar, find the top edge
                int barTop = y;
                while (barTop > frameHeight * 0.6)
                {
                    int pixels = CountGreenPixelsInRow(greenMask, barTop, frameWidth);
                    if (pixels < minBarWidth / 2)
                        break;
                    barTop--;
                }
                bottomBar = barTop; // Use top edge of bar as play area end
                break;
            }
        }

        return (topBar, bottomBar);
    }

    private (int left, int right) DetectVerticalBars(Mat greenMask, int frameWidth, int frameHeight, int topBar, int bottomBar)
    {
        int searchTop = topBar > 0 ? topBar : (int)(frameHeight * 0.1);
        int searchBottom = bottomBar > 0 ? bottomBar : (int)(frameHeight * 0.8);
        int searchHeight = searchBottom - searchTop;
        int minBarHeight = searchHeight / 4;

        int leftEdge = -1;
        int rightEdge = -1;

        // Scan from left to find left edge
        for (int x = 0; x < frameWidth * 0.4; x++)
        {
            int greenPixels = CountGreenPixelsInColumn(greenMask, x, searchTop, searchBottom);

            if (greenPixels > minBarHeight)
            {
                // Found start of bar, find the right edge
                int barRight = x;
                while (barRight < frameWidth * 0.4)
                {
                    int pixels = CountGreenPixelsInColumn(greenMask, barRight, searchTop, searchBottom);
                    if (pixels < minBarHeight / 2)
                        break;
                    barRight++;
                }
                leftEdge = barRight; // Use right edge of bar as play area start
                break;
            }
        }

        // Scan from right to find right edge
        for (int x = frameWidth - 1; x > frameWidth * 0.6; x--)
        {
            int greenPixels = CountGreenPixelsInColumn(greenMask, x, searchTop, searchBottom);

            if (greenPixels > minBarHeight)
            {
                // Found start of bar, find the left edge
                int barLeft = x;
                while (barLeft > frameWidth * 0.6)
                {
                    int pixels = CountGreenPixelsInColumn(greenMask, barLeft, searchTop, searchBottom);
                    if (pixels < minBarHeight / 2)
                        break;
                    barLeft--;
                }
                rightEdge = barLeft; // Use left edge of bar as play area end
                break;
            }
        }

        return (leftEdge, rightEdge);
    }

    private int CountGreenPixelsInRow(Mat mask, int y, int width)
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        {
            if (mask.At<byte>(y, x) > 0)
                count++;
        }
        return count;
    }

    private int CountGreenPixelsInColumn(Mat mask, int x, int yStart, int yEnd)
    {
        int count = 0;
        for (int y = yStart; y < yEnd; y++)
        {
            if (mask.At<byte>(y, x) > 0)
                count++;
        }
        return count;
    }

    private bool ValidateBoundaries(int top, int bottom, int left, int right, int frameWidth, int frameHeight)
    {
        // All boundaries must be detected
        if (top <= 0 || bottom <= 0 || left <= 0 || right <= 0)
            return false;

        // Boundaries must make sense
        if (top >= bottom || left >= right)
            return false;

        // Play area should be a reasonable size (at least 200x200)
        int width = right - left;
        int height = bottom - top;
        if (width < 200 || height < 200)
            return false;

        // Play area shouldn't be the entire screen
        if (width > frameWidth * 0.9 || height > frameHeight * 0.9)
            return false;

        return true;
    }

    /// <summary>
    /// Resets detection to use default boundaries.
    /// </summary>
    public void Reset()
    {
        Bounds = new PlayAreaBounds
        {
            MinX = DefaultMinX,
            MaxX = DefaultMaxX,
            MinY = DefaultMinY,
            MaxY = DefaultMaxY,
            Detected = false
        };
    }

    /// <summary>
    /// Gets the calculated danger zone Y coordinate (bottom 30% of play area).
    /// </summary>
    public int GetDangerZoneY()
    {
        int playAreaHeight = Bounds.MaxY - Bounds.MinY;
        return Bounds.MinY + (int)(playAreaHeight * 0.70);
    }

    /// <summary>
    /// Gets the calculated caution zone Y coordinate (below 55% of play area).
    /// </summary>
    public int GetCautionZoneY()
    {
        int playAreaHeight = Bounds.MaxY - Bounds.MinY;
        return Bounds.MinY + (int)(playAreaHeight * 0.55);
    }

    /// <summary>
    /// Gets the calculated center Y coordinate.
    /// </summary>
    public int GetCenterY()
    {
        return (Bounds.MinY + Bounds.MaxY) / 2;
    }
}

/// <summary>
/// Represents the detected play area boundaries.
/// </summary>
public record PlayAreaBounds
{
    /// <summary>Minimum X coordinate (left boundary with margin)</summary>
    public int MinX { get; init; }

    /// <summary>Maximum X coordinate (right boundary with margin)</summary>
    public int MaxX { get; init; }

    /// <summary>Minimum Y coordinate (top boundary with margin)</summary>
    public int MinY { get; init; }

    /// <summary>Maximum Y coordinate (bottom boundary with margin)</summary>
    public int MaxY { get; init; }

    /// <summary>Whether the boundaries were detected or using defaults</summary>
    public bool Detected { get; init; }

    /// <summary>Raw detected top bar position (before margin)</summary>
    public int RawTop { get; init; }

    /// <summary>Raw detected bottom bar position (before margin)</summary>
    public int RawBottom { get; init; }

    /// <summary>Raw detected left bar position (before margin)</summary>
    public int RawLeft { get; init; }

    /// <summary>Raw detected right bar position (before margin)</summary>
    public int RawRight { get; init; }

    /// <summary>Width of the play area</summary>
    public int Width => MaxX - MinX;

    /// <summary>Height of the play area</summary>
    public int Height => MaxY - MinY;
}
