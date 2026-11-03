using GLauncher.Core.Input;

namespace GLauncher.Core.Tests;

public class InputPolicyTests
{
    private static readonly DefaultInputPolicy Policy = new();
    private static readonly InputPolicySettings Settings = InputPolicySettings.Default;

    private static LauncherFocusFacts Facts(
        bool foreground = true, bool minimized = false, bool typing = false,
        bool gameFocus = false, bool session = false) =>
        new(foreground, minimized, typing, gameFocus, session);

    [Fact]
    public void Focused_launcher_allows_navigation_and_fullscreen_toggle()
    {
        var d = Policy.Evaluate(Facts(), Settings);
        Assert.True(d.LauncherNavigationEnabled);
        Assert.True(d.FullscreenToggleAllowed);
        Assert.Equal(InputSuspensionReason.None, d.SuspensionReason);
    }

    [Fact]
    public void Fullscreen_toggle_is_denied_while_typing_in_an_input()
    {
        var d = Policy.Evaluate(Facts(typing: true), Settings);
        Assert.False(d.PlainKeyShortcutsAllowed);
        Assert.False(d.FullscreenToggleAllowed);
    }

    [Fact]
    public void Fullscreen_toggle_is_denied_when_minimized_even_if_active_flag_is_true()
    {
        // Mirrors the WPF spike finding: IsActive can be true while minimized.
        var d = Policy.Evaluate(Facts(foreground: true, minimized: true), Settings);
        Assert.False(d.FullscreenToggleAllowed);
        Assert.False(d.LauncherNavigationEnabled);
        Assert.Equal(InputSuspensionReason.LauncherMinimized, d.SuspensionReason);
    }

    [Fact]
    public void Fullscreen_toggle_is_denied_when_another_app_is_foreground()
    {
        var d = Policy.Evaluate(Facts(foreground: false), Settings);
        Assert.False(d.FullscreenToggleAllowed);
        Assert.Equal(InputSuspensionReason.LauncherNotForeground, d.SuspensionReason);
    }

    [Fact]
    public void Tracked_game_focus_suspends_launcher_navigation_but_arms_emergency_close()
    {
        var d = Policy.Evaluate(Facts(foreground: false, gameFocus: true, session: true), Settings);
        Assert.False(d.LauncherNavigationEnabled);
        Assert.True(d.EmergencyCloseArmed);
        Assert.Equal(InputSuspensionReason.TrackedGameHasFocus, d.SuspensionReason);
    }

    [Fact]
    public void Emergency_close_is_not_armed_without_an_active_session()
    {
        var d = Policy.Evaluate(Facts(foreground: false, gameFocus: true, session: false), Settings);
        Assert.False(d.EmergencyCloseArmed);
    }

    [Fact]
    public void Emergency_close_is_not_armed_when_disabled_in_settings()
    {
        var off = Settings with { EmergencyCloseEnabled = false };
        var d = Policy.Evaluate(Facts(foreground: false, gameFocus: true, session: true), off);
        Assert.False(d.EmergencyCloseArmed);
    }

    [Fact]
    public void Returning_to_launcher_restores_navigation_while_game_stays_alive()
    {
        var d = Policy.Evaluate(Facts(session: true), Settings);
        Assert.True(d.LauncherNavigationEnabled);
        Assert.False(d.EmergencyCloseArmed); // default: not armed while the launcher itself is focused
    }

    [Fact]
    public void Emergency_close_can_be_opted_in_while_launcher_focused_but_never_while_typing()
    {
        var optIn = Settings with { ArmEmergencyWhileLauncherFocused = true };
        Assert.True(Policy.Evaluate(Facts(session: true), optIn).EmergencyCloseArmed);
        Assert.False(Policy.Evaluate(Facts(session: true, typing: true), optIn).EmergencyCloseArmed);
    }

    [Fact]
    public void Proposed_default_binding_is_delete_with_a_hold()
    {
        var b = EmergencyCloseBinding.ProposedDefault;
        Assert.Equal("Delete", b.KeyName);
        Assert.True(b.HoldDuration > TimeSpan.Zero);
    }
}
