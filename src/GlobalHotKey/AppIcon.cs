namespace GlobalHotKey;

/// <summary>
/// The icon is an embedded resource, not a file next to the exe: a single-file publish has no
/// such file to read at runtime.
/// </summary>
internal static class AppIcon
{
    public static Icon Load(Size? size = null)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico")
            ?? throw new InvalidOperationException("app.ico is missing from the assembly.");
        return size is { } wanted ? new Icon(stream, wanted) : new Icon(stream);
    }
}
