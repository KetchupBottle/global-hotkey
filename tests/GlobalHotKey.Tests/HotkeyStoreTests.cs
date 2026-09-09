using System.Text.Json;

namespace GlobalHotKey.Tests;

public class HotkeyStoreTests
{
    static readonly List<Hotkey> Sample =
    [
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.X, ActionType.Program, @"C:\Windows\notepad.exe", @"C:\notes.txt"),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.D, ActionType.Folder, @"C:\Users", null),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.G, ActionType.Url, "https://github.com", null)
    ];

    [Fact]
    public void Round_trip_preserves_the_list() =>
        Assert.Equal(Sample, HotkeyStore.Deserialize(HotkeyStore.Serialize(Sample)));

    [Fact]
    public void Json_is_written_with_readable_names()
    {
        string json = HotkeyStore.Serialize(Sample);
        // The serializer lists flags in enum value order (Alt=1 before Control=2), which is not the
        // order the UI shows. DisplayText owns the reading order; the file just has to round-trip.
        Assert.Contains("\"modifiers\": \"Alt, Control\"", json);
        Assert.Contains("\"key\": \"X\"", json);
        Assert.Contains("\"action\": \"Program\"", json);
        Assert.Contains("\"target\": \"C:\\\\Users\"", json);
    }

    [Fact]
    public void The_computed_display_text_stays_out_of_the_file() =>
        Assert.DoesNotContain("displayText", HotkeyStore.Serialize(Sample));

    [Fact]
    public void Null_arguments_are_left_out() =>
        Assert.DoesNotContain("\"arguments\": null", HotkeyStore.Serialize(Sample));

    [Fact]
    public void An_empty_array_reads_as_no_hotkeys() => Assert.Empty(HotkeyStore.Deserialize("[]"));

    [Fact]
    public void Broken_json_throws() => Assert.Throws<JsonException>(() => HotkeyStore.Deserialize("{ not json"));

    [Fact]
    public void Invalid_entries_are_dropped_and_counted()
    {
        // One entry has no target, the other is fine.
        const string json = """
            [
              { "modifiers": "Control, Alt", "key": "X", "action": "Program" },
              { "modifiers": "Control, Alt", "key": "Y", "action": "Program", "target": "C:\\Windows\\notepad.exe" }
            ]
            """;

        var hotkeys = HotkeyStore.Deserialize(json, out int dropped);

        Assert.Equal(1, dropped);
        Assert.Equal(Keys.Y, Assert.Single(hotkeys).Key);
    }
}
