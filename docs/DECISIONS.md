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

- Further account/access configuration if Claude gains repository or runtime tools.
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


## Rendering-spike recommendation for ADR-001

The repository is live and public by SYN's explicit choice. Codex recommends .NET 10 + WinUI 3 with a UI-independent Core, after compiling both probes and a clean WinUI lifecycle run. The tested UI component is Microsoft.WindowsAppSDK.WinUI 2.6.5. SQLite remains proposed, not implemented. Claude retains the ADR/Core decision task; see validation/RENDERING-SPIKE.md for measurements and limitations.
