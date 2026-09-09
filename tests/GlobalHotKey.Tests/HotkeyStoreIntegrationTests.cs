namespace GlobalHotKey.Tests;

[Trait("Category", "Integration")]
public class HotkeyStoreIntegrationTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "GlobalHotKey.Tests", Guid.NewGuid().ToString("N"));

    static readonly List<Hotkey> Sample =
    [
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.X, ActionType.Program, @"C:\Windows\notepad.exe", @"C:\notes.txt"),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.G, ActionType.Url, "https://github.com", null)
    ];

    string FilePath => Path.Combine(_directory, "hotkeys.json");
    HotkeyStore Store() => new(FilePath);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void A_first_run_finds_nothing_and_creates_nothing()
    {
        var (hotkeys, warning) = Store().Load();

        Assert.Empty(hotkeys);
        Assert.Null(warning);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Saving_creates_the_folder_and_the_file()
    {
        Store().Save(Sample);

        Assert.True(File.Exists(FilePath));
        var (hotkeys, warning) = Store().Load();
        Assert.Equal(Sample, hotkeys);
        Assert.Null(warning);
    }

    [Fact]
    public void Saving_leaves_no_temporary_file_behind()
    {
        Store().Save(Sample);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Saving_again_replaces_the_previous_contents()
    {
        var store = Store();
        store.Save(Sample);
        store.Save([Sample[0]]);

        Assert.Equal([Sample[0]], store.Load().Hotkeys);
    }

    [Fact]
    public void A_corrupt_file_is_backed_up_and_left_alone()
    {
        const string garbage = "{ this is not json";
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, garbage);

        var (hotkeys, warning) = Store().Load();

        Assert.Empty(hotkeys);
        Assert.NotNull(warning);
        Assert.Equal(garbage, File.ReadAllText(FilePath + ".corrupt"));
        Assert.Equal(garbage, File.ReadAllText(FilePath));
    }
}
