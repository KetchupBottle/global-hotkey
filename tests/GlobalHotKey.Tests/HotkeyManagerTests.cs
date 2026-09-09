using System.Runtime.InteropServices;

namespace GlobalHotKey.Tests;

[Trait("Category", "Integration")]
public class HotkeyManagerTests
{
    // F22 and F23 with every modifier held: no physical keyboard sends them and nothing else claims
    // them, so a leaked press lands harmlessly in whatever window has focus.
    static Hotkey Probe(Keys key) => new(
        HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, key,
        ActionType.Program, @"C:\Windows\System32\rundll32.exe", null);

    const uint KEYEVENTF_KEYUP = 2;
    const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [Fact]
    public void Registering_then_unregistering_leaves_nothing_behind() => MessageThread.Run(() =>
    {
        using var manager = new HotkeyManager();

        Assert.Null(manager.TryRegister(Probe(Keys.F22)));
        Assert.Single(manager.Registered);

        manager.UnregisterAll();
        Assert.Empty(manager.Registered);
    });

    [Fact]
    public void A_combination_already_taken_is_reported_not_thrown() => MessageThread.Run(() =>
    {
        using var manager = new HotkeyManager();
        Assert.Null(manager.TryRegister(Probe(Keys.F22)));

        // Windows answers with ERROR_HOTKEY_ALREADY_REGISTERED whoever holds it, so the second
        // registration reproduces the "another program already has this" case without needing one.
        string? error = manager.TryRegister(Probe(Keys.F22));

        Assert.NotNull(error);
        Assert.Contains("already in use", error);
    });

    [Fact]
    public void RegisterAll_reports_the_failures_and_keeps_the_rest() => MessageThread.Run(() =>
    {
        using var manager = new HotkeyManager();

        var errors = manager.RegisterAll([Probe(Keys.F22), Probe(Keys.F22), Probe(Keys.F23)]);

        Assert.Single(errors);
        Assert.Equal(2, manager.Registered.Count);
    });

    [Fact]
    public void The_message_id_picks_the_right_hotkey() => MessageThread.Run(() =>
    {
        using var manager = new HotkeyManager();
        var first = Probe(Keys.F22);
        var second = Probe(Keys.F23);
        Assert.Null(manager.TryRegister(first));
        Assert.Null(manager.TryRegister(second));

        Hotkey? fired = null;
        manager.Pressed += hotkey => fired = hotkey;

        int id = manager.Registered.First(entry => entry.Value == second).Key;
        Assert.True(PostMessage(manager.Handle, WM_HOTKEY, id, IntPtr.Zero));

        Assert.True(MessageThread.PumpUntil(() => fired is not null, TimeSpan.FromSeconds(2)));
        Assert.Equal(second, fired);
    });

    [Fact]
    public void A_real_key_press_fires_the_hotkey() => MessageThread.Run(() =>
    {
        using var manager = new HotkeyManager();
        var hotkey = Probe(Keys.F23);
        Assert.Null(manager.TryRegister(hotkey));

        Hotkey? fired = null;
        manager.Pressed += pressed => fired = pressed;

        byte[] sequence = [(byte)Keys.ControlKey, (byte)Keys.Menu, (byte)Keys.ShiftKey, (byte)Keys.F23];
        foreach (byte key in sequence) keybd_event(key, 0, 0, UIntPtr.Zero);
        foreach (byte key in sequence.Reverse()) keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        Assert.True(
            MessageThread.PumpUntil(() => fired is not null, TimeSpan.FromSeconds(2)),
            "No WM_HOTKEY arrived. This test needs an interactive desktop session, and UIPI blocks "
            + "synthetic input while an elevated window has focus.");
        Assert.Equal(hotkey, fired);
    });
}
