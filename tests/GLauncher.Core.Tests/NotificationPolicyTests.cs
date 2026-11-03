using GLauncher.Core.Input;
using GLauncher.Core.Notifications;
using GLauncher.Core.Providers;

namespace GLauncher.Core.Tests;

public class NotificationPolicyTests
{
    private static readonly DefaultNotificationPolicy Policy = new();

    private static NotificationRequest Req(
        NotificationCategory c, NotificationPriority p = NotificationPriority.Normal,
        DataProvenance prov = DataProvenance.Real) => new(c, p, "t", null, prov);

    private static NotificationSettings AllGameplayOn() => new(true, true, true, true);

    [Fact]
    public void Foreground_shows_when_enabled_and_suppresses_when_disabled()
    {
        Assert.Equal(NotificationDisposition.Show,
            Policy.Evaluate(AppPresence.Foreground, Req(NotificationCategory.General), NotificationSettings.Default).Disposition);
        var off = NotificationSettings.Default with { ForegroundEnabled = false };
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.Foreground, Req(NotificationCategory.General), off).Disposition);
    }

    [Fact]
    public void Background_idle_allows_only_important()
    {
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.BackgroundIdle, Req(NotificationCategory.General), AllGameplayOn()).Disposition);
        Assert.Equal(NotificationDisposition.Show,
            Policy.Evaluate(AppPresence.BackgroundIdle,
                Req(NotificationCategory.InactivityClosePrompt, NotificationPriority.Important), AllGameplayOn()).Disposition);
    }

    [Fact]
    public void Gameplay_follows_user_enabled_categories_only()
    {
        var friendsOnly = new NotificationSettings(true, true, false, false);
        Assert.Equal(NotificationDisposition.Show,
            Policy.Evaluate(AppPresence.Gameplay, Req(NotificationCategory.FriendOnline), friendsOnly).Disposition);
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.Gameplay, Req(NotificationCategory.GameAchievement), friendsOnly).Disposition);
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.Gameplay, Req(NotificationCategory.Custom), friendsOnly).Disposition);
    }

    [Fact]
    public void Gameplay_never_shows_general_or_inactivity_even_if_important()
    {
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.Gameplay,
                Req(NotificationCategory.InactivityClosePrompt, NotificationPriority.Important), AllGameplayOn()).Disposition);
        Assert.Equal(NotificationDisposition.Suppress,
            Policy.Evaluate(AppPresence.Gameplay,
                Req(NotificationCategory.General, NotificationPriority.Important), AllGameplayOn()).Disposition);
    }

    [Fact]
    public void Demo_provenance_requires_a_demo_label()
    {
        var d = Policy.Evaluate(AppPresence.Gameplay,
            Req(NotificationCategory.GameAchievement, prov: DataProvenance.Demo), AllGameplayOn());
        Assert.True(d.DemoLabelRequired);
        var real = Policy.Evaluate(AppPresence.Gameplay,
            Req(NotificationCategory.GameAchievement, prov: DataProvenance.Real), AllGameplayOn());
        Assert.False(real.DemoLabelRequired);
    }

    [Fact]
    public void Minimized_launcher_with_a_running_game_uses_gameplay_not_background_policy()
    {
        var f = new LauncherFocusFacts(false, true, false, false, true);
        Assert.Equal(AppPresence.Gameplay, AppPresenceResolver.Resolve(f, anySessionPossiblyAlive: true));
    }

    [Fact]
    public void Minimized_launcher_without_a_game_is_background_idle()
    {
        var f = new LauncherFocusFacts(false, true, false, false, false);
        Assert.Equal(AppPresence.BackgroundIdle, AppPresenceResolver.Resolve(f, anySessionPossiblyAlive: false));
    }

    [Fact]
    public void Focused_launcher_is_foreground_even_with_a_running_game()
    {
        var f = new LauncherFocusFacts(true, false, false, false, true);
        Assert.Equal(AppPresence.Foreground, AppPresenceResolver.Resolve(f, anySessionPossiblyAlive: true));
    }

    [Theory]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false)]
    public void Inactivity_prompt_gate(bool minimized, bool session, bool saving, bool downloading, bool recent, bool expected)
    {
        var f = new InactivityFacts(minimized, session, saving, downloading, recent);
        Assert.Equal(expected, InactivityPromptPolicy.MayOfferClosePrompt(f));
    }
}
