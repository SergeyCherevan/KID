using System;
using System.Windows.Media;

namespace KID
{
    /// <summary>
    /// Вспомогательная структура для работы с цветами в разных форматах.
    /// </summary>
    public struct ColorType
    {
        private readonly Func<Brush> _brushFactory;

        internal ColorType(Func<Brush> brushFactory)
        {
            _brushFactory = brushFactory ?? (() => Brushes.Black);
        }

        public static implicit operator ColorType(string colorName)
        {
            return new ColorType(() =>
            {
                try
                {
                    var converter = new BrushConverter();
                    return converter.ConvertFromString(colorName) as Brush ?? Brushes.Black;
                }
                catch
                {
                    return Brushes.Black;
                }
            });
        }

        public static implicit operator ColorType((byte r, byte g, byte b) rgb)
        {
            return new ColorType(() => new SolidColorBrush(System.Windows.Media.Color.FromRgb(rgb.r, rgb.g, rgb.b)));
        }

        public static implicit operator ColorType(int rgb)
        {
            return new ColorType(() =>
            {
                byte red = (byte)((rgb >> 16) & 0xFF);
                byte green = (byte)((rgb >> 8) & 0xFF);
                byte blue = (byte)(rgb & 0xFF);
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
            });
        }

        public static implicit operator ColorType(Brush brush)
        {
            return new ColorType(() => brush ?? Brushes.Black);
        }

        internal Brush CreateBrush()
        {
            return _brushFactory?.Invoke() ?? Brushes.Black;
        }
    }
}

