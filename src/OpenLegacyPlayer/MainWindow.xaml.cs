using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenLegacyPlayer.Interop;
using OpenLegacyPlayer.ViewModels;

namespace OpenLegacyPlayer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        StateChanged += (_, _) => UpdateMaxButtonGlyph();

        // The view model asks us to prompt for a playlist name when needed.
        _vm.RequestPlaylistName += () =>
            Views.InputDialog.Prompt(this, "Create playlist", "Name your playlist:", "New Playlist");

        // …and for a stream / internet radio URL.
        _vm.RequestStreamUrl += () =>
            Views.InputDialog.Prompt(this, "Open stream",
                "Enter an internet radio or media stream URL:", "https://");

        // A newer release exists. Show the changelog and let the user choose —
        // never forced. Installed copies can update in place; portable copies
        // get pointed at the download page.
        _vm.UpdateAvailable += update =>
        {
            bool canAutoUpdate = Services.UpdateService.IsInstalledCopy && update.InstallerUrl is not null;
            if (!Views.UpdateWindow.Ask(this, update, canAutoUpdate))
                return; // "Ask later" — we'll mention it again next launch

            if (canAutoUpdate)
                _ = _vm.InstallUpdateAsync(update);
            else
                OpenUrl(update.ReleaseUrl ?? Services.UpdateService.ReleasesPageUrl);
        };

        // Restore last window size/state.
        var s = _vm.Settings;
        if (s.WindowWidth >= MinWidth && s.WindowHeight >= MinHeight)
        {
            Width = s.WindowWidth;
            Height = s.WindowHeight;
        }
        if (s.IsMaximized)
            WindowState = WindowState.Maximized;

        // Silent update check shortly after startup (portable + installer builds).
        Loaded += async (_, _) =>
        {
            if (!_vm.Settings.CheckUpdatesOnStartup) return;
            await Task.Delay(TimeSpan.FromSeconds(3));
            await _vm.CheckForUpdatesAsync(manual: false);
        };

        // Persist settings + window bounds when the app closes.
        Closing += (_, _) =>
        {
            bool max = WindowState == WindowState.Maximized;
            var bounds = max ? RestoreBounds : new Rect(Left, Top, Width, Height);
            _vm.UpdateWindowMetrics(bounds.Width, bounds.Height, max);
            _vm.PersistSettings();
            _vm.ShutdownPlayback();
        };

        // Dev-only: render the window to a PNG then exit (used for visual checks).
        var shot = Environment.GetEnvironmentVariable("OLP_SCREENSHOT");
        if (!string.IsNullOrEmpty(shot))
        {
            int.TryParse(Environment.GetEnvironmentVariable("OLP_SHOT_DELAY"), out var delayMs);
            Loaded += (_, _) =>
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs > 0 ? delayMs : 200) };
                t.Tick += (_, _) => { t.Stop(); SaveScreenshotAndExit(shot); };
                t.Start();
            };
        }
    }

    private void SaveScreenshotAndExit(string path)
    {
        try
        {
            var rtb = new RenderTargetBitmap(
                (int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }
        catch { }
        Application.Current.Shutdown();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == MaximizeHelper.WM_GETMINMAXINFO)
        {
            MaximizeHelper.HandleGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Opens (and starts playing) files passed on the command line or by file association.</summary>
    public void OpenFiles(string[] paths) => _vm.OpenAndPlay(paths);

    /// <summary>Restores and focuses the window when a second launch forwards files to us.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        new Views.SettingsWindow(_vm) { Owner = this }.ShowDialog();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* no browser available */ }
    }

    private void UpdateMaxButtonGlyph()
    {
        // Restore glyph (E923) when maximized, Maximize glyph (E922) otherwise.
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
        // Trim the 1px chrome border so a maximized window sits flush.
        RootBorder.BorderThickness = WindowState == WindowState.Maximized
            ? new Thickness(0)
            : new Thickness(1);
    }

    private void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryList.SelectedItem is TrackViewModel track)
            _vm.PlayTrackCommand.Execute(track);
    }

    // Right-clicking a row should select it first, so the context menu acts on it.
    private void LibraryList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null and not System.Windows.Controls.ListViewItem)
            dep = VisualTreeHelper.GetParent(dep);

        if (dep is System.Windows.Controls.ListViewItem item)
            item.IsSelected = true;
    }
}
