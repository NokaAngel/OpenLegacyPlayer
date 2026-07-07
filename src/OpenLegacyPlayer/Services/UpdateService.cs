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

public record UpdateResult(UpdateStatus Status, string? LatestVersion = null, string? ReleaseUrl = null);

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

            if (string.IsNullOrWhiteSpace(tag))
                return new UpdateResult(UpdateStatus.CheckFailed);

            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                return new UpdateResult(UpdateStatus.CheckFailed);

            // Compare on major.minor.build only — revision is noise here.
            var current = CurrentVersion;
            bool newer = new Version(latest.Major, Math.Max(latest.Minor, 0), Math.Max(latest.Build, 0))
                         > new Version(current.Major, Math.Max(current.Minor, 0), Math.Max(current.Build, 0));

            return newer
                ? new UpdateResult(UpdateStatus.UpdateAvailable, latest.ToString(), htmlUrl ?? ReleasesPageUrl)
                : new UpdateResult(UpdateStatus.UpToDate, latest.ToString());
        }
        catch
        {
            // Offline, rate-limited, repo not published yet — all fine.
            return new UpdateResult(UpdateStatus.CheckFailed);
        }
    }
}
