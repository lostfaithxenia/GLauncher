# GLauncher

A fresh Windows desktop game-launcher project by SYN, developed with Claude and Codex.

Repository: https://github.com/lostfaithxenia/GLauncher

SYN explicitly selected public visibility for this repository. This is a new project; earlier launcher repositories are outside its scope.

## Current stage

First local Windows alpha on the `codex/2-first-alpha` branch. The WinUI app integrates Claude's reviewed revision-2 Core with Codex's Windows session/input adapter, SQLite library, generated artwork, game pages and editors. PR #6 depends on unmerged PR #5; neither assistant has merged its own work.

- [Alpha usage, build and limitations](docs/ALPHA.md)

- [Product brief](docs/PRODUCT_BRIEF.md)
- [Current status](docs/STATUS.md)
- [Decisions](docs/DECISIONS.md)
- [Collaboration workflow](docs/COLLABORATION.md)
- [Claude handoff](docs/CLAUDE_HANDOFF.md)
- [Accepted visual reference](docs/design/GLauncher-energy-concept-v1.png)

The reference predates the 9:16 cover and large-profile-picture requirements. Follow the newer written brief.

Read AGENTS.md before work. CLAUDE.md points Claude to the same shared rules.

## Access and review

Codex has repository read/write access and a Windows .NET development environment. Claude currently supplies reviewable drafts through SYN and has not established repository or Windows execution access. Codex can publish those drafts with explicit attribution after review and validation.

The initial kit commit is authorized setup. Feature changes use issues and PRs; neither assistant merges its own work.

## Build

Windows x64 with .NET 10 SDK:

```powershell
dotnet build -c Release -m:1 --disable-build-servers
dotnet test -c Release -m:1 --disable-build-servers
./build/Publish-Alpha.ps1
```

The published folder includes GLauncher.exe and its runtimes; keep the folder intact. Normal libraries start empty. Automated UI/process fixtures are separate and labelled.
