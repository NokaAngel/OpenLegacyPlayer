using System.IO;
using System.Text;
using OpenLegacyPlayer.Models;

namespace OpenLegacyPlayer.Services;

/// <summary>
/// Reads and writes playlists as .m3u files under
/// <c>%AppData%\OpenLegacyPlayer\Playlists</c>. The .m3u format is plain text:
/// lines beginning with '#' are directives/comments, every other line is a track path.
/// </summary>
public class PlaylistService
{
    public PlaylistService()
    {
        Directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLegacyPlayer", "Playlists");
        System.IO.Directory.CreateDirectory(Directory);
    }

    /// <summary>The folder that holds every .m3u playlist.</summary>
    public string Directory { get; }

    public List<Playlist> LoadAll()
    {
        var list = new List<Playlist>();
        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.m3u")
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(Load(file));
            }
        }
        catch
        {
            // Missing or unreadable folder — return whatever we managed to load.
        }
        return list;
    }

    public Playlist Load(string path)
    {
        var playlist = new Playlist
        {
            FilePath = path,
            Name = Path.GetFileNameWithoutExtension(path)
        };

        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue; // skip blank lines and #EXTM3U / #EXTINF directives
                playlist.TrackPaths.Add(line);
            }
        }
        catch
        {
            // Corrupt file — treat as empty rather than crashing.
        }

        return playlist;
    }

    /// <summary>Creates a new, empty playlist with a unique file name and saves it.</summary>
    public Playlist Create(string name)
    {
        string safe = Sanitize(string.IsNullOrWhiteSpace(name) ? "New Playlist" : name.Trim());
        string path = Path.Combine(Directory, safe + ".m3u");

        // Avoid clobbering an existing playlist of the same name.
        int n = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(Directory, $"{safe} ({n++}).m3u");
        }

        var playlist = new Playlist
        {
            Name = Path.GetFileNameWithoutExtension(path),
            FilePath = path
        };
        Save(playlist);
        return playlist;
    }

    public void Save(Playlist playlist)
    {
        if (string.IsNullOrEmpty(playlist.FilePath))
            playlist.FilePath = Path.Combine(Directory, Sanitize(playlist.Name) + ".m3u");

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            foreach (var track in playlist.TrackPaths)
                sb.AppendLine(track);
            File.WriteAllText(playlist.FilePath, sb.ToString());
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    public void Delete(Playlist playlist)
    {
        try
        {
            if (File.Exists(playlist.FilePath))
                File.Delete(playlist.FilePath);
        }
        catch { }
    }

    /// <summary>Appends a track to a playlist (ignoring duplicates) and saves it.</summary>
    public void AddTrack(Playlist playlist, string trackPath)
    {
        if (playlist.TrackPaths.Any(p =>
                string.Equals(p, trackPath, StringComparison.OrdinalIgnoreCase)))
            return;

        playlist.TrackPaths.Add(trackPath);
        Save(playlist);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
