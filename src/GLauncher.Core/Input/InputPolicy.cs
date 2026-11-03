namespace GLauncher.Core.Input;

/// <summary>
/// Raw, separate facts reported by the Windows/UI adapters. A single "IsActive" flag is not enough
/// (the WPF spike reported IsActive=true while minimized), so each fact is carried on its own.
/// </summary>
public sealed record LauncherFocusFacts(
    bool LauncherWindowForeground,
    bool LauncherMinimized,
    bool TextEntryFocused,
    bool TrackedGameHasFocus,
    bool HasActiveSession);

[Flags]
public enum KeyModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

/// <summary>Key identified by name so Core needs no UI framework key enum.</summary>
public sealed record EmergencyCloseBinding(string KeyName, KeyModifiers Modifiers, TimeSpan HoldDuration)
{
    // PROPOSED default, pending SYN's decision: Delete, held for 1 second. Delete is also a real
    // game and text key, so an instant single press risks killing a game by accident.
    public static EmergencyCloseBinding ProposedDefault { get; } =
        new("Delete", KeyModifiers.None, TimeSpan.FromSeconds(1));
}

public sealed record InputPolicySettings(
    bool EmergencyCloseEnabled,
    EmergencyCloseBinding EmergencyBinding,
    bool ArmEmergencyWhileLauncherFocused,
    bool FullscreenToggleEnabled)
{
    public static InputPolicySettings Default { get; } =
        new(true, EmergencyCloseBinding.ProposedDefault, false, true);
}

public enum InputSuspensionReason { None, LauncherNotForeground, LauncherMinimized, TrackedGameHasFocus }

public sealed record InputDecision(
    // Keyboard/mouse/controller navigation of the launcher UI.
    bool LauncherNavigationEnabled,
    // Plain-key shortcuts such as F. False while typing in an input.
    bool PlainKeyShortcutsAllowed,
    bool FullscreenToggleAllowed,
    // True when the emergency binding should currently be honoured. It is an intentional exception to
    // suspended navigation and applies only to the tracked session (never the foreground process).
    bool EmergencyCloseArmed,
    InputSuspensionReason SuspensionReason);

public interface IInputPolicy
{
    InputDecision Evaluate(LauncherFocusFacts facts, InputPolicySettings settings);
}

/// <summary>Pure, UI-free default policy. Fully unit-testable.</summary>
public sealed class DefaultInputPolicy : IInputPolicy
{
    public InputDecision Evaluate(LauncherFocusFacts facts, InputPolicySettings settings)
    {
        InputSuspensionReason reason =
            facts.TrackedGameHasFocus ? InputSuspensionReason.TrackedGameHasFocus :
            facts.LauncherMinimized ? InputSuspensionReason.LauncherMinimized :
            !facts.LauncherWindowForeground ? InputSuspensionReason.LauncherNotForeground :
            InputSuspensionReason.None;

        bool interactive = reason == InputSuspensionReason.None;
        bool plainKeys = interactive && !facts.TextEntryFocused;
        bool fullscreen = plainKeys && settings.FullscreenToggleEnabled;

        bool armed =
            settings.EmergencyCloseEnabled &&
            facts.HasActiveSession &&
            (facts.TrackedGameHasFocus ||
             (interactive && settings.ArmEmergencyWhileLauncherFocused && !facts.TextEntryFocused));

        return new InputDecision(interactive, plainKeys, fullscreen, armed, reason);
    }
}
