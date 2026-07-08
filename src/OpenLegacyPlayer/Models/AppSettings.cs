namespace OpenLegacyPlayer.Models;

/// <summary>
/// Everything the app remembers between sessions (beyond the library and
/// playlists, which have their own files). Serialized to
/// <c>%AppData%\OpenLegacyPlayer\settings.json</c>.
/// </summary>
public class AppSettings
{
    // Playback
    public double Volume { get; set; } = 0.7;
    public bool IsMuted { get; set; }
    public bool IsShuffle { get; set; }
    public bool IsRepeat { get; set; }

    // Window
    public double WindowWidth { get; set; } = 1180;
    public double WindowHeight { get; set; } = 720;
    public bool IsMaximized { get; set; }

    // Now Playing
    public string Visualization { get; set; } = "AlbumArt";

    // Updates
    public bool CheckUpdatesOnStartup { get; set; } = true;
}
