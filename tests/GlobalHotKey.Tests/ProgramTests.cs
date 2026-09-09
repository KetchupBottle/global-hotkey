namespace GlobalHotKey.Tests;

[Trait("Category", "Integration")]
public class ProgramTests : IDisposable
{
    // Environment variables are process-wide, so remember what was there and put it back.
    readonly Dictionary<string, string?> _saved = new()
    {
        ["ELECTRON_RUN_AS_NODE"] = Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE"),
        ["VSCODE_PID"] = Environment.GetEnvironmentVariable("VSCODE_PID"),
        ["VSCODE_IPC_HOOK"] = Environment.GetEnvironmentVariable("VSCODE_IPC_HOOK"),
        ["GHK_TEST_KEEPER"] = Environment.GetEnvironmentVariable("GHK_TEST_KEEPER")
    };

    public void Dispose()
    {
        foreach (var (name, value) in _saved) Environment.SetEnvironmentVariable(name, value);
    }

    [Fact]
    public void The_electron_variables_that_break_launching_are_dropped()
    {
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
        Environment.SetEnvironmentVariable("VSCODE_PID", "1234");
        Environment.SetEnvironmentVariable("VSCODE_IPC_HOOK", @"\\.\pipe\whatever");
        Environment.SetEnvironmentVariable("GHK_TEST_KEEPER", "keep me");

        Program.DropInheritedElectronVariables();

        Assert.Null(Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE"));
        Assert.Null(Environment.GetEnvironmentVariable("VSCODE_PID"));
        Assert.Null(Environment.GetEnvironmentVariable("VSCODE_IPC_HOOK"));

        // Everything else has to survive: PATH and friends are how the launched programs find things.
        Assert.Equal("keep me", Environment.GetEnvironmentVariable("GHK_TEST_KEEPER"));
        Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATH")));
    }

    [Fact]
    public void Dropping_them_when_they_are_absent_is_harmless()
    {
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
        Environment.SetEnvironmentVariable("VSCODE_PID", null);

        Program.DropInheritedElectronVariables();

        Assert.Null(Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE"));
    }
}
