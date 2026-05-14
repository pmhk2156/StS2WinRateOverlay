using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace StS2WinRateOverlay;

internal sealed class OverlayForm : Form
{
    private const int WsExAppWindow = 0x00040000;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly OverlayOptions _options;
    private readonly Label _label = new();
    private readonly IconButton _settingsButton = new();
    private readonly IconButton _exitButton = new();
    private readonly ToolTip _toolTip = new();
    private readonly Icon _appIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _topMostTimer = new();
    private string _historyDirectory;
    private RunFilter _filter;
    private NotifyIcon? _notifyIcon;
    private FileSystemWatcher? _watcher;
    private volatile bool _refreshQueued;
    private bool _settingsOpen;

    public OverlayForm(OverlayOptions options)
    {
        _options = options;
        _appIcon = LoadAppIcon();
        _historyDirectory = options.HistoryDirectory;
        _filter = options.InitialFilter;
        InitializeOverlay();
        InitializeTrayIcon();
        InitializeRefresh();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExAppWindow;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyInitialLocation();
        EnsureTopMost();
        RefreshStats();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _topMostTimer.Stop();
        _watcher?.Dispose();
        _notifyIcon?.Dispose();
        _toolTip.Dispose();
        _appIcon.Dispose();
        SaveCurrentSettings();
        base.OnClosing(e);
    }

    private void InitializeOverlay()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(18, 18, 18);
        ClientSize = new Size(420, 104);
        FormBorderStyle = FormBorderStyle.None;
        Text = "StS2 Win Rate Overlay";
        Icon = _appIcon;
        Opacity = 0.82;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        _label.AutoSize = false;
        _label.Dock = DockStyle.Fill;
        _label.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Point);
        _label.ForeColor = Color.White;
        _label.Padding = new Padding(14, 8, 72, 8);
        _label.TextAlign = ContentAlignment.MiddleLeft;
        _label.UseCompatibleTextRendering = true;
        _label.MouseDown += StartOverlayDrag;
        Controls.Add(_label);
        MouseDown += StartOverlayDrag;

        ConfigureOverlayButton(_settingsButton, LoadEmbeddedImage("settings.png"), Width - 62, OpenSettings);
        ConfigureOverlayButton(_exitButton, LoadEmbeddedImage("close.png"), Width - 32, Close);
        _toolTip.SetToolTip(_settingsButton, "Open filters");
        _toolTip.SetToolTip(_exitButton, "Exit");
    }

    private void ConfigureOverlayButton(IconButton button, Image? image, int left, Action action)
    {
        button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        button.BackColor = Color.FromArgb(42, 42, 42);
        button.BorderColor = Color.FromArgb(90, 90, 90);
        button.Location = new Point(left, 8);
        button.Size = new Size(26, 26);
        button.Margin = Padding.Empty;
        button.Padding = Padding.Empty;
        button.TabStop = false;
        button.IconImage = image;
        button.Click += (_, _) => action();
        Controls.Add(button);
        button.BringToFront();
    }

    private static Image? LoadEmbeddedImage(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : Image.FromStream(stream);
    }

    private static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
        if (File.Exists(path))
        {
            return new Icon(path);
        }

        return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    }

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Filters...", null, (_, _) => OpenSettings());
        menu.Items.Add("Refresh", null, (_, _) => RefreshStats());
        menu.Items.Add("Exit", null, (_, _) => Close());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _appIcon,
            Text = "StS2 Win Rate Overlay",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private void InitializeRefresh()
    {
        _refreshTimer.Interval = 1000;
        _refreshTimer.Tick += (_, _) =>
        {
            if (!_refreshQueued)
            {
                return;
            }

            _refreshQueued = false;
            RefreshStats();
        };
        _refreshTimer.Start();

        _topMostTimer.Interval = 3000;
        _topMostTimer.Tick += (_, _) =>
        {
            if (!_settingsOpen)
            {
                EnsureTopMost();
            }
        };
        _topMostTimer.Start();

        ResetWatcher();
    }

    private void QueueRefresh()
    {
        _refreshQueued = true;
    }

    private void RefreshStats()
    {
        if (string.IsNullOrWhiteSpace(_historyDirectory) || !Directory.Exists(_historyDirectory))
        {
            _label.Text = "Set history folder";
            return;
        }

        var stats = RunHistoryReader.Read(_historyDirectory, _filter);

        if (stats.Errors < 0)
        {
            _label.Text = "Set history folder";
            return;
        }

        var rate = stats.Total == 0 ? "0.0%" : stats.WinRate.ToString("P1");
        _label.Text = $"{_filter.AscensionLabel} {_filter.CharacterLabel}  {_filter.GameModeLabel}\r\n{stats.Wins}/{stats.Total} wins  {rate}\r\nWin streak: {stats.CurrentWinStreak}";

        if (stats.Errors > 0)
        {
            _label.Text += $"  ({stats.Errors} skipped)";
        }
    }

    private void OpenSettings()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        _topMostTimer.Stop();

        try
        {
            using var form = new SettingsForm(_historyDirectory, _filter);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _historyDirectory = form.SelectedHistoryDirectory;
            _filter = form.SelectedFilter;
            SaveCurrentSettings();
            ResetWatcher();
            RefreshStats();
        }
        finally
        {
            _settingsOpen = false;
            _topMostTimer.Start();
            EnsureTopMost();
        }
    }

    private void ApplyInitialLocation()
    {
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        if (_options.OverlayX.HasValue && _options.OverlayY.HasValue)
        {
            Location = ClampToScreen(new Point(_options.OverlayX.Value, _options.OverlayY.Value), screen);
            return;
        }

        Location = new Point(screen.Left + 12, screen.Top + (screen.Height / 6));
    }

    private void EnsureTopMost()
    {
        SetWindowPos(Handle, HwndTopmost, Left, Top, Width, Height, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private Point ClampToScreen(Point point, Rectangle screen)
    {
        var x = Math.Clamp(point.X, screen.Left, Math.Max(screen.Left, screen.Right - Width));
        var y = Math.Clamp(point.Y, screen.Top, Math.Max(screen.Top, screen.Bottom - Height));
        return new Point(x, y);
    }

    private void StartOverlayDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
        SaveCurrentSettings();
    }

    private void SaveCurrentSettings()
    {
        AppSettingsStore.Save(new AppSettings(_historyDirectory, _filter, Left, Top));
    }

    private void ResetWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;

        if (!Directory.Exists(_historyDirectory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(_historyDirectory, "*.run")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Created += (_, _) => QueueRefresh();
        _watcher.Changed += (_, _) => QueueRefresh();
        _watcher.Deleted += (_, _) => QueueRefresh();
        _watcher.Renamed += (_, _) => QueueRefresh();
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

internal sealed class IconButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(90, 90, 90);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? IconImage { get; set; }

    public IconButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Text = string.Empty;
        UseVisualStyleBackColor = false;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        if (IconImage is null)
        {
            return;
        }

        var x = (Width - IconImage.Width) / 2;
        var y = (Height - IconImage.Height) / 2;
        graphics.DrawImageUnscaled(IconImage, x, y);
    }
}
