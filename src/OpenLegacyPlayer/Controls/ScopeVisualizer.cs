using System.Windows;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Controls;

/// <summary>
/// "Bars and Waves ▸ Scope" — a classic oscilloscope: the raw waveform sweeps
/// across a black stage as a glowing trace, with ghost frames of the last few
/// sweeps hanging behind it as afterglow. The trace colour drifts slowly
/// through the cool half of the wheel, and the vertical gain rides the level.
/// </summary>
public class ScopeVisualizer : VisualizerBase
{
    private const int Points = 420;
    private const int Ghosts = 4;            // afterglow frames

    private readonly float[][] _frames;      // [0] oldest … [Ghosts-1] newest
    private double _hue = 165;               // start teal, WMP-scope style
    private double _gain;

    public ScopeVisualizer()
    {
        _frames = new float[Ghosts][];
        for (int i = 0; i < Ghosts; i++)
            _frames[i] = new float[Points];
    }

    protected override void Update()
    {
        // Rotate the ghost frames and capture a fresh sweep into the newest.
        var oldest = _frames[0];
        Array.Copy(_frames, 1, _frames, 0, Ghosts - 1);
        _frames[Ghosts - 1] = oldest;

        if (IsActive)
            AudioSpectrum.FillWaveform(_frames[Ghosts - 1]);
        else
            Array.Clear(_frames[Ghosts - 1]);

        // Ride the music: quiet = a calm line, loud = tall excursions.
        double targetGain = 0.35 + 1.4 * AudioSpectrum.Level;
        _gain += (targetGain - _gain) * 0.25;

        // Slow drift across teal → cyan → blue → violet and back.
        _t += 0.004;
        _hue = 165 + 75 * Math.Sin(_t * Math.Tau * 0.5);
    }

    private double _t;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Fade <= 0.001) return;

        double midY = h / 2;
        double amp = h * 0.34 * _gain;

        dc.PushOpacity(Fade);

        // Faint centre reference line, like a real scope graticule.
        int dimHue = VizColor.Index(_hue);
        dc.PushOpacity(0.12);
        dc.DrawLine(VizColor.Core[dimHue], new Point(0, midY), new Point(w, midY));
        dc.Pop();

        for (int f = 0; f < Ghosts; f++)
        {
            float[] frame = _frames[f];
            double age = (Ghosts - 1 - f) / (double)Ghosts;   // 0 = newest
            double opacity = f == Ghosts - 1 ? 1.0 : 0.42 * (1 - age);
            if (opacity <= 0.02) continue;

            var geometry = new StreamGeometry();
            using (var gc = geometry.Open())
            {
                gc.BeginFigure(new Point(0, midY + frame[0] * amp), false, false);
                for (int i = 1; i < Points; i++)
                    gc.LineTo(new Point(i / (double)(Points - 1) * w,
                        midY + frame[i] * amp), true, true);
            }
            geometry.Freeze();

            int hueIndex = VizColor.Index(_hue - (Ghosts - 1 - f) * 9);
            dc.PushOpacity(opacity);
            dc.DrawGeometry(null, VizColor.Halo[hueIndex], geometry);
            if (f == Ghosts - 1)
                dc.DrawGeometry(null, VizColor.Core[hueIndex], geometry);
            dc.Pop();
        }

        dc.Pop();
    }
}
