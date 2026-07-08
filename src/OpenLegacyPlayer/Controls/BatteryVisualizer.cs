using System.Windows;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Controls;

public enum BatteryPreset
{
    /// <summary>Kaleidoscopic smoke + a breathing mandala, palette drifting the whole wheel.</summary>
    SepiaSwirl,

    /// <summary>Slow amorphous nebula clouds locked to deep purples.</summary>
    PurpleHaze
}

/// <summary>
/// "Battery" — the moody, smoky end of the WMP visualization spectrum.
/// Soft glowing clouds orbit the centre on a pure black stage; bass swells
/// fling them outward and brighten them, the mids shape the mandala flower
/// (sepiaswirl only), and the palette either drifts around the whole colour
/// wheel (sepiaswirl) or breathes inside deep violet (purple haze).
/// </summary>
public class BatteryVisualizer : VisualizerBase
{
    private const int TrailLength = 5;
    private const int PetalPoints = 84;      // mandala outline resolution

    private struct Particle
    {
        public double Angle;      // current polar angle
        public double Radius;     // 0..1 of half-min-dimension
        public double Drift;      // angular speed
        public double Speed;      // radial speed
        public double Size;       // blob size factor
        public double Life;       // 1 → 0
        public double HueJitter;
    }

    private Particle[] _particles = Array.Empty<Particle>();
    private Point[,] _trails = new Point[0, 0];
    private readonly double[] _petals = new double[PetalPoints];
    private readonly Random _random = new();
    private double _t;
    private double _paletteHue = 28;         // sepia territory
    private double _bassSmooth;
    private int _head;

    public static readonly DependencyProperty PresetProperty = DependencyProperty.Register(
        nameof(Preset), typeof(BatteryPreset), typeof(BatteryVisualizer),
        new PropertyMetadata(BatteryPreset.SepiaSwirl, (d, _) => ((BatteryVisualizer)d).Configure()));

    public BatteryPreset Preset
    {
        get => (BatteryPreset)GetValue(PresetProperty);
        set => SetValue(PresetProperty, value);
    }

    // Per-preset tuning, set in Configure().
    private int _symmetry;
    private int _particleCount;
    private double _blobScale;
    private bool _showMandala;

    public BatteryVisualizer() => Configure();

    private void Configure()
    {
        if (Preset == BatteryPreset.PurpleHaze)
        {
            _symmetry = 2;              // loose two-arm spiral, reads as free-form smoke
            _particleCount = 14;
            _blobScale = 2.1;           // big soft nebula clouds
            _showMandala = false;
        }
        else
        {
            _symmetry = 6;
            _particleCount = 12;
            _blobScale = 1.0;
            _showMandala = true;
        }

        _particles = new Particle[_particleCount];
        _trails = new Point[_particleCount, TrailLength];
        for (int i = 0; i < _particleCount; i++)
            _particles[i] = SpawnParticle(fresh: false);
    }

    private Particle SpawnParticle(bool fresh)
    {
        return new Particle
        {
            Angle = _random.NextDouble() * Math.Tau,
            Radius = fresh ? 0.05 : _random.NextDouble() * 0.8,
            Drift = (_random.NextDouble() - 0.35) * 0.02,
            Speed = 0.0012 + _random.NextDouble() * 0.0038,
            Size = 0.10 + _random.NextDouble() * 0.10,
            Life = 1,
            HueJitter = (_random.NextDouble() - 0.5) * 46,
        };
    }

    /// <summary>The palette centre for this frame, per preset.</summary>
    private double CurrentHue => Preset == BatteryPreset.PurpleHaze
        ? 278 + 16 * Math.Sin(_t * 0.35)                 // breathe inside violet
        : _paletteHue;                                   // roam the whole wheel

    protected override void Update()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        float[] bands = AudioSpectrum.ComputeBands(24);
        double bass = (bands[0] + bands[1] + bands[2]) / 3.0;
        _bassSmooth += (bass - _bassSmooth) * 0.3;
        double level = AudioSpectrum.Level;

        _t += 0.02;
        // "changes color and stuff": one full trip around the wheel ≈ 45 s.
        _paletteHue = (_paletteHue + 0.2) % 360;

        double cx = w / 2, cy = h / 2, scale = Math.Min(w, h) / 2;
        _head = (_head + 1) % TrailLength;
        for (int i = 0; i < _particleCount; i++)
        {
            ref Particle p = ref _particles[i];
            p.Angle += p.Drift * (1 + 1.6 * level);
            p.Radius += p.Speed * (1 + 4.5 * _bassSmooth);
            p.Life -= 0.004;

            if (p.Life <= 0 || p.Radius > 1.05)
                p = SpawnParticle(fresh: true);

            _trails[i, _head] = new Point(
                cx + Math.Cos(p.Angle) * p.Radius * scale,
                cy + Math.Sin(p.Angle) * p.Radius * scale);
        }

        if (_showMandala)
        {
            // Mandala petals: mid bands wrapped around the circle, smoothed so
            // the flower breathes instead of flickering.
            for (int i = 0; i < PetalPoints; i++)
            {
                double target = bands[4 + i * 16 / PetalPoints];   // bands 4..19
                _petals[i] += (target - _petals[i]) * 0.25;
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Fade <= 0.001) return;
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2, cy = h / 2, scale = Math.Min(w, h) / 2;
        dc.PushOpacity(Fade);

        // --- Smoke, mirrored around the centre with N-fold symmetry ---------
        double armStep = Math.Tau / _symmetry;
        double brightness = 0.42 + 0.5 * AudioSpectrum.Level;
        for (int i = 0; i < _particleCount; i++)
        {
            ref readonly Particle p = ref _particles[i];
            double blobR = p.Size * scale * _blobScale * (0.55 + 0.45 * p.Life) *
                           (1 + 0.5 * _bassSmooth);
            int hueIndex = VizColor.Index(CurrentHue + p.HueJitter + p.Radius * 40);

            for (int t = 0; t < TrailLength; t++)
            {
                int slot = ((_head - t) % TrailLength + TrailLength) % TrailLength;
                Point pt = _trails[i, slot];
                double px = pt.X - cx, py = pt.Y - cy;
                double trailFade = 1 - t / (double)TrailLength;
                double op = brightness * p.Life * trailFade * trailFade;
                if (op <= 0.015) continue;

                dc.PushOpacity(op);
                for (int arm = 0; arm < _symmetry; arm++)
                {
                    double a = arm * armStep + _t * 0.05;   // whole field slowly rotates
                    double rx = px * Math.Cos(a) - py * Math.Sin(a);
                    double ry = px * Math.Sin(a) + py * Math.Cos(a);
                    dc.DrawEllipse(VizColor.Smoke[hueIndex], null,
                        new Point(cx + rx, cy + ry), blobR * trailFade, blobR * trailFade);
                }
                dc.Pop();
            }
        }

        // --- Centre mandala flower (sepiaswirl only) --------------------------
        if (_showMandala)
        {
            double baseR = scale * 0.22 * (1 + 0.35 * AudioSpectrum.Level);
            int flowerHue = VizColor.Index(CurrentHue + 180);   // complementary
            int innerHue = VizColor.Index(CurrentHue + 140);

            // A soft pool of light under the flower.
            dc.PushOpacity(0.5);
            dc.DrawEllipse(VizColor.Smoke[flowerHue], null,
                new Point(cx, cy), baseR * 0.85, baseR * 0.85);
            dc.Pop();

            for (int ring = 0; ring < 2; ring++)
            {
                double ringScale = ring == 0 ? 1.0 : 0.45;
                double spin = _t * (ring == 0 ? 0.3 : -0.5);
                var geometry = new StreamGeometry();
                using (var gc = geometry.Open())
                {
                    bool first = true;
                    for (int i = 0; i <= PetalPoints; i++)
                    {
                        int idx = i % PetalPoints;
                        double ang = idx / (double)PetalPoints * Math.Tau + spin;
                        double r = baseR * ringScale * (0.72 + 0.5 * _petals[idx]);
                        var pt = new Point(cx + Math.Cos(ang) * r, cy + Math.Sin(ang) * r);
                        if (first) { gc.BeginFigure(pt, false, false); first = false; }
                        else gc.LineTo(pt, true, true);
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, ring == 0 ? VizColor.Core[flowerHue] : VizColor.Core[innerHue],
                    geometry);
            }
        }

        dc.Pop();
    }
}
