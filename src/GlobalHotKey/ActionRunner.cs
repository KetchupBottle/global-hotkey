using System.ComponentModel;
using System.Diagnostics;

namespace GlobalHotKey;

internal static class ActionRunner
{
    /// <summary>Runs a hotkey's action. Returns null on success, or a message to show the user.</summary>
    public static string? Run(Hotkey hotkey)
    {
        // All three action types end in the same ShellExecute call: the shell already knows how to open
        // an executable, a directory and a URL. The action type exists for validation and the UI, not
        // for three separate launch paths.
        var info = new ProcessStartInfo(hotkey.Target) { UseShellExecute = true };

        if (hotkey.Action == ActionType.Program)
        {
            info.Arguments = hotkey.Arguments ?? "";
            if (Path.GetDirectoryName(hotkey.Target) is { Length: > 0 } directory) info.WorkingDirectory = directory;
        }

        return Start(info);
    }

    /// <summary>Hands a path or URL to the shell with no extras. Returns null on success.</summary>
    public static string? Open(string target) => Start(new ProcessStartInfo(target) { UseShellExecute = true });

    static string? Start(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info)?.Dispose();   // null when the shell hands the job to a running process
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return $"Could not open {info.FileName}: {ex.Message}";
        }
    }
}
