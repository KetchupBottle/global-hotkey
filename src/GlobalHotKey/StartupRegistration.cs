using Microsoft.Win32;

namespace GlobalHotKey;

/// <summary>
/// The "Start with Windows" toggle, backed by the per-user Run key. HKCU needs no elevation.
/// The run key path is injectable so tests can point at a throwaway key.
/// </summary>
internal sealed class StartupRegistration(
    string exePath,
    string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    const string ValueName = "GlobalHotKey";

    RegistryKey OpenKey() => Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true)
        ?? throw new InvalidOperationException($@"Could not open HKCU\{runKeyPath}.");

    public bool IsEnabled
    {
        get
        {
            using var key = OpenKey();
            return key.GetValue(ValueName) is not null;
        }
    }

    public void Enable()
    {
        using var key = OpenKey();
        key.SetValue(ValueName, $"\"{exePath}\" --tray");
    }

    public void Disable()
    {
        using var key = OpenKey();
        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// The app is portable, so the user can move the exe and leave a stale path in the registry.
    /// Rewriting the value on every start fixes it, because the app can only run from its new location.
    /// </summary>
    public void RefreshPath()
    {
        if (IsEnabled) Enable();
    }
}
