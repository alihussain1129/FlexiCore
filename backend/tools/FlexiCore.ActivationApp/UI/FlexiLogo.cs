using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FlexiCore.ActivationApp;

internal static class FlexiLogo
{
    private static Bitmap? _bitmap;
    private static bool _tried;

    public static Control Mark(double size)
    {
        if (!_tried)
        {
            _tried = true;
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://FlexiCore.ActivationApp/Assets/logo-512.png"));
                _bitmap = new Bitmap(stream);
            }
            catch
            {
                _bitmap = null;
            }
        }
        if (_bitmap is not null)
        {
            var image = new Image
            {
                Source = _bitmap,
                Width = size,
                Height = size,
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.22),
                ClipToBounds = true,
                Child = image
            };
        }
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.3),
            Background = ActivationDesignSystem.PrimaryGradient,
            Child = new TextBlock
            {
                Text = "F",
                FontSize = size * 0.5,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
