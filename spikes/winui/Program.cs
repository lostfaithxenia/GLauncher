using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace GLauncher.RenderingSpike.WinUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ => new ProbeApp(args));
    }
}

public sealed partial class ProbeApp : Application
{
    private readonly string[] args;
    public ProbeApp(string[] args)
    {
        this.args = args;
        UnhandledException += (_, error) =>
        {
            if (args.Length >= 2) File.AppendAllText(Path.GetFullPath(args[1]) + ".error.txt", error.Exception.ToString());
        };
        InitializeComponent();
    }
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    private Window? window, fixture;
    private MediaPlayer? player;
    private DispatcherQueueTimer? timer;
    private StreamWriter? log;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<SpriteVisual> clouds = new();
    private TextBlock? status;
    private IntPtr hwnd;
    private bool active, mediaReady, animating, closing;
    private string phase = "warming";
    private string? mediaError;
    private double startupMs, lastPosition;
    private TimeSpan lastCpu;
    private long lastTicks;
    private int stage;
    private bool automated;

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        if (args.Length < 2) { Exit(); return; }
        automated = args.Contains("--automated");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
        log = new StreamWriter(Path.GetFullPath(args[1])) { AutoFlush = true };
        window = new Window { Title = "GLauncher rendering spike — WinUI (diagnostic only)" };
        var uiQueue = window.DispatcherQueue;
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        window.AppWindow.Resize(new SizeInt32(1080, 760));
        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(255, 12, 10, 27)) };
        player = new MediaPlayer { IsLoopingEnabled = true, IsMuted = true };
        var media = new MediaPlayerElement { Stretch = Stretch.UniformToFill, Opacity = .24 };
        media.SetMediaPlayer(player); root.Children.Add(media);
        var stack = new StackPanel { Margin = new Thickness(36, 20, 36, 12), Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = "GLauncher / WinUI rendering spike", FontSize = 25, Foreground = new SolidColorBrush(Colors.White) });
        stack.Children.Add(new TextBlock { Text = "Synthetic MP4 · 9:16 diagnostic card · compositor cloud/pulse", FontSize = 15, Foreground = new SolidColorBrush(Colors.LightGray) });
        var area = new Grid { Width = 520, Height = 480, HorizontalAlignment = HorizontalAlignment.Center };
        var host = new Border(); area.Children.Add(host);
        var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
        var layer = compositor.CreateContainerVisual(); layer.Size = new Vector2(520, 480);
        for (int i = 0; i < 10; i++)
        {
            double angle = i * Math.PI / 5;
            var gradient = compositor.CreateRadialGradientBrush();
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(160, 35, 235, 235)));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 20, 100, 220)));
            var puff = compositor.CreateSpriteVisual();
            puff.Size = new Vector2(135 + i % 3 * 16, 92 + i % 2 * 30);
            puff.Offset = new Vector3((float)(176 + Math.Cos(angle) * 132), (float)(190 + Math.Sin(angle) * 195), 0);
            puff.Brush = gradient; layer.Children.InsertAtTop(puff); clouds.Add(puff);
        }
        ElementCompositionPreview.SetElementChildVisual(host, layer);
        area.Children.Add(new Border { Width = 236.25, Height = 420, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(Colors.Turquoise), Background = new SolidColorBrush(Color.FromArgb(255, 15, 37, 52)), Child = new TextBlock { Text = "9:16\n\nTEST COVER\n\nPC", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Colors.White), FontSize = 28 } });
        stack.Children.Add(area);
        status = new TextBlock { FontSize = 15, Foreground = new SolidColorBrush(Colors.LightCyan) }; stack.Children.Add(status);
        stack.Children.Add(new TextBlock { Text = "Diagnostic geometry and focus fixture, not the production UI, game artwork or session tracker.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.LightGray) });
        root.Children.Add(stack); window.Content = root;
        root.Loaded += (_, _) => { if (startupMs == 0) startupMs = (DateTime.Now - process.StartTime).TotalMilliseconds; };
        player.MediaOpened += (_, _) => uiQueue.TryEnqueue(() => { if (closing) return; mediaReady = true; UpdateActivity(); });
        player.MediaFailed += (_, err) => mediaError = err.ErrorMessage;
        window.Activated += (_, state) => { active = state.WindowActivationState != WindowActivationState.Deactivated; UpdateActivity(); };
        window.Closed += (_, _) =>
        {
            closing = true;
            timer?.Stop();
            fixture?.Close(); fixture = null;
            media.SetMediaPlayer(null);
            player.Dispose();
            log.Dispose(); process.Dispose();
        };
        timer = window.DispatcherQueue.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(1); timer.Tick += (_, _) => Sample();
        player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(args[0])));
        lastCpu = process.TotalProcessorTime; lastTicks = Stopwatch.GetTimestamp();
        window.Activate(); timer.Start(); player.Play();
    }

    private bool CanAnimate => !closing && active && !IsIconic(hwnd);
    private void UpdateActivity()
    {
        if (closing) return;
        bool enabled = CanAnimate;
        if (mediaReady) { if (enabled) player!.Play(); else player!.Pause(); }
        if (enabled == animating) return;
        animating = enabled;
        for (int i = 0; i < clouds.Count; i++)
        {
            var puff = clouds[i];
            if (enabled)
            {
                var compositor = puff.Compositor;
                var move = compositor.CreateVector3KeyFrameAnimation();
                move.InsertExpressionKeyFrame(0, "this.StartingValue");
                move.InsertExpressionKeyFrame(1, "this.StartingValue + Vector3(16, 8, 0)");
                move.Duration = TimeSpan.FromSeconds(2.3 + i * .17); move.Direction = AnimationDirection.Alternate; move.IterationBehavior = AnimationIterationBehavior.Forever;
                puff.StartAnimation("Offset", move);
                var pulse = compositor.CreateScalarKeyFrameAnimation(); pulse.InsertKeyFrame(0, .35f); pulse.InsertKeyFrame(.5f, .95f); pulse.InsertKeyFrame(1, .35f);
                pulse.Duration = TimeSpan.FromSeconds(3.4 + i * .26); pulse.IterationBehavior = AnimationIterationBehavior.Forever; puff.StartAnimation("Opacity", pulse);
            }
            else { puff.StopAnimation("Offset"); puff.StopAnimation("Opacity"); }
        }
    }

    private void Sample()
    {
        if (closing) return;
        UpdateActivity();
        long now = Stopwatch.GetTimestamp(); process.Refresh(); var cpu = process.TotalProcessorTime;
        double cpuCore = (cpu - lastCpu).TotalMilliseconds / ((now - lastTicks) * 1000d / Stopwatch.Frequency) * 100;
        lastCpu = cpu; lastTicks = now;
        double position = player!.PlaybackSession.Position.TotalMilliseconds;
        var row = new { seconds = clock.Elapsed.TotalSeconds, phase, pid = process.Id, active, minimized = IsIconic(hwnd), mediaReady, mediaPositionMs = position, mediaAdvanceMs = position - lastPosition, animating, startupUiLoadedMs = startupMs, workingSetMiB = process.WorkingSet64 / 1048576d, privateMiB = process.PrivateMemorySize64 / 1048576d, cpuOneCorePercent = cpuCore, cpuMachinePercent = cpuCore / Environment.ProcessorCount, logicalProcessors = Environment.ProcessorCount, mediaError };
        log!.WriteLine(JsonSerializer.Serialize(row)); lastPosition = position;
        if (CanAnimate) status!.Text = $"{phase} | active {active} | video {position / 1000:0.0}s | animate {animating} | WS {row.workingSetMiB:0} MiB";
        if (!automated) return;
        double sec = clock.Elapsed.TotalSeconds;
        if (stage == 0 && sec >= 5) { phase = "foreground"; stage++; }
        else if (stage == 1 && sec >= 15) { phase = "minimized"; ShowWindow(hwnd, 6); UpdateActivity(); stage++; }
        else if (stage == 2 && sec >= 25) { phase = "foreground-resumed"; ShowWindow(hwnd, 9); window!.Activate(); stage++; }
        else if (stage == 3 && sec >= 35) { phase = "game-focus-fixture"; fixture = new Window { Title = "GLauncher WinUI focus fixture — not a game", Content = new TextBlock { Text = "Focus fixture\nLauncher media and animation should pause.", Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap } }; fixture.AppWindow.Resize(new SizeInt32(420, 190)); fixture.Activate(); stage++; }
        else if (stage == 4 && sec >= 45) { phase = "return"; fixture?.Close(); fixture = null; window!.Activate(); stage++; }
        else if (stage == 5 && sec >= 55) window!.Close();
    }
}
