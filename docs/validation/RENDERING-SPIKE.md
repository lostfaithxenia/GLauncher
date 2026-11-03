# GLauncher — repository setup and Windows rendering spike

## Delivered setup

- Live repository: [lostfaithxenia/GLauncher](https://github.com/lostfaithxenia/GLauncher).
- SYN explicitly chose **public** visibility, superseding the initial private request.
- Initial shared instructions: commit 7946adb60fba6f4d7509ee2a894c8a2bfe5cd98e.
- Complete kit and accepted PNG: commit 89fee3b2931456ee8f3b4266961f36c3eb103eb9 on main.
- [Issue #1](https://github.com/lostfaithxenia/GLauncher/issues/1): ADR, skeleton, Core contracts — Claude drafts; Codex reviews and compile-checks.
- [Issue #2](https://github.com/lostfaithxenia/GLauncher/issues/2): Windows sessions and input/focus — Codex, after #1.
- [Issue #3](https://github.com/lostfaithxenia/GLauncher/issues/3): library and cloud UI — Claude drafts, after #1.
- Disposable spike branch: codex/1-rendering-spike. No feature PR has been merged.
- Earlier launcher repositories were not modified or reused.

## Actual environment capabilities

Codex can read and write repository contents through the GitHub connector, create branches/issues/PRs and publish commits. Those capabilities were exercised during setup. Public repository cloning/fetching works through Git.

The GitHub connector has no repository-creation action. Its web fallback was signed out and Git had no saved credential, so SYN created the repository. Command-line authenticated git push is not configured; publishing uses the GitHub connector.

Local Windows tools: Git 2.53.0.windows.3, .NET SDK 10.0.403, runtime 10.0.14 and MSBuild 18.9.15. Both probe projects compiled with zero warnings/errors. Windows executables and automated runtime-observation checks were actually run.

Claude currently has no verified repository/write access or Windows execution environment. Public visibility may help it read files if its session has web access, but does not establish those capabilities. SYN relays its draft files to Codex.

No Claude Core draft has been supplied yet, so no Core contracts or Core unit-test suite have been compile-verified.

## Recommendation for ADR-001

Keep the proposed native WinUI 3 direction, but use **.NET 10 LTS**, not .NET 8. .NET 8 support ends on 10 November 2026; .NET 10 is supported through 14 November 2028. See [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

The tested WinUI project pins Microsoft.WindowsAppSDK.WinUI 2.6.5. The full SDK metapackage began restoring unrelated AI/ML components; the UI component package built successfully. Prefer only the components the application actually uses. This is a spike recommendation, not a final package/deployment decision.

Use native Composition for the first aura. Do not add Composition, Win2D and SkiaSharp simultaneously without a concrete need. Keep Core UI-independent and retain SQLite as the proposed storage choice. SQLite has not been implemented or benchmarked in this spike.

A WPF fallback also compiled and ran. Its cached probe used more foreground CPU/memory in this exploratory run, but the effects, startup events, background activity and cache conditions differ. These are not controlled A/B measurements and do not prove a framework-wide performance ranking.

The probe validates a deployment path on this machine, not a clean-machine installer. Windows App SDK native files are self-contained in this probe; the .NET runtime itself remains framework-dependent. Final deployment and production packaging belong in the ADR/build work.

## Test conditions

- Windows x64; 12 reported logical processors.
- Silent synthetic H.264 MP4, 1280 × 720 at 30 fps, looping 12 seconds.
- One exact 9:16 diagnostic card, 236.25 × 420, and ten soft cloud layers.
- About 55 seconds per automated run.
- App-owned window transitions: foreground, minimized, restored, another same-process focus-fixture window, return and close.
- No real game, external process tracking, controller input, network provider or production artwork.
- Samples use actual process working set/private bytes and CPU time.
- CPU percentages are normalized across the 12 reported logical processors.
- The first two samples in each phase are excluded. Foreground rows must actually be active and not minimized.
- Other desktop applications and build activity were not fully controlled.
- Startup events are different across implementations and are labelled; neither is a measured time to first presented frame.
- WPF appearance was inspected through Computer Use. WinUI inspection waited for a new-app approval and timed out; its appearance is **not visually verified**.

## Final WinUI run

55 samples; runtime exit code **0**; no logged media errors.

- Process start → root Loaded event: **2,427 ms**.
- First media-ready sample: **1.36 seconds after probe-object construction**, a different clock origin from process startup.

| Phase | Stable samples | Median working set | Median private memory | Median total CPU |
| --- | ---: | ---: | ---: | ---: |
| Foreground, video + aura | 8 | 241.63 MiB | 161.56 MiB | 2.70% |
| Minimized | 8 | 244.11 MiB | 163.38 MiB | 0.00% |
| Foreground resumed | 8 | 245.82 MiB | 165.56 MiB | 2.44% |
| Focus fixture | 8 | 252.58 MiB | 171.32 MiB | 0.00% |
| Return | 8 | 253.84 MiB | 171.36 MiB | 1.16% |

Zero is a median of quantized CPU samples, not a promise of zero background work. Memory remains allocated while playback is paused.

All six recorded lifecycle checks passed:
1. Minimized media position remains paused.
2. Minimized cloud animation clocks are stopped.
3. Focus-away media position remains paused.
4. Focus-away cloud animation clocks are stopped.
5. Actual foreground samples show playback progress and active animation clocks.
6. Return samples show playback progress and restarted animation clocks.

These checks do not prove frame presentation rate or that a paused decoder has completely shut down.

## WPF fallback run

54 samples; runtime exit code 0; all six equivalent observation checks passed.

- Process start → ContentRendered event: **3,620 ms**.
- Foreground: median **367.90 MiB working set**, **318.42 MiB private**, **10.02% total CPU**.
- Minimized: median **369.39 MiB working set**, **304.04 MiB private**, **0.13% total CPU**.
- Focus fixture: median **358.93 MiB working set**, **292.68 MiB private**, **1.37% total CPU**.

This quick blur/cached-layer implementation is not production-tuned. Do not use these figures as a general WPF versus WinUI claim.

## GPU evidence

Windows GPU Engine performance counters returned "The specified object was not found on the computer." Reliable per-process GPU utilization was therefore unavailable.

nvidia-smi could read the laptop's NVIDIA device, but its idle whole-device reading is not the app's GPU usage, particularly on a hybrid-GPU machine. No GPU percentage is claimed. WPF reporting rendering tier 2 is capability information, not measured GPU load.

## Failures retained and fixes

- The first restore tried a restricted per-user NuGet folder. A workspace-local CLI/config environment resolved that access issue.
- The full Windows App SDK metapackage restore was cancelled; a UI-only package build then succeeded.
- A WinUI DLL launch through the dotnet host lacked the required native apphost/manifest context and failed before telemetry. Use the apphost EXE.
- The first timed WinUI apphost run recorded the complete cycle but exited with 0xC000027B during shutdown. It is NOT counted as a passing run.
- The corrected run adds a closing guard, captures the dispatcher queue on its owning thread and detaches MediaPlayerElement before disposing MediaPlayer. It completed the cycle and exited 0.
- The reproducible runner keeps the parent alive with Start-Process -Wait and captures the GUI process's exit status.
- The runtime probes use synthetic data and are not the first alpha.

## Core contract feedback for Claude

- Keep game-session lifetime independent from a UI window or watcher subscription.
- Represent launch failures and unknown/detached tracking; a bootstrap parent exiting is not proof of a game exit.
- Use session identities plus process creation identity, not bare reusable PIDs.
- Expose whether safe emergency termination is supported; do not promise Job Objects cover every external launcher.
- Disposing/cancelling a watcher should not kill the game. Emergency termination is an explicit operation.
- Include foreground, minimized, text-entry and game-focus facts separately. WPF reported IsActive=true while minimized in the probe, so a lone active flag is insufficient.
- Returning to the launcher should restore UI controls while a game may still be alive.
- Keep configurable emergency-close policy separate from ordinary background navigation.
- Notification priority should consume app/session state; provider-backed achievements and presence remain separate integrations.
- After #1, #2 adapters and #3 UI can develop against the same reviewed contracts in parallel. Their final integration is shared work; they should not circularly block each other's initial implementation.

[Microsoft Job Object reference](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) documents child/breakaway behavior and the explicit kill-on-close flag. Do not infer universal session ownership from the primitive.

## What remains untested

Production styling or final aura quality, actual presented FPS, GPU utilization, DPI/resize/light-mode coverage, physical controllers, real games/anti-cheat/store handoffs, SQLite, notifications, AI/metadata providers, download integrations, clean-machine packaging and Claude's future Core contracts.

The next deliverable is Claude's ADR/Core draft through SYN. Codex can review and compile-check that draft before issue #2 feature work.
