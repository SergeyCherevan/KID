using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID
{
    public static partial class Graphics
    {
        public static void SetFont(string fontName, double fontSize)
        {
            InvokeOnUI(() =>
            {
                currentFont = new Typeface(fontName);
                currentFontSize = fontSize;
            });
        }

        public static TextBlock? Text(double x, double y, string text)
        {
            if (text == null)
                return null;
            
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                var textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = fillBrush,
                    FontFamily = currentFont?.FontFamily,
                    FontSize = currentFontSize,
                    FontWeight = currentFont?.Weight ?? FontWeights.Normal,
                    FontStyle = currentFont?.Style ?? FontStyles.Normal
                };
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                Canvas.Children.Add(textBlock);

                return textBlock;
            });
        }
        public static TextBlock? Text(Point position, string text)
        {
            return Text(position.X, position.Y, text);
        }

        public static TextBlock? SetText(this TextBlock textBlock, string text)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                textBlock.Text = text;
                return textBlock;
            });
        }
    }
}