using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DigitalManometerMonitor.Core.Acquisition;
using DigitalManometerMonitor.Core.Measurement;

namespace DigitalManometerMonitor;

public sealed class MainForm : Form
{
    private readonly MeasurementController _controller = new();
    private readonly PreviewMonitorController _previewController = new();

    private readonly ComboBox _channelCombo = new();
    private readonly Button _refreshChannelsButton = new();
    private readonly Label _daqStatusValue = new();

    private readonly NumericUpDown _voltageLow = new();
    private readonly NumericUpDown _voltageHigh = new();
    private readonly NumericUpDown _pressureLow = new();
    private readonly NumericUpDown _pressureHigh = new();

    private readonly TextBox _directoryText = new();
    private readonly TextBox _fileNameText = new();
    private readonly Button _browseButton = new();

    private readonly RadioButton _manualRadio = new();
    private readonly RadioButton _timerRadio = new();
    private readonly NumericUpDown _timerSeconds = new();

    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();

    private readonly Label _pressureValue = new();
    private readonly Label _statusLabel = new();
    private readonly Label _elapsedLabel = new();

    private readonly PressurePlotControl _plot = new();

    private bool _allowClose;
    private bool _closing;
    private bool _suppressChannelEvents;
    private bool _measurementTransitionActive;
    private bool _previewScaleValid = true;

    private int _lastDisplayedSecond = -1;
    private long _lastMeasurementUiUpdateMs;

    public MainForm()
    {
        Text = "Digital Manometer Pressure Monitor";

        StartPosition = FormStartPosition.CenterScreen;

        Font = new Font(
            "Segoe UI",
            10.5F,
            FontStyle.Regular);

        AutoScaleMode = AutoScaleMode.Dpi;

        ConfigureInitialWindowBounds();

        BuildUi();
        InitializeDefaults();
        WireEvents();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        await RefreshChannelsAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewController.Dispose();
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    // ============================================================
    // Window size
    // ============================================================

    private void ConfigureInitialWindowBounds()
    {
        Rectangle area =
            Screen.PrimaryScreen?.WorkingArea
            ?? new Rectangle(0, 0, 1366, 768);

        int availableWidth =
            Math.Max(800, area.Width - 12);

        int availableHeight =
            Math.Max(560, area.Height - 12);

        int targetWidth =
            Math.Min(
                availableWidth,
                Math.Max(
                    1080,
                    (int)Math.Round(area.Width * 0.96)));

        int targetHeight =
            Math.Min(
                availableHeight,
                Math.Max(
                    620,
                    (int)Math.Round(area.Height * 0.98)));

        MinimumSize = new Size(
            Math.Min(1020, availableWidth),
            Math.Min(580, availableHeight));

        Size = new Size(
            targetWidth,
            targetHeight);
    }

    // ============================================================
    // Main UI
    // ============================================================

    private void BuildUi()
    {
        BackColor =
            Color.FromArgb(246, 248, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 1,
            RowCount = 3,

            Padding = new Padding(8),

            BackColor = BackColor
        };

        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        // Header
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        // Main content
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        // Bottom status bar
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        Controls.Add(root);

        // --------------------------------------------------------
        // Header
        // --------------------------------------------------------

        root.Controls.Add(
            BuildHeader(),
            0,
            0);

        // --------------------------------------------------------
        // Main content
        // --------------------------------------------------------

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 2,
            RowCount = 1,

            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        /*
         * Left : settings
         * Right: monitor + graph
         *
         * 35 / 65 gives enough width for English labels while
         * preserving a large plot area.
         */
        content.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                35F));

        content.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                65F));

        content.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        Control left =
            BuildControlPanel();

        left.Margin =
            new Padding(
                0,
                2,
                7,
                2);

        Control right =
            BuildMonitorPanel();

        right.Margin =
            new Padding(
                7,
                2,
                0,
                2);

        content.Controls.Add(
            left,
            0,
            0);

        content.Controls.Add(
            right,
            1,
            0);

        root.Controls.Add(
            content,
            0,
            1);

        // --------------------------------------------------------
        // Bottom status bar
        // --------------------------------------------------------

        root.Controls.Add(
            BuildStatusBar(),
            0,
            2);
    }

    // ============================================================
    // Header
    // ============================================================

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 1,
            RowCount = 2,

            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        header.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        header.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,

            Text =
                "Digital Manometer Pressure Monitor",

            Font = new Font(
                "Segoe UI",
                17F,
                FontStyle.Bold),

            Margin =
                new Padding(
                    0,
                    0,
                    0,
                    0)
        };

        var subtitle = new Label
        {
            AutoSize = true,

            Text =
                "NI USB-6000 | RSE analog input | 1000 Hz hardware-timed acquisition",

            ForeColor =
                Color.DimGray,

            Margin =
                new Padding(
                    2,
                    0,
                    0,
                    0)
        };

        header.Controls.Add(
            title,
            0,
            0);

        header.Controls.Add(
            subtitle,
            0,
            1);

        return header;
    }

    // ============================================================
    // Bottom status bar
    // ============================================================

    private Control BuildStatusBar()
    {
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 2,
            RowCount = 1,

            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        bottom.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        bottom.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        _statusLabel.Dock =
            DockStyle.Fill;

        _statusLabel.TextAlign =
            ContentAlignment.MiddleLeft;

        _statusLabel.Text =
            "Ready";

        _statusLabel.ForeColor =
            Color.FromArgb(
                50,
                70,
                85);

        _statusLabel.AutoEllipsis =
            true;

        _elapsedLabel.AutoSize =
            true;

        _elapsedLabel.TextAlign =
            ContentAlignment.MiddleRight;

        _elapsedLabel.Anchor =
            AnchorStyles.Right;

        _elapsedLabel.Font =
            new Font(
                "Segoe UI",
                11F,
                FontStyle.Bold);

        _elapsedLabel.Text =
            "Elapsed: 0 s";

        bottom.Controls.Add(
            _statusLabel,
            0,
            0);

        bottom.Controls.Add(
            _elapsedLabel,
            1,
            0);

        return bottom;
    }

    // ============================================================
    // Left control panel
    //
    // Important:
    // Measurement buttons are outside the scrolling region.
    //
    // Even on a small laptop display, Start / Stop remain visible.
    // ============================================================

    private Control BuildControlPanel()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 1,
            RowCount = 2,

            BackColor =
                Color.Transparent,

            Padding =
                new Padding(0),

            Margin =
                new Padding(0)
        };

        outer.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        // Settings area uses all remaining vertical space.
        outer.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        // Start / Stop stays visible.
        outer.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        // --------------------------------------------------------
        // Scrollable settings section
        // --------------------------------------------------------

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            ColumnCount = 1,
            RowCount = 4,

            AutoScroll = true,

            BackColor =
                Color.Transparent,

            Padding =
                new Padding(
                    0,
                    0,
                    3,
                    0),

            Margin =
                new Padding(0)
        };

        settingsPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        for (int i = 0; i < 4; i++)
        {
            settingsPanel.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
        }

        settingsPanel.Controls.Add(
            BuildDaqGroup(),
            0,
            0);

        settingsPanel.Controls.Add(
            BuildScaleGroup(),
            0,
            1);

        settingsPanel.Controls.Add(
            BuildOutputGroup(),
            0,
            2);

        settingsPanel.Controls.Add(
            BuildStopModeGroup(),
            0,
            3);

        // --------------------------------------------------------
        // Measurement buttons
        // --------------------------------------------------------

        Control actionGroup =
            BuildActionGroup();

        actionGroup.Margin =
            new Padding(
                0,
                3,
                3,
                0);

        outer.Controls.Add(
            settingsPanel,
            0,
            0);

        outer.Controls.Add(
            actionGroup,
            0,
            1);

        return outer;
    }

    // ============================================================
    // DAQ group
    // ============================================================

    private GroupBox BuildDaqGroup()
    {
        var group =
            MakeGroup("DAQ Input");

        var table =
            MakeTwoColumnTable();

        // --------------------------------------------------------
        // AI Channel
        // --------------------------------------------------------

        _channelCombo.DropDownStyle =
            ComboBoxStyle.DropDownList;

        _channelCombo.Dock =
            DockStyle.Fill;

        _channelCombo.MinimumSize =
            new Size(
                80,
                0);

        AddRow(
            table,
            MakeLabel("AI Channel"),
            _channelCombo);

        // --------------------------------------------------------
        // Detect / refresh button
        // --------------------------------------------------------

        _refreshChannelsButton.Text =
            "Detect / Refresh DAQ";

        _refreshChannelsButton.Dock =
            DockStyle.Fill;

        _refreshChannelsButton.AutoSize =
            true;

        _refreshChannelsButton.AutoSizeMode =
            AutoSizeMode.GrowAndShrink;

        _refreshChannelsButton.Padding =
            new Padding(
                4,
                1,
                4,
                1);

        _refreshChannelsButton.MinimumSize =
            new Size(
                0,
                30);

        AddSpanRow(
            table,
            _refreshChannelsButton);

        // --------------------------------------------------------
        // Terminal mode
        // --------------------------------------------------------

        AddRow(
            table,
            MakeLabel("Terminal Mode"),
            MakeValueLabel("RSE (fixed)"));

        // --------------------------------------------------------
        // DAQ Status
        // --------------------------------------------------------

        _daqStatusValue.Text =
            "Not connected";

        _daqStatusValue.AutoSize =
            true;

        _daqStatusValue.Anchor =
            AnchorStyles.Left;

        _daqStatusValue.ForeColor =
            Color.DimGray;

        AddRow(
            table,
            MakeLabel("DAQ Status"),
            _daqStatusValue);

        group.Controls.Add(table);

        return group;
    }

    // ============================================================
    // Pressure scale group
    // ============================================================

    private GroupBox BuildScaleGroup()
    {
        var group =
            MakeGroup(
                "Linear Pressure Scale");

        var table =
            MakeTwoColumnTable();

        ConfigureNumber(
            _voltageLow,
            -10M,
            10M,
            3,
            0M,
            0.001M);

        ConfigureNumber(
            _voltageHigh,
            -10M,
            10M,
            3,
            10M,
            0.001M);

        ConfigureNumber(
            _pressureLow,
            -1_000_000_000M,
            1_000_000_000M,
            1,
            0M,
            1M);

        ConfigureNumber(
            _pressureHigh,
            -1_000_000_000M,
            1_000_000_000M,
            1,
            100_000M,
            1M);

        AddRow(
            table,
            MakeLabel("Voltage Low (V)"),
            _voltageLow);

        AddRow(
            table,
            MakeLabel("Voltage High (V)"),
            _voltageHigh);

        AddRow(
            table,
            MakeLabel("Pressure at Low (Pa)"),
            _pressureLow);

        AddRow(
            table,
            MakeLabel("Pressure at High (Pa)"),
            _pressureHigh);

        group.Controls.Add(table);

        return group;
    }

    // ============================================================
    // CSV output group
    // ============================================================

    private GroupBox BuildOutputGroup()
    {
        var group =
            MakeGroup("CSV Output");

        var table =
            MakeTwoColumnTable();

        var dirPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,

            AutoSize = true,

            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,

            ColumnCount = 2,
            RowCount = 1,

            Margin =
                new Padding(0),

            Padding =
                new Padding(0)
        };

        dirPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        dirPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        _directoryText.Dock =
            DockStyle.Fill;

        _browseButton.Text =
            "...";

        _browseButton.AutoSize =
            true;

        _browseButton.MinimumSize =
            new Size(
                38,
                0);

        dirPanel.Controls.Add(
            _directoryText,
            0,
            0);

        dirPanel.Controls.Add(
            _browseButton,
            1,
            0);

        _fileNameText.Dock =
            DockStyle.Fill;

        AddRow(
            table,
            MakeLabel("Directory"),
            dirPanel);

        AddRow(
            table,
            MakeLabel("File Name"),
            _fileNameText);

        var note = new Label
        {
            Text =
                "Existing files are never overwritten.",

            AutoSize =
                true,

            ForeColor =
                Color.DimGray,

            Margin =
                new Padding(
                    3,
                    1,
                    3,
                    1),

            Anchor =
                AnchorStyles.Left
        };

        AddSpanRow(
            table,
            note);

        group.Controls.Add(table);

        return group;
    }

    // ============================================================
    // Stop mode group
    // ============================================================

    private GroupBox BuildStopModeGroup()
    {
        var group =
            MakeGroup(
                "Measurement Stop Mode");

        var table =
            MakeTwoColumnTable();

        // --------------------------------------------------------
        // Manual
        // --------------------------------------------------------

        _manualRadio.Text =
            "Manual stop";

        _manualRadio.Checked =
            true;

        _manualRadio.AutoSize =
            true;

        _manualRadio.Anchor =
            AnchorStyles.Left;

        AddSpanRow(
            table,
            _manualRadio);

        // --------------------------------------------------------
        // Timer
        // --------------------------------------------------------

        _timerRadio.Text =
            "Timer (s)";

        _timerRadio.AutoSize =
            true;

        _timerRadio.Anchor =
            AnchorStyles.Left;

        ConfigureNumber(
            _timerSeconds,
            0.001M,
            8_640_000M,
            3,
            60M,
            0.001M);

        AddRow(
            table,
            _timerRadio,
            _timerSeconds);

        group.Controls.Add(table);

        return group;
    }

    // ============================================================
    // Measurement action group
    //
    // This group stays visible at all times.
    // ============================================================

    private GroupBox BuildActionGroup()
    {
        var group =
            MakeGroup("Measurement");

        var buttons = new TableLayoutPanel
        {
            Dock =
                DockStyle.Top,

            AutoSize =
                true,

            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,

            ColumnCount =
                2,

            RowCount =
                1,

            Margin =
                new Padding(0),

            Padding =
                new Padding(1)
        };

        buttons.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                50F));

        buttons.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                50F));

        // --------------------------------------------------------
        // Start
        // --------------------------------------------------------

        _startButton.Text =
            "Start Measurement";

        _startButton.Dock =
            DockStyle.Fill;

        _startButton.MinimumSize =
            new Size(
                0,
                34);

        _startButton.Enabled =
            false;

        // --------------------------------------------------------
        // Stop
        // --------------------------------------------------------

        _stopButton.Text =
            "Stop Measurement";

        _stopButton.Dock =
            DockStyle.Fill;

        _stopButton.MinimumSize =
            new Size(
                0,
                34);

        _stopButton.Enabled =
            false;

        buttons.Controls.Add(
            _startButton,
            0,
            0);

        buttons.Controls.Add(
            _stopButton,
            1,
            0);

        group.Controls.Add(buttons);

        return group;
    }

    // ============================================================
    // Right monitoring panel
    // ============================================================

    private Control BuildMonitorPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock =
                DockStyle.Fill,

            RowCount =
                2,

            ColumnCount =
                1,

            BackColor =
                Color.White,

            Padding =
                new Padding(12),

            Margin =
                new Padding(0)
        };

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        // --------------------------------------------------------
        // Current pressure
        // --------------------------------------------------------

        var valuePanel = new TableLayoutPanel
        {
            Dock =
                DockStyle.Fill,

            ColumnCount =
                1,

            RowCount =
                2,

            Margin =
                new Padding(0),

            Padding =
                new Padding(
                    2,
                    0,
                    0,
                    0)
        };

        valuePanel.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        valuePanel.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        var caption = new Label
        {
            Text =
                "Current Pressure",

            AutoSize =
                true,

            ForeColor =
                Color.DimGray,

            Margin =
                new Padding(
                    0,
                    2,
                    0,
                    0)
        };

        _pressureValue.Text =
            "-- Pa";

        _pressureValue.AutoSize =
            true;

        _pressureValue.Anchor =
            AnchorStyles.Left;

        _pressureValue.Font =
            new Font(
                "Segoe UI",
                28F,
                FontStyle.Bold);

        _pressureValue.TextAlign =
            ContentAlignment.MiddleLeft;

        _pressureValue.Margin =
            new Padding(0);

        valuePanel.Controls.Add(
            caption,
            0,
            0);

        valuePanel.Controls.Add(
            _pressureValue,
            0,
            1);

        panel.Controls.Add(
            valuePanel,
            0,
            0);

        // --------------------------------------------------------
        // Plot
        // --------------------------------------------------------

        _plot.Dock =
            DockStyle.Fill;

        _plot.Margin =
            new Padding(0);

        panel.Controls.Add(
            _plot,
            0,
            1);

        return panel;
    }

    // ============================================================
    // Defaults
    // ============================================================

    private void InitializeDefaults()
    {
        string defaultDir =
            GetDefaultDataDirectory(
                "DigitalManometerMonitor.sln");

        try
        {
            Directory.CreateDirectory(
                defaultDir);
        }
        catch
        {
            defaultDir =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);
        }

        _directoryText.Text =
            defaultDir;

        _fileNameText.Text =
            $"pressure_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        _timerSeconds.Enabled =
            false;
    }

    private static string GetDefaultDataDirectory(
        string solutionFileName)
    {
        string current =
            Path.GetFullPath(
                AppContext.BaseDirectory);

        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(
                    Path.Combine(
                        current,
                        solutionFileName)))
            {
                return Path.Combine(
                    current,
                    "Data");
            }

            DirectoryInfo? parent =
                Directory.GetParent(
                    current);

            if (parent is null)
                break;

            current =
                parent.FullName;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "Data");
    }

    // ============================================================
    // Event wiring
    // ============================================================

    private void WireEvents()
    {
        _refreshChannelsButton.Click +=
            async (_, _) =>
                await RefreshChannelsAsync();

        _channelCombo.SelectedIndexChanged +=
            async (_, _) =>
            {
                if (!_suppressChannelEvents &&
                    !_controller.IsRunning)
                {
                    await StartPreviewForSelectedChannelAsync(
                        updateMainStatus: true);
                }
            };

        _browseButton.Click +=
            (_, _) =>
                BrowseForDirectory();

        _manualRadio.CheckedChanged +=
            (_, _) =>
            {
                _timerSeconds.Enabled =
                    _timerRadio.Checked &&
                    !_controller.IsRunning;
            };

        _timerRadio.CheckedChanged +=
            (_, _) =>
            {
                _timerSeconds.Enabled =
                    _timerRadio.Checked &&
                    !_controller.IsRunning;
            };

        // Live update of pressure conversion scale
        _voltageLow.ValueChanged +=
            (_, _) =>
                UpdatePreviewScaleFromUi();

        _voltageHigh.ValueChanged +=
            (_, _) =>
                UpdatePreviewScaleFromUi();

        _pressureLow.ValueChanged +=
            (_, _) =>
                UpdatePreviewScaleFromUi();

        _pressureHigh.ValueChanged +=
            (_, _) =>
                UpdatePreviewScaleFromUi();

        _startButton.Click +=
            async (_, _) =>
                await StartMeasurementAsync();

        _stopButton.Click +=
            async (_, _) =>
                await StopMeasurementAsync();

        _previewController.Updated +=
            PreviewControllerOnUpdated;

        _previewController.Faulted +=
            PreviewControllerOnFaulted;

        _controller.Updated +=
            ControllerOnUpdated;

        _controller.Completed +=
            ControllerOnCompleted;

        FormClosing +=
            MainForm_FormClosing;
    }

    // ============================================================
    // DAQ detection
    // ============================================================

    private async Task RefreshChannelsAsync()
    {
        if (_controller.IsRunning ||
            _closing)
        {
            return;
        }

        _refreshChannelsButton.Enabled =
            false;

        _channelCombo.Enabled =
            false;

        _daqStatusValue.Text =
            "Searching...";

        _daqStatusValue.ForeColor =
            Color.DarkGoldenrod;

        _statusLabel.Text =
            "Searching for NI-DAQ analog input channels...";

        try
        {
            await _previewController.StopAsync();

            IReadOnlyList<string> channels =
                await Task.Run(
                    NIDaqChannelDiscovery.GetAnalogInputChannels);

            string? previous =
                _channelCombo.SelectedItem as string;

            _suppressChannelEvents =
                true;

            try
            {
                _channelCombo.Items.Clear();

                foreach (string channel in channels)
                {
                    _channelCombo.Items.Add(
                        channel);
                }

                if (previous is not null &&
                    _channelCombo.Items.Contains(previous))
                {
                    _channelCombo.SelectedItem =
                        previous;
                }
                else if (_channelCombo.Items.Count > 0)
                {
                    _channelCombo.SelectedIndex =
                        0;
                }
            }
            finally
            {
                _suppressChannelEvents =
                    false;
            }

            if (channels.Count == 0)
            {
                _daqStatusValue.Text =
                    "No AI channel detected";

                _daqStatusValue.ForeColor =
                    Color.Firebrick;

                _pressureValue.Text =
                    "-- Pa";

                _statusLabel.Text =
                    "No NI-DAQ analog input channels detected.";

                return;
            }

            _statusLabel.Text =
                $"Detected {channels.Count} analog input channel(s). Starting live monitor...";

            await StartPreviewForSelectedChannelAsync(
                updateMainStatus: true);
        }
        catch (DllNotFoundException)
        {
            SetDaqUnavailable(
                "NI-DAQmx runtime not found.");

            ShowError(
                "NI-DAQmx runtime was not found. Install NI-DAQmx, then restart this application.");
        }
        catch (PlatformNotSupportedException ex)
        {
            SetDaqUnavailable(
                "NI-DAQmx is available only on Windows.");

            ShowError(
                ex.Message);
        }
        catch (Exception ex)
        {
            SetDaqUnavailable(
                "DAQ detection failed.");

            ShowError(
                ex.Message);
        }
        finally
        {
            bool editable =
                !_controller.IsRunning &&
                !_closing;

            _refreshChannelsButton.Enabled =
                editable;

            _channelCombo.Enabled =
                editable &&
                _channelCombo.Items.Count > 0;

            UpdateStartButtonState();
        }
    }

    // ============================================================
    // Live preview
    // ============================================================

    private async Task<bool>
        StartPreviewForSelectedChannelAsync(
            bool updateMainStatus)
    {
        if (_controller.IsRunning ||
            _closing)
        {
            return false;
        }

        string? channel =
            _channelCombo.SelectedItem as string;

        if (string.IsNullOrWhiteSpace(channel))
        {
            _daqStatusValue.Text =
                "No channel selected";

            _daqStatusValue.ForeColor =
                Color.Firebrick;

            _pressureValue.Text =
                "-- Pa";

            return false;
        }

        PressureScale scale;

        try
        {
            scale =
                BuildScaleFromUi();

            scale.Validate();

            _previewScaleValid =
                true;
        }
        catch (Exception ex)
        {
            _previewScaleValid =
                false;

            _daqStatusValue.Text =
                "Invalid pressure scale";

            _daqStatusValue.ForeColor =
                Color.Firebrick;

            _pressureValue.Text =
                "Scale invalid";

            if (updateMainStatus)
            {
                _statusLabel.Text =
                    ex.Message;
            }

            return false;
        }

        try
        {
            _daqStatusValue.Text =
                "Connecting...";

            _daqStatusValue.ForeColor =
                Color.DarkGoldenrod;

            await _previewController.StartAsync(
                channel,
                scale);

            _daqStatusValue.Text =
                "Connected / Monitoring";

            _daqStatusValue.ForeColor =
                Color.DarkGreen;

            if (updateMainStatus)
            {
                _statusLabel.Text =
                    $"Live monitoring | {channel} | 1000 Hz";
            }

            UpdateStartButtonState();

            return true;
        }
        catch (Exception ex)
        {
            _daqStatusValue.Text =
                "Connection failed";

            _daqStatusValue.ForeColor =
                Color.Firebrick;

            _pressureValue.Text =
                "-- Pa";

            if (updateMainStatus)
            {
                _statusLabel.Text =
                    "DAQ live monitor could not start.";

                ShowError(
                    ex.Message);
            }

            UpdateStartButtonState();

            return false;
        }
    }

    private void UpdatePreviewScaleFromUi()
    {
        try
        {
            PressureScale scale =
                BuildScaleFromUi();

            scale.Validate();

            _previewScaleValid =
                true;

            _previewController.UpdateScale(
                scale);

            UpdateStartButtonState();
        }
        catch
        {
            _previewScaleValid =
                false;

            if (!_controller.IsRunning)
            {
                _pressureValue.Text =
                    "Scale invalid";
            }

            UpdateStartButtonState();
        }
    }

    private void PreviewControllerOnUpdated(
        object? sender,
        PreviewPressureUpdate update)
    {
        if (IsDisposed ||
            !IsHandleCreated ||
            _controller.IsRunning ||
            !_previewScaleValid)
        {
            return;
        }

        BeginInvoke(
            new Action(
                () =>
                {
                    if (!_controller.IsRunning &&
                        _previewScaleValid &&
                        !_closing)
                    {
                        _pressureValue.Text =
                            $"{update.LatestPressurePa:N1} Pa";
                    }
                }));
    }

    private void PreviewControllerOnFaulted(
        object? sender,
        PreviewFaultedEventArgs e)
    {
        if (IsDisposed ||
            !IsHandleCreated ||
            _closing)
        {
            return;
        }

        BeginInvoke(
            new Action(
                () =>
                {
                    _pressureValue.Text =
                        "-- Pa";

                    _daqStatusValue.Text =
                        "Disconnected / Error";

                    _daqStatusValue.ForeColor =
                        Color.Firebrick;

                    _statusLabel.Text =
                        "Live DAQ monitoring stopped. Use Detect / Refresh DAQ to reconnect.";

                    UpdateStartButtonState();

                    ShowError(
                        e.Exception.Message);
                }));
    }

    // ============================================================
    // Browse output directory
    // ============================================================

    private void BrowseForDirectory()
    {
        using var dialog =
            new FolderBrowserDialog
            {
                Description =
                    "Select the folder for measurement CSV files",

                UseDescriptionForTitle =
                    true,

                SelectedPath =
                    Directory.Exists(
                        _directoryText.Text)
                        ? _directoryText.Text
                        : string.Empty,

                ShowNewFolderButton =
                    true
            };

        if (dialog.ShowDialog(this) ==
            DialogResult.OK)
        {
            _directoryText.Text =
                dialog.SelectedPath;
        }
    }

    // ============================================================
    // Start measurement
    // ============================================================

    private async Task StartMeasurementAsync()
    {
        MeasurementSettings settings;

        try
        {
            settings =
                BuildSettingsFromUi();

            settings.Validate();

            string path =
                settings.GetOutputPath();

            // Safety:
            // never overwrite an existing file.
            if (File.Exists(path))
            {
                ShowError(
                    "The output file already exists. " +
                    "Measurement was not started.\n\n" +
                    path);

                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(
                ex.Message);

            return;
        }

        _plot.ClearData();

        _elapsedLabel.Text =
            "Elapsed: 0 s";

        _lastDisplayedSecond =
            -1;

        _lastMeasurementUiUpdateMs =
            0;

        SetMeasurementStartingUiState();

        try
        {
            _statusLabel.Text =
                "Stopping live preview and starting measurement...";

            await _previewController.StopAsync();

            _pressureValue.Text =
                "-- Pa";

            _daqStatusValue.Text =
                "Connected / Starting measurement";

            _daqStatusValue.ForeColor =
                Color.DarkGoldenrod;

            await _controller.StartAsync(
                settings);

            SetMeasurementUiState(
                running: true);

            _daqStatusValue.Text =
                "Connected / Measuring";

            _daqStatusValue.ForeColor =
                Color.DarkGreen;

            _statusLabel.Text =
                settings.StopMode ==
                MeasurementStopMode.Timer

                    ? string.Concat(
                        "Measuring | Timer: ",
                        settings.TimerDurationSeconds.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),
                        " s")

                    : "Measuring | Manual stop mode";
        }
        catch (DllNotFoundException)
        {
            SetMeasurementUiState(
                running: false);

            SetDaqUnavailable(
                "NI-DAQmx runtime not found.");

            ShowError(
                "NI-DAQmx runtime was not found. " +
                "Install NI-DAQmx, then restart this application.");
        }
        catch (Exception ex)
        {
            SetMeasurementUiState(
                running: false);

            _statusLabel.Text =
                "Measurement did not start.";

            ShowError(
                ex.Message);

            await StartPreviewForSelectedChannelAsync(
                updateMainStatus: false);
        }
    }

    // ============================================================
    // Stop measurement
    // ============================================================

    private async Task StopMeasurementAsync()
    {
        if (!_controller.IsRunning)
        {
            return;
        }

        _stopButton.Enabled =
            false;

        _statusLabel.Text =
            "Stopping measurement and closing CSV file...";

        try
        {
            await _controller.StopAsync();
        }
        catch (Exception ex)
        {
            ShowError(
                ex.Message);
        }
    }

    // ============================================================
    // Build settings
    // ============================================================

    private MeasurementSettings BuildSettingsFromUi()
    {
        return new MeasurementSettings
        {
            PhysicalChannel =
                _channelCombo.SelectedItem as string
                ?? string.Empty,

            OutputDirectory =
                _directoryText.Text,

            FileName =
                _fileNameText.Text,

            Scale =
                BuildScaleFromUi(),

            StopMode =
                _timerRadio.Checked

                    ? MeasurementStopMode.Timer

                    : MeasurementStopMode.Manual,

            TimerDurationSeconds =
                (double)_timerSeconds.Value
        };
    }

    private PressureScale BuildScaleFromUi()
    {
        return new PressureScale(
            (double)_voltageLow.Value,
            (double)_voltageHigh.Value,
            (double)_pressureLow.Value,
            (double)_pressureHigh.Value);
    }

    // ============================================================
    // Measurement UI updates
    // ============================================================

    private void ControllerOnUpdated(
        object? sender,
        MeasurementUpdate update)
    {
        if (IsDisposed ||
            !IsHandleCreated ||
            _closing)
        {
            return;
        }

        /*
         * DAQ acquisition remains 1000 Hz.
         *
         * UI update is intentionally throttled to about 20 Hz
         * to prevent WinForms rendering from affecting acquisition.
         */
        long nowMs =
            Environment.TickCount64;

        if (_lastMeasurementUiUpdateMs != 0 &&
            nowMs - _lastMeasurementUiUpdateMs < 50)
        {
            return;
        }

        _lastMeasurementUiUpdateMs =
            nowMs;

        BeginInvoke(
            new Action(
                () =>
                {
                    _pressureValue.Text =
                        $"{update.LatestPressurePa:N1} Pa";

                    _plot.AddPoint(
                        update.ElapsedSeconds,
                        update.LatestPressurePa);

                    int wholeSeconds =
                        (int)Math.Floor(
                            update.ElapsedSeconds +
                            1e-9);

                    if (wholeSeconds !=
                        _lastDisplayedSecond)
                    {
                        _lastDisplayedSecond =
                            wholeSeconds;

                        _elapsedLabel.Text =
                            $"Elapsed: {wholeSeconds} s";
                    }
                }));
    }

    private void ControllerOnCompleted(
        object? sender,
        MeasurementCompletedEventArgs e)
    {
        if (IsDisposed ||
            !IsHandleCreated ||
            _closing)
        {
            return;
        }

        BeginInvoke(
            new Action(
                () =>
                    _ =
                        HandleMeasurementCompletedOnUiAsync(
                            e)));
    }

    private async Task
        HandleMeasurementCompletedOnUiAsync(
            MeasurementCompletedEventArgs e)
    {
        SetMeasurementUiState(
            running: false);

        int wholeSeconds =
            (int)Math.Floor(
                e.ElapsedSeconds +
                1e-9);

        _elapsedLabel.Text =
            $"Elapsed: {wholeSeconds} s";

        string completionMessage;

        if (e.Reason ==
            MeasurementStopReason.Faulted)
        {
            completionMessage =
                $"Measurement stopped by error. " +
                $"Partial CSV preserved: {e.OutputPath}";

            _statusLabel.Text =
                completionMessage;

            ShowError(
                e.Exception?.Message
                ?? "Unknown measurement error.");
        }
        else
        {
            string reason =
                e.Reason ==
                MeasurementStopReason.TimerCompleted

                    ? "Timer completed"

                    : "Measurement stopped";

            completionMessage =
                $"{reason} | CSV saved: {e.OutputPath}";

            _statusLabel.Text =
                completionMessage;
        }

        if (_closing)
        {
            return;
        }

        // Resume live pressure monitoring automatically.
        bool previewStarted =
            await StartPreviewForSelectedChannelAsync(
                updateMainStatus: false);

        if (previewStarted &&
            !_closing &&
            !IsDisposed)
        {
            _statusLabel.Text =
                completionMessage +
                " | Live monitor resumed";
        }
    }

    // ============================================================
    // UI state
    // ============================================================

    private void SetMeasurementStartingUiState()
    {
        _measurementTransitionActive =
            true;

        SetEditableControlsEnabled(
            false);

        _startButton.Enabled =
            false;

        _stopButton.Enabled =
            false;
    }

    private void SetMeasurementUiState(
        bool running)
    {
        _measurementTransitionActive =
            false;

        SetEditableControlsEnabled(
            !running);

        _stopButton.Enabled =
            running;

        UpdateStartButtonState();
    }

    private void SetEditableControlsEnabled(
        bool enabled)
    {
        _channelCombo.Enabled =
            enabled &&
            _channelCombo.Items.Count > 0;

        _refreshChannelsButton.Enabled =
            enabled;

        _voltageLow.Enabled =
            enabled;

        _voltageHigh.Enabled =
            enabled;

        _pressureLow.Enabled =
            enabled;

        _pressureHigh.Enabled =
            enabled;

        _directoryText.Enabled =
            enabled;

        _fileNameText.Enabled =
            enabled;

        _browseButton.Enabled =
            enabled;

        _manualRadio.Enabled =
            enabled;

        _timerRadio.Enabled =
            enabled;

        _timerSeconds.Enabled =
            enabled &&
            _timerRadio.Checked;
    }

    private void UpdateStartButtonState()
    {
        _startButton.Enabled =
            !_controller.IsRunning &&
            !_measurementTransitionActive &&
            !_closing &&
            _previewScaleValid &&
            _previewController.IsRunning &&
            _channelCombo.SelectedIndex >= 0;
    }

    private void SetDaqUnavailable(
        string status)
    {
        _pressureValue.Text =
            "-- Pa";

        _daqStatusValue.Text =
            status;

        _daqStatusValue.ForeColor =
            Color.Firebrick;

        _statusLabel.Text =
            status;

        UpdateStartButtonState();
    }

    // ============================================================
    // Closing
    // ============================================================

    private async void MainForm_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (!_controller.IsRunning)
        {
            _closing =
                true;

            return;
        }

        e.Cancel =
            true;

        DialogResult result =
            MessageBox.Show(
                this,
                "A measurement is running. " +
                "Stop the measurement, save the CSV file, " +
                "and close the application?",

                "Measurement in progress",

                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

        if (result !=
            DialogResult.Yes)
        {
            return;
        }

        _closing =
            true;

        Enabled =
            false;

        try
        {
            await _controller.StopAsync();

            _allowClose =
                true;

            Close();
        }
        finally
        {
            if (!_allowClose)
            {
                _closing =
                    false;

                Enabled =
                    true;
            }
        }
    }

    // ============================================================
    // UI helper functions
    // ============================================================

    private static GroupBox MakeGroup(
        string text)
    {
        return new GroupBox
        {
            Text =
                text,

            Dock =
                DockStyle.Top,

            AutoSize =
                true,

            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,

            // Compact spacing for laptop screens.
            Margin =
                new Padding(
                    0,
                    0,
                    0,
                    3),

            Padding =
                new Padding(
                    7,
                    3,
                    7,
                    4)
        };
    }

    private static TableLayoutPanel
        MakeTwoColumnTable()
    {
        var table =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,

                AutoSize =
                    true,

                AutoSizeMode =
                    AutoSizeMode.GrowAndShrink,

                ColumnCount =
                    2,

                RowCount =
                    0,

                GrowStyle =
                    TableLayoutPanelGrowStyle.AddRows,

                Margin =
                    new Padding(0),

                Padding =
                    new Padding(1)
            };

        // Slightly more space for the value/input side.
        table.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                43F));

        table.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                57F));

        return table;
    }

    private static void AddRow(
        TableLayoutPanel table,
        Control left,
        Control right)
    {
        int row =
            table.RowCount++;

        table.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        // Reduced vertical margins are the main reason
        // the complete left panel fits on laptop displays.
        left.Margin =
            new Padding(
                3,
                1,
                6,
                1);

        right.Margin =
            new Padding(
                3,
                1,
                3,
                1);

        table.Controls.Add(
            left,
            0,
            row);

        table.Controls.Add(
            right,
            1,
            row);
    }

    private static void AddSpanRow(
        TableLayoutPanel table,
        Control control)
    {
        int row =
            table.RowCount++;

        table.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        control.Margin =
            new Padding(
                3,
                1,
                3,
                1);

        table.Controls.Add(
            control,
            0,
            row);

        table.SetColumnSpan(
            control,
            2);
    }

    private static Label MakeLabel(
        string text)
    {
        return new Label
        {
            Text =
                text,

            AutoSize =
                false,

            Dock =
                DockStyle.Fill,

            MinimumSize =
                new Size(
                    0,
                    24),

            TextAlign =
                ContentAlignment.MiddleLeft
        };
    }

    private static Label MakeValueLabel(
        string text)
    {
        return new Label
        {
            Text =
                text,

            AutoSize =
                false,

            Dock =
                DockStyle.Fill,

            MinimumSize =
                new Size(
                    0,
                    24),

            TextAlign =
                ContentAlignment.MiddleLeft
        };
    }

    private static void ConfigureNumber(
        NumericUpDown control,
        decimal minimum,
        decimal maximum,
        int decimalPlaces,
        decimal value,
        decimal increment)
    {
        control.Minimum =
            minimum;

        control.Maximum =
            maximum;

        control.DecimalPlaces =
            decimalPlaces;

        control.Value =
            value;

        control.Increment =
            increment;

        control.ThousandsSeparator =
            true;

        control.Dock =
            DockStyle.Fill;

        control.MinimumSize =
            new Size(
                80,
                0);
    }

    // ============================================================
    // Error message
    // ============================================================

    private void ShowError(
        string message)
    {
        if (_closing ||
            IsDisposed)
        {
            return;
        }

        MessageBox.Show(
            this,
            message,
            "Digital Manometer Pressure Monitor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
