using System.Runtime.InteropServices;
using System.Text;

namespace JettyBoots.ScreenCapture;

/// <summary>
/// Helper class for Windows API window operations.
/// </summary>
public static class WindowHelper
{
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hwnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Gets the title of a window.
    /// </summary>
    public static string GetWindowTitle(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Finds a window by partial title match.
    /// </summary>
    public static IntPtr FindWindowByTitle(string partialTitle)
    {
        IntPtr foundHwnd = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            string title = GetWindowTitle(hwnd);
            if (title.Contains(partialTitle, StringComparison.OrdinalIgnoreCase))
            {
                foundHwnd = hwnd;
                return false; // Stop enumeration
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundHwnd;
    }

    /// <summary>
    /// Finds the Deep Rock Galactic game window.
    /// </summary>
    public static IntPtr FindDeepRockGalacticWindow()
    {
        // Try common window titles for the game
        string[] possibleTitles = new[]
        {
            "Deep Rock Galactic",
            "DeepRockGalactic",
            "FSD" // Internal name
        };

        foreach (var title in possibleTitles)
        {
            var hwnd = FindWindowByTitle(title);
            if (hwnd != IntPtr.Zero)
                return hwnd;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Gets all visible windows with their titles.
    /// </summary>
    public static List<(IntPtr Handle, string Title)> GetAllVisibleWindows()
    {
        var windows = new List<(IntPtr, string)>();

        EnumWindows((hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                string title = GetWindowTitle(hwnd);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    windows.Add((hwnd, title));
                }
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Gets the client area rectangle in screen coordinates.
    /// </summary>
    public static CaptureRegion? GetClientRegion(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out var clientRect))
            return null;

        var point = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref point))
            return null;

        return new CaptureRegion(
            point.X,
            point.Y,
            clientRect.Right - clientRect.Left,
            clientRect.Bottom - clientRect.Top
        );
    }
}
