using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GLauncher.Core.Sessions;

[assembly: InternalsVisibleTo("GLauncher.Windows.Tests")]

namespace GLauncher.Windows;

/// <summary>
/// Owns only processes launched by this instance. Jobs contain native descendants, but cannot prove
/// that an already-running store did not receive an IPC handoff: generic launches stay Incomplete.
/// Disposing this service NEVER kills its games. No global process-name discovery is performed.
/// </summary>
public sealed class WindowsGameSessionService : IGameSessionService, IDisposable
{
    private sealed class Entry(SessionSnapshot snapshot)
    {
        public SessionSnapshot Snapshot = snapshot;
        public JobHandle? Job;
        public string? Diagnostic;
        public List<Channel<SessionSnapshot>> Observers { get; } = [];
    }
    private readonly object gate = new();
    private readonly Dictionary<SessionId, Entry> entries = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task tracker;
    private bool disposed;
    internal Action? AfterRegistration { get; set; }
    internal Action? AfterNativeStart { get; set; }

    public WindowsGameSessionService() => tracker = TrackAsync();

    public async Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entry entry;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (entries.TryGetValue(request.SessionId, out var previous))
                return new(LaunchResultKind.ReplayedExisting, previous.Snapshot);
            var duplicate = entries.Values.FirstOrDefault(e => e.Snapshot.GameId == request.GameId && e.Snapshot.IsPossiblyAlive);
            if (request.DuplicatePolicy == DuplicateLaunchPolicy.RejectIfActive && duplicate is not null)
                return new(LaunchResultKind.RejectedGameAlreadyActive, duplicate.Snapshot);
            entry = new Entry(new SessionSnapshot(request.SessionId, request.GameId, 1, SessionState.Launching,
                null, [], TrackingQuality.RootOnly, TrackingCoverage.Incomplete, TerminationCapability.Unsupported, DateTimeOffset.UtcNow));
            entries.Add(request.SessionId, entry);
        }
        AfterRegistration?.Invoke();
        // A cancellation token never owns this task or a native game. Once registered it always returns a result.
        return await Task.Run(() =>
        {
            lock (gate)
            {
                if (entry.Snapshot.IsTerminal) return new LaunchResult(LaunchResultKind.Started, entry.Snapshot);
                if (cancellationToken.IsCancellationRequested || disposed)
                {
                    Fail(entry, new(LaunchFailureKind.CancelledBeforeStart, "Launch cancelled before Windows created a process."));
                    return new LaunchResult(LaunchResultKind.Started, entry.Snapshot);
                }
                NativeProcesses.Started started;
                try { started = NativeProcesses.Start(request.Target); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or ArgumentException)
                {
                    var kind = error switch
                    {
                        FileNotFoundException => LaunchFailureKind.ExecutableNotFound,
                        UnauthorizedAccessException => LaunchFailureKind.AccessDenied,
                        Win32Exception { NativeErrorCode: 740 } => LaunchFailureKind.ElevationRequired,
                        Win32Exception { NativeErrorCode: 5 } => LaunchFailureKind.AccessDenied,
                        _ => LaunchFailureKind.StartFailed
                    };
                    Fail(entry, new(kind, error.Message));
                    return new LaunchResult(LaunchResultKind.Started, entry.Snapshot);
                }
                entry.Job = started.Job; entry.Diagnostic = started.Warning;
                Publish(entry, entry.Snapshot with
                {
                    State = SessionState.Running, RootProcess = started.Root,
                    TrackedProcesses = started.Root.CreationTimeUtc != DateTimeOffset.MinValue ? [started.Root] : [],
                    Quality = started.Job is null ? TrackingQuality.RootOnly : TrackingQuality.Contained,
                    Coverage = TrackingCoverage.Incomplete,
                    Termination = started.Root.CreationTimeUtc != DateTimeOffset.MinValue ? TerminationCapability.Partial : TerminationCapability.Unsupported
                });
                AfterNativeStart?.Invoke();
                // Cancellation after the side effect cannot detach or terminate the new process.
                Refresh(entry);
                return new LaunchResult(LaunchResultKind.Started, entry.Snapshot);
            }
        }).ConfigureAwait(false);
    }

    public SessionSnapshot? GetSession(SessionId id) { lock (gate) return entries.GetValueOrDefault(id)?.Snapshot; }
    public string? GetDiagnostic(SessionId id) { lock (gate) return entries.GetValueOrDefault(id)?.Diagnostic; }
    public IReadOnlyList<SessionSnapshot> GetActiveSessions()
    { lock (gate) return entries.Values.Select(e => e.Snapshot).Where(s => s.IsPossiblyAlive).ToArray(); }

    public async IAsyncEnumerable<SessionSnapshot> ObserveAsync(SessionId id, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<SessionSnapshot>(new BoundedChannelOptions(1)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropOldest, AllowSynchronousContinuations = false });
        Entry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(id, out entry!)) throw new KeyNotFoundException("Unknown session.");
            channel.Writer.TryWrite(entry.Snapshot);
            if (entry.Snapshot.IsTerminal) channel.Writer.TryComplete();
            else if (disposed) channel.Writer.TryComplete(new ObjectDisposedException(nameof(WindowsGameSessionService)));
            else entry.Observers.Add(channel);
        }
        try { await foreach (var snapshot in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return snapshot; }
        finally { lock (gate) entry.Observers.Remove(channel); }
    }

    public Task<SessionSnapshot> StopTrackingAsync(SessionId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry)) throw new KeyNotFoundException("Unknown session.");
            if (!entry.Snapshot.IsTerminal)
            {
                Publish(entry, entry.Snapshot with { State = SessionState.TrackingStopped, EndedAt = DateTimeOffset.UtcNow,
                    EndReason = SessionEndReason.TrackingDismissedByUser, Termination = TerminationCapability.Unsupported });
                entry.Job?.Dispose(); entry.Job = null;
            }
            return Task.FromResult(entry.Snapshot);
        }
    }

    public Task<TerminationResult> TerminateAsync(TerminationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!entries.TryGetValue(request.SessionId, out var entry) || entry.Snapshot.IsTerminal)
                    return new TerminationResult(TerminationOutcome.SessionNotActive, [], [], [], false, "No active session.");
                Refresh(entry);
                var snapshot = entry.Snapshot;
                if (snapshot.Termination == TerminationCapability.Unsupported || snapshot.RootProcess is not { } root)
                    return new TerminationResult(TerminationOutcome.NotSupported, [], snapshot.TrackedProcesses, [], true, "No verified owned processes can be closed. Stop tracking does not close a game.");
                List<ProcessIdentity> terminated = [], survivors = [], stale = [];
                // Refresh and validate before EACH signal. Descendants that spawn during the attempt are collected
                // on the next pass; a spawning storm is bounded and honestly reported as partial.
                var attempted = new HashSet<ProcessIdentity>();
                for (int pass = 0; pass < 4; pass++)
                {
                    Refresh(entry);
                    var next = entry.Snapshot.TrackedProcesses.Where(p => attempted.Add(p)).ToArray();
                    if (next.Length == 0) break;
                    foreach (var identity in next)
                    {
                        if (NativeProcesses.TryTerminate(identity, entry.Job, root, out bool skipped)) terminated.Add(identity);
                        else if (skipped) stale.Add(identity);
                    }
                }
                Refresh(entry);
                survivors.AddRange(entry.Snapshot.TrackedProcesses);
                // Generic EXEs have incomplete semantic coverage, even when a native job holds.
                return new TerminationResult(survivors.Count == 0 ? TerminationOutcome.KnownProcessesTerminated : TerminationOutcome.PartiallyTerminated,
                    terminated.ToImmutableArray(), survivors.ToImmutableArray(), stale.ToImmutableArray(), true,
                    survivors.Count == 0 ? "Verified processes closed. An external launcher handoff may still be running." : "Some verified processes could not be closed.");
            }
        }, CancellationToken.None);
    }

    private async Task TrackAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
                lock (gate)
                {
                    if (disposed) return;
                    foreach (var entry in entries.Values.Where(e => e.Snapshot.IsPossiblyAlive && e.Snapshot.RootProcess is not null)) Refresh(entry);
                }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private static void Fail(Entry entry, LaunchFailure failure)
        => Publish(entry, entry.Snapshot with { State = SessionState.LaunchFailed, Failure = failure, EndedAt = DateTimeOffset.UtcNow });

    private static void Refresh(Entry entry)
    {
        if (entry.Snapshot.IsTerminal || entry.Snapshot.RootProcess is not { } root) return;
        List<ProcessIdentity> members;
        try { members = NativeProcesses.Members(entry.Job, root); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            entry.Diagnostic = "Process enumeration is uncertain: " + error.Message;
            members = entry.Snapshot.TrackedProcesses.Where(p => NativeProcesses.GetIdentity(p.ProcessId) == p).ToList();
        }
        var resolution = SessionStateResolver.Resolve(entry.Snapshot.State, members.Count > 0, TrackingCoverage.Incomplete);
        var capability = members.Count > 0 ? TerminationCapability.Partial : TerminationCapability.Unsupported;
        if (entry.Snapshot.State == resolution.State && entry.Snapshot.Termination == capability && members.SequenceEqual(entry.Snapshot.TrackedProcesses)) return;
        Publish(entry, entry.Snapshot with { State = resolution.State, TrackedProcesses = members.ToImmutableArray(),
            Termination = capability, Coverage = TrackingCoverage.Incomplete, EndReason = resolution.EndReason });
    }

    private static void Publish(Entry entry, SessionSnapshot next)
    {
        entry.Snapshot = next with { Revision = entry.Snapshot.Revision + 1 };
        foreach (var observer in entry.Observers)
        {
            observer.Writer.TryWrite(entry.Snapshot);
            if (entry.Snapshot.IsTerminal) observer.Writer.TryComplete();
        }
        if (entry.Snapshot.IsTerminal) entry.Observers.Clear();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; lifetime.Cancel();
            foreach (var entry in entries.Values)
            {
                entry.Job?.Dispose(); entry.Job = null;
                foreach (var observer in entry.Observers) observer.Writer.TryComplete(new ObjectDisposedException(nameof(WindowsGameSessionService)));
                entry.Observers.Clear();
            }
        }
        // Keep the cancellation source alive until the polling task has observed cancellation.
        _ = tracker.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
    }
}
