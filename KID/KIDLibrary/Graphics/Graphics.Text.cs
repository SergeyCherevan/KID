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

        public static void Text(double x, double y, string text)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = fillBrush,
                    FontFamily = currentFont.FontFamily,
                    FontSize = currentFontSize,
                    FontWeight = currentFont.Weight,
                    FontStyle = currentFont.Style
                };
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                Canvas.Children.Add(textBlock);
            });
        }
    }
}