using System.Diagnostics;
using GLauncher.Core.Games;
using GLauncher.Core.Sessions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GLauncher.Windows.Tests;

public sealed class SessionTests
{
    private static string Fixture => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../GLauncher.LaunchFixture/bin/Release/net10.0-windows/GLauncher.LaunchFixture.exe"));
    private static LaunchRequest Request(string args = "wait 30000", GameId? game = null)
        => new(SessionId.New(), game ?? GameId.New(), new LaunchTarget(Fixture, args));
    private static bool Alive(ProcessIdentity identity) => NativeProcesses.GetIdentity(identity.ProcessId) == identity;
    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = Stopwatch.StartNew();
        while (!condition() && until.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(70);
        Assert.True(condition(), "Fixture condition was not observed in 10 seconds.");
    }
    private static async Task Cleanup(WindowsGameSessionService service, SessionId id)
        => _ = await service.TerminateAsync(new(id, TerminationTrigger.UserMenu), default);

    [Fact]
    public async Task CallerIdentityAndGameDedupDoNotCreateAnotherProcess()
    {
        using var service = new WindowsGameSessionService();
        var request = Request();
        try
        {
            var first = await service.LaunchAsync(request, default);
            var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.LaunchAsync(request, default)));
            Assert.All(attempts, r => { Assert.Equal(LaunchResultKind.ReplayedExisting, r.Kind); Assert.Equal(first.Session.RootProcess, r.Session.RootProcess); });
            var duplicate = await service.LaunchAsync(Request(game: request.GameId), default);
            Assert.Equal(LaunchResultKind.RejectedGameAlreadyActive, duplicate.Kind);
            Assert.Equal(request.SessionId, duplicate.Session.SessionId);
            Assert.Single(service.GetActiveSessions());
            Assert.Equal(TrackingCoverage.Incomplete, first.Session.Coverage);
            Assert.Equal(TerminationCapability.Partial, first.Session.Termination);
        }
        finally { await Cleanup(service, request.SessionId); }
    }

    [Fact]
    public async Task RegistrationIsQueryableBeforeNativeStartAndCancellationPreventsSideEffect()
    {
        using var service = new WindowsGameSessionService(); using var cancellation = new CancellationTokenSource();
        var request = Request();
        service.AfterRegistration = () =>
        {
            var registered = service.GetSession(request.SessionId)!;
            Assert.Equal(SessionState.Launching, registered.State); Assert.Equal(1, registered.Revision);
            Assert.Null(registered.RootProcess); cancellation.Cancel();
        };
        var result = await service.LaunchAsync(request, cancellation.Token);
        Assert.Equal(SessionState.LaunchFailed, result.Session.State);
        Assert.Equal(LaunchFailureKind.CancelledBeforeStart, result.Session.Failure!.Kind);
        Assert.Null(result.Session.RootProcess); Assert.Empty(service.GetActiveSessions());
    }

    [Fact]
    public async Task AlreadyCancelledEntryDoesNotRegister()
    {
        using var service = new WindowsGameSessionService(); var request = Request();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LaunchAsync(request, new CancellationToken(true)));
        Assert.Null(service.GetSession(request.SessionId));
    }

    [Fact]
    public async Task CancellationAfterCreationLeavesTheGameQueryableAndAlive()
    {
        using var service = new WindowsGameSessionService(); using var cancellation = new CancellationTokenSource();
        var request = Request(); service.AfterNativeStart = cancellation.Cancel;
        try
        {
            var result = await service.LaunchAsync(request, cancellation.Token);
            Assert.Equal(SessionState.Running, result.Session.State);
            Assert.True(Alive(result.Session.RootProcess!.Value));
            Assert.NotNull(service.GetSession(request.SessionId));
        }
        finally { await Cleanup(service, request.SessionId); }
    }

    [Fact]
    public async Task NativeMissingFileIsAFailureResultAndRetainedForReplay()
    {
        using var service = new WindowsGameSessionService();
        var request = new LaunchRequest(SessionId.New(), GameId.New(), new(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
        var result = await service.LaunchAsync(request, default);
        Assert.Equal(LaunchFailureKind.ExecutableNotFound, result.Session.Failure!.Kind);
        Assert.Equal(SessionState.LaunchFailed, result.Session.State);
        Assert.Equal(LaunchResultKind.ReplayedExisting, (await service.LaunchAsync(request, default)).Kind);
    }

    [Fact]
    public async Task BootstrapExitingKeepsVerifiedChildRunningAndDoesNotClaimWholeGameExit()
    {
        using var service = new WindowsGameSessionService();
        string report = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var request = Request($"spawn 30000 \"{report}\"");
        try
        {
            var result = await service.LaunchAsync(request, default);
            Assert.Equal(TrackingQuality.Contained, result.Session.Quality);
            await WaitUntil(() => File.Exists(report) && !Alive(result.Session.RootProcess!.Value) &&
                service.GetSession(request.SessionId) is { TrackedProcesses.Length: > 0 } current && !current.TrackedProcesses.Contains(result.Session.RootProcess.Value));
            var childOnly = service.GetSession(request.SessionId)!;
            Assert.Equal(SessionState.Running, childOnly.State);
            Assert.DoesNotContain(result.Session.RootProcess!.Value, childOnly.TrackedProcesses);
            var terminated = await service.TerminateAsync(new(request.SessionId, TerminationTrigger.UserMenu), default);
            Assert.Equal(TerminationOutcome.KnownProcessesTerminated, terminated.Outcome);
            Assert.True(terminated.UnknownSurvivorsPossible); Assert.False(terminated.ProvesGameEnded);
            Assert.Equal(SessionState.TrackingUncertain, service.GetSession(request.SessionId)!.State);
        }
        finally { await Cleanup(service, request.SessionId); if (File.Exists(report)) File.Delete(report); }
    }

    [Fact]
    public async Task IndependentObserversReplayAndReceiveTerminalAfterSlowConsumer()
    {
        using var service = new WindowsGameSessionService(); var request = Request();
        var launched = await service.LaunchAsync(request, default);
        try
        {
            await using var first = service.ObserveAsync(request.SessionId, default).GetAsyncEnumerator();
            await using var second = service.ObserveAsync(request.SessionId, default).GetAsyncEnumerator();
            Assert.True(await first.MoveNextAsync()); Assert.True(await second.MoveNextAsync());
            Assert.Equal(first.Current, second.Current);
            long revision = first.Current.Revision;
            await Cleanup(service, request.SessionId);
            var stopped = await service.StopTrackingAsync(request.SessionId, default);
            Assert.True(await first.MoveNextAsync()); Assert.True(await second.MoveNextAsync());
            Assert.Equal(stopped, first.Current); Assert.Equal(stopped, second.Current);
            Assert.True(stopped.Revision > revision); Assert.False(await first.MoveNextAsync()); Assert.False(await second.MoveNextAsync());
            await foreach (var late in service.ObserveAsync(request.SessionId, default)) Assert.Equal(stopped, late);
            Assert.Equal(LaunchResultKind.ReplayedExisting, (await service.LaunchAsync(request, default)).Kind);
        }
        finally { if (Alive(launched.Session.RootProcess!.Value)) await Cleanup(service, request.SessionId); }
    }

    [Fact]
    public async Task CancelledWatcherAndTrackingDismissalDoNotKillTheGame()
    {
        using var service = new WindowsGameSessionService(); using var cancellation = new CancellationTokenSource();
        var request = Request(); var result = await service.LaunchAsync(request, default); var identity = result.Session.RootProcess!.Value;
        try
        {
            await using var observer = service.ObserveAsync(request.SessionId, cancellation.Token).GetAsyncEnumerator();
            Assert.True(await observer.MoveNextAsync()); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await observer.MoveNextAsync());
            Assert.True(Alive(identity));
            var terminal = await service.StopTrackingAsync(request.SessionId, default);
            Assert.Equal(SessionState.TrackingStopped, terminal.State);
            Assert.Equal(SessionEndReason.TrackingDismissedByUser, terminal.EndReason);
            Assert.True(Alive(identity)); Assert.Empty(service.GetActiveSessions());
            Assert.Equal(terminal, await service.StopTrackingAsync(request.SessionId, default));
        }
        finally { KillFixture(identity); }
    }

    [Fact]
    public async Task ServiceDisposalLeavesNativeGameAlive()
    {
        var service = new WindowsGameSessionService();
        var result = await service.LaunchAsync(Request(), default); var identity = result.Session.RootProcess!.Value;
        service.Dispose();
        try { await Task.Delay(400); Assert.True(Alive(identity)); }
        finally { KillFixture(identity); }
    }

    [Fact]
    public async Task ScopedTerminationLeavesAnUnrelatedFixtureAliveAndRejectsStaleIdentity()
    {
        using var service = new WindowsGameSessionService();
        using var sentinel = Process.Start(new ProcessStartInfo(Fixture, "wait 30000") { UseShellExecute = false })!;
        var sentinelId = NativeProcesses.GetIdentity(sentinel.Id)!.Value;
        var request = Request(); var result = await service.LaunchAsync(request, default);
        try
        {
            var stale = sentinelId with { CreationTimeUtc = sentinelId.CreationTimeUtc.AddSeconds(-1) };
            Assert.False(NativeProcesses.TryTerminate(stale, null, stale, out bool skipped)); Assert.True(skipped);
            var closed = await service.TerminateAsync(new(request.SessionId, TerminationTrigger.EmergencyBinding), default);
            Assert.Contains(result.Session.RootProcess!.Value, closed.Terminated);
            Assert.True(Alive(sentinelId)); Assert.False(Alive(result.Session.RootProcess.Value));
        }
        finally { await Cleanup(service, request.SessionId); KillFixture(sentinelId); }
    }

    [Fact]
    public async Task UnknownObserverThrowsOnFirstMoveNext()
    {
        using var service = new WindowsGameSessionService();
        await using var observer = service.ObserveAsync(SessionId.New(), default).GetAsyncEnumerator();
        await Assert.ThrowsAsync<KeyNotFoundException>(async () => await observer.MoveNextAsync());
    }

    private static void KillFixture(ProcessIdentity identity)
    {
        if (!Alive(identity)) return;
        // Test cleanup ONLY: identity came from this test's own launch; never global-name matching.
        using var process = Process.GetProcessById(identity.ProcessId); process.Kill(entireProcessTree: true); process.WaitForExit(5000);
    }
}
