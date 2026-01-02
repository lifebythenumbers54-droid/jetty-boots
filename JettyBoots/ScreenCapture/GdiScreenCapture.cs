using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace JettyBoots.ScreenCapture;

/// <summary>
/// Screen capture implementation using GDI+ (Graphics Device Interface).
/// Simple and compatible, but slower than DirectX-based capture.
/// </summary>
public class GdiScreenCapture : IScreenCapture
{
    private CaptureRegion _region;
    private readonly Stopwatch _stopwatch = new();
    private readonly Queue<double> _frameTimes = new();
    private const int FpsWindowSize = 30;
    private int _frameCount;
    private DateTime _lastCaptureTime;
    private double _lastCaptureTimeMs;
    private bool _disposed;

    public GdiScreenCapture() : this(CaptureRegion.FullScreen)
    {
    }

    public GdiScreenCapture(CaptureRegion region)
    {
        _region = region;
    }

    public CaptureRegion Region => _region;

    public CaptureMetrics Metrics => new()
    {
        CaptureTimeMs = _lastCaptureTimeMs,
        FramesPerSecond = CalculateFps(),
        FrameCount = _frameCount,
        LastCaptureTime = _lastCaptureTime
    };

    public void SetRegion(CaptureRegion region)
    {
        _region = region;
    }

    public Mat? CaptureFrame()
    {
        if (_disposed) return null;

        _stopwatch.Restart();

        try
        {
            // Create bitmap to hold the captured image
            using var bitmap = new Bitmap(_region.Width, _region.Height, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(bitmap);

            // Copy screen content to bitmap
            graphics.CopyFromScreen(
                _region.X, _region.Y,
                0, 0,
                new System.Drawing.Size(_region.Width, _region.Height),
                CopyPixelOperation.SourceCopy
            );

            // Convert to OpenCV Mat
            var mat = BitmapConverter.ToMat(bitmap);

            _stopwatch.Stop();
            UpdateMetrics(_stopwatch.Elapsed.TotalMilliseconds);

            return mat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screen capture failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Captures a frame from a specific window handle.
    /// </summary>
    public Mat? CaptureWindow(IntPtr hwnd)
    {
        var region = WindowHelper.GetClientRegion(hwnd);
        if (region == null) return null;

        SetRegion(region);
        return CaptureFrame();
    }

    private void UpdateMetrics(double captureTimeMs)
    {
        _lastCaptureTimeMs = captureTimeMs;
        _lastCaptureTime = DateTime.Now;
        _frameCount++;

        // Maintain sliding window for FPS calculation
        _frameTimes.Enqueue(captureTimeMs);
        if (_frameTimes.Count > FpsWindowSize)
        {
            _frameTimes.Dequeue();
        }
    }

    private double CalculateFps()
    {
        if (_frameTimes.Count == 0) return 0;

        double avgFrameTime = _frameTimes.Average();
        return avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
