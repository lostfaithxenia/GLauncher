# Disposable Windows rendering probes

These are feasibility probes for issue #1, not the production shell, Core contracts, artwork, final aura, input layer or installer.

They render a local silent diagnostic MP4 behind a 236.25 × 420 card (exactly 9:16) and ten soft cloud layers. The WPF variant uses cached radial-gradient/blur layers. WinUI uses compositor radial-gradient visuals and keyframe animation. The implementations and startup events differ, so these are not a controlled framework benchmark.

## Run

Requirements: Windows x64 and .NET 10 SDK. WinUI uses the pinned UI component package rather than the full SDK metapackage. Its first restore needs NuGet access. Node is optional for log analysis. Do not launch the WinUI DLL through the dotnet host; use its apphost EXE and manifest.

Supply a local MP4. For the same synthetic 1280×720/30 test pattern, with FFmpeg installed:

```powershell
New-Item -ItemType Directory -Force artifacts | Out-Null
ffmpeg -f lavfi -i 'testsrc2=size=1280x720:rate=30' -t 12 -an -c:v libx264 -preset fast -crf 25 -pix_fmt yuv420p -movflags +faststart artifacts/motion-test.mp4
./spikes/Run-Probe.ps1 -Framework WinUI -VideoPath artifacts/motion-test.mp4
./spikes/Run-Probe.ps1 -Framework Wpf -VideoPath artifacts/motion-test.mp4
```

Use -DotnetPath with a specific dotnet.exe if the SDK is not on PATH. Run-Probe keeps the parent process alive until the GUI process exits and records its real exit code. The WinUI project includes App.xaml resources and the unpackaged app manifest. This is not clean-machine deployment validation.

## What the timed harness does

It manipulates only its own windows: warming → foreground → minimized → restored → a separate same-process focus-fixture window → return → close, taking about 55 seconds after application construction. It never controls another app, sends keyboard/mouse input, or launches a real game. These are app-owned diagnostic transitions, not game-session integration tests.

Samples record actual activation/minimization, media position, animation state, process working set/private bytes and CPU time. The analyzer excludes the first two transition samples of each phase and selects actual active rows for foreground metrics.

```powershell
node spikes/analyze.mjs <samples.jsonl> <summary.json>
```

Media-position / animation-clock checks are narrow evidence. They do not measure presented FPS, GPU utilization, decoder shutdown, controller input or real-game performance. Startup events are explicitly named: WPF ContentRendered versus WinUI root Loaded. Do not compare them as identical time-to-first-frame measurements.

Windows GPU Engine performance counters were unavailable in the initial environment. An idle NVIDIA device counter is not the launcher's GPU usage; GPU utilization is left unreported.

## Boundaries

- No production code or Core contract is established by these probes.
- No sounds or production icons are generated here; diagnostic geometry/text are placeholders.
- Keep raw successful samples and failure evidence. A run that crashes on exit is not a pass.
- Log analysis can be inconclusive if focus or media readiness leaves too few observations.
- Shared task ownership and next work remain in issues #1–#3.
