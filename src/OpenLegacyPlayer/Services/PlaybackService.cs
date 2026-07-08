using System.IO;
using System.Windows.Threading;
using NAudio.Wave;
using OpenLegacyPlayer.Models;

namespace OpenLegacyPlayer.Services;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// NAudio-based playback engine. Decoding goes through
/// <see cref="TappedSampleProvider"/> so <see cref="AudioSpectrum"/> always has
/// the live samples the visualizers feed on — that's what makes Bars, Alchemy
/// and Battery genuinely react to the music instead of guessing.
/// </summary>
public class PlaybackService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private WaveOutEvent? _output;
    private WaveStream? _reader;
    private bool _isSeeking;
    private bool _suppressStopEvent;   // true while we tear down deliberately

    private double _volume = 1.0;
    private bool _isMuted;

    public PlaybackService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) =>
        {
            if (!_isSeeking)
                PositionChanged?.Invoke(this, Position);
        };
    }

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler? MediaFailed;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;

    public Track? Current { get; private set; }
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is null) return;
            var clamped = value < TimeSpan.Zero ? TimeSpan.Zero
                : value > _reader.TotalTime ? _reader.TotalTime : value;
            try { _reader.CurrentTime = clamped; } catch { /* non-seekable stream */ }
        }
    }

    // Live streams have no meaningful length, so report zero (hides the seek bar).
    public TimeSpan Duration =>
        Current is { IsStream: true } ? TimeSpan.Zero : _reader?.TotalTime ?? TimeSpan.Zero;

    /// <summary>Volume from 0.0 to 1.0.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0d, 1d);
            ApplyVolume();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyVolume();
        }
    }

    private void ApplyVolume()
    {
        if (_output is not null)
            _output.Volume = _isMuted ? 0f : (float)_volume;
    }

    public void Open(Track track)
    {
        TearDown();
        Current = track;
        _autoPlayWhenReady = false;

        // Network streams can take a moment to connect, so open them off the UI
        // thread to keep the app responsive. Local files open instantly inline.
        if (IsNetwork(track))
        {
            OpenStreamAsync(track);
            return;
        }

        try
        {
            _reader = CreateReader(track.FilePath);
            InitOutput();
            MediaOpened?.Invoke(this, EventArgs.Empty);
            PositionChanged?.Invoke(this, TimeSpan.Zero);
        }
        catch
        {
            TearDown();
            Current = track;
            MediaFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void InitOutput()
    {
        var tap = new TappedSampleProvider(_reader!.ToSampleProvider());
        _output = new WaveOutEvent { DesiredLatency = 200 };
        _output.Init(tap);
        _output.PlaybackStopped += OnPlaybackStopped;
        ApplyVolume();
    }

    private bool _autoPlayWhenReady;

    private async void OpenStreamAsync(Track track)
    {
        try
        {
            var reader = await Task.Run(() => CreateReader(track.FilePath));

            // A newer Open() may have superseded this one while we connected.
            if (!ReferenceEquals(Current, track))
            {
                reader.Dispose();
                return;
            }

            _reader = reader;
            InitOutput();
            MediaOpened?.Invoke(this, EventArgs.Empty);
            PositionChanged?.Invoke(this, TimeSpan.Zero);

            if (_autoPlayWhenReady)
                Play();
        }
        catch
        {
            if (ReferenceEquals(Current, track))
            {
                TearDown();
                Current = track;
                MediaFailed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private static bool IsNetwork(Track track) =>
        track.IsStream ||
        (Uri.TryCreate(track.FilePath, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    /// <summary>
    /// Media Foundation handles the common formats (mp3, m4a/aac, wma, wav,
    /// flac and mp4 audio on Windows 10/11); fall back to NAudio's own readers.
    /// </summary>
    private static WaveStream CreateReader(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".wav")
            return new WaveFileReader(path);
        try
        {
            return new MediaFoundationReader(path);
        }
        catch
        {
            return new AudioFileReader(path);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_suppressStopEvent) return;

        // Streams never "end" — a stop means the connection dropped, so don't
        // try to advance to a next track. Local files nearing their end did.
        bool ended = Current is not { IsStream: true } && _reader is not null &&
                     _reader.TotalTime - _reader.CurrentTime < TimeSpan.FromMilliseconds(700);

        State = PlaybackState.Stopped;
        _timer.Stop();
        AudioSpectrum.Reset();
        StateChanged?.Invoke(this, State);

        if (ended && e.Exception is null)
            MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        if (Current is null) return;
        if (_output is null)
        {
            // A stream is still connecting — play the moment it's ready.
            _autoPlayWhenReady = true;
            return;
        }
        _output.Play();
        State = PlaybackState.Playing;
        _timer.Start();
        StateChanged?.Invoke(this, State);
    }

    public void Pause()
    {
        if (_output is null) return;
        _output.Pause();
        State = PlaybackState.Paused;
        _timer.Stop();
        StateChanged?.Invoke(this, State);
    }

    public void Stop()
    {
        if (_output is not null)
        {
            _suppressStopEvent = true;
            _output.Stop();
            _suppressStopEvent = false;
        }
        if (_reader is not null)
        {
            try { _reader.CurrentTime = TimeSpan.Zero; } catch { }
        }

        AudioSpectrum.Reset();
        State = PlaybackState.Stopped;
        _timer.Stop();
        PositionChanged?.Invoke(this, TimeSpan.Zero);
        StateChanged?.Invoke(this, State);
    }

    public void TogglePlayPause()
    {
        if (State == PlaybackState.Playing) Pause();
        else Play();
    }

    /// <summary>Call while the user drags the seek thumb to suppress timer updates.</summary>
    public void BeginSeek() => _isSeeking = true;

    public void EndSeek(TimeSpan position)
    {
        Position = position;
        _isSeeking = false;
        PositionChanged?.Invoke(this, position);
    }

    private void TearDown()
    {
        _suppressStopEvent = true;
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            try { _output.Dispose(); } catch { }
            _output = null;
        }
        if (_reader is not null)
        {
            try { _reader.Dispose(); } catch { }
            _reader = null;
        }
        _suppressStopEvent = false;
        AudioSpectrum.Reset();
    }

    public void Dispose()
    {
        _timer.Stop();
        TearDown();
        GC.SuppressFinalize(this);
    }
}
