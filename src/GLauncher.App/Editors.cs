using GLauncher.Core.Games;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace GLauncher.App;

internal sealed partial class MainWindow
{
    private static TextBox Field(string header, string? value = null, bool multiline = false)
        => new() { Header = header, Text = value ?? "", AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 100 : 0, MaxHeight = multiline ? 180 : double.PositiveInfinity, CornerRadius = new CornerRadius(10) };
    private async Task<string?> PickFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (string extension in extensions) picker.FileTypeFilter.Add(extension);
        return (await picker.PickSingleFileAsync())?.Path;
    }
    private async Task<string?> ImportFileAsync(string? path, string kind)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string full = Path.GetFullPath(path);
        string directory = Path.Combine(Program.DataDirectory, "artwork", kind);
        Directory.CreateDirectory(directory);
        if (full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) return full;
        string destination = Path.Combine(directory, Guid.NewGuid().ToString("N") + Path.GetExtension(full).ToLowerInvariant());
        await Task.Run(() => File.Copy(full, destination)); return destination;
    }

    private async Task EditGameAsync(GameEntry? original)
    {
        if (Program.Option("--smoke-test") is not null) Program.Trace("Editor: constructing dialog");
        var dialog = Dialog(original is null ? "Add a world" : "Edit game");
        if (Program.Option("--smoke-test") is not null) Program.Trace("Editor: constructing fields");
        dialog.PrimaryButtonText = "Save game"; dialog.SecondaryButtonText = "Discard changes"; dialog.CloseButtonText = "Cancel";
        var fields = new StackPanel { Spacing = 12, Width = 520 };
        var title = Field("Game name", original?.Title);
        var platform = new ComboBox { Header = "Platform", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in Enum.GetValues<GamePlatform>()) platform.Items.Add(Visuals.Platform(value)); platform.SelectedIndex = (int)(original?.Platform ?? GamePlatform.Pc);
        var executable = Field("Executable (.exe)", original?.Launch.ExecutablePath);
        var arguments = Field("Arguments (optional — emulator ROM paths go here)", original?.Launch.Arguments);
        var working = Field("Working folder (optional)", original?.Launch.WorkingDirectory);
        var cover = Field("Cover image — framed at 9:16", original?.CoverPath);
        var description = Field("Description", original?.Description, true);
        if (Program.Option("--smoke-test") is not null) Program.Trace("Editor: constructing warning");
        var error = new InfoBar { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Warning };
        if (Program.Option("--smoke-test") is not null) Program.Trace("Editor: adding fields");
        fields.Children.Add(title); fields.Children.Add(platform); fields.Children.Add(executable);
        fields.Children.Add(Button("Choose executable", 9, async () =>
        {
            if (await PickFileAsync(".exe") is { } file) { executable.Text = file; if (string.IsNullOrWhiteSpace(title.Text)) title.Text = Path.GetFileNameWithoutExtension(file); }
        }));
        fields.Children.Add(arguments); fields.Children.Add(working); fields.Children.Add(cover);
        fields.Children.Add(Button("Choose cover", 9, async () => { if (await PickFileAsync(".png", ".jpg", ".jpeg") is { } file) cover.Text = file; }));
        fields.Children.Add(Text("Artwork is copied into GLauncher when you save. Other aspect ratios fill the 9:16 frame and may be cropped.", 12));
        fields.Children.Add(description); fields.Children.Add(error); dialog.Content = Scroll(fields);
        string Draft() => string.Join('\n', title.Text, platform.SelectedIndex, executable.Text, arguments.Text, working.Text, cover.Text, description.Text);
        string initial = Draft(); GameEntry? saved = null;
        warnUnsavedChanges = () => { if (Draft() != initial) { error.Message = "Save your game or choose Discard changes before exiting."; error.IsOpen = true; } };
        dialog.Closing += (_, args) =>
        {
            if (args.Result == ContentDialogResult.None && Draft() != initial)
            { args.Cancel = true; error.Message = "You have unsaved changes. Save the game or choose Discard changes to leave this editor."; error.IsOpen = true; }
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (string.IsNullOrWhiteSpace(title.Text)) throw new ArgumentException("Give your game a name.");
                if (!File.Exists(executable.Text) || !Path.GetExtension(executable.Text).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an existing Windows executable.");
                if (!string.IsNullOrWhiteSpace(working.Text) && !Directory.Exists(working.Text)) throw new ArgumentException("The working folder does not exist.");
                if (!string.IsNullOrWhiteSpace(cover.Text) && (!File.Exists(cover.Text) || !new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(cover.Text).ToLowerInvariant()))) throw new ArgumentException("Choose an existing PNG or JPG cover, or clear the cover field.");
                saved = new GameEntry(original?.Id ?? GameId.New(), title.Text.Trim(), (GamePlatform)platform.SelectedIndex,
                    new(Path.GetFullPath(executable.Text), NullIfEmpty(arguments.Text), NullIfEmpty(working.Text)), original?.AddedAt ?? DateTimeOffset.UtcNow,
                    NullIfEmpty(description.Text), await ImportFileAsync(NullIfEmpty(cover.Text), "covers"), original?.LastPlayedAt);
                await store.UpsertAsync(saved, default);
            }
            catch (Exception exception) { args.Cancel = true; saved = null; error.Message = exception.Message; error.IsOpen = true; }
            finally { deferral.Complete(); }
        };
        if (Program.Option("--smoke-test") is not null) Program.Trace("Editor: showing dialog");
        try { await ShowDialogAsync(dialog); } finally { warnUnsavedChanges = null; }
        if (saved is not null) { library = await store.GetAllAsync(default); ShowGame(saved); SetStatus("Game saved."); }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private async Task RemoveGameAsync(GameEntry game)
    {
        var dialog = Dialog("Remove from your library?"); dialog.Content = Text($"Remove {game.Title}? Your game files stay on your device.", 16);
        dialog.PrimaryButtonText = "Remove game"; dialog.CloseButtonText = "Keep game";
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
        await store.DeleteAsync(game.Id, default); library = await store.GetAllAsync(default); ShowLibrary();
    }

    private async Task EditProfileAsync()
    {
        var dialog = Dialog("Your space"); dialog.PrimaryButtonText = "Save profile"; dialog.SecondaryButtonText = "Discard changes"; dialog.CloseButtonText = "Cancel";
        var stack = new StackPanel { Width = 460, Spacing = 14 };
        var name = Field("Display name", settings.ProfileName); var picture = Field("Profile picture", settings.ProfilePicture);
        var roles = Field("Roles — one per line: Name | #RRGGBB", string.Join('\n', settings.Roles.Select(r => r.Name + " | " + r.Color)), true);
        var error = new InfoBar { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Warning };
        stack.Children.Add(name); stack.Children.Add(picture); stack.Children.Add(Button("Choose picture", 11, async () => { if (await PickFileAsync(".png", ".jpg", ".jpeg") is { } p) picture.Text = p; }));
        stack.Children.Add(roles); stack.Children.Add(Text("Roles decorate your local profile. They do not grant account permissions.", 12)); stack.Children.Add(error); dialog.Content = stack;
        string Initial() => string.Join('\n', name.Text, picture.Text, roles.Text); string initial = Initial();
        warnUnsavedChanges = () => { if (Initial() != initial) { error.Message = "Save your profile or choose Discard changes before exiting."; error.IsOpen = true; } };
        dialog.Closing += (_, args) => { if (args.Result == ContentDialogResult.None && Initial() != initial) { args.Cancel = true; error.Message = "Save your profile or choose Discard changes before leaving."; error.IsOpen = true; } };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Choose a display name.");
                var selectedRoles = roles.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line =>
                {
                    var parts = line.Split('|', 2, StringSplitOptions.TrimEntries); string color = parts.ElementAtOrDefault(1) ?? "#C9A7FF";
                    if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Role colours use #RRGGBB, for example #C9A7FF.");
                    if (parts[0].Length == 0) throw new ArgumentException("Give each role a name."); return new UserRole(parts[0], color);
                }).Take(12).ToArray();
                var updated = settings with { ProfileName = name.Text.Trim(), ProfilePicture = await ImportFileAsync(NullIfEmpty(picture.Text), "profiles"), Roles = selectedRoles };
                await SaveSettingsAsync(updated);
            }
            catch (Exception exception) { args.Cancel = true; error.Message = exception.Message; error.IsOpen = true; }
            finally { deferral.Complete(); }
        };
        try { if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary) ApplySettings(); }
        finally { warnUnsavedChanges = null; }
    }

    private async Task SaveSettingsAsync(UserSettings updated)
    {
        settings = updated;
        await settingsWrites.WaitAsync();
        try { await store.SetPreferenceAsync("settings", settings); SetStatus("Settings saved."); }
        finally { settingsWrites.Release(); }
    }

    private void ShowSettings()
    {
        var stack = PageStack(); stack.MaxWidth = 820; stack.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(Text("Set your atmosphere.", 40, true)); stack.Children.Add(Text("Changes save to this device automatically.", 14));
        ToggleSwitch Toggle(string header, bool value, Func<bool, Task> action)
        {
            var toggle = new ToggleSwitch { Header = header, IsOn = value }; toggle.Toggled += async (_, _) => await GuardAsync(() => action(toggle.IsOn)); return toggle;
        }
        stack.Children.Add(Toggle("Light appearance", settings.LightMode, async value => { await SaveSettingsAsync(settings with { LightMode = value }); ApplySettings(); }));
        stack.Children.Add(Toggle("Animated platform energy", settings.AnimationEnabled, async value => { await SaveSettingsAsync(settings with { AnimationEnabled = value }); UpdateActivity(); }));
        stack.Children.Add(Toggle("Opening video", settings.IntroEnabled, async value => await SaveSettingsAsync(settings with { IntroEnabled = value })));
        var glow = new Slider { Header = "Cloud glow", Minimum = 0, Maximum = 1, StepFrequency = .05, Value = settings.Glow, Width = 500 };
        var motion = new Slider { Header = "Energy motion", Minimum = .3, Maximum = 2, StepFrequency = .1, Value = settings.MotionSpeed, Width = 500 };
        var saveTimer = DispatcherQueue.CreateTimer(); saveTimer.Interval = TimeSpan.FromMilliseconds(250); saveTimer.IsRepeating = false;
        saveTimer.Tick += async (_, _) => await GuardAsync(async () => { await SaveSettingsAsync(settings with { Glow = glow.Value, MotionSpeed = motion.Value }); });
        stack.Unloaded += async (_, _) => { saveTimer.Stop(); if (!closing) await GuardAsync(() => SaveSettingsAsync(settings)); };
        void UpdateSliders() { settings = settings with { Glow = glow.Value, MotionSpeed = motion.Value }; UpdateActivity(); saveTimer.Stop(); saveTimer.Start(); }
        glow.ValueChanged += (_, _) => UpdateSliders(); motion.ValueChanged += (_, _) => UpdateSliders();
        stack.Children.Add(glow); stack.Children.Add(motion);
        stack.Children.Add(Text("Background", 22, true));
        stack.Children.Add(Text(settings.BackgroundPath is { } backgroundPath ? Path.GetFileName(backgroundPath) : "GLauncher energy clouds", 13));
        stack.Children.Add(Row(Button("Choose PNG, JPG or MP4", 9, async () =>
        {
            if (await PickFileAsync(".png", ".jpg", ".jpeg", ".mp4") is not { } file) return;
            SetStatus("Copying your background…"); await SaveSettingsAsync(settings with { BackgroundPath = await ImportFileAsync(file, "backgrounds") }); ApplySettings(); ShowSettings();
        }), Button("Reset background", 12, async () => { await SaveSettingsAsync(settings with { BackgroundPath = null }); ApplySettings(); ShowSettings(); })));
        stack.Children.Add(Text("Game controls", 22, true));
        stack.Children.Add(Toggle("Emergency close: hold Delete in the tracked game", settings.EmergencyEnabled, async value => await SaveSettingsAsync(settings with { EmergencyEnabled = value })));
        var hold = new Slider { Header = "Delete hold duration (seconds)", Minimum = 1, Maximum = 3, StepFrequency = .5, Value = settings.EmergencyHoldSeconds, Width = 500 };
        hold.ValueChanged += async (_, _) => await GuardAsync(() => SaveSettingsAsync(settings with { EmergencyHoldSeconds = hold.Value })); stack.Children.Add(hold);
        stack.Children.Add(Text("The emergency key closes verified processes from this launch only. It is inactive while another unrelated app or GLauncher is focused. Closing GLauncher leaves your games running.", 13));
        stack.Children.Add(Text("Sessions", 22, true));
        foreach (var session in sessions.GetActiveSessions())
        {
            var title = library.FirstOrDefault(g => g.Id == session.GameId)?.Title ?? "Game";
            stack.Children.Add(Button(title + " · " + (session.State == GLauncher.Core.Sessions.SessionState.TrackingUncertain ? "tracking uncertain" : "active"), 15, () => ShowSessionAsync(session.SessionId)));
        }
        stack.Children.Add(Text("GLauncher 0.1.0 alpha · Windows x64", 13, true));
        stack.Children.Add(Text("Local library and profiles. XInput controller support is a first pass. AI, store downloads, friends and achievements are not connected yet. No sound effects are included.", 13));
        stack.Children.Add(Button("Edit profile & roles", 11, EditProfileAsync));
        SetPage("Settings", Scroll(stack));
    }
}
