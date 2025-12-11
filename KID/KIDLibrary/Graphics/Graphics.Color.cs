using System;
using System.Windows.Media;

namespace KID
{
    public static partial class Graphics
    {
        // Вспомогательная структура для работы с цветами в разных форматах
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

            // Создаёт Brush в UI потоке
            internal Brush CreateBrush()
            {
                return _brushFactory?.Invoke() ?? Brushes.Black;
            }
        }

        public static ColorType FillColor
        {
            get => new ColorType(() => fillBrush);
            set
            {
                InvokeOnUI(() =>
                {
                    fillBrush = value.CreateBrush();
                });
            }
        }

        public static ColorType StrokeColor
        {
            get => new ColorType(() => strokeBrush);
            set
            {
                InvokeOnUI(() =>
                {
                    strokeBrush = value.CreateBrush();
                });
            }
        }

        public static ColorType Color
        {
            get => new ColorType(() => fillBrush);
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
