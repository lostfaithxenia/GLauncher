# GLauncher 0.1 alpha

First local Windows vertical slice, implemented by Codex on Claude's reviewed revision-2 Core. This is a dependent branch of PR #5; neither assistant has merged it.

## Run

Extract the entire Windows x64 ZIP, then open **GLauncher.exe**. Keep the other files beside it. The portable distribution bundles .NET and the Windows App SDK. It opens in a window; F toggles fullscreen when the launcher is focused and you are not typing. Esc asks before exiting.

Add an existing EXE from Recommended or Library. Choose one of the seven platform categories, optional arguments/working folder, a description and a PNG/JPG cover. Covers fill a 9:16 frame. For an emulator, choose the emulator EXE and put its ROM command line in Arguments. No shell, elevated launch, game-file modification or automatic installation is involved.

Edits save atomically to SQLite. Unsaved game/profile editors require Save or an explicit Discard changes action before leaving. Settings save locally. The profile button edits your display name, picture and decorative roles. Your original artwork is copied on save; original files remain untouched.

Data lives in `%LOCALAPPDATA%\GLauncher`: library.db, preferences and imported artwork. `--data-directory "path"` selects an independent data directory. Do not commit personal library/profile data.

## Sessions and controls

- Ordinary keyboard, mouse and XInput navigation stays with the foreground window. Returning to GLauncher resumes its controls even if a game remains running.
- The proposed emergency binding is enabled by default: hold Delete for one second **while a verified tracked game process is foreground**. Settings can disable it or increase the hold. It never targets an arbitrary foreground process. This proposal still awaits SYN's final product decision.
- The session menu can close verified processes or stop tracking. Stop tracking never closes the game.
- Native descendants are contained in an unnamed Job Object where Windows permits assignment. Assignment failure falls back to the verified root, with an honest diagnostic.
- Generic executables always have **Incomplete** semantic coverage: an already-running store may receive a handoff outside the job. After the known set ends, the app says tracking is uncertain; dismiss it from Session controls if done. Closing known processes is not presented as proof that every possible game process ended.
- Closing the launcher leaves games running. No kill-on-job-close flag is enabled.
- XInput is a baseline Xbox-compatible adapter, not native recognition of every controller model. Physical devices and actual games require separate testing.

## Appearance

Original generated icon atlas and cloud background, silent three-second opening video, large local profile image, dark/light appearance, platform-coloured focused cloud aura, glow and motion controls. Custom PNG/JPG/MP4 backgrounds are copied to local storage. Media and cloud motion pause when GLauncher loses focus or is minimized. No sound effects are included.

Library cards use GridView virtualization, decoded thumbnails and effects only on a focused/hovered visible card. Local recommendations rank never-launched games first (newest added), then least recently launched. They use only the local library, not an AI service or store feed.

Discover and Studio explicitly show that providers are not connected. Store downloads, AI covers/descriptions, online friends, game achievements, Windows gameplay notifications, automatic game acceleration, additional UI designs and full PlayStation/HID support are later work. No fake offers or seeded owned games appear in a normal user library.

## Build and validate

Windows x64, .NET 10 SDK. From the repository root:

```powershell
dotnet build -c Release -m:1 --disable-build-servers
dotnet test -c Release -m:1 --disable-build-servers
./build/Publish-Alpha.ps1
```

The serial flags avoid the parallel MSBuild failure recorded in the development environment. Original Core is unchanged. Package versions are fixed in the projects.

Native fixture tests create only their own short-lived executables. They cover registration, cancellation, duplicate requests, observers, a bootstrap spawning a child, PID+creation-time guarding, scoped termination, non-destructive tracking stop/disposal and SQLite reopen/cancelled edits. These do not prove actual game, hardware controller or hotkey compatibility.

The opt-in app integration harness requires an explicit empty data directory and a controlled fixture EXE:

```powershell
GLauncher.exe --data-directory "C:\temp\glauncher-fixture-data" --smoke-test "C:\temp\glauncher-evidence" --fixture-exe "path\GLauncher.LaunchFixture.exe"
```

It writes actual check results and own-app XAML captures, then closes. Captures may omit compositor-only effects or media surfaces. See the validation report for executed results; harness code alone is not evidence of a pass.

## Sources

Native job design follows Microsoft's [Job Objects documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects). Packaging follows [self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps). These references explain the platform APIs; the project's own tests establish its observed behavior.
