namespace GlobalHotKey.Tests;

public class HotkeyTests
{
    const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    static Hotkey Make(HotkeyModifiers modifiers, Keys key,
        ActionType action = ActionType.Program, string target = @"C:\Windows\notepad.exe") =>
        new(modifiers, key, action, target, null);

    [Theory]
    [InlineData(CtrlAlt, Keys.X, "Ctrl + Alt + X")]
    [InlineData(HotkeyModifiers.Shift, Keys.D1, "Shift + 1")]
    [InlineData(HotkeyModifiers.Win, Keys.F5, "Win + F5")]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Win | CtrlAlt, Keys.A, "Ctrl + Alt + Shift + Win + A")]
    [InlineData(HotkeyModifiers.Control, Keys.NumPad7, "Ctrl + Num 7")]
    public void DisplayText_lists_modifiers_in_a_fixed_order(HotkeyModifiers modifiers, Keys key, string expected) =>
        Assert.Equal(expected, Make(modifiers, key).DisplayText);

    [Fact]
    public void Modifier_values_match_the_win32_constants()
    {
        // HotkeyManager casts this enum straight into RegisterHotKey; renumbering it would break silently.
        Assert.Equal(1, (int)HotkeyModifiers.Alt);
        Assert.Equal(2, (int)HotkeyModifiers.Control);
        Assert.Equal(4, (int)HotkeyModifiers.Shift);
        Assert.Equal(8, (int)HotkeyModifiers.Win);
    }

    [Fact]
    public void A_letter_needs_a_modifier() => Assert.NotNull(Make(HotkeyModifiers.None, Keys.X).Validate());

    [Fact]
    public void A_function_key_stands_alone() => Assert.Null(Make(HotkeyModifiers.None, Keys.F5).Validate());

    [Fact]
    public void No_key_is_rejected() => Assert.NotNull(Make(CtrlAlt, Keys.None).Validate());

    [Theory]
    [InlineData(Keys.ControlKey)]
    [InlineData(Keys.ShiftKey)]
    [InlineData(Keys.Menu)]
    [InlineData(Keys.LWin)]
    public void A_modifier_on_its_own_is_not_a_shortcut(Keys key) =>
        Assert.NotNull(Make(HotkeyModifiers.Control, key).Validate());

    [Theory]
    [InlineData(Keys.Tab)]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Escape)]
    [InlineData(Keys.F12)]
    public void Keys_the_system_owns_are_rejected(Keys key) => Assert.NotNull(Make(CtrlAlt, key).Validate());

    [Fact]
    public void An_empty_target_is_rejected() => Assert.NotNull(Make(CtrlAlt, Keys.X, target: "   ").Validate());

    [Theory]
    [InlineData("github.com")]
    [InlineData(@"C:\folder")]
    public void A_url_must_be_absolute_and_not_a_file(string target) =>
        Assert.NotNull(Make(CtrlAlt, Keys.G, ActionType.Url, target).Validate());

    [Fact]
    public void A_full_url_is_accepted() =>
        Assert.Null(Make(CtrlAlt, Keys.G, ActionType.Url, "https://github.com").Validate());

    [Fact]
    public void A_missing_program_still_validates() =>
        // Existence is the UI's business. If Validate checked it, uninstalling a program would make
        // its hotkey vanish from the config the next time the app loads.
        Assert.Null(Make(CtrlAlt, Keys.X, target: @"C:\does\not\exist.exe").Validate());
}
