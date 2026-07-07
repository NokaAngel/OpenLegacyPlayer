using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenLegacyPlayer.ViewModels;

namespace OpenLegacyPlayer;

/// <summary>Maps a <see cref="NavIcon"/> to its Segoe MDL2 Assets glyph.</summary>
public class NavGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        int code = value is NavIcon icon
            ? icon switch
            {
                NavIcon.Library => 0xE8F1,          // Library
                NavIcon.Playlist => 0xE142,         // List
                NavIcon.Music => 0xE8D6,            // MusicNote (Audio)
                NavIcon.Artist => 0xE77B,           // Contact
                NavIcon.Album => 0xE93C,            // Album
                NavIcon.Genre => 0xE8EC,            // Tag
                NavIcon.Video => 0xE714,            // Video
                NavIcon.Picture => 0xEB9F,          // Photo
                NavIcon.Drive => 0xEDA2,            // HardDrive
                NavIcon.RemovableDrive => 0xE88E,   // USB
                _ => 0xE904                          // generic circle
            }
            : 0xE904;
        return char.ConvertFromUtf32(code);
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Visible when the bound value is null (used for album-art fallback).</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        bool isNull = value is null;
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>True -> Collapsed, False -> Visible.</summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>
/// Picks between two glyph strings passed as "trueGlyph|falseGlyph" based on a bool.
/// </summary>
public class BoolGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var parts = (p as string ?? "|").Split('|');
        bool b = value is true;
        return b ? parts[0] : (parts.Length > 1 ? parts[1] : parts[0]);
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Renders an integer 0-5 rating as five star glyphs.</summary>
public class RatingConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        int rating = value is int i ? i : 0;
        const char filled = '';  // FavoriteStarFill
        const char empty = '';   // FavoriteStar
        return new string(filled, rating) + new string(empty, Math.Max(0, 5 - rating));
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
