using System.Windows;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// "Bars and Waves ▸ Bars" — a real spectrum analyzer fed by the FFT bands
/// from <see cref="AudioSpectrum"/>, plus the classic thin waveform ribbon
/// gliding above the bars. Glossy blue bars, falling peak caps, and water
/// reflections under the baseline.
/// </summary>
public class BarsVisualizer : VisualizerBase
{
    private const int BarCount = 28;
    private const int WavePoints = 220;
    private const double Gap = 4;

    private readonly double[] _heights = new double[BarCount];  // current, smoothed
    private readonly double[] _targets = new double[BarCount];  // live band values
    private readonly double[] _peaks = new double[BarCount];    // falling peak caps
    private readonly float[] _wave = new float[WavePoints];

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

    private static readonly Pen WaveHaloPen;
    private static readonly Pen WaveCorePen;

    static BarsVisualizer()
    {
        BarBrush.Freeze();
        PeakBrush.Freeze();
        ReflectionBrush.Freeze();

        var halo = new SolidColorBrush(Color.FromArgb(0x2E, 0x9F, 0xE2, 0xFF));
        halo.Freeze();
        WaveHaloPen = new Pen(halo, 6.5) { LineJoin = PenLineJoin.Round };
        WaveHaloPen.Freeze();

        var core = new SolidColorBrush(Color.FromArgb(0xC8, 0xD5, 0xF2, 0xFF));
        core.Freeze();
        WaveCorePen = new Pen(core, 1.7) { LineJoin = PenLineJoin.Round };
        WaveCorePen.Freeze();
    }

    protected override void Update()
    {
        // Real spectrum from the audio tap: each bar is one log-spaced band.
        float[]? bands = IsActive ? AudioSpectrum.ComputeBands(BarCount) : null;
        if (IsActive)
            AudioSpectrum.FillWaveform(_wave);

        for (int i = 0; i < BarCount; i++)
        {
            _targets[i] = bands?[i] ?? 0;

            // Ease toward the live value: hit hard on the rise, fall away slower.
            double delta = _targets[i] - _heights[i];
            _heights[i] += delta * (delta > 0 ? 0.55 : 0.18);

            // Peak caps drift down and get pushed up by the bar.
            _peaks[i] = Math.Max(_peaks[i] - 0.011, _heights[i]);

            if (_heights[i] < 0.004 && _peaks[i] < 0.004)
            {
                _heights[i] = 0;
                _peaks[i] = 0;
            }
        }
    }

    protected override bool IsSettled
    {
        get
        {
            for (int i = 0; i < BarCount; i++)
                if (_heights[i] > 0 || _peaks[i] > 0)
                    return false;
            return true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Fade <= 0.001) return;

        dc.PushOpacity(Fade);

        // --- The wave: raw waveform ribbon gliding in the upper region ------
        if (IsActive || Fade > 0.05)
        {
            double waveY = h * 0.16;
            double amp = h * 0.13;
            var geometry = new StreamGeometry();
            using (var gc = geometry.Open())
            {
                gc.BeginFigure(new Point(0, waveY + _wave[0] * amp), false, false);
                for (int i = 1; i < WavePoints; i++)
                    gc.LineTo(new Point(i / (double)(WavePoints - 1) * w,
                        waveY + _wave[i] * amp), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, WaveHaloPen, geometry);
            dc.DrawGeometry(null, WaveCorePen, geometry);
        }

        // --- The bars --------------------------------------------------------
        // Bottom fifth of the element is reserved for the reflection.
        double baseline = h * 0.8;
        double barWidth = (w - Gap * (BarCount - 1)) / BarCount;
        if (barWidth > 0)
        {
            for (int i = 0; i < BarCount; i++)
            {
                double x = i * (barWidth + Gap);
                double barH = _heights[i] * baseline * 0.92;
                if (barH > 0.5)
                {
                    dc.DrawRoundedRectangle(BarBrush, null,
                        new Rect(x, baseline - barH, barWidth, barH), 1.5, 1.5);

                    // Faint reflection below the baseline.
                    double reflH = Math.Min(barH * 0.35, h - baseline);
                    dc.DrawRectangle(ReflectionBrush, null,
                        new Rect(x, baseline + 2, barWidth, reflH));
                }

                double peakY = baseline - _peaks[i] * baseline * 0.92;
                if (_peaks[i] > 0.01)
                    dc.DrawRectangle(PeakBrush, null, new Rect(x, peakY - 2, barWidth, 2));
            }
        }

        dc.Pop();
    }
}
