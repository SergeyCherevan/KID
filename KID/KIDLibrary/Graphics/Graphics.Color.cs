using System;
using System.Windows.Media;

namespace KID
{
    // Вспомогательная структура для работы с цветами в разных форматах
    public struct ColorValue
    {
        private readonly Func<Brush> _brushFactory;

        internal ColorValue(Func<Brush> brushFactory)
        {
            _brushFactory = brushFactory ?? (() => Brushes.Black);
        }

        public static implicit operator ColorValue(string colorName)
        {
            return new ColorValue(() =>
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

        public static implicit operator ColorValue((byte r, byte g, byte b) rgb)
        {
            return new ColorValue(() => new SolidColorBrush(Color.FromRgb(rgb.r, rgb.g, rgb.b)));
        }

        public static implicit operator ColorValue(int rgb)
        {
            return new ColorValue(() =>
            {
                byte red = (byte)((rgb >> 16) & 0xFF);
                byte green = (byte)((rgb >> 8) & 0xFF);
                byte blue = (byte)(rgb & 0xFF);
                return new SolidColorBrush(Color.FromRgb(red, green, blue));
            });
        }

        public static implicit operator ColorValue(Brush brush)
        {
            return new ColorValue(() => brush ?? Brushes.Black);
        }

        // Создаёт Brush в UI потоке
        internal Brush CreateBrush()
        {
            return _brushFactory?.Invoke() ?? Brushes.Black;
        }
    }

    public static partial class Graphics
    {
        public static ColorValue FillColor
        {
            get => new ColorValue(() => fillBrush);
            set
            {
                InvokeOnUI(() =>
                {
                    fillBrush = value.CreateBrush();
                });
            }
        }

        public static ColorValue StrokeColor
        {
            get => new ColorValue(() => strokeBrush);
            set
            {
                InvokeOnUI(() =>
                {
                    strokeBrush = value.CreateBrush();
                });
            }
        }

        public static ColorValue Color
        {
            get => new ColorValue(() => fillBrush);
            set
            {
                InvokeOnUI(() =>
                {
                    var brush = value.CreateBrush();
                    fillBrush = brush;
                    strokeBrush = brush;
                });
            }
        }
    }
}
