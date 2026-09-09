namespace GlobalHotKey.Tests;

[Trait("Category", "Integration")]
public class ActionRunnerTests
{
    static Hotkey Action(ActionType type, string target, string? arguments = null) =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F22, type, target, arguments);

    [Fact]
    public void Launching_a_real_program_succeeds() =>
        // rundll32 with no arguments exits immediately and shows nothing: a genuine launch, no side effect.
        Assert.Null(ActionRunner.Run(Action(ActionType.Program, @"C:\Windows\System32\rundll32.exe")));

    [Fact]
    public void A_missing_program_comes_back_as_a_message() =>
        Assert.NotNull(ActionRunner.Run(Action(ActionType.Program, @"C:\does\not\exist\nope.exe")));

    [Fact]
    public void A_missing_folder_comes_back_as_a_message() =>
        Assert.NotNull(ActionRunner.Run(Action(ActionType.Folder, @"C:\does\not\exist\nope")));

    // The URL path has no launch test on purpose. It is the same ShellExecute call as the two above,
    // and there is no URL that fails without a side effect: a valid one opens the browser, and an
    // unregistered scheme makes Windows open the default-apps settings page instead of failing.
    // Malformed URLs are rejected by Hotkey.Validate long before they get here, and that is covered.
}
