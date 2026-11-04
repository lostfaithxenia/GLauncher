using System.Numerics;
using GLauncher.Core.Games;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace GLauncher.App;

internal static class Visuals
{
    public static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, "Assets", name);
    public static Color Color(byte r, byte g, byte b) => global::Windows.UI.Color.FromArgb(255, r, g, b);
    public static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color(r, g, b));
    public static string Platform(GamePlatform platform) => platform switch
    { GamePlatform.Ps2 => "PlayStation 2", GamePlatform.Ps3 => "PlayStation 3", GamePlatform.Xbox360 => "Xbox 360", GamePlatform.XboxSeries => "Xbox Series S/X", GamePlatform.Pc => "PC", GamePlatform.Mobile => "Mobile", _ => "Other" };
    public static Color PlatformColor(GamePlatform platform) => platform switch
    { GamePlatform.Ps2 => Color(99, 151, 255), GamePlatform.Ps3 => Color(213, 119, 255), GamePlatform.Xbox360 => Color(153, 238, 129), GamePlatform.XboxSeries => Color(58, 218, 167), GamePlatform.Pc => Color(92, 218, 255), GamePlatform.Mobile => Color(255, 148, 202), _ => Color(251, 200, 104) };
    private static BitmapImage? atlas;
    // The original generated atlas is preserved. Each control clips one equal cell at runtime.
    public static FrameworkElement Icon(int index, double size = 28)
    {
        atlas ??= new BitmapImage(new Uri(Asset("icons.png")));
        var clip = new Canvas { Width = size, Height = size, Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, size, size) } };
        var sprite = new Image { Source = atlas, Width = size * 4, Height = size * 4, Stretch = Stretch.Fill };
        Canvas.SetLeft(sprite, -(index % 4) * size); Canvas.SetTop(sprite, -(index / 4) * size); clip.Children.Add(sprite);
        return clip;
    }
    public static Image Picture(string path, int decodeWidth = 400)
        => new() { Source = new BitmapImage(new Uri(Path.GetFullPath(path))) { DecodePixelWidth = decodeWidth }, Stretch = Stretch.UniformToFill };
}

internal sealed class EnergyAura
{
    private readonly FrameworkElement host;
    private readonly Color color;
    private readonly List<SpriteVisual> puffs = [];
    private ContainerVisual? layer;
    private bool running;
    public EnergyAura(FrameworkElement host, Color color) { this.host = host; this.color = color; }
    public bool Running => running;
    public void SetActive(bool active, double strength, double speed)
    {
        if (active && layer is null)
        {
            var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
            layer = compositor.CreateContainerVisual(); layer.Size = new Vector2(220, 368);
            for (int i = 0; i < 10; i++)
            {
                var brush = compositor.CreateRadialGradientBrush();
                brush.ColorStops.Add(compositor.CreateColorGradientStop(0, global::Windows.UI.Color.FromArgb(150, color.R, color.G, color.B)));
                brush.ColorStops.Add(compositor.CreateColorGradientStop(1, global::Windows.UI.Color.FromArgb(0, color.R, color.G, color.B)));
                var puff = compositor.CreateSpriteVisual(); puff.Brush = brush;
                puff.Size = new Vector2(95 + i % 3 * 10, 105 + i % 2 * 18);
                double angle = i * Math.PI / 5;
                puff.Offset = new Vector3((float)(60 + Math.Cos(angle) * 80), (float)(125 + Math.Sin(angle) * 144), 0);
                layer.Children.InsertAtTop(puff); puffs.Add(puff);
            }
            ElementCompositionPreview.SetElementChildVisual(host, layer);
        }
        if (layer is not null) layer.Opacity = active ? (float)strength : 0;
        if (running == active) return;
        running = active;
        for (int i = 0; i < puffs.Count; i++)
        {
            var puff = puffs[i]; puff.StopAnimation("Offset"); puff.StopAnimation("Opacity");
            if (!active) continue;
            var compositor = puff.Compositor;
            var motion = compositor.CreateVector3KeyFrameAnimation();
            motion.InsertKeyFrame(0, puff.Offset); motion.InsertKeyFrame(.5f, puff.Offset + new Vector3(i % 2 == 0 ? 10 : -10, -12, 0)); motion.InsertKeyFrame(1, puff.Offset);
            motion.Duration = TimeSpan.FromSeconds((3.4 + i * .11) / Math.Clamp(speed, .3, 2)); motion.IterationBehavior = AnimationIterationBehavior.Forever;
            var pulse = compositor.CreateScalarKeyFrameAnimation(); pulse.InsertKeyFrame(0, .35f); pulse.InsertKeyFrame(.5f, .9f); pulse.InsertKeyFrame(1, .35f);
            pulse.Duration = motion.Duration; pulse.IterationBehavior = AnimationIterationBehavior.Forever;
            puff.StartAnimation("Offset", motion); puff.StartAnimation("Opacity", pulse);
        }
    }
    public void Clear()
    {
        SetActive(false, 0, 1); ElementCompositionPreview.SetElementChildVisual(host, null);
        foreach (var puff in puffs) puff.Dispose(); puffs.Clear(); layer?.Dispose(); layer = null;
    }
}
