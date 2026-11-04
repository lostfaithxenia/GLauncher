using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using GLauncher.Core.Games;
using GLauncher.Core.Sessions;
using GLauncher.Windows;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;

namespace GLauncher.App;

internal sealed partial class MainWindow
{
    /// <summary>Opt-in integration harness, only in a caller-specified, empty data directory. No user games.</summary>
    private async Task RunSmokeTestAsync(string evidenceDirectory)
    {
        var checks = new List<object>(); string evidence = Path.GetFullPath(evidenceDirectory); Directory.CreateDirectory(evidence);
        bool passed = false;
        void Check(string name, bool condition, object? facts = null)
        { checks.Add(new { name, passed = condition, facts }); if (!condition) throw new InvalidOperationException("Smoke check failed: " + name); }
        try
        {
            Check("isolated explicit data directory", Program.Option("--data-directory") is not null && library.Count == 0);
            for (int i = 0; i < 120 && introPlaying; i++) await Task.Delay(100);
            Check("opening video completed", !introPlaying && !mediaFailed);
            Check("windowed startup", AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped);
            Check("root loaded", root.ActualWidth > 800 && root.ActualHeight > 500);
            await SnapshotAsync(Path.Combine(evidence, "01-empty.png"));
            string fixture = Program.Option("--fixture-exe") ?? throw new ArgumentException("Smoke mode requires --fixture-exe.");
            string[] titles = ["Moonvale", "Neon drift", "Orbit garden", "Cloudbound", "Starlight trail", "Pocket worlds", "Another horizon"];
            var games = Enum.GetValues<GamePlatform>().Select((platform, index) => new GameEntry(GameId.New(), "Fixture · " + titles[index], platform,
                new LaunchTarget(Path.GetFullPath(fixture), "wait 30000"), DateTimeOffset.UtcNow.AddMinutes(-index),
                "GLauncher UI fixture. This is original test metadata, not an installed or owned game. The launch action runs a controlled short-lived process.", Visuals.Asset("energy-clouds.png"))).ToArray();
            foreach (var game in games) await store.UpsertAsync(game, default);
            library = await store.GetAllAsync(default); ShowLibrary(); await Task.Delay(900);
            Check("seven real persisted fixture entries", library.Count == 7 && libraryGrid?.Items.Count == 7);
            Check("virtualized cards materialized", cardVisuals.Count > 0);
            var card = cardVisuals.Values.First();
            card.Item.Focus(FocusState.Keyboard); FocusCard(card.Item); await Task.Delay(400);
            Check("focused aura starts", card.Aura.Running);
            await SnapshotAsync(Path.Combine(evidence, "02-library-dark.png"));
            librarySearch!.Focus(FocusState.Programmatic); librarySearch.Text = "Moonvale"; await Task.Delay(150);
            Check("search filters library", libraryGrid?.Items.Count == 1);
            var presenter = AppWindow.Presenter.Kind;
            Check("F ignored while typing", !await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == presenter, new { Typing });
            librarySearch.Text = ""; await Task.Delay(200);
            var firstContainer = libraryGrid!.ContainerFromIndex(0) as GridViewItem; firstContainer?.Focus(FocusState.Keyboard);
            Check("F enters fullscreen while foreground", await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen);
            await Task.Delay(300);
            Check("F returns to window", await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped);
            ShowGame(games[0]); await Task.Delay(300); await SnapshotAsync(Path.Combine(evidence, "03-game-page.png"));
            var edited = games[0] with { Description = "Edited fixture description, saved locally." };
            await store.UpsertAsync(edited, default);
            Check("edit survives independent store reopen", (await new GLauncher.Storage.Sqlite.SqliteLibraryStore(Path.Combine(Program.DataDirectory, "library.db")).GetAsync(edited.Id, default)) == edited);
            await SaveSettingsAsync(settings with { LightMode = true }); ApplySettings(); ShowLibrary(); await Task.Delay(500);
            await SnapshotAsync(Path.Combine(evidence, "04-library-light.png"));
            Check("light mode applied and persisted", root.RequestedTheme == ElementTheme.Light && (await store.GetPreferenceAsync<UserSettings>("settings"))!.LightMode);
            await SaveSettingsAsync(settings with { LightMode = false, BackgroundPath = Visuals.Asset("opening.mp4") }); ApplySettings(); ShowLibrary();
            for (int i = 0; i < 80 && !mediaReady; i++) await Task.Delay(100);
            Check("custom MP4 background opened", mediaReady && !mediaFailed);
            await Task.Delay(300);
            var target = cardVisuals.Values.FirstOrDefault(); if (target is not null) FocusCard(target.Item);
            using var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime; var wall = Stopwatch.StartNew(); await Task.Delay(3000); process.Refresh();
            checks.Add(new { name = "foreground resource sample", workingSetMiB = process.WorkingSet64 / 1048576.0, cpuPercentOfMachine = (process.TotalProcessorTime - cpuBefore).TotalSeconds / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100 });
            ((OverlappedPresenter)AppWindow.Presenter).Minimize(); await Task.Delay(700);
            Check("real minimized foreground facts", DesktopInput.IsMinimized(hwnd) && !DesktopInput.IsForeground(hwnd));
            Check("F ignored while minimized", !await ShortcutAsync(VirtualKey.F));
            Check("auras pause while minimized", cardVisuals.Values.All(v => !v.Aura.Running));
            double position = media.PlaybackSession.Position.TotalSeconds; cpuBefore = process.TotalProcessorTime; wall.Restart(); await Task.Delay(3000); process.Refresh();
            Check("MP4 remains paused while minimized", Math.Abs(media.PlaybackSession.Position.TotalSeconds - position) < .2);
            checks.Add(new { name = "minimized resource sample", workingSetMiB = process.WorkingSet64 / 1048576.0, cpuPercentOfMachine = (process.TotalProcessorTime - cpuBefore).TotalSeconds / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100 });
            ((OverlappedPresenter)AppWindow.Presenter).Restore(); Activate(); await Task.Delay(700);
            Check("launcher control resumes on return", DesktopInput.IsForeground(hwnd) && inputPolicy.Evaluate(DesktopInput.Facts(hwnd, false, sessions.GetActiveSessions()), InputSettings).LauncherNavigationEnabled);
            var request = new LaunchRequest(SessionId.New(), games[0].Id, games[0].Launch);
            var launched = await sessions.LaunchAsync(request, default);
            Check("native fixture launches from app", launched.Session.State == SessionState.Running);
            var termination = await sessions.TerminateAsync(new(request.SessionId, TerminationTrigger.UserMenu), default);
            Check("fixture close reports partial semantic coverage", termination.Outcome == TerminationOutcome.KnownProcessesTerminated && !termination.ProvesGameEnded);
            await sessions.StopTrackingAsync(request.SessionId, default);
            Check("tracking dismissal removes gameplay policy", sessions.GetActiveSessions().Count == 0);
            ShowSettings(); await Task.Delay(300); await SnapshotAsync(Path.Combine(evidence, "05-settings.png"));
            passed = true;
        }
        catch (Exception error) { checks.Add(new { name = "exception", passed = false, error = error.ToString() }); }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(evidence, "smoke-result.json"), JsonSerializer.Serialize(new { passed, checks, limits = "Own-app XAML captures can omit compositor-only glow and MediaPlayer surfaces. Fixture processes only; no physical controller, real game, real hotkey press, anti-cheat, notification or download integration was tested." }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = passed ? 0 : 1; allowClose = true; Close();
        }
    }

    private async Task SnapshotAsync(string path)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(root);
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
