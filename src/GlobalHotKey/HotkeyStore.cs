using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlobalHotKey;

internal sealed class HotkeyStore(string filePath)
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlobalHotKey", "hotkeys.json");

    public string FilePath { get; } = filePath;

    public static string Serialize(IReadOnlyList<Hotkey> hotkeys) => JsonSerializer.Serialize(hotkeys, Options);

    public static List<Hotkey> Deserialize(string json) => Deserialize(json, out _);

    internal static List<Hotkey> Deserialize(string json, out int dropped)
    {
        var parsed = JsonSerializer.Deserialize<List<Hotkey>>(json, Options) ?? [];
        List<Hotkey> kept = [.. parsed.Where(h => h is not null && h.Validate() is null)];
        dropped = parsed.Count - kept.Count;
        return kept;
    }

    /// <summary>Never throws. Returns the hotkeys plus a message to show the user, if anything was off.</summary>
    public (List<Hotkey> Hotkeys, string? Warning) Load()
    {
        if (!File.Exists(FilePath)) return ([], null);

        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ([], $"Could not read {FilePath}: {ex.Message}");
        }

        try
        {
            var hotkeys = Deserialize(json, out int dropped);
            return (hotkeys, dropped == 0 ? null : $"{dropped} invalid {(dropped == 1 ? "entry" : "entries")} ignored.");
        }
        catch (JsonException)
        {
            string name = Path.GetFileName(FilePath);
            string backup = FilePath + ".corrupt";
            try
            {
                File.Copy(FilePath, backup, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ([], $"{name} is not valid JSON and could not be backed up: {ex.Message}");
            }
            return ([], $"{name} is not valid JSON. A copy was saved as {Path.GetFileName(backup)}.");
        }
    }

    public void Save(IReadOnlyList<Hotkey> hotkeys)
    {
        if (Path.GetDirectoryName(FilePath) is { Length: > 0 } directory) Directory.CreateDirectory(directory);

        // Write beside the target and rename: a rename within one NTFS volume is atomic, so a crash
        // mid-save leaves an orphan .tmp at worst, never a half-written config.
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, Serialize(hotkeys));
        File.Move(temp, FilePath, overwrite: true);
    }
}
