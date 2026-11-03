# Proposed Codex / Claude workflow

This is the project's proposed collaboration convention, not a claim that branch protection, CI, accounts or task assignments are already configured.

## Initial setup

- Use the actual dedicated GLauncher repository selected by the owner.
- Confirm both environments can access it and what each can read, edit, push and run.
- Give each assistant a separate checkout/worktree and branch.
- One assistant establishes the initial shell and contracts; the other reviews them before dependent parallel implementation.
- Record the chosen stack, actual build/test commands and package layout in the repository once decided.
- Keep the accepted design image with the brief. Mark later changes explicitly.

## One bounded task

Before editing, read the latest issue, relevant PRs and base revision. Add a claim comment:

Assistant: Claude or Codex
Task: issue number and user-visible result
Branch: actual branch
Base: actual base SHA
Owned subsystem/files: ...
Dependencies: ...
Acceptance criteria: ...
Validation: ...
State: claimed / in progress / review / blocked / complete

Check for competing claims immediately before starting. If claims conflict, resolve ownership in the issue and work on independent tasks meanwhile. A comment is a convention, not an atomic lock.

Branch conventions:
- claude/<issue>-<short-purpose>
- codex/<issue>-<short-purpose>

Do not share a dirty working tree, force-push over a partner's branch, or reset/clean away their files.

## Contracts and overlap

Declare one active owner for shared models, app startup, navigation root, design tokens, dependency manifests/lockfiles, storage schemas and CI configuration.

Coordinate changes to service contracts before writing both sides. Keep shared infrastructure changes in focused PRs. Do not solve a blocked dependency by inventing an incompatible duplicate service.

Suggested division, not assigned work:
- Codex: Windows lifecycle, input policy, controllers, persistence, build/CI, packaging.
- Claude: interface shell, library/aura interactions, profiles and creation screens.
- Either can take a different issue once ownership is clear.

## Review and merge

Open a focused PR with its issue, changed behavior, validation and screenshots where relevant. State which parts use fixtures or external integrations.

The other assistant reviews correctness, product fit, performance and contracts. Identify reviews with a plain "Codex review" or "Claude review" comment if appropriate; do not invent GitHub accounts or independent approvals.

Follow the actual repository's merge policy and the owner's existing authorization. Resolve substantive review findings and required checks before merge. Routine edits and draft PRs do not require repeated user permission.

## Handoff

Include:
- Issue and PR links.
- Branch and head SHA.
- Completed behavior.
- Commands/check results.
- Evidence locations.
- Known limitations and exact blocking dependency, if any.
- Next task and affected contract.

Update docs/STATUS.md with a concise factual summary after reading its current version. GitHub issues/PRs remain the detailed work record. Do not overwrite another assistant's newer status entry.

Neither assistant is automatically watching the other chat or repo. A task is handed off when the handoff is recorded and the other active environment is directed to read it.

## Validation priorities

Validate focus/fullscreen behavior, edit/save/exit, process lifetime, suspended launcher input, return-to-launcher controls, scoped emergency close, background resource use and notification priority.

For UI work include screenshots or short recordings of the affected behavior. For performance claims include conditions and measured results. Clearly label physical-controller and real-game coverage.

Keep credentials and personal data out of tracked fixtures. Do not invent build/test commands until the stack exists.

