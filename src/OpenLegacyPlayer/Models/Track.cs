using System.IO;
using System.Text.Json.Serialization;

namespace OpenLegacyPlayer.Models;

/// <summary>
/// A single media item in the library. Mirrors the columns shown in the
/// classic Windows Media Player library view.
/// </summary>
public class Track
{
    public string FilePath { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = "Unknown Artist";
    public string AlbumArtist { get; set; } = "Unknown Artist";
    public string Album { get; set; } = "Unknown Album";
    public string Genre { get; set; } = "Unknown Genre";
    public string Composer { get; set; } = string.Empty;

    public uint Year { get; set; }
    public uint TrackNumber { get; set; }

    /// <summary>Rating from 0-5 stars.</summary>
    public int Rating { get; set; }

    public TimeSpan Duration { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    public MediaKind Kind { get; set; } = MediaKind.Music;

    /// <summary>True when <see cref="FilePath"/> is a network stream (internet radio, etc.).</summary>
    [JsonIgnore]
    public bool IsStream { get; set; }

    /// <summary>Cached album art, stored separately so serialization stays light.</summary>
    [JsonIgnore]
    public byte[]? AlbumArt { get; set; }

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title)
            ? Path.GetFileNameWithoutExtension(FilePath)
            : Title;

    public string DurationText =>
        Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");

    public string SizeText
    {
        get
        {
            if (SizeBytes <= 0) return string.Empty;
            double mb = SizeBytes / 1024d / 1024d;
            return mb >= 1
                ? $"{mb:0.#} MB"
                : $"{SizeBytes / 1024d:0} KB";
        }
    }

    public string YearText => Year == 0 ? "Unknown Year" : Year.ToString();

    /// <summary>Full path of the containing folder.</summary>
    [JsonIgnore]
    public string FolderPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>Just the folder name, used as a group header when browsing a drive.</summary>
    [JsonIgnore]
    public string FolderName
    {
        get
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(dir)) return "Unknown Folder";
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? dir : name;
        }
    }
}

public enum MediaKind
{
    Music,
    Video,
    Picture
}
