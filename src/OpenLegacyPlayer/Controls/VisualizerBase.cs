using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// Common plumbing for every Now Playing visualization: a ~40 fps render
/// timer that only runs while needed, an <see cref="IsActive"/> switch bound
/// to playback state, and a master <see cref="Fade"/> that eases scenes in
/// when music starts and melts them away on pause.
/// </summary>
public abstract class VisualizerBase : FrameworkElement
{
    private readonly DispatcherTimer _timer;

    /// <summary>Master scene opacity, 0..1 — multiply everything by this.</summary>
    protected double Fade { get; private set; }

    protected VisualizerBase()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };
        _timer.Tick += (_, _) => Tick();

        Loaded += (_, _) => { if (IsActive) _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(VisualizerBase),
        new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>Animate while true; fade out and stop the timer when false.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var v = (VisualizerBase)d;
        if ((bool)e.NewValue && v.IsLoaded)
            v._timer.Start();
    }

    private void Tick()
    {
        Fade = IsActive ? Math.Min(1, Fade + 0.03) : Math.Max(0, Fade - 0.04);

        if (!IsActive && Fade <= 0.001 && IsSettled)
        {
            Fade = 0;
            _timer.Stop();
            InvalidateVisual();
            return;
        }

        Update();
        InvalidateVisual();
    }

    /// <summary>Advance the scene one frame (audio polling, motion, decay).</summary>
    protected abstract void Update();

    /// <summary>Return true once everything has visually settled after a fade-out.</summary>
    protected virtual bool IsSettled => true;
}

/// <summary>
/// One shared, frozen palette for the whole visualizer suite: glow sprites,
/// smoke sprites and stroke pens at every hue step. Built once, reused by
/// every scene, so per-frame rendering never allocates a brush.
/// </summary>
internal static class VizColor
{
    public const int HueSteps = 120;

    public static readonly RadialGradientBrush[] Glow = new RadialGradientBrush[HueSteps];
    public static readonly RadialGradientBrush[] Smoke = new RadialGradientBrush[HueSteps];
    public static readonly Pen[] Core = new Pen[HueSteps];
    public static readonly Pen[] Halo = new Pen[HueSteps];

    static VizColor()
    {
        for (int i = 0; i < HueSteps; i++)
        {
            double hue = i * 360.0 / HueSteps;

            Color vivid = Hsv(hue, 0.72, 1.0);
            var glow = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0xD8, vivid.R, vivid.G, vivid.B), 0.0),
                new GradientStop(Color.FromArgb(0x50, vivid.R, vivid.G, vivid.B), 0.55),
                new GradientStop(Color.FromArgb(0x00, vivid.R, vivid.G, vivid.B), 1.0),
            });
            glow.Freeze();
            Glow[i] = glow;

            Color soft = Hsv(hue, 0.45, 0.96);
            var smoke = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0x7A, soft.R, soft.G, soft.B), 0.0),
                new GradientStop(Color.FromArgb(0x30, soft.R, soft.G, soft.B), 0.55),
                new GradientStop(Color.FromArgb(0x00, soft.R, soft.G, soft.B), 1.0),
            });
            smoke.Freeze();
            Smoke[i] = smoke;

            var coreBrush = new SolidColorBrush(Color.FromArgb(0xE6, vivid.R, vivid.G, vivid.B));
            coreBrush.Freeze();
            var core = new Pen(coreBrush, 2.2)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            core.Freeze();
            Core[i] = core;

            var haloBrush = new SolidColorBrush(Color.FromArgb(0x38, vivid.R, vivid.G, vivid.B));
            haloBrush.Freeze();
            var halo = new Pen(haloBrush, 7.5)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            halo.Freeze();
            Halo[i] = halo;
        }
    }

    /// <summary>Maps any hue in degrees onto a cache index.</summary>
    public static int Index(double hue) =>
        (int)(((hue % 360) + 360) % 360 / 360 * HueSteps) % HueSteps;

    public static Color Hsv(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (h / 60) switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return Color.FromRgb(
            (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
