namespace GlobalHotKey;

public enum ActionType { Program, Folder, Url }

/// <summary>Values match the Win32 MOD_* constants so they can go straight to RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public sealed record Hotkey(HotkeyModifiers Modifiers, Keys Key, ActionType Action, string Target, string? Arguments)
{
    static readonly Keys[] ModifierKeys =
    [
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
        Keys.Menu, Keys.LMenu, Keys.RMenu,
        Keys.LWin, Keys.RWin
    ];

    public string DisplayText => Describe(Modifiers, Key);

    internal static string Describe(HotkeyModifiers modifiers, Keys key) =>
        ModifierText(modifiers) is { Length: > 0 } text ? $"{text} + {KeyName(key)}" : KeyName(key);

    internal static string ModifierText(HotkeyModifiers modifiers)
    {
        List<string> parts = [];
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        return string.Join(" + ", parts);
    }

    internal static string KeyName(Keys key) => key switch
    {
        Keys.None => "",
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (char)('0' + (key - Keys.NumPad0)),
        _ => key.ToString()
    };

    /// <summary>
    /// Lets the user type "github.com" instead of the whole thing. Anything that already parses as an
    /// absolute URI is left alone, so http, mailto and Windows paths keep their meaning.
    /// </summary>
    public static string NormalizeUrl(string target) =>
        string.IsNullOrWhiteSpace(target) || Uri.TryCreate(target, UriKind.Absolute, out _)
            ? target
            : $"https://{target}";

    /// <summary>
    /// Structural checks only. Deliberately does not touch the filesystem: an uninstalled program
    /// must not make its hotkey silently disappear from the config on load.
    /// </summary>
    public string? Validate()
    {
        if (Key == Keys.None) return "Record a key combination first.";
        if (Array.IndexOf(ModifierKeys, Key) >= 0) return "A modifier on its own is not a shortcut.";
        if (Key is Keys.Tab or Keys.Enter or Keys.Escape) return $"{KeyName(Key)} cannot be used as a shortcut.";
        if (Key == Keys.F12) return "F12 is reserved by the Windows debugger.";

        bool isFunctionKey = Key is >= Keys.F1 and <= Keys.F24;
        if (Modifiers == HotkeyModifiers.None && !isFunctionKey)
            return "Add at least one modifier: Ctrl, Alt, Shift or Win.";

        if (string.IsNullOrWhiteSpace(Target)) return "Choose what the shortcut should open.";

        if (Action == ActionType.Url &&
            (!Uri.TryCreate(Target, UriKind.Absolute, out var uri) || uri.Scheme == Uri.UriSchemeFile))
            return "That does not look like a web address. Try something like example.com.";

        return null;
    }
}
