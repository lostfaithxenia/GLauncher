using System.Collections.Immutable;
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
    /// <summary>Registered; no process confirmed yet. Registration precedes any native side effect.</summary>
    Launching,
    /// <summary>At least one verified member process is alive. Stays Running while any verified child lives, even if the root exited.</summary>
    Running,
    /// <summary>
    /// No verified member is alive, but coverage is Incomplete, so a handoff/launcher/emulator process the adapter
    /// cannot see may still be running. NOT an exit. Resolved by new evidence, termination, or StopTrackingAsync.
    /// </summary>
    TrackingUncertain,
    /// <summary>Terminal. Tracking positively observed the end of the session with Complete coverage (or a verified termination).</summary>
    Exited,
    /// <summary>
    /// Terminal. The user told the launcher to stop tracking. The game's real state is UNKNOWN; this is not an exit
    /// and the UI must not claim the game closed.
    /// </summary>
    TrackingStopped,
    /// <summary>Terminal. The game never started. See Failure.</summary>
    LaunchFailed
}

public enum SessionEndReason
{
    /// <summary>All verified members were observed to end on their own, with Complete coverage.</summary>
    ObservedExit,
    /// <summary>A TerminateAsync call ended the game and proved it (see TerminationResult.ProvesGameEnded).</summary>
    TerminatedByLauncher,
    /// <summary>Only valid with SessionState.TrackingStopped.</summary>
    TrackingDismissedByUser
}

public enum LaunchFailureKind { ExecutableNotFound, AccessDenied, ElevationRequired, StartFailed, CancelledBeforeStart, Other }

public sealed record LaunchFailure(LaunchFailureKind Kind, string Message);

/// <summary>How the adapter discovered the tracked set (diagnostic).</summary>
public enum TrackingQuality
{
    /// <summary>Membership enforced by the OS (e.g. a job object that held).</summary>
    Contained,
    /// <summary>Discovered by following child processes; may miss breakaway/handoff processes.</summary>
    ProcessTree,
    /// <summary>Only a root process is known.</summary>
    RootOnly
}

/// <summary>
/// The adapter's claim about whether the tracked set is the WHOLE game. Complete means the adapter has no reason
/// to believe a process escaped (e.g. a job that held, or a direct game exe with no handoff signature).
/// It is a verified-as-best-we-can claim, not a guarantee; Incomplete is the honest default for launchers/emulators.
/// </summary>
public enum TrackingCoverage { Complete, Incomplete }

/// <summary>
/// Whether TerminateAsync can end the WHOLE game safely.
/// Supported   = coverage is Complete AND every member is verified-owned: terminating the known set closes the game.
///               Invariant: Supported implies TrackingCoverage.Complete.
/// Partial     = verified-owned members can be terminated, but the game may have other processes we cannot see
///               (bootstrap-only subset, external handoff). Terminating may not close the game.
/// Unsupported = no safe termination (nothing verifiably owned). Do not offer emergency close.
/// </summary>
public enum TerminationCapability { Supported, Partial, Unsupported }

/// <summary>Immutable. Safe to share across observers and threads.</summary>
public sealed record SessionSnapshot(
    SessionId SessionId,
    GameId GameId,
    // Starts at 1 at registration and increases by 1 per published change. Lets observers order and detect skipped snapshots.
    long Revision,
    SessionState State,
    ProcessIdentity? RootProcess,
    // Verified member processes currently alive. Exited members are removed.
    ImmutableArray<ProcessIdentity> TrackedProcesses,
    TrackingQuality Quality,
    TrackingCoverage Coverage,
    TerminationCapability Termination,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    SessionEndReason? EndReason = null,
    LaunchFailure? Failure = null)
{
    /// <summary>True while the game could still be running (Launching, Running, TrackingUncertain).</summary>
    public bool IsPossiblyAlive =>
        State is SessionState.Launching or SessionState.Running or SessionState.TrackingUncertain;

    /// <summary>No further snapshots will follow a terminal one.</summary>
    public bool IsTerminal => SessionStateResolver.IsTerminal(State);
}

public enum DuplicateLaunchPolicy
{
    /// <summary>Default: if this game already has a possibly-alive session, do not launch again.</summary>
    RejectIfActive,
    AllowConcurrent
}

/// <summary>
/// SessionId is supplied by the CALLER and is the idempotency key. Create a fresh id per launch attempt,
/// and keep it: it is how ObserveAsync/GetSession/TerminateAsync find the session even if the launch call
/// was cancelled or its result lost.
/// </summary>
public sealed record LaunchRequest(
    SessionId SessionId,
    GameId GameId,
    LaunchTarget Target,
    DuplicateLaunchPolicy DuplicatePolicy = DuplicateLaunchPolicy.RejectIfActive);

public enum LaunchResultKind
{
    /// <summary>A new session was registered (it may be Launching, Running or already LaunchFailed).</summary>
    Started,
    /// <summary>This SessionId was already registered. Nothing was launched again; Session is the existing one.</summary>
    ReplayedExisting,
    /// <summary>DuplicatePolicy rejected the request. No new session exists; Session is the already-active one.</summary>
    RejectedGameAlreadyActive
}

public sealed record LaunchResult(LaunchResultKind Kind, SessionSnapshot Session);

public enum TerminationTrigger { EmergencyBinding, UserMenu, LauncherExitPolicy }

public sealed record TerminationRequest(SessionId SessionId, TerminationTrigger Trigger);

public enum TerminationOutcome
{
    /// <summary>Capability was Supported, every verified member ended, nothing survived, no unknown survivors possible.</summary>
    Terminated,
    /// <summary>The known verified members ended, but unknown survivors are possible (capability Partial). The game may still run.</summary>
    KnownProcessesTerminated,
    /// <summary>Some known members ended; some survived (see Survivors).</summary>
    PartiallyTerminated,
    NotSupported,
    SessionNotActive,
    Failed
}

public sealed record TerminationResult(
    TerminationOutcome Outcome,
    ImmutableArray<ProcessIdentity> Terminated,
    // Known members that were still alive after the attempt.
    ImmutableArray<ProcessIdentity> Survivors,
    //
    // Members whose PID/creation-time identity no longer matched at the moment of the call (PID reuse or already gone).
    // They are never signalled.

    ImmutableArray<ProcessIdentity> SkippedStale,
    // True when processes we cannot enumerate may survive. Required true whenever capability was Partial.
    bool UnknownSurvivorsPossible,
    string? Detail)
{
    /// <summary>
    /// Only this may justify marking a session Exited/TerminatedByLauncher. An emptied known subset alone does not.
    /// </summary>
    public bool ProvesGameEnded =>
        Outcome == TerminationOutcome.Terminated && !UnknownSurvivorsPossible && Survivors.IsDefaultOrEmpty;
}

/// <summary>What the launcher does with a running game when the launcher itself closes.</summary>
public enum LauncherExitPolicy
{
    /// <summary>Default. The game keeps running; the launcher simply stops observing.</summary>
    LeaveGameRunning,
    AskUser,
    /// <summary>Implemented as explicit TerminateAsync calls (trigger LauncherExitPolicy) before exit; never as kill-on-handle-close.</summary>
    TerminateGame
}

public readonly record struct StateResolution(SessionState State, SessionEndReason? EndReason);

/// <summary>Pure, testable rules for the session state machine. Adapters feed it observations.</summary>
public static class SessionStateResolver
{
    public static bool IsTerminal(SessionState state) =>
        state is SessionState.Exited or SessionState.TrackingStopped or SessionState.LaunchFailed;

    /// <summary>
    /// Terminal states are sticky and returned unchanged (EndReason null here means "keep the existing one").
    /// A live verified member always means Running, even if the root process has exited.
    /// With no live member: Launching stays Launching; otherwise Complete coverage means Exited(ObservedExit),
    /// Incomplete coverage means TrackingUncertain (never Exited).
    /// </summary>
    public static StateResolution Resolve(SessionState current, bool anyVerifiedMemberAlive, TrackingCoverage coverage)
    {
        if (IsTerminal(current))
            return new StateResolution(current, null);

        if (anyVerifiedMemberAlive)
            return new StateResolution(SessionState.Running, null);

        if (current == SessionState.Launching)
            return new StateResolution(SessionState.Launching, null);

        return coverage == TrackingCoverage.Complete
            ? new StateResolution(SessionState.Exited, SessionEndReason.ObservedExit)
            : new StateResolution(SessionState.TrackingUncertain, null);
    }
}

public interface IGameSessionService
{
    /// <summary>
    /// Registers the session (state Launching, Revision 1, queryable via GetSession/ObserveAsync) BEFORE any native
    /// side effect, then starts the game.
    /// - SessionId already registered: returns ReplayedExisting; nothing is launched again.
    /// - Game already has a possibly-alive session and DuplicatePolicy is RejectIfActive: returns
    ///   RejectedGameAlreadyActive with the existing session; no new session is created.
    /// - Token already cancelled on entry: throws OperationCanceledException; nothing is registered.
    /// - Cancelled after registration but BEFORE the process starts: no process is created; the session becomes
    ///   LaunchFailed(CancelledBeforeStart) and is returned (no exception).
    /// - Cancelled AFTER a process started: the game is never killed; the session stays tracked and queryable, and
    ///   the call returns its current snapshot (no exception). The token only ever stops the launcher's wait.
    /// Native start failures are returned as LaunchFailed, not thrown.
    /// </summary>
    Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken cancellationToken);

    /// <summary>Latest snapshot, or null if the id was never registered (or a terminal session was evicted).</summary>
    SessionSnapshot? GetSession(SessionId id);

    /// <summary>Snapshots whose IsPossiblyAlive is true.</summary>
    IReadOnlyList<SessionSnapshot> GetActiveSessions();

    /// <summary>
    /// Subscription contract (each enumeration is an independent observer):
    /// - Atomic replay: the first item is the session's current snapshot at the moment of subscription, with no gap
    ///   before later changes, so an exit between GetSession and subscribing cannot be missed.
    /// - Independent: every enumeration has its own queue; observers never split or consume a shared stream.
    /// - Ordering: snapshots arrive in increasing Revision order.
    /// - Coalescing: a slow observer may skip intermediate snapshots (Revision gaps are allowed) but always receives
    ///   the latest state, and always receives the terminal snapshot.
    /// - Completion: the stream completes after delivering the terminal snapshot. Observing an already-terminal
    ///   session replays the terminal snapshot and completes.
    /// - Unknown id: throws KeyNotFoundException on the first MoveNextAsync.
    /// - Cancellation/disposal removes only that observer (OperationCanceledException per usual convention); it never
    ///   stops the tracker and never terminates the game.
    /// - Snapshots are immutable.
    /// </summary>
    IAsyncEnumerable<SessionSnapshot> ObserveAsync(SessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Non-destructive resolution for a session the user no longer wants tracked (typically TrackingUncertain).
    /// Never signals or terminates any process. Moves a non-terminal session to TrackingStopped with
    /// EndReason TrackingDismissedByUser, which removes it from GetActiveSessions and from gameplay policy.
    /// A terminal session is returned unchanged (idempotent). Unknown id: KeyNotFoundException.
    /// The UI must not describe TrackingStopped as "game closed".
    /// </summary>
    Task<SessionSnapshot> StopTrackingAsync(SessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// The ONLY operation that ends a game. Scoped to the given session; never targets the foreground process.
    /// The adapter must revalidate membership and (PID, creation time) at the moment of the call and skip stale
    /// identities (reported in SkippedStale). Capability Unsupported returns NotSupported. Capability Partial may
    /// terminate verified members but must report UnknownSurvivorsPossible = true.
    /// The session is marked Exited/TerminatedByLauncher only when the result ProvesGameEnded; otherwise state is
    /// derived by SessionStateResolver from what is still observed.
    /// </summary>
    Task<TerminationResult> TerminateAsync(TerminationRequest request, CancellationToken cancellationToken);
}
