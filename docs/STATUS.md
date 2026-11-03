# GLauncher status

## Repository

- https://github.com/lostfaithxenia/GLauncher — PUBLIC by SYN's explicit choice.
- Kit, shared instructions and accepted concept are on main at 89fee3b2931456ee8f3b4266961f36c3eb103eb9.
- This branch, codex/1-rendering-spike, contains disposable probe source and actual validation evidence. It is not the production skeleton.

## Tasks and ownership

- [#1 Architecture/Core](https://github.com/lostfaithxenia/GLauncher/issues/1): Claude drafts through SYN; Codex reviews and compile-verifies.
- [#2 Windows sessions/input](https://github.com/lostfaithxenia/GLauncher/issues/2): claimed by Codex; waits for #1 contracts.
- [#3 Library/aura UI](https://github.com/lostfaithxenia/GLauncher/issues/3): Claude drafts after #1 establishes shell/contracts.
- Codex's isolated #1 spike owns only spikes/ and validation/status evidence. It does not replace Claude's ADR/Core work.
- After #1, #2 and #3 can proceed against agreed contracts; final integration links them, not a circular implementation dependency.
- Neither assistant merges its own work. No feature PR has been merged.

## Verified in the Windows spike

- SDK 10.0.403 / runtime 10.0.14; GitHub connector commits/issues/branches work.
- WPF and WinUI probes compile with zero warnings/errors.
- Final WinUI run exited 0, with 55 samples and six passing observed pause/resume checks.
- Cached WPF fallback exited 0 and passed equivalent observation checks.
- [Detailed measurements, failures and limits](validation/RENDERING-SPIKE.md).
- Recommended for Claude's ADR: .NET 10 + WinUI 3, UI-independent Core and proposed SQLite. This remains a recommendation, not a finalized production architecture.
- GPU utilization was not measurable through available Windows counters.
- WPF was visually inspected. WinUI inspection timed out waiting for new-app approval; it is not visually verified.

## Access and next action

Codex can publish through the connected GitHub account, compile .NET and run Windows validation. The connector has no create-repository action; SYN created this repository. Authenticated CLI git push is not configured.

Claude has not established GitHub/write or Windows execution access. SYN relays its drafts. A public URL does not itself grant tool capability or write permission.

Next: Claude supplies ADR-001 and Core/skeleton draft files, based on current main. Codex reviews and compiles them before #2 feature implementation. There is no Core draft to validate yet.

No production alpha, installer, provider integration, controller/game compatibility claim or download connector is established by this spike.
