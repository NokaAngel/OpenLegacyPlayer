using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace OpenLegacyPlayer.Services;

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    CheckFailed
}

public record UpdateResult(
    UpdateStatus Status,
    string? LatestVersion = null,
    string? ReleaseUrl = null,
    string? InstallerUrl = null,
    string? InstallerName = null,
    long InstallerSize = 0,
    string? ReleaseName = null,
    string? Notes = null);

/// <summary>
/// Checks the GitHub releases feed for a newer version. Works for both the
/// portable build and the installer — it only reads the public API and never
/// downloads anything by itself.
/// </summary>
public class UpdateService
{
    // ── Point these at the repository you publish releases from. ─────────────
    public const string RepoOwner = "NokaAngel";
    public const string RepoName = "OpenLegacyPlayer";
    // ─────────────────────────────────────────────────────────────────────────

    public static string ReleasesPageUrl => $"https://github.com/{RepoOwner}/{RepoName}/releases";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // The GitHub API rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("OpenLegacyPlayer", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>The version baked into the assembly at build time.</summary>
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Asks GitHub for the latest release and compares its tag ("v1.2.3" or
    /// "1.2.3") against the running version. Never throws.
    /// </summary>
    public async Task<UpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await Http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new UpdateResult(UpdateStatus.CheckFailed);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            string? tag = json.RootElement.TryGetProperty("tag_name", out var tagEl)
                ? tagEl.GetString()
                : null;
            string? htmlUrl = json.RootElement.TryGetProperty("html_url", out var urlEl)
                ? urlEl.GetString()
                : null;
            string? releaseName = json.RootElement.TryGetProperty("name", out var relNameEl)
                ? relNameEl.GetString()
                : null;
            string? notes = json.RootElement.TryGetProperty("body", out var bodyEl)
                ? bodyEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tag))
                return new UpdateResult(UpdateStatus.CheckFailed);

            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                return new UpdateResult(UpdateStatus.CheckFailed);

            // Find the Setup exe among the release assets (for the auto-updater).
            string? installerUrl = null, installerName = null;
            long installerSize = 0;
            if (json.RootElement.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (name is null ||
                        !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        !name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                        continue;

                    installerName = name;
                    installerUrl = asset.TryGetProperty("browser_download_url", out var dlEl)
                        ? dlEl.GetString()
                        : null;
                    installerSize = asset.TryGetProperty("size", out var sizeEl)
                        ? sizeEl.GetInt64()
                        : 0;
                    break;
                }
            }

            // Compare on major.minor.build only — revision is noise here.
            var current = CurrentVersion;
            bool newer = new Version(latest.Major, Math.Max(latest.Minor, 0), Math.Max(latest.Build, 0))
                         > new Version(current.Major, Math.Max(current.Minor, 0), Math.Max(current.Build, 0));

            return newer
                ? new UpdateResult(UpdateStatus.UpdateAvailable, latest.ToString(),
                    htmlUrl ?? ReleasesPageUrl, installerUrl, installerName, installerSize,
                    releaseName, notes)
                : new UpdateResult(UpdateStatus.UpToDate, latest.ToString());
        }
        catch
        {
            // Offline, rate-limited, repo not published yet — all fine.
            return new UpdateResult(UpdateStatus.CheckFailed);
        }
    }

    /// <summary>
    /// True when this copy was installed with the Setup exe (Inno drops its
    /// uninstaller beside the app). Portable unzips have no uninstaller, so
    /// they get the open-the-releases-page flow instead of the auto-updater.
    /// </summary>
    public static bool IsInstalledCopy =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>
    /// Downloads the release's Setup exe into %TEMP%, reporting 0..1 progress.
    /// Throws on failure — callers surface that as "update failed".
    /// </summary>
    public async Task<string> DownloadInstallerAsync(
        UpdateResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (update.InstallerUrl is null)
            throw new InvalidOperationException("No installer asset on this release.");

        string fileName = update.InstallerName ?? $"OpenLegacyPlayer-Setup-{update.LatestVersion}.exe";
        string path = Path.Combine(Path.GetTempPath(), fileName);

        using var response = await Http.GetAsync(
            update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? update.InstallerSize;

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                if (total > 0)
                    progress?.Report((double)done / total);
            }
        }

        // A truncated download must never reach the installer step.
        if (update.InstallerSize > 0 && new FileInfo(path).Length != update.InstallerSize)
        {
            try { File.Delete(path); } catch { }
            throw new IOException("Downloaded installer is incomplete.");
        }

        return path;
    }

    /// <summary>
    /// Runs the downloaded Setup exe silently. Inno replaces the app in place
    /// and relaunches it when done — the caller should shut the app down
    /// right after this returns so no files are locked.
    /// </summary>
    public static void LaunchInstaller(string setupPath)
    {
        // Per-user installs (under %LocalAppData%) shouldn't trigger a UAC
        // prompt; machine-wide installs will show one, which is expected.
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bool perUser = AppContext.BaseDirectory.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase);

        string args = "/SILENT /NORESTART /SP-" + (perUser ? " /CURRENTUSER" : "");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setupPath, args)
        {
            UseShellExecute = true
        });
    }
}
