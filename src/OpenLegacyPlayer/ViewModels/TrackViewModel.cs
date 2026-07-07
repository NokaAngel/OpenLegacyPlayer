using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenLegacyPlayer.Models;
using OpenLegacyPlayer.Mvvm;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.ViewModels;

/// <summary>
/// Presentation wrapper around a <see cref="Track"/>. Exposes the raw model
/// plus lazily-decoded album art and a highlight flag for the now-playing row.
/// </summary>
public class TrackViewModel : ObservableObject
{
    private bool _isPlaying;
    private ImageSource? _thumbnail;
    private bool _thumbnailLoaded;

    public TrackViewModel(Track track) => Track = track;

    public Track Track { get; }

    public string DisplayTitle => Track.DisplayTitle;
    public string Artist => Track.Artist;
    public string AlbumArtist => Track.AlbumArtist;
    public string Album => Track.Album;
    public string Genre => Track.Genre;
    public string Composer => Track.Composer;
    public string DurationText => Track.DurationText;
    public string SizeText => Track.SizeText;
    public string YearText => Track.YearText;
    public string FolderName => Track.FolderName;
    public string FolderPath => Track.FolderPath;
    public int Rating => Track.Rating;
    public uint TrackNumber => Track.TrackNumber;
    public string TrackNumberText => Track.TrackNumber == 0 ? string.Empty : Track.TrackNumber.ToString();

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    /// <summary>Small album-art thumbnail, decoded on first access.</summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded)
            {
                _thumbnailLoaded = true;
                _thumbnail = ImageHelper.FromBytes(Track.AlbumArt, 96);
            }
            return _thumbnail;
        }
    }

    public bool HasArt => Track.AlbumArt is { Length: > 0 };
}
