namespace OpenLegacyPlayer.Models;

/// <summary>
/// A named, ordered list of track file paths, backed by an .m3u file on disk.
/// Plain data — <c>PlaylistService</c> handles all the loading and saving.
/// </summary>
public class Playlist
{
    public string Name { get; set; } = "New Playlist";

    /// <summary>Full path to the backing .m3u file (empty until first saved).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Ordered absolute paths of the tracks in the playlist.</summary>
    public List<string> TrackPaths { get; set; } = new();
}
