# GLauncher — working brief

Status: exploratory requirements, not a final specification or an implemented alpha.
This brief records the user's current direction. GLauncher starts fresh; the earlier launcher and the first three rejected visual concepts are not design references.
The energy-cloud concept is the current visual starting point. Colours, artwork and exact layout remain provisional.

## Product and visual foundation

- Name: GLauncher. SYN is the developer's online name.
- A polished, highly animated, cartoonish Windows game launcher with performance as a core requirement.
- Default dark mode; light mode available in Settings.
- Startup plays an opening video, then lands on Recommended.
- Open as a window by default. F toggles fullscreen only when GLauncher is the foreground app and the user is not typing into a text field. F does nothing for GLauncher when it is minimized or another app has focus.
- Esc invokes the save-changes / confirm-exit flow. Unsaved edits must be handled before closing.
- Sound effects will be supplied by the user. Do not generate or synthesize them.
- All app icons should be purpose-made generated image assets in a coherent visual family, rather than assembled from miscellaneous basic shapes.
- Additional themes and alternate UI designs come after the first alpha is finished. The first alpha establishes one coherent default design.

## Navigation

- Recommended is the startup destination.
- Recommended / Your games recommends games already owned, with launch actions.
- A separate Discover subpage recommends games to buy, with a download action for free games.
- Library is the personal collection.
- Each library card opens a full game page.
- Add a creation area for custom covers and a subpage for quick game descriptions.
- Include profiles and Settings. Exact navigation placement is still open.

## Covers, platforms and game pages

- Game covers use a 9:16 aspect ratio.
- Each platform has its own custom card back and/or animated aura.
- Supported platform categories: PS2, PS3, Xbox 360, Xbox Series S/X, PC, Mobile, Other.
- Hover or controller focus highlights and gently lifts the card, revealing game name and platform.
- The highlight is an energy cloud with a smooth pulsing colour wave; the hue varies by platform and the wave shifts through shades of that hue.
- Keep cover artwork readable, with room around cards so effects do not clip or obscure neighbouring cards.
- A dedicated game page includes cover, title, platform, description and launch action.
- A three-dot menu allows editing game information, including artwork and platform.

## Creation tools

- Custom-cover studio: automatically generate a custom 9:16 cover for a selected game.
- Description subpage: enter a game title to get a short description, then copy it into the game's description.
- These features need a defined AI/metadata provider and an internet connection when using an online service. Service credentials and any usage costs must be made clear during setup.
- Do not assume that image-generation tools available in this chat are automatically accessible to the standalone app.
- Generated content is previewed before the user chooses to apply it. Preserve an existing cover until replacement is accepted.
- Provider, credentials, service limits and any optional local generation are undecided.

## Personalization

- Custom backgrounds from PNG, JPG and MP4.
- Large, prominent profile picture and a clearly visible profile identity.
- Discord-like role badges.
- The developer's profile can have Mythical and SYN roles.
- Users can create custom roles.
- Role appearance can be customized; do not infer real administrative permissions from a decorative badge.
- Custom animations and controls for colours, glow, animation and additional visual effects.
- Provide immediate visual previews while keeping the interface responsive.
- The exact colour mapping and limits for effect controls are undecided.

## Launching, input and controllers

- Add games using an executable and associated game information.
- Provide an efficient launch path and a game-running mode that reduces the launcher's own CPU, GPU and memory activity.
- Do not promise that a launcher setting can universally accelerate a game's engine, asset loading or startup.
- While a game is running, stop controller and keyboard/mouse navigation inside GLauncher so playing does not move around or edit the background launcher.
- This suspends only GLauncher's input handling; it must not disable the user's input devices.
- Resume launcher controls when the user returns to GLauncher.
- Recognize the connected controller and show suitable controller identity and prompts where supported.
- Requested emergency action: Delete force-closes the current launched game, including when the game is stuck.
- This emergency binding must target only the tracked launched game and its appropriate child processes. It must not operate while editing text or close arbitrary applications.
- Final activation details for Delete remain to be decided because Delete is also used by games and text fields; make the emergency binding explicit and configurable.
- Emergency close is a separate intentional exception to normal suspended launcher navigation during gameplay.
- Track actual game lifetime, including games that start through another launcher; do not treat the initial bootstrap executable exiting as proof the game has closed.

## Notifications and achievements

The rules distinguish three app states:

| State | Allowed behavior |
| --- | --- |
| GLauncher in foreground | Normal in-app notifications according to user preferences. |
| Minimized, no game session | Important notifications only. Include a restrained inactivity prompt offering to close the unused launcher to save memory. |
| Game session active | User-selected gameplay notifications, including friends coming online, game achievements and user-created custom notifications. Do not steal game focus. |

- A minimized app with a game running follows the gameplay notification rules.
- Inactivity prompts should not interrupt an active game, an ongoing save or an active download.
- The inactivity prompt offers Close / Keep open. Automatic timeout behavior has not been decided.
- Closing must still handle unsaved edits.
- Actual friends-online events need a connected presence service or other real data source.
- Actual game achievement events require a supported platform, emulator or game integration; do not imply every arbitrary executable exposes them.
- User-created custom notifications are a separate supported concept.
- Windows notification settings can suppress banners, including while gaming or using fullscreen apps. Fullscreen overlay behavior and compatibility must be verified rather than promised for every game.
- Notification appearance, categories and scheduling controls still need design.

## Download sources, emulation and stores

- User requests full support for Vimm's Lair and steamrip.com for downloading games, ROMs and relevant emulators.
- Record these as requested integrations, not verified working connectors.
- Availability, supported content, download flows and emulator setup must be investigated before defining the implementation.
- The sites could not be verified through the web reader in this session; no support claim has been established.
- Do not label a game executable, ROM or archive as already playable without the needed installation / extraction / emulator configuration.
- Discover must distinguish paid games from free downloads and present the appropriate action.

## Performance principles

- Scale down or suspend expensive decorative work when minimized or while a game has focus: MP4 backgrounds, cloud effects and continuous animation rendering.
- Keep only essential launch tracking, controller connection state and permitted notifications active in the background.
- Cache cover thumbnails and load visible content on demand.
- Avoid loading every full-resolution cover or starting AI generation work at startup.
- Make the level of animation and effects adjustable.
- Validate real resource usage during library browsing, video backgrounds, minimized operation and active gameplay.
- No performance or compatibility guarantee is established by the concept image.

## Decisions still open before implementation

- AI/metadata provider and how users configure access.
- Local profiles versus online accounts and how friends/presence work.
- Roles as visual customization versus any separately defined account permissions.
- Achievement sources and the first supported integrations.
- Emergency Delete activation and rebinding.
- Download-source feasibility and the first verified connectors.
- First-alpha feature boundary beyond the explicitly deferred additional themes / alternate UIs.
- Final layouts, imagery, aura colours, icon family, sound assets and opening-video asset.

## Current reference

- Energy concept image: [GLauncher-energy-concept-v1.png](design/GLauncher-energy-concept-v1.png).
- This image predates the 9:16 cover change, profiles, creation tools and the latest behavior additions. It does not depict all current requirements.

## Verification source

Windows notification behavior:
https://support.microsoft.com/en-us/windows/experience/notifications-and-do-not-disturb-in-windows

This source documents automatic Do Not Disturb conditions, including games/fullscreen, and per-app notification choices. It does not establish universal overlay support.


