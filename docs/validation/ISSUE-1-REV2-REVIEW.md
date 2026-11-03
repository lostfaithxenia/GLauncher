# Issue #1 revision 2 — Codex validation and adapter decisions

PR: [#5](https://github.com/lostfaithxenia/GLauncher/pull/5), still draft and unmerged.
Base: fe0d6f83f38778c6de2d8e781d3ff9aaa0203e04.
Claude revision-2 source commit: 06463865103f8c98973b1572ed43222aa9636679.
Archive SHA-256: 3fa887e958dcebe8a8b1b4a13b814ebcadadef01c0f917a3072772d1b68680a8.

## Result

The four requested files were applied exactly as received. No compile fixes, package bumps or Codex contract rewrites were needed. The revision README is retained locally as provenance and was not added to the product root.

Windows .NET SDK 10.0.403 / runtime 10.0.14:
- `dotnet build -c Release -m:1 --disable-build-servers`: exit 0, **0 warnings, 0 errors**, elapsed 31.70 seconds.
- `dotnet test -c Release -m:1 --disable-build-servers` with TRX logging: exit 0, **57 passed, 0 failed, 0 skipped**, reported test duration 159 ms.
- TRX counters confirm 57 total/executed/passed.
- The previously failing plain command path was not rerun. The established serialized workaround was used; no claim is made that plain commands now work.

These are Core policy/helper fixtures. Launch registration, idempotency, watcher broadcasting, cancellation races, actual process coverage, Job Objects, hotkeys, controllers and real termination still require #2 implementation and Windows fixture tests.

## Original review findings

| Finding | Revision-2 result |
| --- | --- |
| R1: recoverable launch identity | Addressed in the contract: caller SessionId, registration before side effects, explicit duplicate outcomes and cancellation phases. |
| R2: uncertain tracking resolution | Addressed: StopTrackingAsync, TrackingStopped and its separate reason; live verified children retain Running. |
| R3: observation delivery | Addressed: initial replay, independent subscribers, revisions/coalescing, terminal completion and immutable collections are specified. |
| R4: termination coverage | Addressed in the data contract: separate coverage/capability, unknown survivors, stale identities and explicit end-proof result. |
| R5: kill-on-close | Addressed: the exception is removed and normal terminate-on-exit uses explicit TerminateAsync. |

The original blocking API gaps are closed at the contract level. The interfaces are implementable for #2 under the conservative decisions below. This is technical readiness to implement, not proof that the documented service behavior already exists.

## Answers and decisions for the adapter

### Coverage: default stricter, including apparently direct EXEs

I will explicitly initialize coverage as **Incomplete**, termination as **Unsupported** until owned members are verified, and quality as RootOnly at registration. Verifying a safe owned subset can justify Partial; Supported requires the stronger Complete claim.

An EXE path, name or lack of a known handoff signature does not establish whole-game coverage. A job constrains its process membership, not every possible external continuation. Complete should only be promoted for a clearly defined, verified launch scope with documented limitations; unverified launchers/handoffs remain Incomplete. This is a conservative design judgment based on [Microsoft's process-membership/breakaway documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).

**Advisory:** the supplied enum currently has Complete as numeric zero. It was preserved unchanged. #2 must use explicit conservative initialization rather than default(TrackingCoverage). Making Incomplete zero is a reasonable follow-up before a persisted enum format is fixed.

### LaunchResult: suitable

The result object works for the adapter. Started means a new session was registered, not necessarily that a playable process is already running. Callers must inspect Session.State/Failure and observe the returned session.

RejectedGameAlreadyActive carries the existing session; callers must use that returned SessionId rather than subscribe to the rejected request's ID. ReplayedExisting must not create another process. Registry/idempotency/duplicate checks and native-start/cancellation ordering need atomic adapter tests.

### Terminal retention: retain until launcher/service exit

For the first implementation, I will retain every terminal snapshot and accepted SessionId for the lifetime of the launcher session. I will not use the ADR's optional count cap during that lifetime. That preserves initial/terminal replay and idempotent retries.

A later eviction policy needs an explicit replay/idempotency design; silently forgetting IDs could permit a repeated request to launch again. Full-snapshot retention is the conservative baseline here, not an implemented storage feature.

### Other implementation constraints

- Never use kill-on-job-close; watcher removal and launcher disposal remain passive.
- Revalidate membership and process identity when terminating. Access/identity uncertainty is not proof of exit.
- Treat TrackingStopped as user-ended tracking, never as a claim that the game closed.
- Snapshot/state updates and StopTracking/launch/termination races need serialization in #2.
- Existing unresolved product proposals are unchanged: one-second Delete hold, no unrelated-app emergency binding, unfocused/no-game notification state treated like minimized.

## Next work

#2 and #3 can proceed as dependent branches based on the reviewed PR #5 head. Main still lacks this unmerged foundation. #2 owns native adapter implementation/fixtures; #3 owns the UI and must label its fixtures. Neither assistant merges its own work.

New Codex additions in this follow-up are this report and sanitized validation/provenance records only. Earlier revision-1 evidence remains historical.
