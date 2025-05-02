using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Threading;

namespace KID
{
    public static class Graphics
    {
        private static Canvas canvas;
        private static Brush currentBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;
        private static Dispatcher dispatcher;

        public static void Init(Canvas targetCanvas)
        {
            canvas = targetCanvas;
            dispatcher = Application.Current.Dispatcher;
        }

        private static void InvokeOnUI(Action action)
        {
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
        }

        public static void SetColor(string colorName)
        {
            InvokeOnUI(() =>
            {
                try
                {
                    var converter = new BrushConverter();
                    currentBrush = (Brush)converter.ConvertFromString(colorName);
                }
                catch
                {
                    currentBrush = Brushes.Black;
                }
            });
        }

        public static void SetFont(string fontName, double fontSize)
        {
            InvokeOnUI(() =>
            {
                currentFont = new Typeface(fontName);
                currentFontSize = fontSize;
            });
        }

        public static void Circle(double x, double y, double radius)
        {
            InvokeOnUI(() =>
            {
                if (canvas == null) return;
                var ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = currentBrush,
                    Stroke = currentBrush
                };
                Canvas.SetLeft(ellipse, x - radius);
                Canvas.SetTop(ellipse, y - radius);
                canvas.Children.Add(ellipse);
            });
        }

        public static void Rectangle(double x, double y, double width, double height)
        {
            InvokeOnUI(() =>
            {
                if (canvas == null) return;
                var rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = currentBrush,
                    Stroke = currentBrush
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            });
        }

        public static void Text(double x, double y, string text)
        {
            InvokeOnUI(() =>
            {
                if (canvas == null) return;
                var textBlock = new TextBlock
                {
                    Text = text,
                    Foreground = currentBrush,
                    FontFamily = currentFont.FontFamily,
                    FontSize = currentFontSize,
                    FontWeight = currentFont.Weight,
                    FontStyle = currentFont.Style
                };
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                canvas.Children.Add(textBlock);
            });
        }
    }
}