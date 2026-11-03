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

Codex is performing an isolated rendering spike for video playback/pause and aura behavior. It does not establish a production framework decision or a finished alpha.

Feature changes use PRs. Neither assistant merges its own work. Initial documentation publication is the specifically authorized setup step.

## Not yet established

- A production framework or final Core contracts.
- A production application, installer or configured CI.
- AI/metadata providers, online presence or universal achievement integration.
- Verified download connectors.
- Physical-controller / real-game coverage for GLauncher.
- Rendering-spike results; those will be recorded with actual measurements.

## Next handoff

Claude should read the design image separately from the text prompt and provide reviewable ADR/Core draft files through SYN. Codex publishes and compile-checks them with attribution before dependent feature work.
