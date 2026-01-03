using System.Runtime.InteropServices;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;

namespace JettyBoots.Input;

/// <summary>
/// Simulates keyboard and mouse input for game control.
/// </summary>
public class GameInputSimulator
{
    private readonly InputSimulator _simulator;
    private DateTime _lastInputTime = DateTime.MinValue;
    private int _inputCount = 0;

    // Configuration
    private VirtualKeyCode _jumpKey = VirtualKeyCode.SPACE;
    private VirtualKeyCode _interactKey = VirtualKeyCode.VK_E;
    private bool _useMouseClick = true;  // Default to mouse click for Jetty Boots
    private int _minInputIntervalMs = 50;  // Minimum time between inputs

    // Windows API imports
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    public GameInputSimulator()
    {
        _simulator = new InputSimulator();
    }

    /// <summary>
    /// Gets or sets the key used for jumping.
    /// </summary>
    public VirtualKeyCode JumpKey
    {
        get => _jumpKey;
        set => _jumpKey = value;
    }

    /// <summary>
    /// Gets or sets whether to use mouse click instead of keyboard.
    /// </summary>
    public bool UseMouseClick
    {
        get => _useMouseClick;
        set => _useMouseClick = value;
    }

    /// <summary>
    /// Gets or sets the key used for interaction (default E).
    /// </summary>
    public VirtualKeyCode InteractKey
    {
        get => _interactKey;
        set => _interactKey = value;
    }

    /// <summary>
    /// Gets the total number of inputs sent.
    /// </summary>
    public int InputCount => _inputCount;

    /// <summary>
    /// Focuses the specified window and brings it to the foreground.
    /// </summary>
    public bool FocusWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        try
        {
            // Get the thread IDs
            uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint targetThreadId = GetWindowThreadProcessId(hwnd, out _);
            uint currentThreadId = GetCurrentThreadId();

            // Attach thread input to allow SetForegroundWindow
            if (foregroundThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            // Restore if minimized
            ShowWindow(hwnd, SW_RESTORE);

            // Bring to top and set as foreground
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);

            // Detach thread input
            if (foregroundThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (targetThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }

            // Give the window time to come to foreground
            Thread.Sleep(100);

            Console.WriteLine($"Focused window: 0x{hwnd:X8}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to focus window: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Holds the interact key (E) for the specified duration to start the minigame.
    /// </summary>
    public bool HoldInteractKey(int durationMs = 1500)
    {
        try
        {
            Console.WriteLine($"Holding {_interactKey} for {durationMs}ms to start minigame...");

            _simulator.Keyboard.KeyDown(_interactKey);
            Thread.Sleep(durationMs);
            _simulator.Keyboard.KeyUp(_interactKey);

            Console.WriteLine("Interact key released");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hold interact key failed: {ex.Message}");
            // Make sure key is released
            try { _simulator.Keyboard.KeyUp(_interactKey); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Performs the startup sequence: focus window, hold E to start minigame, wait for game to load.
    /// </summary>
    public bool PerformStartupSequence(IntPtr gameWindow, int interactHoldMs = 1500, int waitAfterMs = 2000)
    {
        Console.WriteLine("=== Starting Jetty Boots Startup Sequence ===");

        // Step 1: Focus the game window
        Console.WriteLine("Step 1: Focusing game window...");
        if (!FocusWindow(gameWindow))
        {
            Console.WriteLine("ERROR: Failed to focus game window!");
            return false;
        }
        Thread.Sleep(500); // Wait for focus to settle

        // Step 2: Click in the center of the window to ensure it has input focus
        Console.WriteLine("Step 2: Clicking to ensure input focus...");
        _simulator.Mouse.LeftButtonClick();
        Thread.Sleep(300);

        // Step 3: Hold E to interact with the arcade machine
        Console.WriteLine("Step 3: Holding E to start Jetty Boots minigame...");
        if (!HoldInteractKey(interactHoldMs))
        {
            Console.WriteLine("ERROR: Failed to hold interact key!");
            return false;
        }

        // Step 4: Wait for the minigame to load
        Console.WriteLine($"Step 4: Waiting {waitAfterMs}ms for minigame to load...");
        Thread.Sleep(waitAfterMs);

        // Step 5: Click to start the game (if needed)
        Console.WriteLine("Step 5: Clicking to start game...");
        _simulator.Mouse.LeftButtonClick();
        Thread.Sleep(500);

        Console.WriteLine("=== Startup Sequence Complete ===");
        return true;
    }

    /// <summary>
    /// Sends a jump input (key press or mouse click).
    /// </summary>
    public bool SendJump()
    {
        // Enforce minimum interval between inputs
        var timeSinceLastInput = (DateTime.Now - _lastInputTime).TotalMilliseconds;
        if (timeSinceLastInput < _minInputIntervalMs)
        {
            return false;
        }

        try
        {
            if (_useMouseClick)
            {
                _simulator.Mouse.LeftButtonClick();
            }
            else
            {
                _simulator.Keyboard.KeyPress(_jumpKey);
            }

            _lastInputTime = DateTime.Now;
            _inputCount++;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Input simulation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends a key press to start/restart the game.
    /// </summary>
    public bool SendStartGame()
    {
        try
        {
            // Usually space or enter starts the game
            _simulator.Keyboard.KeyPress(VirtualKeyCode.SPACE);
            _lastInputTime = DateTime.Now;
            _inputCount++;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Start game input failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Holds a key down (for continuous input if needed).
    /// </summary>
    public void KeyDown(VirtualKeyCode key)
    {
        try
        {
            _simulator.Keyboard.KeyDown(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Key down failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases a held key.
    /// </summary>
    public void KeyUp(VirtualKeyCode key)
    {
        try
        {
            _simulator.Keyboard.KeyUp(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Key up failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the mouse to a specific screen position.
    /// </summary>
    public void MoveMouse(int x, int y)
    {
        try
        {
            // Convert to absolute coordinates (0-65535 range)
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            double absoluteX = (x * 65535.0) / screenWidth;
            double absoluteY = (y * 65535.0) / screenHeight;

            _simulator.Mouse.MoveMouseTo(absoluteX, absoluteY);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mouse move failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the input counter.
    /// </summary>
    public void Reset()
    {
        _inputCount = 0;
        _lastInputTime = DateTime.MinValue;
    }

    // Windows API for screen dimensions
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
