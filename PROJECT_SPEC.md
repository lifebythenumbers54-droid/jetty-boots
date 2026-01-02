# Jetty Boots Auto-Player

## Project Overview

An automated player for the "Jetty Boots" minigame found in Deep Rock Galactic. Jetty Boots is a Flappy Bird-style arcade game where the player must navigate through obstacles by controlling vertical movement. This C# application will use screen capture and image processing to read the game state and automatically play to achieve high scores.

## Game Mechanics

Jetty Boots follows classic Flappy Bird mechanics:
- The player character continuously moves forward (horizontally)
- Gravity constantly pulls the character downward
- Player input causes the character to "jump" or boost upward
- Obstacles (pipes/gaps) appear that must be navigated through
- Collision with obstacles or screen boundaries ends the game
- Score increases based on obstacles passed

## Technical Approach

### 1. Screen Capture
- Capture the game window or a defined screen region in real-time
- Target frame rate: 30-60 FPS for responsive gameplay
- Use Windows APIs (GDI+, DirectX, or Windows.Graphics.Capture)

### 2. Image Processing & Game State Detection
- Detect player character position (vertical coordinate)
- Detect obstacle positions and gap locations
- Detect game state (playing, game over, menu)
- Detect score (optional, for logging)

### 3. Decision Engine
- Calculate optimal timing for jumps based on:
  - Current player height
  - Distance to next obstacle
  - Gap position of next obstacle
  - Player velocity/trajectory
- Implement prediction algorithm for jump timing

### 4. Input Simulation
- Simulate keyboard/mouse input to trigger jumps
- Use Windows Input Simulation APIs (SendInput)
- Minimize input latency for precise control

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Main Application                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Screen     │  │    Game      │  │   Decision   │       │
│  │   Capture    │──│    State     │──│    Engine    │       │
│  │   Module     │  │   Analyzer   │  │              │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
│         │                                    │               │
│         │                                    │               │
│         ▼                                    ▼               │
│  ┌──────────────┐                    ┌──────────────┐       │
│  │    Image     │                    │    Input     │       │
│  │   Processing │                    │  Simulator   │       │
│  │   (OpenCV)   │                    │              │       │
│  └──────────────┘                    └──────────────┘       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Components

### ScreenCapture
- `IScreenCapture` - Interface for screen capture implementations
- `GdiScreenCapture` - GDI+ based capture (simple, compatible)
- `DxScreenCapture` - DirectX based capture (faster, lower latency)

### GameStateAnalyzer
- `PlayerDetector` - Locates player character on screen
- `ObstacleDetector` - Identifies obstacles and gap positions
- `GameStateDetector` - Determines if game is active, paused, or over
- `ScoreReader` - OCR for reading current score (optional)

### DecisionEngine
- `TrajectoryCalculator` - Predicts player movement
- `JumpDecider` - Determines optimal jump timing
- `DifficultyAdapter` - Adjusts strategy based on game speed

### InputSimulator
- `KeyboardSimulator` - Sends keyboard input
- `MouseSimulator` - Sends mouse clicks (if needed)

## Dependencies

| Package | Purpose |
|---------|---------|
| OpenCvSharp4 | Image processing and computer vision |
| OpenCvSharp4.runtime.win | Windows runtime for OpenCV |
| System.Drawing.Common | Basic image handling |
| InputSimulatorStandard | Windows input simulation |

## Configuration

The application should support configuration for:
- Screen region to capture (or auto-detect game window)
- Color/template matching thresholds
- Jump timing parameters
- Target frame rate
- Debug mode (show detection overlays)

```json
{
  "captureRegion": {
    "autoDetect": true,
    "x": 0,
    "y": 0,
    "width": 800,
    "height": 600
  },
  "detection": {
    "playerColorLower": [0, 100, 100],
    "playerColorUpper": [20, 255, 255],
    "obstacleColorLower": [40, 100, 100],
    "obstacleColorUpper": [80, 255, 255]
  },
  "gameplay": {
    "jumpLeadTime": 50,
    "safetyMargin": 20,
    "targetFps": 30
  },
  "debug": {
    "showOverlay": false,
    "logDecisions": true
  }
}
```

## Development Phases

### Phase 1: Foundation
- [ ] Project setup with required NuGet packages
- [ ] Basic screen capture implementation
- [ ] Simple image display for verification

### Phase 2: Detection
- [ ] Player character detection
- [ ] Obstacle detection
- [ ] Gap position identification
- [ ] Game state detection (playing/game over)

### Phase 3: Decision Making
- [ ] Basic jump logic (jump when approaching obstacle)
- [ ] Trajectory prediction
- [ ] Optimal jump timing calculation

### Phase 4: Input & Integration
- [ ] Input simulation implementation
- [ ] Full game loop integration
- [ ] Timing optimization

### Phase 5: Refinement
- [ ] Parameter tuning for high scores
- [ ] Debug overlay for visualization
- [ ] Configuration file support
- [ ] Logging and statistics

## Challenges & Considerations

### Timing Precision
- Screen capture and processing introduce latency
- Input simulation has inherent delays
- May need to predict ahead to compensate

### Visual Detection
- Game graphics may vary (lighting, effects)
- Need robust detection that handles visual noise
- Color-based detection may need calibration

### Game Window Focus
- Game must be visible on screen (not minimized)
- May need to handle window focus for input

### Anti-Cheat Considerations
- This is for a single-player minigame
- No online leaderboards affected
- Personal use/experimentation only

## Usage

```
JettyBootsPlayer.exe [options]

Options:
  --config <path>     Path to configuration file
  --calibrate         Run calibration mode to set up detection
  --debug             Enable debug overlay
  --dry-run           Detect without sending inputs
```

## Success Criteria

- Reliably detect player and obstacle positions
- Successfully navigate through obstacles automatically
- Achieve scores higher than manual play average
- Maintain stable operation without crashes
- Respond quickly enough to handle increasing difficulty

## Future Enhancements

- Machine learning-based detection for robustness
- Neural network for decision making
- Recording and replay functionality
- Statistics dashboard
- Multiple strategy modes (safe vs aggressive)
