namespace GlobalHotKey;

static class Program
{
    // Local\ scopes the mutex to the logon session, which matches how hotkeys work: one per desktop.
    const string InstanceMutexName = @"Local\GlobalHotKey-7B3E2C1A-5D4F-4A6B-9C8D-0E1F2A3B4C5D";

    [STAThread]
    static void Main(string[] args)
    {
        DropInheritedElectronVariables();

        using var instance = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isOnlyInstance);
        ApplicationConfiguration.Initialize();

        if (!isOnlyInstance)
        {
            MessageBox.Show("GlobalHotKey is already running. Look for its icon in the notification area.",
                "GlobalHotKey", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new MainForm();
        if (!args.Contains("--tray")) form.Show();

        // Run without a main form: closing the window only hides it, and Exit on the tray menu ends the app.
        Application.Run();
    }

    /// <summary>
    /// A launcher must not hand its own quirks to the programs it opens. Starting this app from a
    /// terminal inside an Electron editor leaves ELECTRON_RUN_AS_NODE=1 and a set of VSCODE_ variables
    /// in the environment. Every child inherits them, so launching any Electron program from here would
    /// run it headless as Node: no window, no error, nothing at all. Dropping them costs nothing,
    /// because they only ever describe the process that set them.
    /// </summary>
    internal static void DropInheritedElectronVariables()
    {
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);

        foreach (string name in Environment.GetEnvironmentVariables().Keys.OfType<string>()
                     .Where(name => name.StartsWith("VSCODE_", StringComparison.OrdinalIgnoreCase)))
            Environment.SetEnvironmentVariable(name, null);
    }
}
