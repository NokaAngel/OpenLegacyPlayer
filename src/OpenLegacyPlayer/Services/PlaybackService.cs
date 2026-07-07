using System.Windows.Media;
using System.Windows.Threading;
using OpenLegacyPlayer.Models;

namespace OpenLegacyPlayer.Services;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Wraps <see cref="MediaPlayer"/> and exposes a small, UI-friendly surface:
/// transport control, position/volume and events the view model listens to.
/// </summary>
public class PlaybackService
{
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer;
    private bool _isSeeking;

    public PlaybackService()
    {
        _player.MediaOpened += (_, _) =>
        {
            OnPropertyRefresh();
            MediaOpened?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaEnded += (_, _) => MediaEnded?.Invoke(this, EventArgs.Empty);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) =>
        {
            if (!_isSeeking)
                PositionChanged?.Invoke(this, Position);
        };
    }

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;

    public Track? Current { get; private set; }
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan Duration =>
        _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;

    /// <summary>Volume from 0.0 to 1.0.</summary>
    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0d, 1d);
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set => _player.IsMuted = value;
    }

    public void Open(Track track)
    {
        Current = track;
        _player.Open(new Uri(track.FilePath));
    }

    public void Play()
    {
        if (Current is null) return;
        _player.Play();
        State = PlaybackState.Playing;
        _timer.Start();
        StateChanged?.Invoke(this, State);
    }

    public void Pause()
    {
        _player.Pause();
        State = PlaybackState.Paused;
        _timer.Stop();
        StateChanged?.Invoke(this, State);
    }

    public void Stop()
    {
        _player.Stop();
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

    private void OnPropertyRefresh() => PositionChanged?.Invoke(this, Position);
}
