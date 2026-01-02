using OpenCvSharp;

namespace JettyBoots.ScreenCapture;

/// <summary>
/// Interface for screen capture implementations.
/// </summary>
public interface IScreenCapture : IDisposable
{
    /// <summary>
    /// Captures a single frame from the screen or window.
    /// </summary>
    /// <returns>The captured frame as an OpenCV Mat, or null if capture failed.</returns>
    Mat? CaptureFrame();

    /// <summary>
    /// Gets the current capture region.
    /// </summary>
    CaptureRegion Region { get; }

    /// <summary>
    /// Sets the capture region.
    /// </summary>
    void SetRegion(CaptureRegion region);

    /// <summary>
    /// Gets performance metrics for the last capture.
    /// </summary>
    CaptureMetrics Metrics { get; }
}

/// <summary>
/// Defines a region of the screen to capture.
/// </summary>
public record CaptureRegion(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Creates a capture region for the entire primary screen.
    /// </summary>
    public static CaptureRegion FullScreen => new(0, 0,
        System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920,
        System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080);

    /// <summary>
    /// Creates a capture region from a window handle.
    /// </summary>
    public static CaptureRegion FromWindowHandle(IntPtr hwnd)
    {
        if (WindowHelper.GetWindowRect(hwnd, out var rect))
        {
            return new CaptureRegion(rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        return FullScreen;
    }
}

/// <summary>
/// Performance metrics for screen capture.
/// </summary>
public record CaptureMetrics
{
    public double CaptureTimeMs { get; init; }
    public double FramesPerSecond { get; init; }
    public int FrameCount { get; init; }
    public DateTime LastCaptureTime { get; init; }
}
