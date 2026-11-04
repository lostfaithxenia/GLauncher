# GLauncher current state

## Confirmed setup

- Live repository: https://github.com/lostfaithxenia/GLauncher
- SYN selected PUBLIC visibility after the earlier private request.
- This is a fresh repository, not a reuse of an older launcher.
- Initial AGENTS.md commit: 7946adb60fba6f4d7509ee2a894c8a2bfe5cd98e.
- The current product brief and accepted concept are included in the setup commit.
- Latest written requirements override older concept details: 9:16 covers and prominent profiles.
- Codex has GitHub connector read/write permissions and can create file commits, issues, branches and PRs.
- Git and .NET SDK 10.0.403 / WindowsDesktop runtime 10.0.14 are available locally.
- Claude currently has no verified GitHub access or Windows execution environment. SYN relays its drafts.

## Current work boundary

SYN authorized the initial kit commit and three planning/implementation issues:
1. ADR-001, solution skeleton and UI-independent Core contracts — Claude drafts; Codex reviews/compiles.
2. Windows game-session tracking and focus/input policy — Codex, dependent on the contracts.
3. Library shell, 9:16 grid and one platform-cloud aura — Claude, dependent on the agreed shell/contracts.

The rendering spike is preserved in draft PR #4. Claude's revision-2 Core is reviewed and compiled in draft PR #5, head 5deb77210a608efcaa788d09ef88dfc902f3726b (57 pure Core tests).

Codex now owns the first-alpha integration of #2 and #3 on `codex/2-first-alpha`, draft PR #6, based on that reviewed Core. This branch adds WinUI UI, Windows native session/input tracking, SQLite persistence, generated assets and portable publishing. Core contracts are unchanged. The original source checkpoint is ccbdf6e25baddc17baab65ccaccfa92eb2b09d5d; subsequent fixes and evidence appear in the same PR.

Windows Release build passes with 0 warnings/errors. Core 57 and Windows native/storage 14 tests pass. Actual app checks have exercised editing/saving/discarding, profiles, 9:16 frames and resize, 200-item virtualization, theme persistence, MP4 decoding/pause, focus-gated fullscreen and controlled process launch. See the alpha validation report for exact run, skips, measured resource conditions and packaged-build status. A test harness or source checkpoint alone is not a delivery claim.

Feature changes use PRs. Neither assistant merges its own work. Initial documentation publication is the specifically authorized setup step.

## Not yet established

- An independently reviewed/merged alpha. The current distribution is a portable alpha, not a signed installer.
- Hosted CI results must be checked separately; a Windows workflow is included, not claimed to have passed merely because it exists.
- AI/metadata providers, online presence or universal achievement integration.
- Verified download connectors.
- Physical-controller / real-game coverage for GLauncher.
- Rendering-spike results; those will be recorded with actual measurements.

## Delivered alpha checkpoint

Compiled source 177aa6197b1f9ba5e6c1196165b03ff5a1d399f6 produced the portable 0.1.0-alpha.1 package. Final packaged run: exit 0, 31 checks passed, zero skipped. Local Core/Windows/storage tests: 57+14 passing. Exact-source hosted Windows run 41760007881 also passed build (0 warnings/errors), tests, publish checks and artifact upload. See docs/validation/ALPHA-0.1.md for commands, evidence, ZIP digest, captures, short resource samples and remaining real-game/controller/hotkey coverage. The app is an early local alpha; provider features remain unconnected and both feature PRs remain unmerged.

## Next handoff

Read PR #6 and docs/ALPHA.md plus its validation report before continuing. Next work is a real-game and physical-controller pass, input/termination edge cases under external store handoffs, visual polish and provider decisions. The emergency one-second Delete hold, unrelated-app guard and unfocused notification proposal remain unresolved product defaults. Preserve data and feature branches; neither assistant merges its own work.
