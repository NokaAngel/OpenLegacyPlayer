using System.Windows;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// "Bars and Waves ▸ Ocean Mist" — layers of soft, luminous wave ribbons roll
/// across a black stage like sea fog at night. Every ribbon breathes with its
/// own slice of the spectrum: swells lift and thicken it, and the live
/// waveform ripples along its crest.
/// </summary>
public class OceanMistVisualizer : VisualizerBase
{
    private const int RibbonCount = 5;
    private const int Segments = 72;
    private const int WavePoints = 72;

    private struct Ribbon
    {
        public double YFrac;       // vertical anchor, 0..1
        public double Wavelength;  // sine cycles across the width
        public double Phase;       // scroll offset
        public double Speed;       // scroll speed
        public double HueShift;    // slight tint difference per layer
        public double AmpBase;     // resting swell height
    }

    private readonly Ribbon[] _ribbons = new Ribbon[RibbonCount];
    private readonly double[] _energy = new double[RibbonCount];   // smoothed band per ribbon
    private readonly float[] _wave = new float[WavePoints];
    private double _t;

    // Misty blue-white strokes, widest to finest — a fog bank in three passes.
    private static readonly Pen MistWide;
    private static readonly Pen MistMid;
    private static readonly Pen MistCore;

    static OceanMistVisualizer()
    {
        var wide = new SolidColorBrush(Color.FromArgb(0x1E, 0x9E, 0xD4, 0xF2));
        wide.Freeze();
        MistWide = new Pen(wide, 22) { LineJoin = PenLineJoin.Round };
        MistWide.Freeze();

        var mid = new SolidColorBrush(Color.FromArgb(0x3C, 0xB9, 0xE4, 0xFA));
        mid.Freeze();
        MistMid = new Pen(mid, 8) { LineJoin = PenLineJoin.Round };
        MistMid.Freeze();

        var core = new SolidColorBrush(Color.FromArgb(0xAA, 0xE4, 0xF6, 0xFF));
        core.Freeze();
        MistCore = new Pen(core, 1.8) { LineJoin = PenLineJoin.Round };
        MistCore.Freeze();
    }

    public OceanMistVisualizer()
    {
        var random = new Random();
        for (int i = 0; i < RibbonCount; i++)
        {
            _ribbons[i] = new Ribbon
            {
                YFrac = 0.24 + i * 0.52 / (RibbonCount - 1),
                Wavelength = 1.4 + random.NextDouble() * 1.8,
                Phase = random.NextDouble() * Math.Tau,
                Speed = 0.008 + random.NextDouble() * 0.012,
                HueShift = (random.NextDouble() - 0.5) * 24,
                AmpBase = 0.025 + random.NextDouble() * 0.02,
            };
        }
    }

    protected override void Update()
    {
        _t += 1;

        float[] bands = AudioSpectrum.ComputeBands(RibbonCount);
        if (IsActive)
            AudioSpectrum.FillWaveform(_wave);

        for (int i = 0; i < RibbonCount; i++)
        {
            // Deep layers ride the lows, upper layers ride the highs.
            double target = IsActive ? bands[i] : 0;
            _energy[i] += (target - _energy[i]) * 0.22;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Fade <= 0.001) return;

        dc.PushOpacity(Fade);

        for (int i = 0; i < RibbonCount; i++)
        {
            ref readonly Ribbon r = ref _ribbons[i];
            double yBase = r.YFrac * h;
            double swell = h * (r.AmpBase + 0.085 * _energy[i]);
            double ripple = h * 0.05 * _energy[i];
            double phase = r.Phase + _t * r.Speed;

            var geometry = new StreamGeometry();
            using (var gc = geometry.Open())
            {
                bool first = true;
                for (int s = 0; s <= Segments; s++)
                {
                    double fx = s / (double)Segments;
                    double y = yBase
                        + Math.Sin(fx * r.Wavelength * Math.Tau + phase) * swell
                        + Math.Sin(fx * r.Wavelength * 2.7 * Math.Tau - phase * 1.6) * swell * 0.35
                        + _wave[s % WavePoints] * ripple;
                    var pt = new Point(fx * w, y);
                    if (first) { gc.BeginFigure(pt, false, false); first = false; }
                    else gc.LineTo(pt, true, true);
                }
            }
            geometry.Freeze();

            // Brighter when its band sings; the fog never fully vanishes.
            dc.PushOpacity(0.35 + 0.65 * _energy[i]);
            dc.DrawGeometry(null, MistWide, geometry);
            dc.DrawGeometry(null, MistMid, geometry);
            dc.DrawGeometry(null, MistCore, geometry);
            dc.Pop();
        }

        dc.Pop();
    }
}
