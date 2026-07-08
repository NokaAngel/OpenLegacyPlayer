using System.Windows;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// "Alchemy ▸ Random" — jagged neon ribbons snake across the black, each one a
/// comet with a glowing, lens-flared head and a long zigzag tail. Fully
/// audio-reactive: each comet rides its own FFT band, kinking hard when its
/// slice of the spectrum is loud and gliding silky-smooth in the quiet.
/// </summary>
public class AlchemyVisualizer : VisualizerBase
{
    private const int CometCount = 7;
    private const int TrailLength = 40;

    private struct Comet
    {
        public double FreqX, FreqY;     // orbit frequencies
        public double PhaseX, PhaseY;   // orbit phase offsets
        public double HueOffset;        // degrees around the wheel
        public double Spread;           // how far from centre it roams
    }

    private readonly Comet[] _comets = new Comet[CometCount];
    private readonly Point[,] _trails = new Point[CometCount, TrailLength];
    private readonly float[] _bandNow = new float[CometCount];
    private int _head;
    private int _filled;
    private double _t;
    private int _zigzagSign = 1;

    private static readonly SolidColorBrush FlareBrush =
        new(Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF));
    private static readonly Pen FlarePen;

    static AlchemyVisualizer()
    {
        FlareBrush.Freeze();
        var flare = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        flare.Freeze();
        FlarePen = new Pen(flare, 1.1);
        FlarePen.Freeze();
    }

    public AlchemyVisualizer()
    {
        var random = new Random();
        for (int i = 0; i < CometCount; i++)
        {
            _comets[i] = new Comet
            {
                FreqX = 0.3 + random.NextDouble() * 0.7,
                FreqY = 0.3 + random.NextDouble() * 0.7,
                PhaseX = random.NextDouble() * Math.Tau,
                PhaseY = random.NextDouble() * Math.Tau,
                HueOffset = i * (360.0 / CometCount),
                Spread = 0.3 + random.NextDouble() * 0.13,
            };
        }
    }

    protected override void Update()
    {
        if (!IsActive && _filled > 0 && Fade <= 0.001)
            _filled = 0;

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // The music drives the flight: loud passages fling the comets faster,
        // and each comet's tail kinks with the energy of its own band.
        float[] bands = AudioSpectrum.ComputeBands(CometCount);
        double level = AudioSpectrum.Level;
        _t += 0.012 + 0.05 * level;
        _zigzagSign = -_zigzagSign;

        _head = (_head + 1) % TrailLength;
        for (int i = 0; i < CometCount; i++)
        {
            // Smooth the band a touch so the ribbons breathe rather than strobe.
            _bandNow[i] += (bands[i] - _bandNow[i]) * 0.5f;

            ref readonly Comet o = ref _comets[i];
            double wob = 0.85 + 0.15 * Math.Sin(_t * 0.29 + i);
            double x = w * (0.5 + o.Spread * wob * Math.Sin(o.FreqX * _t + o.PhaseX));
            double y = h * (0.5 + o.Spread * wob * Math.Sin(o.FreqY * _t + o.PhaseY));

            // Zigzag: kick alternate samples perpendicular to the motion,
            // scaled by this comet's band — jagged when loud, silky when quiet.
            int prevSlot = ((_head - 1) % TrailLength + TrailLength) % TrailLength;
            Point prev = _filled > 0 ? _trails[i, prevSlot] : new Point(x, y);
            double dx = x - prev.X, dy = y - prev.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.01)
            {
                double amp = (2.5 + 46 * _bandNow[i]) * _zigzagSign;
                x += -dy / len * amp;
                y += dx / len * amp;
            }

            _trails[i, _head] = new Point(x, y);
        }
        if (_filled < TrailLength) _filled++;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Fade <= 0.001 || _filled < 2) return;

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.PushOpacity(Fade);

        for (int i = 0; i < CometCount; i++)
        {
            // Tail: per-segment lines fading toward the oldest point.
            for (int age = _filled - 2; age >= 0; age--)
            {
                int slotA = ((_head - age - 1) % TrailLength + TrailLength) % TrailLength;
                int slotB = ((_head - age) % TrailLength + TrailLength) % TrailLength;
                Point a = _trails[i, slotA];
                Point b = _trails[i, slotB];

                double life = 1 - age / (double)TrailLength;
                int hueIndex = VizColor.Index(_comets[i].HueOffset + _t * 34 - age * 1.6);

                dc.PushOpacity(0.10 + 0.72 * life * life);
                dc.DrawLine(VizColor.Halo[hueIndex], a, b);
                dc.DrawLine(VizColor.Core[hueIndex], a, b);
                dc.Pop();
            }

            // Comet head: bright glow with a little lens-flare cross.
            Point headPt = _trails[i, _head];
            int headIndex = VizColor.Index(_comets[i].HueOffset + _t * 34);
            double r = 9 + 15 * AudioSpectrum.Level + 10 * _bandNow[i];
            dc.DrawEllipse(VizColor.Glow[headIndex], null, headPt, r, r);
            dc.DrawEllipse(FlareBrush, null, headPt, 1.8, 1.8);
            double f = r * 0.85;
            dc.DrawLine(FlarePen, new Point(headPt.X - f, headPt.Y), new Point(headPt.X + f, headPt.Y));
            dc.DrawLine(FlarePen, new Point(headPt.X, headPt.Y - f), new Point(headPt.X, headPt.Y + f));
        }

        dc.Pop();
    }
}
