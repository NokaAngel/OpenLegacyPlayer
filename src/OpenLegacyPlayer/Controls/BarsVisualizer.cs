using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// A WMP "Bars and Waves"-style visualizer. WPF's MediaPlayer exposes no sample
/// data, so the bars are driven by a smoothed random walk with periodic pulses —
/// decorative, like the classic skins, rather than a true spectrum analyzer.
/// Renders only while <see cref="IsActive"/> is true; bars decay gracefully to
/// zero when playback pauses.
/// </summary>
public class BarsVisualizer : FrameworkElement
{
    private const int BarCount = 28;
    private const double Gap = 4;

    private readonly double[] _heights = new double[BarCount];  // current, smoothed
    private readonly double[] _targets = new double[BarCount];  // where each bar is headed
    private readonly double[] _peaks = new double[BarCount];    // falling peak caps
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private int _tick;

    private static readonly LinearGradientBrush BarBrush = new(
        new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0xFF, 0x9F, 0xE2, 0xFF), 0.0),
            new GradientStop(Color.FromArgb(0xFF, 0x4F, 0xB3, 0xEE), 0.45),
            new GradientStop(Color.FromArgb(0xFF, 0x17, 0x6B, 0xAE), 1.0),
        },
        new Point(0, 0), new Point(0, 1));

    private static readonly SolidColorBrush PeakBrush =
        new(Color.FromArgb(0xE6, 0xCF, 0xEF, 0xFF));

    private static readonly LinearGradientBrush ReflectionBrush = new(
        new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0x55, 0x6F, 0xC0, 0xEE), 0.0),
            new GradientStop(Color.FromArgb(0x00, 0x6F, 0xC0, 0xEE), 1.0),
        },
        new Point(0, 0), new Point(0, 1));

    static BarsVisualizer()
    {
        BarBrush.Freeze();
        PeakBrush.Freeze();
        ReflectionBrush.Freeze();
    }

    public BarsVisualizer()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _timer.Tick += (_, _) => Step();

        Loaded += (_, _) => { if (IsActive) _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(BarsVisualizer),
        new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>Animate while true; decay to silence when false.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var v = (BarsVisualizer)d;
        // Keep ticking when deactivated so the bars fall smoothly; the timer
        // stops itself once everything has settled at zero.
        if ((bool)e.NewValue && v.IsLoaded)
            v._timer.Start();
    }

    private void Step()
    {
        bool anythingVisible = false;
        _tick++;

        // Occasionally kick a "beat": several bars jump toward a new loudness.
        bool beat = IsActive && _tick % (8 + _random.Next(7)) == 0;
        double loudness = 0.35 + 0.65 * _random.NextDouble();

        for (int i = 0; i < BarCount; i++)
        {
            if (IsActive)
            {
                if (beat && _random.NextDouble() < 0.5)
                {
                    // Shape the spectrum: lows on the left run hotter than highs.
                    double bias = 1.0 - 0.55 * (i / (double)BarCount);
                    _targets[i] = Math.Clamp(loudness * bias * (0.4 + 0.6 * _random.NextDouble()), 0.04, 1);
                }
                else if (_random.NextDouble() < 0.12)
                {
                    _targets[i] = Math.Clamp(_targets[i] + (_random.NextDouble() - 0.5) * 0.3, 0.03, 1);
                }
            }
            else
            {
                _targets[i] = 0;
            }

            // Ease toward the target: rise fast, fall slower (like the real thing).
            double delta = _targets[i] - _heights[i];
            _heights[i] += delta * (delta > 0 ? 0.35 : 0.12);

            // Peak caps drift down and get pushed up by the bar.
            _peaks[i] = Math.Max(_peaks[i] - 0.008, _heights[i]);

            if (_heights[i] > 0.004 || _peaks[i] > 0.004)
                anythingVisible = true;
            else
            {
                _heights[i] = 0;
                _peaks[i] = 0;
            }
        }

        if (!IsActive && !anythingVisible)
            _timer.Stop();

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Bottom fifth of the element is reserved for the reflection.
        double baseline = h * 0.8;
        double barWidth = (w - Gap * (BarCount - 1)) / BarCount;
        if (barWidth <= 0) return;

        for (int i = 0; i < BarCount; i++)
        {
            double x = i * (barWidth + Gap);
            double barH = _heights[i] * baseline;
            if (barH > 0.5)
            {
                dc.DrawRoundedRectangle(BarBrush, null,
                    new Rect(x, baseline - barH, barWidth, barH), 1.5, 1.5);

                // Faint reflection below the baseline.
                double reflH = Math.Min(barH * 0.35, h - baseline);
                dc.DrawRectangle(ReflectionBrush, null,
                    new Rect(x, baseline + 2, barWidth, reflH));
            }

            double peakY = baseline - _peaks[i] * baseline;
            if (_peaks[i] > 0.01)
                dc.DrawRectangle(PeakBrush, null, new Rect(x, peakY - 2, barWidth, 2));
        }
    }
}
