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
            dispatcher = Dispatcher.CurrentDispatcher;
        }

        private static void InvokeOnUI(Action action)
        {
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
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
                    Stroke = currentBrush,
                    Fill = currentBrush,
                    StrokeThickness = 2
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

                var rectangle = new System.Windows.Shapes.Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = currentBrush,
                    Fill = currentBrush,
                    StrokeThickness = 2
                };

                Canvas.SetLeft(rectangle, x);
                Canvas.SetTop(rectangle, y);

                canvas.Children.Add(rectangle);
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
                    FontSize = currentFontSize
                };

                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);

                canvas.Children.Add(textBlock);
            });
        }
    }
}