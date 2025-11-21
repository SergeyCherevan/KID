using System.Windows.Media;

namespace KID
{
    // Вспомогательная структура для работы с цветами в разных форматах
    public struct ColorValue
    {
        private Brush brush;

        private ColorValue(Brush brush)
        {
            this.brush = brush ?? Brushes.Black;
        }

        public static implicit operator ColorValue(string colorName)
        {
            try
            {
                var converter = new BrushConverter();
                return new ColorValue((Brush)converter.ConvertFromString(colorName));
            }
            catch
            {
                return new ColorValue(Brushes.Black);
            }
        }

        public static implicit operator ColorValue((byte r, byte g, byte b) rgb)
        {
            return new ColorValue(new SolidColorBrush(Color.FromRgb(rgb.r, rgb.g, rgb.b)));
        }

        public static implicit operator ColorValue(int rgb)
        {
            byte red = (byte)((rgb >> 16) & 0xFF);
            byte green = (byte)((rgb >> 8) & 0xFF);
            byte blue = (byte)(rgb & 0xFF);
            return new ColorValue(new SolidColorBrush(Color.FromRgb(red, green, blue)));
        }

        public static implicit operator ColorValue(Brush brush)
        {
            return new ColorValue(brush);
        }

        public static implicit operator Brush(ColorValue colorValue)
        {
            return colorValue.brush;
        }
    }

    public static partial class Graphics
    {
        public static ColorValue FillColor
        {
            get => fillBrush;
            set
            {
                InvokeOnUI(() =>
                {
                    fillBrush = value;
                });
            }
        }

        public static ColorValue StrokeColor
        {
            get => strokeBrush;
            set
            {
                InvokeOnUI(() =>
                {
                    strokeBrush = value;
                });
            }
        }

        public static ColorValue Color
        {
            get => fillBrush;
            set
            {
                InvokeOnUI(() =>
                {
                    fillBrush = value;
                    strokeBrush = value;
                });
            }
        }
    }
}
