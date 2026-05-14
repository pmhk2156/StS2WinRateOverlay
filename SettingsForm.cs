using System.Globalization;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace StS2WinRateOverlay;

internal sealed class SettingsForm : Form
{
    private static readonly Regex BuildVersionPattern = new(@"^[0-9]+(\.[0-9]+){1,3}([-.][A-Za-z0-9]+)?$", RegexOptions.Compiled);

    private readonly Label _historyStatusLabel = new();
    private readonly Button _browseButton = new();
    private readonly Label _historyError = new();
    private readonly ComboBox _ascensionCombo = new();
    private readonly ComboBox _characterCombo = new();
    private readonly CheckBox _buildCheck = new();
    private readonly TextBox _buildText = new();
    private readonly Label _buildError = new();
    private readonly CheckBox _timeCheck = new();
    private readonly TableLayoutPanel _timePanel = new();
    private readonly CheckBox _useFromCheck = new();
    private readonly CheckBox _useToCheck = new();
    private readonly DateTimePicker _fromPicker = new();
    private readonly DateTimePicker _toPicker = new();
    private readonly Label _timeError = new();
    private readonly CheckBox _allModesCheck = new();
    private readonly Button _applyButton = new();
    private readonly Button _cancelButton = new();
    private readonly string _currentHistoryDirectory;
    private readonly RunFilter _currentFilter;
    private string _historyDirectory;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedHistoryDirectory { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RunFilter SelectedFilter { get; private set; }

    public SettingsForm(string currentHistoryDirectory, RunFilter currentFilter)
    {
        _currentHistoryDirectory = currentHistoryDirectory;
        _currentFilter = currentFilter;
        _historyDirectory = currentHistoryDirectory;
        SelectedHistoryDirectory = currentHistoryDirectory;
        SelectedFilter = currentFilter;

        InitializeComponent();
        LoadCurrentFilter();
        ValidateInputs();
    }

    private void InitializeComponent()
    {
        Text = "StS2 Win Rate Filters";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 360);
        AutoScroll = false;

        var scrollPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
        };
        Controls.Add(scrollPanel);

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 11,
            Padding = new Padding(14),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        for (var i = 0; i < root.RowCount; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        scrollPanel.Controls.Add(root);

        AddLabel(root, "Directory", 0);
        var historyPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Height = 24,
        };
        historyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        historyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        _historyStatusLabel.AutoSize = false;
        _historyStatusLabel.Dock = DockStyle.Fill;
        _historyStatusLabel.Margin = new Padding(0, 0, 4, 0);
        _historyStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _browseButton.Text = "...";
        _browseButton.Dock = DockStyle.Top;
        _browseButton.Margin = new Padding(0);
        _browseButton.Size = new Size(28, 24);
        _browseButton.Click += (_, _) => BrowseHistoryFolder();
        historyPanel.Controls.Add(_historyStatusLabel, 0, 0);
        historyPanel.Controls.Add(_browseButton, 1, 0);
        root.Controls.Add(historyPanel, 1, 0);
        _historyError.Text = "!";
        _historyError.ForeColor = Color.Red;
        _historyError.TextAlign = ContentAlignment.MiddleCenter;
        root.Controls.Add(_historyError, 2, 0);

        AddLabel(root, "Ascension", 1);
        _ascensionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _ascensionCombo.Items.Add("All");
        for (var i = 0; i <= 10; i++)
        {
            _ascensionCombo.Items.Add(i.ToString(CultureInfo.InvariantCulture));
        }
        AddControl(root, _ascensionCombo, 1);

        AddLabel(root, "Character", 2);
        _characterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _characterCombo.Items.AddRange(["All", "IRONCLAD", "SILENT", "REGENT", "NECROBINDER", "DEFECT"]);
        AddControl(root, _characterCombo, 2);

        _buildCheck.Text = "App version";
        _buildCheck.CheckedChanged += (_, _) => UpdateOptionalVisibility();
        root.Controls.Add(_buildCheck, 0, 3);
        root.SetColumnSpan(_buildCheck, 2);

        _buildText.PlaceholderText = "0.105.1";
        _buildText.TextChanged += (_, _) => ValidateInputs();
        AddControl(root, _buildText, 4);
        _buildError.Text = "!";
        _buildError.ForeColor = Color.Red;
        _buildError.TextAlign = ContentAlignment.MiddleCenter;
        root.Controls.Add(_buildError, 2, 4);

        _timeCheck.Text = "Date range";
        _timeCheck.CheckedChanged += (_, _) => UpdateOptionalVisibility();
        root.Controls.Add(_timeCheck, 0, 6);
        root.SetColumnSpan(_timeCheck, 2);

        _timePanel.ColumnCount = 4;
        _timePanel.Dock = DockStyle.Fill;
        _timePanel.Margin = new Padding(0);
        _timePanel.Height = 26;
        _timePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        _timePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _timePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        _timePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _useFromCheck.Text = "Start";
        _useFromCheck.AutoSize = true;
        _useFromCheck.Margin = new Padding(0, 3, 4, 0);
        _useFromCheck.CheckedChanged += (_, _) => UpdateOptionalVisibility();
        _useToCheck.Text = "End";
        _useToCheck.AutoSize = true;
        _useToCheck.Margin = new Padding(8, 3, 4, 0);
        _useToCheck.CheckedChanged += (_, _) => UpdateOptionalVisibility();

        ConfigurePicker(_fromPicker);
        ConfigurePicker(_toPicker);
        _fromPicker.ValueChanged += (_, _) => ValidateInputs();
        _toPicker.ValueChanged += (_, _) => ValidateInputs();

        _timePanel.Controls.Add(_useFromCheck, 0, 0);
        _timePanel.Controls.Add(_fromPicker, 1, 0);
        _timePanel.Controls.Add(_useToCheck, 2, 0);
        _timePanel.Controls.Add(_toPicker, 3, 0);
        root.Controls.Add(_timePanel, 1, 7);

        _timeError.Text = "!";
        _timeError.ForeColor = Color.Red;
        _timeError.TextAlign = ContentAlignment.MiddleCenter;
        root.Controls.Add(_timeError, 2, 7);

        _allModesCheck.Text = "Include all game modes. If unchecked, only Singleplayer-Standard runs are counted.";
        _allModesCheck.AutoSize = true;
        _allModesCheck.MaximumSize = new Size(460, 0);
        _allModesCheck.Margin = new Padding(3, 4, 3, 4);
        root.Controls.Add(_allModesCheck, 0, 9);
        root.SetColumnSpan(_allModesCheck, 3);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        _applyButton.Text = "Apply";
        _applyButton.Click += (_, _) => Apply();
        _cancelButton.Text = "Cancel";
        _cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(_applyButton);
        buttonPanel.Controls.Add(_cancelButton);
        root.Controls.Add(buttonPanel, 0, 10);
        root.SetColumnSpan(buttonPanel, 3);

        _ascensionCombo.SelectedIndexChanged += (_, _) => ValidateInputs();
        _characterCombo.SelectedIndexChanged += (_, _) => ValidateInputs();
        _allModesCheck.CheckedChanged += (_, _) => ValidateInputs();
    }

    private static void AddLabel(TableLayoutPanel root, string text, int row)
    {
        root.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
    }

    private static void AddControl(TableLayoutPanel root, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        root.Controls.Add(control, 1, row);
    }

    private static void ConfigurePicker(DateTimePicker picker)
    {
        picker.CustomFormat = "yyyy/MM/dd HH:mm:ss";
        picker.Format = DateTimePickerFormat.Custom;
        picker.ShowUpDown = true;
        picker.Dock = DockStyle.Fill;
    }

    private void LoadCurrentFilter()
    {
        UpdateHistoryStatus();
        _ascensionCombo.SelectedItem = _currentFilter.Ascension?.ToString(CultureInfo.InvariantCulture) ?? "All";

        _characterCombo.SelectedItem = string.IsNullOrWhiteSpace(_currentFilter.Character)
            ? "All"
            : _currentFilter.Character.Split('.').LastOrDefault() ?? "All";

        _buildCheck.Checked = !string.IsNullOrWhiteSpace(_currentFilter.BuildId);
        _buildText.Text = _currentFilter.BuildId?.TrimStart('v', 'V') ?? string.Empty;

        var hasTimeFilter = _currentFilter.StartTimeFrom != DateTime.MinValue || _currentFilter.StartTimeTo != DateTime.MaxValue;
        _timeCheck.Checked = hasTimeFilter;
        _useFromCheck.Checked = _currentFilter.StartTimeFrom != DateTime.MinValue;
        _useToCheck.Checked = _currentFilter.StartTimeTo != DateTime.MaxValue;
        _fromPicker.Value = _useFromCheck.Checked ? _currentFilter.StartTimeFrom : DateTime.Today;
        _toPicker.Value = _useToCheck.Checked ? _currentFilter.StartTimeTo : DateTime.Now;

        _allModesCheck.Checked = !_currentFilter.StandardOnly;
        UpdateOptionalVisibility();
    }

    private void UpdateOptionalVisibility()
    {
        _buildText.Visible = _buildCheck.Checked;
        _buildError.Visible = _buildCheck.Checked && _buildError.Tag is true;

        _timePanel.Visible = _timeCheck.Checked;
        _useFromCheck.Visible = _timeCheck.Checked;
        _useToCheck.Visible = _timeCheck.Checked;
        _fromPicker.Visible = _timeCheck.Checked;
        _toPicker.Visible = _timeCheck.Checked;
        _fromPicker.Enabled = _timeCheck.Checked && _useFromCheck.Checked;
        _toPicker.Enabled = _timeCheck.Checked && _useToCheck.Checked;
        _timeError.Visible = _timeCheck.Checked && _timeError.Tag is true;

        ValidateInputs();
    }

    private bool ValidateInputs()
    {
        var historyValid = ValidateHistory();
        var buildValid = ValidateBuild(out _);
        var timeValid = ValidateTime(out _, out _);

        _historyError.Visible = !historyValid;
        _buildError.Tag = !buildValid;
        _timeError.Tag = !timeValid;
        _buildError.Visible = _buildCheck.Checked && !buildValid;
        _timeError.Visible = _timeCheck.Checked && !timeValid;
        _applyButton.Enabled = historyValid && buildValid && timeValid;
        return historyValid && buildValid && timeValid;
    }

    private bool ValidateHistory()
    {
        var path = _historyDirectory.Trim();
        if (path.Length == 0)
        {
            return true;
        }

        return Directory.Exists(path);
    }

    private bool ValidateBuild(out string? buildId)
    {
        buildId = null;
        if (!_buildCheck.Checked)
        {
            return true;
        }

        var text = _buildText.Text.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (text.StartsWith('v') || text.StartsWith('V') || !BuildVersionPattern.IsMatch(text))
        {
            return false;
        }

        buildId = "v" + text;
        return true;
    }

    private bool ValidateTime(out DateTime from, out DateTime to)
    {
        from = DateTime.MinValue;
        to = DateTime.MaxValue;

        if (!_timeCheck.Checked)
        {
            return true;
        }

        from = _useFromCheck.Checked ? _fromPicker.Value : DateTime.MinValue;
        to = _useToCheck.Checked ? _toPicker.Value : DateTime.MaxValue;

        return from <= to;
    }

    private void Apply()
    {
        if (!ValidateInputs())
        {
            return;
        }

        ValidateBuild(out var buildId);
        ValidateTime(out var from, out var to);

        int? ascension = _ascensionCombo.SelectedItem?.ToString() == "All"
            ? null
            : int.Parse(_ascensionCombo.SelectedItem?.ToString() ?? "10", CultureInfo.InvariantCulture);

        var character = _characterCombo.SelectedItem?.ToString() == "All"
            ? null
            : "CHARACTER." + _characterCombo.SelectedItem;

        SelectedHistoryDirectory = _historyDirectory;
        SelectedFilter = new RunFilter(
            ascension,
            character,
            buildId,
            from,
            to,
            !_allModesCheck.Checked);

        DialogResult = DialogResult.OK;
    }

    private void BrowseHistoryFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Slay the Spire 2 history folder",
            InitialDirectory = Directory.Exists(_historyDirectory) ? _historyDirectory : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _historyDirectory = dialog.SelectedPath;
        UpdateHistoryStatus();
        ValidateInputs();
    }

    private void UpdateHistoryStatus()
    {
        if (string.IsNullOrWhiteSpace(_historyDirectory))
        {
            _historyStatusLabel.Text = "No folder selected";
            _historyStatusLabel.ForeColor = SystemColors.GrayText;
            return;
        }

        _historyStatusLabel.Text = Directory.Exists(_historyDirectory) ? "Folder selected" : "Selected folder not found";
        _historyStatusLabel.ForeColor = Directory.Exists(_historyDirectory) ? SystemColors.ControlText : Color.Red;
    }
}
