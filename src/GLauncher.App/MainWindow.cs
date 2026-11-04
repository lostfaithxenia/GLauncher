using System.Diagnostics;
using System.Numerics;
using GLauncher.Core.Games;
using GLauncher.Core.Input;
using GLauncher.Core.Sessions;
using GLauncher.Storage.Sqlite;
using GLauncher.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using VirtualKey = Windows.System.VirtualKey;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace GLauncher.App;

internal sealed partial class MainWindow : Window
{
    private readonly SqliteLibraryStore store = new(Path.Combine(Program.DataDirectory, "library.db"));
    private readonly WindowsGameSessionService sessions = new();
    private readonly DefaultInputPolicy inputPolicy = new();
    private readonly nint hwnd;
    private readonly Grid root = new(), pageHost = new(), introLayer = new();
    private readonly Image background = Visuals.Picture(Visuals.Asset("energy-clouds.png"), 1600);
    private readonly Border veil = new();
    private readonly MediaPlayerElement mediaElement = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false, AreTransportControlsEnabled = false };
    private readonly MediaPlayer media = new() { IsMuted = true };
    private readonly TextBlock status = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock controllerStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel profileHost = new() { Orientation = Orientation.Horizontal, Spacing = 16 };
    private readonly Dictionary<SelectorItem, CardVisual> cardVisuals = [];
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SelectorItem, object> hookedItems = new();
    private readonly DispatcherQueueTimer timer;
    private readonly Stopwatch emergencyClock = new(), navigationClock = Stopwatch.StartNew();
    private readonly SemaphoreSlim settingsWrites = new(1);
    private UserSettings settings = new();
    private IReadOnlyList<GameEntry> library = [];
    private GameEntry? currentGame;
    private string page = "Recommended", lastSessionText = "";
    private CardVisual? focusedCard;
    private bool closing, allowClose, initialized, showingDialog, introPlaying, mediaReady, mediaFailed, activity;
    private ushort previousButtons;
    private int ticks;
    private SessionId? armedSession;
    private TextBox? librarySearch;
    private Action? warnUnsavedChanges;
    private ContentDialog? currentDialog;
    private GridView? libraryGrid;

    public MainWindow()
    {
        Title = "GLauncher · Alpha 0.1";
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        AppWindow.Resize(new SizeInt32(1280, 860));
        var queue = DispatcherQueue;
        root.RequestedTheme = ElementTheme.Dark;
        root.Children.Add(background); root.Children.Add(mediaElement); root.Children.Add(veil);
        var shell = new Grid { Padding = new Thickness(30, 20, 30, 12), RowSpacing = 16 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid { ColumnSpacing = 26 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        brand.Children.Add(Text("GLauncher", 28, true)); brand.Children.Add(Text("YOUR WORLDS, TOGETHER", 9)); header.Children.Add(brand);
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        navigation.Children.Add(Button("Recommended", 1, () => NavigateAsync("Recommended")));
        navigation.Children.Add(Button("Library", 2, () => NavigateAsync("Library")));
        navigation.Children.Add(Button("Studio", 10, () => NavigateAsync("Studio")));
        navigation.Children.Add(Button("Settings", 3, () => NavigateAsync("Settings")));
        Grid.SetColumn(navigation, 1); header.Children.Add(navigation);
        var profileButton = Button("", -1, EditProfileAsync); profileButton.Content = profileHost; profileButton.Padding = new Thickness(8);
        Grid.SetColumn(profileButton, 2); header.Children.Add(profileButton); shell.Children.Add(header);
        bool compactHeader = false;
        root.SizeChanged += (_, _) =>
        {
            bool compact = root.ActualWidth < 1050;
            if (compactHeader == compact) return; compactHeader = compact;
            Grid.SetRow(navigation, compact ? 1 : 0); Grid.SetColumn(navigation, compact ? 0 : 1); Grid.SetColumnSpan(navigation, compact ? 3 : 1);
            navigation.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        };
        Grid.SetRow(pageHost, 1); shell.Children.Add(pageHost);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(status); controllerStatus.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(controllerStatus, 1); footer.Children.Add(controllerStatus);
        Grid.SetRow(footer, 2); shell.Children.Add(footer); root.Children.Add(shell); root.Children.Add(introLayer); introLayer.Visibility = Visibility.Collapsed;
        Content = root;
        mediaElement.SetMediaPlayer(media);
        media.MediaOpened += (_, _) => queue.TryEnqueue(() => { if (!closing) { mediaReady = true; UpdateActivity(); } });
        media.MediaEnded += (_, _) => queue.TryEnqueue(() => { if (!closing && introPlaying) FinishIntro(); });
        media.MediaFailed += (_, error) => queue.TryEnqueue(() =>
        {
            if (closing) return; mediaFailed = true; SetStatus("Video could not play: " + error.ErrorMessage);
            if (introPlaying) FinishIntro();
        });
        timer = queue.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100); timer.Tick += (_, _) => Tick();
        root.Loaded += async (_, _) => { Program.Trace("Root Loaded"); if (!initialized) { initialized = true; await GuardAsync(InitializeAsync); } };
        root.KeyDown += async (_, e) => { if (await ShortcutAsync(e.Key)) e.Handled = true; };
        Activated += (_, _) => { if (!closing) UpdateActivity(); };
        AppWindow.Closing += async (_, e) =>
        {
            if (allowClose) return; e.Cancel = true;
            warnUnsavedChanges?.Invoke();
            if (!showingDialog) await GuardAsync(ConfirmExitAsync);
        };
        Closed += (_, _) =>
        {
            closing = true; timer.Stop(); ClearCards();
            mediaElement.SetMediaPlayer(null); media.Dispose(); sessions.Dispose();
        };
    }

    private static TextBlock Text(string value, double size = 14, bool bold = false) => new()
    { Text = value, FontSize = size, FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    private Button Button(string label, int icon, Func<Task> action, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (icon >= 0) content.Children.Add(Visuals.Icon(icon, 26));
        if (label.Length > 0) content.Children.Add(Text(label, 14, primary));
        var button = new Button { Content = content, Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        if (primary) { button.Background = Visuals.Brush(175, 139, 239); button.Foreground = Visuals.Brush(25, 17, 44); }
        button.Tag = action;
        button.Click += async (_, _) => await GuardAsync(action);
        AutomationProperties.SetName(button, label.Length > 0 ? label : "Edit profile");
        return button;
    }

    private async Task InitializeAsync()
    {
        Program.Trace("Reading settings");
        store.Diagnostic = Program.Option("--smoke-test") is null ? null : Program.Trace;
        settings = await Task.Run(() => store.GetPreferenceAsync<UserSettings>("settings")) ?? new();
        Program.Trace("Reading library");
        library = await store.GetAllAsync(default); Program.Trace("Applying settings"); ApplySettings(); Program.Trace("Building Recommended"); ShowRecommended(); Program.Trace("Starting timer"); timer.Start();
        if (settings.IntroEnabled && !Program.Arguments.Contains("--skip-intro")) StartIntro();
        if (Program.Option("--smoke-test") is { } evidence) _ = RunSmokeTestAsync(evidence);
    }

    private void ApplySettings()
    {
        root.RequestedTheme = settings.LightMode ? ElementTheme.Light : ElementTheme.Dark;
        veil.Background = settings.LightMode ? new SolidColorBrush(Color.FromArgb(224, 245, 243, 255)) : new SolidColorBrush(Color.FromArgb(195, 13, 12, 30));
        status.Foreground = settings.LightMode ? Visuals.Brush(76, 66, 100) : Visuals.Brush(185, 177, 206);
        controllerStatus.Foreground = status.Foreground;
        profileHost.Children.Clear();
        var avatar = new Border { Width = 80, Height = 80, CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(2), BorderBrush = Visuals.Brush(194, 153, 255),
            Background = Visuals.Brush(46, 32, 74), Child = settings.ProfilePicture is { } p && File.Exists(p) ? Visuals.Picture(p, 160) : Visuals.Icon(11, 76) };
        profileHost.Children.Add(avatar);
        var identity = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170 };
        identity.Children.Add(Text(settings.ProfileName, 20, true));
        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (var role in settings.Roles.Take(2))
        {
            var color = TextColor(ParseColor(role.Color), settings.LightMode);
            badges.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2),
                Child = new TextBlock { Text = role.Name, FontSize = 10, Foreground = new SolidColorBrush(color) } });
        }
        identity.Children.Add(badges); profileHost.Children.Add(identity);
        if (!introPlaying) SetBackground();
        UpdateActivity();
    }

    private static Color ParseColor(string value)
    {
        try { return Color.FromArgb(255, Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16), Convert.ToByte(value.Substring(5, 2), 16)); }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException) { return Visuals.Color(201, 167, 255); }
    }
    private static Color TextColor(Color color, bool light) => light ? Color.FromArgb(255, (byte)(color.R * .52), (byte)(color.G * .52), (byte)(color.B * .52)) : color;

    private void SetBackground()
    {
        mediaReady = false; mediaFailed = false; media.Pause(); media.Source = null;
        string path = settings.BackgroundPath is { } file && File.Exists(file) ? file : Visuals.Asset("energy-clouds.png");
        bool video = Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        background.Visibility = video ? Visibility.Collapsed : Visibility.Visible;
        mediaElement.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        if (video) { media.IsLoopingEnabled = true; media.Source = MediaSource.CreateFromUri(new Uri(path)); }
        else background.Source = new BitmapImage(new Uri(path)) { DecodePixelWidth = 1600 };
    }

    private void StartIntro()
    {
        introPlaying = true; introLayer.Visibility = Visibility.Visible; introLayer.Children.Clear();
        introLayer.Background = new SolidColorBrush(Color.FromArgb(100, 8, 7, 23));
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 14 };
        stack.Children.Add(Text("GLauncher", 68, true)); stack.Children.Add(new TextBlock { Text = "A LITTLE ENERGY. A WHOLE NEW WORLD.", FontSize = 12, CharacterSpacing = 160, HorizontalAlignment = HorizontalAlignment.Center });
        var skip = Button("Enter your worlds", 0, () => { FinishIntro(); return Task.CompletedTask; }); skip.HorizontalAlignment = HorizontalAlignment.Center; skip.Margin = new Thickness(0, 36, 0, 0); stack.Children.Add(skip);
        introLayer.Children.Add(stack); mediaReady = false; media.IsLoopingEnabled = false;
        mediaElement.Visibility = Visibility.Visible; background.Visibility = Visibility.Collapsed;
        media.Source = MediaSource.CreateFromUri(new Uri(Visuals.Asset("opening.mp4"))); UpdateActivity();
    }
    private void FinishIntro() { introPlaying = false; introLayer.Visibility = Visibility.Collapsed; introLayer.Children.Clear(); SetBackground(); UpdateActivity(); }

    private async Task NavigateAsync(string destination)
    {
        if (introPlaying) FinishIntro();
        if (showingDialog) return;
        library = await store.GetAllAsync(default);
        switch (destination) { case "Library": ShowLibrary(); break; case "Settings": ShowSettings(); break; case "Studio": ShowStudio(); break; default: ShowRecommended(); break; }
    }
    private void SetPage(string name, UIElement content)
    {
        ClearCards(); page = name; currentGame = null; libraryGrid = null; librarySearch = null;
        pageHost.Children.Clear(); pageHost.Children.Add(content);
    }
    private static StackPanel PageStack() => new() { Spacing = 16, Margin = new Thickness(6, 12, 6, 0) };
    private static ScrollViewer Scroll(UIElement content) => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static StackPanel Row(params UIElement[] children)
    { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; foreach (var child in children) row.Children.Add(child); return row; }
    private void SetStatus(string value) => status.Text = value;

    private void ShowRecommended()
    {
        var stack = PageStack(); stack.Children.Add(Text("FOR YOU", 11)); stack.Children.Add(Text("Your next adventure.", 42, true));
        stack.Children.Add(Text($"Welcome back, {settings.ProfileName}. Pick a world and make it yours.", 16));
        stack.Children.Add(Row(Button("Your games", 1, () => NavigateAsync("Recommended"), true), Button("Discover", 6, () => { ShowDiscover(); return Task.CompletedTask; }), Button("Add game", 4, () => EditGameAsync(null))));
        if (library.Count == 0) stack.Children.Add(EmptyLibrary());
        else
        {
            stack.Children.Add(Text("A fresh pick from your own library", 19, true));
            var recommendations = new LocalRecommendations().Recommend(library, 8);
            stack.Children.Add(CreateLibraryGrid(recommendations.Select(r => library.First(g => g.Id == r.GameId)).ToArray()));
            stack.Children.Add(Text("Local picks: unplayed games first, then games you have not launched here recently.", 12));
        }
        SetPage("Recommended", Scroll(stack));
    }

    private FrameworkElement EmptyLibrary()
    {
        var stack = new StackPanel { Spacing = 14, MaxWidth = 630, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(34, 40, 34, 40) };
        var art = Visuals.Picture(Visuals.Asset("energy-clouds.png"), 800); art.Height = 180;
        stack.Children.Add(new Border { Child = art, CornerRadius = new CornerRadius(22) });
        stack.Children.Add(Text("Make room for your next world.", 29, true));
        stack.Children.Add(Text("Add a game you own, choose its cover and give it a platform. Your library starts with you.", 16));
        stack.Children.Add(Button("Add your first game", 4, () => EditGameAsync(null), true));
        return new Border { CornerRadius = new CornerRadius(24), Background = new SolidColorBrush(Color.FromArgb(40, 125, 100, 185)), Margin = new Thickness(0, 12, 0, 0), Child = stack };
    }

    private void ShowLibrary()
    {
        var panel = new Grid { RowSpacing = 12, Margin = new Thickness(6, 12, 6, 0) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition());
        var top = PageStack(); top.Margin = new Thickness(0); top.Children.Add(Text("Your worlds.", 40, true));
        var search = new TextBox { PlaceholderText = "Search your library", Width = 320, CornerRadius = new CornerRadius(12) };
        var platform = new ComboBox { Width = 180, PlaceholderText = "All platforms" }; platform.Items.Add("All platforms");
        foreach (var value in Enum.GetValues<GamePlatform>()) platform.Items.Add(Visuals.Platform(value)); platform.SelectedIndex = 0;
        top.Children.Add(Row(search, platform, Button("Add game", 4, () => EditGameAsync(null), true))); panel.Children.Add(top);
        FrameworkElement contents = library.Count == 0 ? Scroll(EmptyLibrary()) : CreateLibraryGrid(library);
        Grid.SetRow(contents, 1); panel.Children.Add(contents); SetPage("Library", panel);
        librarySearch = search; libraryGrid = contents as GridView;
        void Filter()
        {
            if (libraryGrid is null) return;
            ClearCards(); libraryGrid.ItemsSource = library.Where(g => g.Title.Contains(search.Text, StringComparison.OrdinalIgnoreCase) &&
                (platform.SelectedIndex == 0 || (int)g.Platform == platform.SelectedIndex - 1)).Select(g => new CardViewModel(g, settings.LightMode)).ToArray();
        }
        search.TextChanged += (_, _) => Filter(); platform.SelectionChanged += (_, _) => Filter();
    }

    private sealed record CardViewModel(GameEntry Game, bool Light = false)
    {
        public string Title => Game.Title;
        public string Platform => Visuals.Platform(Game.Platform);
        public SolidColorBrush PlatformBrush => new(TextColor(Visuals.PlatformColor(Game.Platform), Light));
        public BitmapImage CoverSource => new(new Uri(Game.CoverPath is { } cover && File.Exists(cover) ? cover : Visuals.Asset("energy-clouds.png"))) { DecodePixelWidth = 342 };
    }
    private sealed record CardVisual(SelectorItem Item, EnergyAura Aura, Grid Root);
    private GridView CreateLibraryGrid(IReadOnlyList<GameEntry> games)
    {
        var grid = new GridView { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true, Margin = new Thickness(-5, 4, -5, 0),
            ItemsSource = games.Select(g => new CardViewModel(g, settings.LightMode)).ToArray(), HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid Width="214" Height="382" Padding="0,14,0,0" Background="Transparent">
                <Grid x:Name="AuraHost" Width="220" Height="368" HorizontalAlignment="Center" VerticalAlignment="Top" IsHitTestVisible="False"/>
                <StackPanel Spacing="6">
                  <Border x:Name="CoverFrame" Width="171" Height="304" CornerRadius="16" BorderThickness="1.5" BorderBrush="{Binding PlatformBrush}" Background="#221B34">
                    <Image Source="{Binding CoverSource}" Stretch="UniformToFill"/>
                  </Border>
                  <TextBlock Text="{Binding Title}" FontSize="15" FontWeight="SemiBold" Margin="22,3,12,0" MaxLines="1" TextTrimming="CharacterEllipsis"/>
                  <TextBlock Text="{Binding Platform}" Foreground="{Binding PlatformBrush}" FontSize="11" Margin="22,0,12,0"/>
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);
        grid.ItemClick += (_, e) => { if (e.ClickedItem is CardViewModel item) ShowGame(item.Game); };
        grid.ContainerContentChanging += PrepareCard;
        return grid;
    }

    private void PrepareCard(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.ItemContainer;
        if (cardVisuals.Remove(item, out var old)) { old.Aura.Clear(); if (focusedCard == old) focusedCard = null; }
        if (args.InRecycleQueue) return;
        if (item.ContentTemplateRoot is not Grid card || card.FindName("AuraHost") is not Grid host || args.Item is not CardViewModel model)
        { if (args.Phase == 0) args.RegisterUpdateCallback(PrepareCard); return; }
        cardVisuals[item] = new CardVisual(item, new EnergyAura(host, Visuals.PlatformColor(model.Game.Platform)), card);
        if (hookedItems.TryGetValue(item, out _)) return;
        hookedItems.Add(item, new object());
        item.PointerEntered += (_, _) => FocusCard(item);
        item.GotFocus += (_, _) => FocusCard(item);
        item.PointerExited += (_, _) => { if (item.FocusState == FocusState.Unfocused) BlurCard(item); };
        item.LostFocus += (_, _) => BlurCard(item);
        item.Unloaded += (_, _) => { if (cardVisuals.Remove(item, out var visual)) { visual.Aura.Clear(); if (focusedCard == visual) focusedCard = null; } };
    }
    private void FocusCard(SelectorItem item)
    {
        if (focusedCard is { } previous && previous.Item != item) { previous.Aura.SetActive(false, 0, 1); previous.Root.RenderTransform = null; }
        focusedCard = cardVisuals.GetValueOrDefault(item); UpdateActivity();
    }
    private void BlurCard(SelectorItem item)
    {
        if (!cardVisuals.TryGetValue(item, out var visual)) return;
        visual.Aura.SetActive(false, 0, 1); visual.Root.RenderTransform = null; if (focusedCard == visual) focusedCard = null;
    }
    private void ClearCards()
    {
        foreach (var visual in cardVisuals.Values) visual.Aura.Clear(); cardVisuals.Clear(); focusedCard = null;
    }

    private void ShowGame(GameEntry game)
    {
        var stack = PageStack(); stack.Children.Add(Button("Back to library", 6, () => NavigateAsync("Library")));
        var details = new Grid { ColumnSpacing = 38, Margin = new Thickness(12, 14, 12, 20) };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) }); details.ColumnDefinitions.Add(new ColumnDefinition());
        details.Children.Add(new Border { Width = 270, Height = 480, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(Visuals.PlatformColor(game.Platform)),
            Child = Visuals.Picture(game.CoverPath is { } cover && File.Exists(cover) ? cover : Visuals.Asset("energy-clouds.png"), 600) });
        var info = new StackPanel { Spacing = 18, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        info.Children.Add(new TextBlock { Text = Visuals.Platform(game.Platform).ToUpperInvariant(), FontSize = 12, Foreground = new SolidColorBrush(TextColor(Visuals.PlatformColor(game.Platform), settings.LightMode)), CharacterSpacing = 100 });
        info.Children.Add(Text(game.Title, 46, true));
        var more = Button("", 13, () => Task.CompletedTask); AutomationProperties.SetName(more, "Game options");
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem { Text = "Edit game information" }; edit.Click += async (_, _) => await GuardAsync(() => EditGameAsync(game)); menu.Items.Add(edit);
        var remove = new MenuFlyoutItem { Text = "Remove from library" }; remove.Click += async (_, _) => await GuardAsync(() => RemoveGameAsync(game)); menu.Items.Add(remove); more.Flyout = menu;
        info.Children.Add(Row(Button("Play now", 0, () => LaunchGameAsync(game), true), more));
        info.Children.Add(Text(string.IsNullOrWhiteSpace(game.Description) ? "Make this page yours. Add a description from the game menu." : game.Description, 17));
        info.Children.Add(Text(game.LastPlayedAt is { } played ? $"Last launched here {played.ToLocalTime():g}" : "Ready for your first launch", 12));
        info.Children.Add(Text(Path.GetFileName(game.Launch.ExecutablePath), 12));
        var active = sessions.GetActiveSessions().FirstOrDefault(s => s.GameId == game.Id);
        if (active is not null)
        {
            info.Children.Add(Text(SessionText(active), 14, true));
            info.Children.Add(Button("Session controls", 15, () => ShowSessionAsync(active.SessionId)));
        }
        Grid.SetColumn(info, 1); details.Children.Add(info); stack.Children.Add(details);
        SetPage("Game", Scroll(stack)); currentGame = game;
    }

    private void ShowDiscover()
    {
        var stack = PageStack(); stack.Children.Add(Text("Find your next world.", 40, true));
        stack.Children.Add(Row(Button("Your games", 1, () => NavigateAsync("Recommended")), Button("Discover", 6, () => Task.CompletedTask, true)));
        stack.Children.Add(Text("Store recommendations arrive in a later build.", 24, true));
        stack.Children.Add(Text("Your local library works offline. Store browsing and free downloads will appear here once a real provider is connected.", 16));
        SetPage("Discover", Scroll(stack));
    }

    private void ShowStudio()
    {
        var stack = PageStack(); stack.Children.Add(Text("Make it yours.", 40, true));
        stack.Children.Add(Text("Cover studio & quick descriptions", 24, true));
        stack.Children.Add(Text("Choose your own 9:16 artwork from each game's edit menu today. Automatic cover generation and descriptions need a provider; they are not connected in this alpha.", 17));
        stack.Children.Add(Button("Open your library", 2, () => NavigateAsync("Library"), true));
        SetPage("Studio", Scroll(stack));
    }

    private async Task LaunchGameAsync(GameEntry game)
    {
        var request = new LaunchRequest(SessionId.New(), game.Id, game.Launch);
        SetStatus("Starting " + game.Title + "…");
        var result = await sessions.LaunchAsync(request, default);
        if (result.Session.Failure is { } failure) { await AlertAsync("Game could not start", failure.Message); return; }
        if (result.Kind == LaunchResultKind.RejectedGameAlreadyActive) { await ShowSessionAsync(result.Session.SessionId); return; }
        game = game with { LastPlayedAt = DateTimeOffset.UtcNow }; await store.UpsertAsync(game, default);
        library = await store.GetAllAsync(default); ShowGame(game); SetStatus(SessionText(result.Session));
        // Native process activation is Windows' decision. Do not steal focus back from the game.
    }

    private static string SessionText(SessionSnapshot snapshot) => snapshot.State switch
    {
        SessionState.Running => $"Game session active · {snapshot.TrackedProcesses.Length} verified process(es)",
        SessionState.TrackingUncertain => "Tracked processes ended · an external handoff may still be running",
        SessionState.TrackingStopped => "Tracking stopped · the game was left alone",
        SessionState.Launching => "Starting game…", SessionState.LaunchFailed => "Launch failed", _ => "Game closed"
    };

    private async Task ShowSessionAsync(SessionId id)
    {
        var snapshot = sessions.GetSession(id); if (snapshot is null) return;
        var dialog = Dialog("Game session");
        dialog.Content = Text(SessionText(snapshot) + "\n\nClose verified processes affects only this launch's verified process set. An external store handoff may survive. Stop tracking leaves the game alone.", 15);
        dialog.PrimaryButtonText = snapshot.Termination == TerminationCapability.Unsupported ? "" : "Close verified processes";
        dialog.SecondaryButtonText = snapshot.IsPossiblyAlive ? "Stop tracking" : ""; dialog.CloseButtonText = "Back";
        var result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            var termination = await sessions.TerminateAsync(new(id, TerminationTrigger.UserMenu), default);
            SetStatus(termination.Detail ?? termination.Outcome.ToString());
        }
        else if (result == ContentDialogResult.Secondary) { await sessions.StopTrackingAsync(id, default); SetStatus("Tracking stopped. The game was left alone."); }
        if (currentGame is { } game) ShowGame(game);
    }

    private async Task ConfirmExitAsync()
    {
        await SaveSettingsAsync(settings);
        var dialog = Dialog("Ready to leave?"); dialog.Content = Text("Your saved library and settings are safe. Games you launched will keep running. Unsaved changes in an open editor must be saved or discarded before exiting.", 15);
        dialog.PrimaryButtonText = "Exit GLauncher"; dialog.CloseButtonText = "Stay here";
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary) { allowClose = true; Close(); }
    }
    private ContentDialog Dialog(string title) => new() { Title = title, XamlRoot = root.XamlRoot, RequestedTheme = root.RequestedTheme, DefaultButton = ContentDialogButton.Close };
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (showingDialog) return ContentDialogResult.None;
        showingDialog = true; currentDialog = dialog;
        try { return await dialog.ShowAsync(); }
        finally { showingDialog = false; currentDialog = null; }
    }
    private async Task AlertAsync(string title, string message)
    { if (closing || showingDialog) { SetStatus(message); return; } var dialog = Dialog(title); dialog.Content = Text(message, 15); dialog.CloseButtonText = "Okay"; await ShowDialogAsync(dialog); }
    private async Task GuardAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception error) { if (!closing) await AlertAsync("Something needs attention", error.Message); }
    }

    private bool Typing => Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot) is TextBox or PasswordBox or RichEditBox;
    private InputPolicySettings InputSettings => InputPolicySettings.Default with { EmergencyCloseEnabled = settings.EmergencyEnabled,
        EmergencyBinding = new("Delete", KeyModifiers.None, TimeSpan.FromSeconds(settings.EmergencyHoldSeconds)) };
    private async Task<bool> ShortcutAsync(VirtualKey key)
    {
        if (closing || !DesktopInput.IsForeground(hwnd)) return false;
        var decision = inputPolicy.Evaluate(DesktopInput.Facts(hwnd, Typing, sessions.GetActiveSessions()), InputSettings);
        if (key == VirtualKey.F && decision.FullscreenToggleAllowed && !showingDialog && !introPlaying)
        {
            AppWindow.SetPresenter(AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen); return true;
        }
        if (key == VirtualKey.Escape && !showingDialog) { await GuardAsync(ConfirmExitAsync); return true; }
        return false;
    }

    private void UpdateActivity()
    {
        if (closing) return;
        activity = DesktopInput.IsForeground(hwnd);
        if (mediaReady)
        {
            if (activity && !mediaFailed && media.PlaybackSession.PlaybackState != MediaPlaybackState.Playing) media.Play();
            else if (!activity && media.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) media.Pause();
        }
        foreach (var visual in cardVisuals.Values)
        {
            bool selected = activity && visual == focusedCard;
            visual.Aura.SetActive(selected && settings.AnimationEnabled, settings.Glow, settings.MotionSpeed);
            if (selected && visual.Root.RenderTransform is not TranslateTransform) visual.Root.RenderTransform = new TranslateTransform { Y = -4 };
            else if (!selected && visual.Root.RenderTransform is not null) visual.Root.RenderTransform = null;
        }
    }

    private void Tick()
    {
        if (closing) return;
        UpdateActivity(); ticks++;
        var active = sessions.GetActiveSessions(); var foreground = DesktopInput.ForegroundSession(active);
        if (!showingDialog)
        {
            string text = active.Count > 0 ? SessionText(active[^1]) + " · Return here to resume controls" : "F fullscreen  ·  Esc exit  ·  Library saved on this device";
            if (lastSessionText != text) { lastSessionText = text; SetStatus(text); }
        }
        bool armed = settings.EmergencyEnabled && foreground is { Termination: not TerminationCapability.Unsupported } && !DesktopInput.ModifiersHeld && DesktopInput.DeleteHeld;
        if (!armed || foreground!.SessionId != armedSession)
        { emergencyClock.Reset(); armedSession = armed ? foreground!.SessionId : null; }
        if (armed)
        {
            if (!emergencyClock.IsRunning) emergencyClock.Start();
            if (emergencyClock.Elapsed.TotalSeconds >= settings.EmergencyHoldSeconds)
            {
                var target = foreground!.SessionId; emergencyClock.Reset(); armedSession = null;
                _ = GuardAsync(async () => { var result = await sessions.TerminateAsync(new(target, TerminationTrigger.EmergencyBinding), default); SetStatus(result.Detail ?? result.Outcome.ToString()); });
            }
        }
        if (!activity && ticks % 10 != 0) { previousButtons = 0; return; }
        var controller = ControllerInput.Read(); controllerStatus.Text = controller.DisplayName;
        if (!activity || showingDialog || Typing || introPlaying) { previousButtons = controller.Buttons; return; }
        ushort pressed = (ushort)(controller.Buttons & ~previousButtons); previousButtons = controller.Buttons;
        if (navigationClock.ElapsedMilliseconds > 230)
        {
            FocusNavigationDirection? direction = (controller.Buttons & 1) != 0 || controller.LeftY > 20000 ? FocusNavigationDirection.Up :
                (controller.Buttons & 2) != 0 || controller.LeftY < -20000 ? FocusNavigationDirection.Down :
                (controller.Buttons & 4) != 0 || controller.LeftX < -20000 ? FocusNavigationDirection.Left :
                (controller.Buttons & 8) != 0 || controller.LeftX > 20000 ? FocusNavigationDirection.Right : null;
            if (direction is { } move) { FocusManager.TryMoveFocus(move, new FindNextElementOptions { SearchRoot = root }); navigationClock.Restart(); }
        }
        if ((pressed & 0x1000) != 0)
        {
            var focus = FocusManager.GetFocusedElement(root.XamlRoot);
            if (focus is Button { Tag: Func<Task> action }) _ = GuardAsync(action);
            else if (focus is GridViewItem item && item.Content is CardViewModel game) ShowGame(game.Game);
        }
        if ((pressed & 0x2000) != 0 && page == "Game") ShowLibrary();
    }
}
