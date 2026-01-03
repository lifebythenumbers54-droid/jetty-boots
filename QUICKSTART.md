# Jetty Boots Auto-Player - Quick Start Guide

## Prerequisites

- Windows 10/11
- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Deep Rock Galactic (Steam)

## Building the Project

```bash
cd C:\ClaudeCode\Projects\FlappyBird\JettyBoots
dotnet build
```

## Step-by-Step Setup

### 1. Launch Deep Rock Galactic

Start the game and navigate to the Space Rig. Find an arcade machine with the "Jetty Boots" minigame.

### 2. Verify Window Detection

First, check if the auto-player can find your game window:

```bash
dotnet run -- --find-game
```

You should see output like:
```
Found: Deep Rock Galactic
Handle: 0x00123456
Position: (0, 0)
Size: 1920 x 1080
```

### 3. Run Calibration (Recommended)

Calibration helps the detection work better with your specific setup:

```bash
dotnet run -- --calibrate
```

The wizard will guide you through:
1. Selecting the game window/region
2. Sampling player character colors
3. Sampling obstacle colors
4. Saving settings to config file

### 4. Test Detection (Dry Run)

Before sending actual inputs, test that detection is working:

```bash
dotnet run -- --live-detection
```

This opens a debug window showing:
- Player detection (yellow box)
- Obstacle detection (red boxes)
- Gap target zones (green areas)

**Controls in debug window:**
- `Q` or `ESC` - Quit
- `P` - Pause/Resume
- `R` - Reset statistics

### 5. Test Decision Making

Test the full detection + decision pipeline without sending inputs:

```bash
dotnet run -- --play --dry-run
```

Watch the debug window - it shows when the bot *would* jump (green "Jump" indicator).

### 6. Run the Auto-Player (Live Mode)

When you're ready to play for real:

```bash
dotnet run -- --play
```

**Important:**
1. Start the Jetty Boots minigame in Deep Rock Galactic
2. Make sure the game window is focused
3. Press Enter in the console to start
4. The bot will automatically detect and play!

**Stop the bot:** Press `Q` in the debug window or `Ctrl+C` in the console.

## Command Reference

| Command | Description |
|---------|-------------|
| `--play` | Run auto-player (sends inputs) |
| `--play --dry-run` | Run without sending inputs |
| `--calibrate` | Run calibration wizard |
| `--live-detection` | Test detection only |
| `--live-decision` | Test detection + decisions |
| `--find-game` | Check if game window is found |
| `--list-windows` | List all windows |
| `--show-config` | Display current settings |
| `--create-config` | Create default config file |
| `--help` | Show all options |

## Configuration Overrides

Adjust settings from command line:

```bash
# Change target FPS
dotnet run -- --play --fps 60

# Use safer play style
dotnet run -- --play --play-style Safe

# Use mouse click instead of spacebar
dotnet run -- --play --use-mouse

# Disable debug window (runs headless)
dotnet run -- --play --no-debug-window
```

## Configuration File

Create a config file for persistent settings:

```bash
dotnet run -- --create-config
```

This creates `jettyboots.json` in the application directory. Edit it to customize:

```json
{
  "Capture": {
    "TargetFps": 30
  },
  "Gameplay": {
    "PlayStyle": "Balanced",
    "SafetyMargin": 15
  },
  "Input": {
    "JumpKey": "SPACE",
    "UseMouseClick": false
  }
}
```

## Troubleshooting

### "Game window not found"

- Make sure Deep Rock Galactic is running (not minimized)
- Try `--list-windows` to see detected windows
- Use `--calibrate` to manually select the game region

### Detection not working well

- Run `--calibrate` to sample colors from your game
- Adjust lighting/graphics settings in game
- Try `--live-detection` to see what's being detected

### Inputs not registering

- Make sure the game window is focused when playing
- Try `--use-mouse` flag for mouse click instead of keyboard
- Some games need administrator privileges - try running as admin

### Performance issues

- Lower FPS with `--fps 20`
- Disable debug window with `--no-debug-window`
- Close other applications

## Logs

Logs are saved to `logs/` directory:
- `jettyboots-YYYYMMDD.log` - General logs
- Session statistics printed at end of each run

## Tips for Best Results

1. **Windowed Mode**: Run DRG in windowed or borderless windowed mode
2. **Consistent Lighting**: Avoid in-game lighting effects that change colors
3. **Calibrate First**: Always run calibration for your specific setup
4. **Test Before Live**: Use `--dry-run` to verify detection before playing
5. **Start Simple**: Begin with `--play-style Safe` until detection is tuned
