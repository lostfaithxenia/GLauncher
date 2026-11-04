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
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
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
        void Skip(string name) => checks.Add(new { name, skipped = true, reason = "Windows did not grant this automated launch foreground focus. No foreground behavior is inferred." });
        void Check(string name, bool condition, object? facts = null)
        { checks.Add(new { name, passed = condition, facts }); Program.Trace("Check: " + name + " = " + condition); if (!condition) throw new InvalidOperationException("Smoke check failed: " + name); }
        try
        {
            Check("isolated explicit data directory", Program.Option("--data-directory") is not null && library.Count == 0);
            // Exercise only our own window's public lifecycle API. Windows decides whether it grants focus.
            if (!DesktopInput.IsForeground(hwnd) && AppWindow.Presenter is OverlappedPresenter ownPresenter)
            { ownPresenter.Minimize(); await Task.Delay(150); ownPresenter.Restore(); Activate(); await Task.Delay(250); }
            for (int i = 0; i < 80 && !mediaReady && !mediaFailed; i++) await Task.Delay(100);
            Check("opening video decodes", mediaReady && !mediaFailed);
            if (DesktopInput.IsForeground(hwnd))
            {
                for (int i = 0; i < 80 && introPlaying && DesktopInput.IsForeground(hwnd); i++) await Task.Delay(100);
                if (DesktopInput.IsForeground(hwnd)) Check("opening video completed in foreground", !introPlaying);
                else Skip("opening video completed in foreground");
            }
            else Skip("opening video completed in foreground");
            if (introPlaying) { Check("background startup video paused", media.PlaybackSession.PlaybackState == global::Windows.Media.Playback.MediaPlaybackState.Paused); FinishIntro(); }
            Check("windowed startup", AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped);
            Check("root loaded", root.ActualWidth > 800 && root.ActualHeight > 500);
            await Task.Delay(350); await SnapshotAsync(Path.Combine(evidence, "01-empty.png"));
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
            var frame = (Border)card.Root.FindName("CoverFrame");
            Check("rendered cover frame is 9:16", GLauncher.Core.Games.CoverSpec.IsNineBySixteen((int)frame.ActualWidth, (int)frame.ActualHeight), new { frame.ActualWidth, frame.ActualHeight });
            card.Item.Focus(FocusState.Keyboard); FocusCard(card.Item); await Task.Delay(400);
            await SnapshotAsync(Path.Combine(evidence, "02-library-dark.png"));
            AppWindow.Resize(new global::Windows.Graphics.SizeInt32(940, 760)); await Task.Delay(300);
            frame = (Border)card.Root.FindName("CoverFrame");
            Check("9:16 survives window resize", CoverSpec.IsNineBySixteen((int)frame.ActualWidth, (int)frame.ActualHeight));
            await SnapshotAsync(Path.Combine(evidence, "06-library-resized.png")); AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1280, 860)); await Task.Delay(250);
            libraryGrid!.ItemsSource = Enumerable.Range(0, 200).Select(i => new CardViewModel(games[i % games.Length])).ToArray(); await Task.Delay(350);
            Check("GridView virtualizes 200 fixture entries", cardVisuals.Count > 0 && cardVisuals.Count < 100, new { realized = cardVisuals.Count, items = libraryGrid.Items.Count });
            libraryGrid.ItemsSource = library.Select(g => new CardViewModel(g)).ToArray(); await Task.Delay(250);
            librarySearch!.Focus(FocusState.Programmatic); librarySearch.Text = "Moonvale"; await Task.Delay(150);
            Check("search filters library", libraryGrid?.Items.Count == 1);
            var presenter = AppWindow.Presenter.Kind;
            if (DesktopInput.IsForeground(hwnd)) Check("F ignored while typing", !await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == presenter, new { Typing });
            else { Skip("F while typing in foreground"); Check("F ignored outside foreground", !await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == presenter); }
            librarySearch.Text = ""; await Task.Delay(200);
            var firstContainer = libraryGrid!.ContainerFromIndex(0) as GridViewItem; firstContainer?.Focus(FocusState.Keyboard);
            if (DesktopInput.IsForeground(hwnd))
            {
                Check("F enters fullscreen while foreground", await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen);
                await Task.Delay(300);
                Check("F returns to window", await ShortcutAsync(VirtualKey.F) && AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped);
            }
            else Skip("foreground F fullscreen round trip");
            ShowGame(games[0]); await Task.Delay(300); await SnapshotAsync(Path.Combine(evidence, "03-game-page.png"));
            Program.Trace("Opening game editor"); var editorTask = EditGameAsync(games[0]); await Task.Delay(300);
            var editor = currentDialog ?? throw new InvalidOperationException("Game editor did not open.");
            var titleField = Descendants<TextBox>(editor).First(t => t.Header?.ToString() == "Game name");
            titleField.Text = "Edited fixture · Moonvale";
            editor.Hide(); await Task.Delay(200);
            Check("dirty editor blocks Escape-style dismissal", showingDialog && Descendants<InfoBar>(editor).Any(b => b.IsOpen));
            InvokeDialogButton(editor, "Save game"); await editorTask.WaitAsync(TimeSpan.FromSeconds(10));
            var edited = await new GLauncher.Storage.Sqlite.SqliteLibraryStore(Path.Combine(Program.DataDirectory, "library.db")).GetAsync(games[0].Id, default);
            Check("game editor saves and imports cover", edited?.Title == "Edited fixture · Moonvale" && edited.CoverPath!.StartsWith(Program.DataDirectory) && File.Exists(edited.CoverPath));
            var discardTask = EditGameAsync(edited); await Task.Delay(200); editor = currentDialog!;
            Descendants<TextBox>(editor).First(t => t.Header?.ToString() == "Game name").Text = "Discard this title";
            InvokeDialogButton(editor, "Discard changes"); await discardTask.WaitAsync(TimeSpan.FromSeconds(10));
            Check("explicit discard preserves saved entry", (await store.GetAsync(games[0].Id, default)) == edited);
            var profileTask = EditProfileAsync(); await Task.Delay(200); var profile = currentDialog!;
            Descendants<TextBox>(profile).First(t => t.Header?.ToString() == "Display name").Text = "SYN fixture profile";
            InvokeDialogButton(profile, "Save profile"); await profileTask.WaitAsync(TimeSpan.FromSeconds(10));
            Check("profile editor saves", (await store.GetPreferenceAsync<UserSettings>("settings"))!.ProfileName == "SYN fixture profile");
            await SaveSettingsAsync(settings with { LightMode = true }); ApplySettings(); ShowLibrary(); await Task.Delay(500);
            await SnapshotAsync(Path.Combine(evidence, "04-library-light.png"));
            Check("light mode applied and persisted", root.RequestedTheme == ElementTheme.Light && (await store.GetPreferenceAsync<UserSettings>("settings"))!.LightMode);
            await SaveSettingsAsync(settings with { LightMode = false, BackgroundPath = Visuals.Asset("opening.mp4") }); ApplySettings(); ShowLibrary();
            for (int i = 0; i < 80 && !mediaReady; i++) await Task.Delay(100);
            Check("custom MP4 background opened", mediaReady && !mediaFailed);
            await Task.Delay(300);
            var target = cardVisuals.Values.FirstOrDefault(); if (target is not null) FocusCard(target.Item);
            if (target is not null && DesktopInput.IsForeground(hwnd)) Check("foreground compositor aura starts", target.Aura.Running);
            else Skip("foreground compositor aura starts");
            using var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime; var wall = Stopwatch.StartNew(); await Task.Delay(3000); process.Refresh();
            checks.Add(new { name = "visible-window resource sample", actualForeground = DesktopInput.IsForeground(hwnd), workingSetMiB = process.WorkingSet64 / 1048576.0, cpuPercentOfMachine = (process.TotalProcessorTime - cpuBefore).TotalSeconds / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100 });
            ((OverlappedPresenter)AppWindow.Presenter).Minimize(); await Task.Delay(700);
            Check("real minimized foreground facts", DesktopInput.IsMinimized(hwnd) && !DesktopInput.IsForeground(hwnd));
            Check("F ignored while minimized", !await ShortcutAsync(VirtualKey.F));
            Check("auras pause while minimized", cardVisuals.Values.All(v => !v.Aura.Running));
            double position = media.PlaybackSession.Position.TotalSeconds; cpuBefore = process.TotalProcessorTime; wall.Restart(); await Task.Delay(3000); process.Refresh();
            Check("MP4 remains paused while minimized", Math.Abs(media.PlaybackSession.Position.TotalSeconds - position) < .2);
            checks.Add(new { name = "minimized resource sample", workingSetMiB = process.WorkingSet64 / 1048576.0, cpuPercentOfMachine = (process.TotalProcessorTime - cpuBefore).TotalSeconds / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100 });
            ((OverlappedPresenter)AppWindow.Presenter).Restore(); Activate(); await Task.Delay(700);
            if (DesktopInput.IsForeground(hwnd)) Check("launcher control resumes on return", inputPolicy.Evaluate(DesktopInput.Facts(hwnd, false, sessions.GetActiveSessions()), InputSettings).LauncherNavigationEnabled);
            else Skip("foreground controls resume on return");
            var request = new LaunchRequest(SessionId.New(), games[0].Id, games[0].Launch);
            var launched = await sessions.LaunchAsync(request, default);
            Check("native fixture launches from app", launched.Session.State == SessionState.Running);
            var termination = await sessions.TerminateAsync(new(request.SessionId, TerminationTrigger.UserMenu), default);
            Check("fixture close reports partial semantic coverage", termination.Outcome == TerminationOutcome.KnownProcessesTerminated && !termination.ProvesGameEnded);
            await sessions.StopTrackingAsync(request.SessionId, default);
            Check("tracking dismissal removes gameplay policy", sessions.GetActiveSessions().Count == 0);
            ShowSettings(); await Task.Delay(300); await SnapshotAsync(Path.Combine(evidence, "05-settings.png"));
            var exitTask = ConfirmExitAsync(); await Task.Delay(200);
            Check("exit confirmation opens", currentDialog?.Title?.ToString() == "Ready to leave?");
            InvokeDialogButton(currentDialog!, "Stay here"); await exitTask.WaitAsync(TimeSpan.FromSeconds(10));
            Check("cancelled exit keeps app open", !allowClose && !closing);
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
        using var fileStream = File.Create(path);
        using var stream = fileStream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) yield return found;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void InvokeDialogButton(ContentDialog dialog, string text)
    {
        var button = Descendants<Button>(dialog).First(b => b.Content?.ToString() == text);
        // Invoke only this app's own control peer; no desktop input or other app is automated.
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }
}
