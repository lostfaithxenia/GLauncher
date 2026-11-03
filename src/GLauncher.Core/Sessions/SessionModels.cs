using GLauncher.Core.Games;

namespace GLauncher.Core.Sessions;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// A process is identified by PID PLUS creation time, because bare PIDs are reused by Windows.
/// Two identities are equal only if both match. No handles or Win32 types live here.
/// </summary>
public readonly record struct ProcessIdentity(int ProcessId, DateTimeOffset CreationTimeUtc);

public enum SessionState
{
    /// <summary>Start requested; no process confirmed yet.</summary>
    Launching,
    /// <summary>At least one tracked process is confirmed alive.</summary>
    Running,
    /// <summary>
    /// The root/bootstrap process ended or tracking was lost, but the game may still be running
    /// (launcher handoff, emulator, anti-cheat stub). This is NOT an exit.
    /// </summary>
    TrackingUncertain,
    /// <summary>Tracking positively observed the end of the session. See EndReason.</summary>
    Exited,
    /// <summary>The game never started. See Failure.</summary>
    LaunchFailed
}

public enum SessionEndReason
{
    /// <summary>All tracked processes were observed to end on their own.</summary>
    ObservedExit,
    /// <summary>Ended after an explicit TerminateAsync call.</summary>
    EmergencyTerminated,
    /// <summary>The user marked an uncertain session as finished.</summary>
    UserDismissed
}

public enum LaunchFailureKind { ExecutableNotFound, AccessDenied, ElevationRequired, StartFailed, Other }

public sealed record LaunchFailure(LaunchFailureKind Kind, string Message);

/// <summary>How strongly the adapter can vouch that the tracked set is the whole game.</summary>
public enum TrackingQuality
{
    /// <summary>Process membership is enforced by the OS (e.g. a job object that held).</summary>
    Contained,
    /// <summary>Discovered by following child processes; may miss breakaway/handoff processes.</summary>
    ProcessTree,
    /// <summary>Only a root process is known.</summary>
    RootOnly
}

/// <summary>Whether explicit emergency termination is safe and meaningful for this session.</summary>
public enum TerminationCapability
{
    /// <summary>Every tracked process is verifiably owned by this session.</summary>
    Supported,
    /// <summary>Some processes can be terminated; others (external launchers) are out of reach.</summary>
    Partial,
    /// <summary>Do not offer emergency close for this session.</summary>
    Unsupported
}

public sealed record SessionSnapshot(
    SessionId SessionId,
    GameId GameId,
    SessionState State,
    ProcessIdentity? RootProcess,
    IReadOnlyList<ProcessIdentity> TrackedProcesses,
    TrackingQuality Quality,
    TerminationCapability Termination,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    SessionEndReason? EndReason = null,
    LaunchFailure? Failure = null)
{
    /// <summary>True while the game could still be running (including uncertain tracking).</summary>
    public bool IsPossiblyAlive => State is SessionState.Launching or SessionState.Running or SessionState.TrackingUncertain;
}

public sealed record LaunchRequest(GameId GameId, LaunchTarget Target);

public enum TerminationTrigger { EmergencyBinding, UserMenu, LauncherExitPolicy }

public sealed record TerminationRequest(SessionId SessionId, TerminationTrigger Trigger);

public enum TerminationOutcome { Terminated, PartiallyTerminated, NotSupported, SessionNotActive, Failed }

public sealed record TerminationResult(
    TerminationOutcome Outcome,
    IReadOnlyList<ProcessIdentity> Terminated,
    IReadOnlyList<ProcessIdentity> Survivors,
    string? Detail);

/// <summary>What the launcher does with a running game when the launcher itself closes.</summary>
public enum LauncherExitPolicy { LeaveGameRunning, AskUser, TerminateGame }

public interface IGameSessionService
{
    /// <summary>
    /// Starts a game. The token cancels only the launcher's WAIT for startup; once a process
    /// exists, cancellation never kills it. Failures are returned in the snapshot (LaunchFailed).
    /// </summary>
    Task<SessionSnapshot> LaunchAsync(LaunchRequest request, CancellationToken cancellationToken);

    SessionSnapshot? GetSession(SessionId id);

    IReadOnlyList<SessionSnapshot> GetActiveSessions();

    /// <summary>
    /// Streams snapshots as the session changes. Cancelling or disposing the enumeration only stops
    /// observing; it NEVER terminates the game. Game lifetime is independent of any UI window or watcher.
    /// </summary>
    IAsyncEnumerable<SessionSnapshot> ObserveAsync(SessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// The ONLY operation that ends a game. Scoped to the processes of the given session identity;
    /// it must never target an arbitrary foreground process. Returns NotSupported when
    /// Termination capability is Unsupported.
    /// </summary>
    Task<TerminationResult> TerminateAsync(TerminationRequest request, CancellationToken cancellationToken);
}
