using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GLauncher.RenderingSpike.Wpf;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 2) return 2;
        var app = new Application();
        app.Run(new ProbeWindow(System.IO.Path.GetFullPath(args[0]), System.IO.Path.GetFullPath(args[1]), args.Contains("--automated")));
        return 0;
    }
}

internal sealed class ProbeWindow : Window
{
    private readonly MediaElement video = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, IsMuted = true, Volume = 0, Stretch = Stretch.UniformToFill, Opacity = .24 };
    private readonly List<FrameworkElement> clouds = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly StreamWriter log;
    private readonly DispatcherTimer timer;
    private readonly TextBlock status = new() { Foreground = Brushes.LightCyan, FontSize = 15, Margin = new Thickness(16) };
    private readonly bool automated;
    private Window? fixture;
    private string phase = "warming";
    private bool mediaReady, animating, firstFrame;
    private double lastMediaPosition, startupMs;
    private TimeSpan lastCpu;
    private long lastTicks;
    private int stage;
    private readonly Process process = Process.GetCurrentProcess();
    private string? mediaError;

    public ProbeWindow(string videoPath, string logPath, bool runAutomatically)
    {
        automated = runAutomatically;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
        log = new StreamWriter(logPath) { AutoFlush = true };
        Title = "GLauncher rendering spike — WPF (diagnostic only)";
        Width = 1080; Height = 760; MinWidth = 800; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(12, 10, 27));
        var root = new Grid();
        root.Children.Add(video);
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(155, 12, 10, 27)) });
        var stack = new StackPanel { Margin = new Thickness(36, 20, 36, 12) };
        stack.Children.Add(new TextBlock { Text = "GLauncher  /  rendering spike", Foreground = Brushes.White, FontSize = 25, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = "Synthetic MP4 · 9:16 diagnostic card · cloud/pulse animation", Foreground = Brushes.LightGray, FontSize = 15, Margin = new Thickness(0, 8, 0, 0) });
        var area = new Canvas { Height = 480, Width = 520, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < 10; i++)
        {
            double angle = i * Math.PI / 5;
            var shape = new Ellipse { Width = 135 + i % 3 * 16, Height = 92 + i % 2 * 30, Fill = new RadialGradientBrush(Color.FromArgb(160, 35, 235, 235), Color.FromArgb(0, 20, 100, 220)), Effect = new BlurEffect { Radius = 14 }, RenderTransform = new TranslateTransform(), Tag = i };
            Canvas.SetLeft(shape, 176 + Math.Cos(angle) * 132);
            Canvas.SetTop(shape, 190 + Math.Sin(angle) * 195);
            shape.CacheMode = new BitmapCache();
            area.Children.Add(shape); clouds.Add(shape);
        }
        var card = new Border { Width = 236.25, Height = 420, CornerRadius = new CornerRadius(18), BorderBrush = Brushes.Turquoise, BorderThickness = new Thickness(2), Background = new LinearGradientBrush(Color.FromRgb(25, 65, 76), Color.FromRgb(10, 14, 30), 90), Child = new TextBlock { Text = "9:16\n\nTEST COVER\n\nPC", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.SemiBold } };
        Canvas.SetLeft(card, 141.875); Canvas.SetTop(card, 30); area.Children.Add(card);
        stack.Children.Add(area); stack.Children.Add(status);
        stack.Children.Add(new TextBlock { Text = "This tests rendering and focus transitions. It is not the production UI, game art, session tracker or a real game.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, FontSize = 13 });
        root.Children.Add(stack); Content = root;
        video.Source = new Uri(videoPath);
        video.MediaOpened += (_, _) => { mediaReady = true; UpdateActivity(); };
        video.MediaFailed += (_, e) => mediaError = e.ErrorException.ToString();
        video.MediaEnded += (_, _) => { video.Position = TimeSpan.Zero; if (CanAnimate) video.Play(); };
        Activated += (_, _) => UpdateActivity();
        Deactivated += (_, _) => UpdateActivity();
        StateChanged += (_, _) => UpdateActivity();
        ContentRendered += (_, _) => { if (firstFrame) return; firstFrame = true; startupMs = (DateTime.Now - process.StartTime).TotalMilliseconds; video.Play(); UpdateActivity(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Sample();
        Loaded += (_, _) => { lastCpu = process.TotalProcessorTime; lastTicks = Stopwatch.GetTimestamp(); timer.Start(); };
        Closed += (_, _) => { timer.Stop(); video.Close(); fixture?.Close(); log.Dispose(); process.Dispose(); };
    }

    private bool CanAnimate => IsActive && WindowState != WindowState.Minimized;

    private void UpdateActivity()
    {
        bool enabled = CanAnimate;
        if (mediaReady) { if (enabled) video.Play(); else video.Pause(); }
        if (enabled == animating) return;
        animating = enabled;
        foreach (var cloud in clouds)
        {
            int index = (int)cloud.Tag;
            var t = (TranslateTransform)cloud.RenderTransform;
            if (enabled)
            {
                var motion = new DoubleAnimation(-10 - index, 12 + index, TimeSpan.FromSeconds(2.3 + index * .17)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                t.BeginAnimation(TranslateTransform.XProperty, motion);
                cloud.BeginAnimation(OpacityProperty, new DoubleAnimation(.35, .95, TimeSpan.FromSeconds(1.7 + index * .13)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            }
            else { t.BeginAnimation(TranslateTransform.XProperty, null); cloud.BeginAnimation(OpacityProperty, null); }
        }
    }

    private void Sample()
    {
        var now = Stopwatch.GetTimestamp();
        process.Refresh();
        var cpu = process.TotalProcessorTime;
        double elapsedMs = (now - lastTicks) * 1000d / Stopwatch.Frequency;
        double cpuCorePercent = (cpu - lastCpu).TotalMilliseconds / elapsedMs * 100;
        lastCpu = cpu; lastTicks = now;
        double position = video.Position.TotalMilliseconds;
        var row = new { seconds = clock.Elapsed.TotalSeconds, phase, pid = process.Id, active = IsActive, minimized = WindowState == WindowState.Minimized, mediaReady, mediaPositionMs = position, mediaAdvanceMs = position - lastMediaPosition, animating, startupContentRenderedMs = startupMs, workingSetMiB = process.WorkingSet64 / 1048576d, privateMiB = process.PrivateMemorySize64 / 1048576d, cpuOneCorePercent = cpuCorePercent, cpuMachinePercent = cpuCorePercent / Environment.ProcessorCount, logicalProcessors = Environment.ProcessorCount, renderingTier = RenderCapability.Tier >> 16, mediaError };
        log.WriteLine(JsonSerializer.Serialize(row)); lastMediaPosition = position;
        if (CanAnimate) status.Text = $"{phase}  |  active {IsActive}  |  media {position / 1000:0.0}s  |  animate {animating}  |  WS {row.workingSetMiB:0} MiB";
        if (!automated) return;
        double sec = clock.Elapsed.TotalSeconds;
        if (stage == 0 && sec >= 5) { phase = "foreground"; stage++; }
        else if (stage == 1 && sec >= 15) { phase = "minimized"; WindowState = WindowState.Minimized; stage++; }
        else if (stage == 2 && sec >= 25) { phase = "foreground-resumed"; WindowState = WindowState.Normal; Activate(); stage++; }
        else if (stage == 3 && sec >= 35)
        {
            phase = "game-focus-fixture";
            fixture = new Window { Title = "GLauncher focus fixture — not a game", Width = 420, Height = 190, Content = new TextBlock { Text = "Focus fixture\nThe launcher video and aura should pause.", Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap }, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            fixture.Show(); fixture.Activate(); stage++;
        }
        else if (stage == 4 && sec >= 45) { phase = "return"; fixture?.Close(); fixture = null; Activate(); stage++; }
        else if (stage == 5 && sec >= 55) Close();
    }
}
