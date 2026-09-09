namespace GlobalHotKey;

internal sealed class EditHotkeyForm : Form
{
    const string Prompt = "Press a key combination...";

    readonly TextBox _shortcut = new() { ReadOnly = true, Dock = DockStyle.Fill, Text = Prompt, TextAlign = HorizontalAlignment.Center };
    readonly ComboBox _action = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox _target = new() { Dock = DockStyle.Fill };
    readonly Button _browse = new() { Text = "...", Dock = DockStyle.Fill };
    readonly TextBox _arguments = new() { Dock = DockStyle.Fill };
    readonly Func<Hotkey, string?> _checkAvailable;

    HotkeyModifiers _modifiers;
    Keys _key;

    public Hotkey Result { get; private set; } = null!;

    ActionType SelectedAction => (ActionType)_action.SelectedIndex;

    public EditHotkeyForm(Hotkey? existing, Func<Hotkey, string?> checkAvailable)
    {
        _checkAvailable = checkAvailable;

        Text = existing is null ? "New hotkey" : "Edit hotkey";
        Icon = AppIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ClientSize = new Size(430, 260);

        _action.Items.AddRange(["Program", "Folder", "URL"]);
        _action.SelectedIndex = (int)(existing?.Action ?? ActionType.Program);
        _action.SelectedIndexChanged += (_, _) => SyncActionFields();
        _browse.Click += (_, _) => Browse();

        if (existing is not null)
        {
            _modifiers = existing.Modifiers;
            _key = existing.Key;
            _shortcut.Text = existing.DisplayText;
            _target.Text = existing.Target;
            _arguments.Text = existing.Arguments ?? "";
        }

        // No "&" anywhere: a mnemonic would swallow the Alt combinations the recorder needs to see.
        var ok = new Button { Text = "OK", Width = 88, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Cancel", Width = 88, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Confirm();
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);

        var hint = new Label
        {
            Text = "If nothing appears when you press the keys, the combination is already taken by "
                 + "another program. Combinations using Win are usually reserved by Windows.",
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0)
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 6, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        foreach (int height in new[] { 30, 30, 30, 30 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        AddRow(layout, 0, "Shortcut", _shortcut);
        AddRow(layout, 1, "Action", _action);
        AddRow(layout, 2, "Target", _target, _browse);
        AddRow(layout, 3, "Arguments", _arguments);
        layout.Controls.Add(hint, 0, 4);
        layout.SetColumnSpan(hint, 3);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 3);

        Controls.Add(layout);
        SyncActionFields();
        ActiveControl = _shortcut;
    }

    static void AddRow(TableLayoutPanel layout, int row, string caption, Control field, Control? extra = null)
    {
        layout.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false }, 0, row);
        layout.Controls.Add(field, 1, row);
        if (extra is not null) layout.Controls.Add(extra, 2, row);
        else layout.SetColumnSpan(field, 2);
    }

    /// <summary>
    /// Recording happens here rather than in KeyDown so that Alt combinations and other keys the dialog
    /// would otherwise consume still reach the recorder. Plain Tab and Escape are left alone so the
    /// keyboard can still navigate and close the dialog.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_shortcut.Focused) return base.ProcessCmdKey(ref msg, keyData);

        Keys key = keyData & Keys.KeyCode;
        var modifiers = HotkeyModifiers.None;
        if ((keyData & Keys.Control) != 0) modifiers |= HotkeyModifiers.Control;
        if ((keyData & Keys.Alt) != 0) modifiers |= HotkeyModifiers.Alt;
        if ((keyData & Keys.Shift) != 0) modifiers |= HotkeyModifiers.Shift;
        if (NativeMethods.IsWinKeyDown()) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None && key is Keys.Tab or Keys.Escape)
            return base.ProcessCmdKey(ref msg, keyData);

        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            // Still waiting for the real key: show the modifiers held so far.
            _shortcut.Text = Hotkey.ModifierText(modifiers) is { Length: > 0 } held ? $"{held} + ..." : Prompt;
            return true;
        }

        _modifiers = modifiers;
        _key = key;
        _shortcut.Text = Hotkey.Describe(modifiers, key);
        return true;
    }

    void SyncActionFields()
    {
        bool isProgram = SelectedAction == ActionType.Program;
        _arguments.Enabled = isProgram;
        _browse.Visible = SelectedAction != ActionType.Url;
        if (!isProgram) _arguments.Clear();
    }

    void Browse()
    {
        if (SelectedAction == ActionType.Program)
        {
            using var dialog = new OpenFileDialog { Filter = "Programs (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) _target.Text = dialog.FileName;
        }
        else
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK) _target.Text = dialog.SelectedPath;
        }
    }

    void Confirm()
    {
        string? arguments = SelectedAction == ActionType.Program && _arguments.Text.Trim() is { Length: > 0 } text ? text : null;
        string target = _target.Text.Trim();
        if (SelectedAction == ActionType.Url) target = Hotkey.NormalizeUrl(target);

        var candidate = new Hotkey(_modifiers, _key, SelectedAction, target, arguments);

        if (candidate.Validate() is { } invalid) { Warn(invalid); return; }
        if (MissingTarget(candidate) is { } missing) { Warn(missing); return; }
        if (_checkAvailable(candidate) is { } unavailable) { Warn(unavailable); return; }

        Result = candidate;
        DialogResult = DialogResult.OK;
    }

    static string? MissingTarget(Hotkey hotkey) => hotkey.Action switch
    {
        ActionType.Program when !File.Exists(hotkey.Target) => "That program does not exist.",
        ActionType.Folder when !Directory.Exists(hotkey.Target) => "That folder does not exist.",
        _ => null
    };

    void Warn(string message) =>
        MessageBox.Show(this, message, "GlobalHotKey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
