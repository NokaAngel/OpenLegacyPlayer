using System.IO;
using System.Text.Json;
using OpenLegacyPlayer.Models;

namespace OpenLegacyPlayer.Services;

/// <summary>
/// Scans folders for media, reads tag metadata and persists the library to
/// disk as JSON so it survives across sessions.
/// </summary>
public class LibraryService
{
    private static readonly string[] AudioExtensions =
        { ".mp3", ".m4a", ".aac", ".flac", ".wav", ".wma", ".ogg", ".opus" };

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".wmv", ".mov", ".m4v", ".webm" };

    private static readonly string[] PictureExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

    private readonly string _libraryPath;

    public LibraryService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLegacyPlayer");
        Directory.CreateDirectory(dir);
        _libraryPath = Path.Combine(dir, "library.json");
    }

    public List<Track> Load()
    {
        try
        {
            if (!File.Exists(_libraryPath))
                return new List<Track>();

            string json = File.ReadAllText(_libraryPath);
            return JsonSerializer.Deserialize<List<Track>>(json) ?? new List<Track>();
        }
        catch
        {
            return new List<Track>();
        }
    }

    public void Save(IEnumerable<Track> tracks)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_libraryPath, JsonSerializer.Serialize(tracks, options));
        }
        catch
        {
            // Persisting the library is best-effort; ignore IO failures.
        }
    }

    // Folders that are never worth scanning for media on a full-drive walk.
    private static readonly string[] SkipFolders =
    {
        "$recycle.bin", "system volume information", "windows", "$windows.~ws",
        "$windows.~bt", "programdata", "recovery", "perflogs", "msocache",
        "node_modules", ".git"
    };

    /// <summary>
    /// Recursively scans a folder (or whole drive) and returns every media item
    /// found. Runs on a background thread; report progress via <paramref name="onFound"/>.
    /// When <paramref name="audioOnly"/> is true, only music files are returned —
    /// used when browsing a drive from the navigation pane.
    /// </summary>
    public async Task<List<Track>> ScanFolderAsync(
        string folder,
        Action<Track>? onFound = null,
        bool audioOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<Track>();

            foreach (var file in EnumerateFilesSafe(folder, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var kind = KindFor(file);
                if (kind is null)
                    continue;
                if (audioOnly && kind != MediaKind.Music)
                    continue;

                var track = ReadTrack(file, kind.Value);
                if (track is null)
                    continue;

                results.Add(track);
                onFound?.Invoke(track);
            }

            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Depth-first file walk that tolerates access-denied directories and skips
    /// well-known system folders, so scanning an entire drive never crashes.
    /// </summary>
    private static IEnumerable<string> EnumerateFilesSafe(
        string root, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = stack.Pop();

            string[] files = Array.Empty<string>();
            try { files = Directory.GetFiles(current); }
            catch { /* unreadable directory */ }

            foreach (var file in files)
                yield return file;

            string[] dirs = Array.Empty<string>();
            try { dirs = Directory.GetDirectories(current); }
            catch { continue; }

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir).ToLowerInvariant();
                if (SkipFolders.Contains(name))
                    continue;
                try
                {
                    // Skip reparse points to avoid symlink loops.
                    if ((new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch { continue; }
                stack.Push(dir);
            }
        }
    }

    private static MediaKind? KindFor(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (AudioExtensions.Contains(ext)) return MediaKind.Music;
        if (VideoExtensions.Contains(ext)) return MediaKind.Video;
        if (PictureExtensions.Contains(ext)) return MediaKind.Picture;
        return null;
    }

    /// <summary>
    /// Reads a media file into a <see cref="Track"/>. Pass <paramref name="readArt"/>
    /// = false during bulk drive scans: decoding and copying embedded cover art for
    /// thousands of files is the single biggest source of memory pressure and lag.
    /// </summary>
    public static Track? ReadTrack(string file, MediaKind kind, bool readArt = true)
    {
        try
        {
            var track = new Track
            {
                FilePath = file,
                Kind = kind,
                SizeBytes = new FileInfo(file).Length,
                Title = Path.GetFileNameWithoutExtension(file)
            };

            // Pictures carry no audio tags; keep the filename-derived title.
            if (kind == MediaKind.Picture)
                return track;

            using var tagFile = TagLib.File.Create(file);
            var tag = tagFile.Tag;

            if (!string.IsNullOrWhiteSpace(tag.Title))
                track.Title = tag.Title;

            track.Artist = FirstOrDefault(tag.Performers, "Unknown Artist");
            track.AlbumArtist = FirstOrDefault(tag.AlbumArtists, track.Artist);
            track.Album = string.IsNullOrWhiteSpace(tag.Album) ? "Unknown Album" : tag.Album;
            track.Genre = FirstOrDefault(tag.Genres, "Unknown Genre");
            track.Composer = FirstOrDefault(tag.Composers, string.Empty);
            track.Year = tag.Year;
            track.TrackNumber = tag.Track;
            track.Duration = tagFile.Properties?.Duration ?? TimeSpan.Zero;

            if (readArt)
            {
                var picture = tag.Pictures.FirstOrDefault();
                if (picture is not null)
                    track.AlbumArt = picture.Data.Data;
            }

            return track;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fast, cancellable, two-phase audio scan used when browsing a drive:
    /// first it enumerates and sorts every audio path (cheap — no tag reads),
    /// reporting the total via <paramref name="onCounted"/>; then it reads tags in
    /// folder order and streams tracks through <paramref name="onFound"/>. Cover art
    /// is skipped for speed. Returning paths pre-sorted means the UI never has to
    /// re-sort a huge grouped list at the end.
    /// </summary>
    public Task ScanAudioStreamingAsync(
        string root,
        Action<int>? onCounted,
        Action<Track> onFound,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var paths = new List<string>();
            foreach (var file in EnumerateFilesSafe(root, cancellationToken))
            {
                if (AudioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    paths.Add(file);
            }

            paths.Sort((a, b) =>
            {
                int d = string.Compare(Path.GetDirectoryName(a), Path.GetDirectoryName(b),
                    StringComparison.OrdinalIgnoreCase);
                return d != 0 ? d : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            onCounted?.Invoke(paths.Count);

            foreach (var file in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var track = ReadTrack(file, MediaKind.Music, readArt: false);
                if (track is not null)
                    onFound(track);
            }
        }, cancellationToken);
    }

    private static string FirstOrDefault(string[]? values, string fallback)
    {
        if (values is null) return fallback;
        var v = values.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
