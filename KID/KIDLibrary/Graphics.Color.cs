using System.Windows.Media;

namespace KID
{
    // Вспомогательная структура для работы с цветами в разных форматах
    public struct ColorValue
    {
        private enum ColorType
        {
            String,
            Rgb,
            Int,
            Brush
        }

        private ColorType type;
        private string colorName;
        private (byte r, byte g, byte b)? rgb;
        private int? rgbInt;
        private Brush brush;

        private ColorValue(ColorType type, string colorName = null, (byte r, byte g, byte b)? rgb = null, int? rgbInt = null, Brush brush = null)
        {
            this.type = type;
            this.colorName = colorName;
            this.rgb = rgb;
            this.rgbInt = rgbInt;
            this.brush = brush;
        }

        public static implicit operator ColorValue(string colorName)
        {
            return new ColorValue(ColorType.String, colorName: colorName);
        }

        public static implicit operator ColorValue((byte r, byte g, byte b) rgb)
        {
            return new ColorValue(ColorType.Rgb, rgb: rgb);
        }

        public static implicit operator ColorValue(int rgb)
        {
            return new ColorValue(ColorType.Int, rgbInt: rgb);
        }

        public static implicit operator ColorValue(Brush brush)
        {
            return new ColorValue(ColorType.Brush, brush: brush);
        }

        // Создаёт Brush в UI потоке
        internal Brush CreateBrush()
        {
            switch (type)
            {
                case ColorType.String:
                    try
                    {
                        var converter = new BrushConverter();
                        return (Brush)converter.ConvertFromString(colorName) ?? Brushes.Black;
                    }
                    catch
                    {
                        return Brushes.Black;
                    }

                case ColorType.Rgb:
                    if (rgb.HasValue)
                    {
                        return new SolidColorBrush(Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b));
                    }
                    return Brushes.Black;

                case ColorType.Int:
                    if (rgbInt.HasValue)
                    {
                        byte red = (byte)((rgbInt.Value >> 16) & 0xFF);
                        byte green = (byte)((rgbInt.Value >> 8) & 0xFF);
                        byte blue = (byte)(rgbInt.Value & 0xFF);
                        return new SolidColorBrush(Color.FromRgb(red, green, blue));
                    }
                    return Brushes.Black;

                case ColorType.Brush:
                    return brush ?? Brushes.Black;

                default:
                    return Brushes.Black;
            }
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
                    fillBrush = value.CreateBrush();
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
                    strokeBrush = value.CreateBrush();
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
                    var brush = value.CreateBrush();
                    fillBrush = brush;
                    strokeBrush = brush;
                });
            }
        }
    }
}
