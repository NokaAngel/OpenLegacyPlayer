using System.IO;

namespace OpenLegacyPlayer.Services;

public record DriveEntry(string Label, string RootPath, bool IsRemovable);

/// <summary>Enumerates ready fixed/removable drives for the navigation pane.</summary>
public static class DriveService
{
    public static List<DriveEntry> GetDrives()
    {
        var list = new List<DriveEntry>();
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { return list; }

        foreach (var d in drives)
        {
            try
            {
                if (!d.IsReady) continue;
                if (d.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network))
                    continue;

                string letter = d.Name.TrimEnd('\\', '/');
                string label = string.IsNullOrWhiteSpace(d.VolumeLabel)
                    ? (d.DriveType == DriveType.Removable ? "Removable Disk" : "Local Disk")
                    : d.VolumeLabel;

                list.Add(new DriveEntry($"{label} ({letter})", d.RootDirectory.FullName,
                    d.DriveType == DriveType.Removable));
            }
            catch
            {
                // Ignore drives that throw while being queried.
            }
        }

        return list;
    }
}
