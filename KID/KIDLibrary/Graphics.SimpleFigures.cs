using System.Windows.Controls;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        public static void Circle(double x, double y, double radius)
        {
            InvokeOnUI(() =>
            {
                if (canvas == null) return;
                var ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = fillBrush,
                    Stroke = strokeBrush
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
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            });
        }
    }
}