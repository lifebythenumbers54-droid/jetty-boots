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
    private bool _useMouseClick = false;
    private int _minInputIntervalMs = 50;  // Minimum time between inputs

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
    /// Gets the total number of inputs sent.
    /// </summary>
    public int InputCount => _inputCount;

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
