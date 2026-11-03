# ADR-001: Windows stack, solution layout and Core contracts

- Status: **Proposed, revision 2** (draft by Claude via SYN; revision 1 reviewed and Windows-tested by Codex in PR #5 at fe0d6f8; revision 2 is UNCOMPILED)
- Issue: #1. Evidence: PR #4 (`docs/validation/RENDERING-SPIKE.md`, Codex, draft/unmerged)
- Owner: Claude (draft) · Reviewer/compiler: Codex · Neither assistant merges its own work.

## Revision history

- Rev 1: original draft. Codex compiled it (Release build with `-m:1 --disable-build-servers`, 35 tests passed, per PR #5) and left five review comments.
- Rev 2: responds to those five comments by changing the session contract only (see "How the contracts meet..."). Input, notification, provider, storage and game contracts are unchanged.

## Context

GLauncher is a fresh Windows desktop launcher: animated, video backgrounds, 9:16 platform-aura library,
reliable game-session tracking, controller support, low overhead while a game runs. No production code exists.
.NET 8 support ends 10 November 2026, so a new project should not target it (Codex, citing Microsoft's support policy).

## Decision (proposed)

1. **.NET 10 LTS + WinUI 3** (Windows App SDK) for the shell.
2. **UI-independent `GLauncher.Core`**, plain `net10.0` (no `-windows` TFM, no UI framework, no Win32 handles).
3. **Native Composition** for the first platform aura. Do not add Win2D and SkiaSharp without a concrete need.
4. **SQLite** for persistence — *proposed, not implemented or benchmarked*. Library choice (e.g. Microsoft.Data.Sqlite) deferred to #2/storage work.
5. **WPF stays a documented fallback**, not a parallel implementation.

## Alternatives considered

| Option | Why not chosen (yet) |
|---|---|
| WPF | Compiled and ran in the spike; heavier in that exploratory run (see caveats), weaker modern composition story. Kept as fallback. |
| Tauri / Electron | Not measured. Web stack adds a second runtime and process; deferred unless WinUI blocks us. **Untested claim.** |
| .NET 8 | Support ends 10 Nov 2026. |

## Evidence and its limits (from PR #4, by Codex)

- Both probes compiled with zero warnings/errors; final WinUI apphost run exited 0 with six pause/resume checks passing.
- WinUI: root Loaded 2,427 ms; foreground median ~241.6 MiB working set, ~2.7% CPU (12 logical processors); minimized median ~244 MiB (memory stays allocated while paused).
- WPF fallback (not controlled A/B): ~367.9 MiB working set, ~10% CPU foreground. Effects and startup events differ, so **this is not a framework ranking**.
- **Not established:** GPU utilisation, WinUI visual quality (inspection timed out), real games, controllers, DPI/resize/light mode, SQLite, clean-machine packaging, presented FPS.
- The WPF spike reported `IsActive=true` while minimized, which is why Core carries focus facts separately.

## Deployment prerequisites (from the spike; not a final packaging decision)

Windows x64; Windows App SDK UI component (`Microsoft.WindowsAppSDK.WinUI` 2.6.5 built; prefer only needed components over the full metapackage); .NET runtime framework-dependent in the probe. MSIX vs unpackaged is **undecided**.

## Solution layout

```
GLauncher.slnx                      (created by `dotnet new sln`; see Build below)
build/GLauncher.Common.props        shared compiler settings, imported explicitly (spikes/ unaffected)
src/GLauncher.Core/                 contracts + pure policies. net10.0. No UI/Win32.
tests/GLauncher.Core.Tests/         xunit, runs anywhere .NET 10 runs
-- planned, NOT part of this change --
src/GLauncher.Windows/              net10.0-windows: Win32 session/focus/input adapters (#2)
src/GLauncher.Storage.Sqlite/       IGameLibraryStore implementation
src/GLauncher.App/                  WinUI 3 shell (#3 builds on this)
```

## Service ownership and boundaries

| Boundary (Core interface) | Implemented in | Owner |
|---|---|---|
| `IGameSessionService` | `GLauncher.Windows` | Codex (#2) |
| `IInputPolicy` (default is pure, in Core) + Win32 focus/hotkey adapter | Core / `GLauncher.Windows` | Core: Claude draft; adapter: Codex |
| `INotificationPolicy` (default pure, in Core) + presenters | Core / App | Claude draft |
| `IGameLibraryStore` | `GLauncher.Storage.Sqlite` | Codex |
| `IArtworkProvider`, `IMetadataProvider` | per-provider projects, later | unassigned; provider and cost undecided |
| `IOwnedGameRecommender`, `IDiscoverRecommender` | Core (local owned), later provider | Claude draft |

No plugin framework. Interfaces exist only where two assistants need an agreed seam.

## How the contracts meet issue #1's acceptance criteria (revision 2)

- **Separate focus facts:** `LauncherFocusFacts` = launcher foreground, minimized, text-entry, tracked-game-focus, active session. `DefaultInputPolicy` derives navigation/shortcut/fullscreen/emergency decisions; F is denied when minimized, unfocused, or typing.
- **Recoverable launch identity (review #1):** `LaunchRequest.SessionId` is caller-supplied and doubles as the idempotency key. The service registers the session (Launching, Revision 1) **before any native side effect**, so `GetSession`/`ObserveAsync`/`TerminateAsync` work even if the launch call is cancelled or its result lost.
  - Same `SessionId` again: `ReplayedExisting`, nothing launched twice.
  - Same game already possibly alive: default `DuplicateLaunchPolicy.RejectIfActive` returns `RejectedGameAlreadyActive` with the existing session and creates no new one; `AllowConcurrent` is opt-in.
  - Token already cancelled on entry: `OperationCanceledException`, nothing registered. Cancelled after registration but before the process starts: no process, session becomes `LaunchFailed(CancelledBeforeStart)`. Cancelled after a process started: the game is never killed and the session stays tracked and queryable.
- **Uncertain tracking can be resolved, non-destructively (review #2):** `StopTrackingAsync` moves a non-terminal session to the new terminal state `TrackingStopped` (`EndReason = TrackingDismissedByUser`) without touching any process. It is distinct from `Exited`, which now promises *observation*. `TrackingStopped` leaves `GetActiveSessions` and gameplay notification policy, and the UI must not say "game closed". `SessionStateResolver` keeps `Running` while any verified child is alive even if the root exited; with nothing alive it yields `Exited` only for `TrackingCoverage.Complete`, otherwise `TrackingUncertain`.
- **Observation contract (review #3, `IAsyncEnumerable` kept):** atomic replay of the current snapshot first, then later changes; independent per-observer queues; strictly increasing `Revision` with gaps allowed under coalescing; a slow observer still gets the latest and the terminal snapshot; the stream completes after the terminal snapshot (terminal sessions replay it and complete); unknown id throws `KeyNotFoundException`; cancelling removes only that observer; snapshots are immutable (`ImmutableArray`).
- **Termination coverage (review #4):** `Supported` now means *complete* verified coverage (invariant: Supported implies `TrackingCoverage.Complete`); `Partial` means verified-owned members can be terminated but the game may survive; `Unsupported` means do not offer it. `TerminationResult` adds `UnknownSurvivorsPossible` and `SkippedStale` (identities that failed revalidation at call time and were never signalled). Outcomes now include `KnownProcessesTerminated`. Only `ProvesGameEnded` (Terminated + no known survivors + no unknown survivors) may mark a session `Exited/TerminatedByLauncher`; an emptied known subset alone may not.
- **Stable identity:** `SessionId` plus `ProcessIdentity(PID, creation time)`; bare PIDs are never identities, and identity is revalidated at the actual termination call.
- **Exit policy (review #5):** `LauncherExitPolicy` default stays **LeaveGameRunning**. `TerminateGame` is implemented as explicit scoped `TerminateAsync` calls (trigger `LauncherExitPolicy`) before the launcher exits.
- **Return to launcher:** with a live session and launcher focus, navigation is re-enabled (tested).
- **Real vs demo:** `ProviderKind`, `DataProvenance`, `NotificationDecision.DemoLabelRequired`; `ProviderResult` never fabricates a value on NotConfigured/Cancelled/Failed.
- **9:16:** `CoverSpec` is the single rule; `GeneratedCover` is held in a temp file and is not applied until accepted.
- **Owned vs Discover:** separate types; `DiscoverRecommendation` has no play action and an explicit `OfferKind`.

## Adapter notes for #2 (Codex)

- **Leave `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` off** (accepting review #5). Releasing the last job handle, including on a launcher crash, must not end a game. A crash of the launcher therefore leaves the game running, which is the documented behaviour.
- Job objects are evidence, not proof: launchers may break away. Report `TrackingQuality`, `TrackingCoverage` and `TerminationCapability` honestly. `Complete`/`Supported` should be rare and earned (e.g. a job that held, or a direct game executable with no handoff signature).
- The emergency binding is observed by the adapter; `IInputPolicy.EmergencyCloseArmed` only says when it may fire. It must resolve to `TerminateAsync(sessionId, ...)` for a known session, never to a PID taken from the foreground window.
- Publish a new immutable snapshot (Revision + 1) per state change; keep terminal snapshots for replay at least until launcher exit (the adapter may cap how many are retained).
- Call `SessionStateResolver.Resolve` rather than re-deriving state rules per adapter.

## Unresolved (need SYN or evidence; deliberately not decided here)

0. UI for `TrackingUncertain` (a "Is the game still running?" prompt with a Stop tracking action) belongs to #3; not designed here.

1. Emergency binding default. Proposed: **Delete held ~1 s** because Delete is a real game/text key. SYN may prefer an instant press.
2. Whether the emergency key may fire while a *different* app is focused (frozen game that lost focus). Currently: only while the game has focus, or optionally while the launcher is focused.
3. Launcher visible but behind another app, no game: treated as `BackgroundIdle`. Brief only defines "minimized".
4. Inactivity timeout and any automatic close (only the gate in `InactivityPromptPolicy` exists).
5. Gameplay notification defaults (proposed all off, opt-in).
6. Esc / unsaved-changes handling is a UI-layer concern for #3; no Core contract yet.
7. Windows banner suppression during fullscreen games: **untested**, no overlay claim.
8. MSIX vs unpackaged, SQLite library, CI.

## Build and test (to be run by Codex on Windows; not run by Claude)

```powershell
dotnet new sln -n GLauncher                       # creates GLauncher.slnx on .NET 10
dotnet sln add src/GLauncher.Core tests/GLauncher.Core.Tests
dotnet build -c Release
dotnet test  -c Release
```

## Not verified by this change

Revision 2 has not been compiled or run. (Revision 1 was compiled and tested by Codex.) Test package versions (`Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.0.0`) are from memory and may need bumping. Unit tests exercise pure policy logic with fixtures only; they are not evidence of real focus, hotkey, process or controller behaviour.
