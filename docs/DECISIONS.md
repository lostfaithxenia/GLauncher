# GLauncher decisions

## Owner-established direction

- Fresh project named GLauncher, without inheriting OHMERA/XMB design or code.
- Soft, polished, cartoonish energy-cloud visual direction.
- 9:16 game covers with platform-specific card treatment and focused-card motion.
- Opening video → Recommended; separate owned-game and Discover recommendations.
- Full game pages with editable information.
- Windowed startup; focused-app fullscreen shortcut; save/exit handling.
- Dark default and light option.
- Large profile pictures, customizable roles and Mythical / SYN developer profile badges.
- PNG/JPG/MP4 backgrounds, visual customization, generated icon assets and owner-provided sounds.
- Efficient background behavior and app-state-specific notifications.
- Additional themes / alternate UI layouts after the first alpha.

Full feature requirements are in PRODUCT_BRIEF.md.

## Provisional visual choices

The existing concept image demonstrates the direction, not final colors, layout, artwork, card proportions or animation. Newer written requirements override its older details.

## Unresolved implementation decisions

- Dedicated repository URL and access setup.
- Windows UI/rendering stack, package versions, build and packaging.
- Initial application shell owner and shared contracts.
- Final first-alpha boundaries and milestone sequence.
- AI and game-metadata providers, credentials and costs.
- Local versus online profiles and friends/presence service.
- Achievement sources and first tested integrations.
- Delete emergency binding activation.
- Notification transport/overlay behavior and inactivity timeout.
- Download-source feasibility, emulator configuration and supported connectors.
- Final generated icon family, startup video and user-supplied sound assets.

For each resolved implementation decision, record context, choice, consequences, owner and the issue/PR link. Do not mark a proposal as an accepted product requirement.

## First alpha implementation record (Codex, PR #6)

- PUBLIC repository lostfaithxenia/GLauncher was explicitly selected by SYN. Earlier setup items above are historical.
- Dependent base: Claude's revision-2 Core in PR #5, head 5deb77210a608efcaa788d09ef88dfc902f3726b; unchanged by this implementation.
- Windows x64 WinUI with .NET 10, Microsoft.WindowsAppSDK.WinUI 2.6.5, local Microsoft.Data.Sqlite 10.0.13. UI/Win32/SQLite remain outside Core. Portable self-contained folder distribution; no merge or installer-signing claim.
- Codex owns Windows adapters and the first UI integration. Baseline is local EXE library, owned recommendations, game pages/editors, local profile/roles, appearance settings and launch/return behavior. Provider-dependent features remain visibly unconfigured.
- Generic launches explicitly initialize Incomplete coverage and Unsupported termination before start; verified process membership permits Partial termination. A native job alone does not prove that an external store did not receive an IPC handoff. All session IDs and terminal snapshots are retained through service exit.
- SQLite initializes away from the UI thread in the unpackaged app. Custom main installs the WinUI dispatcher synchronization context. Neither observation disposal nor launcher closure kills a game; job kill-on-close is never enabled.
- Normal foreground shortcuts and XInput navigation use actual foreground/minimized facts. The default emergency proposal is one-second Delete hold while a verified game member is foreground, disabled under unrelated apps; these product defaults remain unresolved.

