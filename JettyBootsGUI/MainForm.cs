using System.Diagnostics;

namespace JettyBootsGUI;

public class MainForm : Form
{
    private readonly string _jettyBootsPath;
    private RichTextBox _outputBox = null!;
    private Process? _runningProcess;
    private Button? _stopButton;
    private ToolStripStatusLabel _statusLabel = null!;
    private CheckBox _dryRunCheckbox = null!;
    private CheckBox _autoStartCheckbox = null!;
    private ComboBox _playStyleCombo = null!;
    private NumericUpDown _fpsNumeric = null!;

    public MainForm()
    {
        // Find the JettyBoots executable path
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        _jettyBootsPath = Path.Combine(projectRoot, "JettyBoots");

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = "Jetty Boots Auto-Player";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Left panel - Commands
        var leftPanel = CreateCommandPanel();
        mainPanel.Controls.Add(leftPanel, 0, 0);

        // Right panel - Output
        var rightPanel = CreateOutputPanel();
        mainPanel.Controls.Add(rightPanel, 1, 0);

        Controls.Add(mainPanel);

        // Status bar
        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(_statusLabel);
        Controls.Add(statusStrip);

        FormClosing += (s, e) => StopProcess();
    }

    private Panel CreateCommandPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var flowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(5)
        };

        // Title
        flowPanel.Controls.Add(CreateLabel("Jetty Boots Commands", 14, FontStyle.Bold));
        flowPanel.Controls.Add(CreateSeparator());

        // Play Section
        flowPanel.Controls.Add(CreateLabel("Play", 11, FontStyle.Bold));

        var playButton = CreateButton("Play Auto-Player", "Start playing the game automatically", Color.FromArgb(46, 204, 113));
        playButton.Click += (s, e) => RunCommand(BuildPlayCommand());
        flowPanel.Controls.Add(playButton);

        _autoStartCheckbox = new CheckBox
        {
            Text = "Auto-Start (focus & hold E)",
            AutoSize = true,
            Checked = true,  // Default to auto-start
            Margin = new Padding(5, 2, 5, 2)
        };
        flowPanel.Controls.Add(_autoStartCheckbox);

        _dryRunCheckbox = new CheckBox
        {
            Text = "Dry Run (no inputs)",
            AutoSize = true,
            Margin = new Padding(5, 2, 5, 5)
        };
        _dryRunCheckbox.CheckedChanged += (s, e) =>
        {
            // Disable auto-start if dry run is checked
            _autoStartCheckbox.Enabled = !_dryRunCheckbox.Checked;
            if (_dryRunCheckbox.Checked) _autoStartCheckbox.Checked = false;
        };
        flowPanel.Controls.Add(_dryRunCheckbox);

        // Play Style
        var stylePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(5, 2, 5, 5) };
        stylePanel.Controls.Add(new Label { Text = "Style:", AutoSize = true, Margin = new Padding(0, 3, 5, 0) });
        _playStyleCombo = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _playStyleCombo.Items.AddRange(new[] { "Balanced", "Safe", "Aggressive" });
        _playStyleCombo.SelectedIndex = 0;
        stylePanel.Controls.Add(_playStyleCombo);
        flowPanel.Controls.Add(stylePanel);

        // FPS
        var fpsPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(5, 2, 5, 10) };
        fpsPanel.Controls.Add(new Label { Text = "FPS:", AutoSize = true, Margin = new Padding(0, 3, 5, 0) });
        _fpsNumeric = new NumericUpDown { Width = 60, Minimum = 10, Maximum = 120, Value = 30 };
        fpsPanel.Controls.Add(_fpsNumeric);
        flowPanel.Controls.Add(fpsPanel);

        _stopButton = CreateButton("Stop", "Stop the running process", Color.FromArgb(231, 76, 60));
        _stopButton.Enabled = false;
        _stopButton.Click += (s, e) => StopProcess();
        flowPanel.Controls.Add(_stopButton);

        flowPanel.Controls.Add(CreateSeparator());

        // Setup Section
        flowPanel.Controls.Add(CreateLabel("Setup & Calibration", 11, FontStyle.Bold));

        var calibrateButton = CreateButton("Calibrate", "Run calibration wizard to set up detection");
        calibrateButton.Click += (s, e) => RunCommand("--calibrate");
        flowPanel.Controls.Add(calibrateButton);

        var findGameButton = CreateButton("Find Game Window", "Check if Deep Rock Galactic is detected");
        findGameButton.Click += (s, e) => RunCommand("--find-game");
        flowPanel.Controls.Add(findGameButton);

        var listWindowsButton = CreateButton("List Windows", "Show all visible windows");
        listWindowsButton.Click += (s, e) => RunCommand("--list-windows");
        flowPanel.Controls.Add(listWindowsButton);

        flowPanel.Controls.Add(CreateSeparator());

        // Testing Section
        flowPanel.Controls.Add(CreateLabel("Testing", 11, FontStyle.Bold));

        var liveDetectionButton = CreateButton("Live Detection", "Test detection with visual overlay");
        liveDetectionButton.Click += (s, e) => RunCommand("--live-detection");
        flowPanel.Controls.Add(liveDetectionButton);

        var liveDecisionButton = CreateButton("Live Decision", "Test detection + decision making");
        liveDecisionButton.Click += (s, e) => RunCommand("--live-decision");
        flowPanel.Controls.Add(liveDecisionButton);

        var testCaptureButton = CreateButton("Test Capture", "Test screen capture performance");
        testCaptureButton.Click += (s, e) => RunCommand("--test-capture");
        flowPanel.Controls.Add(testCaptureButton);

        var testDetectionButton = CreateButton("Test Detection", "Test detection on a single frame");
        testDetectionButton.Click += (s, e) => RunCommand("--test-detection");
        flowPanel.Controls.Add(testDetectionButton);

        var testDecisionButton = CreateButton("Test Decision", "Test decision engine logic");
        testDecisionButton.Click += (s, e) => RunCommand("--test-decision");
        flowPanel.Controls.Add(testDecisionButton);

        flowPanel.Controls.Add(CreateSeparator());

        // Configuration Section
        flowPanel.Controls.Add(CreateLabel("Configuration", 11, FontStyle.Bold));

        var showConfigButton = CreateButton("Show Config", "Display current configuration");
        showConfigButton.Click += (s, e) => RunCommand("--show-config");
        flowPanel.Controls.Add(showConfigButton);

        var createConfigButton = CreateButton("Create Config", "Create default configuration file");
        createConfigButton.Click += (s, e) => RunCommand("--create-config");
        flowPanel.Controls.Add(createConfigButton);

        var helpButton = CreateButton("Show Help", "Display all available commands");
        helpButton.Click += (s, e) => ShowCommandHelp();
        flowPanel.Controls.Add(helpButton);

        flowPanel.Controls.Add(CreateSeparator());

        // Utility
        var clearButton = CreateButton("Clear Output", "Clear the output window", Color.FromArgb(149, 165, 166));
        clearButton.Click += (s, e) => _outputBox.Clear();
        flowPanel.Controls.Add(clearButton);

        panel.Controls.Add(flowPanel);
        return panel;
    }

    private Panel CreateOutputPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var label = new Label
        {
            Text = "Output",
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 25
        };

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None,
            WordWrap = true
        };

        panel.Controls.Add(_outputBox);
        panel.Controls.Add(label);

        return panel;
    }

    private Button CreateButton(string text, string tooltip, Color? backColor = null)
    {
        var button = new Button
        {
            Text = text,
            Width = 250,
            Height = 32,
            Margin = new Padding(5, 3, 5, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor ?? Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;

        var toolTip = new ToolTip();
        toolTip.SetToolTip(button, tooltip);

        return button;
    }

    private Label CreateLabel(string text, float size, FontStyle style)
    {
        return new Label
        {
            Text = text,
            Font = new Font(Font.FontFamily, size, style),
            AutoSize = true,
            Margin = new Padding(5, 10, 5, 5)
        };
    }

    private Panel CreateSeparator()
    {
        return new Panel
        {
            Height = 1,
            Width = 250,
            BackColor = Color.FromArgb(200, 200, 200),
            Margin = new Padding(5, 10, 5, 10)
        };
    }

    private string BuildPlayCommand()
    {
        var args = "--play";

        if (_dryRunCheckbox.Checked)
            args += " --dry-run";
        else if (_autoStartCheckbox.Checked)
            args += " --auto-start";

        if (_playStyleCombo.SelectedItem?.ToString() != "Balanced")
            args += $" --play-style {_playStyleCombo.SelectedItem}";

        if (_fpsNumeric.Value != 30)
            args += $" --fps {_fpsNumeric.Value}";

        // Always use mouse click for Jetty Boots
        args += " --use-mouse";

        return args;
    }

    private void RunCommand(string arguments)
    {
        if (_runningProcess != null && !_runningProcess.HasExited)
        {
            AppendOutput("\n[ERROR] A process is already running. Stop it first.\n", Color.Red);
            return;
        }

        AppendOutput($"\n{'=',-60}\n", Color.Gray);
        AppendOutput($"Running: dotnet run -- {arguments}\n", Color.Cyan);
        AppendOutput($"{'=',-60}\n\n", Color.Gray);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run -- {arguments}",
                WorkingDirectory = _jettyBootsPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _runningProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _runningProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    BeginInvoke(() => AppendOutput(e.Data + "\n", Color.White));
            };

            _runningProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    BeginInvoke(() => AppendOutput(e.Data + "\n", Color.Orange));
            };

            _runningProcess.Exited += (s, e) =>
            {
                BeginInvoke(() =>
                {
                    var exitCode = _runningProcess?.ExitCode ?? -1;
                    var color = exitCode == 0 ? Color.LightGreen : Color.Red;
                    AppendOutput($"\n[Process exited with code {exitCode}]\n", color);
                    UpdateStatus("Ready");
                    if (_stopButton != null) _stopButton.Enabled = false;
                    _runningProcess = null;
                });
            };

            _runningProcess.Start();
            _runningProcess.BeginOutputReadLine();
            _runningProcess.BeginErrorReadLine();

            UpdateStatus($"Running: {arguments}");
            if (_stopButton != null) _stopButton.Enabled = true;
        }
        catch (Exception ex)
        {
            AppendOutput($"[ERROR] Failed to start process: {ex.Message}\n", Color.Red);
            UpdateStatus("Error");
        }
    }

    private void StopProcess()
    {
        if (_runningProcess != null && !_runningProcess.HasExited)
        {
            try
            {
                _runningProcess.Kill(entireProcessTree: true);
                AppendOutput("\n[Process stopped by user]\n", Color.Yellow);
            }
            catch (Exception ex)
            {
                AppendOutput($"[ERROR] Failed to stop process: {ex.Message}\n", Color.Red);
            }
        }
    }

    private void AppendOutput(string text, Color color)
    {
        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.SelectionLength = 0;
        _outputBox.SelectionColor = color;
        _outputBox.AppendText(text);
        _outputBox.SelectionColor = _outputBox.ForeColor;
        _outputBox.ScrollToCaret();
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }

    private void ShowCommandHelp()
    {
        _outputBox.Clear();
        AppendOutput(@"
╔══════════════════════════════════════════════════════════════════════╗
║               JETTY BOOTS AUTO-PLAYER - COMMAND REFERENCE            ║
╚══════════════════════════════════════════════════════════════════════╝

PLAY COMMANDS
─────────────────────────────────────────────────────────────────────────
  --play                  Run the auto-player (sends inputs to game)
  --play --auto-start     Auto-focus window, hold E, and start minigame
  --play --dry-run        Run auto-player without sending inputs
  --use-mouse             Use mouse click for jumping (recommended)

HOW TO USE
─────────────────────────────────────────────────────────────────────────
  1. Start Deep Rock Galactic and go to the Space Rig
  2. Stand in front of the Jetty Boots arcade machine
  3. Click 'Play Auto-Player' with 'Auto-Start' checked
  4. The bot will: focus the game, hold E to start, then play!

SETUP & CALIBRATION
─────────────────────────────────────────────────────────────────────────
  --calibrate             Run the calibration wizard
  --find-game             Find Deep Rock Galactic window
  --list-windows          List all visible windows

TESTING
─────────────────────────────────────────────────────────────────────────
  --live-detection        Run live detection with visual overlay
  --live-decision         Run live detection + decision making
  --test-capture          Test screen capture functionality
  --test-detection        Test detection on a single frame
  --test-decision         Test decision engine with trajectory simulation

CONFIGURATION
─────────────────────────────────────────────────────────────────────────
  --config, -c <file>     Load configuration from specified file
  --show-config           Display current configuration
  --create-config         Create default configuration file

CONFIGURATION OVERRIDES
─────────────────────────────────────────────────────────────────────────
  --fps <value>           Target frames per second (default: 30)
  --safety-margin <px>    Safety margin from gap edges
  --play-style <style>    Play style: Safe, Balanced, Aggressive
  --jump-key <key>        Key for jumping (e.g., SPACE, UP)
  --use-mouse             Use mouse click instead of keyboard
  --no-debug              Disable debug overlay
  --no-debug-window       Disable debug window
  --save-frames           Save debug frames to disk
  --log-level <level>     Log level: Verbose, Debug, Information, Warning, Error
  --log-decisions         Log individual decisions for analysis
  --no-logging            Disable all logging

DEBUG WINDOW CONTROLS
─────────────────────────────────────────────────────────────────────────
  Q / ESC                 Quit the application
  P                       Pause/Resume
  R                       Reset statistics

─────────────────────────────────────────────────────────────────────────
", Color.LightGreen);
    }
}
