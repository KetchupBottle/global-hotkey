using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GlobalHotKey;

/// <summary>
/// Owns the system-wide hotkey registrations. Uses a message-only window rather than the main form:
/// when the app starts minimised to the tray the form is never shown and so has no window handle.
/// Windows posts WM_HOTKEY to the thread that created the window, so this must be built on the UI
/// thread, before Application.Run() starts pumping messages.
/// </summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    readonly Dictionary<int, Hotkey> _registered = [];
    int _nextId = 1;

    public event Action<Hotkey>? Pressed;

    internal IReadOnlyDictionary<int, Hotkey> Registered => _registered;

    public HotkeyManager() => CreateHandle(new CreateParams { Parent = NativeMethods.HWND_MESSAGE });

    /// <summary>Returns null on success, or a message explaining why Windows refused the combination.</summary>
    public string? TryRegister(Hotkey hotkey)
    {
        // MOD_NOREPEAT stops a held key from firing the action over and over.
        uint modifiers = (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT;

        if (!NativeMethods.RegisterHotKey(Handle, _nextId, modifiers, (uint)hotkey.Key))
        {
            int error = Marshal.GetLastWin32Error();
            return error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? $"{hotkey.DisplayText} is already in use by another program."
                : $"{hotkey.DisplayText} could not be registered: {new Win32Exception(error).Message}";
        }

        _registered[_nextId] = hotkey;
        _nextId++;
        return null;
    }

    /// <summary>Registers everything it can and returns the failures; a bad entry never blocks the rest.</summary>
    public List<string> RegisterAll(IEnumerable<Hotkey> hotkeys)
    {
        UnregisterAll();
        List<string> errors = [];
        foreach (var hotkey in hotkeys)
            if (TryRegister(hotkey) is { } error) errors.Add(error);
        return errors;
    }

    public void UnregisterAll()
    {
        foreach (int id in _registered.Keys) NativeMethods.UnregisterHotKey(Handle, id);
        _registered.Clear();
        _nextId = 1;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && _registered.TryGetValue((int)m.WParam, out var hotkey))
            Pressed?.Invoke(hotkey);
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
