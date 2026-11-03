using System.Collections.Immutable;
using GLauncher.Core.Games;
using GLauncher.Core.Sessions;

namespace GLauncher.Core.Tests;

public class SessionContractTests
{
    private static readonly ProcessIdentity P1 = new(100, new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private static readonly ProcessIdentity P2 = new(200, new DateTimeOffset(2026, 11, 3, 12, 0, 5, TimeSpan.Zero));

    private static SessionSnapshot Snap(SessionState state) => new(
        SessionId.New(), GameId.New(), 1, state, null, ImmutableArray<ProcessIdentity>.Empty,
        TrackingQuality.RootOnly, TrackingCoverage.Incomplete, TerminationCapability.Unsupported,
        DateTimeOffset.UtcNow);

    private static TerminationResult Result(
        TerminationOutcome outcome, bool unknownSurvivors,
        ImmutableArray<ProcessIdentity>? survivors = null) => new(
        outcome, ImmutableArray.Create(P1), survivors ?? ImmutableArray<ProcessIdentity>.Empty,
        ImmutableArray<ProcessIdentity>.Empty, unknownSurvivors, null);

    // ---- identity ----
    [Fact]
    public void Process_identity_is_not_just_a_pid()
    {
        Assert.NotEqual(new ProcessIdentity(4242, P1.CreationTimeUtc), new ProcessIdentity(4242, P2.CreationTimeUtc));
        Assert.Equal(new ProcessIdentity(4242, P1.CreationTimeUtc), new ProcessIdentity(4242, P1.CreationTimeUtc));
    }

    [Fact]
    public void Launch_request_defaults_to_rejecting_a_duplicate_active_game()
    {
        var r = new LaunchRequest(SessionId.New(), GameId.New(), new LaunchTarget("C:\\game.exe"));
        Assert.Equal(DuplicateLaunchPolicy.RejectIfActive, r.DuplicatePolicy);
    }

    // ---- snapshot flags ----
    [Theory]
    [InlineData(SessionState.Launching, true, false)]
    [InlineData(SessionState.Running, true, false)]
    [InlineData(SessionState.TrackingUncertain, true, false)]
    [InlineData(SessionState.Exited, false, true)]
    [InlineData(SessionState.TrackingStopped, false, true)]
    [InlineData(SessionState.LaunchFailed, false, true)]
    public void Snapshot_alive_and_terminal_flags(SessionState state, bool possiblyAlive, bool terminal)
    {
        var s = Snap(state);
        Assert.Equal(possiblyAlive, s.IsPossiblyAlive);
        Assert.Equal(terminal, s.IsTerminal);
    }

    [Fact]
    public void Dismissed_tracking_is_not_an_observed_exit()
    {
        // TrackingStopped is a distinct terminal state with its own end reason.
        Assert.NotEqual(SessionState.Exited, SessionState.TrackingStopped);
        Assert.NotEqual(SessionEndReason.ObservedExit, SessionEndReason.TrackingDismissedByUser);
    }

    // ---- state resolver ----
    [Fact]
    public void Launching_becomes_running_once_a_verified_member_is_alive() =>
        Assert.Equal(SessionState.Running,
            SessionStateResolver.Resolve(SessionState.Launching, true, TrackingCoverage.Incomplete).State);

    [Fact]
    public void Launching_stays_launching_with_nothing_alive_yet() =>
        Assert.Equal(SessionState.Launching,
            SessionStateResolver.Resolve(SessionState.Launching, false, TrackingCoverage.Complete).State);

    [Fact]
    public void Root_exit_with_a_verified_child_still_alive_stays_running() =>
        Assert.Equal(SessionState.Running,
            SessionStateResolver.Resolve(SessionState.Running, true, TrackingCoverage.Incomplete).State);

    [Fact]
    public void Everything_gone_with_complete_coverage_is_an_observed_exit()
    {
        var r = SessionStateResolver.Resolve(SessionState.Running, false, TrackingCoverage.Complete);
        Assert.Equal(SessionState.Exited, r.State);
        Assert.Equal(SessionEndReason.ObservedExit, r.EndReason);
    }

    [Fact]
    public void Everything_known_gone_with_incomplete_coverage_is_uncertain_not_exited()
    {
        var r = SessionStateResolver.Resolve(SessionState.Running, false, TrackingCoverage.Incomplete);
        Assert.Equal(SessionState.TrackingUncertain, r.State);
        Assert.Null(r.EndReason);
    }

    [Fact]
    public void Uncertain_session_returns_to_running_when_a_verified_member_reappears() =>
        Assert.Equal(SessionState.Running,
            SessionStateResolver.Resolve(SessionState.TrackingUncertain, true, TrackingCoverage.Incomplete).State);

    [Theory]
    [InlineData(SessionState.Exited)]
    [InlineData(SessionState.TrackingStopped)]
    [InlineData(SessionState.LaunchFailed)]
    public void Terminal_states_are_sticky(SessionState terminal)
    {
        Assert.Equal(terminal, SessionStateResolver.Resolve(terminal, true, TrackingCoverage.Complete).State);
        Assert.Equal(terminal, SessionStateResolver.Resolve(terminal, false, TrackingCoverage.Incomplete).State);
    }

    // ---- termination result ----
    [Fact]
    public void Terminated_with_no_survivors_and_no_unknowns_proves_the_game_ended() =>
        Assert.True(Result(TerminationOutcome.Terminated, unknownSurvivors: false).ProvesGameEnded);

    [Fact]
    public void Emptied_known_subset_with_possible_unknown_survivors_does_not_prove_exit()
    {
        Assert.False(Result(TerminationOutcome.KnownProcessesTerminated, unknownSurvivors: true).ProvesGameEnded);
        Assert.False(Result(TerminationOutcome.Terminated, unknownSurvivors: true).ProvesGameEnded);
    }

    [Fact]
    public void Known_survivors_never_prove_exit() =>
        Assert.False(Result(TerminationOutcome.PartiallyTerminated, unknownSurvivors: false,
            survivors: ImmutableArray.Create(P2)).ProvesGameEnded);

    [Theory]
    [InlineData(TerminationOutcome.NotSupported)]
    [InlineData(TerminationOutcome.SessionNotActive)]
    [InlineData(TerminationOutcome.Failed)]
    public void Non_terminating_outcomes_never_prove_exit(TerminationOutcome outcome) =>
        Assert.False(Result(outcome, unknownSurvivors: false).ProvesGameEnded);
}
