using NAudio.Dsp;
using NAudio.Wave;

namespace OpenLegacyPlayer.Services;

/// <summary>
/// The bridge between the audio pipeline and the visualizers. The playback
/// chain pushes every decoded sample through <see cref="Push"/> (audio thread);
/// visualizers poll <see cref="ComputeBands"/> / <see cref="Level"/> from the
/// UI thread to get a real, FFT-derived picture of what is playing right now.
/// </summary>
public static class AudioSpectrum
{
    private const int FftSize = 2048;               // ~46 ms window at 44.1 kHz
    private static readonly int FftLog2 = (int)Math.Log2(FftSize);

    private static readonly float[] Ring = new float[FftSize];
    private static readonly object Lock = new();
    private static int _writePos;
    private static float _rms;

    /// <summary>Smoothed overall loudness, 0..~1.</summary>
    public static float Level { get; private set; }

    /// <summary>Mixes interleaved samples to mono into the ring buffer.</summary>
    public static void Push(float[] buffer, int offset, int count, int channels)
    {
        if (count <= 0) return;
        channels = Math.Max(1, channels);

        lock (Lock)
        {
            float sumSquares = 0;
            int frames = count / channels;
            for (int f = 0; f < frames; f++)
            {
                float mono = 0;
                int idx = offset + f * channels;
                for (int c = 0; c < channels; c++)
                    mono += buffer[idx + c];
                mono /= channels;

                Ring[_writePos] = mono;
                _writePos = (_writePos + 1) % FftSize;
                sumSquares += mono * mono;
            }

            float rms = (float)Math.Sqrt(sumSquares / Math.Max(1, frames));
            // Fast attack, slow release, so the level rides the music.
            _rms = rms > _rms ? rms * 0.7f + _rms * 0.3f : _rms * 0.92f;
            Level = Math.Clamp(_rms * 2.2f, 0f, 1f);
        }
    }

    /// <summary>
    /// Copies the most recent samples (oldest → newest, downsampled to fit)
    /// into <paramref name="dest"/> — the raw waveform for scope-style scenes.
    /// </summary>
    public static void FillWaveform(float[] dest)
    {
        lock (Lock)
        {
            for (int i = 0; i < dest.Length; i++)
                dest[i] = Ring[(_writePos + i * FftSize / dest.Length) % FftSize];
        }
    }

    /// <summary>Silences the tap (called on stop so visuals settle to zero).</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            Array.Clear(Ring);
            _rms = 0;
            Level = 0;
        }
    }

    /// <summary>
    /// Runs a Hann-windowed FFT over the latest samples and folds the result
    /// into <paramref name="bandCount"/> log-spaced bands scaled roughly 0..1.
    /// </summary>
    public static float[] ComputeBands(int bandCount)
    {
        var complex = new Complex[FftSize];
        lock (Lock)
        {
            // Unroll the ring so index 0 is the oldest sample.
            for (int i = 0; i < FftSize; i++)
            {
                float sample = Ring[(_writePos + i) % FftSize];
                complex[i].X = sample * (float)FastFourierTransform.HannWindow(i, FftSize);
                complex[i].Y = 0;
            }
        }

        FastFourierTransform.FFT(true, FftLog2, complex);

        var bands = new float[bandCount];
        const int binMin = 2;                        // skip DC / sub-bass rumble
        int binMax = FftSize / 2 - 1;

        for (int b = 0; b < bandCount; b++)
        {
            // Log spacing: each band covers a wider slice than the last.
            int lo = (int)Math.Round(binMin * Math.Pow((double)binMax / binMin, b / (double)bandCount));
            int hi = (int)Math.Round(binMin * Math.Pow((double)binMax / binMin, (b + 1) / (double)bandCount));
            hi = Math.Max(lo + 1, Math.Min(hi, binMax));

            float sum = 0;
            for (int i = lo; i < hi; i++)
            {
                float m = (float)Math.Sqrt(
                    complex[i].X * complex[i].X + complex[i].Y * complex[i].Y);
                if (m > sum) sum = m;               // peak within the band pops harder than the average
            }

            // Perceptual-ish scaling with a treble tilt (high bins run quieter).
            double tilt = 0.7 + 1.6 * (b / (double)bandCount);
            bands[b] = (float)Math.Clamp(Math.Sqrt(sum) * 3.4 * tilt, 0, 1);
        }

        return bands;
    }
}

/// <summary>
/// Pass-through sample provider that feeds everything it reads into
/// <see cref="AudioSpectrum"/> on its way to the sound card.
/// </summary>
public class TappedSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public TappedSampleProvider(ISampleProvider source) => _source = source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read > 0)
            AudioSpectrum.Push(buffer, offset, read, WaveFormat.Channels);
        return read;
    }
}
