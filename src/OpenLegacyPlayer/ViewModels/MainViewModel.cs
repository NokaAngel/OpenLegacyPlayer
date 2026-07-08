using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using OpenLegacyPlayer.Models;
using OpenLegacyPlayer.Mvvm;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.ViewModels;

public enum AppViewMode
{
    Library,
    NowPlaying
}

/// <summary>Which scene the Now Playing view renders.</summary>
public enum VisualizationMode
{
    AlbumArt,
    Bars,
    Scope,
    OceanMist,
    Alchemy,
    Battery,
    PurpleHaze
}

public class MainViewModel : ObservableObject
{
    private readonly LibraryService _library = new();
    private readonly PlaybackService _playback = new();
    private readonly PlaylistService _playlistService = new();
    private readonly SettingsService _settings = new();
    private readonly Random _random = new();

    private readonly ObservableCollection<TrackViewModel> _allTracks = new();
    private List<TrackViewModel> _playQueue = new();

    // Drive/folder browsing: cache scanned results and cancel in-flight scans.
    private readonly Dictionary<string, List<TrackViewModel>> _driveCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentQueue<TrackViewModel> _scanQueue = new();
    private List<TrackViewModel> _scanResults = new();
    private CancellationTokenSource? _scanCts;
    private System.Windows.Threading.DispatcherTimer? _scanFlushTimer;

    // How many rows we allow onto the UI thread per timer tick while streaming.
    private const int ScanBatchSize = 120;

    private int _scanTotal;
    private int _scanFound;
    private bool _isScanIndeterminate;

    private NavigationItem? _selectedNav;
    private string _searchText = string.Empty;

    // Back/forward navigation history over the left-pane selection.
    private readonly Stack<NavigationItem> _navBack = new();
    private readonly Stack<NavigationItem> _navForward = new();
    private bool _isHistoryNavigation;

    private readonly UpdateService _updates = new();
    private TrackViewModel? _currentTrack;
    private TrackViewModel? _selectedTrack;
    private string _statusText = string.Empty;
    private bool _isBusy;

    private double _positionSeconds;
    private double _durationSeconds;
    private double _volume = 0.7;
    private bool _isMuted;
    private bool _isShuffle;
    private bool _isRepeat;
    private bool _suppressSeek;

    private AppViewMode _viewMode = AppViewMode.Library;
    private VisualizationMode _visualization = VisualizationMode.AlbumArt;

    public MainViewModel()
    {
        NavigationItems = new ObservableCollection<NavigationItem>();
        CurrentTracks = new ObservableCollection<TrackViewModel>();
        CurrentTracksView = CollectionViewSource.GetDefaultView(CurrentTracks);

        // Restore persisted playback state before the player is wired up.
        var s = _settings.Current;
        _volume = Math.Clamp(s.Volume, 0, 1);
        _isMuted = s.IsMuted;
        _isShuffle = s.IsShuffle;
        _isRepeat = s.IsRepeat;
        if (Enum.TryParse<VisualizationMode>(s.Visualization, out var viz))
            _visualization = viz;

        WirePlayback();
        InitCommands();
        BuildNavigation();

        // Restore any previously scanned library.
        foreach (var track in _library.Load())
            _allTracks.Add(new TrackViewModel(track));

        SelectedNav = NavigationItems.First(n => n.View == LibraryView.Music && !n.IsHeader);
    }

    #region Navigation

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem? SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (value is null || !value.Selectable) return;
            var previous = _selectedNav;
            if (previous is not null) previous.IsSelected = false;
            if (SetProperty(ref _selectedNav, value))
            {
                // Record history unless this change *is* a back/forward jump.
                if (!_isHistoryNavigation && previous is not null)
                {
                    _navBack.Push(previous);
                    _navForward.Clear();
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                }

                value.IsSelected = true;
                RefreshCurrentView();
                OnPropertyChanged(nameof(Breadcrumb));
                OnPropertyChanged(nameof(IsViewingPlaylist));
            }
        }
    }

    public bool CanGoBack => _navBack.Count > 0;
    public bool CanGoForward => _navForward.Count > 0;

    private void GoBack()
    {
        if (_navBack.Count == 0 || _selectedNav is null) return;
        _navForward.Push(_selectedNav);
        NavigateTo(_navBack.Pop());
    }

    private void GoForward()
    {
        if (_navForward.Count == 0 || _selectedNav is null) return;
        _navBack.Push(_selectedNav);
        NavigateTo(_navForward.Pop());
    }

    private void NavigateTo(NavigationItem target)
    {
        _isHistoryNavigation = true;
        try
        {
            // The nav list is rebuilt when drives/playlists change, so the stored
            // item may be stale — fall back to a match by title/view.
            SelectedNav = NavigationItems.Contains(target)
                ? target
                : NavigationItems.FirstOrDefault(n =>
                      n.View == target.View && n.Title == target.Title && n.Selectable)
                  ?? _selectedNav;
        }
        finally
        {
            _isHistoryNavigation = false;
        }
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public string Breadcrumb => SelectedNav?.View switch
    {
        LibraryView.Music => "Library › Music › All music",
        LibraryView.Artist => "Library › Music › Artist",
        LibraryView.Album => "Library › Music › Album",
        LibraryView.Genre => "Library › Music › Genre",
        LibraryView.Videos => "Library › Videos › All videos",
        LibraryView.Pictures => "Library › Pictures › All pictures",
        LibraryView.Playlists => "Library › Playlists",
        LibraryView.PlaylistItem => $"Library › Playlists › {SelectedNav?.Title}",
        LibraryView.Drive => $"{SelectedNav?.Title} › Music",
        _ => "Library"
    };

    /// <summary>Every playlist known to the app — also used by the "Add to playlist" menu.</summary>
    public ObservableCollection<Playlist> Playlists { get; } = new();

    private void BuildNavigation()
    {
        Playlists.Clear();
        foreach (var p in _playlistService.LoadAll())
            Playlists.Add(p);

        NavigationItems.Clear();
        NavigationItems.Add(new NavigationItem("Library", LibraryView.Library, 0, NavIcon.Library, selectable: false));
        NavigationItems.Add(new NavigationItem("Playlists", LibraryView.Playlists, 0, NavIcon.Playlist));
        foreach (var playlist in Playlists)
            NavigationItems.Add(new NavigationItem(
                playlist.Name, LibraryView.PlaylistItem, 1, NavIcon.Playlist, playlist: playlist));

        NavigationItems.Add(new NavigationItem("Music", LibraryView.Music, 0, NavIcon.Music));
        NavigationItems.Add(new NavigationItem("Artist", LibraryView.Artist, 1, NavIcon.Artist));
        NavigationItems.Add(new NavigationItem("Album", LibraryView.Album, 1, NavIcon.Album));
        NavigationItems.Add(new NavigationItem("Genre", LibraryView.Genre, 1, NavIcon.Genre));
        NavigationItems.Add(new NavigationItem("Videos", LibraryView.Videos, 0, NavIcon.Video));
        NavigationItems.Add(new NavigationItem("Pictures", LibraryView.Pictures, 0, NavIcon.Picture));

        foreach (var drive in DriveService.GetDrives())
        {
            NavigationItems.Add(new NavigationItem(
                drive.Label, LibraryView.Drive, 0,
                drive.IsRemovable ? NavIcon.RemovableDrive : NavIcon.Drive,
                scanPath: drive.RootPath));
        }
    }

    /// <summary>Rebuild the nav and reselect a given playlist (used after create/add).</summary>
    private void RefreshPlaylists(Playlist? select = null)
    {
        BuildNavigation();
        if (select is not null)
        {
            var node = NavigationItems.FirstOrDefault(n =>
                n.View == LibraryView.PlaylistItem &&
                string.Equals(n.Playlist?.FilePath, select.FilePath, StringComparison.OrdinalIgnoreCase));
            if (node is not null) SelectedNav = node;
        }
    }

    /// <summary>Re-detect drives (e.g. after plugging in a USB stick).</summary>
    private void RefreshDrives()
    {
        var selectedPath = SelectedNav?.ScanPath;
        BuildNavigation();
        var restore = NavigationItems.FirstOrDefault(n => n.ScanPath == selectedPath)
                      ?? NavigationItems.First(n => n.View == LibraryView.Music);
        SelectedNav = restore;
    }

    #endregion

    #region Content

    public ObservableCollection<TrackViewModel> CurrentTracks { get; }
    public ICollectionView CurrentTracksView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RefreshCurrentView();
        }
    }

    public TrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set => SetProperty(ref _selectedTrack, value);
    }

    /// <summary>Show the "library is empty" hint only on library views, never mid-scan.</summary>
    public bool ShowEmptyHint =>
        !_isBusy && CurrentTracks.Count == 0 && (SelectedNav?.ScanPath is null) &&
        (SelectedNav?.View is null or LibraryView.Music or LibraryView.Artist
            or LibraryView.Album or LibraryView.Genre or LibraryView.Videos or LibraryView.Pictures)
        && _allTracks.Count == 0;

    private void RefreshCurrentView()
    {
        CancelScan();

        if (SelectedNav?.ScanPath is { } scanPath && SelectedNav.View == LibraryView.Drive)
            LoadDriveView(scanPath);
        else if (SelectedNav?.View == LibraryView.PlaylistItem && SelectedNav.Playlist is { } pl)
            _ = LoadPlaylistView(pl);
        else if (SelectedNav?.View == LibraryView.Playlists)
            PopulateList(Enumerable.Empty<TrackViewModel>(), null);
        else
            ShowLibraryView();

        OnPropertyChanged(nameof(ShowEmptyHint));
    }

    // --- Playlists ---------------------------------------------------------

    /// <summary>
    /// Resolves a playlist's file paths to tracks (reusing library entries where
    /// possible, otherwise reading tags off disk) and shows them in playlist order.
    /// </summary>
    private async Task LoadPlaylistView(Playlist playlist)
    {
        CurrentTracks.Clear();
        SetGrouping(null);

        // Index the existing library so we don't re-read tags we already have.
        // Built manually (not ToDictionary) so duplicate paths can't throw.
        var byPath = new Dictionary<string, TrackViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _allTracks)
            byPath[t.Track.FilePath] = t;

        var resolved = await Task.Run(() =>
        {
            var list = new List<TrackViewModel>();
            foreach (var path in playlist.TrackPaths)
            {
                if (byPath.TryGetValue(path, out var known))
                {
                    list.Add(known);
                }
                else if (File.Exists(path))
                {
                    var track = LibraryService.ReadTrack(path, MediaKind.Music);
                    if (track is not null) list.Add(new TrackViewModel(track));
                }
                // Missing files are silently skipped.
            }
            return list;
        });

        // Guard against the user having navigated away while we resolved.
        if (SelectedNav?.Playlist != playlist) return;

        PopulateList(ApplySearch(resolved), null);
        int n = resolved.Count;
        StatusText = n == 0
            ? "This playlist is empty"
            : $"{n:N0} track{(n == 1 ? "" : "s")}";
    }

    private void ShowLibraryView()
    {
        var view = SelectedNav?.View ?? LibraryView.Music;

        MediaKind? kind = view switch
        {
            LibraryView.Videos => MediaKind.Video,
            LibraryView.Pictures => MediaKind.Picture,
            _ => MediaKind.Music
        };

        IEnumerable<TrackViewModel> items = _allTracks
            .Where(t => t.Track.Kind == kind);

        items = ApplySearch(items);

        items = view switch
        {
            LibraryView.Artist => items.OrderBy(t => t.AlbumArtist).ThenBy(t => t.Album).ThenBy(t => t.TrackNumber),
            LibraryView.Album => items.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber),
            LibraryView.Genre => items.OrderBy(t => t.Genre).ThenBy(t => t.DisplayTitle),
            _ => items.OrderBy(t => t.AlbumArtist).ThenBy(t => t.Album).ThenBy(t => t.TrackNumber)
        };

        string groupProp = view switch
        {
            LibraryView.Genre => nameof(TrackViewModel.Genre),
            LibraryView.Album => nameof(TrackViewModel.Album),
            _ => nameof(TrackViewModel.AlbumArtist)
        };

        PopulateList(items, kind == MediaKind.Music ? groupProp : null);
    }

    // --- Drive / folder browsing -------------------------------------------

    private void LoadDriveView(string scanPath)
    {
        // Instant when we've scanned this drive already this session.
        if (_driveCache.TryGetValue(scanPath, out var cached))
        {
            var items = ApplySearch(cached)
                .OrderBy(t => t.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TrackNumber).ThenBy(t => t.DisplayTitle);
            PopulateList(items, nameof(TrackViewModel.FolderName));
            StatusText = $"{cached.Count} audio file{(cached.Count == 1 ? "" : "s")}";
            return;
        }

        _ = ScanDriveAsync(scanPath);
    }

    private async Task ScanDriveAsync(string scanPath)
    {
        CurrentTracks.Clear();
        SetGrouping(nameof(TrackViewModel.FolderName));

        _scanResults = new List<TrackViewModel>();
        while (_scanQueue.TryDequeue(out _)) { }

        IsBusy = true;
        IsScanIndeterminate = true;
        ScanTotal = 0;
        ScanFound = 0;
        StatusText = "Finding audio files…";
        OnPropertyChanged(nameof(ShowEmptyHint));

        var cts = new CancellationTokenSource();
        _scanCts = cts;
        StartFlushTimer();

        try
        {
            await _library.ScanAudioStreamingAsync(scanPath,
                onCounted: total => App.Current.Dispatcher.Invoke(() =>
                {
                    ScanTotal = total;
                    IsScanIndeterminate = total == 0;
                }),
                onFound: track => _scanQueue.Enqueue(new TrackViewModel(track)),
                cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by another selection; CancelScan already tidied up
        }

        // Drain whatever is still queued, then finalise.
        StopFlushTimer();
        DrainQueue(int.MaxValue);

        _driveCache[scanPath] = _scanResults;
        _scanCts = null;
        cts.Dispose();

        // If the user was typing a filter while it scanned, apply it now.
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var filtered = ApplySearch(_scanResults)
                .OrderBy(t => t.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TrackNumber).ThenBy(t => t.DisplayTitle);
            PopulateList(filtered, nameof(TrackViewModel.FolderName));
        }

        int count = _scanResults.Count;
        IsBusy = false;
        IsScanIndeterminate = false;
        StatusText = count > 0
            ? $"{count:N0} audio file{(count == 1 ? "" : "s")} found"
            : "No audio files found";
        OnPropertyChanged(nameof(ShowEmptyHint));
    }

    private void StartFlushTimer()
    {
        _scanFlushTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _scanFlushTimer.Tick -= OnFlushTick;
        _scanFlushTimer.Tick += OnFlushTick;
        _scanFlushTimer.Start();
    }

    private void StopFlushTimer()
    {
        if (_scanFlushTimer is null) return;
        _scanFlushTimer.Stop();
        _scanFlushTimer.Tick -= OnFlushTick;
    }

    private void OnFlushTick(object? sender, EventArgs e) => DrainQueue(ScanBatchSize);

    /// <summary>
    /// Move up to <paramref name="max"/> scanned rows from the background queue onto
    /// the UI collection. Bounding the batch keeps the UI thread responsive no matter
    /// how fast the scanner produces results.
    /// </summary>
    private void DrainQueue(int max)
    {
        int n = 0;
        while (n < max && _scanQueue.TryDequeue(out var vm))
        {
            CurrentTracks.Add(vm);
            _scanResults.Add(vm);
            n++;
        }
        if (n == 0) return;

        ScanFound = CurrentTracks.Count;
        StatusText = _scanTotal > 0
            ? $"Adding files… {_scanFound:N0} of {_scanTotal:N0}"
            : $"Finding audio files… {_scanFound:N0}";
    }

    private void CancelScan()
    {
        StopFlushTimer();
        while (_scanQueue.TryDequeue(out _)) { }
        if (_scanCts is { } cts)
        {
            _scanCts = null;
            cts.Cancel();
            cts.Dispose();
        }
        if (_isBusy)
        {
            IsBusy = false;
            IsScanIndeterminate = false;
        }
    }

    public int ScanTotal
    {
        get => _scanTotal;
        private set
        {
            if (SetProperty(ref _scanTotal, value))
                OnPropertyChanged(nameof(ScanProgressText));
        }
    }

    public int ScanFound
    {
        get => _scanFound;
        private set
        {
            if (SetProperty(ref _scanFound, value))
                OnPropertyChanged(nameof(ScanProgressText));
        }
    }

    public bool IsScanIndeterminate
    {
        get => _isScanIndeterminate;
        private set => SetProperty(ref _isScanIndeterminate, value);
    }

    public string ScanProgressText => _scanTotal > 0
        ? $"Loading media  •  {_scanFound:N0} of {_scanTotal:N0}"
        : "Searching for audio…";

    private IEnumerable<TrackViewModel> ApplySearch(IEnumerable<TrackViewModel> items)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return items;

        string q = _searchText.Trim();
        return items.Where(t =>
            t.DisplayTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Album.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.FolderName.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private void PopulateList(IEnumerable<TrackViewModel> items, string? groupProp)
    {
        CurrentTracks.Clear();
        foreach (var item in items)
            CurrentTracks.Add(item);
        SetGrouping(groupProp);
        OnPropertyChanged(nameof(ShowEmptyHint));
    }

    private void SetGrouping(string? groupProp)
    {
        using (CurrentTracksView.DeferRefresh())
        {
            CurrentTracksView.GroupDescriptions.Clear();
            if (groupProp is not null)
                CurrentTracksView.GroupDescriptions.Add(new PropertyGroupDescription(groupProp));
        }
    }

    #endregion

    #region Playback state

    public TrackViewModel? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (_currentTrack is not null) _currentTrack.IsPlaying = false;
            if (SetProperty(ref _currentTrack, value))
            {
                if (value is not null) value.IsPlaying = true;
                OnPropertyChanged(nameof(NowPlayingTitle));
                OnPropertyChanged(nameof(NowPlayingArtist));
                OnPropertyChanged(nameof(NowPlayingAlbum));
                OnPropertyChanged(nameof(NowPlayingArt));
                OnPropertyChanged(nameof(HasCurrentTrack));
            }
        }
    }

    public bool HasCurrentTrack => _currentTrack is not null;

    public string NowPlayingTitle => _currentTrack?.DisplayTitle ?? "OpenLegacy Player";

    public string NowPlayingArtist =>
        _currentTrack is null ? "Select something to play" : _currentTrack.Artist;

    public string NowPlayingAlbum =>
        _currentTrack is null ? string.Empty : _currentTrack.Album;

    public ImageSource? NowPlayingArt =>
        _currentTrack is { HasArt: true } t
            ? ImageHelper.FromBytes(t.Track.AlbumArt, 400)
            : null;

    public bool IsPlaying => _playback.State == PlaybackState.Playing;

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (SetProperty(ref _positionSeconds, value))
            {
                OnPropertyChanged(nameof(PositionText));
                if (!_suppressSeek && Math.Abs(_playback.Position.TotalSeconds - value) > 0.75)
                    _playback.EndSeek(TimeSpan.FromSeconds(value));
            }
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, value))
                OnPropertyChanged(nameof(DurationText));
        }
    }

    public string PositionText => TimeSpan.FromSeconds(_positionSeconds).ToString(
        _positionSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    public string DurationText => TimeSpan.FromSeconds(_durationSeconds).ToString(
        _durationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _playback.Volume = value;
                if (value > 0 && _isMuted) IsMuted = false;
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _playback.IsMuted = value;
                OnPropertyChanged(nameof(VolumeGlyph));
            }
        }
    }

    public string VolumeGlyph => _isMuted || _volume <= 0.001
        ? ""   // mute
        : _volume < 0.5 ? ""   // volume low
                        : ""; // volume high

    public bool IsShuffle
    {
        get => _isShuffle;
        set => SetProperty(ref _isShuffle, value);
    }

    public bool IsRepeat
    {
        get => _isRepeat;
        set => SetProperty(ref _isRepeat, value);
    }

    public AppViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetProperty(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(IsNowPlaying));
                OnPropertyChanged(nameof(IsLibraryMode));
                RaiseVisualizationChanged();
            }
        }
    }

    public bool IsNowPlaying => _viewMode == AppViewMode.NowPlaying;
    public bool IsLibraryMode => _viewMode == AppViewMode.Library;

    /// <summary>Which Now Playing scene is selected (right-click menu).</summary>
    public VisualizationMode Visualization
    {
        get => _visualization;
        set
        {
            if (SetProperty(ref _visualization, value))
            {
                Settings.Visualization = value.ToString();
                RaiseVisualizationChanged();
            }
        }
    }

    public bool IsAlbumArtMode => _visualization == VisualizationMode.AlbumArt;
    public bool IsBarsMode => _visualization == VisualizationMode.Bars;
    public bool IsScopeMode => _visualization == VisualizationMode.Scope;
    public bool IsOceanMistMode => _visualization == VisualizationMode.OceanMist;
    public bool IsAlchemyMode => _visualization == VisualizationMode.Alchemy;
    public bool IsBatteryMode => _visualization == VisualizationMode.Battery;
    public bool IsPurpleHazeMode => _visualization == VisualizationMode.PurpleHaze;

    /// <summary>True for every scene except Album art — the visualizers run on a black stage.</summary>
    public bool IsVisualizationScene => _visualization != VisualizationMode.AlbumArt;

    /// <summary>Visualizers animate only when their mode is selected, visible and playing.</summary>
    public bool IsBarsActive => IsNowPlaying && IsPlaying && IsBarsMode;
    public bool IsScopeActive => IsNowPlaying && IsPlaying && IsScopeMode;
    public bool IsOceanMistActive => IsNowPlaying && IsPlaying && IsOceanMistMode;
    public bool IsAlchemyActive => IsNowPlaying && IsPlaying && IsAlchemyMode;
    public bool IsBatteryActive => IsNowPlaying && IsPlaying && IsBatteryMode;
    public bool IsPurpleHazeActive => IsNowPlaying && IsPlaying && IsPurpleHazeMode;

    private void RaiseVisualizationChanged()
    {
        OnPropertyChanged(nameof(IsAlbumArtMode));
        OnPropertyChanged(nameof(IsBarsMode));
        OnPropertyChanged(nameof(IsScopeMode));
        OnPropertyChanged(nameof(IsOceanMistMode));
        OnPropertyChanged(nameof(IsAlchemyMode));
        OnPropertyChanged(nameof(IsBatteryMode));
        OnPropertyChanged(nameof(IsPurpleHazeMode));
        OnPropertyChanged(nameof(IsVisualizationScene));
        OnPropertyChanged(nameof(IsBarsActive));
        OnPropertyChanged(nameof(IsScopeActive));
        OnPropertyChanged(nameof(IsOceanMistActive));
        OnPropertyChanged(nameof(IsAlchemyActive));
        OnPropertyChanged(nameof(IsBatteryActive));
        OnPropertyChanged(nameof(IsPurpleHazeActive));
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(ShowEmptyHint));
        }
    }

    #endregion

    #region Commands

    public RelayCommand PlayPauseCommand { get; private set; } = null!;
    public RelayCommand NextCommand { get; private set; } = null!;
    public RelayCommand PreviousCommand { get; private set; } = null!;
    public RelayCommand StopCommand { get; private set; } = null!;
    public RelayCommand PlayTrackCommand { get; private set; } = null!;
    public RelayCommand ToggleMuteCommand { get; private set; } = null!;
    public RelayCommand ToggleShuffleCommand { get; private set; } = null!;
    public RelayCommand ToggleRepeatCommand { get; private set; } = null!;
    public RelayCommand ToggleNowPlayingCommand { get; private set; } = null!;
    public RelayCommand AddFolderCommand { get; private set; } = null!;
    public RelayCommand AddFilesCommand { get; private set; } = null!;
    public RelayCommand RefreshDrivesCommand { get; private set; } = null!;
    public RelayCommand CreatePlaylistCommand { get; private set; } = null!;
    public RelayCommand OpenStreamCommand { get; private set; } = null!;
    public RelayCommand AddToPlaylistCommand { get; private set; } = null!;

    /// <summary>Raised when the view should prompt for a stream URL.</summary>
    public event Func<string?>? RequestStreamUrl;
    public RelayCommand DeletePlaylistCommand { get; private set; } = null!;
    public RelayCommand RemoveFromPlaylistCommand { get; private set; } = null!;
    public RelayCommand BackCommand { get; private set; } = null!;
    public RelayCommand ForwardCommand { get; private set; } = null!;
    public RelayCommand CheckUpdatesCommand { get; private set; } = null!;
    public RelayCommand SetVisualizationCommand { get; private set; } = null!;

    /// <summary>Raised when an update is found; the view decides how to surface it.</summary>
    public event Action<UpdateResult>? UpdateAvailable;

    /// <summary>True when the content pane is showing a playlist (enables remove/delete menu items).</summary>
    public bool IsViewingPlaylist => SelectedNav?.View == LibraryView.PlaylistItem;

    /// <summary>Raised when the view should prompt the user for a new playlist name.</summary>
    public event Func<string?>? RequestPlaylistName;

    private void InitCommands()
    {
        PlayPauseCommand = new RelayCommand(() =>
        {
            if (CurrentTrack is null)
            {
                var first = SelectedTrack ?? CurrentTracks.FirstOrDefault();
                if (first is not null) PlayTrack(first);
            }
            else
            {
                _playback.TogglePlayPause();
            }
        });

        NextCommand = new RelayCommand(PlayNext);
        PreviousCommand = new RelayCommand(PlayPrevious);
        StopCommand = new RelayCommand(() => _playback.Stop());
        PlayTrackCommand = new RelayCommand(p => { if (p is TrackViewModel t) PlayTrack(t); });
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        ToggleShuffleCommand = new RelayCommand(() => IsShuffle = !IsShuffle);
        ToggleRepeatCommand = new RelayCommand(() => IsRepeat = !IsRepeat);
        ToggleNowPlayingCommand = new RelayCommand(() =>
            ViewMode = ViewMode == AppViewMode.NowPlaying ? AppViewMode.Library : AppViewMode.NowPlaying);
        AddFolderCommand = new RelayCommand(async () => await AddFolderAsync());
        AddFilesCommand = new RelayCommand(async () => await AddFilesAsync());
        RefreshDrivesCommand = new RelayCommand(RefreshDrives);
        CreatePlaylistCommand = new RelayCommand(_ => CreatePlaylist());
        OpenStreamCommand = new RelayCommand(_ =>
        {
            var url = RequestStreamUrl?.Invoke();
            if (!string.IsNullOrWhiteSpace(url)) OpenStream(url);
        });
        AddToPlaylistCommand = new RelayCommand(p => AddSelectedToPlaylist(p as Playlist));
        DeletePlaylistCommand = new RelayCommand(p => DeletePlaylist(p as Playlist));
        RemoveFromPlaylistCommand = new RelayCommand(_ => RemoveSelectedFromPlaylist());
        BackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ForwardCommand = new RelayCommand(GoForward, () => CanGoForward);
        CheckUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(manual: true));
        SetVisualizationCommand = new RelayCommand(p =>
        {
            if (p is string name && Enum.TryParse<VisualizationMode>(name, out var mode))
                Visualization = mode;
        });
    }

    /// <summary>The live settings object — the Settings dialog binds straight to it.</summary>
    public AppSettings Settings => _settings.Current;

    /// <summary>Called by the window on close so bounds survive the session.</summary>
    public void UpdateWindowMetrics(double width, double height, bool maximized)
    {
        Settings.WindowWidth = width;
        Settings.WindowHeight = height;
        Settings.IsMaximized = maximized;
    }

    /// <summary>Syncs playback state into the settings object and writes it to disk.</summary>
    public void PersistSettings()
    {
        Settings.Volume = _volume;
        Settings.IsMuted = _isMuted;
        Settings.IsShuffle = _isShuffle;
        Settings.IsRepeat = _isRepeat;
        _settings.Save();
    }

    /// <summary>Releases the audio device; call when the app closes.</summary>
    public void ShutdownPlayback() => _playback.Dispose();

    /// <summary>
    /// Queries GitHub for a newer release. Manual checks always report a result;
    /// the silent startup check only speaks up when an update actually exists.
    /// </summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual) StatusText = "Checking for updates…";

        var result = await _updates.CheckAsync();
        switch (result.Status)
        {
            case UpdateStatus.UpdateAvailable:
                StatusText = $"Version {result.LatestVersion} is available";
                UpdateAvailable?.Invoke(result);
                break;
            case UpdateStatus.UpToDate when manual:
                StatusText = $"You're up to date (v{UpdateService.CurrentVersion.ToString(3)})";
                break;
            case UpdateStatus.CheckFailed when manual:
                StatusText = "Couldn't check for updates";
                break;
        }
    }

    /// <summary>
    /// The auto-updater for installed copies: downloads the new Setup exe with
    /// live progress in the status area, launches it silently and closes the
    /// app so the installer can swap the files and relaunch us.
    /// </summary>
    public async Task InstallUpdateAsync(UpdateResult update)
    {
        try
        {
            StatusText = "Downloading update…";
            var progress = new Progress<double>(p =>
                StatusText = $"Downloading update… {p * 100:0}%");

            string setupPath = await _updates.DownloadInstallerAsync(update, progress);

            StatusText = "Installing update…";
            PersistSettings();
            UpdateService.LaunchInstaller(setupPath);
            System.Windows.Application.Current.Shutdown();
        }
        catch
        {
            StatusText = "Update failed — try again from Organize › Check for updates";
        }
    }

    private void RemoveSelectedFromPlaylist()
    {
        if (SelectedNav?.Playlist is not { } playlist || SelectedTrack is null) return;

        playlist.TrackPaths.RemoveAll(p =>
            string.Equals(p, SelectedTrack!.Track.FilePath, StringComparison.OrdinalIgnoreCase));
        _playlistService.Save(playlist);
        _ = LoadPlaylistView(playlist);
    }

    private void CreatePlaylist(TrackViewModel? seedTrack = null)
    {
        string? name = RequestPlaylistName?.Invoke();
        if (string.IsNullOrWhiteSpace(name)) return;

        var playlist = _playlistService.Create(name);
        if (seedTrack is not null)
            _playlistService.AddTrack(playlist, seedTrack.Track.FilePath);

        RefreshPlaylists(playlist);
        StatusText = $"Created playlist “{playlist.Name}”";
    }

    private void AddSelectedToPlaylist(Playlist? playlist)
    {
        var track = SelectedTrack;
        if (track is null) return;

        // "New playlist…" is represented by a null playlist parameter.
        if (playlist is null)
        {
            CreatePlaylist(track);
            return;
        }

        _playlistService.AddTrack(playlist, track.Track.FilePath);
        StatusText = $"Added to “{playlist.Name}”";

        // If we're looking at that playlist right now, refresh it.
        if (SelectedNav?.View == LibraryView.PlaylistItem && SelectedNav.Playlist == playlist)
            _ = LoadPlaylistView(playlist);
    }

    private void DeletePlaylist(Playlist? playlist)
    {
        playlist ??= SelectedNav?.Playlist;
        if (playlist is null) return;

        _playlistService.Delete(playlist);
        BuildNavigation();
        SelectedNav = NavigationItems.First(n => n.View == LibraryView.Music && !n.IsHeader);
        StatusText = $"Deleted playlist “{playlist.Name}”";
    }

    private void PlayTrack(TrackViewModel track)
    {
        if (track.Track.Kind == MediaKind.Picture) return;

        _playQueue = CurrentTracks.Where(t => t.Track.Kind != MediaKind.Picture).ToList();
        CurrentTrack = track;
        _playback.Open(track.Track);
        _playback.Play();
        OnPropertyChanged(nameof(IsPlaying));
    }

    /// <summary>Plays a track that isn't necessarily in the current list (its own one-item queue).</summary>
    private void PlaySingle(TrackViewModel track)
    {
        _playQueue = new List<TrackViewModel> { track };
        CurrentTrack = track;
        _playback.Open(track.Track);
        _playback.Play();
        OnPropertyChanged(nameof(IsPlaying));
    }

    /// <summary>
    /// Opens files handed to us on the command line or by a file association:
    /// adds them to the library and starts playing the first playable one.
    /// </summary>
    public void OpenAndPlay(string[] paths)
    {
        TrackViewModel? toPlay = null;
        int added = 0;

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

            var kind = KindFor(path);
            var existing = _allTracks.FirstOrDefault(t =>
                string.Equals(t.Track.FilePath, path, StringComparison.OrdinalIgnoreCase));

            TrackViewModel vm;
            if (existing is not null)
            {
                vm = existing;
            }
            else
            {
                var track = LibraryService.ReadTrack(path, kind);
                if (track is null) continue;
                vm = new TrackViewModel(track);
                _allTracks.Add(vm);
                added++;
            }

            toPlay ??= kind == MediaKind.Picture ? null : vm;
        }

        if (added > 0)
        {
            _library.Save(_allTracks.Select(t => t.Track));
            RefreshCurrentView();
            OnPropertyChanged(nameof(ShowEmptyHint));
        }

        if (toPlay is not null)
            PlaySingle(toPlay);
    }

    /// <summary>Opens an internet radio / stream URL and starts playing it.</summary>
    public void OpenStream(string url, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        url = url.Trim();
        if (!url.Contains("://")) url = "http://" + url;

        var track = new Track
        {
            FilePath = url,
            Title = string.IsNullOrWhiteSpace(name) ? url : name!.Trim(),
            Artist = "Internet stream",
            Album = "Streaming",
            Kind = MediaKind.Music,
            IsStream = true
        };

        PlaySingle(new TrackViewModel(track));
        StatusText = $"Streaming {track.Title}";
    }

    private void PlayNext()
    {
        var next = GetAdjacent(1);
        if (next is not null) PlayTrack(next);
        else _playback.Stop();
    }

    private void PlayPrevious()
    {
        // Restart current track if we're more than 3s in, WMP-style.
        if (_playback.Position.TotalSeconds > 3)
        {
            _playback.EndSeek(TimeSpan.Zero);
            return;
        }
        var prev = GetAdjacent(-1);
        if (prev is not null) PlayTrack(prev);
    }

    private TrackViewModel? GetAdjacent(int direction)
    {
        if (_playQueue.Count == 0 || CurrentTrack is null) return null;

        if (_isShuffle && _playQueue.Count > 1)
        {
            TrackViewModel candidate;
            do { candidate = _playQueue[_random.Next(_playQueue.Count)]; }
            while (candidate == CurrentTrack);
            return candidate;
        }

        int index = _playQueue.IndexOf(CurrentTrack);
        if (index < 0) return _playQueue.FirstOrDefault();

        int nextIndex = index + direction;
        if (nextIndex < 0)
            return _isRepeat ? _playQueue[^1] : null;
        if (nextIndex >= _playQueue.Count)
            return _isRepeat ? _playQueue[0] : null;

        return _playQueue[nextIndex];
    }

    #endregion

    #region Library import

    private async Task AddFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Add a folder to the library" };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusText = "Adding files…";
        int added = 0;

        await _library.ScanFolderAsync(dialog.FolderName, track =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (AddTrack(track)) added++;
            });
        });

        FinishImport(added);
    }

    private async Task AddFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add files to the library",
            Multiselect = true,
            Filter = "Media files|*.mp3;*.m4a;*.aac;*.flac;*.wav;*.wma;*.ogg;*.opus;" +
                     "*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.m4v;*.webm;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusText = "Adding files…";
        int added = 0;

        await Task.Run(() =>
        {
            foreach (var file in dialog.FileNames)
            {
                var kind = KindFor(file);
                var track = LibraryService.ReadTrack(file, kind);
                if (track is null) continue;
                App.Current.Dispatcher.Invoke(() => { if (AddTrack(track)) added++; });
            }
        });

        FinishImport(added);
    }

    private static MediaKind KindFor(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is ".mp4" or ".mkv" or ".avi" or ".wmv" or ".mov" or ".m4v" or ".webm")
            return MediaKind.Video;
        if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif")
            return MediaKind.Picture;
        return MediaKind.Music;
    }

    private bool AddTrack(Track track)
    {
        if (_allTracks.Any(t => string.Equals(t.Track.FilePath, track.FilePath,
                StringComparison.OrdinalIgnoreCase)))
            return false;

        _allTracks.Add(new TrackViewModel(track));
        return true;
    }

    private void FinishImport(int added)
    {
        _library.Save(_allTracks.Select(t => t.Track));
        IsBusy = false;
        RefreshCurrentView();
        StatusText = added > 0 ? "Update complete" : "No new files found";
        OnPropertyChanged(nameof(ShowEmptyHint));
    }

    #endregion

    private void WirePlayback()
    {
        _playback.Volume = _volume;
        _playback.IsMuted = _isMuted;

        _playback.PositionChanged += (_, pos) =>
        {
            _suppressSeek = true;
            PositionSeconds = pos.TotalSeconds;
            _suppressSeek = false;
        };

        _playback.MediaOpened += (_, _) =>
        {
            DurationSeconds = _playback.Duration.TotalSeconds;
        };

        _playback.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsPlaying));
            RaiseVisualizationChanged();
        };

        _playback.MediaFailed += (_, _) =>
        {
            StatusText = "Can't play this file";
            OnPropertyChanged(nameof(IsPlaying));
            RaiseVisualizationChanged();
        };

        _playback.MediaEnded += (_, _) =>
        {
            if (_isRepeat && !_isShuffle && _playQueue.Count == 1 && CurrentTrack is not null)
            {
                _playback.EndSeek(TimeSpan.Zero);
                _playback.Play();
            }
            else
            {
                PlayNext();
            }
        };
    }
}
