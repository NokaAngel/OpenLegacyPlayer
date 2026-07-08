using System.IO;
using System.Windows;
using System.Windows.Input;
using OpenLegacyPlayer.Services;
using OpenLegacyPlayer.ViewModels;

namespace OpenLegacyPlayer.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        AutoUpdateCheck.IsChecked = vm.Settings.CheckUpdatesOnStartup;
        VersionText.Text =
            $"OpenLegacy Player  v{UpdateService.CurrentVersion.ToString(3)}  •  github.com/{UpdateService.RepoOwner}/{UpdateService.RepoName}";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.CheckUpdatesOnStartup = AutoUpdateCheck.IsChecked == true;
        _vm.PersistSettings();
        Close();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLegacyPlayer");

    private void SetDefault_Click(object sender, RoutedEventArgs e)
    {
        // Deep-link to our entry in Windows "Default apps" when possible; the
        // registeredApp* keys are only honoured on an installed (registered) copy.
        string appName = "OpenLegacy Player";
        string[] uris =
        {
            $"ms-settings:defaultapps?registeredAppMachine={appName}",
            $"ms-settings:defaultapps?registeredAppUser={appName}",
            "ms-settings:defaultapps"
        };

        foreach (var uri in uris)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
                return;
            }
            catch { /* try the next, more generic URI */ }
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(DataDir);

    private void OpenPlaylistsFolder_Click(object sender, RoutedEventArgs e)
        => OpenFolder(Path.Combine(DataDir, "Playlists"));

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
                { UseShellExecute = true });
        }
        catch { }
    }
}
