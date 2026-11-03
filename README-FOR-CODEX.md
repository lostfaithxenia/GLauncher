# Claude → Codex handoff for issue #1 (via SYN)

Proposed branch: `claude/1-architecture-contracts` (base: current `main`). Please publish with attribution, then compile/test on Windows.

Files (all new; none overwrite existing files except the optional DECISIONS.md note below):

```
build/GLauncher.Common.props
docs/adr/ADR-001-stack-and-core-contracts.md
src/GLauncher.Core/GLauncher.Core.csproj
src/GLauncher.Core/Games/GameModels.cs
src/GLauncher.Core/Sessions/SessionModels.cs
src/GLauncher.Core/Input/InputPolicy.cs
src/GLauncher.Core/Notifications/NotificationPolicy.cs
src/GLauncher.Core/Providers/ProviderModels.cs
src/GLauncher.Core/Providers/ArtworkAndMetadata.cs
src/GLauncher.Core/Storage/IGameLibraryStore.cs
tests/GLauncher.Core.Tests/GLauncher.Core.Tests.csproj
tests/GLauncher.Core.Tests/{InputPolicyTests,NotificationPolicyTests,ContractTests}.cs
```

Not included: the `.slnx` (generate with the commands in the ADR). Optional one-line addition to docs/DECISIONS.md: "ADR-001 (proposed): .NET 10 LTS + WinUI 3, UI-independent Core, SQLite proposed — see docs/adr/ADR-001-stack-and-core-contracts.md, issue #1."

Honest status: **uncompiled and unrun.** I had no .NET SDK. Expect possible compile fixes; please record them in the PR rather than silently rewriting, so I can see what changed. Please challenge: the Delete-hold default, `TrackingUncertain` semantics, `LauncherExitPolicy` default, and whether `ObserveAsync` (IAsyncEnumerable) suits your session adapter.
