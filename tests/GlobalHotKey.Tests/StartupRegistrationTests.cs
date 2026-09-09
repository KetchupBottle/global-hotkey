using Microsoft.Win32;

namespace GlobalHotKey.Tests;

[Trait("Category", "Integration")]
public class StartupRegistrationTests : IDisposable
{
    const string Root = @"Software\GlobalHotKey.Tests";
    const string ExePath = @"C:\fake\GlobalHotKey.exe";

    // A throwaway key under HKCU. The real Run key is never opened, let alone written to.
    readonly string _keyPath = $@"{Root}\{Guid.NewGuid():N}";

    StartupRegistration Registration(string exePath = ExePath) => new(exePath, _keyPath);

    object? StoredValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue("GlobalHotKey");
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

        bool rootIsEmpty;
        using (var root = Registry.CurrentUser.OpenSubKey(Root))
            rootIsEmpty = root is { SubKeyCount: 0, ValueCount: 0 };
        if (rootIsEmpty) Registry.CurrentUser.DeleteSubKey(Root, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Off_when_the_value_is_not_there() => Assert.False(Registration().IsEnabled);

    [Fact]
    public void Enabling_stores_the_quoted_path_with_the_tray_switch()
    {
        Registration().Enable();

        Assert.Equal($"\"{ExePath}\" --tray", StoredValue());
        Assert.True(Registration().IsEnabled);
    }

    [Fact]
    public void Disabling_removes_the_value_and_repeats_harmlessly()
    {
        var registration = Registration();
        registration.Enable();

        registration.Disable();
        Assert.False(registration.IsEnabled);
        Assert.Null(StoredValue());

        registration.Disable();
    }

    [Fact]
    public void RefreshPath_follows_a_moved_executable()
    {
        Registration().Enable();

        Registration(@"D:\moved\GlobalHotKey.exe").RefreshPath();

        Assert.Equal(@"""D:\moved\GlobalHotKey.exe"" --tray", StoredValue());
    }

    [Fact]
    public void RefreshPath_does_not_switch_startup_on()
    {
        Registration().RefreshPath();

        Assert.False(Registration().IsEnabled);
        Assert.Null(StoredValue());
    }
}
