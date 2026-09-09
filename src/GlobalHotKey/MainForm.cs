using System.Security;

namespace GlobalHotKey;

internal sealed class MainForm : Form
{
    readonly HotkeyStore _store = new(HotkeyStore.DefaultPath);
    readonly HotkeyManager _manager = new();
    readonly StartupRegistration _startup = new(Environment.ProcessPath ?? Application.ExecutablePath);
    readonly NotifyIcon _tray = new();
    readonly ListView _list = new();
    readonly ToolStripMenuItem _startupItem = new("Start with Windows") { CheckOnClick = true };
    readonly List<Hotkey> _hotkeys = [];

    public MainForm()
    {
        Text = "GlobalHotKey";
        Icon = AppIcon.Load();
        ClientSize = new Size(580, 300);
        MinimumSize = new Size(460, 260);
        StartPosition = FormStartPosition.CenterScreen;

        _list.View = View.Details;
        _list.Dock = DockStyle.Fill;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add("Shortcut", 140);
        _list.Columns.Add("Action", 70);
        _list.Columns.Add("Target", 250);
        _list.Columns.Add("Status", 100);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

        var add = new Button { Text = "Add", Width = 88 };
        var edit = new Button { Text = "Edit", Width = 88 };
        var remove = new Button { Text = "Remove", Width = 88 };
        add.Click += (_, _) => AddNew();
        edit.Click += (_, _) => EditSelected();
        remove.Click += (_, _) => RemoveSelected();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 46, Padding = new Padding(8) };
        buttons.Controls.AddRange([remove, edit, add]);

        Controls.Add(_list);
        Controls.Add(buttons);

        _tray.Icon = AppIcon.Load(SystemInformation.SmallIconSize);
        _tray.Text = "GlobalHotKey";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowWindow();

        _startupItem.Click += (_, _) => ToggleStartup();
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            new ToolStripMenuItem("Open", null, (_, _) => ShowWindow()),
            new ToolStripMenuItem("Open hotkeys folder", null, (_, _) => OpenHotkeysFolder()),
            _startupItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitApp())
        ]);
        menu.Opening += (_, _) => _startupItem.Checked = StartupEnabled();
        _tray.ContextMenuStrip = menu;

        _manager.Pressed += OnHotkeyPressed;

        var (loaded, warning) = _store.Load();
        _hotkeys.AddRange(loaded);
        Apply(save: false);
        if (warning is not null) Notify(warning, ToolTipIcon.Warning);
        RefreshStartupPath();
    }

    void AddNew()
    {
        if (Edit(null) is { } hotkey)
        {
            _hotkeys.Add(hotkey);
            Apply(save: true);
        }
    }

    void EditSelected()
    {
        int index = SelectedIndex();
        if (index < 0) return;

        if (Edit(_hotkeys[index]) is { } hotkey)
        {
            _hotkeys[index] = hotkey;
            Apply(save: true);
        }
    }

    void RemoveSelected()
    {
        int index = SelectedIndex();
        if (index < 0) return;

        var confirm = MessageBox.Show(this, $"Remove {_hotkeys[index].DisplayText}?", "GlobalHotKey",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _hotkeys.RemoveAt(index);
        Apply(save: true);
    }

    Hotkey? Edit(Hotkey? existing)
    {
        // Our own registrations would eat the key presses the recorder is trying to read.
        _manager.UnregisterAll();
        try
        {
            using var dialog = new EditHotkeyForm(existing, candidate => CheckAvailable(candidate, existing));
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Result : null;
        }
        finally
        {
            _manager.RegisterAll(_hotkeys);
        }
    }

    /// <summary>Runs while the dialog is open and nothing of ours is registered, so the probe is free to test it.</summary>
    string? CheckAvailable(Hotkey candidate, Hotkey? existing)
    {
        if (_hotkeys.Any(h => h != existing && h.Modifiers == candidate.Modifiers && h.Key == candidate.Key))
            return "A hotkey with this combination already exists.";

        if (_manager.TryRegister(candidate) is { } error) return error;
        _manager.UnregisterAll();
        return null;
    }

    void Apply(bool save)
    {
        if (save)
        {
            try
            {
                _store.Save(_hotkeys);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the in-memory list: the user should not lose what they just entered.
                MessageBox.Show(this, $"Could not save {_store.FilePath}: {ex.Message}", "GlobalHotKey",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        var errors = _manager.RegisterAll(_hotkeys);
        RefreshList();
        if (errors.Count > 0) Notify(string.Join(Environment.NewLine, errors), ToolTipIcon.Warning);
    }

    void RefreshList()
    {
        var live = _manager.Registered.Values.ToHashSet();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var hotkey in _hotkeys)
            _list.Items.Add(new ListViewItem([
                hotkey.DisplayText,
                hotkey.Action.ToString(),
                hotkey.Target,
                live.Contains(hotkey) ? "OK" : "Not registered"
            ]));
        _list.EndUpdate();
    }

    int SelectedIndex() => _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;

    void OnHotkeyPressed(Hotkey hotkey)
    {
        if (ActionRunner.Run(hotkey) is { } error) Notify(error, ToolTipIcon.Error);
    }

    // A balloon rather than a dialog: the user is working in another app and should keep their focus.
    void Notify(string message, ToolTipIcon icon) => _tray.ShowBalloonTip(3000, "GlobalHotKey", message, icon);

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    void OpenHotkeysFolder()
    {
        string folder = Path.GetDirectoryName(_store.FilePath)!;
        try
        {
            // Nothing has been saved yet on a first run, so the folder may not exist.
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify($"Could not open {folder}: {ex.Message}", ToolTipIcon.Error);
            return;
        }

        if (ActionRunner.Open(folder) is { } error) Notify(error, ToolTipIcon.Error);
    }

    void ToggleStartup()
    {
        try
        {
            if (_startupItem.Checked) _startup.Enable();
            else _startup.Disable();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            _startupItem.Checked = !_startupItem.Checked;
            MessageBox.Show(this, $"Could not change the Windows startup setting: {ex.Message}", "GlobalHotKey",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    bool StartupEnabled()
    {
        try
        {
            return _startup.IsEnabled;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Best effort: a stale startup path is worth fixing, but not worth blocking startup over.</summary>
    void RefreshStartupPath()
    {
        try
        {
            _startup.RefreshPath();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
        }
    }

    void ExitApp()
    {
        _tray.Visible = false;   // otherwise the icon lingers in the tray until the mouse passes over it
        _manager.Dispose();
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window hides it; the hotkeys stay live. Exit is on the tray menu.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _manager.Dispose();
        }
        base.Dispose(disposing);
    }
}
