using System.Diagnostics;
using System.Text.RegularExpressions;

namespace L2TerrorWaveGui;

internal sealed class MainForm : Form
{
    private readonly TextBox _romPath = new();
    private readonly Label _romStatus = new();
    private readonly Label _engineStatus = new();
    private readonly ComboBox _mode = new();
    private readonly Label _modeDescription = new();
    private readonly TextBox _seed = new();
    private readonly Dictionary<char, CheckBox> _flagBoxes = [];
    private readonly TrackBar _randomness = new();
    private readonly Label _randomnessValue = new();
    private readonly TrackBar _difficulty = new();
    private readonly Label _difficultyValue = new();
    private readonly ComboBox _scaling = new();
    private readonly NumericUpDown _bossScaling = new();
    private readonly NumericUpDown _nonBossScaling = new();
    private readonly Label _bossScalingLabel = new();
    private readonly Label _nonBossScalingLabel = new();
    private readonly TextBox _customSeedPath = new();
    private readonly Button _customSeedBrowse = new();
    private readonly Label _customSeedLabel = new();
    private readonly CheckBox _airship = new() { Text = "Start with airship" };
    private readonly CheckBox _bossy = new() { Text = "Very random bosses" };
    private readonly CheckBox _monsterMash = new() { Text = "Monster Mash dungeons" };
    private readonly CheckBox _aggressive = new() { Text = "Extremely aggressive enemies" };
    private readonly CheckBox _anywhere = new() { Text = "Random equipment slots" };
    private readonly CheckBox _easyMode = new() { Text = "One-hit enemies" };
    private readonly CheckBox _holiday = new() { Text = "Enemies run away" };
    private readonly CheckBox _noCapsuleMaster = new() { Text = "Disable capsule tagging" };
    private readonly RichTextBox _log = new();
    private readonly Button _runButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openOutputButton = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _runStatus = new();
    private readonly RandomizerRunner _runner = new();
    private CancellationTokenSource? _runCancellation;
    private string? _lastOutputPath;
    private string? _lastOutputDirectory;
    private int _romInspectionVersion;

    public MainForm()
    {
        Text = "Lufia II · Terror Wave Randomizer";
        MinimumSize = new Size(980, 720);
        Size = new Size(1180, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(BuildMainLayout());
        Controls.Add(BuildHeader());

        PopulateDefaults();
        WireEvents();
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Theme.Navy, Padding = new Padding(28, 15, 20, 10) };
        var title = new Label
        {
            AutoSize = true,
            Text = "TERROR WAVE",
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(25, 12)
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Lufia II randomizer control room  ·  v0.3 experimental",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(190, 204, 224),
            Location = new Point(29, 53)
        };
        var version = new LinkLabel
        {
            AutoSize = true,
            Text = "Terror Wave 3.16 by Abyssonym · embedded",
            LinkColor = Theme.Gold,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Theme.Gold,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(header.Width - 175, 35)
        };
        version.LinkClicked += (_, _) => ShowAbout();
        header.Resize += (_, _) => version.Left = header.ClientSize.Width - version.Width - 28;
        header.Controls.AddRange([title, subtitle, version]);
        return header;
    }

    private Control BuildMainLayout()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Canvas
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 0, 8, 0)
        };
        left.SizeChanged += (_, _) => ResizeFlowChildren(left);
        left.Controls.Add(BuildFilesCard());
        left.Controls.Add(BuildModeCard());
        left.Controls.Add(BuildRandomizationCard());

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8, 0, 0, 0)
        };
        right.SizeChanged += (_, _) => ResizeFlowChildren(right);
        right.Controls.Add(BuildTuningCard());
        right.Controls.Add(BuildSpecialOptionsCard());
        right.Controls.Add(BuildLogCard());

        shell.Controls.Add(left, 0, 0);
        shell.Controls.Add(right, 1, 0);
        shell.Controls.Add(BuildActionBar(), 0, 1);
        shell.SetColumnSpan(shell.GetControlFromPosition(0, 1)!, 2);
        return shell;
    }

    private Control BuildFilesCard()
    {
        var body = NewCard("FILES", 174);
        var grid = NewGrid(3);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        AddPathRow(grid, 0, "Source ROM", _romPath, "Browse…", BrowseRom);
        _romStatus.Text = "Choose a ROM to verify its MD5 hash.";
        _romStatus.ForeColor = Theme.Muted;
        _romStatus.AutoEllipsis = true;
        _romStatus.Dock = DockStyle.Fill;
        _romStatus.TextAlign = ContentAlignment.MiddleLeft;
        grid.Controls.Add(_romStatus, 1, 1);
        grid.SetColumnSpan(_romStatus, 2);
        _engineStatus.Text = EmbeddedRandomizer.IsEmbedded
            ? $"Embedded Terror Wave {EmbeddedRandomizer.Version} · verified and extracted on demand"
            : "Embedded randomizer engine is missing";
        _engineStatus.ForeColor = EmbeddedRandomizer.IsEmbedded ? Theme.Success : Theme.Danger;
        _engineStatus.Dock = DockStyle.Fill;
        _engineStatus.TextAlign = ContentAlignment.MiddleLeft;
        grid.Controls.Add(NewFieldLabel("Engine"), 0, 2);
        grid.Controls.Add(_engineStatus, 1, 2);
        grid.SetColumnSpan(_engineStatus, 2);
        body.Controls.Add(grid);
        return body;
    }

    private Control BuildModeCard()
    {
        var body = NewCard("GAME MODE", 154);
        var grid = NewGrid(3);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Dock = DockStyle.Fill;
        grid.Controls.Add(NewFieldLabel("Mode"), 0, 0);
        grid.Controls.Add(_mode, 1, 0);
        grid.SetColumnSpan(_mode, 2);

        _modeDescription.Dock = DockStyle.Fill;
        _modeDescription.ForeColor = Theme.Muted;
        _modeDescription.AutoEllipsis = true;
        grid.Controls.Add(_modeDescription, 1, 1);
        grid.SetColumnSpan(_modeDescription, 2);

        _seed.Dock = DockStyle.Fill;
        _seed.PlaceholderText = "Blank = generated automatically";
        grid.Controls.Add(NewFieldLabel("Seed / folder"), 0, 2);
        grid.Controls.Add(_seed, 1, 2);
        var seedButton = NewSecondaryButton("New seed");
        seedButton.Click += (_, _) => _seed.Text = GenerateSeed();
        grid.Controls.Add(seedButton, 2, 2);
        body.Controls.Add(grid);
        return body;
    }

    private Control BuildRandomizationCard()
    {
        var body = NewCard("RANDOMIZE", 224);
        var controls = new Panel { Dock = DockStyle.Fill };
        var flags = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 128,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(4, 2, 4, 0)
        };
        flags.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        flags.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 4; row++) flags.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        for (var index = 0; index < RandomizerFlags.All.Count; index++)
        {
            var option = RandomizerFlags.All[index];
            var box = new CheckBox { Text = option.Label, Dock = DockStyle.Fill, Checked = true };
            _flagBoxes[option.Flag] = box;
            flags.Controls.Add(box, index / 4, index % 4);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.LeftToRight };
        var all = NewSecondaryButton("Select all");
        var none = NewSecondaryButton("Clear");
        all.Click += (_, _) => SetAllFlags(true);
        none.Click += (_, _) => SetAllFlags(false);
        buttons.Controls.AddRange([all, none]);
        controls.Controls.Add(flags);
        controls.Controls.Add(buttons);
        body.Controls.Add(controls);
        return body;
    }

    private Control BuildTuningCard()
    {
        var body = NewCard("TUNING", 292);
        var grid = NewGrid(7);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));

        ConfigureTrackBar(_randomness, 0, 100, 50, 5);
        ConfigureTrackBar(_difficulty, 25, 300, 100, 25);
        AddSliderRow(grid, 0, "Randomness", _randomness, _randomnessValue);
        AddSliderRow(grid, 1, "Difficulty", _difficulty, _difficultyValue);

        _scaling.DropDownStyle = ComboBoxStyle.DropDownList;
        _scaling.Dock = DockStyle.Fill;
        grid.Controls.Add(NewFieldLabel("Enemy scaling"), 0, 2);
        grid.Controls.Add(_scaling, 1, 2);
        grid.SetColumnSpan(_scaling, 2);

        ConfigureDecimal(_bossScaling, 0.75m);
        ConfigureDecimal(_nonBossScaling, 0.75m);
        _bossScalingLabel.Text = "Boss scale";
        _nonBossScalingLabel.Text = "Nonboss scale";
        grid.Controls.Add(StyleFieldLabel(_bossScalingLabel), 0, 3);
        grid.Controls.Add(_bossScaling, 1, 3);
        grid.SetColumnSpan(_bossScaling, 2);
        grid.Controls.Add(StyleFieldLabel(_nonBossScalingLabel), 0, 4);
        grid.Controls.Add(_nonBossScaling, 1, 4);
        grid.SetColumnSpan(_nonBossScaling, 2);

        _customSeedLabel.Text = "Custom world";
        _customSeedPath.Dock = DockStyle.Fill;
        _customSeedBrowse.Text = "Browse…";
        _customSeedBrowse.Dock = DockStyle.Fill;
        _customSeedBrowse.FlatStyle = FlatStyle.Flat;
        _customSeedBrowse.Click += (_, _) => BrowseCustomSeed();
        grid.Controls.Add(StyleFieldLabel(_customSeedLabel), 0, 5);
        grid.Controls.Add(_customSeedPath, 1, 5);
        grid.Controls.Add(_customSeedBrowse, 2, 5);

        var hint = new Label
        {
            Text = "Terror Wave recommends 0.50 randomness for a first run.",
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        grid.Controls.Add(hint, 1, 6);
        grid.SetColumnSpan(hint, 2);
        body.Controls.Add(grid);
        return body;
    }

    private Control BuildSpecialOptionsCard()
    {
        var body = NewCard("SPECIAL OPTIONS", 176);
        var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 4; row++) options.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var boxes = new[] { _airship, _bossy, _monsterMash, _aggressive, _anywhere, _easyMode, _holiday, _noCapsuleMaster };
        for (var index = 0; index < boxes.Length; index++)
        {
            boxes[index].Dock = DockStyle.Fill;
            options.Controls.Add(boxes[index], index / 4, index % 4);
        }
        body.Controls.Add(options);
        return body;
    }

    private Control BuildLogCard()
    {
        var body = NewCard("RUN LOG", 264);
        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.BackColor = Color.FromArgb(25, 31, 42);
        _log.ForeColor = Color.FromArgb(215, 224, 235);
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Cascadia Mono", 8.5F);
        _log.Text = "Ready. Select a supported ROM and choose your options.\n";
        body.Controls.Add(_log);
        return body;
    }

    private Control BuildActionBar()
    {
        var bar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(18, 13, 18, 10), Margin = new Padding(0, 12, 0, 0) };
        _runButton.Text = "RANDOMIZE ROM";
        _runButton.BackColor = Theme.Gold;
        _runButton.ForeColor = Theme.Navy;
        _runButton.FlatStyle = FlatStyle.Flat;
        _runButton.FlatAppearance.BorderSize = 0;
        _runButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _runButton.Size = new Size(172, 43);
        _runButton.Dock = DockStyle.Right;

        _cancelButton.Text = "Cancel";
        _cancelButton.Size = new Size(86, 43);
        _cancelButton.Dock = DockStyle.Right;
        _cancelButton.Enabled = false;
        _cancelButton.FlatStyle = FlatStyle.Flat;

        _openOutputButton.Text = "Open seed folder";
        _openOutputButton.Size = new Size(132, 43);
        _openOutputButton.Dock = DockStyle.Right;
        _openOutputButton.Enabled = false;
        _openOutputButton.FlatStyle = FlatStyle.Flat;

        var statusArea = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 2, 18, 0) };
        _runStatus.Text = "Ready";
        _runStatus.Dock = DockStyle.Top;
        _runStatus.Height = 22;
        _runStatus.ForeColor = Theme.Muted;
        _progress.Dock = DockStyle.Top;
        _progress.Height = 6;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.Visible = false;
        statusArea.Controls.Add(_progress);
        statusArea.Controls.Add(_runStatus);

        bar.Controls.Add(statusArea);
        bar.Controls.Add(_openOutputButton);
        bar.Controls.Add(_cancelButton);
        bar.Controls.Add(_runButton);
        return bar;
    }

    private void PopulateDefaults()
    {
        _mode.Items.AddRange(["Standard adventure", "Open World", "Four Keys", "Custom Open World", "Vanilla / Fixxxer only"]);
        _mode.SelectedIndex = 0;
        _scaling.Items.AddRange(["Automatic (recommended)", "Force scaling", "No scaling", "Split boss / nonboss"]);
        _scaling.SelectedIndex = 0;
        UpdateSliderLabels();
        UpdateModeControls();
        UpdateScalingControls();
    }

    private void WireEvents()
    {
        _romPath.TextChanged += async (_, _) => await InspectRomAsync();
        _mode.SelectedIndexChanged += (_, _) => UpdateModeControls();
        _scaling.SelectedIndexChanged += (_, _) => UpdateScalingControls();
        _randomness.ValueChanged += (_, _) => UpdateSliderLabels();
        _difficulty.ValueChanged += (_, _) => UpdateSliderLabels();
        _runButton.Click += async (_, _) => await RunRandomizerAsync();
        _cancelButton.Click += (_, _) =>
        {
            _runCancellation?.Cancel();
            _runner.Cancel();
        };
        _openOutputButton.Click += (_, _) => OpenOutputFolder();
        FormClosing += (_, _) => _runner.Cancel();
    }

    private async Task InspectRomAsync()
    {
        var version = ++_romInspectionVersion;
        var path = _romPath.Text.Trim();
        if (!File.Exists(path))
        {
            _romStatus.Text = "Choose a ROM to verify its MD5 hash.";
            _romStatus.ForeColor = Theme.Muted;
            return;
        }

        _romStatus.Text = "Checking ROM…";
        _romStatus.ForeColor = Theme.Muted;
        try
        {
            var result = await RomInspector.InspectAsync(path);
            if (version != _romInspectionVersion) return;
            _romStatus.Text = $"{result.Description}  ·  MD5 {result.Md5}";
            _romStatus.ForeColor = result.IsSupported ? Theme.Success : Theme.Danger;
        }
        catch (Exception exception)
        {
            if (version != _romInspectionVersion) return;
            _romStatus.Text = $"Could not read ROM: {exception.Message}";
            _romStatus.ForeColor = Theme.Danger;
        }
    }

    private async Task RunRandomizerAsync()
    {
        if (string.IsNullOrWhiteSpace(_seed.Text)) _seed.Text = GenerateSeed();
        if (!TryCreateOptions(out var options, out var error))
        {
            MessageBox.Show(this, error, "Cannot start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _runCancellation = new CancellationTokenSource();
        SetRunning(true);
        _lastOutputPath = null;
        _lastOutputDirectory = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(options!.RomPath))!,
            options.Seed);
        _openOutputButton.Enabled = false;
        _log.Clear();
        AppendLog("Starting Terror Wave…");
        AppendLog($"Mode: {_mode.SelectedItem}");

        try
        {
            var result = await _runner.RunAsync(options, AppendLog, _runCancellation.Token);
            _lastOutputPath = result.OutputRomPath;
            _lastOutputDirectory = result.SeedDirectory;
            if (result.Succeeded)
            {
                _runStatus.Text = $"Seed {options.Seed} folder created";
                _runStatus.ForeColor = Theme.Success;
                AppendLog($"Finished successfully. Results: {result.SeedDirectory}");
                _openOutputButton.Enabled = Directory.Exists(GetOutputDirectory());
            }
            else
            {
                _runStatus.Text = $"Terror Wave failed (exit code {result.ExitCode})";
                _runStatus.ForeColor = Theme.Danger;
                AppendLog("No valid randomized ROM was produced. Check randomizer.log in the seed folder.");
                _openOutputButton.Enabled = Directory.Exists(GetOutputDirectory());
            }
        }
        catch (OperationCanceledException)
        {
            _runStatus.Text = "Cancelled";
            _runStatus.ForeColor = Theme.Muted;
            AppendLog("Run cancelled.");
            _openOutputButton.Enabled = Directory.Exists(GetOutputDirectory());
        }
        catch (Exception exception)
        {
            _runStatus.Text = "Could not run Terror Wave";
            _runStatus.ForeColor = Theme.Danger;
            AppendLog(exception.ToString());
            _openOutputButton.Enabled = Directory.Exists(GetOutputDirectory());
            MessageBox.Show(this, exception.Message, "Randomizer error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetRunning(false);
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }

    private bool TryCreateOptions(out RandomizerOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        var rom = _romPath.Text.Trim();
        if (!File.Exists(rom)) error = "Select a Lufia II ROM file first.";
        else if (!EmbeddedRandomizer.IsEmbedded) error = "This build does not contain the Terror Wave engine.";
        else if (!long.TryParse(_seed.Text, out var seed) || seed < 0 || seed >= 10_000_000_000) error = "The seed must be a whole number from 0 to 9,999,999,999.";
        else if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rom))!, _seed.Text.Trim()))) error = "A folder for this seed already exists beside the source ROM. Choose another seed or move that folder first.";
        else if (SelectedMode != GameMode.Vanilla && !_flagBoxes.Values.Any(box => box.Checked)) error = "Choose at least one randomization category.";
        else if (SelectedMode == GameMode.CustomOpenWorld && !File.Exists(_customSeedPath.Text.Trim())) error = "Choose a custom Open World seed file.";
        if (error.Length > 0) return false;

        options = new RandomizerOptions
        {
            RomPath = rom,
            Mode = SelectedMode,
            Flags = _flagBoxes.Where(pair => pair.Value.Checked).Select(pair => pair.Key).ToArray(),
            Seed = _seed.Text.Trim(),
            Randomness = _randomness.Value / 100m,
            Difficulty = _difficulty.Value / 100m,
            Scaling = SelectedScaling,
            BossScaling = _bossScaling.Value,
            NonBossScaling = _nonBossScaling.Value,
            CustomSeedPath = _customSeedPath.Text.Trim(),
            StartWithAirship = _airship.Checked,
            VeryRandomBosses = _bossy.Checked,
            MonsterMash = _monsterMash.Checked,
            AggressiveEnemies = _aggressive.Checked,
            EquipmentAnywhere = _anywhere.Checked,
            EasyMode = _easyMode.Checked,
            EnemiesRunAway = _holiday.Checked,
            NoCapsuleMaster = _noCapsuleMaster.Checked
        };
        return true;
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }

        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
        var match = Regex.Match(line, @"^Output filename:\s*(.+)$", RegexOptions.IgnoreCase);
        if (match.Success) _lastOutputPath = match.Groups[1].Value.Trim();
    }

    private void UpdateModeControls()
    {
        var mode = SelectedMode;
        _modeDescription.Text = mode switch
        {
            GameMode.Standard => "Original story progression with your selected categories randomized.",
            GameMode.OpenWorld => "Nonlinear world with randomized rewards and progression.",
            GameMode.FourKeys => "Open World variant condensed to four key progression items.",
            GameMode.CustomOpenWorld => "Open World using a Terror Wave custom seed template.",
            _ => "Apply the Fixxxer patch without randomizing game data."
        };

        var isOpenWorld = mode is GameMode.OpenWorld or GameMode.FourKeys or GameMode.CustomOpenWorld;
        var isVanilla = mode == GameMode.Vanilla;
        _airship.Enabled = isOpenWorld;
        if (!isOpenWorld) _airship.Checked = false;
        _bossy.Enabled = isOpenWorld;
        _noCapsuleMaster.Enabled = isOpenWorld;
        _scaling.Enabled = isOpenWorld;
        _bossScaling.Enabled = isOpenWorld && SelectedScaling == ScalingMode.SplitScaling;
        _nonBossScaling.Enabled = isOpenWorld && SelectedScaling == ScalingMode.SplitScaling;
        _bossScalingLabel.Enabled = _bossScaling.Enabled;
        _nonBossScalingLabel.Enabled = _nonBossScaling.Enabled;
        _randomness.Enabled = !isVanilla;
        _difficulty.Enabled = !isVanilla;
        _monsterMash.Enabled = !isVanilla;
        _aggressive.Enabled = !isVanilla;
        _anywhere.Enabled = !isVanilla;
        _easyMode.Enabled = !isVanilla;
        _holiday.Enabled = !isVanilla;
        foreach (var box in _flagBoxes.Values) box.Enabled = !isVanilla;
        _customSeedLabel.Enabled = mode == GameMode.CustomOpenWorld;
        _customSeedPath.Enabled = mode == GameMode.CustomOpenWorld;
        _customSeedBrowse.Enabled = mode == GameMode.CustomOpenWorld;
    }

    private void UpdateScalingControls()
    {
        var enabled = SelectedMode is GameMode.OpenWorld or GameMode.FourKeys or GameMode.CustomOpenWorld
            && SelectedScaling == ScalingMode.SplitScaling;
        _bossScaling.Enabled = enabled;
        _nonBossScaling.Enabled = enabled;
        _bossScalingLabel.Enabled = enabled;
        _nonBossScalingLabel.Enabled = enabled;
    }

    private void UpdateSliderLabels()
    {
        _randomnessValue.Text = (_randomness.Value / 100m).ToString("0.00");
        _difficultyValue.Text = $"{_difficulty.Value / 100m:0.00}×";
    }

    private void SetRunning(bool running)
    {
        _runButton.Enabled = !running;
        _cancelButton.Enabled = running;
        _progress.Visible = running;
        if (running)
        {
            _runStatus.Text = "Randomizing… this can take a little while";
            _runStatus.ForeColor = Theme.Gold;
        }
    }

    private void SetAllFlags(bool value)
    {
        foreach (var box in _flagBoxes.Values) box.Checked = value;
    }

    private void BrowseRom()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select your Lufia II ROM",
            Filter = "SNES ROM (*.sfc;*.smc)|*.sfc;*.smc|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _romPath.Text = dialog.FileName;
    }

    private void BrowseCustomSeed()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a Terror Wave custom seed",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _customSeedPath.Text = dialog.FileName;
    }

    private void OpenOutputFolder()
    {
        var directory = GetOutputDirectory();
        if (directory is null || !Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private void ShowAbout()
    {
        const string message =
            "Lufia II Terror Wave GUI 0.3 experimental preview\n\n" +
            "Embeds the unmodified Terror Wave 3.16 engine by Abyssonym.\n" +
            "Engine SHA-256: 769b041d1fad796b…\n\n" +
            "The upstream snapshot has no top-level license file; its randomtools dependency includes GPL-3.0. " +
            "Confirm redistribution terms before releasing this bundle.\n\n" +
            "Open the upstream project page?";
        var result = MessageBox.Show(this, message, "About and third-party notice", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (result == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo("https://github.com/abyssonym/terrorwave") { UseShellExecute = true });
        }
    }

    private string? GetOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_lastOutputDirectory)) return _lastOutputDirectory;
        if (!string.IsNullOrWhiteSpace(_lastOutputPath))
        {
            var output = _lastOutputPath;
            if (!Path.IsPathRooted(output)) output = Path.Combine(Path.GetDirectoryName(_romPath.Text.Trim())!, output);
            return Path.GetDirectoryName(output);
        }
        return Path.GetDirectoryName(_romPath.Text.Trim());
    }

    private static string GenerateSeed() => Random.Shared.NextInt64(0, 10_000_000_000).ToString();

    private GameMode SelectedMode => (GameMode)Math.Max(0, _mode.SelectedIndex);
    private ScalingMode SelectedScaling => (ScalingMode)Math.Max(0, _scaling.SelectedIndex);

    private static Panel NewCard(string title, int height)
    {
        var card = new Panel { Height = height, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(18, 39, 18, 14) };
        var caption = new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = Theme.NavyLight,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Location = new Point(18, 14)
        };
        var accent = new Panel { BackColor = Theme.Gold, Height = 3, Width = 28, Location = new Point(18, 33) };
        card.Controls.Add(caption);
        card.Controls.Add(accent);
        return card;
    }

    private static TableLayoutPanel NewGrid(int rows)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = rows, Padding = Padding.Empty };
        for (var row = 0; row < rows; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
        return grid;
    }

    private static Label NewFieldLabel(string text) => StyleFieldLabel(new Label { Text = text });

    private static Label StyleFieldLabel(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.ForeColor = Theme.Text;
        return label;
    }

    private static Button NewSecondaryButton(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(88, 29),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Theme.NavyLight,
        Margin = new Padding(0, 3, 8, 3)
    };

    private static void AddPathRow(TableLayoutPanel grid, int row, string label, TextBox textBox, string buttonText, Action onClick)
    {
        textBox.Dock = DockStyle.Fill;
        var button = NewSecondaryButton(buttonText);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(4, 2, 0, 2);
        button.Click += (_, _) => onClick();
        grid.Controls.Add(NewFieldLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(button, 2, row);
    }

    private static void AddSliderRow(TableLayoutPanel grid, int row, string label, TrackBar slider, Label value)
    {
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleRight;
        value.ForeColor = Theme.NavyLight;
        value.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grid.Controls.Add(NewFieldLabel(label), 0, row);
        grid.Controls.Add(slider, 1, row);
        grid.Controls.Add(value, 2, row);
    }

    private static void ConfigureTrackBar(TrackBar trackBar, int minimum, int maximum, int value, int tickFrequency)
    {
        trackBar.Minimum = minimum;
        trackBar.Maximum = maximum;
        trackBar.Value = value;
        trackBar.TickFrequency = tickFrequency;
        trackBar.Dock = DockStyle.Fill;
        trackBar.AutoSize = false;
        trackBar.Height = 34;
    }

    private static void ConfigureDecimal(NumericUpDown control, decimal value)
    {
        control.Minimum = 0;
        control.Maximum = 10;
        control.DecimalPlaces = 2;
        control.Increment = 0.05m;
        control.Value = value;
        control.Dock = DockStyle.Fill;
    }

    private static void ResizeFlowChildren(FlowLayoutPanel panel)
    {
        var width = Math.Max(300, panel.ClientSize.Width - panel.Padding.Horizontal - (panel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        foreach (Control child in panel.Controls) child.Width = width;
    }
}
