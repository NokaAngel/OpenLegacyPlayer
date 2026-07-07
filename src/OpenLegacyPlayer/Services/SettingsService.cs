using System.IO;
using System.Text.Json;
using OpenLegacyPlayer.Models;

namespace OpenLegacyPlayer.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON. Reads once at startup,
/// writes on demand (typically when the app closes). All I/O is best-effort —
/// a broken settings file just means fresh defaults.
/// </summary>
public class SettingsService
{
    private readonly string _path;

    public SettingsService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLegacyPlayer");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path))
                       ?? new AppSettings();
        }
        catch { /* corrupt settings — fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path,
                JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
    }
}
