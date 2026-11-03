> Current setup update from SYN's relay: the live repository is https://github.com/lostfaithxenia/GLauncher . SYN explicitly chose PUBLIC visibility after the initial private request. The repository setup statements below are historical and superseded by this update and STATUS.md. Claude currently has no repository/write/runtime access and could not open the ZIP/image; SYN relays its draft files to Codex, who reviews, compiles and publishes them with attribution. Do not assume a repository URL grants private access or gives Claude write capability. Read the concept image separately from docs/design/.
# Claude handoff — GLauncher

You are joining a Windows game-launcher project called GLauncher. You will collaborate with Codex through a shared GitHub repository. Treat me, the user, as the product owner and developer. My online name is SYN; the application is called GLauncher.

Read this entire handoff before making architectural or visual decisions. It contains the current product direction, the limits of what exists, and the collaboration rules.

## 1. Where the project actually stands

We are refining a rough product concept. GLauncher has a written working brief and one concept sheet I described as "pretty good". It does not yet have a verified implementation, chosen technology stack, working installer, configured AI provider, online friend service, or confirmed download integrations.

The current concept sheet shows four stages:
1. Opening video.
2. Recommended page for owned games, with a Discover subpage.
3. Library with platform-coloured cloud effects around covers.
4. A dedicated game page.

That image predates my latest changes: covers must now be 9:16, profile pictures must be large and prominent, and creation tools / expanded personalization must be added. Do not copy the old image's cover proportions.

Before this project there was another launcher called OHMERA and an interest in recreating a PS3 interface. I explicitly chose to start fresh. Do not inherit that architecture, branding, assets, UI or feature list. Do not rename the old launcher and present it as GLauncher. Earlier generic cinematic, light editorial and graphite/lime GLauncher mockups were rejected. The energy-cloud concept is the only current visual starting point.

This is a Windows desktop app, not a website or a static screenshot exercise. The rough description is not a final specification. Preserve the requested direction while documenting unresolved implementation choices.

## 2. GitHub setup — verify before working

Target GLauncher repository URL: NOT YET PROVIDED OR CREATED IN THIS HANDOFF.

Codex could see repositories under lostfaithxenia, including ohmera-build and nova-xmb. Those are historical projects, not automatically the GLauncher target. No dedicated GLauncher repository was visible through the connected GitHub account when this handoff was prepared.

A possible new repository name is lostfaithxenia/GLauncher, but that is a suggestion, not a confirmed live URL. Use the actual repository selected by me. If you cannot determine it from the current conversation or connected workspace, ask for that missing URL. If I authorize creating it, use a separate private repository by default and report its actual URL.

Do not push to, rename, overwrite or use an older repository just to avoid this setup step. This prompt and the accompanying kit are local handoff documents; their existence does not mean they have already been committed or published.

The kit contains:
- AGENTS.md — shared instructions for both assistants.
- CLAUDE.md — Claude entry point pointing to the same instructions.
- docs/PRODUCT_BRIEF.md — current requirements.
- docs/CLAUDE_HANDOFF.md — this prompt.
- docs/COLLABORATION.md — task claiming, branches, review and handoff rules.
- docs/STATUS.md — current verified state.
- docs/DECISIONS.md — decisions and unresolved questions.
- docs/design/GLauncher-energy-concept-v1.png — accepted starting reference.
- docs/design/GLauncher-energy-concept-v1-prompt.md — how that reference was generated.
- .github/pull_request_template.md — a concise review template.

Inspect the real repository before assuming this file tree is already present. Do not overwrite newer files with stale copies from this kit.

## 3. Visual identity and navigation

The experience should feel very polished, animated and a little cartoonish. Use soft sculpted cards, rounded readable typography, rich artwork, smooth transitions and fluid energy-cloud effects. Keep the interface understandable and responsive.

Default appearance: dark mode. Light mode must be available in Settings.

The app opens as a WINDOW by default. Pressing F toggles fullscreen only when GLauncher itself is focused. F must do nothing to GLauncher when it is minimized or I am using another app. It also must not trigger fullscreen while I am typing in an input.

Startup flow:
Opening video → Recommended.

Main areas:
- Recommended.
- Library.
- A creation area for covers, with a quick-description subpage.
- Profiles.
- Settings.

The exact navigation placement is still open. Do not add stores, feeds, ads or unrelated features merely to fill space.

Recommended has two different destinations:
- Your games: recommendations drawn from games the user already owns. Launch actions belong here.
- Discover: recommendations for games to buy, or download if free. Do not mix ownership and purchase state or label an unowned game as ready to play.

For an initial owned-game recommender, a transparent local approach based on available library metadata is acceptable if documented. Do not imply that a sophisticated remote recommendation service already exists.

## 4. Library, covers and full game pages

Covers must be 9:16 portrait images.

Each game has a custom card back and/or aura associated with its platform. Platform categories:
- PS2.
- PS3.
- Xbox 360.
- Xbox Series S/X.
- PC.
- Mobile.
- Other.

On hover or controller focus:
- Highlight the card.
- Use a smooth animated energy cloud around the cover.
- Show a pulsing wave that moves through shades of the platform's colour.
- Reveal the game name and platform clearly.
- Keep the cover readable; avoid glow clipping, obscured neighbours, excessive particles or harsh flashing.

This is soft animated vapour and flowing light, not a generic glowing rectangular border. The exact platform palette is provisional. Resting cards can be calmer so the focused card has a clear hierarchy.

Selecting a card opens a FULL GAME PAGE with:
- Cover.
- Name.
- Platform.
- Description.
- Play action.
- Three-dot menu to edit information and artwork.

Do not substitute a small modal for the requested full page.

## 5. Creation tools and generated assets

Cover studio:
- Select a game and generate a custom 9:16 cover.
- Preview before applying.
- Preserve the current cover until replacement is accepted.
- Handle provider configuration, loading, cancellation and errors honestly.

Description subpage:
- Enter a game title.
- Retrieve or generate a quick useful description.
- Make it easy to copy into the game's description.

The AI/metadata provider has not been chosen. A standalone app needs its own supported integration, credentials where required and clear usage-cost information. It cannot silently borrow image-generation capability from this chat or assume a subscription includes an API.

If your environment lacks image generation, document the required asset and hand the generation task to the assistant that has it. Do not replace the requested final icons with a pile of improvised geometric shapes or a mismatched stock icon set.

All app-owned icons should be generated image assets belonging to one designed family. Record generated-asset prompts and source files. Maintain clear small-size silhouettes, transparency and consistent states.

Do not invent or synthesize sound effects. I will provide sounds. Prepare sensible audio hooks and use silence until supplied.

## 6. Personalization, profiles and roles

- Backgrounds can be PNG, JPG or MP4.
- Profile pictures should be BIG and visibly part of the identity of the app.
- Discord-like role badges.
- As the developer, I want Mythical and SYN roles on my profile.
- Users can create their own roles.
- Custom animations and sliders for colour, glow, animation and additional visual effects.
- Show changes through responsive previews.
- Support dark/light mode.

Do not silently equate a decorative role with administrative access. If accounts or real permissions are added, design those separately.

Whether profiles are local-only, online, or both is undecided. Real friends-online notifications will require a real presence integration or service. A local profile screen alone is not an online social system.

Multiple additional themes and entirely new UI options come AFTER the first alpha is finished. Build a coherent default experience first while leaving reasonable extension points.

## 7. Launching games, input and controllers

Allow a game to be added using its EXE and relevant information.

I want launching to be as fast and efficient as practical. Reduce GLauncher's own overhead and avoid unnecessary work during launch. Do not promise a universal game-loading speed boost or add speculative system tweaks.

When a game runs:
- Suspend the launcher's normal keyboard, mouse and controller navigation so playing cannot move around the hidden launcher.
- Keep the user's input devices working normally for the game.
- Resume GLauncher controls when the user returns.
- Recognize the connected controller and show appropriate identity/prompts where supported.
- Track actual game lifetime, including bootstrap launchers, emulator processes and child-process handoffs.

Requested emergency action:
Delete force-closes the current launched game when it crashes, freezes, or I want to leave quickly.

The activation details need to be settled because Delete is also a real game/text key. Make the emergency binding explicit and configurable. It targets only the tracked game session, never an arbitrary foreground process. It is an intentional exception to normal suspended launcher navigation, not permission to install broad interfering keyboard controls.

Esc in GLauncher should handle unsaved changes and ask whether to exit. Provide a clear way to save or cancel. Do not silently discard edits.

## 8. Notifications, inactivity and achievements

Use three states with explicit rules:

Foreground:
Normal in-app notifications according to user settings.

Minimized with no game session:
IMPORTANT notifications only. One example is a restrained prompt asking whether the unused app can close to save RAM.

Gameplay:
Allow the gameplay categories the user enables, including friends coming online, game achievements and user-created custom notifications. A launcher minimized during a game follows this gameplay policy rather than the general minimized policy.

Never steal the game's focus to show a notification. Avoid repeated inactivity prompts. Do not treat an active game, save operation or download as a reason to prompt for inactivity closure. The exact inactivity timeout and whether anything closes automatically are unresolved; the current brief only defines a Close / Keep open prompt.

Real game achievements need actual supported platform/emulator/game data. Do not fabricate achievements or label a demo event as an imported real achievement. Friends presence has the same requirement for a real source.

Windows settings can suppress notification banners during games/fullscreen. Verify actual behavior. Do not claim an overlay works over every game without testing it.

## 9. Downloads, ROMs and emulation

I requested full support for Vimm's Lair and steamrip.com for downloading games, ROMs and relevant emulators.

These are REQUESTED integrations, not verified working features. Codex could not verify the sites through its web reader. Investigate their current availability and actual download flows before deciding what support is practical. Do not claim an integration based on showing an embedded web page.

Separate browsing, downloads, extraction, installation, emulator assignment and launching. An archive existing on disk is not automatically a configured playable game.

Keep this feature in the requirements even if its implementation needs a later milestone. Explain a concrete limitation rather than silently dropping it.

Discover should distinguish paid purchase links from free downloads. Do not imply the app can turn any paid listing into a free download.

## 10. Performance and architecture expectations

The app should look alive without constantly consuming game resources.

- Pause or scale back MP4 backgrounds and expensive decorative rendering when minimized or a game has focus.
- Cache and resize artwork appropriately.
- Virtualize or otherwise limit offscreen library rendering.
- Do not decode every full-resolution image or start AI jobs at startup.
- Keep essential process tracking and permitted notifications lightweight.
- Make animation/effects levels adjustable.
- Measure startup, library scrolling, background video, minimized use and active-game overhead.

No stack has been selected. Evaluate suitability for Windows process integration, controller support, video backgrounds, animated UI, packaging and predictable resource use. Do not choose a technology simply because it is convenient for generating a website.

Agree the application shell and shared contracts before two assistants build incompatible scaffolds. Keep UI, game-session management, storage, notifications, artwork and provider integrations separated enough that each can be worked on independently. Avoid building an elaborate unused plugin architecture.

Choose routine reversible implementation details autonomously and record them. Ask me only for missing information or consequential decisions that cannot be inferred, such as spending money, account access or a conflicting product requirement.

## 11. How you and Codex share the work

Use GitHub as the persistent coordination channel. Neither assistant should assume it can read the other chat or is automatically watching the repository in real time.

Source of truth:
- Product direction: my latest explicit instructions, then docs/PRODUCT_BRIEF.md.
- Current work: GitHub issues, open pull requests, current branch/head.
- Architecture: docs/DECISIONS.md and agreed code contracts.
- Handoffs: issue/PR comments plus docs/STATUS.md summaries.

Before editing:
1. Verify the correct remote, base branch and latest head.
2. Read AGENTS.md, the brief, status, decisions and relevant issues/PRs.
3. Claim one bounded issue with a comment naming the assistant, intended scope, files/subsystems and dependencies.
4. Check that the other assistant has not already claimed it.
5. Work in your own branch and separate checkout/worktree.

Branch examples:
- claude/<issue>-<short-purpose>
- codex/<issue>-<short-purpose>

Do not have both assistants edit the same working tree concurrently. Do not reset, clean or force-push over the other assistant's uncommitted or published work.

One issue should describe:
- User-visible result.
- Acceptance criteria.
- Owner.
- Owned files/subsystem.
- Dependencies.
- Validation.
- Current state.

Shared models, package manifests, lockfiles, navigation root, design tokens and CI should each have an explicit owner while being changed. Coordinate changes to shared contracts before dependent implementation. A claim comment is a coordination convention, not a GitHub-enforced lock; reread before starting.

Suggested initial split, NOT work already assigned:
- Codex: Windows game sessions/process tracking, focus/input policy, controller integration, persistence, build/CI and packaging.
- Claude: interface shell, 9:16 library layout, platform-cloud interactions, prominent profiles/roles and creation-tool screens.
- One owner first establishes the shell, models and service boundaries. The other reviews that foundation before parallel feature work.
- Either assistant may take a different bounded issue when agreed in GitHub; competence is not limited by this suggested split.

Open a focused PR early when useful. Include the issue link, actual changed behavior, screenshots for UI work, validation results and remaining limitations.

Review the other assistant's work against the product brief, behavior, performance and integration contracts. Record evidence and requested changes. "Claude" and "Codex" identify assistants in branch names/comments; do not assume they are actual GitHub usernames to tag.

If both tools use my same GitHub account, be honest about that identity. A second assistant's review comment is useful evidence, not a fabricated independent account approval. Respect the repository's actual merge rules.

Do not merge your own changes merely because you think the other assistant will approve them. Follow the agreed review/merge policy and any existing authorization. Do not create an extra approval request for every routine edit, test or draft PR.

After a task:
- Commit and push the actual work.
- Update its issue/PR.
- Summarize the branch, head SHA, changed behavior, checks, limitations and next handoff.
- Update shared status carefully without overwriting newer entries.
- Report exact links. Never claim code, tests, uploads, approvals or builds that did not happen.

If you lack GitHub write access or a runnable development environment, state that precise limitation and provide a patch or reviewable file set. Do not pretend a connector with read access can perform writes or run tests.

## 12. First-alpha planning

All the features above remain in the product requirements. Their exact first-alpha scheduling is not settled, apart from extra themes and alternate UI designs explicitly coming later.

Propose a small end-to-end first milestone: a real windowed Windows shell, initial video transition, owned-game recommendations, a 9:16 library with one polished aura interaction, editable game details, add/launch/return behavior, persistence and the core focus/input/exit rules.

Then map profiles, personalization, creation providers, notifications, controller coverage and download integrations into explicit milestones. This is a planning proposal, not permission to delete requirements or call a static mockup a finished alpha.

Validate meaningful behavior:
- F works only in the focused non-text-entry launcher.
- Editing and exit preserve data correctly.
- Game input never navigates the hidden launcher.
- Return restores launcher control.
- Emergency close targets only the intended session.
- Background rendering actually scales down.
- Notification priority follows app state.
- Controller connect/disconnect and navigation work on devices actually tested.
- Provider failures are handled without fake success.
- 9:16 covers and cloud effects survive resizing, different DPI and light mode.

Use Windows validation for Windows behavior. Clearly distinguish compilation, automated checks, fixture tests, visual inspection and real hardware/game testing. Do not claim one proves the others.

## 13. What I want from your first response

1. Confirm the project is GLauncher and summarize its identity accurately.
2. State whether you can access the actual GLauncher repo and edit/run the project.
3. Read the accepted reference and explain the 9:16/profile changes it still needs.
4. Give a concise architecture recommendation and the first milestone.
5. Identify one bounded first task, its owner, files/subsystem and dependencies, and how Codex can continue or review it.
6. Proceed with already-authorized work when the repo/task is ready. Ask only for genuinely missing setup or decisions; do not repeatedly ask whether to continue.

Keep communication plain and concrete. I value momentum, honest progress and a working result. Avoid endless planning, repeated confirmation questions, generic dashboards and unsupported performance claims.


