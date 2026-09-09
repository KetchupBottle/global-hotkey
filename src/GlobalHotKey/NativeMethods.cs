using System.Runtime.InteropServices;

namespace GlobalHotKey;

internal static class NativeMethods
{
    internal const int WM_HOTKEY = 0x0312;
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    /// <summary>Parent value that turns a window into a message-only window.</summary>
    internal static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int dwProcessId);

    /// <summary>
    /// Windows grants the foreground privilege to whoever registered the hotkey that just fired, but
    /// only the process we launch inherits it. A program that is already running gets no more than a
    /// signal from its launcher, so it stays behind whatever window the user was in. ASFW_ANY (-1)
    /// hands the privilege to any process, which is what lets that existing window come forward.
    /// Best effort: if the system refuses, the program still opens, just without focus.
    /// </summary>
    internal static void LetTheLaunchedAppTakeFocus() => AllowSetForegroundWindow(-1);

    /// <summary>KeyEventArgs has no Win flag, so the shortcut recorder has to ask Windows directly.</summary>
    internal static bool IsWinKeyDown() => GetKeyState(VK_LWIN) < 0 || GetKeyState(VK_RWIN) < 0;
}
