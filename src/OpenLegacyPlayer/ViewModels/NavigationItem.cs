using OpenLegacyPlayer.Models;
using OpenLegacyPlayer.Mvvm;

namespace OpenLegacyPlayer.ViewModels;

/// <summary>Identifies which library view the content pane should show.</summary>
public enum LibraryView
{
    Library,
    Playlists,
    PlaylistItem,
    Music,
    Artist,
    Album,
    Genre,
    Videos,
    Pictures,
    Drive
}

/// <summary>The little icon drawn next to a navigation row.</summary>
public enum NavIcon
{
    Orb,
    Music,
    Artist,
    Album,
    Genre,
    Video,
    Picture,
    Playlist,
    Library,
    Drive,
    RemovableDrive
}

/// <summary>A single row in the left navigation tree.</summary>
public class NavigationItem : ObservableObject
{
    private bool _isSelected;

    public NavigationItem(string title, LibraryView view, int indent = 0,
        NavIcon icon = NavIcon.Orb, bool isHeader = false, bool selectable = true,
        string? scanPath = null, Playlist? playlist = null)
    {
        Title = title;
        View = view;
        Indent = indent;
        Icon = icon;
        IsHeader = isHeader;
        Selectable = selectable;
        ScanPath = scanPath;
        Playlist = playlist;
    }

    public string Title { get; }
    public LibraryView View { get; }
    public int Indent { get; }
    public NavIcon Icon { get; }
    public bool IsHeader { get; }
    public bool Selectable { get; }

    /// <summary>When set, selecting this node scans the given folder/drive for audio.</summary>
    public string? ScanPath { get; }

    /// <summary>The playlist this node represents, for <see cref="LibraryView.PlaylistItem"/> nodes.</summary>
    public Playlist? Playlist { get; }

    public System.Windows.Thickness Indentation => new(8 + Indent * 16, 0, 0, 0);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
