using System.IO;
using System.Windows.Media.Imaging;

namespace OpenLegacyPlayer.Services;

public static class ImageHelper
{
    /// <summary>
    /// Turns raw image bytes into a frozen <see cref="BitmapImage"/> that can be
    /// shared across threads and bound directly in XAML. Returns null on failure.
    /// </summary>
    public static BitmapImage? FromBytes(byte[]? data, int decodePixelWidth = 0)
    {
        if (data is null || data.Length == 0)
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(data);
            if (decodePixelWidth > 0)
                image.DecodePixelWidth = decodePixelWidth;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapImage? FromFile(string path, int decodePixelWidth = 0)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            if (decodePixelWidth > 0)
                image.DecodePixelWidth = decodePixelWidth;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
