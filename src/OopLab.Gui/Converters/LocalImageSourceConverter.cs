using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace OopLab.Gui.Converters;

public sealed class LocalImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string source || string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 300;

            if (File.Exists(source))
            {
                bitmap.UriSource = new Uri(Path.GetFullPath(source), UriKind.Absolute);
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var remoteUri))
            {
                bitmap.UriSource = remoteUri;
            }
            else
            {
                return null;
            }

            bitmap.EndInit();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
