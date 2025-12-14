using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace KID
{
    public static partial class Graphics
    {
        public static Image? Image(double x, double y, string path, double? width = null, double? height = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;

                try
                {
                    if (!File.Exists(path))
                        return null;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var image = 
                        width == null || height == null ?
                        new System.Windows.Controls.Image
                        {
                            Source = bitmap
                        } :
                        new System.Windows.Controls.Image
                        {
                            Source = bitmap,
                            Width = width.Value,
                            Height = height.Value
                        };

                    Canvas.SetLeft(image, x);
                    Canvas.SetTop(image, y);
                    Canvas.Children.Add(image);

                    return image;
                }
                catch
                {
                    return null;
                }
            });
        }
        public static Image? Image(Point position, string path, double? width = null, double? height = null)
        {
            return Image(position.X, position.Y, path, width, height);
        }
        public static Image? Image(Point position, string path, Point size)
        {
            return Image(position, path, size.X, size.Y);
        }

        public static Image? SetSource(this Image image, string path, double? width = null, double? height = null)
        {
            if (image == null || string.IsNullOrWhiteSpace(path))
                return null;

            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;

                try
                {
                    if (!File.Exists(path))
                        return null;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    image.Source = bitmap;
                    if (width != null) image.Width = width.Value;
                    if (height != null) image.Height = height.Value;
                    return image;
                }
                catch
                {
                    return null;
                }
            });
        }
        public static Image? SetSource(this Image image, string path, Point size)
        {
            return SetSource(image, path, size.X, size.Y);
        }
    }
}

