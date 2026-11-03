using GLauncher.Core.Input;
using GLauncher.Core.Providers;

namespace GLauncher.Core.Notifications;

/// <summary>
/// Foreground: launcher is interactive. BackgroundIdle: minimized or not focused with no running game.
/// Gameplay: a game session is possibly alive and the launcher is not the interactive window.
/// </summary>
public enum AppPresence { Foreground, BackgroundIdle, Gameplay }

public static class AppPresenceResolver
{
    public static AppPresence Resolve(LauncherFocusFacts f, bool anySessionPossiblyAlive)
    {
        bool interactive = f.LauncherWindowForeground && !f.LauncherMinimized && !f.TrackedGameHasFocus;
        if (interactive) return AppPresence.Foreground;
        return anySessionPossiblyAlive || f.HasActiveSession ? AppPresence.Gameplay : AppPresence.BackgroundIdle;
    }
}

public enum NotificationCategory { General, InactivityClosePrompt, FriendOnline, GameAchievement, Custom }

public enum NotificationPriority { Normal, Important }

public sealed record NotificationRequest(
    NotificationCategory Category,
    NotificationPriority Priority,
    string Title,
    string? Body,
    DataProvenance Provenance);

public sealed record NotificationSettings(
    bool ForegroundEnabled,
    bool GameplayFriendsOnline,
    bool GameplayAchievements,
    bool GameplayCustom)
{
    // PROPOSED defaults: gameplay categories are opt-in.
    public static NotificationSettings Default { get; } = new(true, false, false, false);
}

public enum NotificationDisposition { Show, Suppress }

public sealed record NotificationDecision(
    NotificationDisposition Disposition,
    string Reason,
    // True when the data is demo/fixture; UI must label it and never call it a real import.
    bool DemoLabelRequired);

public interface INotificationPolicy
{
    // Decides only whether to show. Presenters must never take focus from a running game,
    // whatever the decision is.
    NotificationDecision Evaluate(AppPresence presence, NotificationRequest request, NotificationSettings settings);
}

public sealed class DefaultNotificationPolicy : INotificationPolicy
{
    public NotificationDecision Evaluate(AppPresence presence, NotificationRequest request, NotificationSettings settings)
    {
        bool demo = request.Provenance == DataProvenance.Demo;

        switch (presence)
        {
            case AppPresence.Foreground:
                return settings.ForegroundEnabled
                    ? new(NotificationDisposition.Show, "Foreground notifications enabled.", demo)
                    : new(NotificationDisposition.Suppress, "Foreground notifications disabled.", demo);

            case AppPresence.BackgroundIdle:
                return request.Priority == NotificationPriority.Important
                    ? new(NotificationDisposition.Show, "Important notification while backgrounded.", demo)
                    : new(NotificationDisposition.Suppress, "Only important notifications while backgrounded.", demo);

            default: // Gameplay
                bool allowed = request.Category switch
                {
                    NotificationCategory.FriendOnline => settings.GameplayFriendsOnline,
                    NotificationCategory.GameAchievement => settings.GameplayAchievements,
                    NotificationCategory.Custom => settings.GameplayCustom,
                    _ => false
                };
                return allowed
                    ? new(NotificationDisposition.Show, "Gameplay category enabled by the user.", demo)
                    : new(NotificationDisposition.Suppress, "Category not enabled during gameplay.", demo);
        }
    }
}

public sealed record InactivityFacts(
    bool LauncherMinimized,
    bool HasActiveSession,
    bool SaveInProgress,
    bool DownloadInProgress,
    bool PromptRecentlyShown);

/// <summary>
/// Gate for the Close / Keep open prompt. The idle timeout and any automatic close are unresolved
/// product decisions and are intentionally not modelled here.
/// </summary>
public static class InactivityPromptPolicy
{
    public static bool MayOfferClosePrompt(InactivityFacts f) =>
        f.LauncherMinimized && !f.HasActiveSession && !f.SaveInProgress &&
        !f.DownloadInProgress && !f.PromptRecentlyShown;
}
